namespace Pathlabfrontend.Models
{
    public class LabTest
    {
        public int TestId { get; set; }
        public string TestSuiteID { get; set; }
        public string TestSuiteName { get; set; }
        public string ShortName { get; set; }
        public decimal Price { get; set; }
        public string SampleType { get; set; }
        public int TestCount { get; set; }
        public string ReportTime { get; set; }
        public string Fasting { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
    }
}
