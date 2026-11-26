document.addEventListener("DOMContentLoaded", () => {
    // 1. Theme Persistence (Dark / Light)
    const root = document.documentElement;
    const savedTheme = localStorage.getItem("portfolio-theme") || "light";
    root.setAttribute("data-theme", savedTheme);

    const themeBtn = document.getElementById("themeToggle");
    if (themeBtn) {
        themeBtn.addEventListener("click", () => {
            const next = root.getAttribute("data-theme") === "dark" ? "light" : "dark";
            root.setAttribute("data-theme", next);
            localStorage.setItem("portfolio-theme", next);
        });
    }

    // 2. Scroll Reveal Animations
    const revealEls = document.querySelectorAll(".reveal");
    if ("IntersectionObserver" in window && revealEls.length > 0) {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (entry.isIntersecting) {
                    entry.target.classList.add("is-visible");
                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.12 });

        revealEls.forEach((el) => observer.observe(el));
    } else {
        revealEls.forEach((el) => el.classList.add("is-visible"));
    }

    // 3. Interactive Portfolio Category Filtering
    const filterButtons = document.querySelectorAll("[data-filter]");
    const projectCards = document.querySelectorAll("[data-category-slug]");
    const countBadge = document.getElementById("visibleProjectsCount");

    if (filterButtons.length > 0 && projectCards.length > 0) {
        filterButtons.forEach((btn) => {
            btn.addEventListener("click", () => {
                const target = btn.getAttribute("data-filter");

                filterButtons.forEach((b) => b.classList.remove("active"));
                btn.classList.add("active");

                let visibleCount = 0;
                projectCards.forEach((card) => {
                    const cardSlug = card.getAttribute("data-category-slug");
                    const show = target === "all" || cardSlug === target;
                    card.classList.toggle("is-hidden", !show);
                    if (show) {
                        visibleCount++;
                        card.classList.add("is-visible");
                    }
                });

                if (countBadge) {
                    countBadge.textContent = `${visibleCount} project${visibleCount === 1 ? "" : "s"}`;
                }
            });
        });
    }

    // 4. AJAX Contact Form Submission
    const contactForm = document.getElementById("contactForm");
    const contactFeedback = document.getElementById("contactFeedback");
    if (contactForm && contactFeedback) {
        contactForm.addEventListener("submit", async (e) => {
            e.preventDefault();

            const submitBtn = contactForm.querySelector('button[type="submit"]');
            const originalText = submitBtn ? submitBtn.innerHTML : "";
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Sending...';
            }

            try {
                const response = await fetch(contactForm.action, {
                    method: "POST",
                    headers: { "X-Requested-With": "XMLHttpRequest" },
                    body: new FormData(contactForm)
                });

                const data = await response.json();
                if (response.ok && data.success) {
                    contactFeedback.innerHTML = `<div class="alert alert-success-custom d-flex align-items-center gap-2"><i class="bi bi-check-circle-fill"></i><span>${data.message}</span></div>`;
                    contactForm.reset();
                } else {
                    const errList = (data.errors || ["Please check all fields and try again."]).join("<br>");
                    contactFeedback.innerHTML = `<div class="alert alert-danger-custom">${errList}</div>`;
                }
            } catch {
                contactForm.submit();
            } finally {
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = originalText;
                }
            }
        });
    }

    // 5. Case Study Gallery Lightbox Modal
    const lightboxModalEl = document.getElementById("galleryLightboxModal");
    if (lightboxModalEl && window.bootstrap) {
        const lightboxModal = new bootstrap.Modal(lightboxModalEl);
        const lightboxImg = document.getElementById("lightboxImage");
        const lightboxCaption = document.getElementById("lightboxCaption");

        document.querySelectorAll("[data-lightbox-src]").forEach((trigger) => {
            trigger.addEventListener("click", () => {
                if (lightboxImg) lightboxImg.src = trigger.getAttribute("data-lightbox-src") || "";
                if (lightboxCaption) lightboxCaption.textContent = trigger.getAttribute("data-lightbox-caption") || "";
                lightboxModal.show();
            });
        });
    }

    // 6. Admin Live Image Upload Preview
    document.querySelectorAll("[data-preview-target]").forEach((input) => {
        input.addEventListener("change", () => {
            const targetSelector = input.getAttribute("data-preview-target");
            const previewImg = targetSelector ? document.querySelector(targetSelector) : null;
            const file = input.files && input.files[0];
            if (previewImg && file) {
                const reader = new FileReader();
                reader.onload = (ev) => {
                    previewImg.src = ev.target?.result;
                    previewImg.classList.remove("d-none");
                };
                reader.readAsDataURL(file);
            }
        });
    });

    // 7. Close mobile navbar when clicking a section anchor
    const mainNav = document.getElementById("mainNav");
    if (mainNav && window.bootstrap) {
        mainNav.querySelectorAll("a.nav-link").forEach((link) => {
            link.addEventListener("click", () => {
                const bsCollapse = bootstrap.Collapse.getInstance(mainNav);
                if (bsCollapse) bsCollapse.hide();
            });
        });
    }
});
