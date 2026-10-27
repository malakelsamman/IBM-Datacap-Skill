---
name: datacap-wtm-rest-api
description: Call the IBM Datacap wTM REST service (ServicewTM.svc) to log on, create and upload batches, grab/release tasks, read and write batch XML, list queues, read statistics and manage users. Use when building a portal, script or test harness against a live Datacap application. Contains verified endpoints, payloads and quirks.
---

# Datacap wTM REST API (verified on a live GoldenDemo system)

Base: `http://localhost:8070/ServicewTM.svc`. Application `GoldenDemo`, station `1`.
Always send `Accept: application/json`. Always use `AbortSignal.timeout(...)` (or equivalent): a hung call otherwise blocks the whole session.

Last verified against the live system during the Egypt Post demo build. Re-probe before trusting anything not listed here.

## 1. Session
- `POST /Session/Logon` body `{application, user, password, station}`. Success body is the bare text `true`; read the cookie from `Set-Cookie` and send it back as `Cookie` on every call.
- On HTTP 401/403, log on again with the stored credentials and retry once.
- The Datacap account the user logs on with is the identity. The portal derives its persona from the account name, it does not keep its own user table.

## 2. Queue calls (batch work)
| Call | Notes |
|---|---|
| `POST /Queue/CreateBatch` json `{application, job}` | Returns `{queueId, batchId}`. Job `Navigator Job` starts at NScan. |
| `POST /Queue/UploadFile/{app}/{qid}` multipart field `file` | Returns `{pages:[{pageId}]}`. Page ids are `TM000001`... |
| `GET /Queue/GetFile/{app}/{qid}/{name}/xml` | `name` is a page id, a task file (`PageID`, `FixUp`, `Export`) or `nscan`. |
| `POST /Queue/SetFile/{app}/{qid}/{name}/xml` multipart field `file` | Always follow with `ClearCacheFolder`. |
| `PUT /Queue/ClearCacheFolder/{app}/{qid}` | Empty body, `Content-Type: application/octet-stream`. |
| `PUT /Queue/GrabBatch/{app}/{qid}` | Throws if you already own it; treat as success. |
| `PUT /Queue/ReleaseBatch/{app}/{qid}/finished` | Advances the task. |
| `GET /Queue/GetJPGImageFile/{app}/{qid}/{pageBaseName}/jpg` | Page image. |
| `GET /Queue/GetBatchList/{app}/{pageSize}/{page}/qu_id` | Add `?pb_batch==\|{batchId}` to filter. |
| `GET /Queue/GetBatchAttributes/{app}/{qid}` | Fields `batchId`, `task`, `status`, `job`, `priority`. |
| `GET /Queue/GetBatchHistory/{app}/{qid}` | Takes the **queue id**, not the batch id. |

All write calls with no body must still send `Content-Type: application/octet-stream`.

`GetBatchList` row fields: `qu_id`, `qu_job`, `qu_task`, `qu_status`, `qu_priority`, `qu_tsorder`, `qu_parent`, `pb_batch`, `pb_pagefile`, `pb_pages`, `pb_ndocs`, `qu_start`, `qu_done`, `qs_op`. A split batch returns one row per queue id (parent plus Fixup child), so filter on `qu_parent`.

Status values: `pending`, `running`, `hold`, `waiting`, `error`, `Job done`. Match with a regex, not equality.

## 3. Statistics
`Statistics/TotalCounts/{app}`, `BatchByTask/{app}`, `BatchAge`, `AvgAccuracy`, `OperatorTime`. `BatchByTask` returns parallel arrays `jobName[]`, `taskName[]`, `totalBatches[]`.

## 4. Admin (needs an admin account)
- Reads: `Admin/GetUserList`, `GetStationList`, `GetJobList`, `GetTaskList`, `GetWorkFlowList`, `GetPrivilegeList/{app}/7/{userIndex}`, `GetUserPermissionList`.
- Writes: `Admin/SetUserPermissionList`, `Admin/SetPrivilegeList`.
- Indexes, not names, identify users, jobs and tasks. Read them live; do not hard-code across systems.
- Tarek-style reviewers need the Verify task and the FixUp task in their permission list, plus the `clientsWeb` privilege.

## 5. Quirks that cost time
- `Queue/AbandonBatch` takes the queue id as a **JSON string** and is a **hard delete**. Confirm before calling.
- `Queue/CheckIntegrity` returns HTTP 500 on this setup. Do not rely on it.
- `Queue/SaveBatchAttribute` accepts `jobTaskOrder` and `taskID` and can repoint a batch, but Rulerunner never picks the repointed batch up. Abandoned approach: use a rule-raised condition instead (see `datacap-fixup-manual-routing`).
- Console output of Arabic shows `?` because of the code page. Verify content with `grep` on the file, not on the console.
- Rulerunner reads `Rulerunner.xml` and `collection.xml` only at start. After changing either, restart the `Datacap Rulerunner Service`.
