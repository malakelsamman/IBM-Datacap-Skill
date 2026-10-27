---
name: datacap-custom-action-dev
description: Build, compile, deploy, and troubleshoot C# .NET custom actions, character node generation, fingerprint XML zone resolution, and RRX wrappers for IBM Datacap Rulerunner Service (RRS).
---

# IBM Datacap Custom Action Development Guide

When creating or modifying custom C# actions for IBM Datacap Rulerunner Service (RRS):

## 1. RRS Execution Contract & Dynamic Reflection Properties
Every action class must implement dynamic reflection properties for Rulerunner interaction. Use dynamic properties and explicit GC cleanup in property setters to prevent memory leaks and file lock retention:

```csharp
namespace ExternalOCRActions
{
    public class ExternalOCR_RecognizeField
    {
        protected dynamic RRLog = null;
        protected dynamic RRState = null;
        protected dynamic BatchPilot = null;
        protected dynamic DCO = null;
        protected dynamic CurrentDCO = null;

        public dynamic DatacapRRLog { set { this.RRLog = value; GC.Collect(); GC.WaitForPendingFinalizers(); } get { return this.RRLog; } }
        public dynamic DatacapRRState { set { this.RRState = value; GC.Collect(); GC.WaitForPendingFinalizers(); } get { return this.RRState; } }
        public dynamic DatacapRRBatchPilot { set { this.BatchPilot = value; GC.Collect(); GC.WaitForPendingFinalizers(); } get { return this.BatchPilot; } }
        public dynamic DatacapRRDCO { set { this.DCO = value; GC.Collect(); GC.WaitForPendingFinalizers(); } get { return this.DCO; } }
        public dynamic DatacapRRCurrentDCO { set { this.CurrentDCO = value; GC.Collect(); GC.WaitForPendingFinalizers(); } get { return this.CurrentDCO; } }
        public bool DatacapRRCleanupTime {
            set {
                if (value) {
                    RRLog = null; RRState = null; BatchPilot = null; DCO = null; CurrentDCO = null;
                    GC.Collect(); GC.WaitForPendingFinalizers();
                }
            }
        }

        public int Execute(string parameters)
        {
            try {
                // Return 0 for success, 1 for rule failure (advances to next func), 99 for fatal exception
                return 0;
            } catch (Exception ex) {
                if (RRLog != null) RRLog.Write("Error: " + ex.Message);
                return 1;
            }
        }
    }
}
```

---

## 2. Ingested Filename Resolution Pattern (`VScan.xml`)
At runtime, `CurrentDCO.ImageName` is typically renamed to `tm00000x.jpg`. To identify page types from original client filenames, always inspect `VScan.xml` in the batch folder:

```csharp
public static string ResolveSourceFileName(string pageId, dynamic batchPilot, dynamic dco, dynamic pageDco)
{
    // 1. Check VScan.xml in batch directory
    try {
        string batchDir = batchPilot != null ? Convert.ToString(batchPilot.BatchDir) : Convert.ToString(dco.Variable("BatchDir"));
        if (!string.IsNullOrEmpty(batchDir)) {
            string vscanPath = Path.Combine(batchDir, "VScan.xml");
            if (File.Exists(vscanPath)) {
                var xmlDoc = new XmlDocument();
                xmlDoc.Load(vscanPath);
                XmlNode pNode = xmlDoc.SelectSingleNode("//P[@id='" + pageId + "']");
                if (pNode != null) {
                    XmlNode src = pNode.SelectSingleNode("V[@n='ScanSrcPath']") ?? pNode.SelectSingleNode("V[@n='PageName']");
                    if (src != null && !string.IsNullOrEmpty(src.InnerText))
                        return Path.GetFileName(src.InnerText);
                }
            }
        }
    } catch { }

    // 2. Direct DCO variables fallback
    try {
        string srcPath = Convert.ToString(pageDco.Variable("ScanSrcPath"));
        if (!string.IsNullOrEmpty(srcPath)) return Path.GetFileName(srcPath);
        string pageName = Convert.ToString(pageDco.Variable("PageName"));
        if (!string.IsNullOrEmpty(pageName)) return Path.GetFileName(pageName);
    } catch { }

    return null;
}
```

---

## 3. Populating Character DCO Nodes (<C>) for Verification & Highlighting
Datacap Verification panels (Datacap Studio, Navigator, VeriFine) require character nodes `<C>` with Unicode decimal values to display and highlight text correctly:

```csharp
public static void PopulateFieldCharacters(dynamic fieldDco, string text, int confidence = 10, string rect = "0,0,0,0")
{
    // Clear existing characters if any
    try {
        int count = (int)fieldDco.NumOfChildren();
        for (int i = count - 1; i >= 0; i--) {
            fieldDco.DeleteChild(i);
        }
    } catch { }

    // Set Text value
    fieldDco.Text = text;

    // Create child character nodes with Unicode decimal code points
    for (int i = 0; i < text.Length; i++) {
        dynamic cNode = fieldDco.AddChild(2); // 2 = Character node
        int charCode = (int)text[i];
        cNode.Text = charCode.ToString(); // e.g., "1587" for 'س', "32" for ' '
        cNode.set_Variable("cn", confidence.ToString()); // Confidence: 10 = high, 1-9 = low/flagged
        cNode.set_Variable("cr", rect); // Character bounding rectangle
    }
}
```

---

## 4. Structured Privacy-Compliant Telemetry Logging
Never log Base64 payload bodies or full PII in Datacap logs. Use single-line structured telemetry:

```csharp
public static void LogTelemetry(dynamic rrLog, string action, string pageType, string fieldId, string sourceFile, 
    int cropW, int cropH, string route, string endpoint, string lang, string docType, string httpStatus, string parseStatus, bool reviewReq)
{
    string logMsg = string.Format("{0} | PageType='{1}' | FieldID='{2}' | SourceFile='{3}' | Crop='{4}x{5}' | Route='{6}' | Endpoint='{7}' | Lang='{8}' | DocType='{9}' | HTTP='{10}' | ParseStatus='{11}' | VerificationRequired={12}",
        action, pageType, fieldId, sourceFile, cropW, cropH, route, endpoint, lang, docType, httpStatus, parseStatus, reviewReq);
    
    if (rrLog != null) rrLog.Write(logMsg);
}
```

---

## 5. Safe Service Stop & Dual-Deployment Lifecycle
**Use the real script first:** `C:\Datacap\ExternalOCRActions\CompileAndDeploy.bat` compiles `ExternalOCRCommon.cs`, `ExternalOCR_FileNamePageIdentify.cs`, `ExternalOCR_RecognizeField.cs`, `ExternalOCR_TestConnection.cs` with `csc.exe` (framework 4.0.30319, references System, System.Core, System.Drawing, System.Web.Extensions, Microsoft.CSharp), then copies the DLL, `.rrx` and both JSON configs to `C:\Datacap\RRS` and to the application `rules` folder. In this project only a `Restart-Service 'Datacap Rulerunner Service' -Force` was needed afterwards; stopping DCTMS and wTMService was not required. Stop all three only if the DLL copy fails with a file lock.

When a lock does occur, running Datacap background services hold open file handles. Stop services, compile, deploy to both `C:\Datacap\RRS\` and `<AppDir>\dco_<AppName>\rules\`, and restart:

```bat
@echo off
net stop DCTMS
net stop "Datacap Rulerunner Service"
net stop wTMService

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
%CSC% /target:library /platform:anycpu /optimize+ /out:"C:\Datacap\RRS\ExternalOCRActions.dll" /reference:System.dll,System.Core.dll,System.Drawing.dll,System.Web.Extensions.dll,System.Xml.dll src\*.cs

copy /Y "C:\Datacap\RRS\ExternalOCRActions.dll" "C:\Datacap\GoldenDemo\dco_GoldenDemo\rules\"
copy /Y "RRS\ExternalOCRActions.rrx" "C:\Datacap\RRS\"
copy /Y "RRS\ExternalOCRActions.rrx" "C:\Datacap\GoldenDemo\dco_GoldenDemo\rules\"

net start DCTMS
net start "Datacap Rulerunner Service"
net start wTMService
```

---

## 6. DCO Object Hierarchy
- `ObjectType == 0`: Batch (`B`)
- `ObjectType == 1`: Document (`D`)
- `ObjectType == 2`: Page (`P`)
- `ObjectType == 3`: Field (`F`)
- `AddChild(2)` on Field: Character node (`C`)
