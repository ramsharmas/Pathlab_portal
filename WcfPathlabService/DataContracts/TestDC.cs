using System;
using System.Runtime.Serialization;

namespace PathlabWcfService.DataContracts
{
    [DataContract]
    public class TestDC
    {
        [DataMember] public int TestId { get; set; }
        [DataMember] public string TestSuiteID { get; set; }
        [DataMember] public string TestSuiteName { get; set; }
        [DataMember] public string ShortName { get; set; }
        [DataMember] public decimal Price { get; set; }
        [DataMember] public string SampleType { get; set; }
        [DataMember] public int TestCount { get; set; }
        [DataMember] public string ReportTime { get; set; }
        [DataMember] public string Fasting { get; set; }
        [DataMember] public string Category { get; set; }
        [DataMember] public string Description { get; set; }
        [DataMember] public string TestType { get; set; }   // "Test" | "Package" — drives landing-page badge
        [DataMember] public bool IsActive { get; set; }
        [DataMember] public DateTime? LimsSyncedAt { get; set; }
    }

    [DataContract]
    public class PackageDC
    {
        [DataMember] public int PackageId { get; set; }
        [DataMember] public string PackageCode { get; set; }
        [DataMember] public string PackageName { get; set; }
        [DataMember] public string Description { get; set; }
        [DataMember] public decimal Price { get; set; }
        [DataMember] public decimal OriginalPrice { get; set; }
        [DataMember] public int TestCount { get; set; }
        [DataMember] public string SampleType { get; set; }
        [DataMember] public string ReportTime { get; set; }
        [DataMember] public string Fasting { get; set; }
        [DataMember] public string Badge { get; set; }
        [DataMember] public string Includes { get; set; }
        [DataMember] public bool IsActive { get; set; }
    }
}
