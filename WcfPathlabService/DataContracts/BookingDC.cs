using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PathlabWcfService.DataContracts
{
    [DataContract]
    public class BookingDC
    {
        [DataMember] public int BookingId { get; set; }
        [DataMember] public string BookingRef { get; set; }
        [DataMember] public int PatientId { get; set; }
        [DataMember] public string PatientName { get; set; }
        [DataMember] public string PatientPhone { get; set; }
        [DataMember] public int? FamilyMemberId { get; set; }
        [DataMember] public string FamilyMemberName { get; set; }
        [DataMember] public string CollectionType { get; set; }
        [DataMember] public string BranchName { get; set; }
        [DataMember] public string Address { get; set; }
        [DataMember] public DateTime? CollectionDate { get; set; }
        [DataMember] public string TimeSlot { get; set; }
        [DataMember] public decimal Subtotal { get; set; }
        [DataMember] public string PromoCode { get; set; }
        [DataMember] public decimal DiscountAmount { get; set; }
        [DataMember] public decimal GstAmount { get; set; }
        [DataMember] public decimal TotalAmount { get; set; }
        [DataMember] public string PaymentMethod { get; set; }
        [DataMember] public string PaymentStatus { get; set; }
        [DataMember] public string PaymentRef { get; set; }
        [DataMember] public DateTime? PaidAt { get; set; }
        [DataMember] public decimal AmountPaid { get; set; }
        [DataMember] public decimal? RefundAmount { get; set; }
        [DataMember] public string RefundReason { get; set; }
        [DataMember] public DateTime? RefundedAt { get; set; }
        [DataMember] public int SampleStatus { get; set; }
        [DataMember] public string BookingStatus { get; set; }
        [DataMember] public string SampleId { get; set; }
        [DataMember] public string Barcode { get; set; }
        [DataMember] public string LimsJobId { get; set; }
        [DataMember] public string LimsSyncStatus { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime CreatedAt { get; set; }
        [DataMember] public List<BookingTestDC> Tests { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Message { get; set; }
    }

    [DataContract]
    public class BookingTestDC
    {
        [DataMember] public int BookingTestId { get; set; }
        [DataMember] public int BookingId { get; set; }
        [DataMember] public string TestSuiteID { get; set; }
        [DataMember] public string TestSuiteName { get; set; }
        [DataMember] public decimal Price { get; set; }
        [DataMember] public string SampleType { get; set; }
        [DataMember] public int TestCount { get; set; }
    }

    [DataContract]
    public class SampleEventDC
    {
        [DataMember] public int Status { get; set; }
        [DataMember] public string StatusLabel { get; set; }
        [DataMember] public string Source { get; set; }
        [DataMember] public string Notes { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime CreatedAt { get; set; }
    }

    [DataContract]
    public class SampleTimelineDC
    {
        [DataMember] public string BookingRef { get; set; }
        [DataMember] public string SampleId { get; set; }
        [DataMember] public string Barcode { get; set; }
        [DataMember] public string LimsJobId { get; set; }
        [DataMember] public string LimsSyncStatus { get; set; }
        [DataMember] public string CollectionType { get; set; }
        [DataMember] public int CurrentStatus { get; set; }
        [DataMember] public string CurrentStatusLabel { get; set; }
        [DataMember] public string BookingStatus { get; set; }
        [DataMember] public List<SampleEventDC> Events { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Message { get; set; }
    }

    [DataContract]
    public class CreateBookingDC
    {
        [DataMember] public int PatientId { get; set; }
        [DataMember] public string PatientName { get; set; }
        [DataMember] public string PatientPhone { get; set; }
        [DataMember] public string PatientEmail { get; set; }
        [DataMember] public int? FamilyMemberId { get; set; }
        [DataMember] public string CollectionType { get; set; }
        [DataMember] public string BranchName { get; set; }
        [DataMember] public string CollectionAddress { get; set; }
        [DataMember] public string CollectionDate { get; set; }
        [DataMember] public string TimeSlot { get; set; }
        [DataMember] public string PromoCode { get; set; }
        [DataMember] public string PaymentMethod { get; set; }
        [DataMember] public string PaymentId { get; set; }
        [DataMember] public decimal TotalAmount { get; set; }
        [DataMember] public List<BookingTestDC> Tests { get; set; }
    }

    // Payment update pushed after booking â€” front-desk settlement of a
    // pay-at-counter booking, or a gateway webhook confirming/refunding a txn.
    [DataContract]
    public class PaymentUpdateDC
    {
        [DataMember] public string BookingRef { get; set; }
        [DataMember] public string PaymentStatus { get; set; }   // Paid / PartiallyPaid / Pending / Refunded / Failed
        [DataMember] public string PaymentMethod { get; set; }
        [DataMember] public string PaymentRef { get; set; }
        [DataMember] public string Source { get; set; }          // Gateway / FrontDesk / Portal
        [DataMember] public decimal? AmountPaid { get; set; }    // partial-payment installment amount
    }

    // Billing Phase 3 â€” refund on a Paid booking. Never overwrites the original
    // payment fields; RefundAmount/RefundReason/RefundedAt are recorded alongside them.
    [DataContract]
    public class RefundRequestDC
    {
        [DataMember] public string BookingRef { get; set; }
        [DataMember] public decimal Amount { get; set; }
        [DataMember] public string Reason { get; set; }
        [DataMember] public string Source { get; set; }
    }
}
