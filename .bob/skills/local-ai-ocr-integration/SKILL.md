---
name: local-ai-ocr-integration
description: Integrate local or external vision and OCR REST endpoints with Datacap pipelines, handling Base64 payloads, document_type constraints, word alternatives, and verification routing.
---

# Local & External AI OCR Integration Guide

## 1. REST Endpoint Specifications & Capabilities

### A. Pre-Flight Diagnostic Endpoints
Before calling recognition, always query diagnostic endpoints:
- `GET /alive`: `{"message": "alive"}`
- `GET /health_checks`: `{"server_status": "alive", "uptime": "..."}`
- `GET /get_supported_languages`: Returns supported language and model modes:
  ```json
  {
    "supported_languages": {
      "handwritten": ["Arabic/Latest"]
    }
  }
  ```

### B. Field-Level Recognition Endpoints

#### 1. JSON Base64 Payload (`POST /recognize_base64`)
```json
{
  "file": "<Base64_Encoded_Crop_PNG>",
  "language": "Arabic/Latest",
  "document_type": "handwritten",
  "batch_size": 1
}
```

#### 2. Multipart Form Data (`POST /recognize`)
- `file`: Binary image (PNG/JPEG) of cropped field
- `language`: `"Arabic/Latest"`
- `document_type`: `"handwritten"`
- `batch_size`: `1`

> ⚠️ **Engine Constraint Warning:** If an endpoint's model is specialized for handwriting (as indicated by `/get_supported_languages`), sending `document_type: "typewritten"` can result in **HTTP 400 Bad Request**. Always route typewritten fields to Datacap built-in OCR/PL (`RecognizeFieldOCRPL`) or OCR/A.

---

## 2. Standard Response Parsing Hierarchy
Vendor OCR endpoints return nested JSON structures containing page, zone, paragraph, line, and word objects:

```csharp
// Response path: pages[0].zones[*].paragraphs[*].lines[*].text
var respDict = serializer.Deserialize<Dictionary<string, object>>(respJson);
if (respDict.ContainsKey("pages")) {
    var pagesList = respDict["pages"] as ArrayList;
    if (pagesList != null && pagesList.Count > 0) {
        var pageObj = pagesList[0] as Dictionary<string, object>;
        var zonesList = pageObj["zones"] as ArrayList;
        var textLines = new List<string>();

        if (zonesList != null) {
            foreach (Dictionary<string, object> zone in zonesList) {
                var paragraphs = zone["paragraphs"] as ArrayList;
                if (paragraphs != null) {
                    foreach (Dictionary<string, object> para in paragraphs) {
                        var lines = para["lines"] as ArrayList;
                        if (lines != null) {
                            foreach (Dictionary<string, object> line in lines) {
                                if (line.ContainsKey("text"))
                                    textLines.Add(Convert.ToString(line["text"]));
                            }
                        }
                    }
                }
            }
        }
        string fullText = string.Join(" ", textLines).Trim();
    }
}
```

---

## 3. Hybrid Field Routing Policy
Configure Datacap rulesets to send only relevant fields to external AI:

| Field Category | Technology Route | Action / Rule Implementation |
| :--- | :--- | :--- |
| **Handwritten Arabic** | External Vendor OCR API | `ExternalOCR_RecognizeField` (`/recognize_base64`, `handwritten`) |
| **Machine-Printed Arabic** | Built-in OCR/PL | `RecognizeFieldOCRPL` (`RecognitionOCRPL.OCRPActions`) |
| **Machine-Printed Numbers** | Built-in OCR/A | `RecognizeFieldOCR_A` (`RecognitionOCRA.OCRAActions`) |
| **Checkboxes / Choice Lists** | Datacap OMR | Built-in OMR action library |

---

## 4. Safe In-Memory Field Cropping
Never crop full pages onto disk or hold open file locks during HTTP transport. Use in-memory cloning with boundary clamping:

```csharp
public static byte[] CropFieldPng(string imagePath, Rectangle zoneRect, out int cropW, out int cropH)
{
    using (Bitmap fullImg = new Bitmap(imagePath))
    {
        int x = Math.Max(0, Math.Min(zoneRect.X, fullImg.Width - 1));
        int y = Math.Max(0, Math.Min(zoneRect.Y, fullImg.Height - 1));
        int w = Math.Max(1, Math.Min(zoneRect.Width, fullImg.Width - x));
        int h = Math.Max(1, Math.Min(zoneRect.Height, fullImg.Height - y));

        cropW = w;
        cropH = h;

        using (Bitmap crop = fullImg.Clone(new Rectangle(x, y, w, h), fullImg.PixelFormat))
        using (MemoryStream ms = new MemoryStream())
        {
            crop.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }
}
```

---

## 5. Verification Routing
- On HTTP error or empty response:
  - Do NOT guess or hallucinate field values.
  - Set `CurrentDCO.VerificationStatus = 1;` (or `CurrentDCO.Status = 1`).
  - Log error telemetry with endpoint and HTTP status code.
