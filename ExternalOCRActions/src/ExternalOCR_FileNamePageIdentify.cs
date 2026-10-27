using System;
using System.IO;
using System.Xml;
using System.Reflection;

namespace ExternalOCRActions
{
    /// <summary>
    /// Datacap custom action: ExternalOCR_FileNamePageIdentify
    /// Identifies page type based on incoming image filename by inspecting DCO XML and DCO object variables.
    /// </summary>
    public class ExternalOCR_FileNamePageIdentify
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
                if (CurrentDCO == null)
                {
                    Log("ExternalOCR_FileNamePageIdentify: CurrentDCO is null");
                    return 1;
                }

                string pageId = Convert.ToString(CurrentDCO.ID);
                string oldPageType = Convert.ToString(CurrentDCO.Type);

                // 1. Resolve source filename from runtime batch DCO XML or variables
                string fileName = ResolveSourceFileName(pageId, CurrentDCO);

                if (string.IsNullOrEmpty(fileName))
                {
                    Log(string.Format("ExternalOCR_FileNamePageIdentify | PageID='{0}' | PageType='{1}' | SourceFile='UNKNOWN' | Route='FileNameIdentification' | Status='Fallback' | Reason='Could not resolve source filename'",
                        pageId, oldPageType));
                    MarkVerification(1);
                    return 0;
                }

                // 2. Controlled prefix matching
                string identifiedPageType = MatchPageTypeByFileName(fileName);

                if (string.IsNullOrEmpty(identifiedPageType))
                {
                    Log(string.Format("ExternalOCR_FileNamePageIdentify | PageID='{0}' | PageType='{1}' | SourceFile='{2}' | Route='FileNameIdentification' | Status='Fallback' | Reason='Filename did not match controlled prefix patterns'",
                        pageId, oldPageType, fileName));
                    MarkVerification(1);
                    return 0;
                }

                // 3. Set CurrentDCO.Type and FingerprintID/TemplateID
                CurrentDCO.Type = identifiedPageType;

                string fpId = string.Empty;
                if (identifiedPageType == ExternalOCRCommon.TypeNatIdPage)
                    fpId = "568";
                else if (identifiedPageType == ExternalOCRCommon.TypeSignaturePage)
                    fpId = "569";
                else if (identifiedPageType == ExternalOCRCommon.TypeNpaPage)
                    fpId = "570";

                if (!string.IsNullOrEmpty(fpId))
                {
                    try { CurrentDCO.set_Variable("FingerprintID", fpId); } catch { }
                    try { CurrentDCO.set_Variable("TemplateID", fpId); } catch { }
                }

                Log(string.Format("ExternalOCR_FileNamePageIdentify | PageID='{0}' | PageType='{1}->{2}' | FingerprintID='{3}' | SourceFile='{4}' | Route='FileNameIdentification' | Status='Success' | VerificationRequired=False",
                    pageId, oldPageType, identifiedPageType, fpId, fileName));

                return 0;
            }
            catch (Exception ex)
            {
                Log("ExternalOCR_FileNamePageIdentify ERROR: " + ex.Message);
                MarkVerification(1);
                return 1;
            }
        }

        private string ResolveSourceFileName(string pageId, dynamic pageDco)
        {
            // Approach 1: Parse the batch VScan.xml or PageID.xml in the batch folder
            try
            {
                string batchDir = null;
                if (BatchPilot != null)
                {
                    try { batchDir = Convert.ToString(BatchPilot.BatchDir); } catch { }
                }

                if (string.IsNullOrEmpty(batchDir) && DCO != null)
                {
                    try { batchDir = Convert.ToString(DCO.Variable("BatchDir")); } catch { }
                }

                if (!string.IsNullOrEmpty(batchDir) && Directory.Exists(batchDir))
                {
                    string[] xmlFiles = new string[]
                    {
                        Path.Combine(batchDir, "VScan.xml"),
                        Path.Combine(batchDir, "PageID.xml"),
                        Path.Combine(batchDir, "vscan.xml"),
                        Path.Combine(batchDir, "pageid.xml")
                    };

                    foreach (string xmlPath in xmlFiles)
                    {
                        if (File.Exists(xmlPath))
                        {
                            var doc = new XmlDocument();
                            doc.Load(xmlPath);
                            XmlNode pageNode = doc.SelectSingleNode("//P[@id='" + pageId + "']");
                            if (pageNode != null)
                            {
                                XmlNode srcNode = pageNode.SelectSingleNode("V[@n='ScanSrcPath']");
                                if (srcNode != null && !string.IsNullOrEmpty(srcNode.InnerText))
                                    return Path.GetFileName(srcNode.InnerText);

                                XmlNode nameNode = pageNode.SelectSingleNode("V[@n='PageName']");
                                if (nameNode != null && !string.IsNullOrEmpty(nameNode.InnerText))
                                    return Path.GetFileName(nameNode.InnerText);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("ResolveSourceFileName XML parser exception: " + ex.Message);
            }

            // Approach 2: Direct DCO variable properties
            try
            {
                string name = null;
                try { name = Convert.ToString(pageDco.Variable("ScanSrcPath")); } catch { }
                if (!string.IsNullOrEmpty(name)) return Path.GetFileName(name);

                try { name = Convert.ToString(pageDco.Variable("PageName")); } catch { }
                if (!string.IsNullOrEmpty(name)) return Path.GetFileName(name);

                try { name = Convert.ToString(pageDco.ImageName); } catch { }
                if (!string.IsNullOrEmpty(name) && !Path.GetFileNameWithoutExtension(name).StartsWith("tm", StringComparison.OrdinalIgnoreCase))
                    return Path.GetFileName(name);
            }
            catch { }

            return null;
        }

        private string MatchPageTypeByFileName(string rawFileName)
        {
            if (string.IsNullOrEmpty(rawFileName)) return null;

            string fn = rawFileName.ToLowerInvariant();

            // Controlled filename mapping:
            // 1. Signature Form: signature_form_01.jpg / signature_form / Signature_Form_Image_5
            if (fn.StartsWith("signature_form") || fn.Contains("signature_form"))
            {
                return ExternalOCRCommon.TypeSignaturePage;
            }

            // 2. NPA Form: npa_form_01.jpg / npa_form / NPA_Form_Image_2
            if (fn.StartsWith("npa_form") || fn.Contains("npa_form"))
            {
                return ExternalOCRCommon.TypeNpaPage;
            }

            // 3. National ID: national_id_01.jpg / national_id / nationalid
            if (fn.StartsWith("national_id") || fn.Contains("national_id") || fn.StartsWith("nationalid"))
            {
                return ExternalOCRCommon.TypeNatIdPage;
            }

            return null;
        }

        private void MarkVerification(int status)
        {
            try
            {
                if (CurrentDCO != null)
                {
                    CurrentDCO.Status = status;
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
                    string ver = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                    Log("ExternalOCR_FileNamePageIdentify: Assembly " + Assembly.GetExecutingAssembly().Location + " v" + ver);
                    _versionLogged = true;
                }
            }
            catch { }
        }
    }
}
