using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PathlabWcfService.DataContracts
{
    [DataContract]
    public class LimsSyncResultDC
    {
        [DataMember] public bool Success { get; set; }
        [DataMember] public int Added { get; set; }
        [DataMember] public int Updated { get; set; }
        [DataMember] public string Message { get; set; }
    }

    // Practical stand-in for a combined Website+LIMS KPI dashboard: since the
    // real LIMS has no reporting API available yet, this surfaces how much of
    // the Website side is actually synced, so staff can see the integration's
    // health at a glance instead of the two systems drifting silently.
    [DataContract]
    public class LimsSyncStatusDC
    {
        [DataMember] public bool LimsConfigured { get; set; }
        [DataMember] public int PatientsSynced { get; set; }
        [DataMember] public int PatientsPending { get; set; }
        [DataMember] public int BookingsSynced { get; set; }
        [DataMember] public int BookingsPending { get; set; }
        [DataMember] public int BookingsFailed { get; set; }
        [DataMember] public DateTime? LastCatalogueSync { get; set; }
        [DataMember] public int TestsNeverSynced { get; set; }
    }

    // Receiving endpoint for the LIMS to push a finished report back to the
    // portal (or for lab staff to attach one manually while LIMS push isn't wired).
    [DataContract]
    public class ReportAttachDC
    {
        [DataMember] public string BookingRef { get; set; }
        [DataMember] public string LimsJobId { get; set; }
        [DataMember] public string ReportFilePath { get; set; }
        [DataMember] public string Source { get; set; }
    }

    [DataContract]
    public class InvoiceDC
    {
        [DataMember] public int InvoiceId { get; set; }
        [DataMember] public string InvoiceNumber { get; set; }
        [DataMember] public string BookingRef { get; set; }
        [DataMember] public string Gstin { get; set; }
        [DataMember] public string PlaceOfSupply { get; set; }
        [DataMember] public string HsnCode { get; set; }
        [DataMember] public decimal Subtotal { get; set; }
        [DataMember] public decimal GstAmount { get; set; }
        [DataMember] public decimal TotalAmount { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime CreatedAt { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Message { get; set; }
    }
}
