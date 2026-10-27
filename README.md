# IBM Datacap AI & Local OCR Integration Suite (v1.0.0)

Welcome to the **IBM Datacap AI & Local OCR Integration Suite**. This repository contains production-ready C# custom actions, Datacap Rulerunner rulesets, reusable agent skills, architectural blueprints, and boilerplate templates to integrate **IBM Datacap** with on-premise AI/OCR models and export classified documents directly to **IBM FileNet Content Services**.

---

## 📚 What Was Created & Pushed?

Here is a breakdown of every component in this repository, where it lives, and when you or your team need it:

```
├── .bob/skills/                               <- Reusable AI Agent Skills
│   ├── datacap-custom-action-dev/             <- Skill for authoring/compiling C# actions
│   ├── datacap-dco-ruleset-wiring/            <- Skill for DCO bindings & collection.xml rules
│   ├── local-ai-ocr-integration/              <- Skill for REST OCR endpoint communication
│   └── filenet-datacap-export-troubleshooter/ <- Skill for FileNet CEWS export & permissions
├── templates/                                 <- Code & Automation Boilerplates
│   ├── DatacapActionTemplate.cs               <- C# RRS COM reflection template
│   ├── RRXTemplate.rrx                        <- XML metadata descriptor template
│   └── BuildAndDeploy.bat                     <- Automated build & dual-deploy script
├── LocalOCRActions/                           <- Production C# Custom Action Library
│   ├── src/                                   <- C# source code (.NET Framework 4.0)
│   ├── RRS/LocalOCRActions.rrx                <- Action metadata for Datacap Studio
│   ├── CompileAndDeploy.bat                   <- Application-specific compiler script
│   └── Test-LocalOCR.ps1                      <- Standalone PowerShell test harness
├── GoldenDemo/dco_GoldenDemo/                 <- Datacap Application Configuration
│   ├── rules/localocr_pageidentify.rul        <- Ruleset 16: LocalOCR_PageIdentify
│   ├── rules/localocr_extractfields.rul       <- Ruleset 18: LocalOCR_ExtractFields
│   ├── rules/collection.xml                   <- Task profile execution sequence
│   ├── GOLDENDEMO.XML & goldendemo.xmb        <- DCO document hierarchy & rule bindings
│   └── PageID.set.xml                         <- PageID task configuration
├── RRS/ocr_settings.json                      <- Local OCR endpoint configuration
└── DATACAP_LOCAL_OCR_BLUEPRINT.md             <- Master Architectural Blueprint
```

---

## 🎯 Component Guide: What is it and When to use it?

### 1. Architectural Blueprint ([`DATACAP_LOCAL_OCR_BLUEPRINT.md`](DATACAP_LOCAL_OCR_BLUEPRINT.md))
* **What is it?** The single source of truth for the entire end-to-end architecture, lifecycle execution order, and field mappings.
* **When do you need it?** 
  - When onboarding new team members or explaining the solution to enterprise architects.
  - When designing new document types or adding new fields.
  - When troubleshooting execution order (e.g. why fields got cleared during profiling).

---

### 2. Reusable Bob Skills ([`.bob/skills/`](.bob/skills/))
These skills teach AI agents (like IBM Bob) how to automate Datacap tasks according to your organization's exact standards.

| Skill | What it teaches | When to use it |
| :--- | :--- | :--- |
| **`datacap-custom-action-dev`** | C# COM reflection, memory management (`GC.Collect`), Rulerunner return codes (`0`, `1`, `99`), and command-line compilation. | When building a new custom action `.dll` for Rulerunner. |
| **`datacap-dco-ruleset-wiring`** | How to edit `GOLDENDEMO.XML`, sync `goldendemo.xmb`, and configure `collection.xml` without wiping field data. | When adding new rulesets or changing the task profile order in Datacap Studio. |
| **`local-ai-ocr-integration`** | Handling Base64 image payloads in memory, calling `/identify` and `/recognize`, managing LoRA adapters, and routing low confidence ($< 0.80$) to Verify. | When connecting Datacap or custom scripts to the local AI OCR service. |
| **`filenet-datacap-export-troubleshooter`** | CEWS configuration, WSDL endpoints, property symbolic name mapping, and Object Store ACL permissions in FileNet ACCE. | When documents fail to export to IBM FileNet or users see permission errors in IBM Content Navigator. |

---

### 3. Developer Templates ([`templates/`](templates/))
* **[`DatacapActionTemplate.cs`](templates/DatacapActionTemplate.cs)**: Ready-to-use C# starter class with all boilerplate COM properties and garbage collection hooks already configured.
* **[`RRXTemplate.rrx`](templates/RRXTemplate.rrx)**: XML template for registering custom actions into Datacap Studio palettes.
* **[`BuildAndDeploy.bat`](templates/BuildAndDeploy.bat)**: Parameterized batch file to build and dual-deploy `.dll` and `.rrx` files directly to `C:\Datacap\RRS\` and your application's `rules\` folder.

---

### 4. Production Integration Code ([`LocalOCRActions/`](LocalOCRActions/))
* **`LocalOCR_PageIdentify` (Ruleset 16)**: Inspects incoming page images, calls `POST http://127.0.0.1:8019/identify`, classifies the page type (`Signature_Form_1`, `National ID Front`, `Form_Page_1`), and instantiates field child objects.
* **`LocalOCR_ExtractFields` (Ruleset 18)**: Executes after `CreateDocuments`, calls `POST http://127.0.0.1:8019/recognize` using specific adapters, and writes field values and character confidence strings into the runtime DCO.
* **`Test-LocalOCR.ps1`**: A standalone test harness you can run to test the local OCR API independently without launching Datacap.

---

## ⚡ Key Architecture Discovery: The Lifecycle Rule

> **CRITICAL RULE**: `CreateDocuments` MUST execute **after** `LocalOCR_PageIdentify` and **before** `LocalOCR_ExtractFields`.
> 
> If `CreateDocuments` runs after field extraction, its internal `CreateFields()` invocation re-initializes and clears all extracted values.
> 
> **Golden Order:** `VScan` $\rightarrow$ `PageID (Ruleset 16)` $\rightarrow$ `CreateDocuments` $\rightarrow$ `ExtractFields (Ruleset 18)` $\rightarrow$ `Verify` $\rightarrow$ `ExportToFileNet`.

---

## 🚀 How Your Team Can Reuse This Repository

1. **Clone the repository**:
   ```bash
   git clone https://github.com/malakelsamman/IBM-Datacap-Skill.git
   ```
2. **Copy the skills**:
   Copy `.bob/skills/*` into your team's project repository under `.bob/skills/` or into global user profiles at `~/.bob/skills/`.
3. **Build & Deploy Custom Actions**:
   Run `LocalOCRActions\CompileAndDeploy.bat` from an administrator command prompt on the Datacap server.
