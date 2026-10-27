# IBM Datacap AI & Local OCR Integration Suite (v2.0.0)

Welcome to the **IBM Datacap AI & Local OCR Integration Suite**. This repository contains production-ready C# custom actions, Datacap Rulerunner rulesets, reusable agent skills, architectural blueprints, and boilerplate templates to integrate **IBM Datacap** with on-premise AI/OCR models, process complex Arabic forms and Egyptian National IDs, and export classified documents directly to **IBM FileNet Content Services**.

---

## 📚 Repository Structure & Components

```
├── .bob/skills/                               <- Reusable AI Agent Skills (7 Skills)
│   ├── datacap-arabic-document-processing/    <- Skill: Arabic RTL encoding, IDs, stamps & padding (NEW)
│   ├── datacap-fingerprint-zone-calibration/  <- Skill: Fingerprint zones, setup DCO & coordinate fallback (NEW)
│   ├── datacap-rrs-diagnostic-debugger/       <- Skill: RRS log analysis, error 17000008 & field drops (NEW)
│   ├── datacap-custom-action-dev/             <- Skill: C# action authoring, character nodes & dual-deploy
│   ├── datacap-dco-ruleset-wiring/            <- Skill: DCO bindings, hybrid OCR routing & collection.xml
│   ├── local-ai-ocr-integration/              <- Skill: REST endpoint communication, Base64 & alternatives
│   └── filenet-datacap-export-troubleshooter/ <- Skill: FileNet CEWS export, WSDL & security ACLs
├── templates/                                 <- Code & Automation Boilerplates
│   ├── DatacapActionTemplate.cs               <- C# RRS COM reflection template
│   ├── RRXTemplate.rrx                        <- XML metadata descriptor template
│   └── BuildAndDeploy.bat                     <- Automated build & dual-deploy script
├── ExternalOCRActions/                        <- External/Vendor OCR C# Action Library
│   ├── src/                                   <- C# source code (.NET Framework 4.0)
│   │   ├── ExternalOCRCommon.cs               <- XML zone fallback, crop resolution & candidate parser
│   │   ├── ExternalOCR_RecognizeField.cs      <- Field-level OCR and character node builder
│   │   ├── ExternalOCR_FileNamePageIdentify.cs<- Page identification & FingerprintID binder
│   │   └── external_crop_config.json          <- Absolute & ratio crop fallback definitions
│   ├── RRS/ExternalOCRActions.rrx             <- Action metadata for Datacap Studio
│   └── CompileAndDeploy.bat                   <- Compiler and dual-deployment script
├── LocalOCRActions/                           <- Local AI OCR C# Action Library
│   ├── src/                                   <- C# source code (.NET Framework 4.0)
│   ├── RRS/LocalOCRActions.rrx                <- Action metadata for Datacap Studio
│   └── CompileAndDeploy.bat                   <- Application-specific compiler script
├── GoldenDemo/dco_GoldenDemo/                 <- Datacap Application Configuration
│   ├── rules/externalocr_extractandrecognize.rul <- Ruleset: Hybrid field recognition routing
│   ├── rules/externalocr_pageidentify.rul     <- Ruleset: Page ID & template binding
│   ├── rules/collection.xml                   <- Task profile execution sequence
│   ├── GOLDENDEMO.XML & goldendemo.xmb        <- DCO document hierarchy & rule bindings
│   └── PageID.set.xml                         <- PageID task configuration
├── RRS/ocr_settings.json                      <- OCR endpoint configuration
└── DATACAP_LOCAL_OCR_BLUEPRINT.md             <- Master Architectural Blueprint
```

---

## 🎯 Reusable Agent Skills Overview ([`.bob/skills/`](.bob/skills/))

These skills teach AI agents (like IBM Bob) how to automate, troubleshoot, and build Datacap tasks according to enterprise standards:

| Skill | Description & Key Topics | When to Use |
| :--- | :--- | :--- |
| **`datacap-arabic-document-processing`** *(NEW)* | RTL character encoding with decimal Unicode `<C>` nodes, 14-digit Egyptian National ID normalization, faint dot-matrix stamp preservation (`EmployerName`), and Arabic glyph boundary padding. | When processing Arabic documents, National IDs, bank forms, or multi-line stamps. |
| **`datacap-fingerprint-zone-calibration`** *(NEW)* | Synchronizing Fingerprint XMLs (`568.xml`, `569.xml`, `570.xml`), Setup DCO (`GOLDENDEMO.XML`), and handling runtime `Position: 0,0,0,0` through direct XML lookup. | When drawing or fine-tuning field zones in Datacap Studio or debugging unaligned crops. |
| **`datacap-rrs-diagnostic-debugger`** *(NEW)* | Troubleshooting Rulerunner failures, resolving Error `17000008` (`CScript::AddXMLXSL`), analyzing `pageid_rrs.log`, and fixing empty/dropped field values. | When tasks fail, actions cannot be located, or field values are missing in Verification. |
| **`datacap-custom-action-dev`** | Building C# COM reflection actions, managing memory with `GC.Collect()`, generating `<C>` character nodes with confidence codes, and dual-deploying to `C:\Datacap\RRS\` and `dco_<App>\rules\`. | When developing or compiling new custom `.dll` action libraries for Datacap. |
| **`datacap-dco-ruleset-wiring`** | Hybrid OCR routing (handwritten Arabic to AI/vendor OCR, numbers to OCR/A), explicit `FingerprintID` binding, and ruleset sequencing in `collection.xml`. | When binding rulesets to page types or configuring task profiles without clearing runtime fields. |
| **`local-ai-ocr-integration`** | Integrating local (`:8019`) and external (`/recognize_base64`) endpoints, memory-safe image cropping (`File.ReadAllBytes`), `document_type: "handwritten"` enforcement, and evaluating alternative word arrays. | When integrating REST vision/OCR endpoints with Datacap pipelines. |
| **`filenet-datacap-export-troubleshooter`** | CEWS SOAP configuration, WSDL endpoints, property symbolic name mapping, and Object Store ACL permissions in FileNet ACCE. | When documents fail to export to IBM FileNet or users encounter permission errors in IBM Content Navigator. |

---

## ⚡ Core Architecture Patterns

### 1. The Execution Lifecycle Rule
To prevent field clearing caused by re-instantiation:
$$\text{VScan} \longrightarrow \text{PageID (Set Page Type + FingerprintID)} \longrightarrow \text{CreateDocuments} \longrightarrow \text{ExtractFields} \longrightarrow \text{Verify} \longrightarrow \text{ExportToFileNet}$$

### 2. Hybrid OCR Routing Architecture
* **Handwritten & Arabic Text Fields** (`FullNameArabic`, `FamilyNameArabic`, `AddressArabic`, `EmployerName`, `AccountHolderName`, `AvrgMonthlyIncome`): Routed to custom action calling local AI / vendor OCR with `"document_type": "handwritten"`.
* **Machine-Printed Digits** (`NationalIdNumber`, `AccountNumber`): Routed to `net:RecognitionOCRA.OCRAActions.RecognizeFieldOCR_A`.

### 3. Automatic Fingerprint XML Fallback
If runtime fields are created with `Position = 0,0,0,0` (due to missing template inheritance), custom actions query the master fingerprint definition (`fingerprint/<ID>.xml`) directly to retrieve exact zone coordinates.

---

## 🚀 How to Use & Deploy

1. **Clone the repository**:
   ```bash
   git clone https://github.com/malakelsamman/IBM-Datacap-Skill.git
   ```
2. **Copy the skills**:
   Copy `.bob/skills/*` into your workspace `.bob/skills/` or into global user profiles at `~/.bob/skills/`.
3. **Build & Deploy Custom Actions**:
   Run `ExternalOCRActions\CompileAndDeploy.bat` or `LocalOCRActions\CompileAndDeploy.bat` from an administrator command prompt on the Datacap server.
