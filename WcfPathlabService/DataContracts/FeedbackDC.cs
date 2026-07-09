using System;
using System.Runtime.Serialization;

namespace PathlabWcfService.DataContracts
{
    // Home Collection popup fix â€” real server-side lead capture (see
    // HomeCollectionLead entity comment for why this replaces localStorage-only).
    [DataContract]
    public class HomeCollectionLeadDC
    {
        [DataMember] public int HomeCollectionLeadId { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string Mobile { get; set; }
        [DataMember] public string City { get; set; }
        [DataMember] public string Status { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime CreatedAt { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Message { get; set; }
    }

    // Help section â€” real, self-hosted complaint/feedback (see Feedback entity
    // comment for why this replaces linking out to the legacy page).
    [DataContract]
    public class FeedbackDC
    {
        [DataMember] public int FeedbackId { get; set; }
        [DataMember] public int? PatientId { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string Phone { get; set; }
        [DataMember] public string BookingRef { get; set; }
        [DataMember] public string Message { get; set; }
        [DataMember] public string Status { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime CreatedAt { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember] public string ResponseMessage { get; set; }
    }
}
