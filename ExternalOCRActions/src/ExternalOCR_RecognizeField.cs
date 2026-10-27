using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace ExternalOCRActions
{
    /// <summary>
    /// Datacap custom action: ExternalOCR_RecognizeField
    /// Performs field-level recognition by cropping ONLY the current field zone
    /// and sending it to the external Vendor OCR API.
    /// </summary>
    public class ExternalOCR_RecognizeField
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
            string fieldId = string.Empty;
            string pageType = string.Empty;
            string sourceFileName = string.Empty;
            int cropW = 0;
            int cropH = 0;
            string route = "Vendor_OCR_Handwritten";
            string endpointPath = "/recognize_base64";
            string lang = "Arabic/Latest";
            string docType = "handwritten";
            string httpStatus = "N/A";
            string parseStatus = "Failed";
            bool reviewReq = false;

            try
            {
                if (CurrentDCO == null)
                {
                    Log("ExternalOCR_RecognizeField: CurrentDCO is null");
                    return 1;
                }

                // Check object level (Must be Field, ObjectType == 3, or Page ObjectType == 2 if called with field param)
                dynamic fieldDco = null;
                dynamic pageDco = null;

                if (CurrentDCO.ObjectType() == 3) // Field level
                {
                    fieldDco = CurrentDCO;
                    pageDco = CurrentDCO.Parent;
                    fieldId = (string)fieldDco.Type;
                    if (string.IsNullOrEmpty(fieldId)) fieldId = (string)fieldDco.ID;
                }
                else if (CurrentDCO.ObjectType() == 2) // Page level with parameter specifying field
                {
                    pageDco = CurrentDCO;
                    var kv = ExternalOCRCommon.ParseKeyValuePairs(parameters);
                    if (kv.ContainsKey("field"))
                    {
                        fieldId = kv["field"];
                        try { fieldDco = pageDco.FindChild(fieldId); } catch { }
                    }
                }
                else
                {
                    Log("ExternalOCR_RecognizeField: Not at field or page level (ObjectType=" + CurrentDCO.ObjectType() + ") — skipping");
                    return 0;
                }

                if (pageDco != null)
                {
                    pageType = (string)pageDco.Type;
                }

                if (fieldDco == null || string.IsNullOrEmpty(fieldId))
                {
                    Log("ExternalOCR_RecognizeField: Target field could not be resolved");
                    return 1;
                }

                // 1. Resolve source image path
                string imagePath = ExternalOCRCommon.ResolveImagePath(pageDco, RRLog);
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                {
                    Log(string.Format("ExternalOCR_RecognizeField | PageType='{0}' | FieldID='{1}' | SourceFile='null' | Crop='0x0' | Route='{2}' | Endpoint='{3}' | Lang='{4}' | DocType='{5}' | HTTP='N/A' | ParseStatus='Failed' | VerificationRequired=True | Error='Image file not found'",
                        pageType, fieldId, route, endpointPath, lang, docType));
                    SetFieldVerification(fieldDco, 1);
                    return 1;
                }

                sourceFileName = Path.GetFileName(imagePath);

                // 2. Load settings and field configuration
                var settings = ExternalOCRCommon.LoadSettings(parameters);
                lang = settings.Language;
                docType = settings.DocumentType;

                var fallbackZone = ExternalOCRCommon.GetFallbackFieldZone(pageType, fieldId);
                if (fallbackZone != null)
                {
                    if (!string.IsNullOrEmpty(fallbackZone.DocumentType)) docType = fallbackZone.DocumentType;
                    if (!string.IsNullOrEmpty(fallbackZone.Language)) lang = fallbackZone.Language;
                    if (fallbackZone.EnableLineSegmentation.HasValue) settings.EnableLineSegmentation = fallbackZone.EnableLineSegmentation.Value;
                }

                // Check parameter overrides
                if (!string.IsNullOrEmpty(parameters))
                {
                    var kv = ExternalOCRCommon.ParseKeyValuePairs(parameters);
                    if (kv.ContainsKey("doc_type")) docType = kv["doc_type"];
                    if (kv.ContainsKey("document_type")) docType = kv["document_type"];
                    if (kv.ContainsKey("language")) lang = kv["language"];
                    if (kv.ContainsKey("route")) route = kv["route"];
                    bool segOverride;
                    if (kv.ContainsKey("enable_line_segmentation") && bool.TryParse(kv["enable_line_segmentation"], out segOverride))
                        settings.EnableLineSegmentation = segOverride;
                }

                if (docType == "typewritten")
                {
                    route = "Vendor_OCR_Typewritten";
                }
                else
                {
                    route = "Vendor_OCR_Handwritten";
                }

                // Load image dimensions to resolve pixel zone
                int imgW, imgH;
                using (var tmpImg = Image.FromFile(imagePath))
                {
                    imgW = tmpImg.Width;
                    imgH = tmpImg.Height;
                }

                // 3. Resolve field zone rectangle
                Rectangle zoneRect = ExternalOCRCommon.ResolveFieldPixelZone(fieldDco, pageType, fieldId, imgW, imgH, settings.PaddingPixels, RRLog);

                // 4. Crop field image
                byte[] cropBytes = ExternalOCRCommon.CropFieldImage(imagePath, zoneRect, out cropW, out cropH);

                // 5. Call Vendor OCR API with cropped field
                var ocrResult = ExternalOCRCommon.RecognizeCroppedField(cropBytes, cropW, cropH, settings, docType, lang, RRLog);

                httpStatus = ocrResult.HttpStatusCode.ToString();
                reviewReq = ocrResult.ReviewRequired;

                if (!ocrResult.Success || ocrResult.HttpStatusCode != 200)
                {
                    parseStatus = "HTTP_Error_" + ocrResult.HttpStatusCode;
                    SetFieldVerification(fieldDco, 1);

                    Log(string.Format("ExternalOCR_RecognizeField | PageType='{0}' | FieldID='{1}' | SourceFile='{2}' | Crop='{3}x{4}' | Route='{5}' | Endpoint='{6}' | Lang='{7}' | DocType='{8}' | HTTP='{9}' | ParseStatus='{10}' | VerificationRequired={11} | Error='{12}'",
                        pageType, fieldId, sourceFileName, cropW, cropH, route, endpointPath, lang, docType, httpStatus, parseStatus, reviewReq, ocrResult.ErrorMessage));

                    return 1;
                }

                // 6. Set recognized text on Datacap field
                string recognizedText = ocrResult.Text ?? string.Empty;
                if (fieldId.Equals("AccountNumber", StringComparison.OrdinalIgnoreCase) ||
                    fieldId.Equals("NationalIdNumber", StringComparison.OrdinalIgnoreCase))
                {
                    // If numbers have space-separated clusters read in RTL order, reverse cluster order to preserve LTR digits
                    string[] chunks = recognizedText.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (chunks.Length > 1)
                    {
                        Array.Reverse(chunks);
                        recognizedText = string.Join(" ", chunks);
                    }
                }
                fieldDco.Text = recognizedText;

                if (reviewReq)
                {
                    SetFieldVerification(fieldDco, 1);
                    parseStatus = "Review_Required";
                }
                else
                {
                    parseStatus = "Success";
                }

                // Standard structured logging
                Log(string.Format("ExternalOCR_RecognizeField | PageType='{0}' | FieldID='{1}' | SourceFile='{2}' | Crop='{3}x{4}' | Route='{5}' | Endpoint='{6}' | Lang='{7}' | DocType='{8}' | HTTP='{9}' | ParseStatus='{10}' | VerificationRequired={11}",
                    pageType, fieldId, sourceFileName, cropW, cropH, route, endpointPath, lang, docType, httpStatus, parseStatus, reviewReq));

                return 0;
            }
            catch (Exception ex)
            {
                Log(string.Format("ExternalOCR_RecognizeField ERROR: {0} | FieldID='{1}'", ex.Message, fieldId));
                if (CurrentDCO != null)
                {
                    try { CurrentDCO.VerificationStatus = 1; } catch { }
                }
                return 1;
            }
        }

        private void SetFieldVerification(dynamic fieldDco, int status)
        {
            try
            {
                if (fieldDco != null)
                {
                    fieldDco.VerificationStatus = status;
                }
            }
            catch { }
        }

        private void Log(string msg)
        {
            ExternalOCRCommon.Log(RRLog, msg);
        }

        private void LogVersion()
        {
            try
            {
                if (!_versionLogged && RRLog != null)
                {
                    var asm = Assembly.GetExecutingAssembly();
                    Log("ExternalOCR_RecognizeField: Assembly " + asm.Location + " v" + asm.GetName().Version);
                    _versionLogged = true;
                }
            }
            catch { }
        }

        ~ExternalOCR_RecognizeField() { DatacapRRCleanupTime = true; }
    }
}
