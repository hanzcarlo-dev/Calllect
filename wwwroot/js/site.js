
document.addEventListener("DOMContentLoaded", function () {
    const themeButton = document.getElementById("themeToggle");

    const savedTheme = localStorage.getItem("calllect-theme");

    if (savedTheme === "dark") {
        document.body.classList.add("dark-mode");
    }

    if (themeButton) {
        themeButton.addEventListener("click", function () {
            document.body.classList.toggle("dark-mode");

            const isDark =
                document.body.classList.contains("dark-mode");

            localStorage.setItem(
                "calllect-theme",
                isDark ? "dark" : "light"
            );

            themeButton.textContent =
                isDark ? "☀ Light Mode" : "☾ Dark Mode";
        });

        themeButton.textContent =
            document.body.classList.contains("dark-mode")
                ? "☀ Light Mode"
                : "☾ Dark Mode";
    }
});
