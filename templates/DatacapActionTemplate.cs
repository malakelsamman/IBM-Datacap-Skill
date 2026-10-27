using System;
using System.IO;
using System.Reflection;

namespace DatacapTemplates
{
    /// <summary>
    /// Boilerplate template for IBM Datacap Rulerunner Service (RRS) custom actions.
    /// Implements standard COM reflection properties, memory management, and return codes.
    /// </summary>
    public class DatacapActionTemplate
    {
        #region Datacap RRS Required Reflection Properties

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
                    RRLog = null;
                    RRState = null;
                    BatchPilot = null;
                    DCO = null;
                    CurrentDCO = null;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }

        #endregion

        private bool _versionLogged = false;

        /// <summary>
        /// Main entry point for Datacap action execution.
        /// </summary>
        /// <param name="parameters">Comma-separated or formatted string passed from RRX/RUL</param>
        /// <returns>0 for Success (True), 1 for Failure (False - advance to next function), 99 for Fatal Error</returns>
        public int Execute(string parameters)
        {
            try
            {
                Log("DatacapActionTemplate: Starting execution");

                if (CurrentDCO == null)
                {
                    Log("DatacapActionTemplate: CurrentDCO is null");
                    return 1;
                }

                // ObjectType: 0 = Batch, 1 = Document, 2 = Page, 3 = Field
                int objType = CurrentDCO.ObjectType();
                Log("DatacapActionTemplate: CurrentDCO ObjectType = " + objType + ", ID = " + CurrentDCO.ID);

                // --- YOUR CUSTOM ACTION LOGIC HERE ---

                Log("DatacapActionTemplate: Completed successfully");
                return 0; // Success
            }
            catch (Exception ex)
            {
                Log("DatacapActionTemplate FATAL ERROR: " + ex.ToString());
                return 99; // Fatal error
            }
        }

        protected void Log(string message)
        {
            if (RRLog != null)
            {
                try
                {
                    RRLog.Write(message);
                }
                catch { }
            }
        }

        private void LogVersion()
        {
            try
            {
                if (!_versionLogged && RRLog != null)
                {
                    var asm = Assembly.GetExecutingAssembly();
                    Log("DatacapActionTemplate: Loaded Assembly " + asm.Location + " v" + asm.GetName().Version);
                    _versionLogged = true;
                }
            }
            catch { }
        }

        ~DatacapActionTemplate()
        {
            DatacapRRCleanupTime = true;
        }
    }
}
