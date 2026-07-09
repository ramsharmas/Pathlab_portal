# Pathlab Portal — Gap Analysis

**Date:** 03-Jul-2026 (statuses re-audited against live code 07-Jul-2026 — see revision note)
**Scope:** Patient-facing portal (`Pathlabfrontend` ASP.NET MVC + AngularJS) and backend (`WcfPathlabService` WCF + EF6 + SQL Server).

> **Revision note (07-Jul-2026):** re-checked every module against the current codebase. Cart, Billing/Payment, Sample Tracking, and Report Access were built out considerably since the 03-Jul pass (real Razorpay + Pay at Counter checkout, sessionStorage-persisted cart with server-side booking creation, slot-capacity gating, OTP-secured report download + doctor sharing, family-member profiles). Statuses below reflect what's actually in the code now, with file references. Remaining gaps are mostly the two cross-cutting ones (API auth, real LIMS wiring) plus a few config items (Razorpay/Fast2SMS live keys).

## Summary

| # | Module | Status | Key Gap |
|---|--------|--------|---------|
| 1 | Patient Registration | 🟡 Partial | Single OTP login/register flow works (`Views/Account/Login.cshtml`); demo OTP `123456` fallback remains since `Fast2SmsApiKey` isn't set — no LIMS patient sync |
| 2 | Test Catalogue | 🟡 Partial | `GetTestCatalogue` dynamically merges `LabTests` + `HealthPackages` (`PathlabService.svc.cs:419-445`) — single DB source of truth; still not synced from LIMS `SetupTestsuite` |
| 3 | Cart | 🟢 Mostly done | Real Cart page (`Views/Booking/Cart.cshtml`) with add/remove/subtotal/GST/total, `sd_cart` persistence, single Test Suite dropdown with inline price/sample-type/fasting (`Views/Test/BookTest.cshtml:54-84`); remaining gap: server doesn't re-validate submitted prices against the catalogue at booking time |
| 4 | Billing / Invoice | 🟡 Partial | Real Razorpay checkout + "Pay at Counter" (`Views/Booking/Checkout.cshtml:174-204`, `Controller.js:354-388`) — `RAZORPAY_KEY_ID` is still a placeholder in `config.js`; invoice remains a client-side print, not a numbered GST record |
| 5 | Sample Tracking | 🟡 Partial | Slot-capacity gating live (`GetBookedSlots`/`CreateBooking`, `PathlabService.svc.cs:544-740`); status still advanced manually, not from a real LIMS event feed |
| 6 | Report Access | 🟢 Mostly done | Reports tab has OTP-gated download + WhatsApp/email/doctor share (`Views/Patient/Portal.cshtml:232-284`); remaining gap: no ingestion pipeline to attach the real result PDF from LIMS |
| 7 | Notifications | 🟡 Partial | SMS wired at every lifecycle event + `NotificationLogs` + reminders + staff alerts; `Fast2SmsApiKey` still unset so delivery isn't live |
| 8 | Audit Trail | 🟡 Partial | Hash-chained AuditLogs table + login/update/report/workflow logging wired; no admin login gate to audit yet, no export/retention policy |
| 9 | Analytics | 🔴 Confirmed gap | Admin dashboard has internal stat counters only (`Views/Admin/Dashboard.cshtml:592-662`); no Power BI/Tableau or any website+LIMS unified BI — explicitly out of this app's scope (needs the user's own BI workspace) |

**Cross-cutting:** no authentication/authorization on any WCF endpoint (admin data included), and the real LIMS is not yet wired — most 🟡 items trace back to these two. Analytics (#9) is the one module still genuinely behind, not just under-configured.

---

## 1. Patient Registration — 🟡 Partial

**What exists**
- OTP send/verify, register, login, profile update endpoints (`IPathlabService.cs`: `SendOtp`, `VerifyOtp`, `RegisterPatient`, `LoginPatient`, `GetPatient`, `UpdatePatient`).
- `Patients` table with demographics; passwords hashed with BCrypt (`PasswordHelper`).
- Login UI with OTP flow (`Views/Account/Login.cshtml`).

**Gaps**
- OTP is hardcoded to `123456` and held in an in-memory dictionary (`PathlabService.svc.cs:27`) — resets on app-pool recycle, breaks on multi-server, no expiry, no rate limiting. Fast2SMS helper exists but is not used to deliver the OTP.
- No password-reset / forgot-password flow.
- No duplicate-phone handling policy or email verification.
- Registered patients are not pushed to the LIMS patient master — portal and lab identities are disconnected.

## 2. Test Catalogue — 🟡 Partial

**What exists**
- `LabTests` + `HealthPackages` tables with price, sample type, fasting, TAT; `GetAllTests`, `SearchTests`, `GetTestById`, `GetAllPackages` APIs.
- Catalogue browsing/booking pages (`Views/Test/*`).
- LIMS mapping fields already modelled (`TestSuiteID`/`testCode` → LIMS `SetupTestsuite`).

**Gaps**
- Duplicate source of truth: a hardcoded 20-test / 6-package catalogue still lives in `Scripts/pathlab.js` (`TEST_SUITES`, `HEALTH_PACKAGES`) alongside the DB catalogue — prices can diverge.
- No sync from LIMS `SetupTestsuite`; catalogue and prices are maintained by hand.
- No admin CRUD screens to add/edit/deactivate tests or packages.
- Search is keyword-only; no category browse, filters, or sorting on the API.

## 3. Cart — 🟢 Mostly done (rebuilt since 03-Jul)

**What exists**
- Single "Select Test Suite" dropdown (`Views/Test/BookTest.cshtml:54-84`, no old Disease/Organ/Habit filters) showing price, sample type(s), test count, and fasting requirement inline before "Add to Cart".
- Real `Views/Booking/Cart.cshtml`: items read/written to `sd_cart` sessionStorage, working `removeItem()`, Subtotal/GST/Total, "Add More Tests", "Proceed to Checkout" — persists across pages/navigation (not lost on refresh within the session).
- ₹ used consistently across Cart/BookTest (no stray `£`).

**Gaps**
- Still session-scoped (cleared on new browser session) rather than tied to the logged-in patient server-side — lost across devices.
- `CreateBooking` sums prices from the client-submitted request rather than re-pricing against the DB catalogue — totals can still be tampered with client-side.
- No per-family-member line-item selection at cart level (family selection exists at checkout, see §12 below), no abandoned-cart visibility for staff.

## 4. Billing / Invoice — 🟡 Partial (payment gateway added since 03-Jul)

**What exists**
- Booking stores `Subtotal`, `GstAmount` (18%), `TotalAmount`, `PaymentMethod`, `PaymentStatus`.
- **Real Razorpay checkout** wired end-to-end (`Views/Booking/Checkout.cshtml:174-204`, `Controller.js:354-388,712-733`), gated by `razorpayConfigured()`/`SD_CONFIG.RAZORPAY_KEY_ID` — plus a genuine "Pay at Counter" option that creates the booking as payment-pending rather than auto-marking it Paid.
- "Invoice" download and "Pay Now" (for Pay-at-Counter bookings) in the patient portal Bills tab.

**Gaps**
- `RAZORPAY_KEY_ID` in `Scripts/config.js` is still the placeholder `rzp_test_YOUR_KEY_ID` — the integration code path is real, but no live payments will process until a real key is dropped in.
- Invoice is still generated client-side as a printable HTML window — no server-side invoice record, no invoice numbering series, not a GST-compliant tax invoice (no GSTIN, HSN/SAC, place of supply).
- No refund/credit-note handling on cancellation; cancelled bookings keep their amounts with no reversal record.
- GST is a flat rounded 18% with no configurability (diagnostic services are commonly GST-exempt — rate needs to be configurable per item).

## 5. Sample Tracking — 🟡 Partial (slot gating added since 03-Jul)

**What exists**
- 5-stage pipeline (Booked → Sample Collected → At Lab → Processing → Report Ready) with a live step tracker in both the portal Track tab and standalone `Views/Patient/TrackSample.cshtml`, timestamps from `SampleStatusHistory`, and chain-of-custody detail; SMS on each status change.
- **Slot-capacity gating**: `GetBookedSlots` (`PathlabService.svc.cs:547-570`) enforces walk-in (4/branch/slot) and home (2/slot) capacity, and `CreateBooking` (line 736-740) re-checks the same gate authoritatively server-side before confirming — prevents double-booking.

**Gaps**
- Status is still advanced manually through the update-status endpoint rather than a real LIMS event feed — no integration updates status when the sample is actually accessioned/processed at the lab.
- No phlebotomist assignment / home-collection dispatch workflow or barcode data model yet.

## 6. Report Access — 🟢 Mostly done (rebuilt since 03-Jul)

**What exists**
- `Reports` table linked to bookings; real **My Reports** tab (`Views/Patient/Portal.cshtml:232-284`) with report list, **OTP-secured download** (`startReportOtp`/`verifyReportOtp`), and **share** via WhatsApp/email/doctor-view link — not a dead login-only page.
- Every download/share is logged to the audit trail (`LogClientEvent` → `ReportDownload`/`ReportShare`).

**Gaps**
- No ingestion pipeline: a `Report` row is created (status "Pending") at booking time, but there is still no API or admin screen to attach the actual result/PDF from the real LIMS — `ReportFilePath` never gets populated by an automated workflow.
- No server-side PDF generation or smart-report/trend view of parameters (numeric result history across visits).

## 7. Notifications — 🟡 Partial (improved 2026-07-06)

**What exists**
- SMS via Fast2SMS on booking confirmation, status change (incl. "Report Ready"), cancellation, and day-before reminder (`SmsHelper.cs`, `ReminderJob.cs`); Notifications tab in the patient portal.
- **`NotificationLogs` table** — every `SmsHelper.Send` call (any type) writes one row: phone, type, booking ref, message, success/failure, timestamp. Viewable in Admin → Notifications Log (`GetNotificationLogs`). This satisfies "logging of sent notifications".
- **Server-side reminder job** (`ReminderJob.RunOnce`, hourly `System.Threading.Timer` started in `Global.asax Application_Start`) sends the day-before SMS regardless of whether the patient opens the portal; idempotent via `NotificationLogs` lookup so it won't double-send.
- **Staff alerts** (`GetStaffAlerts`) surface on the Admin Overview: overdue sample collection, TAT breach risk (>48h stuck), and today's un-dispatched home collections.

**Gaps**
- `Fast2SmsApiKey` is still not configured in `Web.config` — all SMS are logged to `NotificationLogs` as `Success=false` with an "not configured" error until a real key is added. Wiring is done; delivery isn't live.
- No email channel at all (no SMTP config), no WhatsApp auto-send (WhatsApp is a `wa.me` prefill link the patient must tap, not server-initiated).
- India DLT compliance (registered sender ID + approved templates) not addressed — required before transactional SMS will actually deliver once the key is in.
- The reminder timer only runs while the IIS app pool is warm; an idle-recycled pool pauses it. For guaranteed delivery, front it with IIS "Application Initialization" keep-alive or move `ReminderJob.RunOnce()` to a real Windows/Task Scheduler job.
- Staff alerts are computed on-demand from `Bookings` (no push/badge on login) — an admin only sees them by opening the Overview tab.

## 8. Audit Trail — 🟡 Partial (built 2026-07-06)

**What exists**
- **`AuditLogs` table**, append-only and hash-chained: each row's `Hash` = SHA-256(`PrevHash` + its own fields), written by `AuditHelper.Log()` (`Helpers/AuditHelper.cs`). Nowhere in the app updates or deletes a row — only inserts — so `AuditHelper.VerifyChain()` / `GET /VerifyAuditChain` can recompute the chain and flag the first row where stored vs. recomputed hash diverges (tamper-evidence, not blockchain-grade, but a real detector).
- **Login/access logs:** `SendOtp` (OTP requested), `VerifyOtp` (LoginSuccess/LoginFailed), `LoginPatient` (legacy password path), and client-triggered `Logout` (via new `LogClientEvent` endpoint) all write audit rows with phone, patient id, and caller IP (`AuditHelper.GetClientIp()`, reads `HTTP_X_FORWARDED_FOR` then `Request.UserHostAddress`).
- **Data entry/update tracking:** `RegisterPatient` logs the new patient; `UpdatePatient` diffs old vs. new (`FullName`, `Email`, `Gender`, `Address`, `City`, `Pincode`) and logs only the fields that actually changed, e.g. `"Email: 'a@x.com' -> 'b@x.com'"`.
- **Report access/download logging:** the OTP-secured report download (`verifyReportOtp` in Portal.cshtml/Controller.js) and report share both call `LogClientEvent` with action `ReportDownload`/`ReportShare` and the booking ref.
- **Workflow action logging:** `CreateBooking`, `CancelBooking`, and `UpdateSampleStatus` each write an audit row (test count/amount, cancellation reason, status transition) — this sits alongside the existing `SampleStatusHistory`/`SampleEvent` table, which remains the detailed per-booking timeline for the patient-facing tracker; `AuditLogs` is the compliance-facing unified view across all entity types.
- Admin → **Audit Trail** tab lists the last 300 entries (actor, action, entity, detail, IP, success) with a chain-intact/tamper-detected banner at the top.

**Gaps**
- No admin login/action auditing yet (the Admin dashboard itself has no login gate at all — see cross-cutting gap #1 — so there's nothing to audit there until that's added).
- `Detail` is a free-text diff summary, not a structured before/after JSON blob — fine for eyeballing in the admin UI, harder to machine-parse or feed into a SIEM later.
- Hash chain is a single linear sequence guarded by one lock in one app instance; if the WCF service is ever scaled to multiple instances/servers without a shared serialized writer, concurrent inserts could race. Fine for the current single-instance deployment.
- No retention/archival policy or export (CSV/PDF) for compliance handover — everything lives in one growing SQL table.
- Still covers the website/WCF side only — no link into the real LIMS's own audit trail (the "Integration Recommendation" column asks for LIMS-side centralization), consistent with the original scope split.

## 9. Analytics — 🟡 Basic

**Current state**
- **Website:** analytics limited to Google Analytics (visits/clicks); in the portal itself, the admin dashboard (`Views/Admin/Dashboard.cshtml`) shows stat cards (total patients, bookings, reports ready, etc.) from `GetAdminStats`, plus raw patient/booking tables.
- **LIMS (TatLIMS):** generates operational reports for lab staff (sample flow, test volumes) — internal only, not visible to the website/portal. Reference: [labsols.com/LIMS-Pathology/Admin](https://labsols.com/LIMS-Pathology/Admin)

The two datasets are completely separate — there is no unified view of patient behaviour, lab operations, and financials.

### Website vs LIMS — category-wise gap

| Category | Website | LIMS | Gap Identified | What's Required | Integration Recommendation |
|---|---|---|---|---|---|
| **User Engagement Analytics** | Tracks visits, clicks, and user behaviour via Google Analytics. | Does not track patient portal activity — only internal lab operations. | Website data is separate from lab operational data; limited patient behaviour insight. | Capture patient portal interactions (logins, bookings, report downloads) and combine with LIMS metrics. | ETL pipeline extracting website + LIMS data into a BI platform (Power BI/Tableau) for a unified view. |
| **Operational Analytics** | No operational metrics (sample TAT, test volume). | Detailed operational reports for lab staff (sample flow, test stats). | Website has no visibility into lab operational performance. | Consolidate operational metrics from LIMS with website KPIs. | BI dashboard pulls LIMS data and combines it with website analytics for reporting. |
| **Financial Analytics** | Booking/payment counts, payment-gateway logs. | Billing & invoicing records; payments tracked manually or via front desk. | Financial data scattered across systems; reconciliation is not automated. | Integrate revenue and payment data from both Website and LIMS. | Connect LIMS billing + website gateway logs to the BI platform for consolidated reporting. |
| **Custom Reporting & Dashboards** | Limited reporting — mostly GA dashboards. | Staff reports, but no patient-facing or management dashboards. | No unified dashboard across Website + LIMS. | Dashboards combining patient activity, lab operations, financials, and KPI tracking. | Power BI / Tableau dashboard fed from both systems via ETL/API. |
| **Compliance & Audit Analytics** | Analytics data not retained for long-term compliance. | Stores audit trails and operational logs. | Website logs are not linked to LIMS compliance/audit data. | Include audit metrics (TAT, report delivery times) in consolidated BI reporting. | ETL to bring LIMS audit metrics and website activity into the BI platform. |

### What's required (checklist)

| Requirement | Website | LIMS |
|---|:---:|:---:|
| User behaviour tracking (visits, clicks) | ✔ | — |
| Booking & payment stats | ✔ | ✔ |
| Operational metrics (sample flow, test volume) | — | ✔ |
| Financial metrics (revenue, payments, invoices) | ✔ | ✔ |
| KPI tracking (TAT, report delivery, test completion) | — | ✔ |
| Audit/compliance metrics integration | ✔ | ✔ |
| Combined BI dashboard (Power BI/Tableau) | ✔ | ✔ |
| Exportable/visual reports for management | ✔ | ✔ |

**Recommendation:** integrate website + LIMS data into a single BI dashboard (Power BI/Tableau) via an ETL/API layer for unified visibility across engagement, operations, finance, and compliance.

**Portal-code gaps (in addition to the above)**
- Counters only — no time-series (bookings/revenue per day-week-month), no top-tests ranking, no collection-type or branch breakdown, no charts.
- No date-range filtering and no CSV/Excel export.
- **The admin dashboard and its APIs (`GetAdminStats`, `GetAllPatients`, `GetAllBookings`) have no authentication** — full patient PII is exposed to anyone who opens `/Admin/Dashboard` or calls the endpoints directly.

---

## What needs to connect — Website ↔ LIMS ↔ Patient Portal

*Plain English: what should happen automatically between the three systems, and what doesn't happen today.*

Think of it like this — **Website** = the patient's front door · **LIMS** = the lab's brain · **Portal** = the patient's account. All three must talk to each other. Today, none of them do: every row below is a manual step or a dead end.

| When this happens… | What should happen automatically | What actually happens today |
|---|---|---|
| **Patient registers on the website** | Their details (name, phone, DOB) are saved in LIMS as a new patient record. Next visit, LIMS already knows who they are. | Nothing happens in LIMS. The patient exists on the website only — lab staff have no idea this person registered. |
| **Patient adds tests and confirms a booking** | A booking/job is created in LIMS automatically. Lab staff see it without doing anything — no manual re-entry. | Nothing happens in LIMS. Staff don't know about the online booking and have to re-create the job in LIMS by hand. |
| **Patient pays online** | The payment is recorded on the LIMS job automatically; LIMS shows the job as "Paid" and an invoice is generated and saved for the patient. | No online payment exists on the website at all. All payments are entered manually by staff in LIMS. |
| **Test prices change in LIMS** | The website shows the updated price automatically — no one touches the website. | The website has its own separate price list (shown in £, not ₹). A LIMS price change has no effect; someone must update the website manually too. |
| **Lab staff update sample status in LIMS** (sample collected, at lab…) | The patient's portal updates automatically — they see "Sample Collected" and get an SMS: "Your sample has been picked up." | The patient sees nothing. "Track My Sample" on the website is a blank page; the patient has to call the lab to find out. |
| **Lab staff publish a report in LIMS** | The report appears in the portal under "My Reports"; the patient gets an SMS/WhatsApp — "Your report is ready" — and downloads it securely with OTP. | The report stays in LIMS only. Staff manually email the PDF. The portal has no reports section and the "Download Report" page is blank. |
| **Patient chooses "Pay at Counter"** | A LIMS job is created marked "Payment Pending"; when the patient pays at the counter, staff mark it Paid in LIMS and the portal updates to "Paid" automatically. | No checkout or payment flow exists on the website. Everything is manual. |
| **Patient books a time slot online** | That slot is blocked in the LIMS schedule so no one else can take it — staff see the online booking; double-booking is impossible. | No slot booking on the website. Patients walk in or call; there is no connection between the website and the LIMS schedule. |
| **Patient wants to share a report with their doctor** | One tap in the portal shares a secure link via WhatsApp or email. The doctor can view (not download) the report. | The patient forwards the manually emailed PDF themselves. No secure sharing option. |
| **Lab adds a new test or package in LIMS** | The test appears in the website booking list automatically — no manual website update. | The website test list is separate and hardcoded. A new LIMS test doesn't appear; someone must add it to the website by hand too. |

> **Scope note:** "What actually happens today" describes the current live website + TatLIMS setup. The new portal build in this repository adds the website-side screens for several of these flows (booking, tracking, reports, portal), but the LIMS side of every arrow above is still unwired — see the module sections and cross-cutting gap #2.

## Patient journey — step by step (how it should work end to end)

*From opening the website to downloading the report — what the patient does and what they see at each step.*

| Step | What the patient does | What they should see on screen |
|---|---|---|
| **1 · 🔍 Pick a Test** (Website) | Opens the website, clicks **"Book a Test"**. | ONE dropdown — *"Select Test Suite"* — listing all suites with ₹ price (e.g. *Lipid Profile – ₹650*).<br>After picking, a box appears showing: • price in ₹ • sample needed (Blood/Urine/Stool…) • number of tests included • fasting required Yes/No.<br>**"Add to Cart"** button; can repeat to add more suites. |
| **2 · 🛒 Cart** (Website) | Reviews everything added; ready to book. | Cart page: • all tests listed with price each • total at the bottom • remove (×) on each item • *"Add more tests"* link • **"Proceed to Checkout"** button.<br>Cart doesn't disappear if they go back. |
| **3 · 🔐 Login** (Website) | Prompted to log in before booking. | Simple screen: enter phone → **"Send OTP"** → 6-digit OTP on phone → enter OTP → logged in.<br>New patient: short form after OTP (name, date of birth, gender, email).<br>After login, returns to checkout automatically. **No passwords — OTP only.** |
| **4 · 🏥 Choose Collection** (Website) | Decides how their sample will be taken. | Two clear options:<br>🏥 **Walk-in at Branch** → choose branch → pick date → pick an available time slot.<br>🏠 **Home Collection** → enter address (or use saved one) → pick date → pick an available time slot. |
| **5 · 📋 Review Order** (Website) | Checks everything before paying. | Full summary: • their name • all tests + prices • branch / home address • date and time • total amount • *Edit* link on each section • **"Confirm & Pay"** button. |
| **6 · 💳 Payment** (Website) | Pays for the booking. | Payment screen: • UPI (QR code + UPI ID) • credit/debit card • net banking • wallets (Paytm, PhonePe…) • **"Pay at Counter"** option for cash at the lab.<br>Total shown clearly throughout; security badge shown. |
| **7 · ✅ Booking Confirmed** (Website) | Payment done; booking complete. | Big **"Booking Confirmed!"** message: • Booking ID (e.g. *PL-20240608-001*) • tests booked • date, time, branch or address • *Paid ₹650* or *Pay at Counter*.<br>SMS sent instantly; email if provided.<br>Buttons: *"Go to My Account"* \| *"Book Another Test"*. |
| **8 · 🩸 Track Sample** (Portal) | Waits for the sample to be collected and tested. | Status tracker: 📅 Booked → 🩸 Sample Collected → 🔬 At Lab → ⚙️ Processing → ✅ Report Ready.<br>Date/time shown at each completed stage; SMS on every status change; phlebotomist name shown for home collection. |
| **9 · 📄 Get Report** (Portal) | Gets SMS *"Report ready"*; opens the portal to download. | **My Reports** tab: • test name, date, status *Ready* • **"Download PDF"** → OTP sent to phone → enter OTP → PDF downloads • *"View Online"* • *"Share"* → WhatsApp or email to doctor.<br>Report stays in the portal permanently — always downloadable. |

## Patient Portal — complete journey & tab structure

Target structure for the logged-in patient portal: what each tab shows, where its data comes from, and whether it depends on the LIMS link.

| Tab | What the patient sees | Data source | LIMS link required | Notes / gaps |
|---|---|---|---|---|
| 🏠 **Dashboard / Home** | Upcoming appointments · pending reports · active orders · quick links | Website DB + LIMS sync | **Yes** — job/order status | Central hub; pulls status from LIMS jobs |
| 📋 **My Bookings** | Booking history · date, tests, branch · status (Confirmed / In-Progress / Done) · cancel option | Website booking records | **Yes** — link to LIMS job ID | Booking ID must map to LIMS job ID |
| 🧪 **My Tests / Orders** | Test name, code, package · sample collection status · processing status · TAT indicator | LIMS job/workflow | **Yes** — real-time status pull | Website polls the LIMS API for test status updates |
| 📦 **Sample Collection** | Collection date/time slot · home or walk-in · phlebotomist details (home) · sample received status | LIMS sample module | **Yes** — sample ID + status | Generate the sample barcode link in LIMS at booking time |
| 📄 **Reports** | Available reports · download PDF (OTP-secured) · report date, test name · share to doctor | LIMS report module | **Yes** — auto-push on validation | LIMS pushes on "Published"; download secured by OTP |
| 💳 **Billing & Payments** | Invoice list · payment status (Paid/Pending) · download receipt · pay pending amount | Payment gateway + LIMS billing | **Yes** — payment status sync | Online payment must sync job status back into LIMS |
| 🔔 **Notifications** | Booking confirmations · sample pickup alerts · report-ready alerts · promotional offers | Website notification engine | **Yes** — LIMS triggers events | LIMS webhooks → website → SMS/Email/WhatsApp |
| 👤 **My Profile** | Personal details · edit name, DOB, phone · change password · family member profiles | Website patient DB | **Yes** — sync with LIMS patient | Patient ID in LIMS must match the portal account |
| 📁 **Health Records** | Archive of all past reports · upload external documents · doctor prescriptions | Website storage + LIMS | **Partial** — reports from LIMS | Phase 3 feature; external document upload is website-side |
| 🆘 **Help / Support** | Raise complaint/query · chat support · FAQ · branch locator | Website CMS | No | Independent of LIMS; Phase 2/3 |

> **Where the current build stands (re-audited 07-Jul-2026):** the portal in this repo (`Views/Patient/Portal.cshtml`) has nine tabs — Dashboard, Bookings, Track, Reports, Bills, Notifications, Records, Profile, Help — and most now have real substance, not just a UI shell: Razorpay + Pay-at-Counter checkout is wired behind Billing, Reports has a working OTP-secured download and share-to-doctor flow, family-member profiles exist (add/remove + book-for-family at checkout), and slot booking blocks double-bookings server-side. What's still genuinely missing: every "LIMS link required" row is unwired to a *real* LIMS (the website/portal side is built, the lab-system side isn't connected — see the integration table above), phlebotomist assignment and sample barcodes have no data model yet, and notifications depend on a live Fast2SMS key to actually deliver rather than just log.

### Tab-by-tab build spec (what goes inside each tab)

*The live production site has no patient portal at all — this is the full build spec, tab by tab. (In this repo's new build, the UI shell for most of these already exists; see the note above.)*

| Tab | What the patient can do | What is on this screen |
|---|---|---|
| 🏠 **Dashboard** | See all active info at a glance · quick-jump to any section · know if anything needs attention | Greeting *"Hello [Name]"* · upcoming appointment card (test, date, time) · sample status strip (current stage, if active) · pending-reports alert badge · quick buttons: *Book a Test \| My Reports \| Track Sample* · last 3 notifications |
| 📋 **My Bookings** | View all bookings, past and upcoming · check status of each · cancel an upcoming booking · re-book the same tests | Bookings listed newest first · each card: Booking ID, date, tests, branch/address, amount paid, status · status badges: Booked → Confirmed → Sample Collected → Processing → Report Ready → Completed · **Cancel** on upcoming bookings · **Re-book** on completed ones |
| 🩸 **Track My Sample** | See exactly where the sample is · when each step happened · expected report time | Visual step tracker: Booked → Sample Collected → At Lab → Processing → Report Ready · date + time at each completed step · phlebotomist name for home collection · expected report-ready time · SMS at every status change |
| 📄 **My Reports** | Download a report anytime · view in browser · share to doctor | Report list by date · test name, report date, status (Ready/Pending) · **Download PDF** — OTP sent to phone before download · **View Online** · **Share** via WhatsApp or email · all past reports always here |
| 💳 **Bills & Payments** | See payment history · download invoices · pay a pending amount online | Transaction list: date, tests, amount, method, status · **Download Invoice PDF** · **Pay Now** (only when "Pay at Counter" was chosen) · refund status where applicable |
| 🔔 **Notifications** | See all alerts in one place · know what happened and when | Chronological alert list · types: booking confirmed, reminder, sample collected, report ready, payment received · unread in bold · badge count on the tab · tap → jumps to the relevant screen |
| 📅 **Book a Test** (from portal) | Book without going back to the homepage · re-book a past test quickly · book for a family member | Same dropdown + cart flow · patient details pre-filled · quick re-book shortcuts · family-member selector |
| 👤 **My Profile** | Update personal details · change phone number · add family members | Editable: name, DOB, gender, phone, email, address · phone change verified by OTP on the new number · family members: name, DOB, relation · book tests for family from the same account |

## Cross-cutting gaps (affect every module)

1. **No API security.** Every WCF operation is anonymous; there is no token/session validation, so patient data, admin data, and mutating operations are all publicly callable. This is the single highest-priority fix.
2. **LIMS not wired.** The mapping fields exist (`TestSuiteID`/`testCode`), but no integration pulls the catalogue, pushes patients/orders, or brings back statuses and reports.
3. **Legacy static layer.** `pathlab.js` still carries a full localStorage demo implementation (cart is used; bookings/reports/notifications parts are stale) that overlaps the AngularJS + WCF path — needs pruning to one code path.

## Suggested priority order

1. API authentication/authorization + lock down admin endpoints (security) — still the top gap.
2. Drop in the real Razorpay key + server-side price re-validation + numbered GST invoice records (billing gateway itself is now built, this is about going live + closing correctness gaps).
3. LIMS integration: catalogue sync, order push, status + report pull (unlocks tracking, reports) — the website/portal side of every one of these flows is now built; the LIMS side is the remaining unknown.
4. Real OTP/SMS delivery via a live Fast2SMS key + DLT registration (registration + notifications — code is wired, just needs the key).
5. ~~Audit trail table + logging.~~ Done 2026-07-06 (`AuditLogs` hash chain + Admin Audit Trail tab) — remaining: admin login gate to audit, export/retention policy.
6. Analytics: still the one module with no real progress — needs an actual BI layer (Power BI/Tableau + ETL) if that's in scope, or at minimum charts/filters/export on the existing admin dashboard.
