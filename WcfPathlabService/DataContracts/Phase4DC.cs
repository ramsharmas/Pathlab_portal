using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PathlabWcfService.DataContracts
{
    // Patient Registration Phase 4 â€” one call for the "full dashboard": profile,
    // family, bookings, reports, invoices and integration status all together,
    // instead of the portal firing five separate requests to assemble the same view.
    [DataContract]
    public class PatientDashboardDC
    {
        [DataMember] public PatientDC Profile { get; set; }
        [DataMember] public List<FamilyMemberDC> FamilyMembers { get; set; }
        [DataMember] public List<BookingDC> RecentBookings { get; set; }
        [DataMember] public List<ReportDC> Reports { get; set; }
        [DataMember] public int TotalBookings { get; set; }
        [DataMember] public decimal LifetimeSpend { get; set; }
        [DataMember] public int UnreadNotifications { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Message { get; set; }
    }

    // Cart & Checkout Phase 4 â€” saved cart (survives a device switch).
    [DataContract]
    public class SavedCartDC
    {
        [DataMember] public int PatientId { get; set; }
        [DataMember] public string CartJson { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime UpdatedAt { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Message { get; set; }
    }

    // Cart & Checkout Phase 4 â€” recurring test reminder ("subscription"). Does NOT
    // auto-charge; see TestSubscription entity comment and SubscriptionJob.
    [DataContract]
    public class TestSubscriptionDC
    {
        [DataMember] public int TestSubscriptionId { get; set; }
        [DataMember] public int PatientId { get; set; }
        [DataMember] public string TestSuiteID { get; set; }
        [DataMember] public string TestSuiteName { get; set; }
        [DataMember] public int FrequencyDays { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime NextDueDate { get; set; }
        [DataMember] public bool IsActive { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Message { get; set; }
    }

    // Billing Phase 4 â€” one row per booking for finance to reconcile against the
    // payment-gateway statement (PaymentRef) and the LIMS (LimsSyncStatus).
    [DataContract]
    public class ReconciliationRowDC
    {
        [DataMember] public string BookingRef { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime CreatedAt { get; set; }
        [DataMember] public string PatientName { get; set; }
        [DataMember] public decimal TotalAmount { get; set; }
        [DataMember] public decimal AmountPaid { get; set; }
        [DataMember] public string PaymentStatus { get; set; }
        [DataMember] public string PaymentRef { get; set; }
        [DataMember] public decimal? RefundAmount { get; set; }
        [DataMember] public string InvoiceNumber { get; set; }
        [DataMember] public string LimsSyncStatus { get; set; }
    }

    [DataContract]
    public class ReconciliationSummaryDC
    {
        [DataMember(EmitDefaultValue = false)] public DateTime FromDate { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime ToDate { get; set; }
        [DataMember] public decimal TotalBilled { get; set; }
        [DataMember] public decimal TotalCollected { get; set; }
        [DataMember] public decimal TotalRefunded { get; set; }
        [DataMember] public decimal TotalOutstanding { get; set; }
        [DataMember] public List<ReconciliationRowDC> Rows { get; set; }
    }

    // Sample Collection Phase 4 â€” chain of custody.
    [DataContract]
    public class CustodyEventDC
    {
        [DataMember] public string HandlerName { get; set; }
        [DataMember] public string HandlerRole { get; set; }
        [DataMember] public string Action { get; set; }
        [DataMember] public string Location { get; set; }
        [DataMember] public string Notes { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime CreatedAt { get; set; }
    }

    [DataContract]
    public class LogCustodyEventDC
    {
        [DataMember] public string BookingRef { get; set; }
        [DataMember] public string HandlerName { get; set; }
        [DataMember] public string HandlerRole { get; set; }
        [DataMember] public string Action { get; set; }
        [DataMember] public string Location { get; set; }
        [DataMember] public string Notes { get; set; }
    }

    [DataContract]
    public class ChainOfCustodyDC
    {
        [DataMember] public string BookingRef { get; set; }
        [DataMember] public string SampleId { get; set; }
        [DataMember] public List<CustodyEventDC> Events { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Message { get; set; }
    }

    // Report Delivery Phase 4 â€” minimal FHIR R4 DiagnosticReport-shaped JSON for a
    // third-party/EHR consumer. Deliberately not a certified FHIR server (no
    // conformance statement, no full resource graph) â€” just enough structure that
    // a real integration effort has a documented starting shape to map from.
    [DataContract]
    public class FhirDiagnosticReportDC
    {
        [DataMember(Name = "resourceType")] public string ResourceType { get; set; } = "DiagnosticReport";
        [DataMember(Name = "id")] public string Id { get; set; }
        [DataMember(Name = "status")] public string Status { get; set; }
        [DataMember(Name = "subjectReference")] public string SubjectReference { get; set; }
        [DataMember(Name = "code")] public string Code { get; set; }
        [DataMember(Name = "effectiveDateTime", EmitDefaultValue = false)] public DateTime EffectiveDateTime { get; set; }
        [DataMember(Name = "issued")] public DateTime? Issued { get; set; }
        [DataMember(Name = "presentedFormUrl")] public string PresentedFormUrl { get; set; }
    }

    // Notifications Phase 4 â€” per patient/channel/type opt-out.
    [DataContract]
    public class NotificationPreferenceDC
    {
        [DataMember] public string Channel { get; set; }
        [DataMember] public string Type { get; set; }
        [DataMember] public bool Enabled { get; set; }
    }

    [DataContract]
    public class UpdateNotificationPreferenceDC
    {
        [DataMember] public int PatientId { get; set; }
        [DataMember] public string Channel { get; set; }
        [DataMember] public string Type { get; set; }
        [DataMember] public bool Enabled { get; set; }
    }

    // Audit Trail Phase 4 â€” supporting documentation for an NABH/NABL
    // accreditation audit. This is NOT a certified compliance audit (that
    // requires an accredited external auditor) â€” it's the evidence bundle a
    // compliance officer would otherwise assemble by hand.
    [DataContract]
    public class ComplianceReportDC
    {
        [DataMember(EmitDefaultValue = false)] public DateTime GeneratedAt { get; set; }
        [DataMember] public bool AuditChainIntact { get; set; }
        [DataMember] public int TotalAuditEntries { get; set; }
        [DataMember] public int TotalBookings { get; set; }
        [DataMember] public int TotalCustodyEvents { get; set; }
        [DataMember] public int BookingsWithFullCustodyTrail { get; set; }
        [DataMember] public int BookingsMissingCustodyTrail { get; set; }
        [DataMember] public string DataRetentionNote { get; set; }
        [DataMember] public string AccessControlNote { get; set; }
    }

    // Analytics Phase 4 â€” flat, one-row-per-booking export a BI tool's generic
    // "Web"/REST data-source connector can pull directly (Power BI Desktop:
    // Get Data > Web; Tableau: Web Data Connector or the JSON extract path).
    // Full native Power BI/Tableau integration means the user's own workspace/
    // license connecting to this endpoint â€” not something buildable from here.
    [DataContract]
    public class AnalyticsExportRowDC
    {
        [DataMember] public string BookingRef { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime CreatedAt { get; set; }
        [DataMember] public string CollectionType { get; set; }
        [DataMember] public string BookingStatus { get; set; }
        [DataMember] public string PaymentStatus { get; set; }
        [DataMember] public decimal TotalAmount { get; set; }
        [DataMember] public decimal AmountPaid { get; set; }
        [DataMember] public int TestCount { get; set; }
        [DataMember] public int SampleStatus { get; set; }
    }
}
