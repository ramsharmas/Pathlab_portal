// SD Notify — SMS (Fast2SMS) + WhatsApp helpers. Reads keys from Scripts/config.js (SD_CONFIG).
// Without a real API key it runs in demo mode: messages are logged to console, not sent.
(function (w) {

    function cfg() { return w.SD_CONFIG || {}; }

    function smsLive() {
        var key = cfg().FAST2SMS_API_KEY || '';
        return key.length > 0 && key.indexOf('YOUR_') === -1;
    }

    function sendSms(phone, message) {
        if (!phone || !message) return Promise.resolve({ skipped: true });
        if (!smsLive()) {
            console.info('[SDNotify] demo mode — SMS to ' + phone + ': ' + message);
            return Promise.resolve({ demo: true });
        }
        return fetch('https://www.fast2sms.com/dev/bulkV2', {
            method: 'POST',
            headers: { 'authorization': cfg().FAST2SMS_API_KEY, 'Content-Type': 'application/json' },
            body: JSON.stringify({ route: 'q', numbers: String(phone).replace(/\D/g, ''), message: message })
        }).then(function (r) { return r.json(); })
          .catch(function (e) { console.warn('[SDNotify] SMS send failed', e); return { error: true }; });
    }

    // Opens a WhatsApp chat with the message prefilled. True unattended sending
    // needs the WhatsApp Business API on a server — this is the client-side equivalent.
    function whatsappLink(message, phone) {
        var base = phone ? 'https://wa.me/' + String(phone).replace(/\D/g, '') : 'https://wa.me/';
        return base + '?text=' + encodeURIComponent(message || '');
    }

    // Deterministic phlebotomist assignment per booking ref (demo roster —
    // replace with the LIMS-assigned collector once the LIMS link is live).
    var PHLEBS = [
        { name: 'Ramesh Verma', phone: '9876501001' },
        { name: 'Sunita Sharma', phone: '9876501002' },
        { name: 'Arif Khan', phone: '9876501003' },
        { name: 'Priya Patel', phone: '9876501004' }
    ];
    function phlebotomist(ref) {
        var h = 0, s = String(ref || '');
        for (var i = 0; i < s.length; i++) h = (h + s.charCodeAt(i)) % 997;
        return PHLEBS[h % PHLEBS.length];
    }

    w.SDNotify = { smsLive: smsLive, sendSms: sendSms, whatsappLink: whatsappLink, phlebotomist: phlebotomist };
})(window);
