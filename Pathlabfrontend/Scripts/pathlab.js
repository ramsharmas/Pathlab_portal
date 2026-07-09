// ─────────────────────────────────────────────────────────
//  pathlab.js  –  Shared data, utilities and state for
//                 Sri Sai PathlabPortal
// ─────────────────────────────────────────────────────────

const PATHLAB_NAME   = "Sri Sai Pathlab";
const PATHLAB_PHONE  = "0771-2345678";
const PATHLAB_EMAIL  = "info@srisaipathlab.com";

// ── TEST CATALOGUE ────────────────────────────────────────
// type:"Test"     → individual diagnostic test (maps to LIMS SetupTestsuite.TestSuiteName)
// testCode        → LIMS code used in SetupTestsuite for API mapping
// paramCount      → number of parameters/analytes in the test
// popular:true    → shown in "Most Booked Tests" on the landing page
const TEST_SUITES = [
  { id:1,  type:"Test", testCode:"CBC",     popular:true,  paramCount:24, name:"Complete Blood Count (CBC)",        price:350,  sample:"Blood",        fasting:"None",     reportTime:"Same day",  description:"Measures RBC, WBC, platelets & haemoglobin levels." },
  { id:2,  type:"Test", testCode:"LIP",     popular:true,  paramCount:8,  name:"Lipid Profile",                     price:550,  sample:"Blood",        fasting:"12 hours", reportTime:"Same day",  description:"Cholesterol panel: HDL, LDL, VLDL & triglycerides." },
  { id:3,  type:"Test", testCode:"TFT",     popular:true,  paramCount:3,  name:"Thyroid Profile (T3/T4/TSH)",       price:650,  sample:"Blood",        fasting:"None",     reportTime:"Same day",  description:"Evaluates thyroid gland function." },
  { id:4,  type:"Test", testCode:"LFT",     popular:false, paramCount:12, name:"Liver Function Test (LFT)",         price:750,  sample:"Blood",        fasting:"None",     reportTime:"Same day",  description:"Assesses liver health — bilirubin, ALT, AST, ALP." },
  { id:5,  type:"Test", testCode:"KFT",     popular:false, paramCount:10, name:"Kidney Function Test (KFT)",        price:650,  sample:"Blood",        fasting:"None",     reportTime:"Same day",  description:"Creatinine, urea, uric acid & electrolytes." },
  { id:6,  type:"Test", testCode:"BSF",     popular:false, paramCount:1,  name:"Blood Glucose – Fasting",           price:150,  sample:"Blood",        fasting:"8 hours",  reportTime:"Same day",  description:"Measures blood sugar after overnight fasting." },
  { id:7,  type:"Test", testCode:"HBA1C",   popular:true,  paramCount:1,  name:"HbA1c (Glycated Haemoglobin)",      price:450,  sample:"Blood",        fasting:"None",     reportTime:"Same day",  description:"Average blood sugar over the past 2–3 months." },
  { id:8,  type:"Test", testCode:"URM",     popular:false, paramCount:18, name:"Urine Routine & Microscopy",        price:200,  sample:"Urine",        fasting:"None",     reportTime:"Same day",  description:"Physical, chemical & microscopic urine examination." },
  { id:9,  type:"Test", testCode:"VIT_D",   popular:true,  paramCount:1,  name:"Vitamin D (25-OH)",                 price:1200, sample:"Blood",        fasting:"None",     reportTime:"24 hours",  description:"Measures 25-hydroxyvitamin D levels." },
  { id:10, type:"Test", testCode:"VIT_B12", popular:false, paramCount:1,  name:"Vitamin B12",                       price:850,  sample:"Blood",        fasting:"None",     reportTime:"24 hours",  description:"Cobalamin level – important for nerve function." },
  { id:11, type:"Test", testCode:"CRP",     popular:false, paramCount:1,  name:"C-Reactive Protein (CRP)",          price:400,  sample:"Blood",        fasting:"None",     reportTime:"Same day",  description:"Key marker of inflammation and infection." },
  { id:12, type:"Test", testCode:"DENGUE",  popular:false, paramCount:1,  name:"Dengue NS1 Antigen",                price:900,  sample:"Blood",        fasting:"None",     reportTime:"Same day",  description:"Early detection of dengue virus antigen." },
  { id:13, type:"Test", testCode:"MAL",     popular:false, paramCount:1,  name:"Malaria Parasite Test",             price:350,  sample:"Blood",        fasting:"None",     reportTime:"Same day",  description:"Peripheral blood smear for malaria parasites." },
  { id:14, type:"Test", testCode:"WIDAL",   popular:false, paramCount:4,  name:"Widal Test (Typhoid)",              price:300,  sample:"Blood",        fasting:"None",     reportTime:"Same day",  description:"Detects antibodies against Salmonella Typhi." },
  { id:15, type:"Test", testCode:"HIV",     popular:false, paramCount:1,  name:"HIV 1 & 2 Screening",               price:450,  sample:"Blood",        fasting:"None",     reportTime:"Same day",  description:"ELISA-based HIV antibody screen." },
  { id:16, type:"Test", testCode:"VDRL",    popular:false, paramCount:1,  name:"VDRL (Syphilis Screen)",            price:300,  sample:"Blood",        fasting:"None",     reportTime:"Same day",  description:"Rapid plasma reagin test for syphilis." },
  { id:17, type:"Test", testCode:"IRON",    popular:false, paramCount:4,  name:"Iron Studies",                      price:700,  sample:"Blood",        fasting:"None",     reportTime:"24 hours",  description:"Serum iron, ferritin & TIBC panel." },
  { id:18, type:"Test", testCode:"FBC60",   popular:false, paramCount:60, name:"Full Body Checkup (60 tests)",      price:2999, sample:"Blood + Urine",fasting:"12 hours", reportTime:"24 hours",  description:"Comprehensive 60-parameter profile covering all major organs." },
  { id:19, type:"Test", testCode:"COVID",   popular:false, paramCount:1,  name:"COVID-19 RT-PCR",                   price:800,  sample:"Nasal swab",   fasting:"None",     reportTime:"24 hours",  description:"Gold-standard PCR test for SARS-CoV-2." },
  { id:20, type:"Test", testCode:"CA",      popular:false, paramCount:1,  name:"Serum Calcium",                     price:250,  sample:"Blood",        fasting:"None",     reportTime:"Same day",  description:"Measures calcium levels for bone & muscle health." }
];

// ── HEALTH PACKAGES ───────────────────────────────────────
// type:"Package"  → health panel/package (maps to LIMS SetupTestsuite with panel flag)
// testCode        → LIMS package code for SetupTestsuite mapping
// paramCount      → total number of individual test parameters included
// popular:true    → shown in "Popular Packages" on the landing page
const HEALTH_PACKAGES = [
  {
    id:"basic", type:"Package", testCode:"PKG_BASIC", popular:true, paramCount:20,
    name:"Basic Health Package", price:999, originalPrice:1800,
    badge:"Most Popular", badgeClass:"bg-primary",
    description:"Essential tests for a basic health overview. Ideal for annual check-ups.",
    includes:["Complete Blood Count","Blood Glucose (Fasting)","Urine Routine","Lipid Profile","Liver Function Test","Kidney Function Test","Blood Group & Rh","ESR","Uric Acid"],
    fasting:"8 hours", sample:"Blood + Urine", reportTime:"Same day"
  },
  {
    id:"women", type:"Package", testCode:"PKG_WOMEN", popular:true, paramCount:35,
    name:"Women's Wellness Package", price:1499, originalPrice:2800,
    badge:"Best for Women", badgeClass:"bg-pink",
    description:"Tailored for women's health, covering hormonal, nutritional & reproductive markers.",
    includes:["CBC","Thyroid Profile","Vitamin D","Iron Studies","Hormonal Profile (FSH/LH)","Pap Smear","Calcium","Magnesium"],
    fasting:"10 hours", sample:"Blood + Urine", reportTime:"24 hours"
  },
  {
    id:"diabetes", type:"Package", testCode:"PKG_DM", popular:true, paramCount:25,
    name:"Diabetes Care Package", price:1299, originalPrice:2200,
    badge:"Diabetes Focused", badgeClass:"bg-warning text-dark",
    description:"Complete diabetes monitoring panel for diagnosed and at-risk patients.",
    includes:["HbA1c","Fasting Blood Glucose","Post-Prandial Glucose","Kidney Function","Lipid Profile","Urine Microalbumin","C-Peptide"],
    fasting:"12 hours", sample:"Blood + Urine", reportTime:"Same day"
  },
  {
    id:"heart", type:"Package", testCode:"PKG_HEART", popular:true, paramCount:30,
    name:"Heart Health Package", price:1999, originalPrice:3500,
    badge:"Cardio Focus", badgeClass:"bg-danger",
    description:"Cardiovascular risk assessment panel for prevention and monitoring.",
    includes:["Lipid Profile","CRP (hs)","ECG","Blood Pressure","Blood Sugar","Homocysteine","Troponin I","BNP"],
    fasting:"12 hours", sample:"Blood", reportTime:"24 hours"
  },
  {
    id:"senior", type:"Package", testCode:"PKG_SR", popular:false, paramCount:50,
    name:"Senior Citizen Package", price:2499, originalPrice:4500,
    badge:"Age 60+", badgeClass:"bg-success",
    description:"Comprehensive geriatric screening covering all organ systems.",
    includes:["CBC","Thyroid","Vitamin D","B12","Bone Density Markers","PSA (Men) / CA-125 (Women)","Kidney","Liver","Lipid"],
    fasting:"12 hours", sample:"Blood + Urine", reportTime:"24 hours"
  },
  {
    id:"fullbody", type:"Package", testCode:"PKG_FB", popular:false, paramCount:60,
    name:"Full Body Checkup", price:2999, originalPrice:5500,
    badge:"Most Comprehensive", badgeClass:"bg-purple",
    description:"The most thorough health screening — covers all organs, vitamins, hormones & markers.",
    includes:["All organ function tests","Vitamins & Minerals","Hormonal Panel","Tumour Markers","Infection Markers","Cardiac Markers","Diabetes Panel","Urine Full Exam"],
    fasting:"12 hours", sample:"Blood + Urine", reportTime:"24 hours"
  }
];

// ── BRANCHES ──────────────────────────────────────────────
const BRANCHES = [
  { id:1, name:"Main Branch",  address:"123 MG Road, City Centre",   phone:"0771-2345678", timings:"6:00 AM – 8:00 PM", mapUrl:"#" },
  { id:2, name:"North Branch", address:"45 North Avenue, Sector 7",  phone:"0771-3456789", timings:"7:00 AM – 7:00 PM", mapUrl:"#" },
  { id:3, name:"South Branch", address:"78 South Mall Road, Sector 12", phone:"0771-4567890", timings:"7:00 AM – 6:00 PM", mapUrl:"#" }
];

// ── TIME SLOTS ────────────────────────────────────────────
function generateSlots() {
  const slots = [];
  for (let h = 7; h <= 17; h++) {
    const label = h < 12 ? 'AM' : 'PM';
    const hr12  = h <= 12 ? h : h - 12;
    slots.push(`${hr12}:00 ${label}`);
    slots.push(`${hr12}:30 ${label}`);
  }
  return slots;
}
const TIME_SLOTS = generateSlots();

// ── SAMPLE STATUSES ───────────────────────────────────────
const SAMPLE_STATUSES = ["Booked","Sample Collected","At Lab","Processing","Ready"];

// ═══════════════════════════════════════════════════════════
//  CART
// ═══════════════════════════════════════════════════════════
const Cart = {
  get()  { return JSON.parse(localStorage.getItem('pathlab_cart') || '[]'); },
  save(cart) { localStorage.setItem('pathlab_cart', JSON.stringify(cart)); this.updateBadge(); },
  add(item) {
    const cart = this.get();
    if (!cart.find(i => i.id === item.id)) { cart.push(item); this.save(cart); return true; }
    return false;
  },
  remove(id) {
    this.save(this.get().filter(i => i.id !== id));
  },
  clear() { localStorage.removeItem('pathlab_cart'); this.updateBadge(); },
  total() { return this.get().reduce((s, i) => s + i.price, 0); },
  count() { return this.get().length; },
  updateBadge() {
    const n = this.count();
    document.querySelectorAll('.cart-badge').forEach(el => {
      el.textContent = n;
      el.style.display = n > 0 ? 'inline-flex' : 'none';
    });
  }
};

// ═══════════════════════════════════════════════════════════
//  AUTH
// ═══════════════════════════════════════════════════════════
const Auth = {
  get()      { return JSON.parse(localStorage.getItem('pathlab_patient') || 'null'); },
  set(p)     { localStorage.setItem('pathlab_patient', JSON.stringify(p)); },
  logout()   { localStorage.removeItem('pathlab_patient'); window.location.href = 'index.html'; },
  isLoggedIn(){ return !!this.get(); }
};

// ═══════════════════════════════════════════════════════════
//  BOOKINGS
// ═══════════════════════════════════════════════════════════
const Bookings = {
  get()       { return JSON.parse(localStorage.getItem('pathlab_bookings') || '[]'); },
  getByPhone(phone) { return this.get().filter(b => b.patientPhone === phone); },
  getById(id) { return this.get().find(b => b.id === id) || null; },
  add(booking) {
    const all = this.get();
    booking.id        = 'BK' + Date.now();
    booking.createdAt = new Date().toISOString();
    booking.sampleStatus = 0;
    all.unshift(booking);
    localStorage.setItem('pathlab_bookings', JSON.stringify(all));
    return booking;
  },
  advanceStatus(id) {
    const all = this.get();
    const b   = all.find(x => x.id === id);
    if (b && b.sampleStatus < SAMPLE_STATUSES.length - 1) {
      b.sampleStatus++;
      localStorage.setItem('pathlab_bookings', JSON.stringify(all));
    }
    return b;
  }
};

// ═══════════════════════════════════════════════════════════
//  REPORTS
// ═══════════════════════════════════════════════════════════
const Reports = {
  _seed() {
    const phone = Auth.get()?.phone || '9876543210';
    return [
      { id:'RPT001', bookingId:'BK001', date:'2026-06-01', tests:['CBC','Lipid Profile'], status:'Ready', patientPhone: phone },
      { id:'RPT002', bookingId:'BK002', date:'2026-05-15', tests:['Thyroid Profile'], status:'Ready', patientPhone: phone },
      { id:'RPT003', bookingId:'BK003', date:'2026-04-20', tests:['Full Body Checkup'], status:'Ready', patientPhone: phone }
    ];
  },
  get() {
    const stored = JSON.parse(localStorage.getItem('pathlab_reports') || 'null');
    if (!stored) {
      const seeded = this._seed();
      localStorage.setItem('pathlab_reports', JSON.stringify(seeded));
      return seeded;
    }
    return stored;
  },
  getByPhone(phone) { return this.get().filter(r => r.patientPhone === phone); }
};

// ═══════════════════════════════════════════════════════════
//  NOTIFICATIONS
// ═══════════════════════════════════════════════════════════
const Notifications = {
  get() { return JSON.parse(localStorage.getItem('pathlab_notifs') || '[]'); },
  add(msg, type='info') {
    const all = this.get();
    all.unshift({ id: Date.now(), msg, type, ts: new Date().toISOString(), read: false });
    localStorage.setItem('pathlab_notifs', JSON.stringify(all));
  },
  markAllRead() {
    const all = this.get().map(n => ({ ...n, read: true }));
    localStorage.setItem('pathlab_notifs', JSON.stringify(all));
  },
  unreadCount() { return this.get().filter(n => !n.read).length; }
};

// ═══════════════════════════════════════════════════════════
//  HELPERS
// ═══════════════════════════════════════════════════════════
function fmt(amount) {
  return '₹' + Number(amount).toLocaleString('en-IN');
}

function fmtDate(iso) {
  return new Date(iso).toLocaleDateString('en-IN', { day:'2-digit', month:'short', year:'numeric' });
}

function fmtDateTime(iso) {
  return new Date(iso).toLocaleString('en-IN', { day:'2-digit', month:'short', year:'numeric', hour:'2-digit', minute:'2-digit' });
}

function showToast(msg, type='success') {
  let container = document.getElementById('toast-container');
  if (!container) {
    container = document.createElement('div');
    container.id = 'toast-container';
    container.style.cssText = 'position:fixed;bottom:20px;right:20px;z-index:9999;display:flex;flex-direction:column;gap:8px;';
    document.body.appendChild(container);
  }
  const toast = document.createElement('div');
  const colors = { success:'#1a8a2c', error:'#c0392b', info:'#0066cc', warning:'#e67e22' };
  toast.style.cssText = `background:${colors[type]||colors.info};color:#fff;padding:12px 20px;border-radius:8px;font-size:14px;max-width:320px;box-shadow:0 4px 12px rgba(0,0,0,0.2);display:flex;align-items:center;gap:8px;animation:slideIn .3s ease;`;
  const icon = { success:'check-circle', error:'times-circle', info:'info-circle', warning:'exclamation-triangle' };
  toast.innerHTML = `<i class="fas fa-${icon[type]||'info-circle'}"></i><span>${msg}</span>`;
  container.appendChild(toast);
  setTimeout(() => { toast.style.opacity='0'; toast.style.transition='opacity .3s'; setTimeout(()=>toast.remove(),300); }, 3500);
}

function requireAuth(redirect='login.html') {
  if (!Auth.isLoggedIn()) {
    localStorage.setItem('pathlab_redirect', window.location.href);
    window.location.href = redirect;
    return false;
  }
  return true;
}

// ── SHARED NAV ────────────────────────────────────────────
function renderNav(activePage) {
  const patient  = Auth.get();
  const cartCount = Cart.count();
  const html = `
  <nav class="navbar navbar-expand-lg bg-white shadow-sm sticky-top" style="border-bottom:2px solid #e8f0fe;">
    <div class="container">
      <a class="navbar-brand d-flex align-items-center gap-2" href="index.html">
        <div style="width:38px;height:38px;background:linear-gradient(135deg,#0066cc,#0099ff);border-radius:10px;display:flex;align-items:center;justify-content:center;">
          <i class="fas fa-flask text-white" style="font-size:18px;"></i>
        </div>
        <div>
          <div class="fw-bold text-primary" style="font-size:15px;line-height:1.1;">${PATHLAB_NAME}</div>
          <div class="text-muted" style="font-size:10px;">Diagnostic Centre</div>
        </div>
      </a>
      <button class="navbar-toggler border-0" type="button" data-bs-toggle="collapse" data-bs-target="#mainNav">
        <span class="navbar-toggler-icon"></span>
      </button>
      <div class="collapse navbar-collapse" id="mainNav">
        <ul class="navbar-nav mx-auto gap-1">
          <li class="nav-item"><a class="nav-link px-3 fw-medium ${activePage==='home'?'text-primary active':''}" href="index.html">Home</a></li>
          <li class="nav-item"><a class="nav-link px-3 fw-medium ${activePage==='book'?'text-primary active':''}" href="book-test.html">Book a Test</a></li>
          <li class="nav-item"><a class="nav-link px-3 fw-medium ${activePage==='packages'?'text-primary active':''}" href="health-packages.html">Packages</a></li>
          <li class="nav-item"><a class="nav-link px-3 fw-medium ${activePage==='reports'?'text-primary active':''}" href="download-reports.html">Reports</a></li>
          <li class="nav-item"><a class="nav-link px-3 fw-medium ${activePage==='track'?'text-primary active':''}" href="track-sample.html">Track Sample</a></li>
        </ul>
        <div class="d-flex align-items-center gap-2 mt-2 mt-lg-0">
          <a href="cart.html" class="btn btn-outline-primary position-relative px-3">
            <i class="fas fa-shopping-cart"></i>
            <span class="cart-badge position-absolute top-0 start-100 translate-middle badge rounded-pill bg-danger"
              style="display:${cartCount>0?'inline-flex':'none'};font-size:10px;min-width:18px;height:18px;align-items:center;justify-content:center;">${cartCount}</span>
          </a>
          ${patient
            ? `<a href="patient-portal.html" class="btn btn-primary px-3"><i class="fas fa-user-circle me-1"></i>${patient.name.split(' ')[0]}</a>`
            : `<a href="login.html" class="btn btn-primary px-3"><i class="fas fa-sign-in-alt me-1"></i>Login</a>`
          }
          <a href="admin.html" class="btn btn-outline-secondary px-2" title="Admin"><i class="fas fa-cog"></i></a>
        </div>
      </div>
    </div>
  </nav>`;
  const el = document.getElementById('nav-placeholder');
  if (el) el.innerHTML = html;
  else document.body.insertAdjacentHTML('afterbegin', html);
  Cart.updateBadge();
}

// Shared footer
function renderFooter() {
  const html = `
  <footer style="background:#0a1f3c;color:#ccc;padding:48px 0 24px;">
    <div class="container">
      <div class="row g-4 mb-4">
        <div class="col-md-4">
          <div class="d-flex align-items-center gap-2 mb-3">
            <div style="width:36px;height:36px;background:linear-gradient(135deg,#0066cc,#0099ff);border-radius:8px;display:flex;align-items:center;justify-content:center;">
              <i class="fas fa-flask text-white"></i>
            </div>
            <div>
              <div class="fw-bold text-white">${PATHLAB_NAME}</div>
              <div style="font-size:11px;">Diagnostic Centre</div>
            </div>
          </div>
          <p style="font-size:13px;">NABL-accredited diagnostic laboratory providing accurate, timely reports with home collection services.</p>
          <div class="d-flex gap-3 mt-3">
            <a href="#" class="text-white"><i class="fab fa-facebook-f"></i></a>
            <a href="#" class="text-white"><i class="fab fa-instagram"></i></a>
            <a href="#" class="text-white"><i class="fab fa-whatsapp"></i></a>
          </div>
        </div>
        <div class="col-md-2">
          <h6 class="text-white fw-bold mb-3">Quick Links</h6>
          <ul class="list-unstyled" style="font-size:13px;">
            <li class="mb-2"><a href="book-test.html" class="text-decoration-none" style="color:#aaa;">Book a Test</a></li>
            <li class="mb-2"><a href="health-packages.html" class="text-decoration-none" style="color:#aaa;">Packages</a></li>
            <li class="mb-2"><a href="download-reports.html" class="text-decoration-none" style="color:#aaa;">Download Reports</a></li>
            <li class="mb-2"><a href="track-sample.html" class="text-decoration-none" style="color:#aaa;">Track Sample</a></li>
            <li class="mb-2"><a href="patient-portal.html" class="text-decoration-none" style="color:#aaa;">Patient Portal</a></li>
          </ul>
        </div>
        <div class="col-md-3">
          <h6 class="text-white fw-bold mb-3">Our Branches</h6>
          <ul class="list-unstyled" style="font-size:13px;">
            ${BRANCHES.map(b => `<li class="mb-2"><i class="fas fa-map-marker-alt me-2" style="color:#0099ff;"></i><span style="color:#aaa;">${b.name} – ${b.address}</span></li>`).join('')}
          </ul>
        </div>
        <div class="col-md-3">
          <h6 class="text-white fw-bold mb-3">Contact Us</h6>
          <ul class="list-unstyled" style="font-size:13px;color:#aaa;">
            <li class="mb-2"><i class="fas fa-phone me-2" style="color:#0099ff;"></i>${PATHLAB_PHONE}</li>
            <li class="mb-2"><i class="fas fa-envelope me-2" style="color:#0099ff;"></i>${PATHLAB_EMAIL}</li>
            <li class="mb-2"><i class="fas fa-clock me-2" style="color:#0099ff;"></i>Mon–Sat: 6 AM – 8 PM</li>
            <li class="mb-2"><i class="fas fa-clock me-2" style="color:#0099ff;"></i>Sun: 7 AM – 2 PM</li>
          </ul>
        </div>
      </div>
      <hr style="border-color:#1e3a5f;">
      <div class="d-flex flex-wrap justify-content-between align-items-center" style="font-size:12px;color:#666;">
        <span>© 2026 ${PATHLAB_NAME}. All rights reserved.</span>
        <span>NABL Accredited &nbsp;|&nbsp; ISO 9001:2015 Certified</span>
      </div>
    </div>
  </footer>`;
  const el = document.getElementById('footer-placeholder');
  if (el) el.innerHTML = html;
}

// ── CSS animation injection ───────────────────────────────
const _style = document.createElement('style');
_style.textContent = `
  @keyframes slideIn { from{transform:translateX(100px);opacity:0} to{transform:translateX(0);opacity:1} }
  @keyframes fadeIn  { from{opacity:0;transform:translateY(10px)} to{opacity:1;transform:translateY(0)} }
  .fade-in { animation: fadeIn .4s ease; }
`;
document.head.appendChild(_style);

