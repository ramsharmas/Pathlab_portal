using System;
using System.Runtime.Serialization;

namespace PathlabWcfService.DataContracts
{
    [DataContract]
    public class AuditLogDC
    {
        [DataMember] public int AuditLogId { get; set; }
        [DataMember] public string Actor { get; set; }
        [DataMember] public int? ActorPatientId { get; set; }
        [DataMember] public string Action { get; set; }
        [DataMember] public string EntityType { get; set; }
        [DataMember] public string EntityRef { get; set; }
        [DataMember] public string Detail { get; set; }
        [DataMember] public string IPAddress { get; set; }
        [DataMember] public bool Success { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime CreatedAt { get; set; }
        [DataMember] public string Message { get; set; }
    }

    [DataContract]
    public class AuditVerifyResultDC
    {
        [DataMember] public bool Intact { get; set; }
        [DataMember] public int TotalRows { get; set; }
        [DataMember] public int BrokenAtId { get; set; }
    }
}
