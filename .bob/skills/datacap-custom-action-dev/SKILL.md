---
name: datacap-custom-action-dev
description: Build, compile, deploy, and troubleshoot C# .NET custom actions and RRX wrappers for IBM Datacap Rulerunner Service (RRS).
---

# IBM Datacap Custom Action Development Guide

When creating or modifying custom C# actions for IBM Datacap:

## 1. RRS Execution Contract & Reflection Properties
Every action class must implement dynamic reflection properties for Rulerunner interaction:
- `RRLog`: Dynamic logger object (`RRLog.Write(...)`).
- `RRState`: Execution state.
- `BatchPilot`: Pilot runtime object.
- `DCO`: Root Batch DCO object.
- `CurrentDCO`: Target DCO node for the current execution rule (Batch, Document, Page, or Field).
- `DatacapRRCleanupTime`: Finalizer cleanup trigger.

### Standard Template:
```csharp
namespace CustomActions
{
    public class MyCustomAction
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

## 2. Compilation and Deployment Pattern
Compile directly using .NET Framework 4.x `csc.exe` and dual-deploy to both `C:\Datacap\RRS` and `<AppDir>\dco_<AppName>\rules\`.

```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:library /platform:anycpu /optimize+ /out:"C:\Datacap\RRS\MyActions.dll" /reference:System.dll,System.Core.dll,System.Web.Extensions.dll src\*.cs
copy /Y "C:\Datacap\RRS\MyActions.dll" "C:\Datacap\GoldenDemo\dco_GoldenDemo\rules\"
copy /Y "RRS\MyActions.rrx" "C:\Datacap\RRS\"
copy /Y "RRS\MyActions.rrx" "C:\Datacap\GoldenDemo\dco_GoldenDemo\rules\"
```

## 3. DCO Object Traversal Rules
- `ObjectType == 0`: Batch
- `ObjectType == 1`: Document
- `ObjectType == 2`: Page
- `ObjectType == 3`: Field

Always verify `CurrentDCO.ObjectType()` before accessing object-specific properties.
