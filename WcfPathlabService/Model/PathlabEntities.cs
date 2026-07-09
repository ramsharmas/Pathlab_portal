using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;

namespace PathlabWcfService.Model
{
    public class PathlabDbContext : DbContext
    {
        public PathlabDbContext() : base("name=PathlabDB") { }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<LabTest> LabTests { get; set; }
        public DbSet<HealthPackage> HealthPackages { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<BookingTest> BookingTests { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<SampleEvent> SampleEvents { get; set; }
        public DbSet<NotificationLog> NotificationLogs { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<FamilyMember> FamilyMembers { get; set; }
        public DbSet<PromoCode> PromoCodes { get; set; }
        public DbSet<SavedCart> SavedCarts { get; set; }
        public DbSet<TestSubscription> TestSubscriptions { get; set; }
        public DbSet<CustodyEvent> CustodyEvents { get; set; }
        public DbSet<NotificationPreference> NotificationPreferences { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<HomeCollectionLead> HomeCollectionLeads { get; set; }
    }

    [Table("Patients")]
    public class Patient
    {
        [Key]
        public int PatientId { get; set; }
        [Required, MaxLength(150)]
        public string FullName { get; set; }
        [Required, MaxLength(15)]
        public string Phone { get; set; }
        [MaxLength(150)]
        public string Email { get; set; }
        [MaxLength(10)]
        public string Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        [MaxLength(300)]
        public string Address { get; set; }
        [MaxLength(100)]
        public string City { get; set; }
        [MaxLength(10)]
        public string Pincode { get; set; }
        [Required, MaxLength(255)]
        public string PasswordHash { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        // Set once RegisterPatient successfully pushes this patient to the LIMS
        // user master (LimsSyncStatus: Pending / Synced / Failed).
        [MaxLength(50)]
        public string LimsPatientId { get; set; }
        [MaxLength(20)]
        public string LimsSyncStatus { get; set; }

        public virtual ICollection<Booking> Bookings { get; set; }
        public virtual ICollection<FamilyMember> FamilyMembers { get; set; }
    }

    // Cart & Checkout Phase 3 — discount codes. Value/MaxDiscount/MinOrderValue are
    // enforced server-side in PathlabService.ApplyPromoCode; the client only ever
    // displays what the server computed, never supplies the discount amount itself.
    [Table("PromoCodes")]
    public class PromoCode
    {
        [Key]
        public int PromoCodeId { get; set; }
        [Required, MaxLength(30)]
        public string Code { get; set; }
        [MaxLength(20)]
        public string DiscountType { get; set; } = "Percent"; // "Percent" | "Flat"
        public decimal DiscountValue { get; set; }
        public decimal? MaxDiscount { get; set; }
        public decimal MinOrderValue { get; set; } = 0;
        public DateTime? ExpiryDate { get; set; }
        public int? UsageLimit { get; set; }
        public int UsedCount { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    // Server-side family profile (Patient Registration Phase 3) — replaces the old
    // localStorage-only family list so a member can actually be booked for (see
    // Booking.FamilyMemberId) instead of just being a display-only local note.
    [Table("FamilyMembers")]
    public class FamilyMember
    {
        [Key]
        public int FamilyMemberId { get; set; }
        public int PatientId { get; set; }
        [Required, MaxLength(150)]
        public string Name { get; set; }
        [MaxLength(30)]
        public string Relation { get; set; }
        [MaxLength(10)]
        public string Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        [MaxLength(15)]
        public string Phone { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("PatientId")]
        public virtual Patient Patient { get; set; }
    }

    [Table("LabTests")]
    public class LabTest
    {
        [Key]
        public int TestId { get; set; }
        [Required, MaxLength(50)]
        public string TestSuiteID { get; set; }
        [Required, MaxLength(200)]
        public string TestSuiteName { get; set; }
        [MaxLength(100)]
        public string ShortName { get; set; }
        public decimal Price { get; set; }
        [MaxLength(100)]
        public string SampleType { get; set; }
        public int TestCount { get; set; }
        [MaxLength(50)]
        public string ReportTime { get; set; }
        [MaxLength(50)]
        public string Fasting { get; set; }
        [MaxLength(100)]
        public string Category { get; set; }
        [MaxLength(500)]
        public string Description { get; set; }
        // Landing-page grouping: "Test" (individual) or "Package" (panel/bundle).
        // LIMS SetupTestsuite has no such flag — maintained website-side.
        [MaxLength(20)]
        public string TestType { get; set; } = "Test";
        public bool IsActive { get; set; } = true;
        // Last time SyncTestCatalogueFromLims() matched/refreshed this row's
        // TestCount from the LIMS SetupTestsuite. Null = never synced.
        public DateTime? LimsSyncedAt { get; set; }
    }

    [Table("HealthPackages")]
    public class HealthPackage
    {
        [Key]
        public int PackageId { get; set; }
        [Required, MaxLength(50)]
        public string PackageCode { get; set; }
        [Required, MaxLength(200)]
        public string PackageName { get; set; }
        [MaxLength(500)]
        public string Description { get; set; }
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public int TestCount { get; set; }
        [MaxLength(100)]
        public string SampleType { get; set; }
        [MaxLength(50)]
        public string ReportTime { get; set; }
        [MaxLength(50)]
        public string Fasting { get; set; }
        [MaxLength(100)]
        public string Badge { get; set; }
        public string Includes { get; set; }
        public bool IsActive { get; set; } = true;
    }

    [Table("Bookings")]
    public class Booking
    {
        [Key]
        public int BookingId { get; set; }
        [Required, MaxLength(20)]
        public string BookingRef { get; set; }
        public int PatientId { get; set; }
        // Set when this booking was placed for a family member rather than the
        // account holder; Name is a point-in-time snapshot so it still displays
        // correctly even if the member is later edited or removed.
        public int? FamilyMemberId { get; set; }
        [MaxLength(150)]
        public string FamilyMemberName { get; set; }
        [MaxLength(20)]
        public string CollectionType { get; set; }
        [MaxLength(200)]
        public string BranchName { get; set; }
        [MaxLength(400)]
        public string Address { get; set; }
        public DateTime? CollectionDate { get; set; }
        [MaxLength(50)]
        public string TimeSlot { get; set; }
        public decimal Subtotal { get; set; }
        public decimal GstAmount { get; set; }
        public decimal TotalAmount { get; set; }
        // Promo code applied at checkout (Cart & Checkout Phase 3). DiscountAmount is
        // computed server-side in ApplyPromoCode and subtracted before GST — never
        // trust a client-supplied discount value.
        [MaxLength(30)]
        public string PromoCode { get; set; }
        public decimal DiscountAmount { get; set; } = 0;
        [MaxLength(50)]
        public string PaymentMethod { get; set; }
        [MaxLength(20)]
        public string PaymentStatus { get; set; } = "Pending";
        // Gateway transaction id (e.g. Razorpay payment id) so every online
        // payment is reconcilable against the gateway statement.
        [MaxLength(60)]
        public string PaymentRef { get; set; }
        public DateTime? PaidAt { get; set; }
        // Billing Phase 3: partial payments accumulate here (PaymentStatus becomes
        // "PartiallyPaid" until AmountPaid >= TotalAmount); refunds are recorded
        // rather than silently overwriting the original payment amounts.
        public decimal AmountPaid { get; set; } = 0;
        public decimal? RefundAmount { get; set; }
        [MaxLength(300)]
        public string RefundReason { get; set; }
        public DateTime? RefundedAt { get; set; }
        public int SampleStatus { get; set; } = 0;
        [MaxLength(30)]
        public string BookingStatus { get; set; } = "Booked";
        // Portal-issued sample identity; LimsJobId is filled once the booking
        // is pushed to the LIMS (LimsSyncStatus: Pending / Synced / Failed).
        [MaxLength(30)]
        public string SampleId { get; set; }
        [MaxLength(30)]
        public string Barcode { get; set; }
        [MaxLength(50)]
        public string LimsJobId { get; set; }
        [MaxLength(20)]
        public string LimsSyncStatus { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        // Random opaque token minted by GetOrCreateShareToken so the "share with
        // doctor" link isn't just the guessable sequential BookingRef — a doctor
        // link is only ever built from this, never from BookingRef directly.
        [MaxLength(64)]
        public string ShareToken { get; set; }

        [ForeignKey("PatientId")]
        public virtual Patient Patient { get; set; }
        public virtual ICollection<BookingTest> Tests { get; set; }
        public virtual ICollection<Report> Reports { get; set; }
        public virtual ICollection<SampleEvent> StatusEvents { get; set; }
    }

    [Table("SampleStatusHistory")]
    public class SampleEvent
    {
        [Key]
        public int SampleEventId { get; set; }
        public int BookingId { get; set; }
        public int Status { get; set; }
        [MaxLength(50)]
        public string StatusLabel { get; set; }
        [MaxLength(30)]
        public string Source { get; set; }
        [MaxLength(300)]
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("BookingId")]
        public virtual Booking Booking { get; set; }
    }

    [Table("BookingTests")]
    public class BookingTest
    {
        [Key]
        public int BookingTestId { get; set; }
        public int BookingId { get; set; }
        [MaxLength(50)]
        public string TestSuiteID { get; set; }
        [MaxLength(200)]
        public string TestSuiteName { get; set; }
        public decimal Price { get; set; }
        [MaxLength(100)]
        public string SampleType { get; set; }
        public int TestCount { get; set; }

        [ForeignKey("BookingId")]
        public virtual Booking Booking { get; set; }
    }

    [Table("Reports")]
    public class Report
    {
        [Key]
        public int ReportId { get; set; }
        public int BookingId { get; set; }
        [MaxLength(20)]
        public string BookingRef { get; set; }
        public int PatientId { get; set; }
        [MaxLength(500)]
        public string TestNames { get; set; }
        [MaxLength(500)]
        public string ReportFilePath { get; set; }
        [MaxLength(30)]
        public string Status { get; set; } = "Pending";
        public DateTime ReportDate { get; set; } = DateTime.Now;

        [ForeignKey("BookingId")]
        public virtual Booking Booking { get; set; }
    }

    // Every SMS/notification attempt — sent from SmsHelper so booking confirmations,
    // status updates, cancellations and reminders all land in one auditable log.
    [Table("NotificationLogs")]
    public class NotificationLog
    {
        [Key]
        public int NotificationLogId { get; set; }
        [MaxLength(15)]
        public string Phone { get; set; }
        [MaxLength(30)]
        public string Channel { get; set; } = "SMS";
        [MaxLength(30)]
        public string Type { get; set; }
        [MaxLength(20)]
        public string BookingRef { get; set; }
        [MaxLength(500)]
        public string Message { get; set; }
        public bool Success { get; set; }
        [MaxLength(300)]
        public string ErrorDetail { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    // Append-only, hash-chained audit trail: every row's Hash is a SHA-256 of its own
    // fields plus the previous row's Hash, so editing/deleting any row (or reordering)
    // breaks the chain from that point on — verifiable via AuditHelper.VerifyChain().
    [Table("AuditLogs")]
    public class AuditLog
    {
        [Key]
        public int AuditLogId { get; set; }
        [MaxLength(100)]
        public string Actor { get; set; }
        public int? ActorPatientId { get; set; }
        [MaxLength(50)]
        public string Action { get; set; }
        [MaxLength(50)]
        public string EntityType { get; set; }
        [MaxLength(50)]
        public string EntityRef { get; set; }
        [MaxLength(500)]
        public string Detail { get; set; }
        [MaxLength(50)]
        public string IPAddress { get; set; }
        public bool Success { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        [MaxLength(100)]
        public string PrevHash { get; set; }
        [MaxLength(100)]
        public string Hash { get; set; }
    }

    // Server-generated, numbered tax invoice — created once a booking's payment
    // status first becomes "Paid" (see PathlabService.MaybeGenerateInvoice).
    // Replaces the old browser-print-only "invoice" with an actual GST record.
    [Table("Invoices")]
    public class Invoice
    {
        [Key]
        public int InvoiceId { get; set; }
        public int BookingId { get; set; }
        [MaxLength(30)]
        public string InvoiceNumber { get; set; }
        [MaxLength(20)]
        public string Gstin { get; set; }
        [MaxLength(100)]
        public string PlaceOfSupply { get; set; }
        [MaxLength(10)]
        public string HsnCode { get; set; }
        public decimal Subtotal { get; set; }
        public decimal GstAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("BookingId")]
        public virtual Booking Booking { get; set; }
    }

    // Cart & Checkout Phase 4 — server-persisted cart so it survives a device
    // switch. One row per patient; CartJson is the same shape the client already
    // stores in sessionStorage, just mirrored server-side on demand.
    [Table("SavedCarts")]
    public class SavedCart
    {
        [Key]
        public int SavedCartId { get; set; }
        public int PatientId { get; set; }
        public string CartJson { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    // Cart & Checkout Phase 4 — recurring test reminders ("subscriptions"). This
    // schedules the reminder only; it does NOT auto-charge the patient. Real
    // recurring billing needs a payment-gateway mandate (e.g. Razorpay
    // Subscriptions/UPI Autopay) which isn't wired — see SubscriptionJob.
    [Table("TestSubscriptions")]
    public class TestSubscription
    {
        [Key]
        public int TestSubscriptionId { get; set; }
        public int PatientId { get; set; }
        [MaxLength(50)]
        public string TestSuiteID { get; set; }
        [MaxLength(200)]
        public string TestSuiteName { get; set; }
        public int FrequencyDays { get; set; } = 90;
        public DateTime NextDueDate { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("PatientId")]
        public virtual Patient Patient { get; set; }
    }

    // Sample Collection Phase 4 — chain of custody. A finer-grained handoff log
    // alongside SampleEvent's 5-stage status: who had the sample, in what role,
    // doing what, and where — the record an accreditation audit actually asks for.
    [Table("CustodyEvents")]
    public class CustodyEvent
    {
        [Key]
        public int CustodyEventId { get; set; }
        public int BookingId { get; set; }
        [MaxLength(100)]
        public string HandlerName { get; set; }
        [MaxLength(50)]
        public string HandlerRole { get; set; }   // Phlebotomist / Courier / LabTechnician / ...
        [MaxLength(30)]
        public string Action { get; set; }        // Collected / InTransit / ReceivedAtLab / Accessioned / ...
        [MaxLength(150)]
        public string Location { get; set; }
        [MaxLength(300)]
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("BookingId")]
        public virtual Booking Booking { get; set; }
    }

    // Notifications Phase 4 — per patient, per channel, per event-type opt-out.
    // Absence of a row means "enabled" (opt-out model, not opt-in), so existing
    // patients keep receiving notifications exactly as before this table existed.
    [Table("NotificationPreferences")]
    public class NotificationPreference
    {
        [Key]
        public int NotificationPreferenceId { get; set; }
        public int PatientId { get; set; }
        [MaxLength(30)]
        public string Channel { get; set; }   // SMS / Email / Push
        [MaxLength(30)]
        public string Type { get; set; }       // BookingConfirmation / StatusUpdate / ReportReady / ... or "*" for all
        public bool Enabled { get; set; } = true;
    }

    // Help section (Phase 3 fix) — a real, self-hosted complaint/feedback
    // channel. Replaces linking out to the legacy Feedback.cshtml page, whose
    // form actually POSTs to a third party's ("lupindiagnostics.com") API and
    // has nothing to do with this app.
    [Table("Feedbacks")]
    public class Feedback
    {
        [Key]
        public int FeedbackId { get; set; }
        public int? PatientId { get; set; }
        [MaxLength(150)]
        public string Name { get; set; }
        [MaxLength(15)]
        public string Phone { get; set; }
        [MaxLength(20)]
        public string BookingRef { get; set; }
        [MaxLength(1000)]
        public string Message { get; set; }
        [MaxLength(20)]
        public string Status { get; set; } = "Open";  // Open / Resolved
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    // Home Collection popup fix — the homepage lead-capture popup used to only
    // write to the browser's localStorage, so a lead was lost the moment the
    // visitor closed the tab and no one on staff ever saw it. This is the real,
    // server-side record staff can follow up on.
    [Table("HomeCollectionLeads")]
    public class HomeCollectionLead
    {
        [Key]
        public int HomeCollectionLeadId { get; set; }
        [Required, MaxLength(150)]
        public string Name { get; set; }
        [Required, MaxLength(15)]
        public string Mobile { get; set; }
        [MaxLength(100)]
        public string City { get; set; }
        [MaxLength(20)]
        public string Status { get; set; } = "New";  // New / Contacted / Converted / Closed
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
