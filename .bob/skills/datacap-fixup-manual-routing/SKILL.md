---
name: datacap-fixup-manual-routing
description: Route Datacap batches that contain unidentified pages to a manual Fixup job, let an operator classify pages and type fields by hand, then merge the batch back so it continues to Profiler, Verify and Export. Use when page identification cannot recognise a file or when manual classification must be offered in a portal.
---

# Datacap Fixup: manual classification and field entry

Verified end to end: unrecognised file name -> PageID -> Fixup child batch -> operator classifies and types fields -> parent resumes -> Verify -> Export -> FileNet document with the typed values.

## 1. Automatic routing (ruleset 23)
Ruleset `ExternalOCR_FixupRoute`, id 23, file [`externalocr_fixuproute.rul`](../../../GoldenDemo/dco_GoldenDemo/rules/externalocr_fixuproute.rul):
- One rule, `dcomap.open="P=Other"`.
- Func "Route To Fixup" runs `Task_NumberOfSplits` (param `1`) then `Task_RaiseCondition` (param `0,0`), both `ns="rrunner"`.
- Register it in `collection.xml` and add `<ruleset id="23"/>` **after** the extract ruleset to **both** profiles `PageID` and `PageID_VendorOCR_POC`.
- Studio names the raised condition `Route To Fixup` on the PageID task of the Navigator Job. The Fixup Job holds the FixUp task (task id 17, job 11 on the demo system).
- Back up `collection.xml` before editing (`collection.xml.bak_fixup` exists). Restart Rulerunner afterwards.

Result in the queue: the parent row sits at `Navigator Job / Profiler / waiting`; a child row appears at `Fixup Job / FixUp / pending` with `qu_parent` = parent id.

## 2. FixUp is a manual task
Never add FixUp to `C:\Datacap\Taskmaster\Rulerunner.xml`. Rulerunner would try to run it unattended. The operator account needs the FixUp task permission (see `datacap-wtm-rest-api` section 4).

## 3. Reading and writing the working file
When the child is grabbed, wTM creates `FixUp.xml` (a copy of `PageID.xml`) in the batch folder.
- Read and write `/Queue/GetFile|SetFile/{app}/{childQid}/FixUp/xml`.
- **Never write `PageID/xml`.** The FixUp task overwrites it.
- Grab first (`PUT GrabBatch`), otherwise `FixUp.xml` does not exist yet.

## 4. Completing a page (what the portal writes)
For every `<P>` whose `TYPE` is `Other`:
1. Change `TYPE` to the page type (`National ID Front`, `Form_Page_1`, `Signature_Form_1`) and set `STATUS` 0.
2. Add `<V n="DATAFILE">tm00000N.xml</V>` to the page.
3. Wrap it in a document `<D id="{batchId}.NN">` with `TYPE` = document type (`National ID`, `NPA Form`, `Signature Form`) and `STATUS` 0.
4. Upload `tm00000N.xml` via `SetFile` with one `<F id="FieldName">` per field: `TYPE`, `Position`, `STATUS 0`, `hr_bidi RTL`, `hr_locale ar-EG`, `RecognizedText`, plus one `<C cn="10" cr="0,0,0,0">codepoint</C>` per character.
5. Upload `FixUp.xml`, then `ClearCacheFolder`, then `ReleaseBatch .../finished` on the **child**.

Releasing the child merges it into the parent. The parent continues to Profiler, then Verify (pending).

## 5. Skipping the second review
Fields typed by hand are already verified. Poll the parent (about every 3 s, up to roughly 4.5 min): when a `Verify/pending` row appears, grab and release it. If an `Export` row appears first, stop. Run this in the background so the operator is not blocked.

## 6. Form definitions used
| Doc type | Page type | Fields |
|---|---|---|
| National ID | National ID Front | FullNameArabic, FamilyNameArabic, AddressArabic, NationalIdNumber |
| NPA Form | Form_Page_1 | EmployerName2, AvrgMonthlyIncome |
| Signature Form | Signature_Form_1 | AccountHolderName, AccountNumber |

When a field is added or removed in Datacap, update this table and the portal form list (see `datacap-field-lifecycle-change`).

## 7. Not yet tested
Mixed batches (some pages recognised, some `Other`), multi-page Fixup, and a National ID or Signature form through Fixup. Test these before promising them in a demo.

## 8. Dead ends
Repointing a batch to PageID with `SaveBatchAttribute` leaves it `PageID/pending` but Rulerunner never runs it. Do not retry.
