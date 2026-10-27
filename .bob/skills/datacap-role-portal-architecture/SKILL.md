---
name: datacap-role-portal-architecture
description: Build and operate a bilingual (Arabic RTL / English) multi-persona web portal in front of IBM Datacap, using a zero-dependency Node.js server that proxies a whitelist of wTM REST calls. Use when creating or extending the Egypt Post demo portal or a similar role-based Datacap front end.
---

# Role-based Datacap portal (Egypt Post pattern)

Location: `C:\Datacap\EgyptPostPortal\` - `server.js` (API plus static), `public/app.js`, `public/styles.css`, `public/index.html`. URL `http://localhost:8180`. Style follows egyptpost.gov.eg. Arabic RTL by default, English toggle, font-size controls.

## 1. Rules of the design
- **No dependencies.** Plain `http`, `fs`, `fetch`. Nothing to install on the demo machine.
- **Login is username and password only.** The Datacap account decides the view through the `VIEWS` map (optional `users.json` overrides). Unknown accounts get the supervisor view.
- **The browser never talks to wTM.** `server.js` holds the session cookie, re-logs on 401/403, and exposes only whitelisted `/api/*` routes.
- **Server-side authorisation.** `/api/admin/*` requires `sess.persona === 'hala'`. Hiding a menu item is not security.
- **Everything shown is live.** No mock data. If wTM is down, show a clear error.

## 2. Personas
| View | Datacap account | Purpose |
|---|---|---|
| Mona, counter clerk | `Mona` | Intake, tracker, batches |
| Tarek, verification officer | `Tarek` (and `tracy`) | Review queue, unclassified pages (Fixup), export tracking, batches |
| Dr. Hala, supervisor | `admin` (add `hala`) | Overview, batch monitor, team and access, system setup, correction log, system status |
| Karim, auditor | `karim` (not created yet) | Audit log and batches |

## 3. Server layout (`server.js`)
`VIEWS`; wTM helpers (`wtm`, `wtmJson`, `wtmText`, `wtmSend`, `fileForm`); `runIntake`; `fixupPages` and `completeFixup` (+ `FIXUP_FORMS`); `passVerify`; `exportState`, `recordHandoff`; XML helpers (`vOf`, `parseFields`, `HIDDEN_FIELDS`); `handleAdmin`; `handleApi` route table; static file server.

## 4. Client layout (`app.js`)
Translation tables `T.ar` / `T.en` (extended with `Object.assign`); `PERS` navigation per persona; views (`intakeView`, `queueView`, `verifyView`, `fixupQueueView`, `fixupView`, `exportsView`, `overviewView`, `monitorView`, `teamView`, `configView`, `healthView`, `auditView`); `route()` hash router that clears timers on every navigation. Poll with `timers.push(setTimeout(...))`.

## 5. Operating it
- It runs as the Windows scheduled task `EgyptPostPortal`. **Never start it attached from a tool call.**
- After a `server.js` change: `Stop-ScheduledTask EgyptPostPortal; Start-Sleep 2; Start-ScheduledTask EgyptPostPortal`. Static files in `public/` need no restart.
- Health: `GET /api/health` returns wTM and vendor probes.
- Logs: `server.log`, `corrections.log`, `handoffs.log`.

## 6. Conventions
- Hide a field from the UI with `HIDDEN_FIELDS` (for example `EmployerName`).
- Corrections made in Verify or Fixup are appended to `corrections.log` (`source: fixup` for manual entry) and shown in the correction log.
- Show Arabic digits with `Intl.NumberFormat('ar-EG')`; keep batch ids and numbers `dir="ltr"`.
- Mark OCR/A fields (`NationalIdNumber`, `AccountNumber`) as digit inputs and flag wrong lengths (14 and 16) as low confidence.

## 7. Related skills
`datacap-wtm-rest-api`, `datacap-portal-batch-lifecycle`, `datacap-fixup-manual-routing`, `windows-text-encoding-safety`, `datacap-demo-ops-and-e2e-verification`.
