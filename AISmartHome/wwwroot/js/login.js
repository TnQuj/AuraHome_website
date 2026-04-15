document.addEventListener("DOMContentLoaded", function () {
    // 1. Kiểm tra trạng thái "Đã đăng nhập" từ Trình duyệt
    const savedName = localStorage.getItem('savedGuestName');
    const savedPhone = localStorage.getItem('savedGuestPhone');

    const accountBtn = document.getElementById('headerAccountBtn');
    const accountText = document.getElementById('headerAccountText');
    const dropdown = document.getElementById('customerInfoDropdown');

    if (savedName && savedPhone) {
        // NẾU ĐÃ XÁC THỰC:
        if (accountText) accountText.innerText = savedName;
        if (accountBtn) accountBtn.removeAttribute('onclick');

        // --- LOGIC MỚI: CHE SỐ ĐIỆN THOẠI (MASKING) ---
        // Lấy 4 số đầu + "***" + 3 số cuối (VD: 0972115030 -> 0972***030)
        let maskedPhone = savedPhone;
        if (savedPhone.length >= 9) {
            maskedPhone = savedPhone.substring(0, 4) + 'xxx' + savedPhone.substring(savedPhone.length - 3);
        }

        // Điền Tên và SĐT (đã che) vào Dropdown Menu
        document.getElementById('dropdownName').innerText = savedName;
        document.getElementById('dropdownPhone').innerText = maskedPhone; // Hiển thị số đã che

        if (dropdown) dropdown.classList.remove('hidden');

    } else {
        // NẾU CHƯA XÁC THỰC: 
        if (dropdown) dropdown.classList.add('hidden');
    }
});

// Hàm Đăng xuất (Xóa thông tin để đổi SĐT khác)
// Hàm Đăng xuất (Xóa thông tin và tạo giỏ hàng mới)
function logoutGuest() {
    localStorage.removeItem('favorite_products');
    Swal.fire({
        title: 'Bạn muốn đăng xuất?',
        text: "Hệ thống sẽ làm mới giỏ hàng. Lần sau bạn chỉ cần nhập lại Số điện thoại là lấy lại được giỏ hàng cũ.",
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#06b6d4',
        cancelButtonColor: '#cbd5e1',
        confirmButtonText: 'Đăng xuất',
        cancelButtonText: 'Hủy'
    }).then((result) => {
        if (result.isConfirmed) {
            // 1. Xóa dữ liệu hiển thị ở Trình duyệt
            localStorage.removeItem('savedGuestName');
            localStorage.removeItem('savedGuestPhone');
            localStorage.removeItem('guestInfoSubmitted');

            // 2. GỌI API XÓA COOKIE TRÊN MÁY CHỦ ĐỂ CẮT ĐỨT GIỎ HÀNG CŨ
            fetch('/Customers/LogoutAjax', { method: 'POST' })
                .then(() => {
                    // Tải lại trang web (Lúc này web sẽ tự tạo 1 mã Cookie mới tinh, giỏ hàng trống trơn)
                    window.location.href = '/Customers/Index';
                });
        }
    });
}

let lookupStep = 1; // Bước 1: Nhập SĐT, Bước 2: Nhập OTP
let lookupTimerInterval;

// Hàm mở Modal (Gắn vào nút Icon trên thanh Header)
function openLookupModal() {
    const modal = document.getElementById('customerLookupModal');
    const content = document.getElementById('customerLookupContent');

    // Reset form mỗi khi mở lại
    lookupStep = 1;
    document.getElementById('lookupOtpContainer').classList.add('hidden');
    document.getElementById('lookupOtpInput').value = '';
    document.getElementById('btnLookupSubmit').innerHTML = 'Nhận mã OTP <span class="material-symbols-outlined text-[18px]">send</span>';

    modal.classList.remove('hidden');
    modal.classList.add('flex');
    setTimeout(() => {
        modal.classList.remove('opacity-0');
        content.classList.remove('scale-95');
    }, 10);
}

// Hàm đóng Modal
function closeLookupModal() {
    const modal = document.getElementById('customerLookupModal');
    const content = document.getElementById('customerLookupContent');
    modal.classList.add('opacity-0');
    content.classList.add('scale-95');
    setTimeout(() => {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }, 300);
}

// Hàm xử lý khi bấm nút Xanh đen
function handleLookupSubmit() {
    const phone = document.getElementById('lookupPhoneInput').value.trim();
    const name = document.getElementById('lookupNameInput').value.trim(); // Lấy tên
    const btn = document.getElementById('btnLookupSubmit');

    if (!name) {
        Swal.fire('Chú ý', 'Vui lòng nhập tên của bạn để chúng tôi tiện xưng hô nhé.', 'warning');
        return;
    }
    if (!phone || phone.length < 9) {
        Swal.fire('Chú ý', 'Vui lòng nhập số điện thoại hợp lệ.', 'warning');
        return;
    }

    if (lookupStep === 1) {
        btn.disabled = true;
        btn.innerHTML = '<span class="material-symbols-outlined animate-spin">sync</span> Đang gửi...';

        fetch('/api/Otp/send', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ phone: phone })
        })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    lookupStep = 2;
                    document.getElementById('lookupOtpContainer').classList.remove('hidden');

                    // 🔔 HIỆN HỘP THOẠI THÔNG BÁO MÃ OTP (Mô phỏng tin nhắn SMS)
                    if (data.otp) {
                        Swal.fire({
                            title: '🔔 TIN NHẮN TỪ AURAHOME',
                            html: `Mã xác nhận OTP của bạn là: <b class="text-2xl text-cyan-600 tracking-widest">${data.otp}</b><br><br><i class="text-sm text-slate-500">(Hãy nhập mã này vào ô xác nhận)</i>`,
                            icon: 'info',
                            confirmButtonText: 'Đã hiểu'
                        });
                        // Chú ý: Ta không tự điền nữa để khách trải nghiệm việc tự gõ mã từ hộp thoại vào
                    }

                    btn.innerHTML = 'Xác nhận Khôi phục <span class="material-symbols-outlined text-[18px]">check_circle</span>';
                    btn.disabled = false;

                    // ... (Đoạn code đếm ngược 60s giữ nguyên) ...
                    let timeLeft = 60;
                    clearInterval(lookupTimerInterval);
                    lookupTimerInterval = setInterval(() => {
                        if (timeLeft <= 0) {
                            clearInterval(lookupTimerInterval);
                            document.getElementById('lookupOtpTimer').innerText = '';
                            lookupStep = 1;
                            btn.innerHTML = 'Gửi lại mã OTP';
                        } else {
                            document.getElementById('lookupOtpTimer').innerText = `${timeLeft}s`;
                            timeLeft--;
                        }
                    }, 1000);

                } else {
                    Swal.fire('Lỗi', data.message, 'error');
                    btn.disabled = false;
                    btn.innerHTML = 'Nhận mã OTP';
                }
            });
    }
    else if (lookupStep === 2) {
        const otp = document.getElementById('lookupOtpInput').value.trim();
        if (!otp || otp.length !== 6) {
            Swal.fire('Chú ý', 'Vui lòng nhập đủ 6 số OTP.', 'warning');
            return;
        }

        btn.disabled = true;
        btn.innerHTML = '<span class="material-symbols-outlined animate-spin">sync</span> Đang xử lý...';

        // Gọi API khôi phục giỏ hàng và gửi kèm TÊN lên C#
        fetch(`/Customers/RestoreCartAjax?phone=${encodeURIComponent(phone)}&otp=${encodeURIComponent(otp)}&name=${encodeURIComponent(name)}`, { method: 'POST' })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    // LƯU TÊN VÀ SĐT VÀO TRÌNH DUYỆT ĐỂ ĐỔI LỜI CHÀO TRÊN HEADER
                    localStorage.setItem('savedGuestName', name);
                    localStorage.setItem('savedGuestPhone', phone);

                    closeLookupModal();

                    // LỜI CHÀO KHI ĐĂNG NHẬP THÀNH CÔNG
                    Swal.fire({
                        icon: 'success',
                        title: `Xin chào, ${name}!`,
                        text: 'Xác thực thành công. Đang chuyển đến giỏ hàng...',
                        timer: 2000,
                        showConfirmButton: false
                    }).then(() => {
                        window.location.href = '/Customers/Cart';
                    });
                } else {
                    Swal.fire('Lỗi', data.message, 'error');
                    btn.disabled = false;
                    btn.innerHTML = 'Xác nhận Khôi phục';
                }
            });
    }
}