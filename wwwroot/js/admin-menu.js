const adminMenu = document.getElementById("admin-menu");
const adminMenuToggle = document.querySelector(".admin-menu-toggle");
const adminMenuClose = adminMenu.querySelector(".admin-menu-close");
const narrowAdminLayout = window.matchMedia("(max-width: 768px)");

function setAdminMenuOpen(isOpen) {
    adminMenu.classList.toggle("open", isOpen);
    adminMenuToggle.setAttribute("aria-expanded", String(isOpen));
}

adminMenuToggle.addEventListener("click", () => {
    setAdminMenuOpen(true);
    adminMenuClose.focus();
});

function closeAdminMenu() {
    setAdminMenuOpen(false);
    adminMenuToggle.focus();
}

adminMenuClose.addEventListener("click", closeAdminMenu);
document.addEventListener("keydown", event => {
    if (event.key === "Escape" && adminMenu.classList.contains("open")) {
        closeAdminMenu();
    }
});
narrowAdminLayout.addEventListener("change", () => setAdminMenuOpen(false));
