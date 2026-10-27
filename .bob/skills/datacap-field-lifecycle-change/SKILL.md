---
name: datacap-field-lifecycle-change
description: Checklist for adding, removing or renaming a field across IBM Datacap (DCO, rulesets, export config), FileNet (document class, property) and a custom portal. Use whenever a field such as EmployerName is added to, or dropped from, the capture-to-FileNet chain.
---

# Changing a field end to end

A field lives in many places. Missing one leaves empty columns, failed exports or a stale portal. Work through the list in order and tick each one.

## Datacap
1. **Studio DCO hierarchy**: add or delete the field under the page type. Setup DCO files: `GOLDENDEMO.XML` and the compiled `.xmb`. Keep them in sync (see `datacap-dco-ruleset-wiring`).
2. **Rulesets** in `GoldenDemo\dco_GoldenDemo\rules\`: every `dcomap.open="F=...;F=..."` list that names the field. Separator is `;`, never `,`. Typical file: `externalocr_extractandrecognize.rul` (rule 2 vendor OCR, rule 3 OCR/A).
3. **Crop zones**: `external_crop_config.json` (source copy in `ExternalOCRActions\src\`, deployed copies in `C:\Datacap\RRS\` and the rules folder). Remove or add the entry; set `enable_line_segmentation` per field if needed.
4. **Fingerprint zones** if you use template matching (`datacap-fingerprint-zone-calibration`).
5. **Export mapping**: `ExportToFileNet.Rul.dll.config` - enable or disable the property line. Datacap field name and FileNet symbolic name can differ (`AccountNumber` -> `Accountnumber1`).
6. Restart: `Restart-Service 'Datacap Rulerunner Service' -Force -WarningAction SilentlyContinue`.

## FileNet
7. Property template and document class: create or remove the property on the class (MCP tools `create_property_template`, `create_document_class`, or ACCE). Symbolic names are case-sensitive.
8. Verify with a read-only CEWS search after one real export (`cews_execute_search`, `cews_get_object`). The GraphQL-based tools do not work on this system.

## Portal (`C:\Datacap\EgyptPostPortal`)
9. `server.js`: `HIDDEN_FIELDS` (hide a removed field in review screens), `FIXUP_FORMS` (manual-entry field list).
10. `app.js`: `FX_FORMS`, translation labels `T.ar.F` / `T.en.F`, `OCRA_FIELDS` and `EXPECT_LEN` if the field is numeric.
11. Restart the portal task if `server.js` changed.

## Verify
12. Run one full batch (see `datacap-demo-ops-and-e2e-verification`). Check `Export.xml` has a `Doc_ID`, and the FileNet document carries the new property values and not the removed one.

## Worked example
`EmployerName` (National ID, "جهة العمل") was removed from Studio and from the FileNet NationalID class. Portal hides it through `HIDDEN_FIELDS`; `ExportToFileNet.Rul.dll.config` has it disabled. Open item: no export without it has been confirmed yet.
