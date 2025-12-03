(() => {
    const header = document.querySelector('.app-shell-header');
    if (!header) {
        return;
    }

    let lastScrollY = window.scrollY;

    window.addEventListener('scroll', () => {
        const current = window.scrollY;
        const body = document.body;

        if (current > lastScrollY && current > 80) {
            body.classList.add('nav-hidden');
        } else {
            body.classList.remove('nav-hidden');
        }

        lastScrollY = current;
    });
})();
