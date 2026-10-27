using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace LocalOCRActions
{
    /// <summary>
    /// Shared helper methods and HTTP client for Local OCR actions.
    /// </summary>
    internal static class LocalOCRCommon
    {
        public const string DefaultBaseUrl = "http://127.0.0.1:8019";

        public const string TypeSignaturePage = "Signature_Form_1";
        public const string TypeSignatureDoc  = "Signature Form";
        public const string FormSignature     = "egypt_post_signature_form_v1";

        public const string TypeNpaPage       = "Form_Page_1";
        public const string TypeNpaDoc        = "NPA Form";
        public const string FormNpa           = "egypt_post_npa_form_v1";

        public const string TypeNatIdPage     = "National ID Front";
        public const string TypeNatIdDoc      = "National ID";
        public const string FormNatId         = "egypt_post_national_id_front_v1_experimental";

        /// <summary>
        /// Resolves the base URL from parameters, config files, or default.
        /// </summary>
        public static string ResolveBaseUrl(string parameters, dynamic currentDco)
        {
            // 1. Check parameters string for url=... or base_url=...
            if (!string.IsNullOrEmpty(parameters))
            {
                var kv = ParseKeyValuePairs(parameters);
                if (kv.ContainsKey("url")) return kv["url"].TrimEnd('/');
                if (kv.ContainsKey("base_url")) return kv["base_url"].TrimEnd('/');
                if (kv.ContainsKey("endpoint")) return kv["endpoint"].TrimEnd('/');
                if (parameters.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    parameters.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return parameters.Trim().TrimEnd('/');
                }
            }

            // 2. Check local ocr_settings.json if present next to DLL
            try
            {
                string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string cfgPath = Path.Combine(dllDir, "ocr_settings.json");
                if (File.Exists(cfgPath))
                {
                    string json = File.ReadAllText(cfgPath, Encoding.UTF8);
                    var serializer = new JavaScriptSerializer();
                    var dict = serializer.Deserialize<Dictionary<string, object>>(json);
                    if (dict != null && dict.ContainsKey("base_url"))
                    {
                        string u = Convert.ToString(dict["base_url"]);
                        if (!string.IsNullOrEmpty(u) && !u.Contains("34.69.150.249")) // ignore legacy cloud IP
                            return u.TrimEnd('/');
                    }
                }
            }
            catch { }

            return DefaultBaseUrl;
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
        public static string ResolveImagePath(dynamic currentDco, dynamic rrLog)
        {
            try
            {
                if (currentDco == null) return null;
                string imgName = (string)currentDco.ImageName;
                if (string.IsNullOrEmpty(imgName))
                {
                    Log(rrLog, "LocalOCR: CurrentDCO.ImageName is empty");
                    return null;
                }

                if (File.Exists(imgName)) return Path.GetFullPath(imgName);

                // Try finding batch directory by walking up DCO hierarchy
                dynamic node = currentDco;
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

                // Check common batch locations
                string[] searchRoots = {
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
                Log(rrLog, "LocalOCR: Error resolving image path: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Sends a POST request with JSON payload and UTF-8 encoding.
        /// </summary>
        public static string PostJson(string url, string jsonPayload, int timeoutSeconds, dynamic rrLog)
        {
            Log(rrLog, "LocalOCR: POST " + url + " (Payload size: " + jsonPayload.Length + " chars)");
            
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";
            request.Accept = "application/json";
            request.Timeout = timeoutSeconds * 1000;
            request.ReadWriteTimeout = timeoutSeconds * 1000;

            byte[] bytes = Encoding.UTF8.GetBytes(jsonPayload);
            request.ContentLength = bytes.Length;

            using (var reqStream = request.GetRequestStream())
            {
                reqStream.Write(bytes, 0, bytes.Length);
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var respStream = response.GetResponseStream())
            using (var reader = new StreamReader(respStream, Encoding.UTF8))
            {
                string respText = reader.ReadToEnd();
                Log(rrLog, "LocalOCR: HTTP " + (int)response.StatusCode + " " + response.StatusDescription);
                return respText;
            }
        }

        public static void SetFieldValue(dynamic currentDco, string fieldName, string value, dynamic rrLog)
        {
            try
            {
                dynamic field = currentDco.FindChild(fieldName);
                if (field != null)
                {
                    field.Text = value ?? string.Empty;
                    Log(rrLog, "LocalOCR: Populated field '" + fieldName + "' = [" + (value ?? string.Empty) + "]");
                }
                else
                {
                    Log(rrLog, "LocalOCR: Field '" + fieldName + "' not found on current page");
                }
            }
            catch (Exception ex)
            {
                Log(rrLog, "LocalOCR: Error setting field '" + fieldName + "': " + ex.Message);
            }
        }

        public static void SetFieldVerification(dynamic currentDco, string fieldName, int status, dynamic rrLog)
        {
            try
            {
                dynamic field = currentDco.FindChild(fieldName);
                if (field != null)
                {
                    field.VerificationStatus = status;
                    Log(rrLog, "LocalOCR: Set VerificationStatus = " + status + " on field '" + fieldName + "'");
                }
            }
            catch { }
        }

        public static void MarkPageVerification(dynamic currentDco, int status, dynamic rrLog)
        {
            try
            {
                if (currentDco != null)
                {
                    currentDco.VerificationStatus = status;
                    Log(rrLog, "LocalOCR: Page marked for verification (status=" + status + ")");
                }
            }
            catch { }
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
