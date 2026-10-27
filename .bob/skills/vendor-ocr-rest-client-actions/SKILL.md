---
name: vendor-ocr-rest-client-actions
description: Implement, configure, and execute custom C# field-level REST API clients for external OCR services with zone cropping, safe padding, and Unicode DCO character serialization.
---

# Vendor OCR REST Client Actions Guide

This guide establishes the pattern for building custom C# Datacap actions that extract text from zoned fields using external OCR REST APIs.

## 1. Action Execution Contract & Boundary Rules

### Crucial Security & Operational Rules:
1. **Never send full page images to field OCR endpoints**: Always crop strictly to the target field zone.
2. **Never log Base64 payload data or full PII**: Keep logs clean and structured.
3. **Never edit `tm*.xml` directly**: Always populate DCO values through standard Datacap DCO APIs (`CurrentDCO.Text = ...`).
4. **Never guess values on API failure**: On HTTP error or empty response, set `CurrentDCO.VerificationStatus = 1;` and return failure cleanly.

---

## 2. In-Memory Image Cropping with Safe Padding

```csharp
public static byte[] CropFieldZone(string imgPath, Rectangle zoneRect, int paddingPx, out int cropW, out int cropH)
{
    using (Bitmap fullImg = new Bitmap(imgPath))
    {
        int x = Math.Max(0, zoneRect.X - paddingPx);
        int y = Math.Max(0, zoneRect.Y - paddingPx);
        int w = Math.Min(fullImg.Width - x, zoneRect.Width + 2 * paddingPx);
        int h = Math.Min(fullImg.Height - y, zoneRect.Height + 2 * paddingPx);

        x = Math.Max(0, Math.Min(x, fullImg.Width - 1));
        y = Math.Max(0, Math.Min(y, fullImg.Height - 1));
        w = Math.Max(1, Math.Min(w, fullImg.Width - x));
        h = Math.Max(1, Math.Min(h, fullImg.Height - y));

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

## 3. Invoking `/recognize_base64` JSON API

```csharp
public static RecognitionResult CallRecognizeBase64(byte[] cropPngBytes, string baseUrl, string docType, string lang, int timeoutSec)
{
    string endpointUrl = baseUrl.TrimEnd('/') + "/recognize_base64";
    string b64Image = Convert.ToBase64String(cropPngBytes);

    var reqPayload = new Dictionary<string, object> {
        { "file", b64Image },
        { "language", lang },
        { "document_type", docType },
        { "batch_size", 1 }
    };

    var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    string json = serializer.Serialize(reqPayload);
    byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

    var request = (HttpWebRequest)WebRequest.Create(endpointUrl);
    request.Method = "POST";
    request.ContentType = "application/json; charset=utf-8";
    request.Accept = "application/json";
    request.Timeout = timeoutSec * 1000;
    request.ReadWriteTimeout = timeoutSec * 1000;
    request.ContentLength = jsonBytes.Length;

    using (var reqStream = request.GetRequestStream()) {
        reqStream.Write(jsonBytes, 0, jsonBytes.Length);
    }

    using (var response = (HttpWebResponse)request.GetResponse())
    using (var respStream = response.GetResponseStream())
    using (var reader = new StreamReader(respStream, Encoding.UTF8))
    {
        string respJson = reader.ReadToEnd();
        return ParseVendorResponse(respJson, (int)response.StatusCode);
    }
}
```

---

## 4. Response Parsing Hierarchy

```csharp
public static RecognitionResult ParseVendorResponse(string respJson, int statusCode)
{
    var res = new RecognitionResult { HttpStatusCode = statusCode, Success = false, Text = string.Empty };
    if (string.IsNullOrEmpty(respJson)) return res;

    var serializer = new JavaScriptSerializer();
    var dict = serializer.Deserialize<Dictionary<string, object>>(respJson);
    if (dict == null || !dict.ContainsKey("pages")) return res;

    var pages = dict["pages"] as ArrayList;
    if (pages == null || pages.Count == 0) return res;

    var firstPage = pages[0] as Dictionary<string, object>;
    if (firstPage == null || !firstPage.ContainsKey("zones")) return res;

    var zones = firstPage["zones"] as ArrayList;
    var linesText = new List<string>();

    if (zones != null) {
        foreach (Dictionary<string, object> zone in zones) {
            var paragraphs = zone["paragraphs"] as ArrayList;
            if (paragraphs != null) {
                foreach (Dictionary<string, object> para in paragraphs) {
                    var lines = para["lines"] as ArrayList;
                    if (lines != null) {
                        foreach (Dictionary<string, object> line in lines) {
                            if (line.ContainsKey("text"))
                                linesText.Add(Convert.ToString(line["text"]));
                        }
                    }
                }
            }
        }
    }

    res.Text = string.Join(" ", linesText).Trim();
    res.Success = true;
    return res;
}
```

---

## 5. Setting Field Values & Unicode Decimal DCO Characters

```csharp
public static void AssignFieldValue(dynamic fieldDco, string text, int confidence = 10)
{
    fieldDco.Text = text ?? string.Empty;

    // Populate child character nodes (<C>)
    try {
        int count = (int)fieldDco.NumOfChildren();
        for (int i = count - 1; i >= 0; i--) fieldDco.DeleteChild(i);
    } catch { }

    for (int i = 0; i < (text ?? "").Length; i++) {
        dynamic cNode = fieldDco.AddChild(2);
        cNode.Text = ((int)text[i]).ToString();
        cNode.set_Variable("cn", confidence.ToString());
        cNode.set_Variable("cr", "0,0,0,0");
    }
}
```
