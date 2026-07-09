using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PathlabWcfService.DataContracts
{
    // Patient Registration Phase 3 â€” server-side family profiles.
    [DataContract]
    public class FamilyMemberDC
    {
        [DataMember] public int FamilyMemberId { get; set; }
        [DataMember] public int PatientId { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string Relation { get; set; }
        [DataMember] public string Gender { get; set; }
        [DataMember] public DateTime? DateOfBirth { get; set; }
        [DataMember] public string Phone { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Message { get; set; }
    }

    // Cart & Checkout Phase 3 â€” promo code validation. The server computes
    // DiscountAmount from the current cart subtotal; the client only displays it.
    [DataContract]
    public class PromoValidationDC
    {
        [DataMember] public string Code { get; set; }
        [DataMember] public decimal DiscountAmount { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Message { get; set; }
    }

    // Report Delivery Phase 3 â€” share a ready report with a doctor by email.
    // Honest about the SMTP dependency: Success reflects whether the email
    // actually sent (see EmailHelper), not just whether the request was accepted.
    [DataContract]
    public class ShareReportDC
    {
        [DataMember] public string BookingRef { get; set; }
        [DataMember] public string DoctorName { get; set; }
        [DataMember] public string DoctorEmail { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Message { get; set; }
    }

    // Report Delivery â€” the doctor/WhatsApp/email "share" link used to be built
    // client-side from the plain (sequential, guessable) BookingRef. This is the
    // random opaque token minted per-booking so the shared link can't be
    // enumerated by guessing booking references.
    [DataContract]
    public class ShareTokenDC
    {
        [DataMember] public string Token { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Message { get; set; }
    }

    // Sample Collection Phase 3 â€” phlebotomist-facing collection queue (a
    // mobile-friendly staff web view; see AdminController "Collection Queue" tab).
    [DataContract]
    public class CollectionQueueItemDC
    {
        [DataMember] public string BookingRef { get; set; }
        [DataMember] public string PatientName { get; set; }
        [DataMember] public string PatientPhone { get; set; }
        [DataMember] public string Address { get; set; }
        [DataMember] public DateTime? CollectionDate { get; set; }
        [DataMember] public string TimeSlot { get; set; }
        [DataMember] public string SampleId { get; set; }
        [DataMember] public int SampleStatus { get; set; }
    }

    // Analytics Phase 3 â€” TAT / revenue trend / test volume, with optional
    // date-range filtering (GetAnalyticsSummary?from=&to=).
    [DataContract]
    public class DayMetricDC
    {
        [DataMember] public string Date { get; set; }
        [DataMember] public int Bookings { get; set; }
        [DataMember] public decimal Revenue { get; set; }
    }

    [DataContract]
    public class TestVolumeDC
    {
        [DataMember] public string TestSuiteName { get; set; }
        [DataMember] public int Count { get; set; }
    }

    [DataContract]
    public class AnalyticsSummaryDC
    {
        [DataMember(EmitDefaultValue = false)] public DateTime FromDate { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime ToDate { get; set; }
        [DataMember] public decimal TotalRevenue { get; set; }
        [DataMember] public int TotalBookings { get; set; }
        [DataMember] public double AvgTatHours { get; set; }   // booking created -> report ready
        [DataMember] public List<DayMetricDC> DailyTrend { get; set; }
        [DataMember] public List<TestVolumeDC> TestVolume { get; set; }
    }
}
