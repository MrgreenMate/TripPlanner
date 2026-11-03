

(function setupNavbar() {
    const root = document.getElementById("navbar-root");
    if (!root) return;

    const cfg = window.TP_CONFIG || {};
    const STORAGE_KEY_USER_ID = cfg.storageUserIdKey || "tp_userId";
    const STORAGE_KEY_USER_NAME = cfg.storageUserNameKey || "tp_userName";

    const navItems = [
        { href: "index.html", label: "🏠 Kezdőlap" },
        { href: "new-itinerary.html", label: "🧠 Új útvonal" },
        { href: "itineraries.html", label: "📋 Útvonalak" },
        { href: "user-preferences.html", label: "⚙️ AI beállítások" }
    ];

    const current = location.pathname.split("/").pop() || "index.html";

    let linksHtml = "";
    navItems.forEach(n => {
        const active = n.href === current ? "active" : "";
        linksHtml += `<a class="nav-link ${active}" href="${n.href}">${n.label}</a>`;
    });

    const uid = localStorage.getItem(STORAGE_KEY_USER_ID);
    const uname = localStorage.getItem(STORAGE_KEY_USER_NAME) || "";

    const userInfo = uid
        ? `<span class="nav-user">${uname || "Felhasználó"} (id=${uid})</span>`
        : `<span class="nav-user nav-user-guest">Nincs bejelentkezve</span>`;

    root.innerHTML = `
        <div class="navbar">
            <div class="nav-left">${linksHtml}</div>
            <div class="nav-right">${userInfo}</div>
        </div>
    `;
})();
