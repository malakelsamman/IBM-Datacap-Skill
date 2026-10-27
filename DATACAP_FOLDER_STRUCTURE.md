# IBM Datacap Root Directory Guide (`C:\Datacap`)

This document provides a comprehensive structural overview of the `C:\Datacap` environment, explaining what each folder, application, and system component contains and its role in the document processing pipeline.

---

## 📁 1. Active Integration & Repository Assets

| Directory / File | Description & Purpose |
| :--- | :--- |
| **`.bob/`** | Contains AI Agent Skills (`.bob/skills/`), including Datacap custom action development, DCO ruleset wiring, universal OCR integration, and FileNet export diagnostics. |
| **`templates/`** | Boilerplate C# actions (`DatacapActionTemplate.cs`), XML action descriptors (`RRXTemplate.rrx`), and build scripts (`BuildAndDeploy.bat`). |
| **`examples/`** | Reference form images (`.jpg`) and input/output JSON payload specifications (`examples/sample_forms/`). |
| **`LocalOCRActions/`** | C# source code, compilation scripts, test harness (`Test-LocalOCR.ps1`), and RRX descriptors for the on-premise local OCR integration (`http://127.0.0.1:8019`). |
| **`DATACAP_LOCAL_OCR_BLUEPRINT.md`** | Master architectural blueprint detailing the end-to-end execution pipeline, DCO lifecycle, and FileNet mapping. |
| **`README.md`** | Main entry documentation for the Datacap AI & Local OCR Integration Suite. |

---

## 🏛️ 2. Datacap Applications & Sample Projects

| Directory / File | Description & Purpose |
| :--- | :--- |
| **`GoldenDemo/`** | Primary enterprise demonstration application containing full DCO hierarchies (`GOLDENDEMO.XML`), rulesets (`dco_GoldenDemo/rules/`), batch processing workspaces, and FileNet export configurations. |
| **`APT/`** | Accounts Payable / Invoice Processing template application. |
| **`TravelDocs/`** | Travel & Expense document processing sample application. |
| **`Flex/`** | Flexible / dynamic form recognition sample application. |
| **`Vet/`** | Veterinary claims sample application. |
| **`Transaction/`** | Transactional document processing sample application. |
| **`Templates/`** | Datacap Studio application templates (e.g., `FormTemplate`, `LearningTemplate`). |

---

## ⚙️ 3. Rulerunner, Engine & Shared Runtime Libraries

| Directory / File | Description & Purpose |
| :--- | :--- |
| **`RRS/`** | **Rulerunner Service (RRS) Action Library Directory**: Contains all native Datacap action assemblies (`.dll`), `.rrx` action headers, recognition engines (OCR/A, OCR/S, OCR/PL, OMR), language resource packs, and configuration files (`ocr_settings.json`). |
| **`CentralRulerunnerService/`** | Rulerunner background processing service host and thread managers. |
| **`dcshared/` & `dcshared_x64/`** | Core shared libraries, COM interop wrappers, and utilities for 32-bit and 64-bit Datacap operations. |
| **`ProgramSet/`** | Datacap core program assemblies and engine configuration sets. |
| **`TaskRun/`** | Task execution runner binaries and runtime cache. |

---

## 🖥️ 4. Datacap Client & Server Infrastructure

| Directory / File | Description & Purpose |
| :--- | :--- |
| **`Taskmaster/` & `Taskmaster_x64/`** | Datacap Taskmaster Server service, batch database controllers, and background job engine. |
| **`DStudio/`** | **Datacap Studio**: Integrated Development Environment (IDE) for building rulesets, configuring DCO hierarchies, and testing task profiles. |
| **`DcDesktop/`** | **Datacap Desktop**: Client interface for document scanning, verification, and manual indexing. |
| **`FastDoc/`** | Simplified client application for quick form template configuration and batch scanning. |
| **`FingerprintService/`** | Background service for caching and matching document fingerprints (`.fcf` / `.tif`). |
| **`wTM/`** | **Datacap Web Taskmaster (wTM)**: REST API gateway allowing external web portals and microservices to create batches, upload files, and poll processing status. |
| **`RV2/`** | **Datacap Report Viewer**: Web-based reporting and analytics dashboard for batch status and throughput. |
| **`tmweb.net/` & `tmweb.java/`** | Web client components for browser-based scanning and verification. |

---

## 🌐 5. Web Portals & Custom Integrations

| Directory / File | Description & Purpose |
| :--- | :--- |
| **`EgyptPostPortal/`** | Custom web ingestion and verification frontend portal integrated with Datacap wTM. |
| **`watsonxai/` & `WatsonaiRedaction/`** | IBM watsonx.ai generative AI integration rulesets and redaction components. |
| **`AutomationDocumentProcessing/`** | Connectors and configurations for Cloud Pak for Business Automation (CP4BA) / ADP. |
| **`ExternalOCRActions/` & `FieldOCRActions/`** | Previous vendor/cloud external OCR action source and configuration archives. |
| **`Bob_Local_OCR_Datacap_Handoff_Final/`** | Reference handoff folder containing local OCR service specifications, prompt guides, and sample images. |

---

## 📄 6. Root Configuration & Utility Files

| File | Description & Purpose |
| :--- | :--- |
| **`datacap.xml`** | Master Datacap application registry (maps application names to their folder paths, admin databases, and engine databases). |
| **`dc_start.bat` & `dc_stop.bat`** | Batch scripts to start and stop all Datacap Windows services (Taskmaster, Rulerunner, wTM). |
| **`dco.xsl`** | XSL stylesheet for rendering Datacap DCO batch and setup XMLs. |
| **`version.txt`** | Datacap platform version and build stamp. |
| **`tms.log.*`** | Datacap Taskmaster server runtime logs. |
