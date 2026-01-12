
document.addEventListener('DOMContentLoaded', () => {
    const container = document.getElementById('page-transition-container');
    const content = document.getElementById('page-content');

    if (!container || !content) return;

    let isTransitioning = false;

    const navigate = async (url, isBack = false) => {
        if (isTransitioning) return;
        isTransitioning = true;

        try {
            const response = await fetch(url);
            const html = await response.text();

            const parser = new DOMParser();
            const doc = parser.parseFromString(html, 'text/html');
            const newContent = doc.getElementById('page-content');
            const newTitle = doc.querySelector('title').innerText;

            if (!newContent) {
                window.location.href = url;
                return;
            }

            // Prepare for GSAP animation
            const oldContent = content.cloneNode(true);
            oldContent.id = 'page-content-old';

            // Setup container for transition
            gsap.set(container, { position: 'relative', overflow: 'hidden' });
            gsap.set(oldContent, {
                position: 'absolute',
                top: 0,
                left: 0,
                width: '100%',
                zIndex: 1
            });

            container.appendChild(oldContent);

            // Update actual content
            content.innerHTML = newContent.innerHTML;

            // Execute scripts in the new content
            const scripts = content.querySelectorAll('script');
            scripts.forEach(oldScript => {
                const newScript = document.createElement('script');
                Array.from(oldScript.attributes).forEach(attr => newScript.setAttribute(attr.name, attr.value));
                newScript.appendChild(document.createTextNode(oldScript.innerHTML));
                oldScript.parentNode.replaceChild(newScript, oldScript);
            });

            document.title = newTitle;
            if (!isBack) {
                history.pushState({ url }, newTitle, url);
            }

            // GSAP Transition
            const tl = gsap.timeline({
                onComplete: () => {
                    oldContent.remove();
                    gsap.set(content, { clearProps: "all" });
                    isTransitioning = false;
                }
            });

            if (isBack) {
                // Back Transition: Old slides right, New slides in from left
                gsap.set(content, { x: '-10%', opacity: 0 });
                tl.to(oldContent, { x: '100%', opacity: 0, duration: 0.6, ease: "expo.inOut" })
                    .to(content, { x: '0%', opacity: 1, duration: 0.6, ease: "expo.out" }, "-=0.4");
            } else {
                // Forward Transition: Old slides left, New slides in from right
                gsap.set(content, { x: '10%', opacity: 0 });
                tl.to(oldContent, { x: '-100%', opacity: 0, duration: 0.6, ease: "expo.inOut" })
                    .to(content, { x: '0%', opacity: 1, duration: 0.6, ease: "expo.out" }, "-=0.4");
            }

            window.scrollTo({ top: 0, behavior: 'smooth' });
            if (window.initializeCursor) window.initializeCursor();
            if (window.initScrollAnimations) window.initScrollAnimations();
            if (window.ScrollTrigger) ScrollTrigger.refresh();
            updateActiveLinks(url);

        } catch (error) {
            console.error('Navigation error:', error);
            window.location.href = url;
        }
    };

    const updateActiveLinks = (url) => {
        const path = new URL(url, window.location.origin).pathname;
        document.querySelectorAll('nav a, header a').forEach(a => {
            const h = a.getAttribute('href');
            if (!h) return;
            const aPath = new URL(h, window.location.origin).pathname;
            if (path === aPath) {
                a.classList.add('text-[var(--text-primary)]');
                a.classList.remove('text-[var(--text-secondary)]');
            } else if (!h.startsWith('#')) {
                a.classList.remove('text-[var(--text-primary)]');
                a.classList.add('text-[var(--text-secondary)]');
            }
        });
    };

    document.addEventListener('click', (e) => {
        const anchor = e.target.closest('a');
        if (!anchor) return;

        const href = anchor.getAttribute('href');
        if (!href || href.startsWith('#') || href.startsWith('javascript:') || anchor.target === '_blank') return;

        const url = new URL(href, window.location.origin);
        if (url.origin !== window.location.origin) return;
        if (href.includes('/api/') || href.includes('.pdf')) return;

        e.preventDefault();
        navigate(href);
    });

    window.addEventListener('popstate', (e) => {
        navigate(window.location.pathname, true);
    });
});
