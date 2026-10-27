---
name: filenet-datacap-export-troubleshooter
description: Configure, diagnose, and troubleshoot IBM Datacap to IBM FileNet Content Services exports (CEWS, GraphQL, object store permissions, metadata mapping).
---

# IBM FileNet & Datacap Export Troubleshooter Guide

## 1. Export Architecture & Protocols
- **CEWS (Content Engine Web Services)**: SOAP-based interface configured via `ExportToFileNet.Rul.dll.config` and `FNP8_Export` action library.
- **GraphQL / REST**: Modern API layer configured via endpoints like `http://<filenet-host>:<port>/content-services-graphql/api/rpc/graphql`.

## 2. Common Export Failure Modes & Remediation

### Issue A: "Access Denied" / "No permission to access repository: ECM"
- **Symptom**: User/Operator cannot file documents or browse the object store in IBM Content Navigator.
- **Root Cause**: The authenticated user lacks the `CONNECT` permission on the Object Store object in FileNet.
- **Remediation**:
  1. Open FileNet ACCE (Administrative Console for Content Engine).
  2. Navigate to **Object Stores** -> **[Target Object Store (e.g., ECM)]** -> **Security**.
  3. Ensure the service account or user group has `Connect to store` and `Store objects` permissions granted.

### Issue B: Property Template / Symbolic Name Mismatch
- **Symptom**: Export action fails with `Property does not exist on class`.
- **Root Cause**: Datacap field name does not match the FileNet class property symbolic name.
- **Remediation**:
  - Always map Datacap DCO fields explicitly using `FNP8_SetProperty("SymbolicName", "@P\\FieldName.Text")`.
  - Check case-sensitivity: FileNet symbolic names are case-sensitive.

### Issue C: Invalid WSDL Endpoint
- **Symptom**: `WSDLException: faultCode=OTHER_ERROR: Unable to resolve WSDL`.
- **Remediation**: Ensure `ExportToFileNet.Rul.dll.config` points to `http://<host>:<port>/wsi/FNCEWS40MTOM/` and that MTOM is enabled.

## 3. Pre-Export Diagnostic Checklist
1. Validate object store connectivity via CEWS search tool.
2. Confirm target document class exists and contains all required custom properties.
3. Check versioning policy: Major vs. Minor version check-in.
