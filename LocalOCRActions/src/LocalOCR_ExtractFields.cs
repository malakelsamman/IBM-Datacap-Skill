using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace LocalOCRActions
{
    /// <summary>
    /// Datacap custom action: LocalOCR_ExtractFields
    /// Calls the local OCR service for the identified page and populates
    /// all corresponding Datacap field objects.
    /// </summary>
    public class LocalOCR_ExtractFields
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
                Log("LocalOCR_ExtractFields: Starting execution");

                if (CurrentDCO == null)
                {
                    Log("LocalOCR_ExtractFields: CurrentDCO is null");
                    return 1;
                }

                if (CurrentDCO.ObjectType() != 2)
                {
                    Log("LocalOCR_ExtractFields: Target object is not a page (ObjectType=" + CurrentDCO.ObjectType() + ") — skipping");
                    return 0;
                }

                string pageType = (string)CurrentDCO.Type;
                Log("LocalOCR_ExtractFields: Current page type = '" + pageType + "'");

                // Determine target OCR form identifier from page type
                string formId = ResolveFormId(pageType, parameters);
                if (string.IsNullOrEmpty(formId))
                {
                    Log("LocalOCR_ExtractFields: Page type '" + pageType + "' is not supported by Local OCR — skipping extraction");
                    return 0;
                }

                string imgPath = LocalOCRCommon.ResolveImagePath(CurrentDCO, RRLog);
                if (string.IsNullOrEmpty(imgPath) || !File.Exists(imgPath))
                {
                    Log("LocalOCR_ExtractFields: Page image file not found: " + (imgPath ?? "null"));
                    LocalOCRCommon.MarkPageVerification(CurrentDCO, 1, RRLog);
                    return 1;
                }

                Log("LocalOCR_ExtractFields: Extracting fields for form '" + formId + "' from " + imgPath);

                // Read image and encode to Base64
                byte[] imgBytes = File.ReadAllBytes(imgPath);
                string imgBase64 = Convert.ToBase64String(imgBytes);
                string fileName = Path.GetFileName(imgPath);

                // Build request
                var serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;

                var reqObj = new Dictionary<string, object>
                {
                    { "form", formId },
                    { "file_name", fileName },
                    { "image_base64", imgBase64 }
                };
                string reqJson = serializer.Serialize(reqObj);

                // Resolve service endpoint
                string baseUrl = LocalOCRCommon.ResolveBaseUrl(parameters, CurrentDCO);
                string endpointUrl = baseUrl + "/recognize";

                Log("LocalOCR_ExtractFields: Calling local OCR service at " + endpointUrl);

                string respJson;
                try
                {
                    respJson = LocalOCRCommon.PostJson(endpointUrl, reqJson, 300, RRLog);
                }
                catch (Exception httpEx)
                {
                    Log("LocalOCR_ExtractFields: HTTP error calling /recognize: " + httpEx.Message);
                    LocalOCRCommon.MarkPageVerification(CurrentDCO, 1, RRLog);
                    return 1;
                }

                if (string.IsNullOrEmpty(respJson))
                {
                    Log("LocalOCR_ExtractFields: Empty response from /recognize");
                    LocalOCRCommon.MarkPageVerification(CurrentDCO, 1, RRLog);
                    return 1;
                }

                // Parse recognition response
                var resp = serializer.Deserialize<Dictionary<string, object>>(respJson);
                if (resp == null || !resp.ContainsKey("ok") || !Convert.ToBoolean(resp["ok"]))
                {
                    string err = resp != null && resp.ContainsKey("error") ? Convert.ToString(resp["error"]) : "Unknown error";
                    Log("LocalOCR_ExtractFields: /recognize returned failure: " + err);
                    LocalOCRCommon.MarkPageVerification(CurrentDCO, 1, RRLog);
                    return 1;
                }

                var fieldsDict = resp.ContainsKey("fields") ? resp["fields"] as Dictionary<string, object> : null;
                if (fieldsDict == null)
                {
                    Log("LocalOCR_ExtractFields: No 'fields' block returned in response");
                    return 0;
                }

                // Populate Datacap fields according to form type
                PopulateFieldsForForm(pageType, fieldsDict);

                Log("LocalOCR_ExtractFields: Field extraction and mapping completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                Log("LocalOCR_ExtractFields ERROR: " + ex.ToString());
                LocalOCRCommon.MarkPageVerification(CurrentDCO, 1, RRLog);
                return 99;
            }
        }

        private string ResolveFormId(string pageType, string parameters)
        {
            if (!string.IsNullOrEmpty(parameters))
            {
                var kv = LocalOCRCommon.ParseKeyValuePairs(parameters);
                if (kv.ContainsKey("form")) return kv["form"];
            }

            switch (pageType)
            {
                case LocalOCRCommon.TypeSignaturePage:
                    return LocalOCRCommon.FormSignature;

                case LocalOCRCommon.TypeNpaPage:
                    return LocalOCRCommon.FormNpa;

                case LocalOCRCommon.TypeNatIdPage:
                    return LocalOCRCommon.FormNatId;

                default:
                    return null;
            }
        }

        private void PopulateFieldsForForm(string pageType, Dictionary<string, object> fields)
        {
            switch (pageType)
            {
                case LocalOCRCommon.TypeSignaturePage:
                    PopulateSignatureForm(fields);
                    break;

                case LocalOCRCommon.TypeNpaPage:
                    PopulateNpaForm(fields);
                    break;

                case LocalOCRCommon.TypeNatIdPage:
                    PopulateNationalIdForm(fields);
                    break;

                default:
                    Log("LocalOCR_ExtractFields: Unknown page type for population: " + pageType);
                    break;
            }
        }

        private void PopulateSignatureForm(Dictionary<string, object> fields)
        {
            // 1. AccountHolderName
            if (fields.ContainsKey("account_holder_name"))
            {
                var ahObj = fields["account_holder_name"] as Dictionary<string, object>;
                if (ahObj != null)
                {
                    string nameVal = ahObj.ContainsKey("value") ? Convert.ToString(ahObj["value"]) : string.Empty;
                    bool review = ahObj.ContainsKey("review_required") && Convert.ToBoolean(ahObj["review_required"]);

                    LocalOCRCommon.SetFieldValue(CurrentDCO, "AccountHolderName", nameVal, RRLog);
                    if (review)
                    {
                        LocalOCRCommon.SetFieldVerification(CurrentDCO, "AccountHolderName", 1, RRLog);
                    }
                }
            }

            // 2. AccountNumber (preserve all 16 positions)
            if (fields.ContainsKey("account_number"))
            {
                var anObj = fields["account_number"] as Dictionary<string, object>;
                if (anObj != null)
                {
                    string acctVal = string.Empty;
                    if (anObj.ContainsKey("positioned_arabic_value") && !string.IsNullOrEmpty(Convert.ToString(anObj["positioned_arabic_value"])))
                        acctVal = Convert.ToString(anObj["positioned_arabic_value"]);
                    else if (anObj.ContainsKey("positioned_value") && !string.IsNullOrEmpty(Convert.ToString(anObj["positioned_value"])))
                        acctVal = Convert.ToString(anObj["positioned_value"]);
                    else if (anObj.ContainsKey("value") && !string.IsNullOrEmpty(Convert.ToString(anObj["value"])))
                        acctVal = Convert.ToString(anObj["value"]);

                    bool review = anObj.ContainsKey("review_required") && Convert.ToBoolean(anObj["review_required"]);

                    // Check for missing positions or question mark placeholders
                    var missingList = anObj.ContainsKey("missing_positions") ? anObj["missing_positions"] as ArrayList : null;
                    if (missingList != null && missingList.Count > 0)
                    {
                        review = true;
                        Log("LocalOCR_ExtractFields: AccountNumber has " + missingList.Count + " missing position(s)");
                    }

                    if (acctVal.Contains("؟") || acctVal.Contains("?"))
                    {
                        review = true;
                        Log("LocalOCR_ExtractFields: AccountNumber contains unresolved digit placeholder");
                    }

                    LocalOCRCommon.SetFieldValue(CurrentDCO, "AccountNumber", acctVal, RRLog);
                    if (review)
                    {
                        LocalOCRCommon.SetFieldVerification(CurrentDCO, "AccountNumber", 1, RRLog);
                    }
                }
            }
        }

        private void PopulateNpaForm(Dictionary<string, object> fields)
        {
            // 1. EmployerName2
            if (fields.ContainsKey("employer_name"))
            {
                var empObj = fields["employer_name"] as Dictionary<string, object>;
                if (empObj != null)
                {
                    string empVal = empObj.ContainsKey("value") ? Convert.ToString(empObj["value"]) : string.Empty;
                    bool review = empObj.ContainsKey("review_required") && Convert.ToBoolean(empObj["review_required"]);

                    LocalOCRCommon.SetFieldValue(CurrentDCO, "EmployerName2", empVal, RRLog);
                    if (review)
                    {
                        LocalOCRCommon.SetFieldVerification(CurrentDCO, "EmployerName2", 1, RRLog);
                    }
                }
            }

            // 2. AvrgMonthlyIncome
            if (fields.ContainsKey("average_monthly_income"))
            {
                var incObj = fields["average_monthly_income"] as Dictionary<string, object>;
                if (incObj != null)
                {
                    string incVal = incObj.ContainsKey("value") ? Convert.ToString(incObj["value"]) : string.Empty;
                    bool review = incObj.ContainsKey("review_required") && Convert.ToBoolean(incObj["review_required"]);

                    LocalOCRCommon.SetFieldValue(CurrentDCO, "AvrgMonthlyIncome", incVal, RRLog);
                    if (review)
                    {
                        LocalOCRCommon.SetFieldVerification(CurrentDCO, "AvrgMonthlyIncome", 1, RRLog);
                    }
                }
            }
        }

        private void PopulateNationalIdForm(Dictionary<string, object> fields)
        {
            // Experimental form fields
            SetGenericField(fields, "full_name_arabic", "FullNameArabic");
            SetGenericField(fields, "family_name_arabic", "FamilyNameArabic");
            SetGenericField(fields, "address_arabic", "AddressArabic");
            SetGenericField(fields, "national_id_number", "NationalIdNumber");
            SetGenericField(fields, "employer_name", "EmployerName");

            // National ID is experimental: mark page for verification
            LocalOCRCommon.MarkPageVerification(CurrentDCO, 1, RRLog);
        }

        private void SetGenericField(Dictionary<string, object> fields, string jsonKey, string dcoFieldName)
        {
            if (fields.ContainsKey(jsonKey))
            {
                var obj = fields[jsonKey] as Dictionary<string, object>;
                if (obj != null)
                {
                    string val = obj.ContainsKey("value") ? Convert.ToString(obj["value"]) : string.Empty;
                    bool review = obj.ContainsKey("review_required") && Convert.ToBoolean(obj["review_required"]);

                    LocalOCRCommon.SetFieldValue(CurrentDCO, dcoFieldName, val, RRLog);
                    if (review)
                    {
                        LocalOCRCommon.SetFieldVerification(CurrentDCO, dcoFieldName, 1, RRLog);
                    }
                }
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
                    Log("LocalOCR_ExtractFields: Assembly " + asm.Location + " v" + asm.GetName().Version);
                    _versionLogged = true;
                }
            }
            catch { }
        }

        ~LocalOCR_ExtractFields() { DatacapRRCleanupTime = true; }
    }
}
