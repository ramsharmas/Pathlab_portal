namespace Pathlabfrontend.Models
{
    public class HealthPackage
    {
        public int PackageId { get; set; }
        public string PackageCode { get; set; }
        public string PackageName { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public int TestCount { get; set; }
        public string SampleType { get; set; }
        public string ReportTime { get; set; }
        public string Fasting { get; set; }
        public string Badge { get; set; }
        public string Includes { get; set; }
        public bool IsActive { get; set; }
    }
}
