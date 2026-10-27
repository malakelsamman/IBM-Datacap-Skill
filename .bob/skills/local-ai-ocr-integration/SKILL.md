---
name: local-ai-ocr-integration
description: Integrate local or on-premise vision and OCR REST endpoints (e.g., http://127.0.0.1:8019) with document ingestion pipelines, handling Base64 payloads, adapters, confidence scoring, and verification routing.
---

# Local AI OCR Integration Guide

## 1. REST Endpoint Specification (`http://127.0.0.1:8019`)

- `GET /health`: Returns service health status `{"status": "healthy"}`.
- `GET /model-info`: Returns model status, device, and active LoRA adapters (`account_holder_name`, `employer_name`).
- `POST /identify`:
  - Request: `{"file_name": "...", "image_base64": "..."}`
  - Response: `{"ok": true, "form": "...", "page_type": "...", "document_type": "...", "confidence": 0.95, "review_required": false}`
- `POST /recognize`:
  - Request: `{"file_name": "...", "image_base64": "...", "form": "...", "page_type": "..."}`
  - Response: `{"ok": true, "extracted_fields": {"FieldName": "ExtractedValue"}, "confidence": 0.92, "review_required": false}`

## 2. Memory-Efficient Image Processing
- Never lock image files on disk during processing.
- Read image bytes using `File.ReadAllBytes(path)` and serialize to Base64 in memory.
- Set `serializer.MaxJsonLength = int.MaxValue` in C# `JavaScriptSerializer`.

## 3. Dynamic Verification Routing
- If `review_required == true` OR `fallback_used == true` OR `confidence < 0.80`:
  - Flag the target DCO page or field: `DCO.Status = 1`.
  - Log reason clearly in Datacap RRS Log.
