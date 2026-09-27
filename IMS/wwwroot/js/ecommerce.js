/* E-Commerce Pharmacy Store - JavaScript */

// Header scroll effect
window.addEventListener('scroll', function () {
    const header = document.getElementById('ec-header');
    if (header) {
        header.classList.toggle('scrolled', window.scrollY > 20);
    }
});

// Toast notification
function showToast(message, isError) {
    const toast = document.getElementById('ec-toast');
    const msgEl = document.getElementById('ec-toast-message');
    if (!toast || !msgEl) return;

    const icon = toast.querySelector('i');
    if (isError) {
        icon.className = 'fas fa-exclamation-circle';
        icon.style.color = '#f87171';
    } else {
        icon.className = 'fas fa-check-circle';
        icon.style.color = '#4ade80';
    }

    msgEl.textContent = message;
    toast.classList.add('show');

    setTimeout(function () {
        toast.classList.remove('show');
    }, 3000);
}

// Update cart badge
function updateCartBadge(count) {
    var badge = document.getElementById('cart-badge');
    if (badge) {
        badge.textContent = count;
        badge.style.animation = 'none';
        badge.offsetHeight; // trigger reflow
        badge.style.animation = 'badgePop 0.3s ease';
    }
}

// Add to cart (from product grid or detail page)
function addToCart(productId, event, quantity) {
    if (event) event.preventDefault();
    var qty = quantity || 1;

    $.post('/ECommerce/Cart/AddToCart', { productId: productId, quantity: qty }, function (data) {
        if (data.success) {
            showToast(data.message);
            updateCartBadge(data.cartCount);
        } else {
            showToast(data.message, true);
        }
    }).fail(function () {
        showToast('حدث خطأ أثناء الإضافة', true);
    });
}

// Update cart quantity
function updateCartQty(productId, newQty) {
    if (newQty < 1) {
        removeCartItem(productId);
        return;
    }

    $.post('/ECommerce/Cart/UpdateQuantity', { productId: productId, quantity: newQty }, function (data) {
        if (data.success) {
            updateCartBadge(data.cartCount);
            // Reload cart page for simplicity
            location.reload();
        }
    }).fail(function () {
        showToast('حدث خطأ أثناء التحديث', true);
    });
}

// Remove cart item
function removeCartItem(productId) {
    $.post('/ECommerce/Cart/RemoveItem', { productId: productId }, function (data) {
        if (data.success) {
            updateCartBadge(data.cartCount);
            var item = document.getElementById('cart-item-' + productId);
            if (item) {
                item.style.transition = 'all 0.3s ease';
                item.style.opacity = '0';
                item.style.transform = 'translateX(50px)';
                setTimeout(function () {
                    item.remove();
                    // Update summary
                    var summaryTotal = document.getElementById('summary-total');
                    var summaryGrand = document.getElementById('summary-grand');
                    if (summaryTotal) summaryTotal.textContent = data.cartTotal.toFixed(2) + ' ج.م';
                    if (summaryGrand) summaryGrand.textContent = data.cartTotal.toFixed(2) + ' ج.م';

                    // If cart is empty, reload page
                    if (data.cartCount === 0) location.reload();
                }, 300);
            }
        }
    }).fail(function () {
        showToast('حدث خطأ أثناء الحذف', true);
    });
}

// Mobile Menu Toggling
document.addEventListener('DOMContentLoaded', function () {
    const mobileMenuBtn = document.getElementById('mobile-menu-btn');
    const closeMobileNav = document.getElementById('close-mobile-nav');
    const mobileNav = document.getElementById('mobile-nav');
    const mobileOverlay = document.getElementById('mobile-overlay');

    function toggleMenu() {
        mobileNav.classList.toggle('open');
        mobileOverlay.classList.toggle('show');
        document.body.style.overflow = mobileNav.classList.contains('open') ? 'hidden' : '';
    }

    if (mobileMenuBtn) mobileMenuBtn.addEventListener('click', toggleMenu);
    if (closeMobileNav) closeMobileNav.addEventListener('click', toggleMenu);
    if (mobileOverlay) mobileOverlay.addEventListener('click', toggleMenu);

    // Close menu on link click (for same-page anchors if any)
    document.querySelectorAll('.ec-mobile-link').forEach(link => {
        link.addEventListener('click', () => {
            if (mobileNav.classList.contains('open')) toggleMenu();
        });
    });

    // Intersection Observer for product cards
    var observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.style.opacity = '1';
                entry.target.style.transform = 'translateY(0)';
                observer.unobserve(entry.target);
            }
        });
    }, { threshold: 0.1 });

    document.querySelectorAll('.ec-product-card').forEach(function (card) {
        card.style.opacity = '0';
        card.style.transform = 'translateY(20px)';
        card.style.transition = 'all 0.5s cubic-bezier(0.4, 0, 0.2, 1)';
        observer.observe(card);
    });
});
