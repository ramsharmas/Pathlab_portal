using System;

namespace Pathlabfrontend.Models
{
    public class Report
    {
        public int ReportId { get; set; }
        public int BookingId { get; set; }
        public string BookingRef { get; set; }
        public int PatientId { get; set; }
        public string TestNames { get; set; }
        public string ReportFilePath { get; set; }
        public string Status { get; set; }
        public DateTime ReportDate { get; set; }
    }
}
