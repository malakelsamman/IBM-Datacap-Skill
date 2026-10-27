---
name: datacap-dco-ruleset-wiring
description: Configure, bind, and sequence IBM Datacap rulesets, task profiles (collection.xml), and DCO hierarchies without causing field wiping or lifecycle race conditions.
---

# IBM Datacap Ruleset & DCO Wiring Guide

## 1. Golden Task Profile Execution Order
To prevent field clearing caused by re-instantiation:

```
1. Ingestion (VScan)
2. Page Identification (LocalOCR_PageIdentify) -> calls CurrentDCO.CreateFields()
3. Document Assembly (CreateDocuments) -> groups pages into documents
4. Data Extraction (LocalOCR_ExtractFields) -> populates text & confidence into fields
5. Verify -> Operator review
6. Export -> ExportToFileNet
```

## 2. DCO Rule Binding Synchronization
When creating or modifying rulesets, always update both:
1. `GOLDENDEMO.XML` (Main Setup DCO)
2. `goldendemo.xmb` (Compiled Binary/XML Setup DCO)

### Page Level Rule Binding Format:
```xml
<!-- Example binding Ruleset 18 (LocalOCR_ExtractFields) to a Page Type -->
<P type="Signature_Form_1">
  <V n="rules">&lt;in&gt;&lt;r id=&quot;1&quot; rs=&quot;18&quot; /&gt;&lt;/in&gt;&lt;out /&gt;</V>
</P>
```

### Unclassified / Other Page Binding Format:
```xml
<!-- Example binding Ruleset 16 (LocalOCR_PageIdentify) to Other -->
<P type="Other">
  <V n="rules">&lt;in&gt;&lt;r id=&quot;1&quot; rs=&quot;16&quot; /&gt;&lt;/in&gt;&lt;out /&gt;</V>
</P>
```

## 3. Ruleset Configuration in `collection.xml`
Ensure task profiles (`PageID`, `Profiler`, `Export`) list the rulesets in exact chronological order inside `dco_<AppName>/rules/collection.xml`.
