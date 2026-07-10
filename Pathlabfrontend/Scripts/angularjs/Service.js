angular.module("PathlabModule")
    .service("PathlabService", function ($http) {
        var baseUrl = ((typeof WCF_SERVICE_URL !== 'undefined' && WCF_SERVICE_URL) ? WCF_SERVICE_URL : "http://localhost:2091/PathlabService.svc") + "/";

        function post(url, data) {
            return $http({
                method: "POST",
                url: baseUrl + url,
                data: data,
                headers: { "Content-Type": "application/json" }
            });
        }
        function get(url) {
            return $http({ method: "GET", url: baseUrl + url });
        }
        // Admin-panel-only endpoints — ADMIN_API_KEY is emitted by
        // Views/Admin/Dashboard.cshtml only after the session PIN gate passes,
        // and must match WcfPathlabService/Web.config's AdminApiKey.
        function adminGet(url) {
            return $http({
                method: "GET", url: baseUrl + url,
                headers: { "X-Admin-Key": (typeof ADMIN_API_KEY !== 'undefined' && ADMIN_API_KEY) || "" }
            });
        }
        function adminPost(url, data) {
            return $http({
                method: "POST", url: baseUrl + url, data: data,
                headers: { "Content-Type": "application/json", "X-Admin-Key": (typeof ADMIN_API_KEY !== 'undefined' && ADMIN_API_KEY) || "" }
            });
        }

        // Patient
        this.sendOtp = function (phone) {
            return post("SendOtp", { Phone: phone });
        };
        this.verifyOtp = function (phone, otp) {
            return post("VerifyOtp", { Phone: phone, Token: otp });
        };
        this.registerPatient = function (data) {
            return post("RegisterPatient", data);
        };
        this.getPatient = function (patientId) {
            return get("GetPatient/" + patientId);
        };
        this.updatePatient = function (data) {
            return post("UpdatePatient", data);
        };
        // New number must be OTP-verified first: sendOtp(newPhone) → changePhone(id, newPhone, otp)
        this.changePhone = function (patientId, newPhone, otp) {
            return post("ChangePhone", { PatientId: patientId, Phone: newPhone, Token: otp });
        };

        // Family members (server-side — Patient Registration Phase 3)
        this.getFamilyMembers = function (patientId) {
            return get("GetFamilyMembers/" + patientId);
        };
        this.addFamilyMember = function (data) {
            return post("AddFamilyMember", data);
        };
        this.removeFamilyMember = function (familyMemberId) {
            return get("RemoveFamilyMember/" + familyMemberId);
        };
        // Full patient dashboard in one call (Patient Registration Phase 4)
        this.getPatientDashboard = function (patientId) {
            return get("GetPatientDashboard/" + patientId);
        };
        // Notification preferences (Notifications Phase 4)
        this.getNotificationPreferences = function (patientId) {
            return get("GetNotificationPreferences/" + patientId);
        };
        this.updateNotificationPreference = function (patientId, channel, type, enabled) {
            return post("UpdateNotificationPreference", { PatientId: patientId, Channel: channel, Type: type, Enabled: enabled });
        };

        // Tests
        this.getAllTests = function () {
            return get("GetAllTests");
        };
        this.searchTests = function (keyword) {
            return get("SearchTests/" + encodeURIComponent(keyword));
        };
        this.getAllPackages = function () {
            return get("GetAllPackages");
        };
        // Unified tests + packages list with TestType ("Test"/"Package") for the landing page
        this.getTestCatalogue = function () {
            return get("GetTestCatalogue");
        };
        this.getPackageById = function (id) {
            return get("GetPackageById/" + id);
        };

        // Bookings
        this.createBooking = function (data) {
            return post("CreateBooking", data);
        };
        this.getBookingByRef = function (ref) {
            return get("GetBookingByRef/" + encodeURIComponent(ref));
        };
        this.getBookingsByPatient = function (patientId) {
            return get("GetBookingsByPatient/" + patientId);
        };
        this.getSampleTimeline = function (bookingRef) {
            return get("GetSampleTimeline/" + encodeURIComponent(bookingRef));
        };
        this.updateSampleStatus = function (bookingRef, status) {
            return get("UpdateSampleStatus/" + encodeURIComponent(bookingRef) + "/" + status);
        };
        this.cancelBooking = function (bookingRef) {
            return get("CancelBooking/" + encodeURIComponent(bookingRef));
        };
        // Promo codes (Cart & Checkout Phase 3) — server computes the discount
        this.validatePromoCode = function (code, subtotal) {
            return get("ValidatePromoCode?code=" + encodeURIComponent(code) + "&subtotal=" + subtotal);
        };
        // Phlebotomist-facing collection queue (Sample Collection Phase 3)
        this.getCollectionQueue = function () {
            return get("GetCollectionQueue");
        };
        // Chain of custody (Sample Collection Phase 4)
        this.logCustodyEvent = function (bookingRef, handlerName, handlerRole, action, location, notes) {
            return post("LogCustodyEvent", {
                BookingRef: bookingRef, HandlerName: handlerName, HandlerRole: handlerRole,
                Action: action, Location: location, Notes: notes
            });
        };
        this.getChainOfCustody = function (bookingRef) {
            return get("GetChainOfCustody/" + encodeURIComponent(bookingRef));
        };
        // Saved carts (Cart & Checkout Phase 4)
        this.getSavedCart = function (patientId) {
            return get("GetSavedCart/" + patientId);
        };
        this.saveCart = function (patientId, cartJson) {
            return post("SaveCart", { PatientId: patientId, CartJson: cartJson });
        };
        // Recurring-test reminders — no auto-charge (Cart & Checkout Phase 4)
        this.getTestSubscriptions = function (patientId) {
            return get("GetTestSubscriptions/" + patientId);
        };
        this.addTestSubscription = function (patientId, testSuiteId, testSuiteName, frequencyDays) {
            return post("AddTestSubscription", { PatientId: patientId, TestSuiteID: testSuiteId, TestSuiteName: testSuiteName, FrequencyDays: frequencyDays });
        };
        this.cancelTestSubscription = function (id) {
            return get("CancelTestSubscription/" + id);
        };

        // Payments — front-desk settlement or gateway webhook relay
        this.updatePaymentStatus = function (bookingRef, status, method, paymentRef, source, amountPaid) {
            return post("UpdatePaymentStatus", {
                BookingRef: bookingRef, PaymentStatus: status,
                PaymentMethod: method || null, PaymentRef: paymentRef || null,
                Source: source || "Portal", AmountPaid: amountPaid || null
            });
        };
        // Refund on a Paid/PartiallyPaid booking (Billing Phase 3)
        this.refundPayment = function (bookingRef, amount, reason, source) {
            return post("RefundPayment", { BookingRef: bookingRef, Amount: amount, Reason: reason, Source: source || "FrontDesk" });
        };

        // Reports
        this.getReportsByPatient = function (patientId) {
            return get("GetReportsByPatient/" + patientId);
        };
        // Lab-staff/LIMS ingestion of a finished report (Report Delivery Phase 2)
        this.attachReport = function (bookingRef, reportFilePath, source) {
            return post("AttachReport", { BookingRef: bookingRef, ReportFilePath: reportFilePath, Source: source || "Staff" });
        };

        // Invoices (Billing Phase 2 — server-generated, numbered GST invoice)
        this.getInvoiceByBookingRef = function (bookingRef) {
            return get("GetInvoiceByBookingRef/" + encodeURIComponent(bookingRef));
        };
        // Reconciliation (Billing Phase 4)
        this.getReconciliationSummary = function (from, to) {
            return adminGet("GetReconciliationSummary?from=" + (from || "") + "&to=" + (to || ""));
        };
        // Doctor sharing (Report Delivery Phase 3)
        this.shareReportWithDoctor = function (bookingRef, doctorName, doctorEmail) {
            return post("ShareReportWithDoctor", { BookingRef: bookingRef, DoctorName: doctorName, DoctorEmail: doctorEmail });
        };
        // Doctor share link fix — random token instead of the guessable BookingRef
        this.getOrCreateShareToken = function (bookingRef, patientId) {
            return get("GetOrCreateShareToken/" + encodeURIComponent(bookingRef) + "/" + patientId);
        };
        this.getBookingByShareToken = function (token) {
            return get("GetBookingByShareToken/" + encodeURIComponent(token));
        };

        // Home Collection popup fix — real server-side lead capture
        this.submitHomeCollectionLead = function (name, mobile, city) {
            return post("SubmitHomeCollectionLead", { Name: name, Mobile: mobile, City: city });
        };
        this.getHomeCollectionLeads = function () {
            return adminGet("GetHomeCollectionLeads");
        };

        // Admin
        this.getAdminStats = function () {
            return adminGet("GetAdminStats");
        };
        this.getAllPatients = function () {
            return adminGet("GetAllPatients");
        };
        this.getAllBookings = function () {
            return adminGet("GetAllBookings");
        };
        this.getStaffAlerts = function () {
            return adminGet("GetStaffAlerts");
        };
        this.getNotificationLogs = function () {
            return adminGet("GetNotificationLogs");
        };
        this.getNotificationsByPatient = function (patientId) {
            return get("GetNotificationsByPatient/" + patientId);
        };
        // Website+LIMS integration health (Analytics Phase 2 stand-in for combined BI)
        this.getLimsSyncStatus = function () {
            return adminGet("GetLimsSyncStatus");
        };
        this.syncTestCatalogue = function () {
            return adminPost("SyncTestCatalogue", {});
        };
        // TAT / revenue trend / test volume (Analytics Phase 3)
        this.getAnalyticsSummary = function (from, to) {
            return adminGet("GetAnalyticsSummary?from=" + (from || "") + "&to=" + (to || ""));
        };
        // Flat export for a BI tool's Web/REST connector (Analytics Phase 4)
        this.getAnalyticsExport = function (from, to) {
            return adminGet("GetAnalyticsExport?from=" + (from || "") + "&to=" + (to || ""));
        };

        // Audit trail
        this.getAuditLogs = function () {
            return adminGet("GetAuditLogs");
        };
        this.verifyAuditChain = function () {
            return adminGet("VerifyAuditChain");
        };
        this.logClientEvent = function (actor, actorPatientId, action, entityType, entityRef, detail) {
            return post("LogClientEvent", {
                Actor: actor, ActorPatientId: actorPatientId, Action: action,
                EntityType: entityType, EntityRef: entityRef, Detail: detail, Success: true
            });
        };
        // Supporting documentation for an NABH/NABL audit — not a certified audit (Phase 4)
        this.getComplianceReport = function () {
            return adminGet("GetComplianceReport");
        };

        // Feedback / complaints (Help section fix) — real, self-hosted, not the
        // legacy Feedback.cshtml page whose form posts to a third party's API.
        this.submitFeedback = function (data) {
            return post("SubmitFeedback", data);
        };
        this.getAllFeedback = function () {
            return adminGet("GetAllFeedback");
        };
    });
