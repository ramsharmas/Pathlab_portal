using System;
using System.Runtime.Serialization;

namespace PathlabWcfService.DataContracts
{
    [DataContract]
    public class ReportDC
    {
        [DataMember] public int ReportId { get; set; }
        [DataMember] public string BookingRef { get; set; }
        [DataMember] public int PatientId { get; set; }
        [DataMember] public string PatientName { get; set; }
        [DataMember] public string TestNames { get; set; }
        [DataMember] public string ReportFilePath { get; set; }
        [DataMember] public string Status { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime ReportDate { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Message { get; set; }
    }

    [DataContract]
    public class AdminStatsDC
    {
        [DataMember] public int TotalPatients { get; set; }
        [DataMember] public int TotalBookings { get; set; }
        [DataMember] public int TodayBookings { get; set; }
        [DataMember] public int PendingReports { get; set; }
        [DataMember] public decimal TotalRevenue { get; set; }
        [DataMember] public decimal MonthRevenue { get; set; }
    }

    [DataContract]
    public class StaffAlertDC
    {
        [DataMember] public string Level { get; set; }       // "warning" | "danger"
        [DataMember] public string Icon { get; set; }
        [DataMember] public string Title { get; set; }
        [DataMember] public string Detail { get; set; }
        [DataMember] public string BookingRef { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime CreatedAt { get; set; }
    }

    [DataContract]
    public class NotificationLogDC
    {
        [DataMember] public int NotificationLogId { get; set; }
        [DataMember] public string Phone { get; set; }
        [DataMember] public string Channel { get; set; }
        [DataMember] public string Type { get; set; }
        [DataMember] public string BookingRef { get; set; }
        [DataMember] public string Message { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember] public string ErrorDetail { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime CreatedAt { get; set; }
    }
}
