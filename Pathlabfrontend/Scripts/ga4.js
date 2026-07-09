// ── GOOGLE ANALYTICS 4 (Analytics Phase 1) ──────────────────────────────────
// Loads gtag.js only when a real Measurement ID is set in config.js — stays
// completely inert on every environment that hasn't configured one yet, so
// this never fires network calls or console noise during local/demo runs.
// SDAnalytics.trackEvent() is the single entry point the rest of the site
// calls into; it silently no-ops when GA hasn't been enabled.
(function (w, d) {
  var cfg = w.SD_CONFIG || {};
  var id = cfg.GA4_ID || "";
  var enabled = !!id && id.indexOf("G-XXXX") === -1 && id.indexOf("YOUR_") === -1;

  w.dataLayer = w.dataLayer || [];
  function gtag() { w.dataLayer.push(arguments); }
  w.gtag = w.gtag || gtag;

  if (enabled) {
    var s = d.createElement("script");
    s.async = true;
    s.src = "https://www.googletagmanager.com/gtag/js?id=" + encodeURIComponent(id);
    d.head.appendChild(s);
    gtag("js", new Date());
    gtag("config", id);
  }

  w.SDAnalytics = {
    enabled: enabled,
    // name: GA4 event name (e.g. "view_item_list", "add_to_cart", "begin_checkout", "purchase")
    // params: plain object of GA4 event parameters
    trackEvent: function (name, params) {
      if (!enabled) return;
      w.gtag("event", name, params || {});
    }
  };
})(window, document);
