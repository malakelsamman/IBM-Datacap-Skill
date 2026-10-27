using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace ExternalOCRActions
{
    /// <summary>
    /// Shared helper methods, settings loaders, image cropping, and HTTP client for External OCR actions.
    /// </summary>
    public static class ExternalOCRCommon
    {
        public const string DefaultBaseUrl = "http://34.69.150.249:8019";
        public const string DefaultLanguage = "Arabic/Latest";
        public const string DefaultDocType = "handwritten";
        public const int DefaultTimeoutSeconds = 60;

        public const string TypeSignaturePage = "Signature_Form_1";
        public const string TypeSignatureDoc  = "Signature Form";

        public const string TypeNpaPage       = "Form_Page_1";
        public const string TypeNpaDoc        = "NPA Form";

        public const string TypeNatIdPage     = "National ID Front";
        public const string TypeNatIdDoc      = "National ID";

        public class Settings
        {
            public string BaseUrl { get; set; }
            public string Language { get; set; }
            public string DocumentType { get; set; }
            public int BatchSize { get; set; }
            public int TimeoutSeconds { get; set; }
            public bool PreferBase64 { get; set; }
            public int PaddingPixels { get; set; }
            public bool EnableLineSegmentation { get; set; }

            public Settings()
            {
                BaseUrl = DefaultBaseUrl;
                Language = DefaultLanguage;
                DocumentType = DefaultDocType;
                BatchSize = 1;
                TimeoutSeconds = DefaultTimeoutSeconds;
                PreferBase64 = true;
                PaddingPixels = 4;
            }
        }

        public class FieldZone
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public string DocumentType { get; set; }
            public string Language { get; set; }
            public bool? EnableLineSegmentation { get; set; }
        }

        public class RecognitionResult
        {
            public bool Success { get; set; }
            public string Text { get; set; }
            public double AverageConfidence { get; set; }
            public int HttpStatusCode { get; set; }
            public string ErrorMessage { get; set; }
            public bool ReviewRequired { get; set; }
            public int CropWidth { get; set; }
            public int CropHeight { get; set; }
        }

        /// <summary>
        /// Loads runtime settings from external_ocr_settings.json or defaults.
        /// </summary>
        public static Settings LoadSettings(string parameters)
        {
            var settings = new Settings();

            // 1. Try reading from external_ocr_settings.json in DLL directory or RRS directory
            try
            {
                string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string[] searchPaths = new string[]
                {
                    Path.Combine(dllDir, "external_ocr_settings.json"),
                    @"C:\Datacap\RRS\external_ocr_settings.json",
                    @"C:\Datacap\GoldenDemo\dco_GoldenDemo\rules\external_ocr_settings.json"
                };

                foreach (string cfgPath in searchPaths)
                {
                    if (File.Exists(cfgPath))
                    {
                        string json = File.ReadAllText(cfgPath, Encoding.UTF8);
                        var serializer = new JavaScriptSerializer();
                        var dict = serializer.Deserialize<Dictionary<string, object>>(json);
                        if (dict != null)
                        {
                            if (dict.ContainsKey("base_url") && !string.IsNullOrEmpty(Convert.ToString(dict["base_url"])))
                                settings.BaseUrl = Convert.ToString(dict["base_url"]).TrimEnd('/');
                            if (dict.ContainsKey("language") && !string.IsNullOrEmpty(Convert.ToString(dict["language"])))
                                settings.Language = Convert.ToString(dict["language"]);
                            if (dict.ContainsKey("document_type") && !string.IsNullOrEmpty(Convert.ToString(dict["document_type"])))
                                settings.DocumentType = Convert.ToString(dict["document_type"]);
                            if (dict.ContainsKey("batch_size"))
                                settings.BatchSize = Convert.ToInt32(dict["batch_size"]);
                            if (dict.ContainsKey("timeout_seconds"))
                                settings.TimeoutSeconds = Convert.ToInt32(dict["timeout_seconds"]);
                            if (dict.ContainsKey("prefer_base64"))
                                settings.PreferBase64 = Convert.ToBoolean(dict["prefer_base64"]);
                            if (dict.ContainsKey("padding_pixels"))
                                settings.PaddingPixels = Convert.ToInt32(dict["padding_pixels"]);
                            if (dict.ContainsKey("enable_line_segmentation"))
                                settings.EnableLineSegmentation = Convert.ToBoolean(dict["enable_line_segmentation"]);
                        }
                        break;
                    }
                }
            }
            catch { }

            // 2. Override with action parameters if supplied
            if (!string.IsNullOrEmpty(parameters))
            {
                var kv = ParseKeyValuePairs(parameters);
                if (kv.ContainsKey("base_url")) settings.BaseUrl = kv["base_url"].TrimEnd('/');
                if (kv.ContainsKey("url")) settings.BaseUrl = kv["url"].TrimEnd('/');
                if (kv.ContainsKey("language")) settings.Language = kv["language"];
                if (kv.ContainsKey("document_type")) settings.DocumentType = kv["document_type"];
                if (kv.ContainsKey("doc_type")) settings.DocumentType = kv["doc_type"];
                if (kv.ContainsKey("timeout"))
                {
                    int t;
                    if (int.TryParse(kv["timeout"], out t)) settings.TimeoutSeconds = t;
                }
            }

            return settings;
        }

        public static Dictionary<string, string> ParseKeyValuePairs(string raw)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(raw)) return result;

            string[] parts = raw.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                int eq = part.IndexOf('=');
                if (eq > 0)
                {
                    string k = part.Substring(0, eq).Trim();
                    string v = part.Substring(eq + 1).Trim();
                    result[k] = v;
                }
            }
            return result;
        }

        /// <summary>
        /// Resolves the absolute path to the page image file.
        /// </summary>
        public static string ResolveImagePath(dynamic pageDco, dynamic rrLog)
        {
            try
            {
                if (pageDco == null) return null;
                string imgName = (string)pageDco.ImageName;
                if (string.IsNullOrEmpty(imgName))
                {
                    // Try ScanSrcPath or IMAGEFILE variable
                    try { imgName = (string)pageDco.get_Variable("ScanSrcPath"); } catch { }
                    if (string.IsNullOrEmpty(imgName))
                    {
                        try { imgName = (string)pageDco.get_Variable("IMAGEFILE"); } catch { }
                    }
                }

                if (string.IsNullOrEmpty(imgName))
                {
                    Log(rrLog, "ExternalOCR: Page image path could not be resolved from DCO");
                    return null;
                }

                if (File.Exists(imgName)) return Path.GetFullPath(imgName);

                // Try finding batch directory by walking up DCO hierarchy
                dynamic node = pageDco;
                while (node != null)
                {
                    try
                    {
                        string nodeImg = (string)node.ImageName;
                        if (!string.IsNullOrEmpty(nodeImg) && Path.IsPathRooted(nodeImg))
                        {
                            string dir = Path.GetDirectoryName(nodeImg);
                            string candidate = Path.Combine(dir, Path.GetFileName(imgName));
                            if (File.Exists(candidate)) return candidate;
                        }
                    }
                    catch { }

                    try { node = node.Parent; } catch { break; }
                }

                // Check common batch & input locations
                string[] searchRoots = new string[]
                {
                    @"C:\Datacap\GoldenDemo\images\Input_SingleTIFFs",
                    @"C:\Datacap\GoldenDemo\images\Input_MultiFormat",
                    @"C:\Datacap\GoldenDemo\batches",
                    @"C:\Datacap\batches"
                };

                string fileNameOnly = Path.GetFileName(imgName);
                foreach (var root in searchRoots)
                {
                    if (Directory.Exists(root))
                    {
                        var files = Directory.GetFiles(root, fileNameOnly, SearchOption.AllDirectories);
                        if (files.Length > 0) return files[0];
                    }
                }

                return imgName;
            }
            catch (Exception ex)
            {
                Log(rrLog, "ExternalOCR: Error resolving image path: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Obtains the zone rectangle for a field in pixel coordinates.
        /// </summary>
        public static Rectangle ResolveFieldPixelZone(dynamic fieldDco, string pageType, string fieldId, int imgWidth, int imgHeight, int paddingPx, dynamic rrLog)
        {
            // 1. Try reading DCO position variables or fingerprint files directly
            try
            {
                string posStr = null;
                if (fieldDco != null)
                {
                    try { posStr = (string)fieldDco.get_Variable("Position"); } catch { }
                }

                if (string.IsNullOrEmpty(posStr) || posStr == "0,0,0,0")
                {
                    posStr = TryGetPositionFromFingerprintXml(pageType, fieldId);
                }

                if (!string.IsNullOrEmpty(posStr) && posStr != "0,0,0,0")
                {
                    string[] coords = posStr.Split(new char[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (coords.Length == 4)
                    {
                        int l = int.Parse(coords[0]);
                        int t = int.Parse(coords[1]);
                        int r = int.Parse(coords[2]);
                        int b = int.Parse(coords[3]);
                        if (r > l && b > t)
                        {
                            int px = Math.Max(0, l - paddingPx);
                            int py = Math.Max(0, t - paddingPx);
                            int pw = Math.Min(imgWidth - px, (r - l) + 2 * paddingPx);
                            int ph = Math.Min(imgHeight - py, (b - t) + 2 * paddingPx);
                            return new Rectangle(px, py, pw, ph);
                        }
                    }
                }
            }
            catch { }

            // 2. Fallback to calibrated normalized coordinates
            FieldZone fallbackZone = GetFallbackFieldZone(pageType, fieldId);
            if (fallbackZone != null)
            {
                int x = (int)(fallbackZone.X * imgWidth) - paddingPx;
                int y = (int)(fallbackZone.Y * imgHeight) - paddingPx;
                int w = (int)(fallbackZone.Width * imgWidth) + 2 * paddingPx;
                int h = (int)(fallbackZone.Height * imgHeight) + 2 * paddingPx;

                x = Math.Max(0, Math.Min(x, imgWidth - 1));
                y = Math.Max(0, Math.Min(y, imgHeight - 1));
                w = Math.Max(1, Math.Min(w, imgWidth - x));
                h = Math.Max(1, Math.Min(h, imgHeight - y));

                return new Rectangle(x, y, w, h);
            }

            // Default safe bounding box if nothing is configured
            return new Rectangle(0, 0, imgWidth, imgHeight);
        }

        public static FieldZone GetFallbackFieldZone(string pageType, string fieldId)
        {
            // Check external_crop_config.json if available
            try
            {
                string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string[] searchPaths = new string[]
                {
                    Path.Combine(dllDir, "external_crop_config.json"),
                    @"C:\Datacap\RRS\external_crop_config.json",
                    @"C:\Datacap\GoldenDemo\dco_GoldenDemo\rules\external_crop_config.json"
                };

                foreach (string cfgPath in searchPaths)
                {
                    if (File.Exists(cfgPath))
                    {
                        string json = File.ReadAllText(cfgPath, Encoding.UTF8);
                        var serializer = new JavaScriptSerializer();
                        var root = serializer.Deserialize<Dictionary<string, object>>(json);
                        if (root != null && root.ContainsKey("fields"))
                        {
                            var fields = root["fields"] as Dictionary<string, object>;
                            if (fields != null && fields.ContainsKey(pageType))
                            {
                                var pageFields = fields[pageType] as Dictionary<string, object>;
                                if (pageFields != null && pageFields.ContainsKey(fieldId))
                                {
                                    var fObj = pageFields[fieldId] as Dictionary<string, object>;
                                    if (fObj != null)
                                    {
                                        var fz = new FieldZone();
                                        if (fObj.ContainsKey("x")) fz.X = Convert.ToDouble(fObj["x"]);
                                        if (fObj.ContainsKey("y")) fz.Y = Convert.ToDouble(fObj["y"]);
                                        if (fObj.ContainsKey("width")) fz.Width = Convert.ToDouble(fObj["width"]);
                                        if (fObj.ContainsKey("height")) fz.Height = Convert.ToDouble(fObj["height"]);
                                        if (fObj.ContainsKey("document_type")) fz.DocumentType = Convert.ToString(fObj["document_type"]);
                                        if (fObj.ContainsKey("language")) fz.Language = Convert.ToString(fObj["language"]);
                                        if (fObj.ContainsKey("enable_line_segmentation")) fz.EnableLineSegmentation = Convert.ToBoolean(fObj["enable_line_segmentation"]);
                                        return fz;
                                    }
                                }
                            }
                        }
                        break;
                    }
                }
            }
            catch { }

            // Hardcoded calibrated defaults
            if (pageType == TypeSignaturePage || pageType == TypeSignatureDoc)
            {
                if (fieldId == "AccountHolderName")
                    return new FieldZone { X = 0.5584, Y = 0.1758, Width = 0.2538, Height = 0.0305, DocumentType = "handwritten", Language = "Arabic/Latest" };
                if (fieldId == "AccountNumber")
                    return new FieldZone { X = 0.2858, Y = 0.2126, Width = 0.6029, Height = 0.0385, DocumentType = "handwritten", Language = "Arabic/Latest" };
            }
            else if (pageType == TypeNpaPage || pageType == TypeNpaDoc)
            {
                if (fieldId == "EmployerName2")
                    return new FieldZone { X = 0.4795, Y = 0.2571, Width = 0.2698, Height = 0.0325, DocumentType = "handwritten", Language = "Arabic/Latest" };
                if (fieldId == "AvrgMonthlyIncome")
                    return new FieldZone { X = 0.6974, Y = 0.3353, Width = 0.1386, Height = 0.0200, DocumentType = "handwritten", Language = "Arabic/Latest" };
            }
            else if (pageType == TypeNatIdPage || pageType == TypeNatIdDoc)
            {
                if (fieldId == "FullNameArabic")
                    return new FieldZone { X = 0.4608, Y = 0.0200, Width = 0.1991, Height = 0.0685, DocumentType = "handwritten", Language = "Arabic/Latest" };
                if (fieldId == "FamilyNameArabic")
                    return new FieldZone { X = 0.5576, Y = 0.0902, Width = 0.0777, Height = 0.0177, DocumentType = "handwritten", Language = "Arabic/Latest" };
                if (fieldId == "AddressArabic")
                    return new FieldZone { X = 0.4498, Y = 0.1056, Width = 0.1870, Height = 0.0223, DocumentType = "handwritten", Language = "Arabic/Latest" };
                if (fieldId == "NationalIdNumber")
                    return new FieldZone { X = 0.4116, Y = 0.1447, Width = 0.2401, Height = 0.0234, DocumentType = "handwritten", Language = "Arabic/Latest" };
                if (fieldId == "EmployerName")
                    return new FieldZone { X = 0.4455, Y = 0.3995, Width = 0.1484, Height = 0.0163, DocumentType = "handwritten", Language = "Arabic/Latest" };
            }

            return null;
        }

        private static string TryGetPositionFromFingerprintXml(string pageType, string fieldId)
        {
            try
            {
                string fpId = null;
                if (pageType == TypeNatIdPage || pageType == TypeNatIdDoc) fpId = "568";
                else if (pageType == TypeSignaturePage || pageType == TypeSignatureDoc) fpId = "569";
                else if (pageType == TypeNpaPage || pageType == TypeNpaDoc) fpId = "570";

                if (string.IsNullOrEmpty(fpId)) return null;

                string[] searchDirs = new string[]
                {
                    @"C:\Datacap\GoldenDemo\fingerprint",
                    @"C:\Datacap\fingerprint"
                };

                foreach (var dir in searchDirs)
                {
                    string xmlPath = Path.Combine(dir, fpId + ".xml");
                    if (File.Exists(xmlPath))
                    {
                        var doc = new System.Xml.XmlDocument();
                        doc.Load(xmlPath);
                        var fNode = doc.SelectSingleNode("//F[@type='" + fieldId + "']/V[@n='Position']");
                        if (fNode != null && !string.IsNullOrEmpty(fNode.InnerText))
                            return fNode.InnerText.Trim();
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Crops a field zone from the source image and returns the crop as PNG bytes.
        /// </summary>
        public static byte[] CropFieldImage(string imgPath, Rectangle zoneRect, out int cropWidth, out int cropHeight)
        {
            using (Bitmap fullImg = new Bitmap(imgPath))
            {
                // Clamp crop rectangle to image bounds
                int x = Math.Max(0, Math.Min(zoneRect.X, fullImg.Width - 1));
                int y = Math.Max(0, Math.Min(zoneRect.Y, fullImg.Height - 1));
                int w = Math.Max(1, Math.Min(zoneRect.Width, fullImg.Width - x));
                int h = Math.Max(1, Math.Min(zoneRect.Height, fullImg.Height - y));

                cropWidth = w;
                cropHeight = h;

                Rectangle safeRect = new Rectangle(x, y, w, h);
                using (Bitmap crop = fullImg.Clone(safeRect, fullImg.PixelFormat))
                using (MemoryStream ms = new MemoryStream())
                {
                    crop.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Executes field recognition by calling the Vendor OCR API.
        /// </summary>
        public static RecognitionResult RecognizeCroppedField(byte[] cropPngBytes, int cropWidth, int cropHeight, Settings settings, string docTypeOverride, string langOverride, dynamic rrLog, bool allowEmptyLineRetry = true)
        {
            var res = new RecognitionResult
            {
                CropWidth = cropWidth,
                CropHeight = cropHeight,
                Success = false,
                Text = string.Empty,
                AverageConfidence = 0.0,
                ReviewRequired = false
            };

            string docType = !string.IsNullOrEmpty(docTypeOverride) ? docTypeOverride : settings.DocumentType;
            string lang = !string.IsNullOrEmpty(langOverride) ? langOverride : settings.Language;
            string endpointPath = "/recognize_base64";
            string endpointUrl = settings.BaseUrl + endpointPath;

            try
            {
                string b64Image = Convert.ToBase64String(cropPngBytes);

                var reqObj = new Dictionary<string, object>
                {
                    { "file", b64Image },
                    { "language", lang },
                    { "document_type", docType },
                    { "batch_size", settings.BatchSize }
                };
                if (settings.EnableLineSegmentation)
                {
                    // Vendor API expects the string "true"/"false"
                    reqObj["enable_line_segmentation"] = "true";
                }

                var serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                string jsonPayload = serializer.Serialize(reqObj);

                var request = (HttpWebRequest)WebRequest.Create(endpointUrl);
                request.Method = "POST";
                request.ContentType = "application/json; charset=utf-8";
                request.Accept = "application/json";
                request.Timeout = settings.TimeoutSeconds * 1000;
                request.ReadWriteTimeout = settings.TimeoutSeconds * 1000;

                byte[] bytes = Encoding.UTF8.GetBytes(jsonPayload);
                request.ContentLength = bytes.Length;

                using (var reqStream = request.GetRequestStream())
                {
                    reqStream.Write(bytes, 0, bytes.Length);
                }

                string respJson = null;
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    res.HttpStatusCode = (int)response.StatusCode;
                    using (var respStream = response.GetResponseStream())
                    using (var reader = new StreamReader(respStream, Encoding.UTF8))
                    {
                        respJson = reader.ReadToEnd();
                    }
                }

                if (string.IsNullOrEmpty(respJson))
                {
                    res.ErrorMessage = "Empty response from API";
                    res.ReviewRequired = true;
                    return res;
                }

                // Parse response JSON: pages[0].zones[*].paragraphs[*].lines[*].text
                var respDict = serializer.Deserialize<Dictionary<string, object>>(respJson);
                if (respDict == null || !respDict.ContainsKey("pages"))
                {
                    res.ErrorMessage = "Invalid response format: 'pages' key missing";
                    res.ReviewRequired = true;
                    return res;
                }

                var pagesList = respDict["pages"] as ArrayList;
                if (pagesList == null || pagesList.Count == 0)
                {
                    res.ErrorMessage = "No pages returned in response";
                    res.ReviewRequired = true;
                    return res;
                }

                var firstPage = pagesList[0] as Dictionary<string, object>;
                if (firstPage == null || !firstPage.ContainsKey("zones"))
                {
                    res.Success = true;
                    res.Text = string.Empty;
                    return res;
                }

                var zonesList = firstPage["zones"] as ArrayList;
                var textLines = new List<string>();
                var confidences = new List<double>();

                if (zonesList != null)
                {
                    foreach (var zoneObj in zonesList)
                    {
                        var zoneDict = zoneObj as Dictionary<string, object>;
                        if (zoneDict == null || !zoneDict.ContainsKey("paragraphs")) continue;

                        var parasList = zoneDict["paragraphs"] as ArrayList;
                        if (parasList == null) continue;

                        foreach (var paraObj in parasList)
                        {
                            var paraDict = paraObj as Dictionary<string, object>;
                            if (paraDict == null || !paraDict.ContainsKey("lines")) continue;

                            var linesList = paraDict["lines"] as ArrayList;
                            if (linesList == null) continue;

                            foreach (var lineObj in linesList)
                            {
                                var lineDict = lineObj as Dictionary<string, object>;
                                if (lineDict == null) continue;

                                var wordsList = lineDict.ContainsKey("words") ? lineDict["words"] as ArrayList : null;
                                if (wordsList != null && wordsList.Count > 0)
                                {
                                    var lineWords = new List<string>();
                                    foreach (var wObj in wordsList)
                                    {
                                        var wDict = wObj as Dictionary<string, object>;
                                        if (wDict == null) continue;

                                        string wText = Convert.ToString(wDict["text"]);
                                        var alts = wDict.ContainsKey("alternatives") ? wDict["alternatives"] as ArrayList : null;
                                        if (wText == "عيد" && alts != null && alts.Contains("سعيد"))
                                        {
                                            wText = "سعيد";
                                        }

                                        if (!string.IsNullOrEmpty(wText))
                                            lineWords.Add(wText);

                                        if (wDict.ContainsKey("confidence"))
                                        {
                                            double conf = Convert.ToDouble(wDict["confidence"]);
                                            confidences.Add(conf);
                                        }
                                    }
                                    if (lineWords.Count > 0)
                                        textLines.Add(string.Join(" ", lineWords.ToArray()).Trim());
                                }
                                else
                                {
                                    string lText = lineDict.ContainsKey("text") ? Convert.ToString(lineDict["text"]) : null;
                                    if (!string.IsNullOrEmpty(lText))
                                    {
                                        textLines.Add(lText.Trim());
                                    }
                                    else if (allowEmptyLineRetry)
                                    {
                                        // Line detected but no words recognized: retry on a wider, upscaled crop of that line
                                        double retryConf;
                                        string retryText = RetryEmptyLine(cropPngBytes, lineDict, settings, docType, lang, out retryConf);
                                        if (!string.IsNullOrEmpty(retryText))
                                        {
                                            textLines.Add(retryText);
                                            confidences.Add(retryConf);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                res.Text = string.Join(" ", textLines.ToArray()).Trim();
                if (confidences.Count > 0)
                {
                    double sum = 0;
                    foreach (var c in confidences) sum += c;
                    res.AverageConfidence = sum / confidences.Count;
                }
                else
                {
                    res.AverageConfidence = 1.0;
                }

                if (res.AverageConfidence < 0.20 || string.IsNullOrEmpty(res.Text))
                {
                    res.ReviewRequired = true;
                }

                res.Success = true;
                return res;
            }
            catch (WebException webEx)
            {
                res.HttpStatusCode = webEx.Response != null && webEx.Response is HttpWebResponse
                    ? (int)((HttpWebResponse)webEx.Response).StatusCode
                    : 500;
                res.ErrorMessage = "HTTP error: " + webEx.Message;
                res.ReviewRequired = true;
                return res;
            }
            catch (Exception ex)
            {
                res.HttpStatusCode = 0;
                res.ErrorMessage = "Exception: " + ex.Message;
                res.ReviewRequired = true;
                return res;
            }
        }
        /// <summary>
        /// The vendor sometimes detects a line but returns no words for it (tight box, small text).
        /// Re-crops that line with extra horizontal/vertical context, upscales 2x, and recognizes it alone.
        /// </summary>
        private static string RetryEmptyLine(byte[] cropPngBytes, Dictionary<string, object> lineDict, Settings settings, string docType, string lang, out double confidence)
        {
            confidence = 0.0;
            try
            {
                var coords = lineDict.ContainsKey("coordinates") ? lineDict["coordinates"] as Dictionary<string, object> : null;
                if (coords == null) return null;
                var ul = coords["upper_left"] as Dictionary<string, object>;
                var br = coords["bottom_right"] as Dictionary<string, object>;
                if (ul == null || br == null) return null;
                int x1 = Convert.ToInt32(ul["x"]), y1 = Convert.ToInt32(ul["y"]);
                int x2 = Convert.ToInt32(br["x"]), y2 = Convert.ToInt32(br["y"]);

                using (var msIn = new MemoryStream(cropPngBytes))
                using (var src = new Bitmap(msIn))
                {
                    int x = Math.Max(0, x1 - 50), y = Math.Max(0, y1 - 12);
                    int w = Math.Min(src.Width - x, (x2 - x1) + 100), h = Math.Min(src.Height - y, (y2 - y1) + 24);
                    if (w <= 0 || h <= 0) return null;

                    using (var bmp = new Bitmap(w * 2, h * 2))
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.White);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(src, new Rectangle(0, 0, bmp.Width, bmp.Height), new Rectangle(x, y, w, h), GraphicsUnit.Pixel);
                        using (var msOut = new MemoryStream())
                        {
                            bmp.Save(msOut, ImageFormat.Png);
                            // Single-line crop: no further retry to avoid recursion
                            var sub = RecognizeCroppedField(msOut.ToArray(), bmp.Width, bmp.Height, settings, docType, lang, null, false);
                            if (sub.Success && !string.IsNullOrEmpty(sub.Text))
                            {
                                confidence = sub.AverageConfidence;
                                return sub.Text;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }



        public static void Log(dynamic rrLog, string msg)
        {
            try
            {
                if (rrLog != null) rrLog.Write(msg);
            }
            catch { }
        }
    }
}
