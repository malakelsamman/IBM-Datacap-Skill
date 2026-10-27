---
name: local-ai-ocr-integration
description: Integrate local or external vision and OCR REST endpoints with Datacap pipelines, handling Base64 payloads, document_type constraints, word alternatives, and verification routing.
---

# Local & External AI OCR Integration Guide

## 1. REST Endpoint Specifications

### A. Local Vision/AI Model Service (`http://127.0.0.1:8019`)
- `GET /health`: Health status `{"status": "healthy"}`.
- `GET /model-info`: Active LoRA adapters (`account_holder_name`, `employer_name`).
- `POST /identify`: `{"file_name": "...", "image_base64": "..."}` -> Returns `{"ok": true, "form": "...", "page_type": "...", "confidence": 0.95}`.
- `POST /recognize`: `{"file_name": "...", "image_base64": "...", "form": "...", "page_type": "..."}` -> Returns `{"ok": true, "extracted_fields": {"FieldName": "ExtractedValue"}}`.

### B. External Vendor OCR Service (`/recognize_base64`)
- **Payload Format**:
  ```json
  {
    "image_base64": "...",
    "mime_type": "image/png",
    "document_type": "handwritten",
    "language": "ar"
  }
  ```
- ⚠️ **Critical Constraint:** Always specify `"document_type": "handwritten"`. Specifying `"typewritten"` may fail with HTTP 400.

## 2. Word Alternatives Inspection & Scoring
External OCR engines provide multiple recognition candidates for individual words in the `words` array. When primary text contains low-confidence tokens or noise:

```csharp
// Evaluate top candidate words from OCR response
if (responseObj.ContainsKey("words")) {
    var wordsList = responseObj["words"] as ArrayList;
    var candidateWords = new List<string>();
    foreach (Dictionary<string, object> wordItem in wordsList) {
        if (wordItem.ContainsKey("text")) {
            candidateWords.Add(wordItem["text"].ToString());
        }
    }
    // Reconstruct normalized full text from best word candidates
}
```

## 3. Safe In-Memory Cropping
Never hold open file locks on page TIFF/JPEG files during recognition:

```csharp
public static byte[] CropFieldPng(string imagePath, int left, int top, int right, int bottom)
{
    byte[] fileBytes = File.ReadAllBytes(imagePath);
    using (var ms = new MemoryStream(fileBytes))
    using (var srcBmp = new Bitmap(ms))
    {
        int w = Math.Max(1, right - left);
        int h = Math.Max(1, bottom - top);
        var cropRect = new Rectangle(left, top, w, h);

        using (var croppedBmp = srcBmp.Clone(cropRect, srcBmp.PixelFormat))
        using (var outMs = new MemoryStream())
        {
            croppedBmp.Save(outMs, System.Drawing.Imaging.ImageFormat.Png);
            return outMs.ToArray();
        }
    }
}
```

## 4. Verification Routing
- If `review_required == true` OR `fallback_used == true` OR `confidence < 0.80`:
  - Set DCO field status: `CurrentDCO.Status = 1;`
  - Write detailed warning to `RRLog`.
