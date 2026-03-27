// =========================================================
// 1. CÁC HÀM QUICK VIEW & ĐỔI ẢNH CHI TIẾT
// =========================================================
function openQuickView(id) {
    const modal = document.getElementById('quickview-' + id);
    if (modal) {
        modal.classList.remove('hidden');
        modal.classList.add('flex');
        document.body.style.overflow = 'hidden';
    }
}

function closeQuickView(id) {
    const modal = document.getElementById('quickview-' + id);
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
        document.body.style.overflow = 'auto';
    }
}

function changeMainImage(productId, newSrc, thumbnailElement) {
    const mainImg = document.getElementById('main-img-' + productId) || document.getElementById('main-product-img');
    if (mainImg) {
        mainImg.style.opacity = '0.5';
        setTimeout(() => {
            mainImg.src = newSrc;
            mainImg.style.opacity = '1';
        }, 100);
    }

    const allThumbs = document.querySelectorAll('.qv-thumb-' + productId + ', .detail-thumb');
    allThumbs.forEach(thumb => {
        thumb.classList.remove('border-[#2b3a82]', 'dark:border-indigo-400', 'border-brand-cyan', 'shadow-md');
        thumb.classList.add('border-transparent');
    });

    if (thumbnailElement) {
        thumbnailElement.classList.remove('border-transparent');
        thumbnailElement.classList.add('border-[#2b3a82]', 'dark:border-indigo-400', 'border-brand-cyan', 'shadow-md');
    }
}

// =========================================================
// 2. BANNER SLIDER (ĐÃ CHỐNG LỖI TRANG KHÔNG CÓ BANNER)
// =========================================================
const bannerImages = [
    "https://images.unsplash.com/photo-1558002038-1055907df827?q=80&w=2070&auto=format&fit=crop",
    "https://images.unsplash.com/photo-1584438784894-089d6a62b8fa?q=80&w=2070&auto=format&fit=crop",
    "https://images.unsplash.com/photo-1518770660439-4636190af475?q=80&w=2070&auto=format&fit=crop"
];

let currentBannerIndex = 0;
let autoSlideInterval;

function updateBannerView() {
    const imgElement = document.getElementById('banner-img');
    const dotsContainer = document.getElementById('banner-dots');

    if (!imgElement || !dotsContainer) return;

    imgElement.style.opacity = '0';
    setTimeout(() => {
        imgElement.src = bannerImages[currentBannerIndex];
        imgElement.style.opacity = '0.4';
    }, 250);

    dotsContainer.innerHTML = '';
    bannerImages.forEach((_, index) => {
        const dot = document.createElement('span');
        if (index === currentBannerIndex) {
            dot.className = 'w-6 h-2 rounded-full bg-cyan-500 transition-all duration-300 cursor-pointer';
        } else {
            dot.className = 'w-2 h-2 rounded-full bg-white/30 hover:bg-white/50 transition-all duration-300 cursor-pointer';
        }
        dot.onclick = () => goToBanner(index);
        dotsContainer.appendChild(dot);
    });
}

function nextBanner() {
    currentBannerIndex = (currentBannerIndex + 1) % bannerImages.length;
    updateBannerView();
    resetAutoSlide();
}

function prevBanner() {
    currentBannerIndex = (currentBannerIndex - 1 + bannerImages.length) % bannerImages.length;
    updateBannerView();
    resetAutoSlide();
}

function goToBanner(index) {
    currentBannerIndex = index;
    updateBannerView();
    resetAutoSlide();
}

function startAutoSlide() {
    autoSlideInterval = setInterval(nextBanner, 5000);
}

function resetAutoSlide() {
    if (autoSlideInterval) clearInterval(autoSlideInterval);
    startAutoSlide();
}

document.addEventListener('DOMContentLoaded', () => {
    if (document.getElementById('banner-img')) {
        updateBannerView();
        startAutoSlide();
    }
});

// =========================================================
// 3. XỬ LÝ SỐ LƯỢNG TRONG GIỎ HÀNG / TRANG CHI TIẾT
// =========================================================
function decreaseQuantity(productId = '') {
    // Nếu có truyền ID thì tìm theo ID, không thì dùng mặc định
    const inputId = productId ? 'qty-' + productId : 'quantity-input';
    const input = document.getElementById(inputId);
    if (input) {
        let currentValue = parseInt(input.value);
        if (currentValue > 1) {
            input.value = currentValue - 1;
        }
    }
}

function increaseQuantity(productId = '') {
    const inputId = productId ? 'qty-' + productId : 'quantity-input';
    const input = document.getElementById(inputId);
    if (input) {
        let currentValue = parseInt(input.value);
        input.value = currentValue + 1;
    }
}

// =========================================================
// 4. GIỎ HÀNG TRƯỢT (OFF-CANVAS MINI CART) BẰNG AJAX
// =========================================================
async function addToCartAjax(event) {
    event.preventDefault();
    const form = event.target;
    const formData = new FormData(form);

    try {
        const response = await fetch('/Customers/AddToCartAjax', {
            method: 'POST',
            body: formData
        });
        const result = await response.json();
        if (result.success) {
            renderMiniCart(result);
            openMiniCart();
        } else {
            alert(result.message);
        }
    } catch (error) {
        console.error('Lỗi khi thêm vào giỏ:', error);
    }
}

async function removeFromCartAjax(id, event) {
    event.preventDefault();
    const formData = new FormData();
    formData.append("id", id);

    try {
        const response = await fetch('/Customers/RemoveFromCartAjax', {
            method: 'POST',
            body: formData
        });
        const result = await response.json();
        if (result.success) {
            renderMiniCart(result);
        }
    } catch (error) {
        console.error('Lỗi khi xóa:', error);
    }
}

function renderMiniCart(data) {
    const container = document.getElementById('mini-cart-items');
    if (!container) return;

    container.innerHTML = '';
    data.items.forEach(item => {
        const html = `
            <div class="flex gap-4 items-center border-b border-slate-100 pb-4 relative group">
                <img src="${item.hinhAnh}" class="w-20 h-20 object-contain bg-slate-50 rounded-lg p-2 border border-slate-100" />
                <div class="flex-1 pr-6">
                    <a href="/Customers/Details?id=${item.maSanPham}" class="text-[14px] font-medium text-brand-navy leading-snug line-clamp-2 hover:text-brand-cyan mb-1">${item.tenSanPham}</a>
                    <div class="text-sm text-slate-500">${item.soLuong} × <span class="font-bold text-brand-navy">${item.gia.toLocaleString('vi-VN')} ₫</span></div>
                </div>
                <a href="#" onclick="removeFromCartAjax(${item.maSanPham}, event)" class="absolute right-0 top-1/2 -translate-y-1/2 w-7 h-7 rounded-full border border-slate-200 flex items-center justify-center text-slate-400 hover:text-brand-red hover:border-brand-red transition-colors">
                    <i class="fa-solid fa-xmark text-xs"></i>
                </a>
            </div>
        `;
        container.insertAdjacentHTML('beforeend', html);
    });

    const totalEl = document.getElementById('mini-cart-total');
    if (totalEl) totalEl.innerText = data.totalPrice.toLocaleString('vi-VN') + ' ₫';

    const badge = document.getElementById('cart-badge');
    if (badge) {
        if (data.totalItems > 0) {
            badge.innerText = data.totalItems;
            badge.classList.remove('hidden');
        } else {
            badge.classList.add('hidden');
        }
    }
}

function openMiniCart() {
    const overlay = document.getElementById('mini-cart-overlay');
    const panel = document.getElementById('mini-cart-panel');
    if (overlay && panel) {
        overlay.classList.remove('hidden');
        setTimeout(() => {
            overlay.classList.remove('opacity-0');
            panel.classList.remove('translate-x-full');
        }, 10);
        document.body.style.overflow = 'hidden';
    }
}

function closeMiniCart() {
    const overlay = document.getElementById('mini-cart-overlay');
    const panel = document.getElementById('mini-cart-panel');
    if (overlay && panel) {
        overlay.classList.add('opacity-0');
        panel.classList.add('translate-x-full');
        setTimeout(() => {
            overlay.classList.add('hidden');
            document.body.style.overflow = '';
        }, 300);
    }
}