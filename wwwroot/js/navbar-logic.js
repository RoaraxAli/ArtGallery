document.addEventListener('DOMContentLoaded', () => {
    const header = document.getElementById('main-header');
    const container = document.getElementById('nav-container');

    // Define toggleMenu globally so buttons can access it
    window.toggleMenu = function (menuId) {
        const menu = document.getElementById(menuId);
        if (menu) {
            menu.classList.toggle('hidden');
        }
    };

    // Close menus when clicking outside
    document.addEventListener('click', function (e) {
        if (!e.target.closest('.relative')) {
            document.querySelectorAll('.absolute.animate-slide-down').forEach(el => {
                if (!el.classList.contains('hidden')) el.classList.add('hidden');
            });
        }
    });

    // Make handleScroll globally accessible
    window.handleScroll = () => {
        if (!header || !container) return;

        if (window.scrollY > 50) {
            // Scrolled State: Floating "Island"
            header.classList.remove('top-0', 'left-0', 'w-full');
            header.classList.add('top-4', 'left-1/2', '-translate-x-1/2', 'w-[95%]', 'max-w-7xl');

            container.classList.add('glass-morphism', 'bg-glass', 'rounded-full', 'shadow-2xl', 'py-3', 'px-8');
            container.classList.remove('py-6', 'bg-transparent', 'border-b', 'border-[#ffffff10]', 'rounded-b-[2.5rem]');

        } else {
            // Top State: Stuck to top
            header.classList.add('top-0', 'left-0', 'w-full');
            header.classList.remove('top-4', 'left-1/2', '-translate-x-1/2', 'w-[95%]', 'max-w-7xl');

            container.classList.remove('glass-morphism', 'bg-glass', 'rounded-full', 'shadow-2xl', 'py-3', 'px-8');
            container.classList.add('py-6', 'bg-transparent', 'border-b', 'border-[#ffffff10]', 'rounded-b-[2.5rem]');
        }
    };

    // Initial check
    window.handleScroll();

    // Listen to scroll
    window.addEventListener('scroll', window.handleScroll);
});
