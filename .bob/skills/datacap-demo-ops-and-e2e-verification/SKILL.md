---
name: datacap-demo-ops-and-e2e-verification
description: Operate and verify the Datacap demo environment - which service to restart after which change, tool-call timeouts, backups, a scripted end-to-end test through the portal API for the review and Fixup paths, test-data cleanup, and a pre-demo checklist. Use before any demo or after changing rules, DLLs, configs or the portal.
---

# Demo operations and end-to-end verification

## 1. Restart matrix
| You changed | Do |
|---|---|
| `ExternalOCRActions` C# | Run `ExternalOCRActions\CompileAndDeploy.bat`. It builds the DLL and copies DLL, `.rrx` and both JSON configs to `C:\Datacap\RRS` and the app rules folder. Then restart Rulerunner. |
| `external_ocr_settings.json`, `external_crop_config.json` | Copy to both `C:\Datacap\RRS` and the rules folder, restart Rulerunner. |
| `collection.xml`, a `.rul` file, `Rulerunner.xml` | Restart Rulerunner (it reads them only at start). Back up `collection.xml` first. |
| `ExportToFileNet.Rul.dll.config` | Restart Rulerunner. |
| Portal `server.js` | `Stop-ScheduledTask EgyptPostPortal; Start-Sleep 2; Start-ScheduledTask EgyptPostPortal` |
| Portal `public/*` | Nothing. Browser refresh. |

Rulerunner restart: `Restart-Service 'Datacap Rulerunner Service' -Force -WarningAction SilentlyContinue`.
Never run the portal attached from a tool call: it blocks the call and gets cancelled.

## 2. Tool hygiene
- Keep every command under about 290 s and always pass a timeout.
- Long Node probes can be cancelled: use `AbortSignal.timeout(...)` on every `fetch`.
- Do not paste Node one-liners containing `||`, quotes and regexes into PowerShell: it parses them. Write a `.js` file and run `node file.js`.
- Arabic output: see `windows-text-encoding-safety`.

## 3. Scripted end-to-end test (portal API)
Use `http://localhost:8180/api` with personas' tokens from `POST /login`.

Review path:
1. Mona: `POST /intake` with a file named `npa_form_777_1.jpg`; poll `GET /intake/{queueId}/status` until `state === 'review'`.
2. Tarek: `GET /queue`, find the batch, `POST /batch/{qid}/grab`, `POST /batch/{qid}/approve`.
3. Poll `GET /handoffs` (Tarek) and the intake status (Mona) until `state === 'filed'`.

Fixup path:
1. Mona submits a file with an unrecognised name (for example `scan_unknown_9.jpg`); expect `state === 'fixup'` within about 30 s.
2. Tarek: `GET /fixup/queue`, `GET /fixup/{qid}`, then `POST /fixup/{qid}/complete` with `{pages:{TM000001:{type:'npa_form',fields:{EmployerName2:'...',AvrgMonthlyIncome:'4000'}}}}`.
3. Poll until `filed` (about 90 s overall on the demo system).

Use any real form JPG as test content, for example a previous batch's `tm000001.jpg`. Delete the test script when done.

## 4. Cleanup after testing
- Leftover queue rows: remove with the Hala batch monitor or `POST /Queue/AbandonBatch` (hard delete, ask the user first).
- Clear `corrections.log` and `handoffs.log` so the demo starts clean.
- Remove throwaway scripts from `EgyptPostPortal` (`admin_test.js`, `fixup_test.js`, `fixup_api_test.js`, `repoint_test.js`, `patch_fixup.js`, `patch_ui.js`, `probe_admin.js`, `probe_write.js`, `mona_test.js`, `e2e.js`).
- Optionally delete test documents in FileNet.

## 5. Pre-demo checklist
1. `GET /api/health` shows wTM and the vendor OCR service up (vendor URL is in `external_ocr_settings.json`).
2. Rulerunner service is running.
3. Log in as Mona, Tarek and admin in a browser; open every screen in Arabic and English.
4. Run one correct-name batch and one bad-name batch to export.
5. Confirm both documents in FileNet.
6. Accounts `hala` and `karim` created in Datacap if the demo shows those personas.

## 6. Known open items
Not yet browser-tested: the supervisor screens, the unclassified-pages screen, the export-tracking screen. Not yet tested: mixed or multi-page Fixup, National ID or Signature form through Fixup, a NationalID export without `EmployerName`, and the signature-form line-segmentation plus OCR/A change on a real batch.
