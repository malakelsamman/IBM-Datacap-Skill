---
name: datacap-rrs-diagnostic-debugger
description: Diagnose and resolve IBM Datacap Rulerunner (RRS) runtime errors (17000008, 17000001), analyze RRS logs, and troubleshoot empty or unpopulated DCO fields.
---

# IBM Datacap RRS Diagnostic & Debugging Guide

Use this guide to systematically diagnose and resolve Rulerunner Service (RRS) execution failures, missing RRX references, and unpopulated runtime fields.

## 1. Common RRS Error Codes & Fixes

### A. Error `17000008` (`0x1036648`) in `CScript::AddXMLXSL`
- **Error Message:** `error: couldn't locate RRX code "<library>.<action>". Tried to load: ...`
- **Root Cause:** A ruleset (.rul) references an action library or DLL that does not exist in `C:\Datacap\RRS\` or `<AppDir>\dco_<AppName>\rules\`.
- **Diagnostic Steps:**
  1. Open `<task>_rrs.log` and search for `Error (385875976` or `17000008`.
  2. Identify the failing action name and ruleset.
  3. Replace obsolete actions (e.g., `RecognizeFieldOCRPL`) with valid registered actions (e.g. `RecognizeFieldOCR_A` or your custom .NET action `net:Namespace.Class.Method`).
  4. Ensure both `<AppName>\dco_<AppName>\rules\dumpns.xml` and Studio cache are refreshed.

### B. Return Code `99` / Exception in Custom Action
- **Root Cause:** Unhandled exception in C# action (null reference, file lock, invalid JSON parse).
- **Fix:** Ensure all file access uses in-memory streams (`File.ReadAllBytes`) and wrap action bodies in try-catch blocks that write to `RRLog.Write(...)` before returning 99.

## 2. Troubleshooting Unpopulated Fields Checklist

When fields are empty or not appearing in Datacap Studio Test/Verify panels, check these in order:

| Step | Check | Where to Look | Remediation |
|---|---|---|---|
| 1 | **Fatal Rule Halt** | `<task>_rrs.log` | If any action in a rule fails or throws an exception, all subsequent field actions are skipped. Fix the failing action first. |
| 2 | **Position Coordinates** | `tm00000x.xml` | If `Position="0,0,0,0"`, the engine does not know where to crop. Ensure `FingerprintID` is set and `TryGetPositionFromFingerprintXml` fallback is active. |
| 3 | **Character `<C>` Nodes** | `tm00000x.xml` | Datacap Studio displays field values only if `<C>` child nodes exist. Verify that character nodes with decimal Unicode values are added. |
| 4 | **API / OCR Status** | `<task>_rrs.log` | Check for HTTP 400/500 errors. For external OCR, verify `"document_type": "handwritten"` is sent. |

## 3. Log Analysis Workflow

> **Log names follow the profile, not the task.** The Navigator PageID task runs profile `PageID_VendorOCR_POC`, so its log is `pageid_vendorocr_poc_rrs.log`. Other logs: `profiler_rrs.log`, `export_rrs.log`.

1. Open `GoldenDemo\batches\<BatchID>\<ProfileName>_rrs.log`.
2. Trace the execution tree:
   ```
   calling action <ActionName> () on F:<FieldName>
      call "<Assembly>-><Method>"
      result 0 (success) or 1 (failure) or 99 (exception)
   ```
3. Inspect `GoldenDemo\batches\<BatchID>\tm00000x.xml`:
   - Check `<F id="...">` node.
   - Verify `<V n="STATUS">0</V>` (0 = valid, 1 = low confidence / review needed).
   - Verify character nodes `<C cn="10" cr="...">...</C>`.

## 4. Queue and Routing Symptoms

| Symptom | Cause | Fix |
|---|---|---|
| Batch sits at `PageID/pending` and never runs | Repointed with `SaveBatchAttribute`; Rulerunner ignores it | Do not repoint. Use a raised condition (`datacap-fixup-manual-routing`) or abandon and resubmit |
| Parent stays `Profiler/waiting` | Normal: it waits for its Fixup child | Complete the Fixup child; releasing it merges the parent |
| Fixup child never appears | Ruleset 23 missing from the profile that PageID really runs, or Rulerunner not restarted | Add id 23 to `PageID_VendorOCR_POC` (and `PageID`), restart Rulerunner |
| Unattended Fixup errors | FixUp was added to `Rulerunner.xml` | Remove it: FixUp is manual |
| Condition names | `Route To Fixup` (PageID), `Requires Verify` (Profiler) | Check Studio workflow |
| `FixUp.xml` missing | The child was not grabbed yet | `GrabBatch` first |
