// ======================== SHARED AUTH BRIDGE ========================
// Two login UIs persist the patient differently: the Account/Login page writes
// localStorage "sd_user" (PascalCase name parts), while the portal OTP popup
// writes sessionStorage "pathlabUser" (WCF VerifyOtp response shape). These
// helpers read/write BOTH so a login made on either screen is recognised
// everywhere (cart guards, checkout prefill, portal).
function sdGetUser() {
    var u = JSON.parse(sessionStorage.getItem("pathlabUser") || "null");
    if (u) return u;
    var s = JSON.parse(localStorage.getItem("sd_user") || "null");
    if (!s) return null;
    u = {
        Success: true,
        PatientId: s.WcfPatientId || 0,
        FullName: ((s.Title || "") + " " + (s.FirstName || "") + " " + (s.LastName || "")).replace(/\s+/g, " ").trim(),
        Phone: s.MobileNumber || "",
        Email: s.Email || "",
        Gender: s.Gender || "",
        DateOfBirth: s.DOB || "",
        Address: s.Address || "",
        City: s.City || "",
        Pincode: s.Pincode || ""
    };
    sessionStorage.setItem("pathlabUser", JSON.stringify(u));
    return u;
}

function sdSyncUser(u) {
    sessionStorage.setItem("pathlabUser", JSON.stringify(u));
    var parts = (u.FullName || "").trim().split(" ");
    localStorage.setItem("sd_user", JSON.stringify({
        PatientID: "PAT" + (u.PatientId || 0),
        WcfPatientId: u.PatientId || 0,
        MobileNumber: u.Phone || "",
        Title: "",
        FirstName: parts[0] || "",
        LastName: parts.slice(1).join(" "),
        Email: u.Email || "",
        Gender: u.Gender || "",
        DOB: u.DateOfBirth || "",
        Address: u.Address || "",
        City: u.City || "",
        Pincode: u.Pincode || "",
        registeredAt: new Date().toISOString()
    }));
}

function sdClearUser() {
    sessionStorage.removeItem("pathlabUser");
    localStorage.removeItem("sd_user");
}

// ======================== GA4 BOOKING-FUNNEL EVENTS ========================
// Thin wrapper so every call site stays a one-liner and never throws when
// ga4.js hasn't defined SDAnalytics yet (script load order edge cases) or GA
// isn't configured (SDAnalytics.trackEvent itself no-ops in that case).
function trackGa(name, params) {
    if (window.SDAnalytics) window.SDAnalytics.trackEvent(name, params);
}
function gaItem(t) {
    return { item_id: t.TestSuiteID || t.TestId, item_name: t.TestSuiteName, price: t.Price, item_category: t.TestType || "Test" };
}

// ======================== HOME CONTROLLER ========================
angular.module("PathlabModule")
    .controller("HomeController", function ($scope, $window, PathlabService) {
        $scope.searchQuery = "";
        $scope.searchResults = [];
        $scope.popularTests = [];
        $scope.packages = [];
        $scope.featuredCatalogue = [];

        PathlabService.getAllTests().then(function (r) {
            $scope.popularTests = r.data;
        });
        PathlabService.getAllPackages().then(function (r) {
            $scope.packages = r.data;
        });

        // Landing-page cards: curated picks first (3 tests + 1 package), then
        // top up from whatever the catalogue returns so the row is never short.
        function pickFeatured(list) {
            var preferred = ["TST001", "TST006", "TST005", "PKG002"];
            var featured = preferred.map(function (id) {
                return list.filter(function (t) { return t.TestSuiteID === id; })[0];
            }).filter(Boolean);
            list.forEach(function (t) {
                if (featured.length < 4 && featured.indexOf(t) === -1) featured.push(t);
            });
            return featured.slice(0, 4);
        }

        PathlabService.getTestCatalogue().then(function (r) {
            $scope.featuredCatalogue = pickFeatured(r.data || []);
        }, function () {
            // WCF down — lims-api.js serves the offline catalogue (native Promise, so applyAsync)
            if (!$window.LIMS) return;
            $window.LIMS.getCatalogue().then(function (list) {
                $scope.$applyAsync(function () { $scope.featuredCatalogue = pickFeatured(list); });
            });
        });

        $scope.searchTests = function () {
            var q = $scope.searchQuery.trim();
            if (q.length < 2) { $scope.searchResults = []; return; }
            PathlabService.searchTests(q).then(function (r) {
                $scope.searchResults = r.data;
            });
        };

        $scope.addToCart = function (test) {
            var entry = {
                TestId: test.TestId, TestSuiteID: test.TestSuiteID,
                TestSuiteName: test.TestSuiteName, Price: test.Price,
                SampleType: test.SampleType, TestCount: test.TestCount,
                FastingRequired: test.FastingRequired || /yes|required/i.test(test.Fasting || ""),
                TestType: test.TestType || "Test"
            };
            var cart = JSON.parse(sessionStorage.getItem("sd_cart") || "[]");
            var exists = cart.some(function (t) { return t.TestSuiteID === entry.TestSuiteID; });
            if (!exists) cart.push(entry);
            sessionStorage.setItem("sd_cart", JSON.stringify(cart));
            trackGa("add_to_cart", { currency: "INR", value: entry.Price, items: [gaItem(entry)] });
            $window.location.href = APP_ROOT + "Booking/Cart";
        };

        $scope.goBookTest = function () {
            $window.location.href = APP_ROOT + "Test/BookTest";
        };

        // Home collection lead popup — shown on every homepage load, like the original site
        $scope.showLeadPopup = true;
        $scope.leadDone = false;
        $scope.leadError = "";
        $scope.lead = { name: "", mobile: "", city: "Bhopal" };

        $scope.closeLeadPopup = function () {
            $scope.showLeadPopup = false;
        };

        $scope.leadSubmitting = false;
        $scope.submitLead = function () {
            var name = ($scope.lead.name || "").trim();
            var mobile = ($scope.lead.mobile || "").trim();
            if (!name) { $scope.leadError = "Please enter your full name."; return; }
            if (!/^[6-9]\d{9}$/.test(mobile)) { $scope.leadError = "Please enter a valid 10 digit mobile number."; return; }
            $scope.leadError = "";

            // Connect the popup to the actual booking flow: carry what they just
            // typed forward so Choose Collection pre-selects Home Collection and
            // pre-fills their city instead of discarding it once they click through.
            sessionStorage.setItem("sd_home_lead", JSON.stringify({ name: name, mobile: mobile, city: $scope.lead.city }));

            // Home Collection popup fix — this used to only write to
            // localStorage, so the lead was lost the moment the tab closed and
            // no one on staff ever saw it. Now it's a real server record (see
            // Admin > Leads); the popup still confirms immediately either way
            // so a slow/failed request never blocks the visitor.
            $scope.leadSubmitting = true;
            PathlabService.submitHomeCollectionLead(name, mobile, $scope.lead.city).then(function () {
                $scope.leadSubmitting = false;
            }, function () {
                $scope.leadSubmitting = false;
            });

            $scope.leadDone = true;
        };
    });

// ======================== BOOK TEST CONTROLLER ========================
angular.module("PathlabModule")
    .controller("BookTestController", function ($scope, $window, PathlabService) {
        $scope.allTests = [];
        $scope.filteredTests = [];
        $scope.categories = [];
        $scope.activeCategory = "";
        $scope.searchQuery = "";
        $scope.cart = JSON.parse(sessionStorage.getItem("sd_cart") || "[]");
        $scope.isLoading = true;

        PathlabService.getAllTests().then(function (r) {
            $scope.allTests = r.data;
            $scope.filteredTests = r.data;
            var cats = {};
            r.data.forEach(function (t) { if (t.Category) cats[t.Category] = true; });
            $scope.categories = Object.keys(cats);
            $scope.isLoading = false;
            trackGa("view_item_list", { item_list_name: "Test Catalogue", items: r.data.slice(0, 20).map(gaItem) });
        });

        // Saved cart (Cart & Checkout Phase 4) — survives a device switch. Only
        // offered when the local cart is empty, so it never clobbers what the
        // patient is actively building on this device.
        var cartUser = sdGetUser();
        $scope.savedCartAvailable = null;
        if (cartUser && $scope.cart.length === 0) {
            PathlabService.getSavedCart(cartUser.PatientId).then(function (r) {
                if (r.data && r.data.Success && r.data.CartJson) $scope.savedCartAvailable = r.data;
            });
        }
        $scope.restoreSavedCart = function () {
            $scope.cart = JSON.parse($scope.savedCartAvailable.CartJson || "[]");
            sessionStorage.setItem("sd_cart", JSON.stringify($scope.cart));
            $scope.savedCartAvailable = null;
            showToast("Saved cart restored", "success");
        };
        $scope.saveCartForLater = function () {
            if (!cartUser) { showToast("Login to save your cart", "error"); return; }
            PathlabService.saveCart(cartUser.PatientId, JSON.stringify($scope.cart)).then(function () {
                showToast("Cart saved — pick up where you left off on any device", "success");
            });
        };

        $scope.filterTests = function () {
            var q = ($scope.searchQuery || "").toLowerCase();
            $scope.filteredTests = $scope.allTests.filter(function (t) {
                var matchCat = !$scope.activeCategory || t.Category === $scope.activeCategory;
                var matchQ = !q || t.TestSuiteName.toLowerCase().indexOf(q) >= 0;
                return matchCat && matchQ;
            });
        };

        $scope.setCategory = function (cat) {
            $scope.activeCategory = cat;
            $scope.filterTests();
        };

        // Single "Select Test Suite" dropdown
        $scope.selectedSuiteId = "";
        $scope.selectedSuite = function () {
            if (!$scope.selectedSuiteId) return null;
            return $scope.allTests.filter(function (t) { return t.TestSuiteID === $scope.selectedSuiteId; })[0] || null;
        };

        function testKey(t) { return t.TestSuiteID || t.TestId; }

        $scope.isInCart = function (t) {
            if (!t) return false;
            return $scope.cart.some(function (c) { return testKey(c) === testKey(t); });
        };

        $scope.toggleCart = function (t) {
            if (!t) return;
            if ($scope.isInCart(t)) {
                $scope.cart = $scope.cart.filter(function (c) { return testKey(c) !== testKey(t); });
            } else {
                $scope.cart.push(t);
                showToast(t.TestSuiteName + " added to cart");
                trackGa("add_to_cart", { currency: "INR", value: t.Price, items: [gaItem(t)] });
            }
            sessionStorage.setItem("sd_cart", JSON.stringify($scope.cart));
        };

        $scope.removeFromCart = function (item) {
            $scope.cart = $scope.cart.filter(function (c) { return testKey(c) !== testKey(item); });
            sessionStorage.setItem("sd_cart", JSON.stringify($scope.cart));
        };

        $scope.cartSubtotal = function () {
            return $scope.cart.reduce(function (s, t) { return s + t.Price; }, 0);
        };
        $scope.cartGst = function () {
            return Math.round($scope.cartSubtotal() * 0.18);
        };
        $scope.cartTotal = function () {
            return $scope.cartSubtotal() + $scope.cartGst();
        };

        $scope.proceedCheckout = function () {
            sessionStorage.setItem("sd_cart", JSON.stringify($scope.cart));
            // Cart page handles the rest of the flow: login → collection → review
            $window.location.href = APP_ROOT + "Booking/Cart";
        };
    });

// ======================== CHECKOUT CONTROLLER ========================
angular.module("PathlabModule")
    .controller("CheckoutController", function ($scope, $window, PathlabService) {
        $scope.step = 1;
        $scope.cart = JSON.parse(sessionStorage.getItem("sd_cart") || "[]");
        if ($scope.cart.length) {
            trackGa("begin_checkout", {
                currency: "INR",
                value: $scope.cart.reduce(function (s, t) { return s + t.Price; }, 0),
                items: $scope.cart.map(gaItem)
            });
        }
        $scope.collectionType = "walkin";
        $scope.payMethod = "";
        $scope.isProcessing = false;
        $scope.confirmedRef = "";
        $scope.confirmedSampleId = "";
        $scope.patient = {};
        $scope.branchName = "";
        $scope.homeAddress = "";
        $scope.collectionDate = "";
        $scope.timeSlot = "";

        var d = new Date();
        $scope.minDate = d.toISOString().split("T")[0];

        // Same branch and slot lists as the ChooseCollection page, so values
        // saved there match the dropdown options here.
        $scope.branches = [
            "Bhopal – Main Branch, MP Nagar",
            "Bhopal – Kolar Road Centre",
            "Bhopal – Arera Colony",
            "Indore – Vijay Nagar",
            "Indore – Palasia Square"
        ];
        $scope.timeSlots = ["07:00 – 08:00 AM", "08:00 – 09:00 AM", "09:00 – 10:00 AM", "10:00 – 11:00 AM", "11:00 AM – 12:00 PM", "12:00 – 01:00 PM", "02:00 – 03:00 PM", "03:00 – 04:00 PM", "04:00 – 05:00 PM", "05:00 – 06:00 PM"];

        var user = sdGetUser();
        if (user) {
            $scope.patient.FullName = user.FullName;
            $scope.patient.Phone = user.Phone;
            $scope.patient.Email = user.Email;
            $scope.patient.Gender = user.Gender;
        }

        // Book for a family member (Patient Registration Phase 3) — defaults to
        // the account holder ("" = self, matches CreateBookingDC.FamilyMemberId=null).
        $scope.familyMembers = [];
        $scope.bookingForId = "";
        if (user && user.PatientId) {
            PathlabService.getFamilyMembers(user.PatientId).then(function (r) {
                $scope.familyMembers = r.data || [];
            });
        }

        // Promo code (Cart & Checkout Phase 3) — server computes the discount;
        // this only ever displays what ValidatePromoCode/CreateBooking returned.
        $scope.promoCode = "";
        $scope.promoResult = null;
        $scope.applyPromoCode = function () {
            if (!$scope.promoCode) return;
            PathlabService.validatePromoCode($scope.promoCode, $scope.subtotal()).then(function (r) {
                $scope.promoResult = r.data;
                showToast(r.data.Message, r.data.Success ? "success" : "error");
            }, function () { showToast("Unable to validate promo code", "error"); });
        };
        $scope.removePromoCode = function () {
            $scope.promoCode = ""; $scope.promoResult = null;
        };
        $scope.discount = function () {
            return ($scope.promoResult && $scope.promoResult.Success) ? $scope.promoResult.DiscountAmount : 0;
        };

        // Prefill from the ChooseCollection step so the patient reviews their
        // choice here instead of entering it twice. Fields stay editable.
        var col = JSON.parse(sessionStorage.getItem("sd_collection") || "null");
        if (col) {
            $scope.collectionType = col.type === "home" ? "home" : "walkin";
            $scope.branchName = col.branchName || "";
            if (col.address) {
                $scope.homeAddress = [col.address.street, col.address.city, col.address.state, col.address.pin]
                    .filter(Boolean).join(", ");
            }
            if (col.date) $scope.collectionDate = new Date(col.date + "T00:00:00");
            if (col.timeSlot && $scope.timeSlots.indexOf(col.timeSlot) === -1) $scope.timeSlots.unshift(col.timeSlot);
            $scope.timeSlot = col.timeSlot || "";
            if ($scope.branchName && $scope.branches.indexOf($scope.branchName) === -1) $scope.branches.unshift($scope.branchName);
        }

        $scope.subtotal = function () { return $scope.cart.reduce(function (s, t) { return s + t.Price; }, 0); };
        $scope.gst = function () { return Math.round(($scope.subtotal() - $scope.discount()) * 0.18); };
        $scope.total = function () {
            var extra = $scope.collectionType === "home" ? 100 : 0;
            return $scope.subtotal() - $scope.discount() + $scope.gst() + extra;
        };

        $scope.goStep2 = function () {
            if ($scope.cart.length === 0) { showToast("Your cart is empty", "error"); return; }
            if (!$scope.patient.FullName || !$scope.patient.Phone) {
                showToast("Please fill in patient details", "error"); return;
            }
            if (!$scope.collectionDate || !$scope.timeSlot) {
                showToast("Please select date and time slot", "error"); return;
            }
            $scope.step = 2;
        };

        $scope.waConfirmLink = "";

        function razorpayConfigured() {
            var key = (window.SD_CONFIG && SD_CONFIG.RAZORPAY_KEY_ID) || "";
            return key.length > 0 && key.indexOf("YOUR_") === -1;
        }

        $scope.doPayment = function () {
            if (!$scope.payMethod) { showToast("Please select a payment method", "error"); return; }
            if ($scope.payMethod !== "counter" && razorpayConfigured()) {
                openRazorpay();
            } else {
                finalizeBooking(null);
            }
        };

        function openRazorpay() {
            $scope.isProcessing = true;
            var start = function () {
                var rzp = new Razorpay({
                    key: SD_CONFIG.RAZORPAY_KEY_ID,
                    amount: $scope.total() * 100, // paise
                    currency: "INR",
                    name: "PATHLAB Diagnostics",
                    description: "Lab test booking",
                    prefill: { name: $scope.patient.FullName, contact: $scope.patient.Phone, email: $scope.patient.Email || "" },
                    theme: { color: "#632c76" },
                    handler: function (resp) {
                        $scope.$apply(function () { finalizeBooking(resp.razorpay_payment_id); });
                    },
                    modal: { ondismiss: function () { $scope.$apply(function () { $scope.isProcessing = false; }); } }
                });
                rzp.open();
            };
            if (window.Razorpay) { start(); return; }
            var s = document.createElement("script");
            s.src = "https://checkout.razorpay.com/v1/checkout.js";
            s.onload = start;
            s.onerror = function () {
                $scope.$apply(function () {
                    $scope.isProcessing = false;
                    showToast("Payment gateway failed to load. Please try again.", "error");
                });
            };
            document.body.appendChild(s);
        }

        function finalizeBooking(paymentId) {
            $scope.isProcessing = true;

            var patientId = user ? user.PatientId : 0;
            var bookingData = {
                PatientId: patientId,
                PatientName: $scope.patient.FullName,
                PatientPhone: $scope.patient.Phone,
                PatientEmail: $scope.patient.Email,
                FamilyMemberId: $scope.bookingForId || null,
                CollectionType: $scope.collectionType,
                CollectionAddress: $scope.collectionType === "home" ? $scope.homeAddress : $scope.branchName,
                BranchName: $scope.collectionType === "walkin" ? $scope.branchName : null,
                CollectionDate: $scope.collectionDate,
                TimeSlot: $scope.timeSlot,
                PaymentMethod: $scope.payMethod,
                PaymentId: paymentId,
                PromoCode: ($scope.promoResult && $scope.promoResult.Success) ? $scope.promoResult.Code : null,
                TotalAmount: $scope.total(),
                Tests: $scope.cart.map(function (t) {
                    return {
                        TestSuiteID: t.TestSuiteID || t.TestId,
                        TestSuiteName: t.TestSuiteName,
                        Price: t.Price,
                        SampleType: t.SampleType || "",
                        TestCount: t.TestCount || 1
                    };
                })
            };

            PathlabService.createBooking(bookingData).then(function (r) {
                $scope.isProcessing = false;
                if (r.data && r.data.Success) {
                    $scope.confirmedRef = r.data.BookingRef;
                    $scope.confirmedSampleId = r.data.SampleId;
                    $scope.step = 3;
                    trackGa("purchase", {
                        transaction_id: r.data.BookingRef, currency: "INR",
                        value: $scope.total(), items: $scope.cart.map(gaItem)
                    });
                    sessionStorage.removeItem("sd_cart");
                    sessionStorage.removeItem("sd_collection");
                    buildWhatsAppLink(r.data.BookingRef, paymentId);
                } else {
                    showToast((r.data && r.data.Message) || "Booking failed. Please try again.", "error");
                }
            }, function () {
                $scope.isProcessing = false;
                showToast("Unable to connect to service. Please try again.", "error");
            });
        }

        // Confirmation SMS is sent server-side by WcfPathlabService (CreateBooking →
        // SmsHelper.Send, logged to NotificationLogs) — do NOT also send one from here,
        // that would double-SMS the patient. This just builds the optional WhatsApp link.
        function buildWhatsAppLink(ref, paymentId) {
            if (!window.SDNotify) return;
            var payLine = $scope.payMethod === "counter"
                ? "Payment: Rs." + $scope.total() + " due at counter."
                : "Paid: Rs." + $scope.total() + (paymentId ? " (Txn " + paymentId + ")" : "") + ".";
            var msg = "PATHLAB Diagnostics: Booking " + ref + " confirmed for " +
                $scope.collectionDate + " " + $scope.timeSlot + ". " + payLine +
                " Track your sample at " + window.location.origin + APP_ROOT + "Patient/TrackSample";
            $scope.waConfirmLink = SDNotify.whatsappLink(msg);
        }

        // Recurring-test rebook reminder (Cart & Checkout Phase 4) — schedules an
        // SMS reminder only, does NOT auto-charge (see TestSubscription entity).
        $scope.subscribeToRebook = false;
        $scope.rebookFrequencyDays = 90;
        $scope.createRebookReminders = function () {
            if (!user || !$scope.subscribeToRebook) return;
            $scope.cart.forEach(function (t) {
                PathlabService.addTestSubscription(user.PatientId, t.TestSuiteID || t.TestId, t.TestSuiteName, $scope.rebookFrequencyDays);
            });
            showToast("We'll remind you to rebook in " + $scope.rebookFrequencyDays + " days", "success");
        };
    });

// ======================== PATIENT PORTAL CONTROLLER ========================
angular.module("PathlabModule")
    .controller("PatientPortalController", function ($scope, $window, $interval, PathlabService) {
        $scope.currentUser = sdGetUser();
        $scope.activeTab = "dashboard";
        $scope.bookings = [];
        $scope.reports = [];
        $scope.notifications = [];
        $scope.profileEdit = {};
        $scope.isLoading = false;
        $scope.sampleSteps = ["Booked", "Sample Collected", "Processing", "Report Ready"];
        $scope.phleb = function (b) { return window.SDNotify ? SDNotify.phlebotomist(b.BookingRef) : { name: "", phone: "" }; };

        // read tab param from Razor ViewBag passed via script block
        var tabParam = document.getElementById("portalTab");
        if (tabParam && tabParam.value) $scope.activeTab = tabParam.value;

        if ($scope.currentUser) loadPortalData();

        function loadPortalData() {
            $scope.isLoading = true;
            PathlabService.getBookingsByPatient($scope.currentUser.PatientId).then(function (r) {
                $scope.bookings = r.data || [];
                $scope.isLoading = false;
                buildNotifications();
            });
            PathlabService.getReportsByPatient($scope.currentUser.PatientId).then(function (r) {
                $scope.reports = r.data || [];
            });
            PathlabService.getNotificationsByPatient($scope.currentUser.PatientId).then(function (r) {
                applyServerNotifs(r.data || []);
            });
            $scope.profileEdit = angular.copy($scope.currentUser);
            // input[type=date] only renders when ng-model is a Date object (Angular's
            // dateInputType formatter blanks anything that isn't) — currentUser stores
            // DateOfBirth as a plain "yyyy-MM-dd" string, so convert it here.
            if ($scope.profileEdit.DateOfBirth) {
                var dob = new Date($scope.profileEdit.DateOfBirth);
                $scope.profileEdit.DateOfBirth = isNaN(dob.getTime()) ? null : dob;
            }
            startPushPolling();
            loadFamilyMembers();
            loadNotifPreferences();

            // Full patient dashboard in one call (Patient Registration Phase 4) —
            // only the lifetime-spend figure isn't already derivable client-side
            // from bookings/reports, so that's the piece surfaced here.
            PathlabService.getPatientDashboard($scope.currentUser.PatientId).then(function (r) {
                $scope.patientDashboard = r.data;
            });
        }

        $scope.setTab = function (tab) { $scope.activeTab = tab; };

        // ── Book a Test (from inside the portal) — same catalogue + single
        // Test Suite dropdown as Views/Test/BookTest.cshtml, so patients don't
        // have to leave the portal to add tests; cart writes to the same
        // sessionStorage keys the standalone Cart page and quick re-book use.
        $scope.bookCatalogue = [];
        $scope.bookLoading = false;
        $scope.selectedSuiteId = "";
        $scope.selectedSuite = function () {
            if (!$scope.selectedSuiteId) return null;
            return $scope.bookCatalogue.filter(function (t) { return t.TestSuiteID === $scope.selectedSuiteId; })[0] || null;
        };
        function loadBookCatalogue() {
            if ($scope.bookCatalogue.length || $scope.bookLoading) return;
            $scope.bookLoading = true;
            PathlabService.getAllTests().then(function (r) {
                $scope.bookCatalogue = r.data || [];
                $scope.bookLoading = false;
            }, function () { $scope.bookLoading = false; });
        }
        $scope.portalCart = JSON.parse(sessionStorage.getItem("sd_cart") || "[]");
        $scope.isInPortalCart = function (t) {
            return !!t && $scope.portalCart.some(function (c) { return c.TestSuiteID === t.TestSuiteID; });
        };
        $scope.addSuiteToCart = function (t) {
            if (!t || $scope.isInPortalCart(t)) return;
            $scope.portalCart.push(t);
            ["cart", "sd_cart"].forEach(function (key) { sessionStorage.setItem(key, JSON.stringify($scope.portalCart)); });
            showToast(t.TestSuiteName + " added to cart", "success");
        };
        $scope.removeSuiteFromCart = function (t) {
            $scope.portalCart = $scope.portalCart.filter(function (c) { return c.TestSuiteID !== t.TestSuiteID; });
            ["cart", "sd_cart"].forEach(function (key) { sessionStorage.setItem(key, JSON.stringify($scope.portalCart)); });
        };
        $scope.portalCartTotal = function () {
            return $scope.portalCart.reduce(function (s, t) { return s + (t.Price || 0); }, 0);
        };
        var origSetTab = $scope.setTab;
        $scope.setTab = function (tab) {
            origSetTab(tab);
            if (tab === "book") loadBookCatalogue();
        };
        if ($scope.activeTab === "book") loadBookCatalogue();

        // Login/registration happens on the single Account/Login page (see the
        // Auth Gate link in Portal.cshtml) — this controller only ever reads the
        // resulting session via sdGetUser(), it doesn't run its own OTP flow.

        $scope.saveProfile = function () {
            var payload = angular.copy($scope.profileEdit);
            if (payload.DateOfBirth instanceof Date) {
                payload.DateOfBirth = payload.DateOfBirth.toISOString().split("T")[0];
            }
            PathlabService.updatePatient(payload).then(function (r) {
                if (r.data && r.data.Success) {
                    $scope.currentUser = angular.extend($scope.currentUser, payload);
                    sdSyncUser($scope.currentUser);
                    showToast("Profile updated!", "success");
                } else {
                    showToast("Update failed. Please try again.", "error");
                }
            });
        };

        // ── Change phone number (new number verified by OTP) ──
        $scope.phoneChange = { open: false, step: "enter", newPhone: "", otp: "" };

        $scope.startPhoneChange = function () {
            $scope.phoneChange = { open: true, step: "enter", newPhone: "", otp: "" };
        };

        $scope.sendPhoneChangeOtp = function () {
            var p = ($scope.phoneChange.newPhone || "").trim();
            if (!/^[6-9]\d{9}$/.test(p)) { showToast("Enter a valid 10-digit mobile number", "error"); return; }
            if (p === $scope.currentUser.Phone) { showToast("That is already your registered number", "error"); return; }
            PathlabService.sendOtp(p).then(function () {
                $scope.phoneChange.step = "otp";
                showToast("OTP sent to " + p + " (demo: 123456)");
            }, function () { showToast("Service error. Try again.", "error"); });
        };

        $scope.verifyPhoneChange = function () {
            PathlabService.changePhone($scope.currentUser.PatientId, $scope.phoneChange.newPhone, $scope.phoneChange.otp)
                .then(function (r) {
                    if (r.data && r.data.Success) {
                        $scope.currentUser.Phone = r.data.Phone;
                        $scope.profileEdit.Phone = r.data.Phone;
                        sdSyncUser($scope.currentUser);
                        $scope.phoneChange.open = false;
                        showToast("Phone number updated!", "success");
                    } else {
                        showToast((r.data && r.data.Message) || "Could not change number", "error");
                    }
                }, function () { showToast("Service error. Try again.", "error"); });
        };

        // ── Family Members (server-side — Patient Registration Phase 3) ──
        $scope.familyMembers = [];
        $scope.newMember = {};
        function loadFamilyMembers() {
            if (!$scope.currentUser) return;
            PathlabService.getFamilyMembers($scope.currentUser.PatientId).then(function (r) {
                $scope.familyMembers = (r.data || []).map(function (m) {
                    if (m.DateOfBirth) m.Age = Math.floor((new Date() - new Date(m.DateOfBirth)) / 31557600000);
                    return m;
                });
            });
        }

        $scope.addFamilyMember = function () {
            if (!$scope.newMember.Name || !$scope.newMember.Relation) {
                showToast("Enter name and relation", "error"); return;
            }
            var dob = null;
            if ($scope.newMember.Age) {
                var d = new Date(); d.setFullYear(d.getFullYear() - Number($scope.newMember.Age));
                dob = d.toISOString();
            }
            PathlabService.addFamilyMember({
                PatientId: $scope.currentUser.PatientId, Name: $scope.newMember.Name,
                Relation: $scope.newMember.Relation, Gender: $scope.newMember.Gender, DateOfBirth: dob
            }).then(function (r) {
                if (r.data && r.data.Success) {
                    loadFamilyMembers();
                    $scope.newMember = {};
                    showToast("Family member added", "success");
                } else {
                    showToast((r.data && r.data.Message) || "Could not add family member", "error");
                }
            }, function () { showToast("Service error. Try again.", "error"); });
        };
        $scope.removeFamilyMember = function (idx) {
            var m = $scope.familyMembers[idx];
            if (!m) return;
            PathlabService.removeFamilyMember(m.FamilyMemberId).then(function () {
                $scope.familyMembers.splice(idx, 1);
            });
        };

        $scope.logout = function () {
            if ($scope.currentUser) {
                PathlabService.logClientEvent($scope.currentUser.Phone, $scope.currentUser.PatientId,
                    "Logout", "Patient", $scope.currentUser.Phone, "Patient logged out from portal.");
            }
            sdClearUser();
            $scope.currentUser = null;
        };

        // ── Helpers ──
        var statusIdx = { "Booked": 0, "Confirmed": 1, "Processing": 2, "Ready": 3 };

        $scope.activeBookingsCount = function () {
            return $scope.bookings.filter(function (b) { return b.BookingStatus !== "Ready"; }).length;
        };
        $scope.pendingBillsCount = function () {
            return $scope.bookings.filter(function (b) { return b.PaymentStatus !== "Paid"; }).length;
        };

        $scope.dashStep = 0;
        $scope.$watch("bookings", function (v) {
            if (v && v.length) $scope.dashStep = v[0].SampleStatus || 0;
        });
        $scope.dashStepClass = function (i) {
            if (i < $scope.dashStep) return "s-done";
            if (i === $scope.dashStep) return "s-active";
            return "";
        };
        $scope.stepClass = function (b, i) {
            if (i < b.SampleStatus) return "s-done";
            if (i === b.SampleStatus) return "s-active";
            return "";
        };
        $scope.trackBooking = function (b) { $scope.activeTab = "track"; };

        $scope.cancelBooking = function (b) {
            if (!$window.confirm("Cancel booking " + b.BookingRef + "?")) return;
            PathlabService.cancelBooking(b.BookingRef).then(function (r) {
                if (r.data && r.data.Success) {
                    b.BookingStatus = "Cancelled";
                    showToast("Booking cancelled", "success");
                } else {
                    showToast((r.data && r.data.Message) || "Could not cancel booking", "error");
                }
            }, function () { showToast("Service error. Try again.", "error"); });
        };

        $scope.rebookBooking = function (b) {
            var cart = (b.Tests || []).map(function (t) {
                return { TestId: t.TestSuiteID, TestSuiteID: t.TestSuiteID, TestSuiteName: t.TestSuiteName, Price: t.Price, SampleType: t.SampleType, TestCount: t.TestCount };
            });
            sessionStorage.setItem("sd_cart", JSON.stringify(cart));
            $window.location.href = APP_ROOT + "Booking/ChooseCollection";
        };

        // ── Bills ──
        // Pulls the server-generated numbered invoice (Billing Phase 2) when one
        // exists — falls back to a plain receipt (no invoice number/GSTIN) for
        // bookings that haven't been marked Paid yet, so this never blocks on it.
        function printInvoice(b, inv) {
            var rows = (b.Tests || []).map(function (t) {
                return "<tr><td>" + t.TestSuiteName + "</td><td style='text-align:right'>&#8377;" + t.Price + "</td></tr>";
            }).join("");
            var gstLabel = inv && inv.HsnCode ? "GST (HSN " + inv.HsnCode + ")" : "GST";
            var header = inv && inv.Success
                ? "<p><strong>Invoice No:</strong> " + inv.InvoiceNumber + "<br>" +
                  (inv.Gstin ? "<strong>GSTIN:</strong> " + inv.Gstin + "<br>" : "") +
                  (inv.PlaceOfSupply ? "<strong>Place of Supply:</strong> " + inv.PlaceOfSupply + "<br>" : "") +
                  "<strong>Booking Ref:</strong> " + b.BookingRef + "<br>" +
                  "<strong>Date:</strong> " + new Date(inv.CreatedAt).toLocaleDateString() + "<br>"
                : "<p><strong>Booking Ref:</strong> " + b.BookingRef + " (receipt — invoice issues once payment is confirmed)<br>" +
                  "<strong>Date:</strong> " + new Date(b.CreatedAt).toLocaleDateString() + "<br>";
            var html = "<html><head><title>Invoice " + b.BookingRef + "</title>" +
                "<style>body{font-family:Arial,sans-serif;padding:30px;color:#222}h1{color:#632c76;font-size:20px}table{width:100%;border-collapse:collapse;margin-top:16px}td,th{padding:8px;border-bottom:1px solid #eee}.tot{font-weight:700;font-size:16px;color:#632c76}</style>" +
                "</head><body>" +
                "<h1>PATHLAB Diagnostics — Invoice</h1>" +
                header +
                "<strong>Payment Method:</strong> " + (b.PaymentMethod || "-") + "<br>" +
                "<strong>Payment Status:</strong> " + (b.PaymentStatus || "-") + "</p>" +
                "<table><thead><tr><th style='text-align:left'>Test</th><th style='text-align:right'>Price</th></tr></thead><tbody>" + rows +
                "<tr><td>Subtotal</td><td style='text-align:right'>&#8377;" + b.Subtotal + "</td></tr>" +
                "<tr><td>" + gstLabel + "</td><td style='text-align:right'>&#8377;" + b.GstAmount + "</td></tr>" +
                "<tr class='tot'><td>Total</td><td style='text-align:right'>&#8377;" + b.TotalAmount + "</td></tr>" +
                "</tbody></table></body></html>";
            var win = $window.open("", "_blank");
            win.document.write(html);
            win.document.close();
            win.focus();
            win.print();
        }
        $scope.downloadInvoice = function (b) {
            PathlabService.getInvoiceByBookingRef(b.BookingRef).then(function (r) {
                printInvoice(b, r.data);
            }, function () {
                printInvoice(b, null);
            });
        };
        // Opens Razorpay for a given amount against a booking (full or partial),
        // falling back to a simulated gateway callback when no live key is
        // configured — shared by payNow (full amount) and payPartial below.
        function openRazorpayPayment(b, amount, description, onGatewaySuccess) {
            var key = ($window.SD_CONFIG && $window.SD_CONFIG.RAZORPAY_KEY_ID) || "";
            if (!key || key.indexOf("YOUR_") !== -1) {
                onGatewaySuccess("DEMO-" + Date.now());
                return;
            }
            var open = function () {
                new Razorpay({
                    key: key,
                    amount: Math.round(amount * 100), // paise
                    currency: "INR",
                    name: "PATHLAB Diagnostics",
                    description: description,
                    prefill: { name: $scope.currentUser.FullName, contact: $scope.currentUser.Phone, email: $scope.currentUser.Email || "" },
                    theme: { color: "#632c76" },
                    handler: function (resp) { $scope.$apply(function () { onGatewaySuccess(resp.razorpay_payment_id); }); }
                }).open();
            };
            if ($window.Razorpay) { open(); return; }
            var s = document.createElement("script");
            s.src = "https://checkout.razorpay.com/v1/checkout.js";
            s.onload = open;
            s.onerror = function () { $scope.$apply(function () { showToast("Payment gateway failed to load. Please try again.", "error"); }); };
            document.body.appendChild(s);
        }
        // Settles an existing Pending booking in place (gateway → UpdatePaymentStatus)
        // rather than routing back through Checkout, which would re-create the booking.
        $scope.payNow = function (b) {
            openRazorpayPayment(b, b.TotalAmount - (b.AmountPaid || 0), "Booking " + b.BookingRef, function (txnId) {
                settlePayment(b, txnId, null);
            });
        };
        // Part-payment installment paid online by the patient (Billing Phase 3,
        // patient-facing counterpart to the front-desk "Part" control in Admin).
        $scope.payPartial = function (b) {
            var remaining = b.TotalAmount - (b.AmountPaid || 0);
            var amount = Number(b._partialAmount);
            if (!amount || amount <= 0 || amount >= remaining) {
                showToast("Enter an amount less than the remaining balance (Rs." + remaining + ")", "error");
                return;
            }
            openRazorpayPayment(b, amount, "Booking " + b.BookingRef + " (partial)", function (txnId) {
                settlePayment(b, txnId, amount);
                b._partialAmount = "";
            });
        };
        function settlePayment(b, txnId, partialAmount) {
            PathlabService.updatePaymentStatus(b.BookingRef, partialAmount ? "PartiallyPaid" : "Paid", "online", txnId, "Portal", partialAmount || null).then(function (r) {
                if (r.data && r.data.Success) {
                    b.PaymentStatus = r.data.PaymentStatus || (partialAmount ? "PartiallyPaid" : "Paid");
                    b.AmountPaid = r.data.AmountPaid;
                    showToast("Payment recorded for " + b.BookingRef, "success");
                } else {
                    showToast((r.data && r.data.Message) || "Payment update failed", "error");
                }
            }, function () {
                showToast("Unable to reach service. Please try again.", "error");
            });
        }

        // ── Patient-initiated refund requests (Billing Phase 3) ──
        // RefundPayment is a bookkeeping call that immediately marks a booking
        // "Refunded" and SMSes the patient — it assumes the money has already
        // moved via the gateway/counter. A patient can't trigger that directly
        // (there's no real reversal behind it), so this only records a request
        // for staff to action via the existing Admin > Bookings refund control;
        // it never flips PaymentStatus itself.
        var REFUND_REQ_KEY = "sd_refund_requests";
        function refundRequests() { return JSON.parse(localStorage.getItem(REFUND_REQ_KEY) || "[]"); }
        $scope.isRefundRequested = function (b) { return refundRequests().indexOf(b.BookingRef) !== -1; };
        $scope.requestRefund = function (b) {
            if ($scope.isRefundRequested(b)) return;
            var max = b.AmountPaid || b.TotalAmount;
            var reason = $window.prompt("Why are you requesting a refund for " + b.BookingRef + "? (optional)", "") || "";
            if (reason === null) return;
            PathlabService.logClientEvent($scope.currentUser.Phone, $scope.currentUser.PatientId,
                "RefundRequested", "Booking", b.BookingRef,
                "Patient requested a refund (paid Rs." + max + "). Reason: " + (reason || "not given")).then(function () {
                var reqs = refundRequests();
                reqs.push(b.BookingRef);
                localStorage.setItem(REFUND_REQ_KEY, JSON.stringify(reqs));
                showToast("Refund requested — our team will review and process it within 2-3 business days.", "success");
            }, function () {
                showToast("Unable to reach service. Please try again.", "error");
            });
        };

        // ── Notifications ──
        $scope.unreadCount = 0;
        $scope.notifications = [];
        function isTomorrow(dateStr) {
            if (!dateStr) return false;
            var d = new Date(dateStr), t = new Date();
            t.setDate(t.getDate() + 1);
            return d.getFullYear() === t.getFullYear() && d.getMonth() === t.getMonth() && d.getDate() === t.getDate();
        }

        // Day-before appointment reminder SMS is now sent server-side (WcfPathlabService
        // ReminderJob, hourly timer in Global.asax) so it fires even if the patient never
        // opens the portal. This just mirrors it as an in-portal notice — no SMS from here.

        // Unified feed from the LIMS NotificationLogs (GetNotificationsByPatient):
        // every SMS the lab actually sent — booking, collection, payment, report,
        // reminder — mirrored in-portal. Empty when WCF is unreachable, in which
        // case buildNotifications falls back to synthesizing from bookings/reports.
        var serverNotifs = [];

        // ── Browser push notifications (Notifications Phase 3) ──
        // Honest about scope: this fires a native OS notification for genuinely
        // new events while the portal tab is open (polling every 60s), using the
        // browser Notification API. It is NOT push-when-the-tab-is-closed — that
        // needs a service worker + VAPID keys + a push-subscription backend,
        // which isn't built. Until then this is the real, working half.
        $scope.pushSupported = "Notification" in $window;
        $scope.pushPermission = $scope.pushSupported ? Notification.permission : "unsupported";
        $scope.enablePushNotifications = function () {
            if (!$scope.pushSupported) return;
            Notification.requestPermission().then(function (perm) {
                $scope.$apply(function () { $scope.pushPermission = perm; });
            });
        };

        // ── Notification preferences (Notifications Phase 4) ──
        // Opt-out model: an absent server row means "enabled", matching how the
        // server treats it — so a patient who never opens this panel keeps
        // getting every notification exactly as before this existed.
        $scope.notifPrefs = [];
        var NOTIF_TYPE_LABELS = {
            BookingConfirmation: "Booking confirmed", StatusUpdate: "Sample status updates",
            ReportReady: "Report ready", PaymentReceived: "Payment received", Reminder: "Appointment reminders"
        };
        function loadNotifPreferences() {
            if (!$scope.currentUser) return;
            PathlabService.getNotificationPreferences($scope.currentUser.PatientId).then(function (r) {
                $scope.notifPrefs = (r.data || []).map(function (p) {
                    p.label = NOTIF_TYPE_LABELS[p.Type] || p.Type;
                    return p;
                });
            });
        }
        $scope.toggleNotifPreference = function (pref) {
            PathlabService.updateNotificationPreference($scope.currentUser.PatientId, pref.Channel, pref.Type, pref.Enabled)
                .then(function () { showToast((pref.Enabled ? "Enabled" : "Disabled") + " " + pref.label + " via " + pref.Channel, "success"); },
                      function () { pref.Enabled = !pref.Enabled; showToast("Could not save preference", "error"); });
        };

        var seenNotifIds = JSON.parse(sessionStorage.getItem("sd_seen_notifs") || "[]");
        var pushTimer = null;
        function applyServerNotifs(data) {
            var isFirstLoad = serverNotifs.length === 0 && seenNotifIds.length === 0;
            serverNotifs = data || [];
            buildNotifications();

            if ($scope.pushSupported && Notification.permission === "granted") {
                serverNotifs.forEach(function (n) {
                    if (seenNotifIds.indexOf(n.NotificationLogId) !== -1) return;
                    seenNotifIds.push(n.NotificationLogId);
                    if (!isFirstLoad) {
                        var style = NOTIF_STYLES[n.Type] || { title: "PATHLAB Diagnostics" };
                        new Notification(style.title, { body: n.Message, icon: "/favicon.ico" });
                    }
                });
                sessionStorage.setItem("sd_seen_notifs", JSON.stringify(seenNotifIds.slice(-200)));
            }
        }
        function startPushPolling() {
            if (pushTimer) return;
            pushTimer = $interval(function () {
                PathlabService.getNotificationsByPatient($scope.currentUser.PatientId).then(function (r) {
                    applyServerNotifs(r.data || []);
                });
            }, 60000);
        }
        $scope.$on("$destroy", function () { if (pushTimer) $interval.cancel(pushTimer); });

        // tab = portal tab a click on the notification navigates to
        var NOTIF_STYLES = {
            BookingConfirmation: { icon: "fa-calendar-check-o", color: "#1565c0", title: "Booking Confirmed",     tab: "bookings" },
            StatusUpdate:        { icon: "fa-flask",            color: "#6a1b9a", title: "Sample Status Update",  tab: "track" },
            ReportReady:         { icon: "fa-file-text-o",      color: "#2e7d32", title: "Report Ready",          tab: "reports" },
            PaymentReceived:     { icon: "fa-credit-card",      color: "#00695c", title: "Payment Received",      tab: "bills" },
            Cancellation:        { icon: "fa-times-circle",     color: "#c62828", title: "Booking Cancelled",     tab: "bookings" },
            Reminder:            { icon: "fa-bell",             color: "#e67e22", title: "Appointment Reminder",  tab: "track" }
        };

        function buildNotifications() {
            var notifs = [];
            if (serverNotifs.length > 0) {
                var twoDaysAgo = Date.now() - 48 * 3600 * 1000;
                serverNotifs.forEach(function (n) {
                    var style = NOTIF_STYLES[n.Type] || { icon: "fa-bell-o", color: "#555", title: n.Type || "Notification", tab: "notifications" };
                    notifs.push({
                        icon: style.icon, color: style.color, title: style.title, tab: style.tab,
                        body: n.Message, time: n.CreatedAt,
                        read: new Date(n.CreatedAt).getTime() < twoDaysAgo
                    });
                });
            } else {
                $scope.bookings.forEach(function (b) {
                    notifs.push({ icon: "fa-calendar-check-o", color: "#1565c0", title: "Booking Confirmed", body: "Your booking " + b.BookingRef + " is confirmed.", time: b.CreatedAt, read: true, tab: "bookings" });
                    if (b.BookingStatus === "Ready") {
                        notifs.push({ icon: "fa-file-text-o", color: "#2e7d32", title: "Report Ready", body: "Your report for " + b.BookingRef + " is ready to download.", time: b.CreatedAt, read: false, tab: "reports" });
                    }
                });
                $scope.reports.forEach(function (r) {
                    if (r.Status === "Ready") {
                        notifs.push({ icon: "fa-download", color: "#00695c", title: "Report Available", body: "Report for " + r.BookingRef + " can now be downloaded.", time: r.ReportDate, read: false, tab: "reports" });
                    }
                });
            }
            // Forward-looking reminder is synthesized either way — the server log
            // only has it once ReminderJob's hourly timer has fired.
            $scope.bookings.forEach(function (b) {
                if (b.BookingStatus !== "Cancelled" && isTomorrow(b.CollectionDate)) {
                    notifs.push({ icon: "fa-bell", color: "#e67e22", title: "Appointment Reminder", body: "Your sample collection for " + b.BookingRef + " is tomorrow at " + (b.TimeSlot || "your selected slot") + ".", time: new Date().toISOString(), read: false, tab: "track" });
                }
            });
            notifs.sort(function (a, b) { return new Date(b.time) - new Date(a.time); });
            $scope.notifications = notifs;
            $scope.unreadCount = notifs.filter(function (n) { return !n.read; }).length;
        }

        $scope.markAllRead = function () {
            $scope.notifications.forEach(function (n) { n.read = true; });
            $scope.unreadCount = 0;
        };

        // Clicking a notification marks it read and jumps to the relevant tab
        $scope.openNotification = function (n) {
            if (!n.read) {
                n.read = true;
                $scope.unreadCount = $scope.unreadCount > 0 ? $scope.unreadCount - 1 : 0;
            }
            if (n.tab) $scope.setTab(n.tab);
        };

        // ── Quick Re-book: last 3 distinct tests across past bookings ──
        $scope.recentTests = [];
        $scope.$watch("bookings", function (v) {
            if (!v) return;
            var seen = {}, out = [];
            v.forEach(function (b) {
                (b.Tests || []).forEach(function (t) {
                    var key = t.TestSuiteID || t.TestSuiteName;
                    if (out.length < 3 && !seen[key]) { seen[key] = true; out.push(t); }
                });
            });
            $scope.recentTests = out;
        });

        $scope.quickRebook = function (t) {
            var entry = {
                TestSuiteID: t.TestSuiteID, TestSuiteName: t.TestSuiteName,
                Price: t.Price, SampleType: t.SampleType, TestCount: t.TestCount
            };
            ["cart", "sd_cart"].forEach(function (key) {
                var cart = JSON.parse(sessionStorage.getItem(key) || "[]");
                if (!cart.some(function (c) { return c.TestSuiteID === entry.TestSuiteID; })) cart.push(entry);
                sessionStorage.setItem(key, JSON.stringify(cart));
            });
            $window.location.href = APP_ROOT + "Booking/Cart";
        };

        // ── Health Records (lab reports + patient-uploaded documents) ──
        $scope.allRecords = [];
        $scope.uploadedRecords = [];
        function uploadKey() { return "sd_uploads_" + ($scope.currentUser ? $scope.currentUser.PatientId : "guest"); }
        function loadUploadedRecords() {
            $scope.uploadedRecords = JSON.parse(localStorage.getItem(uploadKey()) || "[]");
        }
        function rebuildAllRecords() {
            $scope.allRecords = ($scope.reports || []).concat($scope.uploadedRecords);
        }
        if ($scope.currentUser) loadUploadedRecords();
        $scope.$watch("reports", function (v) {
            if (v) { rebuildAllRecords(); buildNotifications(); }
        });

        $scope.uploadDocument = function (file) {
            if (!file) return;
            var reader = new FileReader();
            reader.onload = function (e) {
                $scope.$apply(function () {
                    $scope.uploadedRecords.unshift({
                        TestNames: file.name,
                        BookingRef: "Self-uploaded",
                        ReportDate: new Date().toISOString(),
                        ReportFilePath: e.target.result,
                        IsUpload: true
                    });
                    localStorage.setItem(uploadKey(), JSON.stringify($scope.uploadedRecords));
                    rebuildAllRecords();
                    showToast("Document uploaded", "success");
                });
            };
            reader.readAsDataURL(file);
        };

        $scope.removeUploadedRecord = function (r) {
            var idx = $scope.uploadedRecords.indexOf(r);
            if (idx > -1) $scope.uploadedRecords.splice(idx, 1);
            localStorage.setItem(uploadKey(), JSON.stringify($scope.uploadedRecords));
            rebuildAllRecords();
        };

        // ── OTP-secured report download ──
        $scope.dlPanel = { visible: false };
        $scope.startReportOtp = function (r, $event) {
            $event.stopPropagation();
            var live = window.SDNotify ? SDNotify.smsLive() : false;
            var code = live ? String(Math.floor(100000 + Math.random() * 900000)) : "123456";
            $scope.dlPanel = { visible: true, ref: r.BookingRef, path: r.ReportFilePath, code: code, entered: "", error: "", live: live };
            $scope.sharePanel.visible = false;
            if (window.SDNotify && $scope.currentUser) {
                SDNotify.sendSms($scope.currentUser.Phone, "Your OTP to download report " + r.BookingRef + " is " + code + ". Do not share it. - PATHLAB Diagnostics");
            }
        };
        $scope.verifyReportOtp = function () {
            if (($scope.dlPanel.entered || "").trim() === $scope.dlPanel.code) {
                var path = $scope.dlPanel.path, ref = $scope.dlPanel.ref;
                $scope.dlPanel = { visible: false };
                if ($scope.currentUser) {
                    PathlabService.logClientEvent($scope.currentUser.Phone, $scope.currentUser.PatientId,
                        "ReportDownload", "Report", ref, "OTP-verified report download.");
                }
                $window.open(path, "_blank");
            } else {
                $scope.dlPanel.error = "Incorrect OTP. Please try again.";
            }
        };

        // ── Reports Share ──
        // Share links are built from a random token (GetOrCreateShareToken), not
        // the plain BookingRef — a sequential booking number is guessable, and
        // this link is meant to be safely forwardable to a doctor over
        // WhatsApp/email without exposing any other patient's report.
        $scope.sharePanel = { visible: false };
        $scope.shareReport = function (r, $event) {
            $event.stopPropagation();
            $scope.sharePanel = { visible: true, ref: r.BookingRef, link: "", waText: "", loading: true };
            var patientId = $scope.currentUser ? $scope.currentUser.PatientId : 0;
            PathlabService.getOrCreateShareToken(r.BookingRef, patientId).then(function (resp) {
                $scope.sharePanel.loading = false;
                if (resp.data && resp.data.Success) {
                    var link = window.location.origin + APP_ROOT + "Patient/ViewReport?token=" + resp.data.Token;
                    $scope.sharePanel.link = link;
                    $scope.sharePanel.waText = encodeURIComponent("View my lab report: " + link);
                } else {
                    showToast((resp.data && resp.data.Message) || "Could not create a share link", "error");
                    $scope.sharePanel.visible = false;
                }
            }, function () {
                $scope.sharePanel.loading = false;
                $scope.sharePanel.visible = false;
                showToast("Service error. Try again.", "error");
            });
            if ($scope.currentUser) {
                PathlabService.logClientEvent($scope.currentUser.Phone, $scope.currentUser.PatientId,
                    "ReportShare", "Report", r.BookingRef, "Share link generated.");
            }
        };
        $scope.copyShareLink = function () {
            try { navigator.clipboard.writeText($scope.sharePanel.link); showToast("Link copied!", "success"); }
            catch (e) { showToast("Copy the link manually", ""); }
        };

        // Doctor sharing (Report Delivery Phase 3) — honest about the SMTP
        // dependency: shows whatever message the server actually returns rather
        // than always claiming success.
        $scope.doctorShare = { name: "", email: "" };
        $scope.shareWithDoctor = function () {
            if (!$scope.doctorShare.email) { showToast("Enter the doctor's email", "error"); return; }
            PathlabService.shareReportWithDoctor($scope.sharePanel.ref, $scope.doctorShare.name, $scope.doctorShare.email)
                .then(function (r) {
                    showToast((r.data && r.data.Message) || "Share failed", r.data && r.data.Success ? "success" : "error");
                    if (r.data && r.data.Success) $scope.doctorShare = { name: "", email: "" };
                }, function () { showToast("Service error. Try again.", "error"); });
        };

        // ── Help FAQ ──
        $scope.helpItems = [
            { q: "How do I prepare for fasting tests?", a: "Do not eat or drink anything except water for 8–12 hours before the test. Take your medicines as prescribed unless told otherwise." },
            { q: "How long does it take to get my report?", a: "Most reports are ready within 24–48 hours. Some specialized tests may take longer. You will receive an SMS when your report is ready." },
            { q: "Can I book a home collection?", a: "Yes! Select 'Home Collection' during checkout. A phlebotomist will visit at your chosen time slot." },
            { q: "How do I download my report?", a: "Login to your portal → My Reports → click Download next to the report." },
            { q: "How do I track my sample?", a: "Go to Track Sample in the portal or enter your Booking ID on the Track My Sample page." },
            { q: "Which branches are available?", a: "See the branch list below — call ahead to confirm timings." }
        ];

        // ── Branch Locator (Help section fix) — same real branch list used at
        // checkout (ChooseCollection), so this can never drift out of sync with
        // what's actually bookable. Replaces linking to the legacy
        // CentreLocator.cshtml page, which shows fake generic addresses.
        $scope.branches = [
            { id: "BR001", name: "Bhopal – Main Branch, MP Nagar", phone: "8269331264" },
            { id: "BR002", name: "Bhopal – Kolar Road Centre", phone: "8269331264" },
            { id: "BR003", name: "Bhopal – Arera Colony", phone: "8269331264" },
            { id: "BR004", name: "Indore – Vijay Nagar", phone: "8269331264" },
            { id: "BR005", name: "Indore – Palasia Square", phone: "8269331264" }
        ];

        // ── Feedback / Complaints (Help section fix) — real, self-hosted
        // submission instead of linking to the legacy Feedback.cshtml page,
        // whose form actually posts to a third party's ("lupindiagnostics.com") API.
        $scope.feedback = { message: "" };
        $scope.feedbackSent = false;
        $scope.submitFeedback = function () {
            if (!$scope.feedback.message || !$scope.feedback.message.trim()) {
                showToast("Please enter a message", "error"); return;
            }
            PathlabService.submitFeedback({
                PatientId: $scope.currentUser ? $scope.currentUser.PatientId : 0,
                Name: $scope.currentUser ? $scope.currentUser.FullName : "",
                Phone: $scope.currentUser ? $scope.currentUser.Phone : "",
                Message: $scope.feedback.message
            }).then(function (r) {
                if (r.data && r.data.Success) {
                    $scope.feedbackSent = true;
                    $scope.feedback = { message: "" };
                    showToast(r.data.ResponseMessage || "Thanks for your feedback!", "success");
                } else {
                    showToast((r.data && r.data.ResponseMessage) || "Could not submit — please try again", "error");
                }
            }, function () { showToast("Service error. Try again.", "error"); });
        };
    });

// ======================== PACKAGES CONTROLLER ========================
angular.module("PathlabModule")
    .controller("PackagesController", function ($scope, $window, PathlabService) {
        $scope.packages = [];
        $scope.isLoading = true;

        PathlabService.getAllPackages().then(function (r) {
            $scope.packages = r.data || [];
            $scope.isLoading = false;
        }, function () {
            $scope.isLoading = false;
        });

        $scope.bookPackage = function (pkg) {
            var item = {
                TestId: "PKG_" + pkg.PackageId,
                TestSuiteID: "PKG_" + pkg.PackageId,
                TestSuiteName: pkg.PackageName,
                Price: pkg.Price,
                SampleType: "Multiple",
                TestCount: pkg.TestCount
            };
            var cart = JSON.parse(sessionStorage.getItem("sd_cart") || "[]");
            if (!cart.some(function (t) { return (t.TestSuiteID || t.TestId) === item.TestSuiteID; })) cart.push(item);
            sessionStorage.setItem("sd_cart", JSON.stringify(cart));
            $window.location.href = APP_ROOT + "Booking/Cart";
        };
    });

// ======================== PACKAGE DETAIL CONTROLLER ========================
angular.module("PathlabModule")
    .controller("PackageDetailController", function ($scope, $window, $element, PathlabService) {
        $scope.pkg = null;
        $scope.includesList = [];
        $scope.isLoading = true;

        var packageId = $element.attr("data-package-id");
        PathlabService.getPackageById(packageId).then(function (r) {
            $scope.pkg = r.data;
            $scope.includesList = $scope.pkg && $scope.pkg.Includes ? $scope.pkg.Includes.split(",").map(function (s) { return s.trim(); }) : [];
            $scope.isLoading = false;
        }, function () {
            $scope.isLoading = false;
        });

        $scope.bookThisPackage = function () {
            var item = {
                TestId: "PKG_" + $scope.pkg.PackageId,
                TestSuiteID: "PKG_" + $scope.pkg.PackageId,
                TestSuiteName: $scope.pkg.PackageName,
                Price: $scope.pkg.Price,
                SampleType: "Multiple",
                TestCount: $scope.pkg.TestCount
            };
            var cart = JSON.parse(sessionStorage.getItem("sd_cart") || "[]");
            if (!cart.some(function (t) { return (t.TestSuiteID || t.TestId) === item.TestSuiteID; })) cart.push(item);
            sessionStorage.setItem("sd_cart", JSON.stringify(cart));
            $window.location.href = APP_ROOT + "Booking/Cart";
        };
    });

// ======================== TRACK SAMPLE CONTROLLER ========================
angular.module("PathlabModule")
    .controller("TrackSampleController", function ($scope, $interval, PathlabService) {
        $scope.refInput = "";
        $scope.booking = null;
        $scope.timeline = null;
        $scope.custody = null;
        $scope.barcodeBars = [];
        $scope.isLoading = false;
        $scope.errorMsg = "";
        $scope.lastRefreshed = null;
        $scope.phleb = function (b) { return window.SDNotify ? SDNotify.phlebotomist(b.BookingRef) : { name: "", phone: "" }; };

        var refreshTimer = null;
        var REFRESH_MS = 30000;

        // Deterministic pseudo-barcode stripes from the barcode digits (display
        // only — the scannable label is printed by the LIMS at accessioning).
        function makeBars(code) {
            var bars = [];
            String(code || "").split("").forEach(function (ch) {
                var d = ch.charCodeAt(0) % 10;
                bars.push({ w: 1 + (d % 3), gap: 1 + (d % 2) });
                bars.push({ w: 1 + ((d + 1) % 2), gap: 1 });
            });
            return bars;
        }

        function loadStatus(ref, silent) {
            if (!silent) { $scope.isLoading = true; $scope.booking = null; $scope.timeline = null; }
            $scope.errorMsg = "";
            PathlabService.getBookingByRef(ref).then(function (r) {
                $scope.isLoading = false;
                if (r.data && r.data.Success) {
                    $scope.booking = r.data;
                    $scope.stepIndex = r.data.SampleStatus || 0;
                    $scope.lastRefreshed = new Date();
                    startAutoRefresh(ref);
                } else if (!silent) {
                    $scope.errorMsg = "Booking reference not found. Please check and try again.";
                    stopAutoRefresh();
                }
            }, function () {
                $scope.isLoading = false;
                if (!silent) {
                    $scope.errorMsg = "Unable to connect to the service. Please try again.";
                    stopAutoRefresh();
                }
            });
            PathlabService.getSampleTimeline(ref).then(function (r) {
                if (r.data && r.data.Success) {
                    $scope.timeline = r.data;
                    $scope.barcodeBars = makeBars(r.data.Barcode);
                }
            }, function () { /* timeline is optional — booking card still renders */ });
            // Chain of custody (Sample Collection Phase 4) — finer-grained than the
            // 5-stage timeline above; only shown when staff have actually logged handoffs.
            PathlabService.getChainOfCustody(ref).then(function (r) {
                $scope.custody = (r.data && r.data.Success) ? r.data : null;
            }, function () { $scope.custody = null; });
        }

        function startAutoRefresh(ref) {
            if (refreshTimer) return;
            refreshTimer = $interval(function () { loadStatus(ref, true); }, REFRESH_MS);
        }
        function stopAutoRefresh() {
            if (refreshTimer) { $interval.cancel(refreshTimer); refreshTimer = null; }
        }
        $scope.$on("$destroy", stopAutoRefresh);

        $scope.trackSample = function () {
            var ref = ($scope.refInput || "").trim();
            if (!ref) { showToast("Please enter a booking reference", "error"); return; }
            stopAutoRefresh();
            loadStatus(ref, false);
        };

        $scope.getStepClass = function (idx) {
            if (idx < $scope.stepIndex) return "done";
            if (idx === $scope.stepIndex) return "active";
            return "";
        };
    });

// ======================== VIEW REPORT (doctor share link) CONTROLLER ========================
// Public, no-login page opened from the "Share with doctor" link built by
// shareReport() above (/Patient/ViewReport?token=...). Looked up by the random
// share token (GetBookingByShareToken), not the plain BookingRef — a sequential
// booking number is guessable, and this page is meant to be safely forwardable.
// Report status is read off SampleStatus (index 4 = "Report Ready", same
// convention as TrackSampleController's stepIndex) since the lookup doesn't
// carry the Report row — the file path mirrors the placeholder convention
// already used server-side in UpdateSampleStatus.
angular.module("PathlabModule")
    .controller("ViewReportController", function ($scope, $sce, PathlabService) {
        $scope.isLoading = true;
        $scope.errorMsg = "";
        $scope.booking = null;
        $scope.isReady = false;
        $scope.reportUrl = null;

        var tokenField = document.getElementById("viewReportToken");
        var token = tokenField ? (tokenField.value || "").trim() : "";

        if (!token) {
            $scope.isLoading = false;
            $scope.errorMsg = "No report reference was provided in this link.";
        } else {
            PathlabService.getBookingByShareToken(token).then(function (r) {
                $scope.isLoading = false;
                if (r.data && r.data.Success) {
                    $scope.booking = r.data;
                    $scope.isReady = (r.data.SampleStatus || 0) >= 4;
                    if ($scope.isReady) {
                        $scope.reportUrl = $sce.trustAsResourceUrl(APP_ROOT + "Reports/" + r.data.BookingRef + ".pdf");
                    }
                    // Best-effort access log — never blocks the view if it fails.
                    PathlabService.logClientEvent("", null, "ReportViewedByDoctor", "Report", r.data.BookingRef, "Shared report link opened.")
                        .then(function () {}, function () {});
                } else {
                    $scope.errorMsg = "This report link is invalid or has expired.";
                }
            }, function () {
                $scope.isLoading = false;
                $scope.errorMsg = "Unable to connect to the service. Please try again.";
            });
        }
    });

// ======================== ADMIN CONTROLLER ========================
angular.module("PathlabModule")
    .controller("AdminController", function ($scope, $window, PathlabService) {
        $scope.activeTab = "overview";
        $scope.stats = null;
        $scope.recentBookings = [];
        $scope.allBookings = [];
        $scope.allPatients = [];
        $scope.isLoadingBookings = false;
        $scope.isLoadingPatients = false;
        $scope.bookingFilter = "";
        $scope.patientFilter = "";

        $scope.staffAlerts = [];
        $scope.notificationLogs = [];
        $scope.auditLogs = [];
        $scope.auditChain = null;
        $scope.limsSync = null;
        $scope.isSyncingCatalogue = false;

        $scope.setTab = function (tab) {
            $scope.activeTab = tab;
            if (tab === "bookings" && $scope.allBookings.length === 0) loadAllBookings();
            if (tab === "patients" && $scope.allPatients.length === 0) loadAllPatients();
            if (tab === "notifications") loadNotificationLogs();
            if (tab === "audit") { loadAuditLogs(); loadComplianceReport(); }
            if (tab === "lims") loadLimsSyncStatus();
            if (tab === "queue") loadCollectionQueue();
            if (tab === "analytics" && !$scope.analytics) loadAnalytics();
            if (tab === "reconciliation") loadReconciliation();
            if (tab === "feedback") loadFeedback();
            if (tab === "leads") loadLeads();
        };

        // ── Feedback / Complaints (Help section fix) ──
        $scope.allFeedback = [];
        $scope.isLoadingFeedback = false;
        function loadFeedback() {
            $scope.isLoadingFeedback = true;
            PathlabService.getAllFeedback().then(function (r) {
                $scope.allFeedback = r.data || [];
                $scope.isLoadingFeedback = false;
            }, function () { $scope.isLoadingFeedback = false; });
        }

        // ── Home Collection Leads (Home Collection popup fix) ──
        $scope.allLeads = [];
        $scope.isLoadingLeads = false;
        function loadLeads() {
            $scope.isLoadingLeads = true;
            PathlabService.getHomeCollectionLeads().then(function (r) {
                $scope.allLeads = r.data || [];
                $scope.isLoadingLeads = false;
            }, function () { $scope.isLoadingLeads = false; });
        }

        // ── Collection Queue (Sample Collection Phase 3 — phlebotomist view) ──
        $scope.collectionQueue = [];
        $scope.isLoadingQueue = false;
        function loadCollectionQueue() {
            $scope.isLoadingQueue = true;
            PathlabService.getCollectionQueue().then(function (r) {
                $scope.collectionQueue = r.data || [];
                $scope.isLoadingQueue = false;
            }, function () { $scope.isLoadingQueue = false; });
        }
        // Also logs a chain-of-custody handoff (Sample Collection Phase 4) —
        // the finer-grained record an accreditation audit asks for, alongside
        // the coarse 5-stage status the patient sees.
        $scope.markCollected = function (item) {
            PathlabService.updateSampleStatus(item.BookingRef, "1").then(function (r) {
                if (r.data && r.data.Success) {
                    $scope.collectionQueue = $scope.collectionQueue.filter(function (q) { return q.BookingRef !== item.BookingRef; });
                    PathlabService.logCustodyEvent(item.BookingRef, item._handlerName || "Phlebotomist", "Phlebotomist", "Collected", item.Address, null);
                    showToast("Marked collected: " + item.BookingRef, "success");
                } else {
                    showToast((r.data && r.data.Message) || "Update failed", "error");
                }
            });
        };

        // ── Reconciliation (Billing Phase 4) ──
        $scope.reconciliation = null;
        $scope.reconFrom = "";
        $scope.reconTo = "";
        function loadReconciliation() {
            PathlabService.getReconciliationSummary($scope.reconFrom, $scope.reconTo).then(function (r) {
                $scope.reconciliation = r.data;
            });
        }
        $scope.applyReconFilter = function () { loadReconciliation(); };
        $scope.exportReconciliationCsv = function () {
            if (!$scope.reconciliation) return;
            downloadCsv("reconciliation_" + $scope.reconciliation.FromDate + "_to_" + $scope.reconciliation.ToDate + ".csv",
                toCsv($scope.reconciliation.Rows, ["BookingRef", "CreatedAt", "PatientName", "TotalAmount", "AmountPaid", "PaymentStatus", "PaymentRef", "RefundAmount", "InvoiceNumber", "LimsSyncStatus"]));
        };

        // ── Compliance report (Audit Trail Phase 4 — NABH/NABL supporting doc) ──
        $scope.compliance = null;
        function loadComplianceReport() {
            PathlabService.getComplianceReport().then(function (r) { $scope.compliance = r.data; });
        }

        // ── Analytics (TAT / revenue trend / test volume — Phase 3) ──
        $scope.analytics = null;
        $scope.analyticsFrom = "";
        $scope.analyticsTo = "";
        function loadAnalytics() {
            PathlabService.getAnalyticsSummary($scope.analyticsFrom, $scope.analyticsTo).then(function (r) {
                $scope.analytics = r.data;
                var max = 1;
                (r.data.DailyTrend || []).forEach(function (d) { if (d.Revenue > max) max = d.Revenue; });
                (r.data.DailyTrend || []).forEach(function (d) { d.barPct = Math.round((d.Revenue / max) * 100); });
            });
        }
        $scope.applyAnalyticsFilter = function () { loadAnalytics(); };

        function toCsv(rows, headers) {
            var lines = [headers.join(",")];
            rows.forEach(function (r) {
                lines.push(headers.map(function (h) {
                    var v = r[h] === undefined || r[h] === null ? "" : String(r[h]);
                    return '"' + v.replace(/"/g, '""') + '"';
                }).join(","));
            });
            return lines.join("\r\n");
        }
        function downloadCsv(filename, csv) {
            var blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
            var link = document.createElement("a");
            link.href = URL.createObjectURL(blob);
            link.download = filename;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
        }
        $scope.exportAnalyticsCsv = function () {
            if (!$scope.analytics) return;
            downloadCsv("analytics_" + $scope.analytics.FromDate + "_to_" + $scope.analytics.ToDate + ".csv",
                toCsv($scope.analytics.DailyTrend, ["Date", "Bookings", "Revenue"]));
        };

        // ── Compliance export (Audit Trail Phase 3) ──
        $scope.exportAuditCsv = function () {
            downloadCsv("audit_trail_export.csv",
                toCsv($scope.auditLogs, ["CreatedAt", "Actor", "Action", "EntityType", "EntityRef", "Detail", "IPAddress", "Success"]));
        };

        // Practical Website+LIMS "combined KPI" view — see LimsSyncStatusDC on the
        // server for why this exists instead of a true cross-system BI merge.
        function loadLimsSyncStatus() {
            PathlabService.getLimsSyncStatus().then(function (r) {
                $scope.limsSync = r.data;
            });
        }

        $scope.syncCatalogueNow = function () {
            $scope.isSyncingCatalogue = true;
            PathlabService.syncTestCatalogue().then(function (r) {
                $scope.isSyncingCatalogue = false;
                if (r.data && r.data.Success) {
                    showToast(r.data.Message, "success");
                    loadLimsSyncStatus();
                } else {
                    showToast((r.data && r.data.Message) || "Sync failed", "error");
                }
            }, function () {
                $scope.isSyncingCatalogue = false;
                showToast("Unable to reach the service", "error");
            });
        };

        function loadStaffAlerts() {
            PathlabService.getStaffAlerts().then(function (r) {
                $scope.staffAlerts = r.data || [];
            });
        }

        function loadNotificationLogs() {
            PathlabService.getNotificationLogs().then(function (r) {
                $scope.notificationLogs = r.data || [];
            });
        }

        function loadAuditLogs() {
            PathlabService.getAuditLogs().then(function (r) {
                $scope.auditLogs = r.data || [];
            });
            PathlabService.verifyAuditChain().then(function (r) {
                $scope.auditChain = r.data;
            });
        }

        $scope.topTests = [];
        function buildTopTests(bookings) {
            var counts = {};
            bookings.forEach(function (b) {
                (b.Tests || []).forEach(function (t) {
                    var name = t.TestSuiteName || "Unknown";
                    if (!counts[name]) counts[name] = { name: name, count: 0, revenue: 0 };
                    counts[name].count += 1;
                    counts[name].revenue += (t.Price || 0);
                });
            });
            $scope.topTests = Object.keys(counts).map(function (k) { return counts[k]; })
                .sort(function (a, b) { return b.count - a.count; })
                .slice(0, 5);
        }

        $scope.loadStats = function () {
            PathlabService.getAdminStats().then(function (r) {
                $scope.stats = r.data;
            });
            PathlabService.getAllBookings().then(function (r) {
                $scope.recentBookings = r.data || [];
                buildTopTests($scope.recentBookings);
            });
            loadStaffAlerts();
        };

        function loadAllBookings() {
            $scope.isLoadingBookings = true;
            PathlabService.getAllBookings().then(function (r) {
                $scope.allBookings = r.data || [];
                $scope.isLoadingBookings = false;
            });
        }

        function loadAllPatients() {
            $scope.isLoadingPatients = true;
            PathlabService.getAllPatients().then(function (r) {
                $scope.allPatients = r.data || [];
                $scope.isLoadingPatients = false;
            });
        }

        var statusIndexMap = { "Booked": 0, "Confirmed": 1, "Processing": 2, "Ready": 3 };
        $scope.updateStatus = function (booking) {
            var idx = statusIndexMap[booking.BookingStatus];
            if (idx === undefined) idx = 0;
            PathlabService.updateSampleStatus(booking.BookingRef, String(idx)).then(function () {
                showToast("Status updated for " + booking.BookingRef, "success");
            });
        };

        // Lab-staff ingestion of a finished report (Report Delivery Phase 2) —
        // the only thing that ever unlocks the patient's Download/Share buttons
        // in My Reports, since ReportFilePath otherwise stays null forever.
        $scope.attachReport = function (booking) {
            var path = $window.prompt(
                "Report file path/URL for " + booking.BookingRef + ":",
                "/images/pathlab-brochure.pdf");
            if (!path) return;
            PathlabService.attachReport(booking.BookingRef, path, "Staff").then(function (r) {
                if (r.data && r.data.Success) {
                    booking.BookingStatus = "Ready";
                    showToast("Report attached — patient can now download it", "success");
                } else {
                    showToast((r.data && r.data.Message) || "Could not attach report", "error");
                }
            }, function () { showToast("Unable to reach service. Please try again.", "error"); });
        };

        // Front-desk settlement of a pay-at-counter booking.
        $scope.markPaid = function (booking) {
            PathlabService.updatePaymentStatus(booking.BookingRef, "Paid", booking.PaymentMethod || "counter", null, "FrontDesk").then(function (r) {
                if (r.data && r.data.Success) {
                    booking.PaymentStatus = "Paid";
                    booking.AmountPaid = booking.TotalAmount;
                    showToast("Payment recorded for " + booking.BookingRef, "success");
                } else {
                    showToast((r.data && r.data.Message) || "Update failed", "error");
                }
            });
        };

        // Part-payment installment (Billing Phase 3) — accumulates server-side;
        // status becomes Paid automatically once AmountPaid reaches TotalAmount.
        $scope.recordPartialPayment = function (booking) {
            var amount = Number(booking._partialAmount);
            if (!amount || amount <= 0) { showToast("Enter a valid amount", "error"); return; }
            PathlabService.updatePaymentStatus(booking.BookingRef, "PartiallyPaid", booking.PaymentMethod || "counter", null, "FrontDesk", amount).then(function (r) {
                if (r.data && r.data.Success) {
                    booking.PaymentStatus = r.data.PaymentStatus;
                    booking.AmountPaid = r.data.AmountPaid;
                    booking._partialAmount = "";
                    showToast("Recorded Rs." + amount + " for " + booking.BookingRef, "success");
                } else {
                    showToast((r.data && r.data.Message) || "Update failed", "error");
                }
            });
        };

        // Refund on a Paid/PartiallyPaid booking (Billing Phase 3).
        $scope.refundBooking = function (booking) {
            var max = booking.AmountPaid || booking.TotalAmount;
            var amountStr = $window.prompt("Refund amount for " + booking.BookingRef + " (max Rs." + max + "):", String(max));
            if (amountStr === null) return;
            var amount = Number(amountStr);
            if (!amount || amount <= 0) { showToast("Invalid refund amount", "error"); return; }
            var reason = $window.prompt("Reason for refund (optional):", "") || "";
            PathlabService.refundPayment(booking.BookingRef, amount, reason, "FrontDesk").then(function (r) {
                if (r.data && r.data.Success) {
                    booking.PaymentStatus = "Refunded";
                    booking.RefundAmount = r.data.RefundAmount;
                    showToast("Refunded Rs." + amount + " for " + booking.BookingRef, "success");
                } else {
                    showToast((r.data && r.data.Message) || "Refund failed", "error");
                }
            });
        };

        // initial load
        $scope.loadStats();
        loadLimsSyncStatus();
    });

// ======================== PHLEBOTOMIST CONTROLLER ========================
// Standalone mobile-first collection queue (Sample Collection Phase 3) — the
// same data/actions as Admin > Collection Queue, but on its own PIN-gated
// page (Staff/Collection) so a field phlebotomist never sees the rest of
// the Admin Dashboard.
angular.module("PathlabModule")
    .controller("PhlebotomistController", function ($scope, PathlabService) {
        $scope.collectionQueue = [];
        $scope.isLoadingQueue = false;

        $scope.loadCollectionQueue = function () {
            $scope.isLoadingQueue = true;
            PathlabService.getCollectionQueue().then(function (r) {
                $scope.collectionQueue = r.data || [];
                $scope.isLoadingQueue = false;
            }, function () { $scope.isLoadingQueue = false; });
        };

        $scope.markCollected = function (item) {
            PathlabService.updateSampleStatus(item.BookingRef, "1").then(function (r) {
                if (r.data && r.data.Success) {
                    $scope.collectionQueue = $scope.collectionQueue.filter(function (q) { return q.BookingRef !== item.BookingRef; });
                    PathlabService.logCustodyEvent(item.BookingRef, item._handlerName || "Phlebotomist", "Phlebotomist", "Collected", item.Address, null);
                    showToast("Marked collected: " + item.BookingRef, "success");
                } else {
                    showToast((r.data && r.data.Message) || "Update failed", "error");
                }
            });
        };

        $scope.loadCollectionQueue();
    });

// ======================== SHARED TOAST ========================
function showToast(msg, type) {
    var el = document.getElementById("sdToast");
    if (!el) return;
    el.textContent = msg;
    el.className = "sd-toast show" + (type ? " " + type : "");
    setTimeout(function () { el.className = "sd-toast"; }, 3000);
}
