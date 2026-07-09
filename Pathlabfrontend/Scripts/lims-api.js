// LIMS API client — fetches tests/bookings/reports from WCfPathlabService.
// Falls back to offline data if WCF is unreachable (dev/demo mode).
// SWAP: change BASE to your IIS/production URL before go-live.
(function(w){
  var BASE = 'http://localhost:2091/PathlabService.svc';

  // Offline fallback — mirrors the LabTests seed data.
  var FALLBACK_TESTS = [
    {TestSuiteID:'TST001',TestSuiteName:'Complete Blood Count (CBC)',               Price:350,  SampleType:'Blood',        TestCount:24, FastingRequired:false, Category:'Haematology'},
    {TestSuiteID:'TST002',TestSuiteName:'Lipid Profile',                            Price:650,  SampleType:'Blood',        TestCount:8,  FastingRequired:true,  Category:'Biochemistry'},
    {TestSuiteID:'TST003',TestSuiteName:'Liver Function Test (LFT)',                Price:750,  SampleType:'Blood',        TestCount:12, FastingRequired:true,  Category:'Biochemistry'},
    {TestSuiteID:'TST004',TestSuiteName:'Kidney Function Test (KFT)',               Price:700,  SampleType:'Blood',        TestCount:10, FastingRequired:false, Category:'Biochemistry'},
    {TestSuiteID:'TST005',TestSuiteName:'Thyroid Profile (T3, T4, TSH)',            Price:550,  SampleType:'Blood',        TestCount:3,  FastingRequired:false, Category:'Endocrinology'},
    {TestSuiteID:'TST006',TestSuiteName:'HbA1c (Glycated Haemoglobin)',             Price:450,  SampleType:'Blood',        TestCount:1,  FastingRequired:false, Category:'Diabetes'},
    {TestSuiteID:'TST007',TestSuiteName:'Urine Routine & Microscopy',              Price:200,  SampleType:'Urine',        TestCount:18, FastingRequired:false, Category:'Urine'},
    {TestSuiteID:'TST008',TestSuiteName:'Dengue NS1 Antigen',                      Price:600,  SampleType:'Blood',        TestCount:1,  FastingRequired:false, Category:'Infection'},
    {TestSuiteID:'TST009',TestSuiteName:'Vitamin D (25-OH)',                        Price:1200, SampleType:'Blood',        TestCount:1,  FastingRequired:false, Category:'Vitamins'},
    {TestSuiteID:'TST010',TestSuiteName:'Vitamin B12',                             Price:850,  SampleType:'Blood',        TestCount:1,  FastingRequired:false, Category:'Vitamins'},
    {TestSuiteID:'TST011',TestSuiteName:'Iron Studies (Serum Iron, TIBC, Ferritin)',Price:900, SampleType:'Blood',        TestCount:3,  FastingRequired:false, Category:'Haematology'},
    {TestSuiteID:'TST012',TestSuiteName:'Blood Glucose Fasting (FBS)',              Price:150,  SampleType:'Blood',        TestCount:1,  FastingRequired:true,  Category:'Diabetes'},
    {TestSuiteID:'TST013',TestSuiteName:'C-Reactive Protein (CRP)',                Price:350,  SampleType:'Blood',        TestCount:1,  FastingRequired:false, Category:'Infection'},
    {TestSuiteID:'TST014',TestSuiteName:'Allergy Screen Panel (20 Allergens)',      Price:2500, SampleType:'Blood',        TestCount:20, FastingRequired:false, Category:'Allergy'},
    {TestSuiteID:'TST015',TestSuiteName:'Comprehensive Health Package',             Price:3500, SampleType:'Blood, Urine', TestCount:65, FastingRequired:true,  Category:'Packages'},
    {TestSuiteID:'TST016',TestSuiteName:'Blood Glucose Post Prandial (PPBS)',       Price:150,  SampleType:'Blood',        TestCount:1,  FastingRequired:false, Category:'Diabetes'},
    {TestSuiteID:'TST017',TestSuiteName:'Serum Calcium',                           Price:280,  SampleType:'Blood',        TestCount:1,  FastingRequired:false, Category:'Biochemistry'},
    {TestSuiteID:'TST018',TestSuiteName:'PSA (Prostate Specific Antigen)',          Price:800,  SampleType:'Blood',        TestCount:1,  FastingRequired:false, Category:'Oncology'},
    {TestSuiteID:'TST019',TestSuiteName:'Dengue IgG & IgM Antibody',               Price:900,  SampleType:'Blood',        TestCount:2,  FastingRequired:false, Category:'Infection'},
    {TestSuiteID:'TST020',TestSuiteName:'COVID-19 RT-PCR',                         Price:500,  SampleType:'Nasal Swab',   TestCount:1,  FastingRequired:false, Category:'Infection'}
  ];

  // Offline fallback packages — mirrors the HealthPackages seed data.
  var FALLBACK_PACKAGES = [
    {TestSuiteID:'PKG001',TestSuiteName:'Basic Health Checkup',      Price:999,  SampleType:'Blood, Urine', TestCount:15, FastingRequired:true,  Category:'Packages', TestType:'Package', Description:'Essential screening for everyday wellness.'},
    {TestSuiteID:'PKG002',TestSuiteName:'Full Body Checkup',         Price:2499, SampleType:'Blood, Urine', TestCount:65, FastingRequired:true,  Category:'Packages', TestType:'Package', Description:'Comprehensive checkup covering major organ systems.'},
    {TestSuiteID:'PKG003',TestSuiteName:'Diabetes Care Package',     Price:799,  SampleType:'Blood',        TestCount:8,  FastingRequired:true,  Category:'Packages', TestType:'Package', Description:'Focused monitoring for diabetes management.'},
    {TestSuiteID:'PKG004',TestSuiteName:"Women's Wellness Package",  Price:1899, SampleType:'Blood, Urine', TestCount:32, FastingRequired:true,  Category:'Packages', TestType:'Package', Description:'Health screening tailored for women.'},
    {TestSuiteID:'PKG005',TestSuiteName:'Senior Citizen Package',    Price:2999, SampleType:'Blood, Urine', TestCount:48, FastingRequired:true,  Category:'Packages', TestType:'Package', Description:'Extensive checkup for ages 55 and above.'},
    {TestSuiteID:'PKG006',TestSuiteName:'Fever Panel',               Price:1299, SampleType:'Blood',        TestCount:5,  FastingRequired:false, Category:'Packages', TestType:'Package', Description:'Quick screening for common fever-causing infections.'}
  ];

  function apiFetch(path, opts) {
    return fetch(BASE + path, Object.assign({
      headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' }
    }, opts || {})).then(function(r) {
      if (!r.ok) throw new Error('HTTP ' + r.status);
      return r.json();
    });
  }

  // WCF returns Fasting as string ("Yes"/"No"). UI expects FastingRequired boolean.
  function mapTest(t) {
    if (typeof t.FastingRequired === 'undefined') {
      t.FastingRequired = /yes|required/i.test(t.Fasting || '');
    }
    t.Price = Number(t.Price) || 0;
    t.TestCount = Number(t.TestCount) || 1;
    t.TestType = t.TestType || (t.Category === 'Packages' ? 'Package' : 'Test');
    return t;
  }

  w.LIMS = {
    BASE: BASE,

    getTests: function() {
      return apiFetch('/GetAllTests')
        .then(function(arr) { return (arr || []).map(mapTest); })
        .catch(function() {
          // SWAP: remove this catch block once WCF is connected
          console.info('[LIMS] WCF unreachable — running in offline/demo mode. Start WcfPathlabService on port 2091 to enable live data.');
          return FALLBACK_TESTS.slice();
        });
    },

    // Unified catalogue: individual tests + packages, tagged TestType 'Test'/'Package'.
    // Backs the landing-page "Most Booked Tests" cards (name, price, parameter count).
    getCatalogue: function() {
      return apiFetch('/GetTestCatalogue')
        .then(function(arr) { return (arr || []).map(mapTest); })
        .catch(function() {
          console.info('[LIMS] WCF unreachable — catalogue served from offline/demo data.');
          return FALLBACK_TESTS.concat(FALLBACK_PACKAGES).map(mapTest);
        });
    },

    searchTests: function(kw) {
      return apiFetch('/SearchTests/' + encodeURIComponent(kw))
        .then(function(arr) { return (arr || []).map(mapTest); })
        .catch(function() {
          var kl = (kw || '').toLowerCase();
          return FALLBACK_TESTS.filter(function(t) {
            return t.TestSuiteName.toLowerCase().indexOf(kl) !== -1 ||
                   (t.Category || '').toLowerCase().indexOf(kl) !== -1;
          });
        });
    },

    // Slots already at capacity for a date (+ branch for walk-in) — the UI greys
    // these out so patients can't double-book. Offline fallback counts local bookings.
    getBookedSlots: function(date, type, branch) {
      return apiFetch('/GetBookedSlots?date=' + encodeURIComponent(date) +
                      '&type=' + encodeURIComponent(type || '') +
                      '&branch=' + encodeURIComponent(branch || ''))
        .catch(function() {
          var capacity = type === 'home' ? 2 : 4;
          var counts = {};
          JSON.parse(localStorage.getItem('sd_bookings') || '[]').forEach(function(b) {
            if (b.JobStatus === 'Cancelled') return;
            if (b.collectionType !== type || b.date !== date) return;
            if (type !== 'home' && branch && (b.branch || '').indexOf(branch) === -1) return;
            counts[b.timeSlot] = (counts[b.timeSlot] || 0) + 1;
          });
          return Object.keys(counts).filter(function(s) { return counts[s] >= capacity; });
        });
    },

    createBooking: function(data) {
      // SWAP: remove the throw once WCF DB is set up; this will persist to SQL
      return apiFetch('/CreateBooking', { method: 'POST', body: JSON.stringify(data) });
    },

    getBookingsByPatient: function(patientId) {
      return apiFetch('/GetBookingsByPatient/' + patientId);
    },

    getBookingByRef: function(ref) {
      return apiFetch('/GetBookingByRef/' + ref);
    },

    getReportsByPatient: function(patientId) {
      return apiFetch('/GetReportsByPatient/' + patientId);
    },

    updateSampleStatus: function(ref, status) {
      return apiFetch('/UpdateSampleStatus/' + ref + '/' + status);
    },

    getSampleTimeline: function(ref) {
      return apiFetch('/GetSampleTimeline/' + encodeURIComponent(ref));
    },

    cancelBooking: function(ref) {
      return apiFetch('/CancelBooking/' + ref);
    },

    validatePromoCode: function(code, subtotal) {
      return apiFetch('/ValidatePromoCode?code=' + encodeURIComponent(code) + '&subtotal=' + subtotal);
    },

    getCollectionQueue: function() {
      return apiFetch('/GetCollectionQueue');
    },

    getFamilyMembers: function(patientId) {
      return apiFetch('/GetFamilyMembers/' + patientId);
    },

    addFamilyMember: function(data) {
      return apiFetch('/AddFamilyMember', {method:'POST', body:JSON.stringify(data)});
    },

    removeFamilyMember: function(familyMemberId) {
      return apiFetch('/RemoveFamilyMember/' + familyMemberId);
    },

    getAdminStats: function() {
      return apiFetch('/GetAdminStats');
    },

    getAllBookings: function() {
      return apiFetch('/GetAllBookings');
    },

    getPatient: function(patientId) {
      return apiFetch('/GetPatient/' + patientId);
    },

    sendOtp: function(phone) {
      return apiFetch('/SendOtp', {method:'POST', body:JSON.stringify({Phone:phone})});
    },

    verifyOtp: function(phone, otp) {
      // WCF PatientDC uses "Token" for the OTP value — NOT "Otp".
      // Sending "Otp" made dc.Token always null, so every OTP was rejected.
      return apiFetch('/VerifyOtp', {method:'POST', body:JSON.stringify({Phone:phone, Token:otp})});
    },

    registerPatient: function(data) {
      return apiFetch('/RegisterPatient', {method:'POST', body:JSON.stringify(data)});
    },

    // Payment reconciliation: front-desk settlement of counter bookings or a
    // gateway webhook relay. status: Paid / Pending / Refunded / Failed.
    updatePaymentStatus: function(ref, status, method, paymentRef, source, amountPaid) {
      return apiFetch('/UpdatePaymentStatus', {method:'POST', body:JSON.stringify({
        BookingRef:ref, PaymentStatus:status, PaymentMethod:method||null,
        PaymentRef:paymentRef||null, Source:source||'Portal', AmountPaid:amountPaid||null
      })});
    },

    refundPayment: function(ref, amount, reason, source) {
      return apiFetch('/RefundPayment', {method:'POST', body:JSON.stringify({
        BookingRef:ref, Amount:amount, Reason:reason, Source:source||'FrontDesk'
      })});
    },

    getNotificationsByPatient: function(patientId) {
      return apiFetch('/GetNotificationsByPatient/' + patientId);
    },

    attachReport: function(bookingRef, reportFilePath, source) {
      return apiFetch('/AttachReport', {method:'POST', body:JSON.stringify({
        BookingRef: bookingRef, ReportFilePath: reportFilePath, Source: source || 'Staff'
      })});
    },

    getInvoiceByBookingRef: function(ref) {
      return apiFetch('/GetInvoiceByBookingRef/' + encodeURIComponent(ref));
    },

    getLimsSyncStatus: function() {
      return apiFetch('/GetLimsSyncStatus');
    },

    syncTestCatalogue: function() {
      return apiFetch('/SyncTestCatalogue', {method:'POST', body:'{}'});
    },

    shareReportWithDoctor: function(bookingRef, doctorName, doctorEmail) {
      return apiFetch('/ShareReportWithDoctor', {method:'POST', body:JSON.stringify({
        BookingRef: bookingRef, DoctorName: doctorName, DoctorEmail: doctorEmail
      })});
    },

    getAnalyticsSummary: function(from, to) {
      return apiFetch('/GetAnalyticsSummary?from=' + (from||'') + '&to=' + (to||''));
    },

    // Maps a portal registration (sd_user shape) to the field set required by
    // LIMS-Pathology User/New. Fields the patient never sees (Position,
    // Reference, Accounts, Username) are auto-filled with portal defaults so
    // the record is always structurally complete for job creation.
    toLimsUser: function(u) {
      u = u || {};
      return {
        FirstName: u.FirstName || '',
        LastName:  u.LastName  || '',
        Phone:     u.MobileNumber || u.Phone || '',
        Email:     u.Email || '',
        Address:   [u.Address, u.City, u.Pincode].filter(Boolean).join(', '),
        Username:  u.Email || u.MobileNumber || u.Phone || '',
        Position:  'Patient',
        Reference: 'Patient Portal',
        Accounts:  u.PatientID || ''
      };
    }
  };
})(window);
