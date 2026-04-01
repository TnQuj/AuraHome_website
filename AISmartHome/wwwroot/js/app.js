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

// Hàm Xóa sản phẩm ngầm (AJAX)
async function removeFromCartAjax(id, event) {
    if (event) event.preventDefault();

    const formData = new FormData();
    formData.append("id", id);

    try {
        const response = await fetch('/Customers/RemoveFromCartAjax', {
            method: 'POST',
            body: formData
        });

        if (response.ok) { // 200 OK sẽ chui vào đây
            const result = await response.json();
            if (result.success) {
                renderMiniCart(result); // Vẽ lại giỏ hàng (sản phẩm sẽ biến mất)
            }
        }
    } catch (error) {
        console.error('Lỗi khi xóa sản phẩm:', error);
    }
}

// Hàm vẽ lại Giỏ hàng trượt khi có dữ liệu mới
function renderMiniCart(data) {
    const container = document.getElementById('mini-cart-items');
    const totalEl = document.getElementById('mini-cart-total');
    const badge = document.getElementById('cart-badge');

    // 1. Nếu giỏ hàng trống
    if (!data || !data.items || data.items.length === 0) {
        container.innerHTML = `
            <div class="flex flex-col items-center justify-center h-full text-slate-400 gap-3 opacity-70 mt-10">
                <i class="fa-solid fa-cart-shopping text-5xl"></i>
                <p class="text-sm font-medium">Giỏ hàng của bạn đang trống</p>
            </div>`;
        totalEl.innerText = '0 ₫';
        if (badge) badge.classList.add('hidden'); // Ẩn chấm đỏ
        return;
    }

    // 2. Nếu có sản phẩm thì vẽ danh sách
    let html = '';
    data.items.forEach(item => {
        html += `
            <div class="flex items-center gap-4 bg-slate-50/50 p-3 rounded-2xl border border-slate-100 relative group">
                
                <div class="w-16 h-16 rounded-xl bg-white border border-slate-100 overflow-hidden shrink-0 flex items-center justify-center p-1">
                    <img src="${item.hinhAnh}" alt="${item.tenSanPham}" class="w-full h-full object-cover rounded-lg" onerror="this.src='https://via.placeholder.com/150'">
                </div>
                
                <div class="flex-1 min-w-0 pr-6">
                    <h4 class="text-[13px] font-bold text-slate-800 leading-snug truncate mb-1">${item.tenSanPham}</h4>
                    <div class="text-[12px] text-slate-500 font-medium">
                        ${item.soLuong} × <span class="text-brand-cyan font-bold">${item.gia.toLocaleString('vi-VN')} ₫</span>
                    </div>
                </div>

                <button type="button" onclick="removeFromCartAjax(${item.maSanPham}, event)" class="absolute right-3 top-1/2 -translate-y-1/2 w-8 h-8 flex items-center justify-center rounded-full bg-white text-slate-400 hover:text-rose-500 hover:bg-rose-50 hover:shadow-sm border border-slate-100 transition-all opacity-0 group-hover:opacity-100">
                    <i class="fa-solid fa-xmark text-sm"></i>
                </button>

            </div>
        `;
    });

    // Cập nhật HTML danh sách sản phẩm
    container.innerHTML = html;

    // Cập nhật Tổng tiền
    totalEl.innerText = data.totalPrice.toLocaleString('vi-VN') + ' ₫';

    // Cập nhật Chấm đỏ trên Header
    if (badge) {
        badge.innerText = data.totalItems;
        badge.classList.remove('hidden');
    }
}
// Hàm Mở Giỏ Hàng Trượt
async function openMiniCart() {
    const overlay = document.getElementById('mini-cart-overlay');
    const panel = document.getElementById('mini-cart-panel');

    if (!overlay || !panel) return;

    // 1. Mở giao diện ra trước cho mượt (Hiệu ứng)
    overlay.classList.remove('hidden');
    // Đợi 10ms để Tailwind kích hoạt hiệu ứng Transition
    setTimeout(() => {
        overlay.classList.remove('opacity-0');
        panel.classList.remove('translate-x-full');
    }, 10);

    // 2. GỌI AJAX LẤY DỮ LIỆU TỪ SERVER ĐẮP VÀO GIAO DIỆN
    try {
        const response = await fetch('/Customers/GetMiniCartAjax');
        if (response.ok) {
            const result = await response.json();
            if (result.success) {
                // Gọi hàm vẽ lại danh sách sản phẩm (Hàm này bạn đã có sẵn rồi)
                renderMiniCart(result);
            }
        }
    } catch (error) {
        console.error('Lỗi khi lấy dữ liệu giỏ hàng:', error);
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

// =========================================================
// CHUYỂN ĐỔI CHẾ ĐỘ XEM (GRID / LIST)
// =========================================================
function changeViewMode(mode) {
    const container = document.getElementById('product-list-container');
    const btnGrid = document.getElementById('btn-grid');
    const btnList = document.getElementById('btn-list');

    if (!container || !btnGrid || !btnList) return;

    // Lấy tất cả các thẻ Card sản phẩm
    const cards = container.querySelectorAll('.group');

    if (mode === 'list') {
        // --- CHUYỂN SANG DẠNG DANH SÁCH (LIST) ---
        // 1. Đổi container thành 1 cột (hoặc 2 cột cho màn hình to)
        container.className = 'grid grid-cols-1 lg:grid-cols-2 gap-6';

        // 2. Bật màu cho nút List, tắt màu nút Grid
        btnList.classList.add('bg-white', 'dark:bg-slate-700', 'text-cyan-500', 'shadow-sm');
        btnList.classList.remove('text-slate-400');
        btnGrid.classList.remove('bg-white', 'dark:bg-slate-700', 'text-cyan-500', 'shadow-sm');
        btnGrid.classList.add('text-slate-400');

        // 3. Xoay ngang thẻ sản phẩm
        cards.forEach(card => {
            card.classList.remove('flex-col');
            card.classList.add('flex-row', 'items-center');

            // Chỉnh lại kích thước phần chứa hình ảnh cho gọn lại
            const imgBox = card.firstElementChild;
            imgBox.classList.add('w-2/5', 'sm:w-1/3', 'shrink-0', 'border-r', 'border-slate-100', 'dark:border-slate-700');
        });

        // 4. Lưu tùy chọn vào LocalStorage (F5 không bị mất)
        localStorage.setItem('userViewMode', 'list');

    } else {
        // --- CHUYỂN SANG DẠNG LƯỚI (GRID) ---
        // 1. Đổi container về 4 cột mặc định
        container.className = 'grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6';

        // 2. Bật màu nút Grid, tắt màu nút List
        btnGrid.classList.add('bg-white', 'dark:bg-slate-700', 'text-cyan-500', 'shadow-sm');
        btnGrid.classList.remove('text-slate-400');
        btnList.classList.remove('bg-white', 'dark:bg-slate-700', 'text-cyan-500', 'shadow-sm');
        btnList.classList.add('text-slate-400');

        // 3. Xoay dọc thẻ sản phẩm lại
        cards.forEach(card => {
            card.classList.add('flex-col');
            card.classList.remove('flex-row', 'items-center');

            const imgBox = card.firstElementChild;
            imgBox.classList.remove('w-2/5', 'sm:w-1/3', 'shrink-0', 'border-r', 'border-slate-100', 'dark:border-slate-700');
        });

        localStorage.setItem('userViewMode', 'grid');
    }
}

// Tự động khôi phục chế độ xem khi vừa load trang
document.addEventListener('DOMContentLoaded', () => {
    const savedMode = localStorage.getItem('userViewMode');
    if (savedMode === 'list') {
        changeViewMode('list');
    }
});
