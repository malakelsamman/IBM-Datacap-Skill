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

        public int Execute()
        {
            try {
                // Return 0 for success, 1 for rule failure (advance to next func), 99 for fatal exception
                return 0;
            } catch (Exception ex) {
                if (RRLog != null) RRLog.Write("Error: " + ex.Message);
                return 99;
            }
        }
    }
}
```

## 2. Direct Fingerprint XML Coordinate Fallback
When runtime DCO fields are created before fingerprint matching or without zone inheritance, `CurrentDCO.Position` defaults to `"0,0,0,0"`. Always implement a direct fingerprint XML reader fallback:

```csharp
public static bool TryGetPositionFromFingerprintXml(string batchDir, string fingerprintId, string fieldName, out int[] pos)
{
    pos = null;
    if (string.IsNullOrEmpty(fingerprintId)) return false;

    // Locate fingerprint directory relative to batch or application root
    string fpPath = Path.Combine(batchDir, "..", "..", "fingerprint", fingerprintId + ".xml");
    if (!File.Exists(fpPath)) return false;

    try {
        var doc = new System.Xml.XmlDocument();
        doc.Load(fpPath);
        var fieldNode = doc.SelectSingleNode("//F[@id='" + fieldName + "']");
        if (fieldNode != null) {
            var posVar = fieldNode.SelectSingleNode("V[@n='Position']");
            if (posVar != null && !string.IsNullOrEmpty(posVar.InnerText) && posVar.InnerText != "0,0,0,0") {
                string[] parts = posVar.InnerText.Split(',');
                if (parts.Length == 4) {
                    pos = new int[] {
                        int.Parse(parts[0].Trim()),
                        int.Parse(parts[1].Trim()),
                        int.Parse(parts[2].Trim()),
                        int.Parse(parts[3].Trim())
                    };
                    return true;
                }
            }
        }
    } catch { }
    return false;
}
```

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

## 4. Compilation and Dual-Deployment Pattern
Compile directly with .NET Framework 4.x `csc.exe` and deploy to **both** `C:\Datacap\RRS\` and `<AppDir>\dco_<AppName>\rules\`:

```powershell
# Compile DLL
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:library /platform:anycpu /optimize+ /out:"C:\Datacap\RRS\ExternalOCRActions.dll" /reference:System.dll,System.Core.dll,System.Drawing.dll,System.Web.Extensions.dll,System.Xml.dll (Get-ChildItem -Path "ExternalOCRActions\src\*.cs" | Select-Object -ExpandProperty FullName)

# Deploy to Application Rules
Copy-Item "C:\Datacap\RRS\ExternalOCRActions.dll" "GoldenDemo\dco_GoldenDemo\rules\" -Force
```

## 5. DCO Object Hierarchy
- `ObjectType == 0`: Batch (`B`)
- `ObjectType == 1`: Document (`D`)
- `ObjectType == 2`: Page (`P`)
- `ObjectType == 3`: Field (`F`)
- `ObjectType == 4`: Character (`C`)
