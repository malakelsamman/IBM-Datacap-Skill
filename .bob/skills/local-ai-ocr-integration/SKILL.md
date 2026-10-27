---
name: local-ai-ocr-integration
description: General pattern and instructions to integrate ANY local or remote vision, OCR, or multimodal AI REST model with document ingestion workflows (Datacap, custom pipelines, etc.), handling Base64 payloads, confidence thresholds, and dynamic review routing.
---

# General Local AI & OCR Model Integration Skill

This skill defines the universal design pattern for integrating any vision, OCR, or multimodal Large Language Model (LLM) REST service with document ingestion engines (such as IBM Datacap, Kofax, or custom .NET/Python pipelines).

---

## 1. Universal REST Service Contract Pattern

Any local vision/OCR model should expose two standard lifecycle endpoints:

### Endpoint A: Classification / Identification (`POST /identify`)
* **Purpose**: Inspects visual layout and text cues to determine document/page category.
* **General Request Schema**:
  ```json
  {
    "file_name": "document_image.jpg",
    "image_base64": "<BASE64_ENCODED_IMAGE_STRING>"
  }
  ```
* **General Response Schema**:
  ```json
  {
    "ok": true,
    "page_type": "<IDENTIFIED_PAGE_TYPE>",
    "document_type": "<OPTIONAL_PARENT_DOC_TYPE>",
    "confidence": 0.95,
    "review_required": false
  }
  ```

### Endpoint B: Data Extraction / Recognition (`POST /recognize`)
* **Purpose**: Extracts key-value field data from the classified image using domain-specific models, LoRA adapters, or prompt templates.
* **General Request Schema**:
  ```json
  {
    "file_name": "document_image.jpg",
    "image_base64": "<BASE64_ENCODED_IMAGE_STRING>",
    "page_type": "<IDENTIFIED_PAGE_TYPE>"
  }
  ```
* **General Response Schema**:
  ```json
  {
    "ok": true,
    "extracted_fields": {
      "Field_1": "Extracted text or numeric value",
      "Field_2": "Another extracted value"
    },
    "confidence": 0.92,
    "review_required": false
  }
  ```

---

## 2. Generalization Principles (Applicable to Any New Document Type)

When adding a **new document type** (e.g. Invoices, Passports, Tax Forms, Custom Applications):

1. **Do not hardcode document types in C#**:
   - The integration action reads whatever `page_type` is returned by the AI model.
   - It maps `extracted_fields` keys directly to target DCO fields of the same name.

2. **DCO Hierarchy Alignment**:
   - In your document setup hierarchy (e.g. `GOLDENDEMO.XML`), create a Page Type matching `<page_type>` returned by your AI service.
   - Create child `<F>` field elements corresponding to the keys returned in `extracted_fields`.

3. **Multi-Model / LoRA Adapter Routing**:
   - The model backend can activate specialized LoRA adapters or prompt templates dynamically based on `page_type` without requiring code changes in Datacap.

4. **Confidence-Based Human-in-the-Loop Routing**:
   - Apply a universal confidence rule:
     ```
     IF confidence < Threshold (default 0.80) OR review_required == true OR field_is_empty
     THEN set TargetNode.Status = 1 (Route to Human Review / Verify)
     ELSE set TargetNode.Status = 0 (Auto-pass)
     ```

---

## 3. Representative Examples & References

For concrete examples of this pattern in practice (Signature cards, ID cards, Application forms), inspect:
- [`examples/sample_forms/README.md`](../../examples/sample_forms/README.md)
- [`DATACAP_LOCAL_OCR_BLUEPRINT.md`](../../DATACAP_LOCAL_OCR_BLUEPRINT.md)
