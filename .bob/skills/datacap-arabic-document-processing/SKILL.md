---
name: datacap-arabic-document-processing
description: Process Arabic documents, Egyptian National IDs, forms, and stamps in IBM Datacap, covering RTL Unicode DCO encoding, numeric reordering, dot-matrix ink, and Arabic glyph padding.
---

# Arabic Document & Form Processing in IBM Datacap

This guide establishes the standard rules for processing Egyptian National IDs, official banking/postal forms, and Arabic text in IBM Datacap pipelines.

## 1. DCO Unicode Decimal Character Representation
Datacap Verification interfaces (Datacap Studio, Navigator, VeriFine) do not store raw UTF-8 Arabic text directly in `<C>` nodes. Instead:
- Set `<V n="hr_bidi">RTL</V>` on the field node.
- Set `<V n="hr_locale">ar-EG</V>` on the field node.
- Store each character's decimal Unicode code point in its respective `<C>` child node.

### Example C# Encoding Helper:
```csharp
public static void PopulateArabicField(dynamic fieldDco, string arabicText, int confidence = 10)
{
    fieldDco.Text = arabicText;
    fieldDco.set_Variable("hr_bidi", "RTL");
    fieldDco.set_Variable("hr_locale", "ar-EG");

    for (int i = 0; i < arabicText.Length; i++) {
        dynamic cNode = fieldDco.AddChild(2); // Character node
        int unicodeDec = (int)arabicText[i];
        cNode.Text = unicodeDec.ToString(); // e.g. "1587" for 'س', "32" for ' '
        cNode.set_Variable("cn", confidence.ToString());
        cNode.set_Variable("cr", "0,0,0,0");
    }
}
```

## 2. 14-Digit Egyptian National ID & Numeric Normalization
Arabic OCR and RTL rendering engines often flip numeric strings or insert directional formatting.

### Rules for Egyptian National ID (`NationalIdNumber`):
1. **Engine Selection**: Always route to OCR/A (`net:RecognitionOCRA.OCRAActions.RecognizeFieldOCR_A`) rather than generic handwritten Arabic OCR for machine-printed national ID numbers.
2. **Length & Structure**: 14 digits total:
   - Digit 1: Century (`2` = 1900-1999, `3` = 2000-2099)
   - Digits 2-7: Birthdate (`YYMMDD`)
   - Digits 8-9: Governorate code (`01` = Cairo, `12` = Qalyubia, `14` = Gharbia, etc.)
   - Digits 10-13: Sequence number
   - Digit 14: Check digit
3. **Reordering Logic**: If OCR returns text in reverse RTL order, reverse the digit sequence before committing to DCO.

## 3. Faint Dot-Matrix Ink & Stamp Preservation
Employer names such as `شركة بيوت الأزياء الراقية` (NPA form field `EmployerName2`) are printed using dot-matrix ink (red or light blue) or stamped. The National ID field `EmployerName` ("جهة العمل") was **removed** from Studio and FileNet; see `datacap-field-lifecycle-change`.
- **Avoid Heavy Binarization / Contrast Stretching**: Aggressive Otsu thresholding or high-contrast filters erode dot-matrix dots and fragment characters.
- **Pass Raw Crops**: Supply uncompressed 24-bit/32-bit PNG crops directly to the recognition model.
- **Line Segmentation**: For multi-line handwriting (for example `AccountHolderName` on the signature form), set `enable_line_segmentation` for that field in `external_crop_config.json`; keep the full bounding box so descending glyphs are not clipped. An empty returned line is retried by `RetryEmptyLine`.

## 4. Arabic Zone Padding Rules
Arabic script flows right-to-left. Letter ligatures and initial tooth extensions (`س`, `ش`, `ي`, `ص`) often extend into neighboring margins:
- **Right Padding**: Always extend the right boundary by 15-25 pixels to avoid clipping the start of names (e.g. `سعيد`).
- **Top Padding**: Add 10-15 pixels of top padding for upper diacritics and ascenders (`ا`, `ل`, `ط`, `ظ`).
- **Multi-Candidate Filtering**: If initial OCR cuts off the first word, check the `words` array in the response to rebuild the full four-part Arabic name.
