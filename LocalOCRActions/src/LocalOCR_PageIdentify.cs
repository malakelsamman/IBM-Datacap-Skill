using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace LocalOCRActions
{
    /// <summary>
    /// Datacap custom action: LocalOCR_PageIdentify
    /// Classifies the current page using the local OCR service and establishes
    /// the Datacap document/page hierarchy and field objects.
    /// </summary>
    public class LocalOCR_PageIdentify
    {
        #region Datacap required properties

        protected dynamic RRLog      = null;
        protected dynamic RRState    = null;
        protected dynamic BatchPilot = null;
        protected dynamic DCO        = null;
        protected dynamic CurrentDCO = null;

        public dynamic DatacapRRLog
        {
            set { this.RRLog = value; LogVersion(); GC.Collect(); GC.WaitForPendingFinalizers(); }
            get { return this.RRLog; }
        }
        public dynamic DatacapRRState
        {
            set { this.RRState = value; GC.Collect(); GC.WaitForPendingFinalizers(); }
            get { return this.RRState; }
        }
        public dynamic DatacapRRBatchPilot
        {
            set { this.BatchPilot = value; GC.Collect(); GC.WaitForPendingFinalizers(); }
            get { return this.BatchPilot; }
        }
        public dynamic DatacapRRDCO
        {
            set { this.DCO = value; GC.Collect(); GC.WaitForPendingFinalizers(); }
            get { return this.DCO; }
        }
        public dynamic DatacapRRCurrentDCO
        {
            set { this.CurrentDCO = value; GC.Collect(); GC.WaitForPendingFinalizers(); }
            get { return this.CurrentDCO; }
        }
        public bool DatacapRRCleanupTime
        {
            set
            {
                if (value)
                {
                    RRLog = null; RRState = null; BatchPilot = null;
                    DCO = null; CurrentDCO = null;
                    GC.Collect(); GC.WaitForPendingFinalizers();
                }
            }
        }

        #endregion

        private bool _versionLogged = false;

        public int Execute(string parameters)
        {
            try
            {
                Log("LocalOCR_PageIdentify: Starting execution");

                if (CurrentDCO == null)
                {
                    Log("LocalOCR_PageIdentify: CurrentDCO is null");
                    return 1;
                }

                if (CurrentDCO.ObjectType() != 2)
                {
                    Log("LocalOCR_PageIdentify: Target object is not a page (ObjectType=" + CurrentDCO.ObjectType() + ") — skipping");
                    return 0;
                }

                string imgPath = LocalOCRCommon.ResolveImagePath(CurrentDCO, RRLog);
                if (string.IsNullOrEmpty(imgPath) || !File.Exists(imgPath))
                {
                    Log("LocalOCR_PageIdentify: Page image file not found: " + (imgPath ?? "null"));
                    LocalOCRCommon.MarkPageVerification(CurrentDCO, 1, RRLog);
                    return 1;
                }

                Log("LocalOCR_PageIdentify: Using page image " + imgPath);

                // Read image and encode to Base64 in memory
                byte[] imgBytes = File.ReadAllBytes(imgPath);
                string imgBase64 = Convert.ToBase64String(imgBytes);
                string fileName = Path.GetFileName(imgPath);

                // Prepare request payload
                var serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;

                var reqObj = new Dictionary<string, object>
                {
                    { "file_name", fileName },
                    { "image_base64", imgBase64 }
                };
                string reqJson = serializer.Serialize(reqObj);

                // Resolve service endpoint
                string baseUrl = LocalOCRCommon.ResolveBaseUrl(parameters, CurrentDCO);
                string endpointUrl = baseUrl + "/identify";

                Log("LocalOCR_PageIdentify: Calling local OCR service at " + endpointUrl);

                string respJson;
                try
                {
                    respJson = LocalOCRCommon.PostJson(endpointUrl, reqJson, 300, RRLog);
                }
                catch (Exception httpEx)
                {
                    Log("LocalOCR_PageIdentify: HTTP error calling /identify: " + httpEx.Message);
                    LocalOCRCommon.MarkPageVerification(CurrentDCO, 1, RRLog);
                    return 1;
                }

                if (string.IsNullOrEmpty(respJson))
                {
                    Log("LocalOCR_PageIdentify: Empty response from /identify");
                    LocalOCRCommon.MarkPageVerification(CurrentDCO, 1, RRLog);
                    return 1;
                }

                // Parse identification response
                var resp = serializer.Deserialize<Dictionary<string, object>>(respJson);
                if (resp == null || !resp.ContainsKey("ok") || !Convert.ToBoolean(resp["ok"]))
                {
                    string err = resp != null && resp.ContainsKey("error") ? Convert.ToString(resp["error"]) : "Unknown error";
                    Log("LocalOCR_PageIdentify: /identify returned failure: " + err);
                    LocalOCRCommon.MarkPageVerification(CurrentDCO, 1, RRLog);
                    return 1;
                }

                string formId   = resp.ContainsKey("form") ? Convert.ToString(resp["form"]) : string.Empty;
                string pageType = resp.ContainsKey("page_type") ? Convert.ToString(resp["page_type"]) : string.Empty;
                string docType  = resp.ContainsKey("document_type") ? Convert.ToString(resp["document_type"]) : string.Empty;
                double confidence = resp.ContainsKey("confidence") ? Convert.ToDouble(resp["confidence"]) : 0.0;
                bool reviewReq  = resp.ContainsKey("review_required") && Convert.ToBoolean(resp["review_required"]);
                bool fallback   = resp.ContainsKey("fallback_used") && Convert.ToBoolean(resp["fallback_used"]);

                Log(string.Format("LocalOCR_PageIdentify: Identified page_type='{0}', doc_type='{1}', form='{2}', confidence={3:F2}, review_required={4}, fallback={5}",
                    pageType, docType, formId, confidence, reviewReq, fallback));

                if (string.IsNullOrEmpty(pageType))
                {
                    Log("LocalOCR_PageIdentify: No valid page_type in response — leaving unchanged");
                    LocalOCRCommon.MarkPageVerification(CurrentDCO, 1, RRLog);
                    return 1;
                }

                // Step 1: Set CurrentDCO.Type
                string oldType = (string)CurrentDCO.Type;
                CurrentDCO.Type = pageType;
                Log("LocalOCR_PageIdentify: Updated CurrentDCO.Type from '" + oldType + "' to '" + pageType + "'");

                // Step 2: Create fields on the page
                try
                {
                    CurrentDCO.CreateFields();
                    Log("LocalOCR_PageIdentify: CreateFields() executed successfully for " + pageType);
                }
                catch (Exception cfe)
                {
                    Log("LocalOCR_PageIdentify: CreateFields() warning: " + cfe.Message);
                }

                // Step 3: Update parent document type if parent exists
                try
                {
                    dynamic parent = CurrentDCO.Parent;
                    if (parent != null && parent.ObjectType() == 1)
                    {
                        if (!string.IsNullOrEmpty(docType))
                        {
                            parent.Type = docType;
                            Log("LocalOCR_PageIdentify: Set parent document type = '" + docType + "'");
                        }
                    }
                }
                catch { }

                // Step 4: Handle verification routing if low confidence or review flag is set
                if (reviewReq || fallback || confidence < 0.80)
                {
                    Log("LocalOCR_PageIdentify: Flagging page for verification (confidence=" + confidence + ", review_required=" + reviewReq + ")");
                    LocalOCRCommon.MarkPageVerification(CurrentDCO, 1, RRLog);
                }

                Log("LocalOCR_PageIdentify: Completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                Log("LocalOCR_PageIdentify ERROR: " + ex.ToString());
                LocalOCRCommon.MarkPageVerification(CurrentDCO, 1, RRLog);
                return 99;
            }
        }

        private void Log(string msg)
        {
            LocalOCRCommon.Log(RRLog, msg);
        }

        private void LogVersion()
        {
            try
            {
                if (!_versionLogged && RRLog != null)
                {
                    var asm = Assembly.GetExecutingAssembly();
                    Log("LocalOCR_PageIdentify: Assembly " + asm.Location + " v" + asm.GetName().Version);
                    _versionLogged = true;
                }
            }
            catch { }
        }

        ~LocalOCR_PageIdentify() { DatacapRRCleanupTime = true; }
    }
}
