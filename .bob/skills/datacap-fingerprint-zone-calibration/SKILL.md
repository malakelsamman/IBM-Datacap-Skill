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
3. **External Crop Fallback Config (`external_crop_config.json`)**: Ratio-based and absolute bounding box fallbacks used when no fingerprint is matched or `Position="0,0,0,0"`.

---

## 2. Standard Normalized Field Coordinates (`external_crop_config.json`)
When zones are not yet bound, use normalized ratios (0.0 to 1.0) relative to full image width and height.

**Do not copy numbers from this skill.** The live values are in `ExternalOCRActions\src\external_crop_config.json` (deployed copies in `C:\Datacap\RRS` and the app `rules` folder). Earlier sample values in this guide were stale (for example `AccountHolderName` x 0.23 vs the real x 0.5584) and have been removed.

Schema, per page type and field:
```json
{ "fields": { "<PageType>": { "<FieldId>": {
    "x": 0.0, "y": 0.0, "width": 0.0, "height": 0.0,
    "document_type": "handwritten", "language": "Arabic/Latest",
    "enable_line_segmentation": true } } } }
```
- `document_type` is `handwritten` for every field the vendor engine receives. The vendor model rejects `typewritten` with HTTP 400 (see `local-ai-ocr-integration`); the current file uses `handwritten` even for National ID fields.
- `enable_line_segmentation` is optional per field and overrides the global setting (see `vendor-ocr-rest-client-actions` section 1b).
- After editing, copy to all three locations and restart Rulerunner.

---

## 3. Resolving Pixel Coordinates at Runtime with Clamping
Custom actions must resolve pixel bounding boxes safely by following this priority order:

```csharp
public static Rectangle ResolveFieldPixelZone(dynamic fieldDco, string pageType, string fieldId, int imgWidth, int imgHeight, int paddingPx)
{
    // 1. Try reading DCO Position variable
    try {
        string posStr = Convert.ToString(fieldDco.get_Variable("Position"));
        if (!string.IsNullOrEmpty(posStr)) {
            string[] parts = posStr.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4) {
                int l = int.Parse(parts[0]), t = int.Parse(parts[1]), r = int.Parse(parts[2]), b = int.Parse(parts[3]);
                if (r > l && b > t) {
                    int px = Math.Max(0, l - paddingPx);
                    int py = Math.Max(0, t - paddingPx);
                    int pw = Math.Min(imgWidth - px, (r - l) + 2 * paddingPx);
                    int ph = Math.Min(imgHeight - py, (b - t) + 2 * paddingPx);
                    return new Rectangle(px, py, pw, ph);
                }
            }
        }
    } catch { }

    // 2. Fallback to calibrated normalized ratio
    FieldZone fallback = GetFallbackFieldZone(pageType, fieldId);
    if (fallback != null) {
        int x = (int)(fallback.X * imgWidth) - paddingPx;
        int y = (int)(fallback.Y * imgHeight) - paddingPx;
        int w = (int)(fallback.Width * imgWidth) + 2 * paddingPx;
        int h = (int)(fallback.Height * imgHeight) + 2 * paddingPx;

        x = Math.Max(0, Math.Min(x, imgWidth - 1));
        y = Math.Max(0, Math.Min(y, imgHeight - 1));
        w = Math.Max(1, Math.Min(w, imgWidth - x));
        h = Math.Max(1, Math.Min(h, imgHeight - y));

        return new Rectangle(x, y, w, h);
    }

    return new Rectangle(0, 0, imgWidth, imgHeight);
}
```
