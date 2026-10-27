---
name: datacap-parallel-pipeline-migration
description: Design, implement, and verify parallel OCR, AI, or classification pipelines in IBM Datacap without modifying or breaking existing legacy rulesets, DLLs, or task profiles.
---

# Datacap Parallel Pipeline Migration Guide

Use this guide when adding a new OCR engine, AI service, or document classification pipeline to an existing IBM Datacap application (e.g. GoldenDemo) alongside existing production workflows.

## 1. Zero-Collision Architecture Principles

To ensure complete non-interference:
1. **Never rename, delete, disable, or edit legacy files**:
   - Keep legacy rulesets (e.g., `localocr_pageidentify.rul`, `localocr_extractfields.rul`, `createdoc2.rul`) untouched.
   - Keep legacy DLLs and RRX wrappers (e.g., `LocalOCRActions.dll`) intact in `C:\Datacap\RRS\`.
   - Keep legacy task profiles (`PageID`, `Profiler`, `Export`) valid and functional.
2. **Use isolated naming conventions**:
   - Use dedicated action names (`ExternalOCR_*`).
   - Use dedicated ruleset IDs and file names (`externalocr_pageidentify.rul` with ID `20`, `externalocr_createdoc.rul` with ID `21`, `externalocr_extractandrecognize.rul` with ID `22`).
   - Create a dedicated task profile (e.g. `PageID_VendorOCR_POC`).
3. **Do not use vendor product names in Datacap components**:
   - Use generic action, class, ruleset, and variable names.

---

## 2. Parallel Workflow Structure

```mermaid
graph TD
    subgraph Legacy Pipeline
        L1[Legacy PageID] --> L2[Local OCR Service]
        L2 --> L3[Legacy Document Assembly]
        L3 --> L4[Legacy Field Extraction]
    end

    subgraph Parallel POC Pipeline
        P1[PageID_VendorOCR_POC] --> P2[ExternalOCR_FileNamePageIdentify]
        P2 --> P3[ExternalOCR_CreateDoc: Batch Level CreateDocuments]
        P3 --> P4[ExternalOCR_ExtractAndRecognize]
        P4 -->|Handwritten Fields| P5[ExternalOCR_RecognizeField: Vendor API]
        P4 -->|Machine Printed Fields| P6[RecognizeFieldOCRPL / OCR/A]
        P4 -->|Checkboxes| P7[Datacap OMR]
    end

    L4 --> V[Verify Task]
    P5 --> V
    P6 --> V
    P7 --> V
    V --> E[ExportToFileNet Task]
```

---

## 3. Step-by-Step Implementation Blueprint

### Step 1: Create Independent Rulesets
1. **Ruleset 20 (`externalocr_pageidentify.rul`)**:
   - Level: Page (`P`)
   - Action: `ExternalOCRActions.ExternalOCR_FileNamePageIdentify`
2. **Ruleset 21 (`externalocr_createdoc.rul`)**:
   - Level: Batch (`B`)
   - Action: `CreateDocuments()` (creates document structure only; does NOT call `CreateFields()`)
3. **Ruleset 22 (`externalocr_extractandrecognize.rul`)**:
   - Rule 1 (Page level): Set RTL bidi, Arabic locale, call `CreateFields()`
   - Rule 2 (Handwritten fields): `dcomap.open="F=Field1;F=Field2"` $\rightarrow$ `ExternalOCR_RecognizeField`
   - Rule 3 (Printed fields): `dcomap.open="F=Field3;F=Field4"` $\rightarrow$ `RecognizeFieldOCRPL`

### Step 2: Register in `collection.xml` & `<AppName>.app`
Add rulesets and the new task profile to `collection.xml`:
```xml
<rsc>
  ... (legacy rulesets preserved)
  <ruleset id="20" name="ExternalOCR_PageIdentify" depends=""/>
  <ruleset id="21" name="ExternalOCR_CreateDoc" depends=""/>
  <ruleset id="22" name="ExternalOCR_ExtractAndRecognize" depends=""/>
</rsc>
<tps>
  <tprofile name="PageID_VendorOCR_POC">
    <ruleset id="20"/>
    <ruleset id="21"/>
    <ruleset id="22"/>
  </tprofile>
</tps>
```

### Step 3: Create Task Profile Settings XML
Create `<AppDir>\dco_<AppName>\<TaskProfileName>.set.xml` (e.g. `PageID_VendorOCR_POC.set.xml`) pointing `TProfile` to `PageID_VendorOCR_POC`.

---

## 4. Verification & Validation Checklist
Before releasing:
1. Verify legacy DLLs (`LocalOCRActions.dll`) still exist and pass standalone execution tests.
2. Confirm the new filename identify action classifies incoming test images correctly into target page types.
3. Confirm batch `CreateDocuments()` creates documents without instantiating empty fields prematurely.
4. Confirm `CreateFields()` runs only once per page right before recognition.
5. Verify that only cropped field images are sent to the external OCR API (never full pages).
6. Verify that runtime DCO XML (`tm00000x.xml`) contains populated values and Unicode decimal character nodes.
7. Verify FileNet export task receives intact documents and metadata.
