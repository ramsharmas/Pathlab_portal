using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using PathlabWcfService.DataContracts;
using PathlabWcfService.Model;

namespace PathlabWcfService.Helpers
{
    // Append-only audit trail with a SHA-256 hash chain: each row hashes its own
    // fields + the previous row's hash. Nothing in this codebase ever updates or
    // deletes an AuditLog row — only Log() inserts and VerifyChain() reads — so a
    // hash mismatch on replay means a row was edited/deleted/reordered outside the app.
    public static class AuditHelper
    {
        private static readonly object _chainLock = new object();

        public static void Log(string actor, int? actorPatientId, string action, string entityType, string entityRef, string detail, bool success = true)
        {
            try
            {
                string ip = GetClientIp();
                lock (_chainLock)
                {
                    using (var db = new PathlabDbContext())
                    {
                        string prevHash = db.AuditLogs.OrderByDescending(a => a.AuditLogId)
                            .Select(a => a.Hash).FirstOrDefault() ?? "GENESIS";

                        var entry = new AuditLog
                        {
                            Actor = string.IsNullOrWhiteSpace(actor) ? "Guest" : actor,
                            ActorPatientId = actorPatientId,
                            Action = action,
                            EntityType = entityType,
                            EntityRef = entityRef,
                            Detail = (detail ?? "").Length > 500 ? detail.Substring(0, 500) : detail,
                            IPAddress = ip,
                            Success = success,
                            CreatedAt = DateTime.Now,
                            PrevHash = prevHash
                        };
                        entry.Hash = ComputeHash(entry);
                        db.AuditLogs.Add(entry);
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                // Audit logging must never break the calling flow.
                System.Diagnostics.Trace.TraceError("[Audit] Failed to write AuditLog: " + ex.Message);
            }
        }

        public static string GetClientIp()
        {
            try
            {
                var ctx = HttpContext.Current;
                if (ctx == null) return null;
                string ip = ctx.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
                if (string.IsNullOrWhiteSpace(ip)) ip = ctx.Request.UserHostAddress;
                return ip;
            }
            catch { return null; }
        }

        private static string ComputeHash(AuditLog e)
        {
            string raw = string.Join("|",
                e.PrevHash, e.Actor, e.Action, e.EntityType, e.EntityRef,
                e.Detail, e.IPAddress, e.Success, e.CreatedAt.Ticks.ToString());
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return Convert.ToBase64String(bytes);
            }
        }

        // Recomputes every row's hash from its stored fields and compares against
        // what's stored — returns the first row where they diverge, or Intact=true.
        public static AuditVerifyResultDC VerifyChain()
        {
            using (var db = new PathlabDbContext())
            {
                var rows = db.AuditLogs.OrderBy(a => a.AuditLogId).ToList();
                string prev = "GENESIS";
                foreach (var r in rows)
                {
                    if (r.PrevHash != prev || ComputeHash(r) != r.Hash)
                        return new AuditVerifyResultDC { Intact = false, BrokenAtId = r.AuditLogId, TotalRows = rows.Count };
                    prev = r.Hash;
                }
                return new AuditVerifyResultDC { Intact = true, BrokenAtId = 0, TotalRows = rows.Count };
            }
        }
    }
}
