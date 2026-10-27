---
name: datacap-filename-page-identification
description: Classify Datacap page types from controlled image filenames, scanning prefixes, or metadata without running OCR or template matching.
---

# Datacap Filename Page Identification Guide

Use this guide to implement and configure fast, non-OCR page identification in IBM Datacap based on client image filenames or batch metadata.

## 1. When to Use
- Controlled batch ingestion with predictable file naming conventions (e.g., `signature_form_01.jpg`, `npa_form_01.jpg`, `national_id_01.jpg`).
- Avoiding expensive OCR or template matching during initial page classification.
- Preserving pristine un-identified pages when names do not match controlled patterns.

---

## 2. Ingested Filename Resolution Strategy

At runtime, Datacap renames ingested page images to standard batch tokens (e.g. `TM000001.jpg`). To recover the original client filename, check the following sources in order:

```csharp
public static string ResolveSourceFileName(string pageId, dynamic batchPilot, dynamic dco, dynamic pageDco)
{
    // Source 1: VScan.xml in batch directory
    try {
        string batchDir = batchPilot != null ? Convert.ToString(batchPilot.BatchDir) : Convert.ToString(dco.Variable("BatchDir"));
        if (!string.IsNullOrEmpty(batchDir) && Directory.Exists(batchDir)) {
            string vscanPath = Path.Combine(batchDir, "VScan.xml");
            if (File.Exists(vscanPath)) {
                var doc = new XmlDocument();
                doc.Load(vscanPath);
                XmlNode pageNode = doc.SelectSingleNode("//P[@id='" + pageId + "']");
                if (pageNode != null) {
                    XmlNode srcNode = pageNode.SelectSingleNode("V[@n='ScanSrcPath']") ?? pageNode.SelectSingleNode("V[@n='PageName']");
                    if (srcNode != null && !string.IsNullOrEmpty(srcNode.InnerText))
                        return Path.GetFileName(srcNode.InnerText);
                }
            }
        }
    } catch { }

    // Source 2: Direct DCO variables
    try {
        string src = Convert.ToString(pageDco.Variable("ScanSrcPath"));
        if (!string.IsNullOrEmpty(src)) return Path.GetFileName(src);
        string name = Convert.ToString(pageDco.Variable("PageName"));
        if (!string.IsNullOrEmpty(name)) return Path.GetFileName(name);
    } catch { }

    return null;
}
```

---

## 3. Case-Insensitive Prefix Matching & Fallback Policy

```csharp
public static string MatchPageType(string fileName)
{
    if (string.IsNullOrEmpty(fileName)) return null;
    string fn = fileName.ToLowerInvariant();

    if (fn.StartsWith("signature_form") || fn.Contains("signature_form"))
        return "Signature_Form_1";

    if (fn.StartsWith("npa_form") || fn.Contains("npa_form"))
        return "Form_Page_1";

    if (fn.StartsWith("national_id") || fn.Contains("national_id") || fn.StartsWith("nationalid"))
        return "National ID Front";

    return null; // Fallback: unmatched
}
```

### Safe Fallback Rules:
- **Never default silently to a specific document type** (e.g., never default unknown files to National ID).
- Leave unclassified pages as `Other` or flag them for verification (`CurrentDCO.Status = 1`).
- Log the source filename and fallback reason clearly in the batch execution log.

---

## 4. Datacap Action Template (`ExternalOCR_FileNamePageIdentify`)

```csharp
public int Execute(string parameters)
{
    if (CurrentDCO == null) return 1;

    string pageId = Convert.ToString(CurrentDCO.ID);
    string oldPageType = Convert.ToString(CurrentDCO.Type);
    string fileName = ResolveSourceFileName(pageId, BatchPilot, DCO, CurrentDCO);

    if (string.IsNullOrEmpty(fileName)) {
        CurrentDCO.Status = 1;
        Log(string.Format("ExternalOCR_FileNamePageIdentify | PageID='{0}' | Status='Fallback' | Reason='Unresolved filename'", pageId));
        return 0;
    }

    string pageType = MatchPageType(fileName);
    if (string.IsNullOrEmpty(pageType)) {
        CurrentDCO.Status = 1;
        Log(string.Format("ExternalOCR_FileNamePageIdentify | PageID='{0}' | Source='{1}' | Status='Fallback' | Reason='No pattern match'", pageId, fileName));
        return 0;
    }

    CurrentDCO.Type = pageType;
    Log(string.Format("ExternalOCR_FileNamePageIdentify | PageID='{0}' | PageType='{1}->{2}' | SourceFile='{3}' | Status='Success'", pageId, oldPageType, pageType, fileName));
    return 0;
}
```
