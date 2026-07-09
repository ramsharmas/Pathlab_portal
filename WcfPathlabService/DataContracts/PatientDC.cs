using System;
using System.Runtime.Serialization;

namespace PathlabWcfService.DataContracts
{
    [DataContract]
    public class PatientDC
    {
        [DataMember] public int PatientId { get; set; }
        [DataMember] public string FullName { get; set; }
        [DataMember] public string Phone { get; set; }
        [DataMember] public string Email { get; set; }
        [DataMember] public string Gender { get; set; }
        [DataMember] public DateTime? DateOfBirth { get; set; }
        [DataMember] public string Address { get; set; }
        [DataMember] public string City { get; set; }
        [DataMember] public string Pincode { get; set; }
        [DataMember] public string PasswordHash { get; set; }
        [DataMember] public bool IsActive { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime CreatedDate { get; set; }
        [DataMember] public string LimsPatientId { get; set; }
        [DataMember] public string LimsSyncStatus { get; set; }
        [DataMember] public string Token { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Message { get; set; }
    }
}
