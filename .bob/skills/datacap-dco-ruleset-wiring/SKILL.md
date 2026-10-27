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
<rule name="Handwritten Vendor OCR Routing" id="2" dcomap.open="F=AccountHolderName;F=EmployerName2;F=AvrgMonthlyIncome">
  <func name="Recognize Field">
    <a name="Execute" ns="net:ExternalOCRActions.ExternalOCR_RecognizeField" />
  </func>
</rule>

<!-- ❌ INCORRECT: Comma separated (matches only AccountHolderName, drops the rest) -->
<rule name="Handwritten Routing" id="2" dcomap.open="F=AccountHolderName,EmployerName2,AvrgMonthlyIncome">
```

---

## 3. Hybrid OCR Routing Pattern in Rulesets (.rul)
Split recognition rules by character set. This is the **deployed** pattern (`externalocr_extractandrecognize.rul`, ruleset 22):

```xml
<!-- Rule 2: handwritten / name / address fields to the vendor action -->
<rule name="Vendor OCR Routing" id="2" dcomap.open="F=AccountHolderName;F=EmployerName2;F=AvrgMonthlyIncome;F=FullNameArabic;F=FamilyNameArabic;F=AddressArabic;F=EmployerName">
  <func name="Recognize Field with Vendor OCR">
    <a name="rrSet" ns="rrunner.dll"><p name="varSource" v="RTL"/><p name="varTarget" v="@F.hr_bidi"/></a>
    <a name="rrSet" ns="rrunner.dll"><p name="varSource" v="ar-EG"/><p name="varTarget" v="@F.hr_locale"/></a>
    <a name="Execute" ns="net:ExternalOCRActions.ExternalOCR_RecognizeField"><p name="parameters" type="string"/></a>
  </func>
</rule>

<!-- Rule 3: numbers to Datacap OCR/A (no vendor call) -->
<rule name="OCR_A Numeric Routing" id="3" dcomap.open="F=NationalIdNumber;F=AccountNumber">
  <func name="Recognize Numeric Field with OCR_A">
    <a name="RecognizeFieldOCR_A" ns="net:RecognitionOCRA.OCRAActions"/>
  </func>
</rule>
```
`RecognizeFieldOCRPL` is not used (fails with 17000008). To pass a per-field option, fill the `parameters` string, for example `enable_line_segmentation=true`.

## 3b. Ruleset 23 and profile mapping
- Ruleset 23 `ExternalOCR_FixupRoute` routes `P=Other` pages to Fixup (see `datacap-fixup-manual-routing`). Put it **last** in the profile.
- The Navigator `PageID` task runs profile **`PageID_VendorOCR_POC`** (confirmed by log names `pageid_vendorocr_poc_rrs.log`). Keep both `PageID` and `PageID_VendorOCR_POC` listing 20, 21, 22, 23.
- Studio may add empty profiles (`FixUp`, `iVScan`, one with a blank name) to `collection.xml`. They are harmless.

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
