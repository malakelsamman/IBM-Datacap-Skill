# IBM Datacap AI & Local OCR Integration Suite (v2.1.0)

Welcome to the **IBM Datacap AI & Local OCR Integration Suite**. This repository contains enterprise-grade C# custom actions, Datacap Rulerunner rulesets, reusable agent skills, architectural blueprints, and boilerplate templates to integrate **IBM Datacap** with on-premise AI/OCR models and external vendor OCR REST APIs, process complex Arabic forms and Egyptian National IDs, and export classified documents directly to **IBM FileNet Content Services**.

---

## 📚 Repository Structure & Components

```
├── .bob/skills/                               <- Reusable AI Agent Skills (10 Skills)
│   ├── datacap-parallel-pipeline-migration/   <- [NEW] Parallel POC & non-destructive migration blueprints
│   ├── datacap-filename-page-identification/  <- [NEW] Fast non-OCR filename page classification & VScan parsing
│   ├── vendor-ocr-rest-client-actions/        <- [NEW] Cropped field REST client, Base64 & DCO Unicode <C> nodes
│   ├── datacap-arabic-document-processing/    <- Arabic RTL encoding, National IDs, stamps & padding
│   ├── datacap-fingerprint-zone-calibration/  <- Fingerprint zones, setup DCO & normalized ratio fallbacks
│   ├── datacap-rrs-diagnostic-debugger/       <- RRS log analysis, error 17000008 & field drops
│   ├── datacap-custom-action-dev/             <- C# action authoring, VScan.xml resolution & deploy lifecycle
│   ├── datacap-dco-ruleset-wiring/            <- DCO bindings, semicolon dcomap syntax & sequencing
│   ├── local-ai-ocr-integration/              <- Hybrid OCR routing, document_type constraints & alternatives
│   └── filenet-datacap-export-troubleshooter/ <- FileNet CEWS export, WSDL & security ACLs
├── templates/                                 <- Code & Automation Boilerplates
│   ├── DatacapActionTemplate.cs               <- C# RRS COM reflection template
│   ├── RRXTemplate.rrx                        <- XML metadata descriptor template
│   └── BuildAndDeploy.bat                     <- Automated build & dual-deploy script
├── ExternalOCRActions/                        <- External/Vendor OCR C# Action Library
│   ├── src/                                   <- C# source code (.NET Framework 4.0/4.8)
│   │   ├── ExternalOCRCommon.cs               <- XML zone fallback, crop resolution & candidate parser
│   │   ├── ExternalOCR_RecognizeField.cs      <- Field-level OCR and character node builder
│   │   ├── ExternalOCR_FileNamePageIdentify.cs<- Page identification & VScan source filename resolver
│   │   ├── ExternalOCR_TestConnection.cs      <- Pre-flight health and diagnostic tester
│   │   ├── external_crop_config.json          <- Absolute & ratio crop fallback definitions
│   │   └── external_ocr_settings.json         <- Endpoint and runtime settings
│   ├── RRS/ExternalOCRActions.rrx             <- Action metadata for Datacap Studio
│   └── CompileAndDeploy.bat                   <- Compiler and dual-deployment script
├── LocalOCRActions/                           <- Local AI OCR C# Action Library
│   ├── src/                                   <- C# source code (.NET Framework 4.0)
│   ├── RRS/LocalOCRActions.rrx                <- Action metadata for Datacap Studio
│   └── CompileAndDeploy.bat                   <- Application-specific compiler script
├── GoldenDemo/dco_GoldenDemo/                 <- Datacap Application Configuration
│   ├── rules/externalocr_extractandrecognize.rul <- Ruleset 22: Hybrid field recognition routing
│   ├── rules/externalocr_createdoc.rul        <- Ruleset 21: Batch-level document assembly
│   ├── rules/externalocr_pageidentify.rul     <- Ruleset 20: Filename-based page identification
│   ├── rules/collection.xml                   <- Task profile execution sequence
│   ├── GOLDENDEMO.XML & goldendemo.xmb        <- DCO document hierarchy & rule bindings
│   └── PageID_VendorOCR_POC.set.xml           <- Parallel POC task configuration
├── Test-VendorOCR-POC.ps1                     <- End-to-end verification script
└── DATACAP_LOCAL_OCR_BLUEPRINT.md             <- Master Architectural Blueprint
```

---

## 🎯 Reusable Agent Skills Overview ([`.bob/skills/`](.bob/skills/))

These 10 skills teach AI agents (like IBM Bob) how to automate, troubleshoot, and build Datacap tasks according to enterprise standards:

| Skill | Description & Key Topics | When to Use |
| :--- | :--- | :--- |
| **`datacap-parallel-pipeline-migration`** *(NEW)* | Blueprints for zero-collision parallel POC workflows in Datacap without modifying or breaking legacy actions, rulesets, DLLs, or task profiles. | When migrating to a new OCR engine or adding a parallel POC flow. |
| **`datacap-filename-page-identification`** *(NEW)* | Fast page classification from client filenames or metadata via `VScan.xml` parsing without calling OCR or template matching. | When ingesting controlled file batches or avoiding expensive OCR on page classification. |
| **`vendor-ocr-rest-client-actions`** *(NEW)* | Implementing custom C# field-level REST API clients for external OCR services with in-memory zone cropping and Unicode decimal `<C>` serialization. | When building field-level OCR actions that communicate with remote AI/OCR endpoints. |
| **`datacap-dco-ruleset-wiring`** | Semicolon delimiter syntax for `dcomap.open="F=A;F=B"`, separating batch-level `CreateDocuments()` from page-level `CreateFields()`, and ruleset sequencing. | When binding rulesets to page/field types or configuring task profiles in `collection.xml`. |
| **`datacap-custom-action-dev`** | Building C# COM reflection actions, managing memory with `GC.Collect()`, `VScan.xml` filename resolution, privacy telemetry, and service stop/deploy lifecycles. | When developing, compiling, or dual-deploying new custom `.dll` action libraries for Datacap. |
| **`local-ai-ocr-integration`** | Pre-flight diagnostics (`/alive`, `/health_checks`, `/get_supported_languages`), handling `document_type="handwritten"` engine constraints, and hybrid field routing. | When integrating REST vision/OCR endpoints with Datacap pipelines. |
| **`datacap-fingerprint-zone-calibration`** | Synchronizing Fingerprint XMLs (`568.xml`, `569.xml`, `570.xml`), normalized ratio crop fallbacks (`external_crop_config.json`), and coordinate clamping. | When drawing field zones in Datacap Studio or resolving `Position: 0,0,0,0` at runtime. |
| **`datacap-arabic-document-processing`** | RTL character encoding with decimal Unicode `<C>` nodes, 14-digit Egyptian National ID normalization, dot-matrix stamp preservation, and Arabic glyph padding. | When processing Arabic documents, National IDs, bank forms, or multi-line stamps. |
| **`datacap-rrs-diagnostic-debugger`** | Troubleshooting Rulerunner failures, resolving Error `17000008` (`CScript::AddXMLXSL`), analyzing `pageid_rrs.log`, and fixing empty/dropped field values. | When tasks fail, actions cannot be located, or field values are missing in Verification. |
| **`filenet-datacap-export-troubleshooter`** | CEWS SOAP configuration, WSDL endpoints, property symbolic name mapping, and Object Store ACL permissions in FileNet ACCE. | When documents fail to export to IBM FileNet or users encounter permission errors in Content Navigator. |

---

## ⚡ Core Architecture Patterns

### 1. The Execution Lifecycle Rule
To prevent field clearing and duplicate node instantiation:
$$\text{VScan} \longrightarrow \text{PageID (Identify by Filename)} \longrightarrow \text{CreateDocuments (Batch Level)} \longrightarrow \text{ExtractFields (Page Setup + Field OCR)} \longrightarrow \text{Verify} \longrightarrow \text{ExportToFileNet}$$

### 2. Semicolon DCO Map Syntax
```xml
<!-- ✅ CORRECT: Semicolon delimited -->
<rule name="Handwritten Vendor OCR Routing" id="2" dcomap.open="F=AccountHolderName;F=AccountNumber;F=EmployerName2">
  <func name="Recognize Field">
    <a name="Execute" ns="net:ExternalOCRActions.ExternalOCR_RecognizeField" />
  </func>
</rule>
```

### 3. Hybrid OCR Routing Architecture
* **Handwritten Arabic Fields** (`AccountHolderName`, `AccountNumber`, `EmployerName2`): Routed to `ExternalOCR_RecognizeField` calling the Vendor OCR REST API (`/recognize_base64`, `document_type="handwritten"`, `Arabic/Latest`).
* **Machine-Printed Fields** (`FullNameArabic`, `FamilyNameArabic`, `AddressArabic`, `NationalIdNumber`, `EmployerName`): Routed to built-in Datacap OCR/PL (`RecognizeFieldOCRPL`) or OCR/A (`RecognizeFieldOCR_A`).
* **Checkbox Fields** (`AvrgMonthlyIncome`): Preserved under Datacap OMR.

---

## 🚀 How to Build & Deploy

1. **Build and Deploy Actions**:
   Run `ExternalOCRActions\CompileAndDeploy.bat` from an administrator command prompt. The script cleanly stops Datacap services, builds with .NET Framework 4.8 `csc.exe`, deploys to `C:\Datacap\RRS\` and `GoldenDemo\dco_GoldenDemo\rules\`, and restarts the services.
2. **Execute End-to-End Verification**:
   Run `Test-VendorOCR-POC.ps1` in PowerShell to test API health, field cropping, recognition, and legacy flow isolation.
