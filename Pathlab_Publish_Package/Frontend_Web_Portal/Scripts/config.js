// ── SWAPNIL DIAGNOSTICS — CENTRAL CONFIG ─────────────────────────────────────
// Edit ONLY this file when going live. All pages read keys from here.
// IMPORTANT: Do NOT commit real keys to version control.

var SD_CONFIG = {

  // ── SMS (Fast2SMS) ─────────────────────────────────────────────────────────
  // Get your key from https://fast2sms.com  (free plan: 50 SMS/day)
  // Used for: OTP send, booking confirmation SMS, appointment reminders
  FAST2SMS_API_KEY: 'YOUR_FAST2SMS_API_KEY',

  // ── PAYMENT (Razorpay) ────────────────────────────────────────────────────
  // Get from https://dashboard.razorpay.com → Settings → API Keys
  // Use rzp_test_... for testing, rzp_live_... for production
  RAZORPAY_KEY_ID: 'rzp_test_YOUR_KEY_ID',

  // ── ANALYTICS (Google Analytics 4) ───────────────────────────────────────
  // Get from https://analytics.google.com → Admin → Data Streams → Measurement ID
  // Looks like: G-AB12CD34EF
  GA4_ID: 'G-XXXXXXXXXX',

  // ── UPI ───────────────────────────────────────────────────────────────────
  // Your lab's UPI ID shown on QR code in checkout
  UPI_ID: 'swapnil@upi',

  // ── WHATSAPP SUPPORT ──────────────────────────────────────────────────────
  // Include country code, no +, no spaces: 91XXXXXXXXXX
  WHATSAPP_NUMBER: '918269331264',

};
