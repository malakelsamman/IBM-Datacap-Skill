# Sample Form Inputs & Expected Model Outputs

> **Important Note on Strategy Context**:
> The examples below illustrate the **Tier 1 (Local AI OCR / On-Premise REST Service)** contract format using `http://127.0.0.1:8019`. 
> 
> IBM Datacap also supports **Tier 2 (Vendor / Cloud OCR APIs)** and **Tier 3 (Native Datacap OCR/A, OCR/S, OCR/PL, OMR)** as detailed in the master strategy guide: [`.bob/skills/datacap-ocr-strategy-and-integration/SKILL.md`](../../.bob/skills/datacap-ocr-strategy-and-integration/SKILL.md).
> 
> The 3 document types below (Signature Form, National ID, NPA Form) serve as **concrete representative examples** to demonstrate how any arbitrary document type (Invoices, Tax Forms, Custom IDs, etc.) can be ingested and mapped into Datacap DCO fields.

---

## 1. Example A: Signature Specimen Form

* **Sample Image File**: [`Signature_Form_Image_5.jpg`](./Signature_Form_Image_5.jpg)
* **Datacap Page Type**: `Signature_Form_1`

### Step 1: Identification Payload (`POST /identify`)
**Request Sent to Local OCR:**
```json
{
  "file_name": "Signature_Form_Image_5.jpg",
  "image_base64": "<base64-encoded-image-bytes>"
}
```
**Expected Response:**
```json
{
  "ok": true,
  "form": "Signature_Form_1",
  "page_type": "Signature_Form_1",
  "document_type": "Signature_Form",
  "confidence": 0.98,
  "review_required": false
}
```

### Step 2: Field Recognition Payload (`POST /recognize`)
**Request Sent to Local OCR:**
```json
{
  "file_name": "Signature_Form_Image_5.jpg",
  "image_base64": "<base64-encoded-image-bytes>",
  "form": "Signature_Form_1",
  "page_type": "Signature_Form_1"
}
```
**Expected Response:**
```json
{
  "ok": true,
  "extracted_fields": {
    "AccountHolderName": "AHMED MOHAMED AL-SAYED",
    "AccountNumber": "1234567890123456"
  },
  "confidence": 0.96,
  "review_required": false
}
```

---

## 2. Example B: National ID Card (Front)

* **Sample Image File**: [`National_ID_Image_6.jpg`](./National_ID_Image_6.jpg)
* **Datacap Page Type**: `National ID Front`

### Step 1: Identification Payload (`POST /identify`)
**Expected Response:**
```json
{
  "ok": true,
  "form": "National ID Front",
  "page_type": "National ID Front",
  "document_type": "National_ID",
  "confidence": 0.95,
  "review_required": false
}
```

### Step 2: Field Recognition Payload (`POST /recognize`)
**Expected Response:**
```json
{
  "ok": true,
  "extracted_fields": {
    "FullNameArabic": "أحمد محمد السيد",
    "FamilyNameArabic": "السيد",
    "AddressArabic": "القاهرة - مصر",
    "NationalIdNumber": "29001011234567",
    "EmployerName": "شركة البريد المصري"
  },
  "confidence": 0.91,
  "review_required": false
}
```

---

## 3. Example C: NPA Application Form

* **Sample Image File**: [`NPA_Form_Image_2.jpg`](./NPA_Form_Image_2.jpg)
* **Datacap Page Type**: `Form_Page_1`

### Step 1: Identification Payload (`POST /identify`)
**Expected Response:**
```json
{
  "ok": true,
  "form": "Form_Page_1",
  "page_type": "Form_Page_1",
  "document_type": "NPA_Application",
  "confidence": 0.94,
  "review_required": false
}
```

### Step 2: Field Recognition Payload (`POST /recognize`)
**Expected Response:**
```json
{
  "ok": true,
  "extracted_fields": {
    "EmployerName2": "الهيئة القومية للبريد",
    "AvrgMonthlyIncome": "Less Than 10,000"
  },
  "confidence": 0.89,
  "review_required": false
}
```

---

## 4. How This Generalizes to Any Other Use Case

To process a new document (e.g. an **Invoice** or **Commercial Contract**):
1. **Model Backend**: Ensure the OCR model / LoRA / Prompt recognizes the new layout and outputs:
   - `page_type`: e.g., `Vendor_Invoice`
   - `extracted_fields`: e.g., `{"InvoiceNumber": "INV-9901", "TotalAmount": "4,500.00"}`
2. **Datacap DCO**: In `GOLDENDEMO.XML`, add a Page `<P type="Vendor_Invoice">` with child fields `<F type="InvoiceNumber">` and `<F type="TotalAmount">`.
3. **Execution**: Datacap dynamically routes the image, creates the fields, and populates the data without any C# action re-compilation.
