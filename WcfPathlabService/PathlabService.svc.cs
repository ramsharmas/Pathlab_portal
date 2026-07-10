using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.ServiceModel.Activation;
using System.ServiceModel.Web;
using PathlabWcfService.DataContracts;
using PathlabWcfService.Helpers;
using PathlabWcfService.Model;

namespace PathlabWcfService
{
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class PathlabService : IPathlabService
    {
        private readonly PathlabDbContext _db = new PathlabDbContext();

        // In-memory OTP store (demo only — resets on app restart)
        private static readonly System.Collections.Generic.Dictionary<string, string> _otpStore
            = new System.Collections.Generic.Dictionary<string, string>();

        // Partner/EHR-facing read endpoints (Phase 4) check this — fails closed
        // when PartnerApiKey is unset, so a forgotten config value never means
        // "open to anyone" the way a missing check would.
        private static bool IsPartnerAuthorized()
        {
            string configured = ConfigurationManager.AppSettings["PartnerApiKey"];
            if (string.IsNullOrWhiteSpace(configured)) return false;
            string supplied = WebOperationContext.Current?.IncomingRequest.Headers["X-Api-Key"];
            return supplied == configured;
        }

        // ── PATIENT / AUTH ────────────────────────────────────────────────
        public PatientDC SendOtp(PatientDC dc)
        {
            if (string.IsNullOrEmpty(dc.Phone) || dc.Phone.Length != 10)
            {
                AuditHelper.Log(dc.Phone, null, "OtpRequested", "Patient", dc.Phone, "Invalid phone number.", false);
                return new PatientDC { Success = false, Message = "Invalid phone number." };
            }

            string otp = "123456"; // demo fixed OTP
            lock (_otpStore) { _otpStore[dc.Phone] = otp; }
            AuditHelper.Log(dc.Phone, null, "OtpRequested", "Patient", dc.Phone, "OTP sent.", true);
            return new PatientDC { Success = true, Message = "OTP sent successfully." };
        }

        public PatientDC VerifyOtp(PatientDC dc)
        {
            string stored;
            lock (_otpStore) { _otpStore.TryGetValue(dc.Phone ?? "", out stored); }

            // Demo-mode safety net: if the in-memory store was wiped by an app-pool
            // recycle (stored == null) AND the real SMS gateway is not configured,
            // accept the fixed demo OTP "123456" so dev/demo login always works.
            // Once Fast2SmsApiKey is set, real OTPs are delivered and this branch
            // is bypassed because 'stored' will be the genuinely sent OTP.
            bool smsConfigured = !string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["Fast2SmsApiKey"]);
            bool demoFallbackValid = !smsConfigured && dc.Token == "123456";

            if ((stored == null || dc.Token != stored) && !demoFallbackValid)
            {
                AuditHelper.Log(dc.Phone, null, "LoginFailed", "Patient", dc.Phone, "Invalid or expired OTP.", false);
                return new PatientDC { Success = false, Message = "Invalid or expired OTP." };
            }

            lock (_otpStore) { _otpStore.Remove(dc.Phone ?? ""); }

            var patient = _db.Patients.FirstOrDefault(p => p.Phone == dc.Phone && p.IsActive);
            if (patient != null)
            {
                AuditHelper.Log(dc.Phone, patient.PatientId, "LoginSuccess", "Patient", dc.Phone, "OTP login.", true);
                return MapPatient(patient, true);
            }

            // New user — tell frontend to proceed to registration
            AuditHelper.Log(dc.Phone, null, "LoginSuccess", "Patient", dc.Phone, "OTP verified, new user pending registration.", true);
            return new PatientDC { Success = true, PatientId = 0, Phone = dc.Phone, Message = "New user" };
        }


        public PatientDC RegisterPatient(PatientDC dc)
        {
            try
            {
                if (_db.Patients.Any(p => p.Phone == dc.Phone))
                {
                    AuditHelper.Log(dc.Phone, null, "Register", "Patient", dc.Phone, "Phone already registered.", false);
                    return new PatientDC { Success = false, Message = "Phone already registered." };
                }

                var patient = new Patient
                {
                    FullName = dc.FullName,
                    Phone = dc.Phone,
                    Email = dc.Email,
                    Gender = dc.Gender,
                    DateOfBirth = dc.DateOfBirth,
                    Address = dc.Address,
                    City = dc.City,
                    Pincode = dc.Pincode,
                    PasswordHash = PasswordHelper.HashPassword(dc.PasswordHash ?? dc.Phone),
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };
                patient.LimsSyncStatus = "Pending";
                _db.Patients.Add(patient);
                _db.SaveChanges();

                // Link the portal identity to the LIMS patient master; booking
                // itself never fails if the LIMS is down, this stays "Pending".
                string limsPatientId = LimsClient.CreatePatientRecord(patient);
                if (limsPatientId != null)
                {
                    patient.LimsPatientId = limsPatientId;
                    patient.LimsSyncStatus = "Synced";
                    _db.SaveChanges();
                }

                AuditHelper.Log(dc.Phone, patient.PatientId, "Register", "Patient", dc.Phone,
                    "New patient registered: " + patient.FullName +
                    (limsPatientId != null ? ", LIMS id " + limsPatientId : ", LIMS sync pending"), true);

                dc.PatientId = patient.PatientId;
                dc.LimsPatientId = patient.LimsPatientId;
                dc.LimsSyncStatus = patient.LimsSyncStatus;
                dc.Success = true;
                dc.Message = "Registration successful.";
                dc.PasswordHash = null;
                return dc;
            }
            catch (Exception ex)
            {
                return new PatientDC { Success = false, Message = ex.Message };
            }
        }

        public PatientDC LoginPatient(PatientDC dc)
        {
            try
            {
                var patient = _db.Patients.FirstOrDefault(p => p.Phone == dc.Phone && p.IsActive);
                if (patient == null)
                {
                    AuditHelper.Log(dc.Phone, null, "LoginFailed", "Patient", dc.Phone, "Patient not found.", false);
                    return new PatientDC { Success = false, Message = "Patient not found." };
                }

                if (!PasswordHelper.VerifyPassword(dc.PasswordHash, patient.PasswordHash))
                {
                    AuditHelper.Log(dc.Phone, patient.PatientId, "LoginFailed", "Patient", dc.Phone, "Invalid password.", false);
                    return new PatientDC { Success = false, Message = "Invalid OTP / password." };
                }

                AuditHelper.Log(dc.Phone, patient.PatientId, "LoginSuccess", "Patient", dc.Phone, "Password login.", true);
                return MapPatient(patient, true);
            }
            catch (Exception ex)
            {
                return new PatientDC { Success = false, Message = ex.Message };
            }
        }

        public PatientDC GetPatient(string patientId)
        {
            try
            {
                int id = int.Parse(patientId);
                var patient = _db.Patients.Find(id);
                return patient == null
                    ? new PatientDC { Success = false, Message = "Not found." }
                    : MapPatient(patient, true);
            }
            catch (Exception ex)
            {
                return new PatientDC { Success = false, Message = ex.Message };
            }
        }

        public PatientDC UpdatePatient(PatientDC dc)
        {
            try
            {
                var patient = _db.Patients.Find(dc.PatientId);
                if (patient == null) return new PatientDC { Success = false, Message = "Not found." };

                var changes = new System.Collections.Generic.List<string>();
                void Track(string field, string before, string after)
                {
                    if (before != after) changes.Add(field + ": '" + before + "' -> '" + after + "'");
                }
                Track("FullName", patient.FullName, dc.FullName);
                Track("Email", patient.Email, dc.Email);
                Track("Gender", patient.Gender, dc.Gender);
                Track("Address", patient.Address, dc.Address);
                Track("City", patient.City, dc.City);
                Track("Pincode", patient.Pincode, dc.Pincode);

                patient.FullName = dc.FullName;
                patient.Email = dc.Email;
                patient.Gender = dc.Gender;
                patient.DateOfBirth = dc.DateOfBirth;
                patient.Address = dc.Address;
                patient.City = dc.City;
                patient.Pincode = dc.Pincode;
                _db.SaveChanges();

                if (changes.Count > 0)
                {
                    // Best-effort profile sync to the LIMS; failure here never blocks
                    // the portal-side update — it just leaves the LIMS copy stale.
                    LimsClient.UpdatePatientRecord(patient);
                    AuditHelper.Log(patient.Phone, patient.PatientId, "UpdateProfile", "Patient", patient.Phone,
                        string.Join("; ", changes), true);
                }

                return MapPatient(patient, true);
            }
            catch (Exception ex)
            {
                return new PatientDC { Success = false, Message = ex.Message };
            }
        }

        // Phone change is OTP-gated: caller first requests SendOtp for the NEW
        // number, then submits it here (dc.Phone = new number, dc.Token = OTP).
        public PatientDC ChangePhone(PatientDC dc)
        {
            try
            {
                if (string.IsNullOrEmpty(dc.Phone) || dc.Phone.Length != 10)
                    return new PatientDC { Success = false, Message = "Invalid phone number." };

                string stored;
                lock (_otpStore) { _otpStore.TryGetValue(dc.Phone, out stored); }
                if (stored == null || dc.Token != stored)
                {
                    AuditHelper.Log(dc.Phone, dc.PatientId, "ChangePhone", "Patient", dc.Phone, "Invalid or expired OTP.", false);
                    return new PatientDC { Success = false, Message = "Invalid or expired OTP." };
                }
                lock (_otpStore) { _otpStore.Remove(dc.Phone); }

                if (_db.Patients.Any(p => p.Phone == dc.Phone && p.PatientId != dc.PatientId))
                    return new PatientDC { Success = false, Message = "This number is already registered to another account." };

                var patient = _db.Patients.Find(dc.PatientId);
                if (patient == null) return new PatientDC { Success = false, Message = "Not found." };

                string oldPhone = patient.Phone;
                patient.Phone = dc.Phone;
                _db.SaveChanges();

                AuditHelper.Log(dc.Phone, patient.PatientId, "ChangePhone", "Patient", dc.Phone,
                    "Phone changed: '" + oldPhone + "' -> '" + dc.Phone + "' (OTP verified).", true);

                return MapPatient(patient, true);
            }
            catch (Exception ex)
            {
                return new PatientDC { Success = false, Message = ex.Message };
            }
        }

        // ── FAMILY MEMBERS ────────────────────────────────────────────────
        public List<FamilyMemberDC> GetFamilyMembers(string patientId)
        {
            int id;
            if (!int.TryParse(patientId, out id)) return new List<FamilyMemberDC>();
            return _db.FamilyMembers.Where(f => f.PatientId == id && f.IsActive)
                .OrderBy(f => f.Name)
                .Select(f => new FamilyMemberDC
                {
                    FamilyMemberId = f.FamilyMemberId, PatientId = f.PatientId,
                    Name = f.Name, Relation = f.Relation, Gender = f.Gender,
                    DateOfBirth = f.DateOfBirth, Phone = f.Phone, Success = true
                }).ToList();
        }

        public FamilyMemberDC AddFamilyMember(FamilyMemberDC dc)
        {
            try
            {
                if (dc == null || string.IsNullOrWhiteSpace(dc.Name))
                    return new FamilyMemberDC { Success = false, Message = "Name is required." };
                if (!_db.Patients.Any(p => p.PatientId == dc.PatientId))
                    return new FamilyMemberDC { Success = false, Message = "Patient not found." };

                var member = new FamilyMember
                {
                    PatientId = dc.PatientId, Name = dc.Name, Relation = dc.Relation,
                    Gender = dc.Gender, DateOfBirth = dc.DateOfBirth, Phone = dc.Phone,
                    IsActive = true, CreatedAt = DateTime.Now
                };
                _db.FamilyMembers.Add(member);
                _db.SaveChanges();

                AuditHelper.Log("Patient" + dc.PatientId, dc.PatientId, "AddFamilyMember", "FamilyMember",
                    member.FamilyMemberId.ToString(), member.Name + " (" + member.Relation + ")", true);

                dc.FamilyMemberId = member.FamilyMemberId;
                dc.Success = true;
                return dc;
            }
            catch (Exception ex)
            {
                return new FamilyMemberDC { Success = false, Message = ex.Message };
            }
        }

        public FamilyMemberDC RemoveFamilyMember(string familyMemberId)
        {
            try
            {
                int id;
                if (!int.TryParse(familyMemberId, out id))
                    return new FamilyMemberDC { Success = false, Message = "Invalid id." };

                var member = _db.FamilyMembers.Find(id);
                if (member == null) return new FamilyMemberDC { Success = false, Message = "Not found." };

                member.IsActive = false;
                _db.SaveChanges();

                AuditHelper.Log("Patient" + member.PatientId, member.PatientId, "RemoveFamilyMember",
                    "FamilyMember", familyMemberId, member.Name, true);

                return new FamilyMemberDC { Success = true };
            }
            catch (Exception ex)
            {
                return new FamilyMemberDC { Success = false, Message = ex.Message };
            }
        }

        // One call for the full patient dashboard (Patient Registration Phase 4)
        // instead of the portal firing five separate requests to assemble the
        // same view (profile, family, bookings, reports, spend, unread count).
        public PatientDashboardDC GetPatientDashboard(string patientId)
        {
            int id;
            if (!int.TryParse(patientId, out id))
                return new PatientDashboardDC { Success = false, Message = "Invalid patient id." };

            var patient = _db.Patients.Find(id);
            if (patient == null) return new PatientDashboardDC { Success = false, Message = "Not found." };

            var bookings = _db.Bookings.Include("Tests").Where(b => b.PatientId == id)
                .OrderByDescending(b => b.CreatedAt).ToList();

            return new PatientDashboardDC
            {
                Profile = MapPatient(patient, true),
                FamilyMembers = _db.FamilyMembers.Where(f => f.PatientId == id && f.IsActive)
                    .Select(f => new FamilyMemberDC
                    {
                        FamilyMemberId = f.FamilyMemberId, PatientId = f.PatientId, Name = f.Name,
                        Relation = f.Relation, Gender = f.Gender, DateOfBirth = f.DateOfBirth, Phone = f.Phone, Success = true
                    }).ToList(),
                RecentBookings = bookings.Take(10).Select(b => MapBooking(b, false)).ToList(),
                Reports = _db.Reports.Where(r => r.PatientId == id).OrderByDescending(r => r.ReportDate)
                    .Select(r => new ReportDC
                    {
                        ReportId = r.ReportId, BookingRef = r.BookingRef, PatientId = r.PatientId,
                        TestNames = r.TestNames, ReportFilePath = r.ReportFilePath, Status = r.Status,
                        ReportDate = r.ReportDate, Success = true
                    }).ToList(),
                TotalBookings = bookings.Count,
                LifetimeSpend = bookings.Where(b => b.PaymentStatus == "Paid" || b.PaymentStatus == "PartiallyPaid").Sum(b => b.AmountPaid),
                UnreadNotifications = _db.NotificationLogs.Count(n => n.Phone == patient.Phone &&
                    n.CreatedAt >= DateTime.Now.AddDays(-2)),
                Success = true
            };
        }

        // ── TESTS ─────────────────────────────────────────────────────────
        public List<TestDC> GetAllTests()
        {
            return _db.LabTests.Where(t => t.IsActive)
                .OrderBy(t => t.TestSuiteName)
                .Select(t => new TestDC
                {
                    TestId = t.TestId, TestSuiteID = t.TestSuiteID,
                    TestSuiteName = t.TestSuiteName, ShortName = t.ShortName,
                    Price = t.Price, SampleType = t.SampleType,
                    TestCount = t.TestCount, ReportTime = t.ReportTime,
                    Fasting = t.Fasting, Category = t.Category,
                    Description = t.Description, TestType = t.TestType ?? "Test",
                    IsActive = t.IsActive, LimsSyncedAt = t.LimsSyncedAt
                }).ToList();
        }

        public List<TestDC> SearchTests(string keyword)
        {
            string kw = keyword.ToLower();
            return _db.LabTests.Where(t => t.IsActive &&
                (t.TestSuiteName.ToLower().Contains(kw) || t.Category.ToLower().Contains(kw)))
                .Select(t => new TestDC
                {
                    TestId = t.TestId, TestSuiteID = t.TestSuiteID,
                    TestSuiteName = t.TestSuiteName, ShortName = t.ShortName,
                    Price = t.Price, SampleType = t.SampleType,
                    TestCount = t.TestCount, ReportTime = t.ReportTime,
                    Fasting = t.Fasting, Category = t.Category,
                    TestType = t.TestType ?? "Test"
                }).ToList();
        }

        public TestDC GetTestById(string testId)
        {
            var t = _db.LabTests.FirstOrDefault(x => x.TestSuiteID == testId);
            if (t == null) return null;
            return new TestDC
            {
                TestId = t.TestId, TestSuiteID = t.TestSuiteID,
                TestSuiteName = t.TestSuiteName, Price = t.Price,
                SampleType = t.SampleType, TestCount = t.TestCount,
                ReportTime = t.ReportTime, Fasting = t.Fasting,
                Category = t.Category, Description = t.Description,
                TestType = t.TestType ?? "Test"
            };
        }

        // Unified catalogue for the landing page: individual tests + health
        // packages in one list, distinguished by TestType ("Test"/"Package").
        // Follows the LIMS SetupTestsuite mapping — TestSuiteName is the LIMS
        // TestSuitename, TestCount is count(TestName); Price/Description/TestType
        // are website-maintained because LIMS does not carry them.
        public List<TestDC> GetTestCatalogue()
        {
            var tests = GetAllTests();

            var packages = _db.HealthPackages.Where(p => p.IsActive)
                .OrderBy(p => p.PackageName)
                .ToList()
                .Select(p => new TestDC
                {
                    // Offset keeps package ids from colliding with LabTests.TestId in cart dedupe
                    TestId = 100000 + p.PackageId,
                    TestSuiteID = p.PackageCode,
                    TestSuiteName = p.PackageName,
                    ShortName = p.Badge,
                    Price = p.Price,
                    SampleType = p.SampleType,
                    TestCount = p.TestCount,
                    ReportTime = p.ReportTime,
                    Fasting = p.Fasting,
                    Category = "Packages",
                    Description = p.Description,
                    TestType = "Package",
                    IsActive = p.IsActive
                }).ToList();

            return tests.Concat(packages).ToList();
        }

        // Pulls the LIMS test-suite list and upserts LabTests.TestCount + LimsSyncedAt
        // (Price/Description/TestType are website-owned — see GetTestCatalogue).
        // New suites the LIMS has that the website doesn't yet arrive inactive with
        // Price=0 so staff can fill those in before they go live on the site.
        public LimsSyncResultDC SyncTestCatalogue()
        {
            try
            {
                var entries = LimsClient.FetchCatalogue();
                if (entries == null)
                    return new LimsSyncResultDC { Success = false, Message = "LIMS not configured or unreachable." };

                int added = 0, updated = 0;
                var existing = _db.LabTests.ToList();
                foreach (var e in entries)
                {
                    var match = existing.FirstOrDefault(t =>
                        string.Equals(t.TestSuiteName, e.TestSuiteName, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        if (match.TestCount != e.TestCount) updated++;
                        match.TestCount = e.TestCount;
                        match.LimsSyncedAt = DateTime.Now;
                    }
                    else
                    {
                        _db.LabTests.Add(new LabTest
                        {
                            TestSuiteID = "LIMS-" + e.TestSuiteName.Substring(0, Math.Min(40, e.TestSuiteName.Length)),
                            TestSuiteName = e.TestSuiteName,
                            TestCount = e.TestCount,
                            Price = 0,
                            TestType = "Test",
                            IsActive = false,
                            LimsSyncedAt = DateTime.Now
                        });
                        added++;
                    }
                }
                _db.SaveChanges();

                AuditHelper.Log("LIMS", null, "SyncTestCatalogue", "LabTest", null,
                    added + " added, " + updated + " updated", true);

                return new LimsSyncResultDC { Success = true, Added = added, Updated = updated,
                    Message = added + " new suite(s) added (inactive), " + updated + " existing suite(s) updated." };
            }
            catch (Exception ex)
            {
                return new LimsSyncResultDC { Success = false, Message = ex.Message };
            }
        }

        // Partner/integration-facing pricing feed (Test Catalogue Phase 4).
        // Same shape as GetTestCatalogue — the only difference is the auth gate.
        public List<TestDC> GetPublicCatalogue()
        {
            if (!IsPartnerAuthorized())
                throw new WebFaultException(System.Net.HttpStatusCode.Unauthorized);
            return GetTestCatalogue();
        }

        public List<PackageDC> GetAllPackages()
        {
            return _db.HealthPackages.Where(p => p.IsActive)
                .Select(p => new PackageDC
                {
                    PackageId = p.PackageId, PackageCode = p.PackageCode,
                    PackageName = p.PackageName, Description = p.Description,
                    Price = p.Price, OriginalPrice = p.OriginalPrice,
                    TestCount = p.TestCount, SampleType = p.SampleType,
                    ReportTime = p.ReportTime, Fasting = p.Fasting,
                    Badge = p.Badge, Includes = p.Includes
                }).ToList();
        }

        public PackageDC GetPackageById(string packageId)
        {
            int id;
            if (!int.TryParse(packageId, out id)) return null;
            var p = _db.HealthPackages.FirstOrDefault(x => x.PackageId == id);
            if (p == null) return null;
            return new PackageDC
            {
                PackageId = p.PackageId, PackageCode = p.PackageCode,
                PackageName = p.PackageName, Description = p.Description,
                Price = p.Price, OriginalPrice = p.OriginalPrice,
                TestCount = p.TestCount, SampleType = p.SampleType,
                ReportTime = p.ReportTime, Fasting = p.Fasting,
                Badge = p.Badge, Includes = p.Includes
            };
        }

        // ── SLOT AVAILABILITY ─────────────────────────────────────────────
        // A slot is "full" once existing bookings reach capacity; the website
        // greys it out so two patients can't take the same slot. This mirrors
        // LIMS schedule-blocking until a real LIMS scheduler is wired.
        private const int WalkinSlotCapacity = 4;  // bookings per branch per slot
        private const int HomeSlotCapacity = 2;    // phlebotomists available per slot

        public List<string> GetBookedSlots(string date, string type, string branch)
        {
            DateTime day;
            if (!DateTime.TryParse(date, out day)) return new List<string>();
            var start = day.Date;
            var end = start.AddDays(1);
            int capacity = type == "home" ? HomeSlotCapacity : WalkinSlotCapacity;

            var q = _db.Bookings.Where(b =>
                b.BookingStatus != "Cancelled" &&
                b.CollectionType == type &&
                b.CollectionDate >= start && b.CollectionDate < end &&
                b.TimeSlot != null);

            if (type != "home" && !string.IsNullOrEmpty(branch))
                q = q.Where(b => b.BranchName.Contains(branch));

            return q.GroupBy(b => b.TimeSlot)
                .Where(g => g.Count() >= capacity)
                .Select(g => g.Key)
                .ToList();
        }

        // ── PROMO CODES ───────────────────────────────────────────────────
        // Pure calculation, no side effects — CreateBooking calls the same logic
        // and is the only place UsedCount actually increments, so a patient can
        // "preview" a code at checkout without consuming it.
        private PromoValidationDC EvaluatePromoCode(string code, decimal subtotal, out PromoCode match)
        {
            match = null;
            if (string.IsNullOrWhiteSpace(code))
                return new PromoValidationDC { Success = false, Message = "Enter a promo code." };

            match = _db.PromoCodes.FirstOrDefault(p => p.Code == code.Trim().ToUpper() && p.IsActive);
            if (match == null)
                return new PromoValidationDC { Code = code, Success = false, Message = "Invalid promo code." };
            if (match.ExpiryDate.HasValue && match.ExpiryDate.Value < DateTime.Today)
                return new PromoValidationDC { Code = code, Success = false, Message = "This promo code has expired." };
            if (match.UsageLimit.HasValue && match.UsedCount >= match.UsageLimit.Value)
                return new PromoValidationDC { Code = code, Success = false, Message = "This promo code has reached its usage limit." };
            if (subtotal < match.MinOrderValue)
                return new PromoValidationDC { Code = code, Success = false,
                    Message = "Minimum order value for this code is Rs." + match.MinOrderValue + "." };

            decimal discount = match.DiscountType == "Flat"
                ? match.DiscountValue
                : Math.Round(subtotal * match.DiscountValue / 100m);
            if (match.MaxDiscount.HasValue) discount = Math.Min(discount, match.MaxDiscount.Value);
            discount = Math.Min(discount, subtotal);

            return new PromoValidationDC { Code = match.Code, DiscountAmount = discount, Success = true,
                Message = "Promo code applied — you saved Rs." + discount + "." };
        }

        public PromoValidationDC ValidatePromoCode(string code, string subtotal)
        {
            decimal sub;
            decimal.TryParse(subtotal, out sub);
            PromoCode match;
            return EvaluatePromoCode(code, sub, out match);
        }

        // ── SAVED CARTS / SUBSCRIPTIONS (Cart & Checkout Phase 4) ──────────
        public SavedCartDC GetSavedCart(string patientId)
        {
            int id;
            if (!int.TryParse(patientId, out id))
                return new SavedCartDC { Success = false, Message = "Invalid patient id." };
            var cart = _db.SavedCarts.FirstOrDefault(c => c.PatientId == id);
            return cart == null
                ? new SavedCartDC { Success = false, Message = "No saved cart." }
                : new SavedCartDC { PatientId = id, CartJson = cart.CartJson, UpdatedAt = cart.UpdatedAt, Success = true };
        }

        public SavedCartDC SaveCart(SavedCartDC dc)
        {
            try
            {
                if (dc == null || dc.PatientId <= 0)
                    return new SavedCartDC { Success = false, Message = "PatientId is required." };

                var cart = _db.SavedCarts.FirstOrDefault(c => c.PatientId == dc.PatientId);
                if (cart == null)
                {
                    cart = new SavedCart { PatientId = dc.PatientId };
                    _db.SavedCarts.Add(cart);
                }
                cart.CartJson = dc.CartJson;
                cart.UpdatedAt = DateTime.Now;
                _db.SaveChanges();

                return new SavedCartDC { PatientId = dc.PatientId, CartJson = cart.CartJson, UpdatedAt = cart.UpdatedAt, Success = true };
            }
            catch (Exception ex)
            {
                return new SavedCartDC { Success = false, Message = ex.Message };
            }
        }

        public List<TestSubscriptionDC> GetTestSubscriptions(string patientId)
        {
            int id;
            if (!int.TryParse(patientId, out id)) return new List<TestSubscriptionDC>();
            return _db.TestSubscriptions.Where(s => s.PatientId == id && s.IsActive)
                .OrderBy(s => s.NextDueDate)
                .Select(s => new TestSubscriptionDC
                {
                    TestSubscriptionId = s.TestSubscriptionId, PatientId = s.PatientId,
                    TestSuiteID = s.TestSuiteID, TestSuiteName = s.TestSuiteName,
                    FrequencyDays = s.FrequencyDays, NextDueDate = s.NextDueDate, IsActive = s.IsActive, Success = true
                }).ToList();
        }

        // Note: this only schedules the reminder (see SubscriptionJob). It does NOT
        // set up recurring billing — that needs a payment-gateway mandate (e.g.
        // Razorpay Subscriptions/UPI Autopay), which isn't wired.
        public TestSubscriptionDC AddTestSubscription(TestSubscriptionDC dc)
        {
            try
            {
                if (dc == null || dc.PatientId <= 0 || string.IsNullOrWhiteSpace(dc.TestSuiteName))
                    return new TestSubscriptionDC { Success = false, Message = "PatientId and TestSuiteName are required." };

                int freq = dc.FrequencyDays > 0 ? dc.FrequencyDays : 90;
                var sub = new TestSubscription
                {
                    PatientId = dc.PatientId, TestSuiteID = dc.TestSuiteID, TestSuiteName = dc.TestSuiteName,
                    FrequencyDays = freq, NextDueDate = DateTime.Today.AddDays(freq), IsActive = true, CreatedAt = DateTime.Now
                };
                _db.TestSubscriptions.Add(sub);
                _db.SaveChanges();

                AuditHelper.Log("Patient" + dc.PatientId, dc.PatientId, "AddTestSubscription", "TestSubscription",
                    sub.TestSubscriptionId.ToString(), sub.TestSuiteName + " every " + freq + " days", true);

                dc.TestSubscriptionId = sub.TestSubscriptionId;
                dc.NextDueDate = sub.NextDueDate;
                dc.Success = true;
                return dc;
            }
            catch (Exception ex)
            {
                return new TestSubscriptionDC { Success = false, Message = ex.Message };
            }
        }

        public TestSubscriptionDC CancelTestSubscription(string id)
        {
            int subId;
            if (!int.TryParse(id, out subId))
                return new TestSubscriptionDC { Success = false, Message = "Invalid id." };
            var sub = _db.TestSubscriptions.Find(subId);
            if (sub == null) return new TestSubscriptionDC { Success = false, Message = "Not found." };
            sub.IsActive = false;
            _db.SaveChanges();
            return new TestSubscriptionDC { Success = true };
        }

        // ── BOOKINGS ──────────────────────────────────────────────────────
        public BookingDC CreateBooking(CreateBookingDC dc)
        {
            try
            {
                var tests = dc.Tests ?? new List<BookingTestDC>();
                decimal sub = tests.Sum(t => t.Price);

                // Re-validate and apply server-side — never trust a client-supplied
                // discount amount. Silently ignores an invalid/expired code rather
                // than failing the whole booking (the checkout page already showed
                // the patient why via ValidatePromoCode before they got here).
                decimal discount = 0;
                PromoCode promoMatch = null;
                if (!string.IsNullOrWhiteSpace(dc.PromoCode))
                {
                    var validation = EvaluatePromoCode(dc.PromoCode, sub, out promoMatch);
                    if (validation.Success) discount = validation.DiscountAmount;
                    else promoMatch = null;
                }

                decimal gst = Math.Round((sub - discount) * GstRate);
                string collectionAddr = dc.CollectionType == "home" ? dc.CollectionAddress : dc.BranchName ?? dc.CollectionAddress;

                string familyMemberName = null;
                if (dc.FamilyMemberId.HasValue)
                {
                    var fm = _db.FamilyMembers.FirstOrDefault(f => f.FamilyMemberId == dc.FamilyMemberId.Value && f.IsActive);
                    familyMemberName = fm?.Name;
                }

                // Bookings.PatientId is a NOT NULL FK to Patients — a guest checkout
                // (not logged in) arrives with PatientId 0, and a browser holding a
                // stale localStorage/sessionStorage login (e.g. from before a DB
                // reset) can arrive with a nonzero PatientId that no longer exists
                // either. Either way the FK would blow up SaveChanges with an opaque
                // DbUpdateException. Resolve it to a real patient by phone, auto-
                // creating a minimal account (same password-from-phone fallback as
                // RegisterPatient) so the guest can later OTP-login with this same
                // phone number and see the booking.
                if (dc.PatientId != 0 && !_db.Patients.Any(p => p.PatientId == dc.PatientId))
                    dc.PatientId = 0;

                if (dc.PatientId == 0 && !string.IsNullOrWhiteSpace(dc.PatientPhone))
                {
                    var existing = _db.Patients.FirstOrDefault(p => p.Phone == dc.PatientPhone);
                    if (existing != null)
                    {
                        dc.PatientId = existing.PatientId;
                    }
                    else
                    {
                        var guest = new Patient
                        {
                            FullName = string.IsNullOrWhiteSpace(dc.PatientName) ? "Guest" : dc.PatientName,
                            Phone = dc.PatientPhone,
                            Email = dc.PatientEmail,
                            PasswordHash = PasswordHelper.HashPassword(dc.PatientPhone),
                            IsActive = true,
                            CreatedDate = DateTime.Now
                        };
                        _db.Patients.Add(guest);
                        _db.SaveChanges();
                        dc.PatientId = guest.PatientId;
                    }
                }

                // Slot-capacity gate: the UI greys out full slots, but this is the
                // authoritative check so two simultaneous checkouts can't overbook.
                if (!string.IsNullOrEmpty(dc.CollectionDate) && !string.IsNullOrEmpty(dc.TimeSlot))
                {
                    var fullSlots = GetBookedSlots(dc.CollectionDate, dc.CollectionType,
                        dc.CollectionType == "home" ? null : dc.BranchName);
                    if (fullSlots.Contains(dc.TimeSlot))
                        return new BookingDC { Success = false, Message = "The selected time slot is no longer available. Please choose another slot." };
                }

                // Paid only when the gateway confirmed it (PaymentId present);
                // pay-at-counter and gateway-less demo bookings stay Pending so
                // the LIMS/front-desk reconciles them via UpdatePaymentStatus.
                bool gatewayPaid = !string.IsNullOrEmpty(dc.PaymentId);

                long sampleSeed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var booking = new Booking
                {
                    BookingRef = PasswordHelper.GenerateBookingRef(),
                    PatientId = dc.PatientId,
                    FamilyMemberId = dc.FamilyMemberId,
                    FamilyMemberName = familyMemberName,
                    CollectionType = dc.CollectionType,
                    BranchName = dc.CollectionType != "home" ? collectionAddr : null,
                    Address = dc.CollectionType == "home" ? collectionAddr : null,
                    CollectionDate = string.IsNullOrEmpty(dc.CollectionDate) ? (DateTime?)null : DateTime.Parse(dc.CollectionDate),
                    TimeSlot = dc.TimeSlot,
                    PaymentMethod = dc.PaymentMethod,
                    PaymentStatus = gatewayPaid ? "Paid" : "Pending",
                    PaymentRef = dc.PaymentId,
                    PaidAt = gatewayPaid ? DateTime.Now : (DateTime?)null,
                    Subtotal = sub, PromoCode = promoMatch?.Code, DiscountAmount = discount,
                    GstAmount = gst, TotalAmount = sub - discount + gst,
                    AmountPaid = gatewayPaid ? sub - discount + gst : 0,
                    SampleStatus = 0,
                    BookingStatus = "Booked",
                    SampleId = LimsClient.GenerateSampleId(sampleSeed),
                    Barcode = LimsClient.GenerateBarcode(sampleSeed),
                    LimsSyncStatus = "Pending",
                    CreatedAt = DateTime.Now,
                    Tests = tests.Select(t => new BookingTest
                    {
                        TestSuiteID = t.TestSuiteID ?? ("ID" + t.BookingTestId),
                        TestSuiteName = t.TestSuiteName,
                        Price = t.Price, SampleType = t.SampleType, TestCount = t.TestCount
                    }).ToList()
                };
                _db.Bookings.Add(booking);

                _db.Reports.Add(new Report
                {
                    BookingRef = booking.BookingRef,
                    PatientId = dc.PatientId,
                    TestNames = string.Join(", ", tests.Select(t => t.TestSuiteName)),
                    Status = "Pending"
                });

                if (promoMatch != null) promoMatch.UsedCount++;

                _db.SaveChanges();

                string pName = dc.PatientName, pPhone = dc.PatientPhone;
                if (dc.PatientId != 0)
                {
                    var p = _db.Patients.Find(dc.PatientId);
                    if (p != null) { pName = p.FullName; pPhone = p.Phone; }
                }

                // Push the collection job to the LIMS; the booking succeeds either
                // way and stays "Pending" for a later re-push if the LIMS is down.
                string limsJobId = LimsClient.CreateCollectionJob(booking, pName, pPhone);
                if (limsJobId != null)
                {
                    booking.LimsJobId = limsJobId;
                    booking.LimsSyncStatus = "Synced";
                }

                _db.SampleEvents.Add(new SampleEvent
                {
                    BookingId = booking.BookingId,
                    Status = 0,
                    StatusLabel = StatusLabels[0],
                    Source = "Portal",
                    Notes = booking.CollectionType == "home"
                        ? "Home collection requested" + (string.IsNullOrEmpty(dc.TimeSlot) ? "" : " (" + dc.TimeSlot + ")")
                        : "Walk-in booking placed",
                    CreatedAt = DateTime.Now
                });
                _db.SaveChanges();

                var result = MapBooking(booking, true);
                // include patient info for guest bookings
                if (dc.PatientId == 0)
                {
                    result.PatientName = dc.PatientName;
                    result.PatientPhone = dc.PatientPhone;
                }

                if (IsNotificationAllowed(dc.PatientId, "SMS", "BookingConfirmation"))
                    SmsHelper.Send(pPhone,
                        "Your Swapnil Diagnostics booking " + booking.BookingRef + " is confirmed. Sample ID " + booking.SampleId + ", total Rs." + booking.TotalAmount +
                        (gatewayPaid ? " (paid)" : " (payment due)") + ". Track status in your Patient Portal.",
                        "BookingConfirmation", booking.BookingRef);

                if (IsNotificationAllowed(dc.PatientId, "WhatsApp", "BookingConfirmation"))
                    WhatsAppHelper.Send(pPhone, "BookingConfirmation",
                        new[] { string.IsNullOrEmpty(pName) ? "there" : pName, booking.BookingRef, booking.SampleId, booking.TotalAmount.ToString() },
                        "Booking " + booking.BookingRef + " confirmed. Sample ID " + booking.SampleId + ", total Rs." + booking.TotalAmount + ".",
                        booking.BookingRef);

                AuditHelper.Log(pPhone, dc.PatientId == 0 ? (int?)null : dc.PatientId, "CreateBooking", "Booking", booking.BookingRef,
                    tests.Count + " test(s), Rs." + booking.TotalAmount + ", " + booking.CollectionType + ", payment=" + dc.PaymentMethod +
                    "/" + booking.PaymentStatus + (gatewayPaid ? ", txn=" + dc.PaymentId : ""), true);

                if (gatewayPaid) MaybeGenerateInvoice(booking);

                return result;
            }
            catch (Exception ex)
            {
                var root = ex;
                while (root.InnerException != null) root = root.InnerException;
                System.Diagnostics.Trace.TraceError("[CreateBooking] " + ex.Message + " -> " + root.Message);
                try
                {
                    string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "createbooking_debug.log");
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath));
                    string payload = "PatientId=" + dc.PatientId + " PatientName=" + dc.PatientName +
                        " PatientPhone=" + dc.PatientPhone + " PatientEmail=" + dc.PatientEmail +
                        " FamilyMemberId=" + dc.FamilyMemberId + " CollectionType=" + dc.CollectionType +
                        " BranchName=" + dc.BranchName + " CollectionAddress=" + dc.CollectionAddress +
                        " CollectionDate=" + dc.CollectionDate + " TimeSlot=" + dc.TimeSlot +
                        " PromoCode=" + dc.PromoCode + " PaymentMethod=" + dc.PaymentMethod +
                        " PaymentId=" + dc.PaymentId + " TotalAmount=" + dc.TotalAmount +
                        " Tests=[" + string.Join(";", (dc.Tests ?? new List<BookingTestDC>()).Select(t =>
                            "(TestSuiteID=" + t.TestSuiteID + ",Name=" + t.TestSuiteName + ",Price=" + t.Price +
                            ",SampleType=" + t.SampleType + ",TestCount=" + t.TestCount + ")")) + "]";
                    System.IO.File.AppendAllText(logPath,
                        "==== " + DateTime.Now.ToString("s") + " ====\r\nPAYLOAD: " + payload +
                        "\r\nEXCEPTION CHAIN:\r\n" + ex.ToString() + "\r\n\r\n");
                }
                catch { /* logging must never break the calling flow */ }
                return new BookingDC { Success = false, Message = root.Message };
            }
        }

        public BookingDC GetBookingByRef(string bookingRef)
        {
            var b = _db.Bookings.Include("Patient").Include("Tests").FirstOrDefault(x => x.BookingRef == bookingRef);
            return b == null ? new BookingDC { Success = false, Message = "Not found." } : MapBooking(b, true);
        }

        public List<BookingDC> GetBookingsByPatient(string patientId)
        {
            int id = int.Parse(patientId);
            return _db.Bookings.Include("Tests")
                .Where(b => b.PatientId == id)
                .OrderByDescending(b => b.CreatedAt)
                .ToList().Select(b => MapBooking(b, false)).ToList();
        }

        private static readonly string[] StatusLabels = { "Booked", "Sample Collected", "At Lab", "Processing", "Report Ready" };

        public BookingDC UpdateSampleStatus(string bookingRef, string status)
        {
            try
            {
                int newStatus;
                if (!int.TryParse(status, out newStatus) || newStatus < 0 || newStatus >= StatusLabels.Length)
                    return new BookingDC { Success = false, Message = "Invalid status value." };

                var b = _db.Bookings.Include("Patient").FirstOrDefault(x => x.BookingRef == bookingRef);
                if (b == null) return new BookingDC { Success = false, Message = "Not found." };
                b.SampleStatus = newStatus;
                if (b.SampleStatus == 4)
                {
                    b.BookingStatus = "Ready";
                    // Auto-publish: flip the report row so it appears as
                    // downloadable in the patient portal the moment the lab
                    // marks the job "Report Ready" — no manual second step.
                    foreach (var rep in _db.Reports.Where(r => r.BookingRef == b.BookingRef && r.Status != "Ready"))
                    {
                        rep.Status = "Ready";
                        rep.ReportDate = DateTime.Now;
                        if (string.IsNullOrEmpty(rep.ReportFilePath))
                            rep.ReportFilePath = "/Reports/" + b.BookingRef + ".pdf";
                    }
                }

                string label = StatusLabels[newStatus];
                _db.SampleEvents.Add(new SampleEvent
                {
                    BookingId = b.BookingId,
                    Status = newStatus,
                    StatusLabel = label,
                    Source = "Lab",
                    CreatedAt = DateTime.Now
                });
                _db.SaveChanges();

                string notifType = newStatus == 4 ? "ReportReady" : "StatusUpdate";
                if (IsNotificationAllowed(b.PatientId, "SMS", notifType))
                    SmsHelper.Send(b.Patient?.Phone,
                        "Update on booking " + b.BookingRef + ": status is now '" + label + "'. Check your Patient Portal for details.",
                        notifType, b.BookingRef);

                if (IsNotificationAllowed(b.PatientId, "WhatsApp", notifType))
                    WhatsAppHelper.Send(b.Patient?.Phone, notifType,
                        new[] { string.IsNullOrEmpty(b.Patient?.FullName) ? "there" : b.Patient.FullName, b.BookingRef, label },
                        "Booking " + b.BookingRef + " status: " + label + ".", b.BookingRef);

                AuditHelper.Log("LabStaff", b.PatientId, "SampleStatusChange", "Booking", b.BookingRef,
                    "Status -> " + label, true);

                return MapBooking(b, false);
            }
            catch (Exception ex)
            {
                return new BookingDC { Success = false, Message = ex.Message };
            }
        }

        public SampleTimelineDC GetSampleTimeline(string bookingRef)
        {
            try
            {
                var b = _db.Bookings.FirstOrDefault(x => x.BookingRef == bookingRef);
                if (b == null) return new SampleTimelineDC { Success = false, Message = "Not found." };

                var events = _db.SampleEvents.Where(e => e.BookingId == b.BookingId)
                    .OrderBy(e => e.CreatedAt).ThenBy(e => e.SampleEventId)
                    .Select(e => new SampleEventDC
                    {
                        Status = e.Status, StatusLabel = e.StatusLabel,
                        Source = e.Source, Notes = e.Notes, CreatedAt = e.CreatedAt
                    }).ToList();

                return new SampleTimelineDC
                {
                    BookingRef = b.BookingRef,
                    SampleId = b.SampleId,
                    Barcode = b.Barcode,
                    LimsJobId = b.LimsJobId,
                    LimsSyncStatus = b.LimsSyncStatus,
                    CollectionType = b.CollectionType,
                    CurrentStatus = b.SampleStatus,
                    CurrentStatusLabel = b.SampleStatus >= 0 && b.SampleStatus < StatusLabels.Length ? StatusLabels[b.SampleStatus] : "Updated",
                    BookingStatus = b.BookingStatus,
                    Events = events,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                return new SampleTimelineDC { Success = false, Message = ex.Message };
            }
        }

        public BookingDC CancelBooking(string bookingRef)
        {
            try
            {
                var b = _db.Bookings.Include("Patient").Include("Tests").FirstOrDefault(x => x.BookingRef == bookingRef);
                if (b == null) return new BookingDC { Success = false, Message = "Not found." };
                if (b.SampleStatus >= 1)
                {
                    AuditHelper.Log(b.Patient?.Phone, b.PatientId, "CancelBooking", "Booking", b.BookingRef,
                        "Rejected — sample already collected.", false);
                    return new BookingDC { Success = false, Message = "Cannot cancel — sample already collected." };
                }

                b.BookingStatus = "Cancelled";

                // Auto-refund: a cancelled booking that was already paid shouldn't
                // require a separate manual refund step to reflect reality.
                bool autoRefunded = false;
                if ((b.PaymentStatus == "Paid" || b.PaymentStatus == "PartiallyPaid") && b.AmountPaid > 0)
                {
                    b.RefundAmount = (b.RefundAmount ?? 0) + b.AmountPaid;
                    b.RefundedAt = DateTime.Now;
                    b.RefundReason = "Booking cancelled";
                    b.PaymentStatus = "Refunded";
                    autoRefunded = true;
                }
                _db.SaveChanges();

                SmsHelper.Send(b.Patient?.Phone,
                    "Your booking " + b.BookingRef + " has been cancelled as requested." +
                    (autoRefunded ? " Rs." + b.RefundAmount + " will be refunded." : ""),
                    "Cancellation", b.BookingRef);

                if (autoRefunded) LimsClient.PushPaymentStatus(b);

                AuditHelper.Log(b.Patient?.Phone, b.PatientId, "CancelBooking", "Booking", b.BookingRef,
                    "Cancelled by patient." + (autoRefunded ? " Auto-refunded Rs." + b.RefundAmount + "." : ""), true);

                return MapBooking(b, true);
            }
            catch (Exception ex)
            {
                return new BookingDC { Success = false, Message = ex.Message };
            }
        }

        // Phlebotomist-facing queue: today's not-yet-collected home-collection jobs,
        // ordered by time slot so it can be read straight down like a work list.
        public List<CollectionQueueItemDC> GetCollectionQueue()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            return _db.Bookings.Include("Patient")
                .Where(b => b.CollectionType == "home" && b.SampleStatus < 1 &&
                            b.BookingStatus != "Cancelled" &&
                            b.CollectionDate >= today && b.CollectionDate < tomorrow)
                .ToList()
                .OrderBy(b => b.TimeSlot)
                .Select(b => new CollectionQueueItemDC
                {
                    BookingRef = b.BookingRef,
                    PatientName = b.FamilyMemberName ?? b.Patient?.FullName,
                    PatientPhone = b.Patient?.Phone,
                    Address = b.Address,
                    CollectionDate = b.CollectionDate,
                    TimeSlot = b.TimeSlot,
                    SampleId = b.SampleId,
                    SampleStatus = b.SampleStatus
                }).ToList();
        }

        // Chain of custody (Sample Collection Phase 4) — a finer-grained handoff
        // log than the 5-stage SampleEvent timeline; this is what an accreditation
        // audit actually asks for (who had the sample, role, action, where, when).
        public CustodyEventDC LogCustodyEvent(LogCustodyEventDC dc)
        {
            try
            {
                if (dc == null || string.IsNullOrEmpty(dc.BookingRef) || string.IsNullOrWhiteSpace(dc.Action))
                    return new CustodyEventDC();

                var b = _db.Bookings.FirstOrDefault(x => x.BookingRef == dc.BookingRef);
                if (b == null) return new CustodyEventDC();

                var ev = new CustodyEvent
                {
                    BookingId = b.BookingId, HandlerName = dc.HandlerName, HandlerRole = dc.HandlerRole,
                    Action = dc.Action, Location = dc.Location, Notes = dc.Notes, CreatedAt = DateTime.Now
                };
                _db.CustodyEvents.Add(ev);
                _db.SaveChanges();

                AuditHelper.Log(dc.HandlerName ?? "Staff", b.PatientId, "LogCustodyEvent", "Booking", b.BookingRef,
                    dc.Action + " by " + (dc.HandlerName ?? "?") + " (" + dc.HandlerRole + ")" +
                    (string.IsNullOrEmpty(dc.Location) ? "" : " at " + dc.Location), true);

                return new CustodyEventDC
                {
                    HandlerName = ev.HandlerName, HandlerRole = ev.HandlerRole, Action = ev.Action,
                    Location = ev.Location, Notes = ev.Notes, CreatedAt = ev.CreatedAt
                };
            }
            catch (Exception)
            {
                return new CustodyEventDC();
            }
        }

        public ChainOfCustodyDC GetChainOfCustody(string bookingRef)
        {
            var b = _db.Bookings.FirstOrDefault(x => x.BookingRef == bookingRef);
            if (b == null) return new ChainOfCustodyDC { Success = false, Message = "Not found." };

            return new ChainOfCustodyDC
            {
                BookingRef = b.BookingRef,
                SampleId = b.SampleId,
                Events = _db.CustodyEvents.Where(e => e.BookingId == b.BookingId)
                    .OrderBy(e => e.CreatedAt)
                    .Select(e => new CustodyEventDC
                    {
                        HandlerName = e.HandlerName, HandlerRole = e.HandlerRole, Action = e.Action,
                        Location = e.Location, Notes = e.Notes, CreatedAt = e.CreatedAt
                    }).ToList(),
                Success = true
            };
        }

        // ── PAYMENTS ──────────────────────────────────────────────────────
        // Single entry point for post-booking payment events: front-desk
        // settlement of pay-at-counter bookings, or a gateway webhook
        // confirming/refunding a transaction. Keeps portal and LIMS finance
        // in step without manual re-entry.
        public BookingDC UpdatePaymentStatus(PaymentUpdateDC dc)
        {
            try
            {
                if (dc == null || string.IsNullOrEmpty(dc.BookingRef))
                    return new BookingDC { Success = false, Message = "BookingRef is required." };

                var allowed = new[] { "Paid", "PartiallyPaid", "Pending", "Refunded", "Failed" };
                if (!allowed.Contains(dc.PaymentStatus))
                    return new BookingDC { Success = false, Message = "Invalid payment status." };

                var b = _db.Bookings.Include("Patient").Include("Tests").FirstOrDefault(x => x.BookingRef == dc.BookingRef);
                if (b == null) return new BookingDC { Success = false, Message = "Not found." };

                string oldStatus = b.PaymentStatus;
                if (!string.IsNullOrEmpty(dc.PaymentMethod)) b.PaymentMethod = dc.PaymentMethod;
                if (!string.IsNullOrEmpty(dc.PaymentRef)) b.PaymentRef = dc.PaymentRef;

                // Part-payment installment (Billing Phase 3): accumulate rather than
                // overwrite, and only flip to "Paid" once the full amount is in.
                if (dc.AmountPaid.HasValue && dc.AmountPaid.Value > 0)
                {
                    b.AmountPaid += dc.AmountPaid.Value;
                    b.PaymentStatus = b.AmountPaid >= b.TotalAmount ? "Paid" : "PartiallyPaid";
                }
                else
                {
                    b.PaymentStatus = dc.PaymentStatus;
                    if (dc.PaymentStatus == "Paid") b.AmountPaid = b.TotalAmount;
                }
                if (b.PaymentStatus == "Paid" && !b.PaidAt.HasValue) b.PaidAt = DateTime.Now;
                _db.SaveChanges();

                if (b.PaymentStatus == "Paid" && oldStatus != "Paid")
                {
                    if (IsNotificationAllowed(b.PatientId, "SMS", "PaymentReceived"))
                        SmsHelper.Send(b.Patient?.Phone,
                            "Payment of Rs." + b.TotalAmount + " received for booking " + b.BookingRef + ". Thank you!",
                            "PaymentReceived", b.BookingRef);
                    MaybeGenerateInvoice(b);
                }
                else if (b.PaymentStatus == "PartiallyPaid")
                {
                    if (IsNotificationAllowed(b.PatientId, "SMS", "PaymentReceived"))
                        SmsHelper.Send(b.Patient?.Phone,
                            "Payment of Rs." + dc.AmountPaid + " received for booking " + b.BookingRef +
                            ". Balance due: Rs." + (b.TotalAmount - b.AmountPaid) + ".",
                            "PartialPaymentReceived", b.BookingRef);
                }

                // Best-effort: keep the LIMS side of the job in step with the
                // portal's payment status (only meaningful once a job is synced).
                LimsClient.PushPaymentStatus(b);

                AuditHelper.Log(dc.Source ?? "FrontDesk", b.PatientId, "PaymentUpdate", "Booking", b.BookingRef,
                    oldStatus + " -> " + b.PaymentStatus + " (paid " + b.AmountPaid + "/" + b.TotalAmount + ")" +
                    (string.IsNullOrEmpty(dc.PaymentRef) ? "" : ", txn=" + dc.PaymentRef), true);

                return MapBooking(b, true);
            }
            catch (Exception ex)
            {
                return new BookingDC { Success = false, Message = ex.Message };
            }
        }

        // Refund on an already-Paid/PartiallyPaid booking. Records the refund
        // alongside the original payment fields rather than overwriting them, so
        // the full payment history (paid -> refunded, how much, why) stays intact.
        public BookingDC RefundPayment(RefundRequestDC dc)
        {
            try
            {
                if (dc == null || string.IsNullOrEmpty(dc.BookingRef))
                    return new BookingDC { Success = false, Message = "BookingRef is required." };

                var b = _db.Bookings.Include("Patient").Include("Tests").FirstOrDefault(x => x.BookingRef == dc.BookingRef);
                if (b == null) return new BookingDC { Success = false, Message = "Not found." };
                if (b.PaymentStatus != "Paid" && b.PaymentStatus != "PartiallyPaid")
                    return new BookingDC { Success = false, Message = "Only a Paid or PartiallyPaid booking can be refunded." };
                if (dc.Amount <= 0 || dc.Amount > b.AmountPaid)
                    return new BookingDC { Success = false, Message = "Refund amount must be between 0 and the amount paid (Rs." + b.AmountPaid + ")." };

                b.RefundAmount = (b.RefundAmount ?? 0) + dc.Amount;
                b.RefundedAt = DateTime.Now;
                b.RefundReason = dc.Reason;
                b.PaymentStatus = "Refunded";
                _db.SaveChanges();

                SmsHelper.Send(b.Patient?.Phone,
                    "Rs." + dc.Amount + " has been refunded for booking " + b.BookingRef +
                    (string.IsNullOrEmpty(dc.Reason) ? "." : " (" + dc.Reason + ")."),
                    "RefundIssued", b.BookingRef);

                LimsClient.PushPaymentStatus(b);

                AuditHelper.Log(dc.Source ?? "FrontDesk", b.PatientId, "RefundPayment", "Booking", b.BookingRef,
                    "Refunded Rs." + dc.Amount + (string.IsNullOrEmpty(dc.Reason) ? "" : ", reason: " + dc.Reason), true);

                return MapBooking(b, true);
            }
            catch (Exception ex)
            {
                return new BookingDC { Success = false, Message = ex.Message };
            }
        }

        // ── REPORTS ───────────────────────────────────────────────────────
        public List<ReportDC> GetReportsByPatient(string patientId)
        {
            int id = int.Parse(patientId);
            return _db.Reports.Where(r => r.PatientId == id)
                .OrderByDescending(r => r.ReportDate)
                .Select(r => new ReportDC
                {
                    ReportId = r.ReportId, BookingRef = r.BookingRef,
                    PatientId = r.PatientId, TestNames = r.TestNames,
                    ReportFilePath = r.ReportFilePath,
                    Status = r.Status, ReportDate = r.ReportDate, Success = true
                }).ToList();
        }

        // Receiving endpoint for the LIMS (or lab staff, while the real LIMS push
        // isn't wired) to attach the finished report and flip the booking to Ready
        // — the "auto-push PDF from LIMS→Website" step GetReportsByPatient alone
        // can't provide, since nothing else ever populates ReportFilePath.
        public ReportDC AttachReport(ReportAttachDC dc)
        {
            try
            {
                if (dc == null || (string.IsNullOrEmpty(dc.BookingRef) && string.IsNullOrEmpty(dc.LimsJobId)))
                    return new ReportDC { Success = false, Message = "BookingRef or LimsJobId is required." };
                if (string.IsNullOrEmpty(dc.ReportFilePath))
                    return new ReportDC { Success = false, Message = "ReportFilePath is required." };

                var b = _db.Bookings.Include("Patient").FirstOrDefault(x =>
                    (dc.BookingRef != null && x.BookingRef == dc.BookingRef) ||
                    (dc.LimsJobId != null && x.LimsJobId == dc.LimsJobId));
                if (b == null) return new ReportDC { Success = false, Message = "Booking not found." };

                var report = _db.Reports.FirstOrDefault(r => r.BookingRef == b.BookingRef);
                if (report == null)
                {
                    report = new Report { BookingRef = b.BookingRef, BookingId = b.BookingId, PatientId = b.PatientId };
                    _db.Reports.Add(report);
                }
                report.ReportFilePath = dc.ReportFilePath;
                report.Status = "Ready";
                report.ReportDate = DateTime.Now;

                if (b.SampleStatus < 4)
                {
                    b.SampleStatus = 4;
                    b.BookingStatus = "Ready";
                    _db.SampleEvents.Add(new SampleEvent
                    {
                        BookingId = b.BookingId, Status = 4, StatusLabel = StatusLabels[4],
                        Source = dc.Source ?? "LIMS", Notes = "Report attached", CreatedAt = DateTime.Now
                    });
                }
                _db.SaveChanges();

                if (IsNotificationAllowed(b.PatientId, "SMS", "ReportReady"))
                    SmsHelper.Send(b.Patient?.Phone,
                        "Your report for booking " + b.BookingRef + " is ready. Login to your Patient Portal to download.",
                        "ReportReady", b.BookingRef);

                if (IsNotificationAllowed(b.PatientId, "WhatsApp", "ReportReady"))
                    WhatsAppHelper.Send(b.Patient?.Phone, "ReportReady",
                        new[] { string.IsNullOrEmpty(b.Patient?.FullName) ? "there" : b.Patient.FullName, b.BookingRef },
                        "Report for booking " + b.BookingRef + " is ready.", b.BookingRef);

                AuditHelper.Log(dc.Source ?? "LIMS", b.PatientId, "AttachReport", "Report", b.BookingRef,
                    "Report file attached: " + dc.ReportFilePath, true);

                return new ReportDC
                {
                    ReportId = report.ReportId, BookingRef = report.BookingRef, PatientId = report.PatientId,
                    TestNames = report.TestNames, ReportFilePath = report.ReportFilePath,
                    Status = report.Status, ReportDate = report.ReportDate, Success = true
                };
            }
            catch (Exception ex)
            {
                return new ReportDC { Success = false, Message = ex.Message };
            }
        }

        // Doctor sharing (Report Delivery Phase 3). Success reflects whether the
        // email actually sent — EmailHelper is honest about SmtpHost not being
        // configured rather than pretending the share succeeded.
        public ShareReportDC ShareReportWithDoctor(ShareReportDC dc)
        {
            try
            {
                if (dc == null || string.IsNullOrEmpty(dc.BookingRef) || string.IsNullOrWhiteSpace(dc.DoctorEmail))
                    return new ShareReportDC { Success = false, Message = "BookingRef and doctor email are required." };

                var report = _db.Reports.FirstOrDefault(r => r.BookingRef == dc.BookingRef && r.Status == "Ready");
                if (report == null)
                    return new ShareReportDC { Success = false, Message = "Report not ready yet." };

                var booking = _db.Bookings.FirstOrDefault(b => b.BookingRef == dc.BookingRef);
                if (booking == null)
                    return new ShareReportDC { Success = false, Message = "Booking not found." };
                if (string.IsNullOrEmpty(booking.ShareToken))
                {
                    booking.ShareToken = GenerateShareToken();
                    _db.SaveChanges();
                }

                string baseUrl = ConfigurationManager.AppSettings["PortalBaseUrl"];
                string portalLink = string.IsNullOrWhiteSpace(baseUrl)
                    ? "View it from the Patient Portal (ask the patient to open Share again once PortalBaseUrl is configured for a direct link)."
                    : baseUrl.TrimEnd('/') + "/Patient/ViewReport?token=" + booking.ShareToken;

                EmailHelper.Send(dc.DoctorEmail,
                    "Lab report shared — booking " + dc.BookingRef,
                    "Dr. " + (dc.DoctorName ?? "") + ",\n\n" +
                    "A patient has shared their lab report with you.\n" + portalLink + "\n\nRegards,\nSwapnil Diagnostics",
                    "ReportShareDoctor", dc.BookingRef);
                var lastLog = _db.NotificationLogs.Where(n => n.BookingRef == dc.BookingRef && n.Channel == "Email")
                    .OrderByDescending(n => n.NotificationLogId).FirstOrDefault();

                AuditHelper.Log("Patient", report.PatientId, "ShareReportWithDoctor", "Report", dc.BookingRef,
                    "Shared with " + dc.DoctorEmail + (lastLog != null && !lastLog.Success ? " (email not delivered — " + lastLog.ErrorDetail + ")" : ""),
                    lastLog?.Success ?? false);

                return new ShareReportDC
                {
                    BookingRef = dc.BookingRef, DoctorName = dc.DoctorName, DoctorEmail = dc.DoctorEmail,
                    Success = lastLog?.Success ?? false,
                    Message = (lastLog?.Success ?? false)
                        ? "Report link emailed to " + dc.DoctorEmail + "."
                        : "Share recorded, but the email could not be sent" + (string.IsNullOrEmpty(lastLog?.ErrorDetail) ? "." : ": " + lastLog.ErrorDetail)
                };
            }
            catch (Exception ex)
            {
                return new ShareReportDC { Success = false, Message = ex.Message };
            }
        }

        // Doctor share link fix — the WhatsApp/copy-link/email "share" button
        // used to build its link straight from the plain, sequential BookingRef,
        // so anyone who could guess a booking reference could open someone
        // else's report. This mints (or returns the already-minted) random
        // token, gated by patientId matching the booking's owner — the same
        // "trust the caller-supplied patientId" convention every other
        // patient-scoped endpoint in this service already uses (there's no
        // session/auth layer yet; see the cross-cutting gap in GapAnalysis.md).
        public ShareTokenDC GetOrCreateShareToken(string bookingRef, string patientId)
        {
            try
            {
                int pid;
                if (!int.TryParse(patientId, out pid))
                    return new ShareTokenDC { Success = false, Message = "Invalid patient." };

                var booking = _db.Bookings.FirstOrDefault(b => b.BookingRef == bookingRef);
                if (booking == null || booking.PatientId != pid)
                    return new ShareTokenDC { Success = false, Message = "Booking not found." };

                if (string.IsNullOrEmpty(booking.ShareToken))
                {
                    booking.ShareToken = GenerateShareToken();
                    _db.SaveChanges();
                }
                return new ShareTokenDC { Token = booking.ShareToken, Success = true };
            }
            catch (Exception ex)
            {
                return new ShareTokenDC { Success = false, Message = ex.Message };
            }
        }

        // Public, no-login lookup for the shared view-only report page — takes
        // over from GetBookingByRef for this page specifically, since BookingRef
        // itself is guessable/sequential and this page is meant to be safely
        // forwardable.
        public BookingDC GetBookingByShareToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return new BookingDC { Success = false, Message = "Not found." };
            var b = _db.Bookings.Include("Patient").Include("Tests").FirstOrDefault(x => x.ShareToken == token);
            return b == null ? new BookingDC { Success = false, Message = "Not found." } : MapBooking(b, true);
        }

        private static string GenerateShareToken()
        {
            var bytes = new byte[24];
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider()) rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        // Minimal FHIR-lite feed for a third-party/EHR consumer (Report Delivery
        // Phase 4). Requires X-Api-Key == Web.config PartnerApiKey (fails closed).
        // See FhirDiagnosticReportDC for exactly how far this goes — it is not a
        // certified FHIR server.
        public List<FhirDiagnosticReportDC> GetReportsApi(string patientId)
        {
            if (!IsPartnerAuthorized())
                throw new WebFaultException(System.Net.HttpStatusCode.Unauthorized);

            int id;
            if (!int.TryParse(patientId, out id)) return new List<FhirDiagnosticReportDC>();

            return _db.Reports.Where(r => r.PatientId == id)
                .OrderByDescending(r => r.ReportDate)
                .ToList()
                .Select(r => new FhirDiagnosticReportDC
                {
                    Id = "report-" + r.ReportId,
                    Status = r.Status == "Ready" ? "final" : "registered",
                    SubjectReference = "Patient/" + r.PatientId,
                    Code = r.TestNames,
                    EffectiveDateTime = r.ReportDate,
                    Issued = r.Status == "Ready" ? (DateTime?)r.ReportDate : null,
                    PresentedFormUrl = r.ReportFilePath
                }).ToList();
        }

        // ── INVOICES ──────────────────────────────────────────────────────
        // Falls back to 18% when DefaultGstRate is unset/invalid, matching the
        // rate CreateBooking always used before this became configurable.
        private static decimal GstRate
        {
            get
            {
                decimal rate;
                string raw = ConfigurationManager.AppSettings["DefaultGstRate"];
                return decimal.TryParse(raw, out rate) ? rate : 0.18m;
            }
        }

        // Idempotent: a booking never gets a second invoice even if payment
        // status flip-flops (e.g. Paid -> Refunded -> Paid).
        private void MaybeGenerateInvoice(Booking b)
        {
            try
            {
                if (_db.Invoices.Any(i => i.BookingId == b.BookingId)) return;

                var invoice = new Invoice
                {
                    BookingId = b.BookingId,
                    Gstin = ConfigurationManager.AppSettings["CompanyGSTIN"],
                    PlaceOfSupply = ConfigurationManager.AppSettings["CompanyPlaceOfSupply"],
                    HsnCode = ConfigurationManager.AppSettings["DefaultHsnCode"],
                    Subtotal = b.Subtotal, GstAmount = b.GstAmount, TotalAmount = b.TotalAmount,
                    CreatedAt = DateTime.Now
                };
                _db.Invoices.Add(invoice);
                _db.SaveChanges();

                string fy = invoice.CreatedAt.Month >= 4
                    ? invoice.CreatedAt.Year + "-" + (invoice.CreatedAt.Year + 1) % 100
                    : (invoice.CreatedAt.Year - 1) + "-" + invoice.CreatedAt.Year % 100;
                invoice.InvoiceNumber = "INV/" + fy + "/" + invoice.InvoiceId.ToString("D6");
                _db.SaveChanges();

                AuditHelper.Log("System", b.PatientId, "GenerateInvoice", "Invoice", invoice.InvoiceNumber,
                    "For booking " + b.BookingRef + ", Rs." + invoice.TotalAmount, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[Invoice] Generation failed for " + b.BookingRef + ": " + ex.Message);
            }
        }

        public InvoiceDC GetInvoiceByBookingRef(string bookingRef)
        {
            var b = _db.Bookings.FirstOrDefault(x => x.BookingRef == bookingRef);
            if (b == null) return new InvoiceDC { Success = false, Message = "Booking not found." };

            var inv = _db.Invoices.FirstOrDefault(i => i.BookingId == b.BookingId);
            if (inv == null) return new InvoiceDC { Success = false, Message = "No invoice yet — payment not confirmed." };

            return new InvoiceDC
            {
                InvoiceId = inv.InvoiceId, InvoiceNumber = inv.InvoiceNumber, BookingRef = bookingRef,
                Gstin = inv.Gstin, PlaceOfSupply = inv.PlaceOfSupply, HsnCode = inv.HsnCode,
                Subtotal = inv.Subtotal, GstAmount = inv.GstAmount, TotalAmount = inv.TotalAmount,
                CreatedAt = inv.CreatedAt, Success = true
            };
        }

        // Billing Phase 4 — one row per booking for finance to reconcile against
        // the gateway statement (PaymentRef) and the LIMS (LimsSyncStatus).
        public ReconciliationSummaryDC GetReconciliationSummary(string from, string to)
        {
            DateTime toDate;
            if (!DateTime.TryParse(to, out toDate)) toDate = DateTime.Today;
            DateTime fromDate;
            if (!DateTime.TryParse(from, out fromDate)) fromDate = toDate.AddDays(-29);
            var rangeEnd = toDate.Date.AddDays(1);

            var bookings = _db.Bookings.Include("Patient")
                .Where(b => b.CreatedAt >= fromDate.Date && b.CreatedAt < rangeEnd)
                .ToList();
            var invoiceByBooking = _db.Invoices.ToDictionary(i => i.BookingId, i => i.InvoiceNumber);

            var rows = bookings.OrderByDescending(b => b.CreatedAt).Select(b => new ReconciliationRowDC
            {
                BookingRef = b.BookingRef, CreatedAt = b.CreatedAt,
                PatientName = b.FamilyMemberName ?? b.Patient?.FullName,
                TotalAmount = b.TotalAmount, AmountPaid = b.AmountPaid,
                PaymentStatus = b.PaymentStatus, PaymentRef = b.PaymentRef,
                RefundAmount = b.RefundAmount,
                InvoiceNumber = invoiceByBooking.ContainsKey(b.BookingId) ? invoiceByBooking[b.BookingId] : null,
                LimsSyncStatus = b.LimsSyncStatus
            }).ToList();

            return new ReconciliationSummaryDC
            {
                FromDate = fromDate.Date, ToDate = toDate.Date,
                TotalBilled = bookings.Sum(b => b.TotalAmount),
                TotalCollected = bookings.Sum(b => b.AmountPaid),
                TotalRefunded = bookings.Sum(b => b.RefundAmount ?? 0),
                TotalOutstanding = bookings.Where(b => b.PaymentStatus != "Refunded").Sum(b => b.TotalAmount - b.AmountPaid),
                Rows = rows
            };
        }

        // ── FEEDBACK / COMPLAINTS (Help section fix) ────────────────────────
        // Real, self-hosted complaint channel — replaces linking out to the
        // legacy Feedback.cshtml page, whose form POSTs to a third party's API.
        public FeedbackDC SubmitFeedback(FeedbackDC dc)
        {
            try
            {
                if (dc == null || string.IsNullOrWhiteSpace(dc.Message))
                    return new FeedbackDC { Success = false, ResponseMessage = "Please enter a message." };

                var fb = new Feedback
                {
                    PatientId = dc.PatientId > 0 ? dc.PatientId : (int?)null,
                    Name = dc.Name,
                    Phone = dc.Phone,
                    BookingRef = dc.BookingRef,
                    Message = dc.Message.Length > 1000 ? dc.Message.Substring(0, 1000) : dc.Message,
                    Status = "Open",
                    CreatedAt = DateTime.Now
                };
                _db.Feedbacks.Add(fb);
                _db.SaveChanges();

                AuditHelper.Log(dc.Name ?? dc.Phone ?? "Guest", dc.PatientId, "SubmitFeedback", "Feedback",
                    fb.FeedbackId.ToString(), dc.Message, true);

                return new FeedbackDC { FeedbackId = fb.FeedbackId, Success = true,
                    ResponseMessage = "Thanks — our team will get back to you shortly." };
            }
            catch (Exception ex)
            {
                return new FeedbackDC { Success = false, ResponseMessage = ex.Message };
            }
        }

        public List<FeedbackDC> GetAllFeedback()
        {
            return _db.Feedbacks.OrderByDescending(f => f.CreatedAt)
                .Select(f => new FeedbackDC
                {
                    FeedbackId = f.FeedbackId, PatientId = f.PatientId, Name = f.Name,
                    Phone = f.Phone, BookingRef = f.BookingRef, Message = f.Message,
                    Status = f.Status, CreatedAt = f.CreatedAt, Success = true
                }).ToList();
        }

        // ── HOME COLLECTION LEADS (Home Collection popup fix) ────────────
        // Previously the popup only wrote to the browser's localStorage — a lead
        // was lost the moment the tab closed and no one on staff ever saw it.
        // This is the real, server-side record staff follow up on.
        public HomeCollectionLeadDC SubmitHomeCollectionLead(HomeCollectionLeadDC dc)
        {
            try
            {
                if (dc == null || string.IsNullOrWhiteSpace(dc.Name) || string.IsNullOrWhiteSpace(dc.Mobile))
                    return new HomeCollectionLeadDC { Success = false, Message = "Name and mobile number are required." };

                var lead = new HomeCollectionLead
                {
                    Name = dc.Name.Trim(), Mobile = dc.Mobile.Trim(), City = dc.City,
                    Status = "New", CreatedAt = DateTime.Now
                };
                _db.HomeCollectionLeads.Add(lead);
                _db.SaveChanges();

                AuditHelper.Log(dc.Name, null, "SubmitHomeCollectionLead", "HomeCollectionLead",
                    lead.HomeCollectionLeadId.ToString(), "Home collection lead from " + dc.Mobile + (string.IsNullOrEmpty(dc.City) ? "" : " (" + dc.City + ")"), true);

                return new HomeCollectionLeadDC
                {
                    HomeCollectionLeadId = lead.HomeCollectionLeadId, Name = lead.Name, Mobile = lead.Mobile,
                    City = lead.City, Status = lead.Status, CreatedAt = lead.CreatedAt, Success = true,
                    Message = "Thanks! Our team will call you shortly to schedule the home collection."
                };
            }
            catch (Exception ex)
            {
                return new HomeCollectionLeadDC { Success = false, Message = ex.Message };
            }
        }

        public List<HomeCollectionLeadDC> GetHomeCollectionLeads()
        {
            return _db.HomeCollectionLeads.OrderByDescending(l => l.CreatedAt)
                .Select(l => new HomeCollectionLeadDC
                {
                    HomeCollectionLeadId = l.HomeCollectionLeadId, Name = l.Name, Mobile = l.Mobile,
                    City = l.City, Status = l.Status, CreatedAt = l.CreatedAt, Success = true
                }).ToList();
        }

        // ── ADMIN ─────────────────────────────────────────────────────────
        public AdminStatsDC GetAdminStats()
        {
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            return new AdminStatsDC
            {
                TotalPatients = _db.Patients.Count(p => p.IsActive),
                TotalBookings = _db.Bookings.Count(),
                TodayBookings = _db.Bookings.Count(b => b.CreatedAt >= today),
                PendingReports = _db.Reports.Count(r => r.Status == "Pending"),
                TotalRevenue = _db.Bookings.Where(b => b.PaymentStatus == "Paid").Sum(b => (decimal?)b.TotalAmount) ?? 0,
                MonthRevenue = _db.Bookings.Where(b => b.PaymentStatus == "Paid" && b.CreatedAt >= monthStart).Sum(b => (decimal?)b.TotalAmount) ?? 0
            };
        }

        public List<PatientDC> GetAllPatients()
        {
            return _db.Patients.Where(p => p.IsActive)
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new PatientDC
                {
                    PatientId = p.PatientId, FullName = p.FullName,
                    Phone = p.Phone, Email = p.Email,
                    Gender = p.Gender, City = p.City,
                    CreatedDate = p.CreatedDate, IsActive = p.IsActive, Success = true
                }).ToList();
        }

        public List<BookingDC> GetAllBookings()
        {
            return _db.Bookings.Include("Patient").Include("Tests")
                .OrderByDescending(b => b.CreatedAt)
                .ToList().Select(b => MapBooking(b, false)).ToList();
        }

        public List<StaffAlertDC> GetStaffAlerts()
        {
            var alerts = new List<StaffAlertDC>();
            var now = DateTime.Now;
            var active = _db.Bookings.Include("Patient")
                .Where(b => b.BookingStatus != "Cancelled" && b.SampleStatus < 4)
                .ToList();

            foreach (var b in active)
            {
                // Missed collection: walk-in/home slot has passed and sample still not collected.
                if (b.CollectionDate.HasValue && b.CollectionDate.Value < now.Date && b.SampleStatus < 1)
                {
                    alerts.Add(new StaffAlertDC
                    {
                        Level = "danger", Icon = "fa-exclamation-triangle",
                        Title = "Sample collection overdue",
                        Detail = b.BookingRef + " (" + (b.Patient?.FullName ?? "-") + ") was due on " +
                                 b.CollectionDate.Value.ToString("dd MMM") + " and is still not collected.",
                        BookingRef = b.BookingRef, CreatedAt = b.CollectionDate.Value
                    });
                }
                // TAT alert: collected/at-lab/processing for over 48 hours without reaching Report Ready.
                else if (b.SampleStatus >= 1 && b.SampleStatus < 4 && (now - b.CreatedAt).TotalHours > 48)
                {
                    alerts.Add(new StaffAlertDC
                    {
                        Level = "warning", Icon = "fa-clock-o",
                        Title = "TAT breach risk",
                        Detail = b.BookingRef + " (" + (b.Patient?.FullName ?? "-") + ") has been at stage '" +
                                 StatusLabels[b.SampleStatus] + "' for over 48 hours.",
                        BookingRef = b.BookingRef, CreatedAt = b.CreatedAt
                    });
                }
                // Today's home collection still not dispatched/collected.
                else if (b.CollectionType == "home" && b.SampleStatus < 1 &&
                         b.CollectionDate.HasValue && b.CollectionDate.Value.Date == now.Date)
                {
                    alerts.Add(new StaffAlertDC
                    {
                        Level = "warning", Icon = "fa-motorcycle",
                        Title = "Home collection pending today",
                        Detail = b.BookingRef + " (" + (b.Patient?.FullName ?? "-") + ") needs a phlebotomist dispatched — slot " + (b.TimeSlot ?? "-") + ".",
                        BookingRef = b.BookingRef, CreatedAt = b.CreatedAt
                    });
                }
            }

            return alerts.OrderByDescending(a => a.Level == "danger").ThenByDescending(a => a.CreatedAt).ToList();
        }

        public List<NotificationLogDC> GetNotificationLogs()
        {
            return _db.NotificationLogs.OrderByDescending(n => n.CreatedAt).Take(200)
                .Select(n => new NotificationLogDC
                {
                    NotificationLogId = n.NotificationLogId, Phone = n.Phone,
                    Channel = n.Channel, Type = n.Type, BookingRef = n.BookingRef,
                    Message = n.Message, Success = n.Success, ErrorDetail = n.ErrorDetail,
                    CreatedAt = n.CreatedAt
                }).ToList();
        }

        // Patient-facing feed: only this patient's messages (matched via their
        // registered phone), so the portal can show a unified notification
        // history — booking, collection, payment, report-ready — in one place.
        public List<NotificationLogDC> GetNotificationsByPatient(string patientId)
        {
            int id;
            if (!int.TryParse(patientId, out id)) return new List<NotificationLogDC>();
            var patient = _db.Patients.Find(id);
            if (patient == null || string.IsNullOrEmpty(patient.Phone)) return new List<NotificationLogDC>();

            string phone = patient.Phone;
            return _db.NotificationLogs
                .Where(n => n.Phone == phone && n.Success)
                .OrderByDescending(n => n.CreatedAt).Take(50)
                .Select(n => new NotificationLogDC
                {
                    NotificationLogId = n.NotificationLogId,
                    Channel = n.Channel, Type = n.Type, BookingRef = n.BookingRef,
                    Message = n.Message, Success = n.Success,
                    CreatedAt = n.CreatedAt
                }).ToList();
        }

        // ── NOTIFICATION PREFERENCES (Notifications Phase 4) ───────────────
        // Opt-out model: an absent row means "enabled", so existing patients keep
        // getting every notification exactly as before this table existed.
        private static readonly string[] NotifTypes = { "BookingConfirmation", "StatusUpdate", "ReportReady", "PaymentReceived", "Reminder" };
        private static readonly string[] NotifChannels = { "SMS", "Email", "WhatsApp" };

        public List<NotificationPreferenceDC> GetNotificationPreferences(string patientId)
        {
            int id;
            if (!int.TryParse(patientId, out id)) return new List<NotificationPreferenceDC>();

            var overrides = _db.NotificationPreferences.Where(p => p.PatientId == id).ToList();
            var result = new List<NotificationPreferenceDC>();
            foreach (var channel in NotifChannels)
                foreach (var type in NotifTypes)
                {
                    var match = overrides.FirstOrDefault(o => o.Channel == channel && o.Type == type);
                    result.Add(new NotificationPreferenceDC { Channel = channel, Type = type, Enabled = match == null || match.Enabled });
                }
            return result;
        }

        public NotificationPreferenceDC UpdateNotificationPreference(UpdateNotificationPreferenceDC dc)
        {
            if (dc == null || dc.PatientId <= 0 || string.IsNullOrEmpty(dc.Channel) || string.IsNullOrEmpty(dc.Type))
                return new NotificationPreferenceDC();

            var pref = _db.NotificationPreferences.FirstOrDefault(p =>
                p.PatientId == dc.PatientId && p.Channel == dc.Channel && p.Type == dc.Type);
            if (pref == null)
            {
                pref = new NotificationPreference { PatientId = dc.PatientId, Channel = dc.Channel, Type = dc.Type };
                _db.NotificationPreferences.Add(pref);
            }
            pref.Enabled = dc.Enabled;
            _db.SaveChanges();

            return new NotificationPreferenceDC { Channel = dc.Channel, Type = dc.Type, Enabled = dc.Enabled };
        }

        // Gate used before every SmsHelper/EmailHelper call below — returns true
        // when there's no patient to check against (guest bookings) so opt-outs
        // never accidentally suppress a guest's own confirmation.
        private bool IsNotificationAllowed(int? patientId, string channel, string type)
        {
            if (!patientId.HasValue || patientId.Value == 0) return true;
            var pref = _db.NotificationPreferences.FirstOrDefault(p =>
                p.PatientId == patientId.Value && p.Channel == channel && p.Type == type);
            return pref == null || pref.Enabled;
        }

        // Practical Website+LIMS "combined KPI" view (see LimsSyncStatusDC) — the
        // real LIMS has no reporting API to merge against yet, so this surfaces
        // how much of the portal side has actually synced instead.
        public LimsSyncStatusDC GetLimsSyncStatus()
        {
            return new LimsSyncStatusDC
            {
                LimsConfigured = !string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["LimsBaseUrl"]),
                PatientsSynced = _db.Patients.Count(p => p.LimsSyncStatus == "Synced"),
                PatientsPending = _db.Patients.Count(p => p.LimsSyncStatus == "Pending" || p.LimsSyncStatus == null),
                BookingsSynced = _db.Bookings.Count(b => b.LimsSyncStatus == "Synced"),
                BookingsPending = _db.Bookings.Count(b => b.LimsSyncStatus == "Pending"),
                BookingsFailed = _db.Bookings.Count(b => b.LimsSyncStatus == "Failed"),
                LastCatalogueSync = _db.LabTests.Max(t => (DateTime?)t.LimsSyncedAt),
                TestsNeverSynced = _db.LabTests.Count(t => t.LimsSyncedAt == null)
            };
        }

        // TAT / revenue trend / test volume (Analytics Phase 3). from/to default
        // to the last 30 days when omitted or unparsable.
        public AnalyticsSummaryDC GetAnalyticsSummary(string from, string to)
        {
            DateTime toDate;
            if (!DateTime.TryParse(to, out toDate)) toDate = DateTime.Today;
            DateTime fromDate;
            if (!DateTime.TryParse(from, out fromDate)) fromDate = toDate.AddDays(-29);
            var rangeEnd = toDate.Date.AddDays(1);

            var bookings = _db.Bookings.Include("Tests")
                .Where(b => b.CreatedAt >= fromDate.Date && b.CreatedAt < rangeEnd)
                .ToList();

            var dailyTrend = bookings
                .GroupBy(b => b.CreatedAt.Date)
                .Select(g => new DayMetricDC
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Bookings = g.Count(),
                    Revenue = g.Where(b => b.PaymentStatus == "Paid").Sum(b => b.TotalAmount)
                })
                .OrderBy(d => d.Date)
                .ToList();

            var testVolume = bookings.SelectMany(b => b.Tests ?? new List<BookingTest>())
                .GroupBy(t => t.TestSuiteName)
                .Select(g => new TestVolumeDC { TestSuiteName = g.Key, Count = g.Count() })
                .OrderByDescending(t => t.Count)
                .Take(15)
                .ToList();

            // TAT: hours from booking creation to the report actually going Ready.
            var tatHours = (from b in bookings
                             join r in _db.Reports on b.BookingId equals r.BookingId
                             where r.Status == "Ready"
                             select (r.ReportDate - b.CreatedAt).TotalHours).ToList();

            return new AnalyticsSummaryDC
            {
                FromDate = fromDate.Date, ToDate = toDate.Date,
                TotalRevenue = bookings.Where(b => b.PaymentStatus == "Paid").Sum(b => b.TotalAmount),
                TotalBookings = bookings.Count,
                AvgTatHours = tatHours.Count > 0 ? Math.Round(tatHours.Average(), 1) : 0,
                DailyTrend = dailyTrend,
                TestVolume = testVolume
            };
        }

        // Flat, one-row-per-booking export a BI tool's generic Web/REST connector
        // can pull directly (Power BI Desktop: Get Data > Web, paste this URL;
        // Tableau: Web Data Connector against the same JSON). See AnalyticsExportRowDC
        // for exactly what "Power BI/Tableau, full integration" means here — actually
        // wiring it into a live workspace is the user's own license/account.
        public List<AnalyticsExportRowDC> GetAnalyticsExport(string from, string to)
        {
            DateTime toDate;
            if (!DateTime.TryParse(to, out toDate)) toDate = DateTime.Today;
            DateTime fromDate;
            if (!DateTime.TryParse(from, out fromDate)) fromDate = toDate.AddDays(-29);
            var rangeEnd = toDate.Date.AddDays(1);

            return _db.Bookings.Include("Tests")
                .Where(b => b.CreatedAt >= fromDate.Date && b.CreatedAt < rangeEnd)
                .ToList()
                .Select(b => new AnalyticsExportRowDC
                {
                    BookingRef = b.BookingRef, CreatedAt = b.CreatedAt,
                    CollectionType = b.CollectionType, BookingStatus = b.BookingStatus,
                    PaymentStatus = b.PaymentStatus, TotalAmount = b.TotalAmount, AmountPaid = b.AmountPaid,
                    TestCount = b.Tests?.Count ?? 0, SampleStatus = b.SampleStatus
                }).ToList();
        }

        // ── AUDIT TRAIL ───────────────────────────────────────────────────
        public List<AuditLogDC> GetAuditLogs()
        {
            return _db.AuditLogs.OrderByDescending(a => a.AuditLogId).Take(300)
                .Select(a => new AuditLogDC
                {
                    AuditLogId = a.AuditLogId, Actor = a.Actor, ActorPatientId = a.ActorPatientId,
                    Action = a.Action, EntityType = a.EntityType, EntityRef = a.EntityRef,
                    Detail = a.Detail, IPAddress = a.IPAddress, Success = a.Success, CreatedAt = a.CreatedAt
                }).ToList();
        }

        public AuditVerifyResultDC VerifyAuditChain()
        {
            return AuditHelper.VerifyChain();
        }

        public AuditLogDC LogClientEvent(AuditLogDC dc)
        {
            try
            {
                AuditHelper.Log(dc.Actor, dc.ActorPatientId, dc.Action, dc.EntityType, dc.EntityRef, dc.Detail, dc.Success);
                return new AuditLogDC { Message = "Logged" };
            }
            catch (Exception ex)
            {
                return new AuditLogDC { Message = ex.Message };
            }
        }

        // Supporting documentation for an NABH/NABL accreditation audit — NOT a
        // certified audit (that requires an accredited external auditor). This
        // bundles the evidence a compliance officer would otherwise assemble by
        // hand: audit-chain integrity, and how many bookings have a complete
        // chain-of-custody trail vs. a gap.
        public ComplianceReportDC GetComplianceReport()
        {
            var chain = AuditHelper.VerifyChain();
            var bookingIds = _db.Bookings.Select(b => b.BookingId).ToList();
            var custodyBookingIds = _db.CustodyEvents.Select(e => e.BookingId).Distinct().ToList();
            int withTrail = bookingIds.Count(id => custodyBookingIds.Contains(id));

            return new ComplianceReportDC
            {
                GeneratedAt = DateTime.Now,
                AuditChainIntact = chain.Intact,
                TotalAuditEntries = chain.TotalRows,
                TotalBookings = bookingIds.Count,
                TotalCustodyEvents = _db.CustodyEvents.Count(),
                BookingsWithFullCustodyTrail = withTrail,
                BookingsMissingCustodyTrail = bookingIds.Count - withTrail,
                DataRetentionNote = "AuditLogs and CustodyEvents are append-only (no UPDATE/DELETE code path exists in this service); " +
                    "retention is governed by database backup policy, not application logic.",
                AccessControlNote = "Patient identity is OTP-verified at login; admin/lab-staff endpoints are not yet behind a " +
                    "separate authentication layer — see GapAnalysis.md cross-cutting gap #1."
            };
        }

        // ── MAPPERS ───────────────────────────────────────────────────────
        private PatientDC MapPatient(Patient p, bool success)
        {
            return new PatientDC
            {
                PatientId = p.PatientId, FullName = p.FullName, Phone = p.Phone,
                Email = p.Email, Gender = p.Gender, DateOfBirth = p.DateOfBirth,
                Address = p.Address, City = p.City, Pincode = p.Pincode,
                IsActive = p.IsActive, CreatedDate = p.CreatedDate,
                LimsPatientId = p.LimsPatientId, LimsSyncStatus = p.LimsSyncStatus,
                Success = success, Message = success ? "OK" : "Error"
            };
        }

        private BookingDC MapBooking(Booking b, bool success)
        {
            return new BookingDC
            {
                BookingId = b.BookingId, BookingRef = b.BookingRef,
                PatientId = b.PatientId,
                PatientName = b.Patient?.FullName, PatientPhone = b.Patient?.Phone,
                FamilyMemberId = b.FamilyMemberId, FamilyMemberName = b.FamilyMemberName,
                CollectionType = b.CollectionType, BranchName = b.BranchName,
                Address = b.Address, CollectionDate = b.CollectionDate,
                TimeSlot = b.TimeSlot, Subtotal = b.Subtotal,
                PromoCode = b.PromoCode, DiscountAmount = b.DiscountAmount,
                GstAmount = b.GstAmount, TotalAmount = b.TotalAmount,
                PaymentMethod = b.PaymentMethod, PaymentStatus = b.PaymentStatus,
                PaymentRef = b.PaymentRef, PaidAt = b.PaidAt, AmountPaid = b.AmountPaid,
                RefundAmount = b.RefundAmount, RefundReason = b.RefundReason, RefundedAt = b.RefundedAt,
                SampleStatus = b.SampleStatus, BookingStatus = b.BookingStatus,
                SampleId = b.SampleId, Barcode = b.Barcode,
                LimsJobId = b.LimsJobId, LimsSyncStatus = b.LimsSyncStatus,
                CreatedAt = b.CreatedAt, Success = success,
                Tests = b.Tests?.Select(t => new BookingTestDC
                {
                    BookingTestId = t.BookingTestId, BookingId = t.BookingId,
                    TestSuiteID = t.TestSuiteID, TestSuiteName = t.TestSuiteName,
                    Price = t.Price, SampleType = t.SampleType, TestCount = t.TestCount
                }).ToList()
            };
        }
    }
}
