using System;
using System.Collections.Generic;

namespace Pathlabfrontend.Models
{
    public class Booking
    {
        public int BookingId { get; set; }
        public string BookingRef { get; set; }
        public int PatientId { get; set; }
        public string CollectionType { get; set; }
        public string BranchName { get; set; }
        public string Address { get; set; }
        public DateTime? CollectionDate { get; set; }
        public string TimeSlot { get; set; }
        public decimal Subtotal { get; set; }
        public decimal GstAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentStatus { get; set; }
        public int SampleStatus { get; set; }
        public string BookingStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<BookingTest> Tests { get; set; }
    }

    public class BookingTest
    {
        public int BookingTestId { get; set; }
        public int BookingId { get; set; }
        public string TestSuiteID { get; set; }
        public string TestSuiteName { get; set; }
        public decimal Price { get; set; }
        public string SampleType { get; set; }
        public int TestCount { get; set; }
    }
}
