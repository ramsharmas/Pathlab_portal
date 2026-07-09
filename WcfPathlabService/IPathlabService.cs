using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Web;
using PathlabWcfService.DataContracts;

namespace PathlabWcfService
{
    [ServiceContract]
    public interface IPathlabService
    {
        // ── PATIENT / AUTH ────────────────────────────────────────────────
        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/SendOtp")]
        PatientDC SendOtp(PatientDC dc);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/VerifyOtp")]
        PatientDC VerifyOtp(PatientDC dc);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/RegisterPatient")]
        PatientDC RegisterPatient(PatientDC patient);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/LoginPatient")]
        PatientDC LoginPatient(PatientDC patient);

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetPatient/{patientId}")]
        PatientDC GetPatient(string patientId);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/UpdatePatient")]
        PatientDC UpdatePatient(PatientDC patient);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/ChangePhone")]
        PatientDC ChangePhone(PatientDC patient);

        // ── FAMILY MEMBERS (server-side, replaces localStorage-only list) ──
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetFamilyMembers/{patientId}")]
        List<FamilyMemberDC> GetFamilyMembers(string patientId);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/AddFamilyMember")]
        FamilyMemberDC AddFamilyMember(FamilyMemberDC dc);

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/RemoveFamilyMember/{familyMemberId}")]
        FamilyMemberDC RemoveFamilyMember(string familyMemberId);

        // Patient Registration Phase 4 — one call for the full patient dashboard.
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetPatientDashboard/{patientId}")]
        PatientDashboardDC GetPatientDashboard(string patientId);

        // ── TESTS ─────────────────────────────────────────────────────────
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetAllTests")]
        List<TestDC> GetAllTests();

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/SearchTests/{keyword}")]
        List<TestDC> SearchTests(string keyword);

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetTestById/{testId}")]
        TestDC GetTestById(string testId);

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetTestCatalogue")]
        List<TestDC> GetTestCatalogue();

        // Admin-triggered (and hourly-scheduled) pull from LIMS SetupTestsuite;
        // upserts TestCount only — Price/Description/TestType stay website-owned.
        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/SyncTestCatalogue")]
        LimsSyncResultDC SyncTestCatalogue();

        // Test Catalogue Phase 4 — partner/integration-facing pricing feed.
        // Requires header X-Api-Key == Web.config PartnerApiKey (fails closed).
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetPublicCatalogue")]
        List<TestDC> GetPublicCatalogue();

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetAllPackages")]
        List<PackageDC> GetAllPackages();

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetPackageById/{packageId}")]
        PackageDC GetPackageById(string packageId);

        // ── BOOKINGS ──────────────────────────────────────────────────────
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetBookedSlots?date={date}&type={type}&branch={branch}")]
        List<string> GetBookedSlots(string date, string type, string branch);

        // Server-side validation only — the client never supplies a discount
        // amount, it just displays what this returns.
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/ValidatePromoCode?code={code}&subtotal={subtotal}")]
        PromoValidationDC ValidatePromoCode(string code, string subtotal);

        // Cart & Checkout Phase 4 — server-persisted cart (survives a device switch).
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetSavedCart/{patientId}")]
        SavedCartDC GetSavedCart(string patientId);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/SaveCart")]
        SavedCartDC SaveCart(SavedCartDC dc);

        // Cart & Checkout Phase 4 — recurring test reminders (no auto-charge).
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetTestSubscriptions/{patientId}")]
        List<TestSubscriptionDC> GetTestSubscriptions(string patientId);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/AddTestSubscription")]
        TestSubscriptionDC AddTestSubscription(TestSubscriptionDC dc);

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/CancelTestSubscription/{id}")]
        TestSubscriptionDC CancelTestSubscription(string id);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/CreateBooking")]
        BookingDC CreateBooking(CreateBookingDC booking);

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetBookingByRef/{bookingRef}")]
        BookingDC GetBookingByRef(string bookingRef);

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetBookingsByPatient/{patientId}")]
        List<BookingDC> GetBookingsByPatient(string patientId);

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/UpdateSampleStatus/{bookingRef}/{status}")]
        BookingDC UpdateSampleStatus(string bookingRef, string status);

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetSampleTimeline/{bookingRef}")]
        SampleTimelineDC GetSampleTimeline(string bookingRef);

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/CancelBooking/{bookingRef}")]
        BookingDC CancelBooking(string bookingRef);

        // Phlebotomist-facing view: today's not-yet-collected home-collection jobs.
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetCollectionQueue")]
        List<CollectionQueueItemDC> GetCollectionQueue();

        // Chain of custody (Sample Collection Phase 4).
        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/LogCustodyEvent")]
        CustodyEventDC LogCustodyEvent(LogCustodyEventDC dc);

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetChainOfCustody/{bookingRef}")]
        ChainOfCustodyDC GetChainOfCustody(string bookingRef);

        // ── PAYMENTS ──────────────────────────────────────────────────────
        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/UpdatePaymentStatus")]
        BookingDC UpdatePaymentStatus(PaymentUpdateDC payment);

        // Refund on an already-Paid booking; records RefundAmount/Reason rather
        // than overwriting the original payment fields.
        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/RefundPayment")]
        BookingDC RefundPayment(RefundRequestDC dc);

        // ── REPORTS ───────────────────────────────────────────────────────
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetReportsByPatient/{patientId}")]
        List<ReportDC> GetReportsByPatient(string patientId);

        // Push endpoint for the LIMS (or lab staff, until the real push is wired)
        // to attach the finished report file and flip the booking to Ready.
        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/AttachReport")]
        ReportDC AttachReport(ReportAttachDC dc);

        // Doctor sharing (Report Delivery Phase 3) — honest about the SMTP
        // dependency; see EmailHelper / ShareReportDC.
        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/ShareReportWithDoctor")]
        ShareReportDC ShareReportWithDoctor(ShareReportDC dc);

        // Doctor share link fix — mints (or returns the existing) random share
        // token for a booking. Gated by patientId so guessing a sequential
        // BookingRef alone is no longer enough to obtain a working share link.
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetOrCreateShareToken/{bookingRef}/{patientId}")]
        ShareTokenDC GetOrCreateShareToken(string bookingRef, string patientId);

        // Public, no-login lookup used by the "Share with doctor" view page —
        // replaces the old GetBookingByRef(plain BookingRef) lookup for this page.
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetBookingByShareToken/{token}")]
        BookingDC GetBookingByShareToken(string token);

        // Report Delivery Phase 4 — minimal FHIR-lite feed for a third-party/EHR
        // consumer. Requires X-Api-Key == Web.config PartnerApiKey (fails closed).
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetReportsApi/{patientId}")]
        List<FhirDiagnosticReportDC> GetReportsApi(string patientId);

        // ── INVOICES ──────────────────────────────────────────────────────
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetInvoiceByBookingRef/{bookingRef}")]
        InvoiceDC GetInvoiceByBookingRef(string bookingRef);

        // Billing Phase 4 — reconciliation view against gateway/LIMS records.
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetReconciliationSummary?from={from}&to={to}")]
        ReconciliationSummaryDC GetReconciliationSummary(string from, string to);

        // ── FEEDBACK / COMPLAINTS (Help section fix) ────────────────────────
        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/SubmitFeedback")]
        FeedbackDC SubmitFeedback(FeedbackDC dc);

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetAllFeedback")]
        List<FeedbackDC> GetAllFeedback();

        // ── HOME COLLECTION LEADS (Home Collection popup fix) ────────────
        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/SubmitHomeCollectionLead")]
        HomeCollectionLeadDC SubmitHomeCollectionLead(HomeCollectionLeadDC dc);

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetHomeCollectionLeads")]
        List<HomeCollectionLeadDC> GetHomeCollectionLeads();

        // ── NOTIFICATION PREFERENCES ─────────────────────────────────────
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetNotificationPreferences/{patientId}")]
        List<NotificationPreferenceDC> GetNotificationPreferences(string patientId);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/UpdateNotificationPreference")]
        NotificationPreferenceDC UpdateNotificationPreference(UpdateNotificationPreferenceDC dc);

        // ── ADMIN ─────────────────────────────────────────────────────────
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetAdminStats")]
        AdminStatsDC GetAdminStats();

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetAllPatients")]
        List<PatientDC> GetAllPatients();

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetAllBookings")]
        List<BookingDC> GetAllBookings();

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetStaffAlerts")]
        List<StaffAlertDC> GetStaffAlerts();

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetNotificationLogs")]
        List<NotificationLogDC> GetNotificationLogs();

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetNotificationsByPatient/{patientId}")]
        List<NotificationLogDC> GetNotificationsByPatient(string patientId);

        // Practical Website+LIMS "combined KPI" view — see LimsSyncStatusDC.
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetLimsSyncStatus")]
        LimsSyncStatusDC GetLimsSyncStatus();

        // TAT / revenue trend / test volume. from/to are "yyyy-MM-dd"; both
        // optional — defaults to the last 30 days.
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetAnalyticsSummary?from={from}&to={to}")]
        AnalyticsSummaryDC GetAnalyticsSummary(string from, string to);

        // Analytics Phase 4 — flat tabular export for a BI tool's generic Web/REST
        // connector (see AnalyticsExportRowDC for what "full integration" means here).
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetAnalyticsExport?from={from}&to={to}")]
        List<AnalyticsExportRowDC> GetAnalyticsExport(string from, string to);

        // ── AUDIT TRAIL ───────────────────────────────────────────────────
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetAuditLogs")]
        List<AuditLogDC> GetAuditLogs();

        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/VerifyAuditChain")]
        AuditVerifyResultDC VerifyAuditChain();

        // For client-only events with no other server round-trip: logout, report
        // access/download, report share.
        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/LogClientEvent")]
        AuditLogDC LogClientEvent(AuditLogDC dc);

        // Supporting documentation for an NABH/NABL accreditation audit — NOT a
        // certified audit itself (see ComplianceReportDC).
        [OperationContract]
        [WebInvoke(Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json, UriTemplate = "/GetComplianceReport")]
        ComplianceReportDC GetComplianceReport();
    }
}
