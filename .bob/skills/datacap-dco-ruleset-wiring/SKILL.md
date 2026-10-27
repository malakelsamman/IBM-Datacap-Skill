---
name: datacap-dco-ruleset-wiring
description: Configure, bind, and sequence IBM Datacap rulesets, task profiles (collection.xml), DCO hierarchies, and hybrid OCR routing without causing field wiping or RRX loading errors.
---

# IBM Datacap Ruleset & DCO Wiring Guide

## 1. Golden Task Profile Execution Order
To prevent field clearing, duplicate field creation, or missing zone inheritance:

```
1. Ingestion (VScan / ScanFromDisk)
2. Page Identification (PageID)
   - Assign Page Type (e.g., Signature_Form_1, Form_Page_1, National ID Front)
   - Do NOT run CreateFields here if document hierarchy is built next
3. Document Assembly (CreateDocuments)
   - Bound at BATCH (B) level ONLY in a dedicated ruleset
   - Assembles pages into document objects (<D>) based on document integrity rules
4. Data Extraction & Recognition (ExtractFields)
   - Page Level (P): Set hr_bidi="RTL", hr_locale="ar-EG", call CreateFields()
   - Field Level (F):
     * Handwritten Arabic fields -> External Vendor OCR action
     * Machine-printed Arabic / alphanumeric fields -> Built-in OCR/PL or OCR/A
     * Checkboxes -> Datacap OMR
5. Verify -> Operator review
6. Export -> ExportToFileNet
```

> ⚠️ **Critical DCO Rule:** Never place `CreateFields()` inside the batch-level `CreateDocuments` ruleset or after field recognition. `CreateFields()` must run exactly once per final page before field recognition occurs.

---

## 2. DCO Map Syntax in Rulesets (.rul)

### ⚠️ Strict Delimiter Rule: Semicolon `;` vs Comma `,`
When binding rules to multiple field types via `dcomap.open`, **Datacap Rulerunner requires semicolon delimiters**. Using commas causes Datacap to match only the first field and silently drop the rest!

```xml
<!-- ✅ CORRECT: Semicolon delimited -->
<rule name="Handwritten Vendor OCR Routing" id="2" dcomap.open="F=AccountHolderName;F=AccountNumber;F=EmployerName2">
  <func name="Recognize Field">
    <a name="Execute" ns="net:ExternalOCRActions.ExternalOCR_RecognizeField" />
  </func>
</rule>

<!-- ❌ INCORRECT: Comma separated (matches only AccountHolderName, drops AccountNumber) -->
<rule name="Handwritten Routing" id="2" dcomap.open="F=AccountHolderName,AccountNumber,EmployerName2">
```

---

## 3. Hybrid OCR Routing Pattern in Rulesets (.rul)
Split recognition rules cleanly based on character set and print style:

```xml
<!-- Example: Machine-printed Arabic text routed to OCR/PL -->
<rule name="Machine Printed OCRPL Routing" id="3" dcomap.open="F=FullNameArabic;F=FamilyNameArabic;F=AddressArabic;F=NationalIdNumber;F=EmployerName">
  <func name="Recognize Machine Printed Field">
    <a name="RecognizeFieldOCRPL" ns="RecognitionOCRPL.OCRPActions" />
  </func>
</rule>

<!-- Example: Handwritten Arabic text routed to External Vendor OCR -->
<rule name="Handwritten Vendor OCR Routing" id="2" dcomap.open="F=AccountHolderName;F=AccountNumber;F=EmployerName2">
  <func name="Recognize Handwritten Field">
    <a name="Execute" ns="net:ExternalOCRActions.ExternalOCR_RecognizeField">
      <p name="parameters" type="string" />
    </a>
  </func>
</rule>
```

---

## 4. Page Identification & Fingerprint Binding
When identifying pages by filename or metadata:
1. Set `CurrentDCO.Type = "Signature_Form_1"`
2. If using template matching, bind `FingerprintID` / `TemplateID`:
   ```csharp
   CurrentDCO.set_Variable("FingerprintID", "569");
   CurrentDCO.set_Variable("TemplateID", "569");
   ```
3. Fallback unclassified pages to `Other` or mark for verification (`CurrentDCO.Status = 1`). Never default silently to a specific document type.

---

## 5. DCO Rule Binding Synchronization
When creating or modifying rulesets, keep configuration files synchronized:
1. `GOLDENDEMO.XML` (Main Setup DCO)
2. `goldendemo.xmb` (Compiled Binary/XML Setup DCO)
3. `dco_<AppName>/rules/collection.xml` (Task Profile and Ruleset Registration)
4. `<AppName>.app` (Application Task Profiles)

---

## 6. Ruleset Registration in `collection.xml`
Ensure task profiles (`PageID`, `PageID_VendorOCR_POC`, `Verify`, `Export`) list rulesets in strict execution order:

```xml
<tprofile name="PageID_VendorOCR_POC">
  <ruleset id="20"/> <!-- ExternalOCR_PageIdentify -->
  <ruleset id="21"/> <!-- ExternalOCR_CreateDoc -->
  <ruleset id="22"/> <!-- ExternalOCR_ExtractAndRecognize -->
</tprofile>
```
