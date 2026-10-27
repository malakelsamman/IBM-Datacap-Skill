---
name: datacap-portal-batch-lifecycle
description: Drive a Datacap Navigator batch from a custom web portal - intake upload, classification by file name, stage tracking (received, classifying, extracting, review, fixup, exporting, filed), Verify approval and proof of export to FileNet. Use when a portal must show users where a batch is and tell them when it reached FileNet.
---

# Portal view of the batch lifecycle

Task order in `Navigator Job`: NScan -> NUpload -> PageID -> Profiler -> Verify -> Export. Fixup is a separate job (see `datacap-fixup-manual-routing`).
The PageID task actually runs profile `PageID_VendorOCR_POC` (log files are `pageid_vendorocr_poc_rrs.log`).

## 1. Intake (counter clerk)
1. `CreateBatch` job `Navigator Job`.
2. `UploadFile` each image. Collect the returned page ids.
3. Classification is by **file name only**. Write `<V n="PageName">` and `<V n="ScanSrcPath">` into `nscan.xml` after each page's `IMAGEFILE`, upload it with `SetFile`, `ClearCacheFolder`.
4. `ReleaseBatch .../finished` (NScan -> NUpload). Poll up to 10 s until the task is no longer `nscan`.
5. `GrabBatch` then `ReleaseBatch .../finished` **again** so Rulerunner takes over from NUpload.
6. The portal renames files from the user's choice, for example `npa_form_123456_1.jpg`, and never shows the pattern to the user. A file with no recognised type keeps its own name and goes to Fixup.

Name patterns (lower-cased, in [`ExternalOCR_FileNamePageIdentify.cs`](../../../ExternalOCRActions/src/ExternalOCR_FileNamePageIdentify.cs)): `signature_form` -> `Signature_Form_1` (fingerprint 569); `npa_form` -> `Form_Page_1` (570); `national_id` or starts with `nationalid` -> `National ID Front` (568); anything else stays `Other`.

## 2. State machine for one batch id
Query `GetBatchList` filtered by `pb_batch`. Take the row with `qu_parent == 0` as the parent, and a row with `qu_parent != 0` and a Fixup job as the child.

| State | Rule |
|---|---|
| `processing` | parent task PageID or Profiler, or NUpload |
| `fixup` | a Fixup child exists and its status is not "done" |
| `review` | parent task Verify |
| `exporting` | parent task Export and status not "Job done" |
| `filed` | parent task Export, status "Job done", and `Export.xml` holds at least one `<V n="Doc_ID">{...}` |
| `empty` | Export done but no `Doc_ID` |
| `failed` | status `error` |

Check `fixup` before `review`. Cache final states (`filed`) in memory so polling does not re-read the file.

## 3. Proof of export
`GetFile/{app}/{parentQid}/Export/xml` contains the batch `Folder_ID` and one `Doc_ID` per exported document. This is the evidence that FileNet received the batch. Optionally confirm with a read-only CEWS search (see `filenet-datacap-export-troubleshooter`).

## 4. Reviewer actions
- Approve: `ClearCacheFolder`, then `ReleaseBatch .../finished` on the Verify queue id. Look up the batch id first with `GetBatchAttributes`, then record the hand-off.
- Hand-off log: append `{time, batch, source: verify|fixup, by}` to a small JSON-lines file on every release. Never show hand-offs from before the server started.
- The reviewer sees (a) a banner notification when a hand-off reaches `filed`, `failed` or `empty`, and (b) an "Export tracking" list that refreshes while any item is still moving.

## 5. UI rules for the clerk tracker
- Four steps: Received, Classification, Field extraction, Ready.
- On `fixup`, keep step 2 amber, relabel it "needs manual handling", show a notice that the batch went to the reviewer, and keep polling.
- On `exporting` / `filed`, relabel step 4 as "Sent to FileNet" and stop polling only at `filed`, `empty` or `failed`.
- No elapsed-time counter. The user asked for it to be removed.

## 6. Verified
Both paths (Fixup and Verify) were run end to end: tracker went processing -> fixup/review -> exporting -> filed, and the reviewer list matched.
