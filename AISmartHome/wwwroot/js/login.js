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
function logoutGuest() {
    Swal.fire({
        title: 'Xác nhận đăng xuất?',
        text: "Mọi thông tin phiên làm việc và giỏ hàng tạm thời sẽ được làm mới.",
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#06b6d4',
        cancelButtonColor: '#cbd5e1',
        confirmButtonText: 'Đăng xuất ngay',
        cancelButtonText: 'Ở lại'
    }).then((result) => {
        if (result.isConfirmed) {
            // --- BƯỚC 1: QUÉT SẠCH LOCALSTORAGE ---
            localStorage.clear(); // Xóa sạch sành sanh tất cả (nhanh và an toàn nhất)

            // Hoặc nếu bạn muốn giữ lại một vài cài đặt khác, hãy xóa thủ công các key sau:
            // localStorage.removeItem('savedGuestName');
            // localStorage.removeItem('savedGuestPhone');
            // localStorage.removeItem('guestName');
            // localStorage.removeItem('guestPhone');
            // localStorage.removeItem('favorite_products');

            // --- BƯỚC 2: GỌI SERVER XÓA COOKIE ---
            fetch('/Customers/LogoutAjax', {
                method: 'POST',
                headers: {
                    // Thêm Header để tránh lỗi Request Verification nếu cần
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
                }
            })
                .then(response => {
                    // --- BƯỚC 3: RESET WEBSITE VỀ TRANG CHỦ ---
                    // Dùng location.replace để khách không "Back" lại trang cũ có dữ liệu được
                    window.location.replace('/');
                })
                .catch(err => {
                    console.error("Lỗi đăng xuất:", err);
                    window.location.reload();
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
//Mã opt dành cho đăng nhập trước
// Hàm xử lý khi bấm nút Xanh đen
function handleLookupSubmit() {
    // 👇👇👇 KHAI BÁO THÊM emailInput Ở ĐÂY 👇👇👇
    const phoneInput = document.getElementById('lookupPhoneInput').value;
    const nameInput = document.getElementById('lookupNameInput').value;
    const emailInput = document.getElementById('lookupEmailInput') ? document.getElementById('lookupEmailInput').value : '';
    const otpInput = document.getElementById('lookupOtpInput') ? document.getElementById('lookupOtpInput').value : '';
    const otpContainer = document.getElementById('lookupOtpContainer');
    const btnSubmit = document.getElementById('btnLookupSubmit');

    // =================================================================
    // BƯỚC 1: NẾU ĐANG ẨN OTP -> GỌI API GỬI MÃ
    // =================================================================
    if (otpContainer.classList.contains('hidden')) {
        if (!phoneInput || !nameInput || !emailInput) {
            Swal.fire('Lỗi', 'Vui lòng nhập đầy đủ thông tin (Tên, SĐT, Email)', 'error');
            return;
        }

        if (!emailInput.includes('@') || !emailInput.includes('.')) {
            Swal.fire('Lỗi', 'Vui lòng nhập Email hợp lệ để nhận Voucher', 'warning');
            return;
        }

        btnSubmit.innerHTML = 'Đang gửi... <i class="fa-solid fa-spinner fa-spin"></i>';

        fetch('/api/Otp/send', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ phone: phoneInput })
        })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    // Hiện khung nhập OTP và đổi nút
                    otpContainer.classList.remove('hidden');
                    btnSubmit.innerHTML = 'Xác nhận OTP <i class="fa-solid fa-check"></i>';

                    // 🌟 NÂNG CẤP UI: Hiển thị OTP dạng Toast (Giả lập tin nhắn SMS tới)
                    Swal.fire({
                        toast: true,
                        position: 'top-end', // Trượt từ góc phải trên xuống
                        icon: 'info',
                        title: 'Tin nhắn hệ thống',
                        text: `Mã OTP của bạn là: ${data.otp}`,
                        showConfirmButton: false,
                        timer: 6000,
                        timerProgressBar: true,
                        customClass: {
                            popup: 'rounded-xl shadow-lg border border-slate-100 bg-white'
                        }
                    });

                    // 🌟 NÂNG CẤP UI: Kích hoạt đếm ngược 60s cho nút gửi lại (nếu bạn có dùng HTML nút Gửi lại)
                    if (typeof startOtpTimer === 'function') {
                        startOtpTimer(60);
                    }
                } else {
                    // Xử lý trường hợp API trả về lỗi (vd: sđt không hợp lệ)
                    Swal.fire('Lỗi', data.message || 'Không thể gửi mã', 'error');
                    btnSubmit.innerHTML = 'Tra cứu ngay <i class="fa-solid fa-magnifying-glass"></i>';
                }
            })
            .catch(err => {
                console.error('Lỗi kết nối:', err);
                Swal.fire('Lỗi', 'Lỗi kết nối mạng', 'error');
                btnSubmit.innerHTML = 'Tra cứu ngay <i class="fa-solid fa-magnifying-glass"></i>';
            });
    }
    // =================================================================
    // BƯỚC 2: NẾU ĐANG HIỆN OTP -> GỌI API XÁC THỰC & LƯU DB
    // =================================================================
    else {
        if (!otpInput) {
            Swal.fire('Lỗi', 'Vui lòng nhập mã OTP', 'warning');
            return;
        }

        btnSubmit.innerHTML = 'Đang xác thực... <i class="fa-solid fa-spinner fa-spin"></i>';

        // Dùng URLSearchParams để C# dễ đọc [FromForm]
        const params = new URLSearchParams();
        params.append('phone', phoneInput);
        params.append('otpCode', otpInput);
        params.append('fullName', nameInput);
        params.append('email', emailInput);

        fetch('/api/Otp/VerifyOtpLogin', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: params
        })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    // ĐỒNG BỘ TÊN BIẾN LOCALSTORAGE (Quan trọng!)
                    localStorage.setItem('savedGuestName', nameInput);
                    localStorage.setItem('savedGuestPhone', phoneInput);
                    localStorage.setItem('savedGuestEmail', emailInput);

                    Swal.fire('Thành công', 'Hệ thống đã xác thực thành công!', 'success')
                        .then(() => {
                            if (typeof closeLookupModal === 'function') closeLookupModal();
                            window.location.reload();
                        });
                } else {
                    Swal.fire('Lỗi', data.message, 'error');
                    btnSubmit.innerHTML = 'Xác nhận OTP <i class="fa-solid fa-check"></i>';
                }
            })
            .catch(err => {
                console.error('Lỗi kết nối:', err);
                Swal.fire('Lỗi', 'Không thể kết nối máy chủ', 'error');
                btnSubmit.innerHTML = 'Xác nhận OTP <i class="fa-solid fa-check"></i>';
            });
    }
}
let lookupOtpInterval;

function startLookupOtpTimer(duration = 30) {
    const timerDisplay = document.getElementById('lookupOtpTimer');
    const btnResend = document.getElementById('btnResendLookupOtp');
    let timer = duration;

    // Khóa nút gửi lại và hiển thị số đếm ngược trong ô input
    btnResend.disabled = true;
    timerDisplay.innerText = timer + 's';
    btnResend.innerText = `Gửi lại mã (${timer}s)`;

    clearInterval(lookupOtpInterval);

    lookupOtpInterval = setInterval(function () {
        timer--;
        timerDisplay.innerText = timer + 's';
        btnResend.innerText = `Gửi lại mã (${timer}s)`;

        if (timer <= 0) {
            // Khi hết giờ: Dừng đếm, ẩn số đếm ngược trong ô input, bật sáng nút gửi lại
            clearInterval(lookupOtpInterval);
            timerDisplay.innerText = '';
            btnResend.disabled = false;
            btnResend.innerText = 'Chưa nhận được mã? Gửi lại ngay';
        }
    }, 1000);
}

// Hàm giả lập gửi lại mã (Bạn gắn API gửi thật vào đây)
function resendLookupOtp() {
    const phoneInput = document.getElementById('lookupPhoneInput').value;

    // 1. Gọi hàm gửi SMS giả lập (hoặc fetch API)
    if (typeof sendSmsToPhone === 'function') {
        sendSmsToPhone(phoneInput);
    }

    // 2. Bắt đầu đếm ngược lại từ đầu (30s)
    startLookupOtpTimer(30);
}