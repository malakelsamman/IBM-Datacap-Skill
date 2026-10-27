using System;
using System.IO;
using System.Net;
using System.Reflection;

namespace ExternalOCRActions
{
    /// <summary>
    /// Datacap custom action: ExternalOCR_TestConnection
    /// Tests connectivity to the Vendor OCR API (/alive and /health_checks).
    /// </summary>
    public class ExternalOCR_TestConnection
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
                var settings = ExternalOCRCommon.LoadSettings(parameters);
                string aliveUrl = settings.BaseUrl + "/alive";
                string healthUrl = settings.BaseUrl + "/health_checks";

                Log("ExternalOCR_TestConnection: Testing connection to " + settings.BaseUrl);

                // 1. Test /alive
                var aliveReq = (HttpWebRequest)WebRequest.Create(aliveUrl);
                aliveReq.Method = "GET";
                aliveReq.Timeout = 10000;
                using (var aliveResp = (HttpWebResponse)aliveReq.GetResponse())
                {
                    if (aliveResp.StatusCode != HttpStatusCode.OK)
                    {
                        Log("ExternalOCR_TestConnection: /alive returned HTTP " + (int)aliveResp.StatusCode);
                        return 1;
                    }
                }

                // 2. Test /health_checks
                var healthReq = (HttpWebRequest)WebRequest.Create(healthUrl);
                healthReq.Method = "GET";
                healthReq.Timeout = 10000;
                using (var healthResp = (HttpWebResponse)healthReq.GetResponse())
                {
                    if (healthResp.StatusCode != HttpStatusCode.OK)
                    {
                        Log("ExternalOCR_TestConnection: /health_checks returned HTTP " + (int)healthResp.StatusCode);
                        return 1;
                    }
                }

                Log("ExternalOCR_TestConnection: Connection verified successfully (200 OK)");
                return 0;
            }
            catch (Exception ex)
            {
                Log("ExternalOCR_TestConnection ERROR: " + ex.Message);
                return 1;
            }
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
                    Log("ExternalOCR_TestConnection: Assembly " + asm.Location + " v" + asm.GetName().Version);
                    _versionLogged = true;
                }
            }
            catch { }
        }

        ~ExternalOCR_TestConnection() { DatacapRRCleanupTime = true; }
    }
}
