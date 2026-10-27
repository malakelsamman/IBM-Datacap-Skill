---
name: datacap-fingerprint-zone-calibration
description: Define, calibrate, and synchronize Datacap fingerprint zones, Setup DCO hierarchy, and runtime coordinate resolution for forms and IDs.
---

# Datacap Fingerprint Zone Calibration Guide

This guide establishes the pattern for maintaining zone coordinates across fingerprint files, Setup DCO, and custom recognition actions.

## 1. The Three Sources of Truth
To ensure consistency across Datacap Studio Zones tab, Rulerunner execution, and custom recognition actions:

1. **Fingerprint Zone XMLs (`<AppDir>\fingerprint\<ID>.xml`)**: The master definition created or updated when drawing zones in Datacap Studio.
2. **Setup DCO (`<AppDir>\dco_<AppName>\<APPNAME>.XML` and `.xmb`)**: The document hierarchy definition holding default field templates and ruleset bindings.
3. **External Crop Fallback Config (`external_crop_config.json`)**: Ratio-based and absolute bounding box fallbacks used when no fingerprint is matched.

## 2. Standard Fingerprint IDs and Zone Coordinates

### A. National ID Front (Fingerprint `568.xml`)
- `FullNameArabic`: `1180, 70, 1690, 310` (Includes padding for initial letters like `س`)
- `FamilyNameArabic`: `1428, 316, 1627, 378`
- `AddressArabic`: `1152, 370, 1631, 448`
- `NationalIdNumber`: `1054, 507, 1669, 589`
- `EmployerName`: `1141, 1400, 1521, 1457`

### B. Signature Form (Fingerprint `569.xml`)
- `AccountHolderName`: `1430, 616, 2080, 723`
- `AccountNumber`: `732, 745, 2276, 880`

### C. NPA Form (Fingerprint `570.xml`)
- `EmployerName2`: `1141, 1400, 1521, 1457`
- `AvrgMonthlyIncome`: `1786, 1175, 2141, 1245` (Targets the checked income bracket)

## 3. Resolving `Position: 0,0,0,0` at Runtime
When page identification sets page type without assigning `FingerprintID` / `TemplateID`, `CreateFields()` instantiates fields with `Position="0,0,0,0"`.

### Prevention & Handling:
1. **Explicit Template Binding**:
   ```csharp
   CurrentDCO.set_Variable("FingerprintID", "568");
   CurrentDCO.set_Variable("TemplateID", "568");
   ```
2. **Direct XML Fallback in Custom Actions**: If `CurrentDCO.Position` is `"0,0,0,0"`, load `<AppDir>\fingerprint\<ID>.xml`, query `/P/F[@id='FieldName']/V[@n='Position']`, and crop using the resolved coordinates.
