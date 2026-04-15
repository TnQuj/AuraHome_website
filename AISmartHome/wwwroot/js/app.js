// =========================================================
// 1. CÁC HÀM QUICK VIEW & ĐỔI ẢNH CHI TIẾT
// =========================================================

// Gắn vào window để đảm bảo HTML gọi được từ bên ngoài nếu dùng module
window.openQuickView = function (id) {
    const modal = document.getElementById('quickview-' + id);
    const content = document.getElementById('content-' + id); // Lấy thẻ chứa nội dung

    if (modal && content) {
        // 1. Hiện cái nền đen lên
        modal.classList.remove('hidden');
        modal.classList.add('flex');
        document.body.style.overflow = 'hidden'; // Khóa cuộn trang

        // 2. Chờ 10ms rồi gỡ tàng hình cho nội dung bay ra mượt mà
        setTimeout(() => {
            content.classList.remove('scale-95', 'opacity-0');
            content.classList.add('scale-100', 'opacity-100');
        }, 10);
    }
}

window.closeQuickView = function (id) {
    const modal = document.getElementById('quickview-' + id);
    const content = document.getElementById('content-' + id);

    if (modal && content) {
        // 1. Làm tàng hình và thu nhỏ nội dung trước
        content.classList.add('scale-95', 'opacity-0');
        content.classList.remove('scale-100', 'opacity-100');
        document.body.style.overflow = 'auto'; // Mở lại cuộn trang

        // 2. Chờ 300ms (bằng thời gian CSS transition) rồi mới giấu hẳn nền đen đi
        setTimeout(() => {
            modal.classList.add('hidden');
            modal.classList.remove('flex');
        }, 300);
    }
}

window.changeMainImage = function (productId, newSrc, thumbnailElement) {
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
    function updateCartUI(productId) {
        // 1. Lấy số lượng mới từ ô input
        const input = document.getElementById(`qty-${productId}`);
    const quantity = parseInt(input.value);

    // 2. Lấy đơn giá (đã lưu trong data-price)
    const unitPriceEl = input.closest('.group').querySelector('.js-unit-price');
    const unitPrice = parseFloat(unitPriceEl.getAttribute('data-price'));

    // 3. Tính thành tiền mới cho sản phẩm này
    const subtotalEl = document.getElementById(`subtotal-${productId}`);
    const newSubtotal = quantity * unitPrice;

    // 4. Hiển thị thành tiền mới (định dạng VNĐ)
    subtotalEl.innerText = newSubtotal.toLocaleString('vi-VN') + " ₫";

    // 5. Tính lại Tổng thanh toán cuối cùng
    calculateTotal();
    }

function calculateTotal() {
    let totalMoney = 0;
    let totalQty = 0;

    // 1. Duyệt qua tất cả các input số lượng để tính tổng sản phẩm
    document.querySelectorAll('input[id^="qty-"]').forEach(input => {
        totalQty += parseInt(input.value) || 0;
    });

    // 2. Duyệt qua tất cả thành tiền từng món để tính tổng tiền
    document.querySelectorAll('.js-subtotal').forEach(el => {
        const value = parseInt(el.innerText.replace(/[^\d]/g, '')) || 0;
        totalMoney += value;
    });

    // 3. Cập nhật lên giao diện
    const totalQtyEl = document.getElementById('total-quantity');
    const totalMoneyEl = document.getElementById('cart-total'); // Tổng cuối
    const summaryMoneyEl = document.getElementById('subtotal-summary'); // Chỗ Tạm tính

    if (totalQtyEl) totalQtyEl.innerText = totalQty;
    if (totalMoneyEl) totalMoneyEl.innerText = totalMoney.toLocaleString('vi-VN') + " ₫";
    if (summaryMoneyEl) summaryMoneyEl.innerText = totalMoney.toLocaleString('vi-VN') + " ₫";
}

// --- GIỮ NGUYÊN HÀM updateCartUI và calculateTotal CỦA BẠN ---

function increaseQuantity(productId) {
    const input = document.getElementById(`qty-${productId}`);
    let newQty = parseInt(input.value) + 1;
    input.value = newQty;

    // Cập nhật giao diện
    updateCartUI(productId);
    calculateTotal();

    // LƯU VÀO DATABASE (MỚI THÊM)
    updateCartOnServer(productId, newQty);
}

function decreaseQuantity(productId) {
    const input = document.getElementById(`qty-${productId}`);
    let currentQty = parseInt(input.value);

    if (currentQty > 1) {
        let newQty = currentQty - 1;
        input.value = newQty;

        // Cập nhật giao diện
        updateCartUI(productId);
        calculateTotal();

        // LƯU VÀO DATABASE (MỚI THÊM)
        updateCartOnServer(productId, newQty);
    }
}

// HÀM MỚI: Gửi dữ liệu ngầm về Controller C#
function updateCartOnServer(productId, quantity) {
    fetch('/Customers/UpdateCart', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            productId: parseInt(productId),
            quantity: quantity
        })
    })
        .then(response => response.json())
        .then(data => {
            if (!data.success) {
                alert("Lỗi khi lưu giỏ hàng: " + data.message);
            }
        })
        .catch(error => {
            console.error("Lỗi:", error);
        });
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


// =========================================================
// PHẦN XỬ LÝ SỐ LƯỢNG CHO TRANG CHI TIẾT SẢN PHẨM (DETAILS)
// =========================================================

function updateDetailPrice(qty) {
    const input = document.getElementById('detail-qty');
    // Nếu không tìm thấy thẻ input này (tức là đang ở trang khác) thì dừng lại
    if (!input) return;

    const price = parseFloat(input.getAttribute('data-price')) || 0;
    const total = qty * price;

    const subtotalEl = document.getElementById('detail-subtotal');
    if (subtotalEl) {
        subtotalEl.innerText = total.toLocaleString('vi-VN') + ' ₫';
    }
}

function increaseDetailQty() {
    const input = document.getElementById('detail-qty');
    if (!input) return;

    let currentQty = parseInt(input.value) || 1;
    let newQty = currentQty + 1;

    input.value = newQty;
    updateDetailPrice(newQty);
}

function decreaseDetailQty() {
    const input = document.getElementById('detail-qty');
    if (!input) return;

    let currentQty = parseInt(input.value) || 1;

    if (currentQty > 1) {
        let newQty = currentQty - 1;
        input.value = newQty;
        updateDetailPrice(newQty);
    }
}


// =========================================================
// PHẦN XỬ LÝ YÊU THÍCH SẢN PHẨM
// =========================================================

function toggleFavorite(id, name, price, image) {
    let favs = JSON.parse(localStorage.getItem('favorite_products')) || [];
    const index = favs.findIndex(x => x.id === id);

    if (index > -1) {
        // Đã có -> Xóa đi
        favs.splice(index, 1);
        Swal.fire({ toast: true, position: 'top-end', showConfirmButton: false, timer: 1500, icon: 'info', title: 'Đã bỏ yêu thích!' });
    } else {
        // Chưa có -> Thêm vào
        favs.push({ id, name, price, image });
        Swal.fire({ toast: true, position: 'top-end', showConfirmButton: false, timer: 1500, icon: 'success', title: 'Đã thêm vào yêu thích!' });
    }

    // Lưu lại bộ nhớ và cập nhật giao diện
    localStorage.setItem('favorite_products', JSON.stringify(favs));
    renderFavorites();
    updateHeartIcons();
}

// 2. Hàm Đổ dữ liệu ra Sidebar
function renderFavorites() {
    let favs = JSON.parse(localStorage.getItem('favorite_products')) || [];
    const container = document.getElementById('favorite-products-list');
    const clearBtn = document.getElementById('clear-fav-container');

    if (!container) return;

    if (favs.length === 0) {
        container.innerHTML = '<p class="text-sm text-slate-400 italic py-4 text-center">Bạn chưa có sản phẩm yêu thích nào. Hãy thả tim nhé!</p>';
        if (clearBtn) clearBtn.classList.add('hidden');
        return;
    }

    if (clearBtn) clearBtn.classList.remove('hidden');
    let html = '';

    // Vẽ từng sản phẩm ra
    favs.forEach(sp => {
        // 👇 FIX LỖI GIÁ TIỀN TẠI ĐÂY: Dùng parseFloat để lấy đúng số (ví dụ 200000.00 -> 200000)
        let parsedPrice = parseFloat(sp.price);
        let priceFormatted = isNaN(parsedPrice) ? "0 đ" : parsedPrice.toLocaleString('vi-VN') + ' đ';

        let imgSrc = sp.image && sp.image !== 'null' && sp.image !== '' ? '/img/' + sp.image : '/img/placeholder.jpg';

        html += `
                <div class="py-3 border-b border-slate-100 last:border-0 group relative animate-fade-in">
                    <div class="flex gap-3">
                        <div class="w-14 shrink-0">
                            <a href="/SanPhams/Details/${sp.id}" class="w-14 h-14 bg-slate-50 rounded-xl p-1 border border-slate-100 block hover:border-rose-400 transition-colors">
                                <img src="${imgSrc}" class="w-full h-full object-contain" onerror="this.src='/img/placeholder.jpg'">
                            </a>
                        </div>
                        <div class="flex-1 min-w-0 flex flex-col justify-center">
                            <a href="/SanPhams/Details/${sp.id}" class="text-[13px] font-bold text-brand-navy line-clamp-2 leading-tight hover:text-rose-500 transition-colors">
                                ${sp.name}
                            </a>
                            <div class="text-sm font-black text-rose-500 mt-1">${priceFormatted}</div>
                        </div>
                        <button onclick="toggleFavorite('${sp.id}', '${sp.name.replace(/'/g, "\\'")}', '${sp.price}', '${sp.image}')" class="absolute top-1/2 -translate-y-1/2 right-0 w-6 h-6 bg-white border border-slate-200 hover:bg-rose-500 text-slate-400 hover:text-white rounded-full flex items-center justify-center opacity-0 group-hover:opacity-100 transition-all shadow-sm">
                            <i class="fa-solid fa-xmark text-[10px]"></i>
                        </button>
                    </div>
                </div>
                `;
    });
    container.innerHTML = html;
}

// 3. Hàm Đổi màu Nút Tim trên trang (Đỏ nếu đã thích)
function updateHeartIcons() {
    let favs = JSON.parse(localStorage.getItem('favorite_products')) || [];
    let favIds = favs.map(x => x.id.toString());

    document.querySelectorAll('[class*="fav-btn-"]').forEach(btn => {
        btn.classList.remove('text-rose-500', 'shadow-md');
        btn.classList.add('text-slate-400');

        let classes = btn.className.split(' ');
        let idClass = classes.find(c => c.startsWith('fav-btn-'));
        if (idClass) {
            let id = idClass.replace('fav-btn-', '');
            if (favIds.includes(id)) {
                btn.classList.remove('text-slate-400');
                btn.classList.add('text-rose-500', 'shadow-md');
            }
        }
    });
}

// 4. Hàm Xóa tất cả
function clearFavorites() {
    localStorage.removeItem('favorite_products');
    renderFavorites();
    updateHeartIcons();
}

// Tự động chạy khi load trang
document.addEventListener("DOMContentLoaded", function () {
    renderFavorites();
    updateHeartIcons();
});
