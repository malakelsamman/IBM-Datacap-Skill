---
name: datacap-dco-ruleset-wiring
description: Configure, bind, and sequence IBM Datacap rulesets, task profiles (collection.xml), DCO hierarchies, and hybrid OCR routing without causing field wiping or RRX loading errors.
---

# IBM Datacap Ruleset & DCO Wiring Guide

## 1. Golden Task Profile Execution Order
To prevent field clearing or missing zone inheritance:

```
1. Ingestion (VScan)
2. Page Identification (PageID)
   - Assign Page Type (e.g., National ID Front, NPA Form, Signature Form)
   - Explicitly assign FingerprintID / TemplateID (e.g., 568, 569, 570)
   - Call CurrentDCO.CreateFields() to instantiate field nodes
3. Document Assembly (CreateDocuments) -> groups pages into documents
4. Data Extraction & Recognition (ExtractFields)
   - Route Handwritten / Arabic text -> Vendor OCR / Local AI action
   - Route Machine-printed Numbers -> OCR/A (RecognizeFieldOCR_A)
5. Verify -> Operator review
6. Export -> ExportToFileNet
```

## 2. Hybrid OCR Routing Pattern in Rulesets (.rul)
Split recognition rules based on character set and print style:

```xml
<!-- Example: Machine-printed numeric field routed to OCR/A -->
<func name="Recognize National ID Number">
  <a name="RecognizeFieldOCR_A" customaction="true">
    <p name="Action" type="string" v="net:RecognitionOCRA.OCRAActions.RecognizeFieldOCR_A" />
  </a>
</func>

<!-- Example: Handwritten / Arabic text routed to Vendor OCR / Custom Action -->
<func name="Recognize Arabic Text Field">
  <a name="ExternalOCR_RecognizeField" customaction="true">
    <p name="Action" type="string" v="net:ExternalOCRActions.ExternalOCR_RecognizeField.ExternalOCR_RecognizeField" />
  </a>
</func>
```

> **Warning (Error 17000008):** Never reference obsolete or uninstalled RRX libraries (e.g. `RecognizeFieldOCRPL`). Always use valid registered .NET assemblies or standard Datacap actions.

## 3. Page Identification & Fingerprint Binding
Ensure custom Page Identification actions set both `PageType` and `FingerprintID`:

```csharp
// Set Page Type and Fingerprint ID
CurrentDCO.Type = "National ID Front";
CurrentDCO.set_Variable("FingerprintID", "568");
CurrentDCO.set_Variable("TemplateID", "568");

// Re-instantiate fields with template zones
CurrentDCO.CreateFields();
```

## 4. DCO Rule Binding Synchronization
When creating or modifying rulesets, always keep both files updated:
1. `GOLDENDEMO.XML` (Main Setup DCO)
2. `goldendemo.xmb` (Compiled Binary/XML Setup DCO)

### Page Level Rule Binding Format:
```xml
<P type="National ID Front">
  <V n="rules">&lt;in&gt;&lt;r id=&quot;1&quot; rs=&quot;18&quot; /&gt;&lt;/in&gt;&lt;out /&gt;</V>
</P>
```

## 5. Ruleset Sequencing in `collection.xml`
Ensure task profiles (`PageID`, `Profiler`, `Export`) list rulesets in strict sequential order inside `dco_<AppName>/rules/collection.xml`.
