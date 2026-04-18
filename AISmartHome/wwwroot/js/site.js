document.addEventListener("DOMContentLoaded", function () {
    // =========================================================
    // CHỐT CHẶN AN TOÀN
    // =========================================================
    const form = document.getElementById('checkoutForm');
    if (!form) return;

    // =========================================================
    // 1. XỬ LÝ API TỈNH/THÀNH PHỐ
    // =========================================================
    const provinceSelect = document.getElementById("province");
    const districtSelect = document.getElementById("district");

    if (provinceSelect) {
        fetch('https://provinces.open-api.vn/api/p/')
            .then(response => response.json())
            .then(data => { 
                data.forEach(province => {
                    let option = document.createElement("option");
                    option.value = province.name;
                    option.setAttribute("data-code", province.code);
                    option.text = province.name;
                    provinceSelect.appendChild(option);
                });
            })
            .catch(error => console.error('Lỗi tải Tỉnh/TP:', error));

        provinceSelect.addEventListener("change", function () {
            districtSelect.innerHTML = '<option value="">Chọn Quận/Huyện</option>';
            const selectedOption = this.options[this.selectedIndex];
            const provinceCode = selectedOption.getAttribute("data-code");

            if (provinceCode) {
                fetch(`https://provinces.open-api.vn/api/p/${provinceCode}?depth=2`)
                    .then(response => response.json())
                    .then(data => {
                        if (data.districts) {
                            data.districts.forEach(district => {
                                let option = document.createElement("option");
                                option.value = district.name;
                                option.text = district.name;
                                districtSelect.appendChild(option);
                            });
                        }
                    })
                    .catch(error => console.error('Lỗi tải Quận/Huyện:', error));
            }
        });
    }

    // =========================================================
    // 2. XỬ LÝ GIAO DIỆN CHỌN GIÁ TIỀN & CỌC
    // =========================================================
    const chkLapDat = document.getElementById('CanLapDat');
    const depositOptionWrapper = document.getElementById('depositOptionWrapper');
    const paymentRadios = document.querySelectorAll('input[name="PaymentMode"]');
    const depositAmountDisplay = document.getElementById('depositAmountDisplay');
    const badgePaymentType = document.getElementById('badgePaymentType');
    const btnCheckout = document.getElementById('btn-checkout');

    if (depositAmountDisplay && chkLapDat) {

        // ❌ BẠN HÃY XÓA 2 DÒNG CŨ NÀY ĐI (Vì nó khiến số tiền bị kẹt cứng)
        // const totalVal = parseFloat(depositAmountDisplay.getAttribute('data-total'));
        // const depositVal = parseFloat(depositAmountDisplay.getAttribute('data-deposit'));

        function updatePricingDisplay() {
            // ✅ ĐƯA 2 DÒNG ĐÓ VÀO TRONG NÀY ĐỂ LUÔN LẤY GIÁ TRỊ MỚI NHẤT
            const currentTotal = parseFloat(depositAmountDisplay.getAttribute('data-total'));
            const currentDeposit = parseFloat(depositAmountDisplay.getAttribute('data-deposit'));

            const checkedRadio = document.querySelector('input[name="PaymentMode"]:checked');
            if (!checkedRadio) return;

            const selectedMode = checkedRadio.value;
            if (selectedMode === "30") {
                depositAmountDisplay.innerText = new Intl.NumberFormat('vi-VN').format(currentDeposit);
                depositAmountDisplay.setAttribute('data-value', currentDeposit);

                if (badgePaymentType) badgePaymentType.innerHTML = '<i class="fa-solid fa-check-circle text-[10px]"></i> Cọc 30% giữ đơn';
                if (btnCheckout) btnCheckout.innerHTML = '<i class="fa-solid fa-shield-halved text-[18px] opacity-90"></i> <span>Đặt cọc & Xác nhận</span> <i class="fa-solid fa-arrow-right text-[15px] opacity-70 group-hover:opacity-100 group-hover:translate-x-1.5 transition-all duration-300"></i>';
            } else {
                depositAmountDisplay.innerText = new Intl.NumberFormat('vi-VN').format(currentTotal);
                depositAmountDisplay.setAttribute('data-value', currentTotal);

                if (badgePaymentType) badgePaymentType.innerHTML = '<i class="fa-solid fa-check-circle text-[10px]"></i> Thanh toán 100%';
                if (btnCheckout) btnCheckout.innerHTML = '<i class="fa-solid fa-shield-halved text-[18px] opacity-90"></i> <span>Thanh toán an toàn</span> <i class="fa-solid fa-arrow-right text-[15px] opacity-70 group-hover:opacity-100 group-hover:translate-x-1.5 transition-all duration-300"></i>';
            }
        }

        paymentRadios.forEach(radio => radio.addEventListener('change', updatePricingDisplay));

        chkLapDat.addEventListener('change', function () {
            if (this.checked) {
                if (depositOptionWrapper) {
                    depositOptionWrapper.style.opacity = "1";
                    depositOptionWrapper.style.pointerEvents = "auto";
                }
            } else {
                if (depositOptionWrapper) {
                    depositOptionWrapper.style.opacity = "0.4";
                    depositOptionWrapper.style.pointerEvents = "none";
                }
                const radio100 = document.querySelector('input[name="PaymentMode"][value="100"]');
                if (radio100) radio100.checked = true;
            }
            updatePricingDisplay();
        });

        chkLapDat.dispatchEvent(new Event('change'));
    }
    // =========================================================
    // 3. XỬ LÝ LƯU ĐƠN - OTP - VÀ HIỂN THỊ MÃ QR 
    // =========================================================
    const otpModal = document.getElementById('otpModal');
    const qrModal = document.getElementById('qrModal');
    const btnVerifyOtp = document.getElementById('btnVerifyOtp');
    const btnCancelOtp = document.getElementById('btnCancelOtp');
    const btnResendOtp = document.getElementById('btnResendOtp');
    const otpInput = document.getElementById('otpInput');
    const btnFinishOrder = document.getElementById('btnFinishOrder');

    let countdownInterval;
    let timeLeft = 60;

    function startCountdown() {
        clearInterval(countdownInterval);
        timeLeft = 60;
        if (btnResendOtp) btnResendOtp.disabled = true;

        countdownInterval = setInterval(() => {
            timeLeft--;
            const timerDisplay = document.getElementById('otpTimer');
            if (timerDisplay) timerDisplay.innerText = timeLeft;

            if (timeLeft <= 0) {
                clearInterval(countdownInterval);
                if (btnResendOtp) {
                    btnResendOtp.disabled = false;
                    btnResendOtp.innerHTML = "Gửi lại mã";
                }
            } else {
                if (btnResendOtp) {
                    btnResendOtp.innerHTML = `Gửi lại (<span id="otpTimer">${timeLeft}</span>s)`;
                }
            }
        }, 1000);
    }

    function sendSmsToPhone(phoneNumber) {
        fetch('/api/otp/send', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ phone: phoneNumber })
        })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    // 🌟 HIỆU ỨNG SMS THỰC TẾ TRÊN ĐIỆN THOẠI
                    Swal.fire({
                        toast: true,
                        position: 'top',
                        showConfirmButton: false,
                        timer: 6000,
                        timerProgressBar: false, // Tắt thanh chạy cho giống thông báo hệ thống
                        // Icon giả lập App Tin nhắn của điện thoại
                        iconHtml: '<div class="w-10 h-10 bg-emerald-500 rounded-full flex items-center justify-center shadow-sm"><i class="fa-solid fa-message text-white"></i></div>',
                        title: 'Tin nhắn mới',
                        html: `<div class="text-[13px] text-slate-600 mt-0.5 text-left leading-relaxed">
                                Mã xác nhận của bạn là <strong class="text-slate-900 text-lg tracking-widest">${data.otp}</strong>. Vui lòng không chia sẻ mã này.
                           </div>`,
                        customClass: {
                            // Nền trắng hơi trong suốt (kính mờ) y hệt iOS
                            popup: 'rounded-2xl shadow-[0_8px_30px_rgb(0,0,0,0.12)] border border-slate-100/50 bg-white/95 backdrop-blur-md px-4 py-3 min-w-[320px]',
                            title: 'text-sm font-bold text-slate-900 text-left',
                            icon: 'border-none w-auto h-auto m-0 mr-3 mt-1',
                            htmlContainer: 'm-0' // Căn lề lại cho chuẩn
                        }
                    });
                } else {
                    // Cảnh báo lỗi cũng làm theo format thông báo hệ thống
                    Swal.fire({
                        toast: true,
                        position: 'top',
                        showConfirmButton: false,
                        timer: 4000,
                        iconHtml: '<div class="w-10 h-10 bg-rose-500 rounded-full flex items-center justify-center shadow-sm"><i class="fa-solid fa-triangle-exclamation text-white"></i></div>',
                        title: 'Không thể gửi tin nhắn',
                        html: `<div class="text-[13px] text-slate-600 mt-0.5 text-left">${data.message}</div>`,
                        customClass: {
                            popup: 'rounded-2xl shadow-[0_8px_30px_rgb(0,0,0,0.12)] border border-slate-100/50 bg-white/95 backdrop-blur-md px-4 py-3 min-w-[320px]',
                            title: 'text-sm font-bold text-slate-900 text-left',
                            icon: 'border-none w-auto h-auto m-0 mr-3 mt-1',
                            htmlContainer: 'm-0'
                        }
                    });
                }
            })
            .catch(err => console.log("Lưu ý: API OTP chưa được bật."));

        startCountdown();
    }
    // ==================================================
    // BƯỚC A: LOGIC THÔNG MINH KHI BẤM NÚT ĐẶT HÀNG
    // ==================================================
    form.addEventListener('submit', function (e) {
        e.preventDefault(); // Luôn chặn mặc định để JS xử lý

        const phoneInput = document.querySelector('input[name="Phone"]');
        let currentPhone = phoneInput ? phoneInput.value.replace(/\D/g, '') : '';
        if (currentPhone.startsWith('84')) currentPhone = '0' + currentPhone.substring(2);

        // Lấy số điện thoại đã xác thực từ Server (Biến này đã khai báo ở Checkout.cshtml)
        let verifiedPhone = (typeof serverVerifiedPhone !== 'undefined' ? serverVerifiedPhone : '').replace(/\D/g, '');
        if (verifiedPhone.startsWith('84')) verifiedPhone = '0' + verifiedPhone.substring(2);

        // NẾU ĐÃ XÁC THỰC -> TẠO ĐƠN VÀ HIỆN QR LUÔN (Bỏ qua Popup OTP)
        if (currentPhone === verifiedPhone && currentPhone !== '') {
            const formData = new FormData(form);
            processOrderAndShowQR(formData);
        }
        // NẾU CHƯA XÁC THỰC -> MỞ POPUP OTP ĐỂ KHÁCH NHẬP
        else {
            const displayPhone = document.getElementById('displayPhone');
            if (displayPhone) displayPhone.innerText = currentPhone;

            if (otpModal) {
                otpModal.classList.remove('hidden');
                otpModal.classList.add('flex');
                setTimeout(() => { if (otpInput) otpInput.focus(); }, 100);
            }
            sendSmsToPhone(currentPhone);
        }
    });

    if (btnResendOtp) {
        btnResendOtp.addEventListener('click', () => {
            const phoneInput = document.querySelector('input[name="Phone"]');
            if (phoneInput) sendSmsToPhone(phoneInput.value);
        });
    }

    if (btnCancelOtp) {
        btnCancelOtp.addEventListener('click', () => {
            if (otpModal) {
                otpModal.classList.remove('flex');
                otpModal.classList.add('hidden');
            }
            if (otpInput) otpInput.value = '';
            clearInterval(countdownInterval);
        });
    }

    // ==================================================
    // BƯỚC B: KHI KHÁCH BẤM "XÁC NHẬN" TRONG POPUP OTP
    // ==================================================
    if (btnVerifyOtp) {
        btnVerifyOtp.addEventListener('click', function () {
            const enteredOtp = otpInput ? otpInput.value.trim() : '';
            if (!enteredOtp || enteredOtp.length !== 6) {
                return Swal.fire('Cảnh báo', 'Vui lòng nhập đủ mã OTP gồm 6 chữ số.', 'warning');
            }

            // Gắn thẳng mã OTP vào Form để gửi lên C# xác minh 1 lần duy nhất (Không cần gọi API Verify trung gian nữa)
            const formData = new FormData(form);
            formData.append('OtpCode', enteredOtp);

            processOrderAndShowQR(formData);
        });
    }

    // ==================================================
    // HÀM DÙNG CHUNG: GỬI ĐƠN HÀNG LÊN C# VÀ VẼ MÃ QR
    // ==================================================
    function processOrderAndShowQR(formData) {
        Swal.fire({
            title: 'Đang xử lý giao dịch...',
            didOpen: () => Swal.showLoading()
        });

        fetch(form.action, { method: 'POST', body: formData })
            .then(res => res.json())
            .then(orderData => {
                if (orderData.success) {
                    Swal.close();

                    // Nếu Popup OTP đang mở thì đóng lại
                    if (otpModal && !otpModal.classList.contains('hidden')) {
                        clearInterval(countdownInterval);
                        otpModal.classList.remove('flex');
                        otpModal.classList.add('hidden');
                    }

                    // --- TIẾN HÀNH VẼ MÃ QR NHƯ CŨ ---
                    const finalAmount = parseFloat(depositAmountDisplay.getAttribute('data-value'));
                    const paymentMode = document.querySelector('input[name="PaymentMode"]:checked').value;
                    const isDeposit = (paymentMode === "30");

                    const paymentTypeTxt = isDeposit ? "CỌC 30%" : "THANH TOÁN 100%";
                    const transferMsg = isDeposit ? `Coc don hang ${orderData.orderId}` : `Thanh toan don hang ${orderData.orderId}`;

                    if (document.getElementById('qrModalTitle'))
                        document.getElementById('qrModalTitle').innerText = isDeposit ? "Thanh toán cọc 30%" : "Thanh toán 100%";

                    if (document.getElementById('qrAmountLabel'))
                        document.getElementById('qrAmountLabel').innerText = isDeposit ? "SỐ TIỀN CỌC:" : "TỔNG THANH TOÁN:";

                    if (document.getElementById('qrFooterNote'))
                        document.getElementById('qrFooterNote').innerText = isDeposit
                            ? "70% còn lại sẽ được thanh toán cho nhân viên sau khi nghiệm thu."
                            : "Cảm ơn bạn đã thanh toán toàn bộ giá trị đơn hàng. Chúng tôi sẽ giao hàng sớm nhất.";

                    const qrTextData = `XÁC NHẬN THANH TOÁN\n-----------------\nCửa hàng: AuraHome\nMã đơn: #${orderData.orderId}\nLoại thanh toán: ${paymentTypeTxt}\nSố tiền: ${new Intl.NumberFormat('vi-VN').format(finalAmount)} VND`;
                    const qrApiUrl = `https://api.qrserver.com/v1/create-qr-code/?size=300x300&data=${encodeURIComponent(qrTextData)}`;

                    if (document.getElementById('qrOrderId')) document.getElementById('qrOrderId').innerText = "#" + orderData.orderId;
                    if (document.getElementById('qrAmount')) document.getElementById('qrAmount').innerText = new Intl.NumberFormat('vi-VN').format(finalAmount) + " VND";
                    if (document.getElementById('qrContent')) document.getElementById('qrContent').innerText = transferMsg;

                    const qrImage = document.getElementById('qrImage');
                    const qrLoading = document.getElementById('qrLoading');

                    if (qrImage && qrLoading) {
                        qrImage.classList.add('hidden');
                        qrLoading.classList.remove('hidden');
                        qrImage.onload = () => {
                            qrLoading.classList.add('hidden');
                            qrImage.classList.remove('hidden');
                        };
                        qrImage.src = qrApiUrl;
                    }

                    if (qrModal) {
                        qrModal.classList.remove('hidden');
                        qrModal.classList.add('flex');
                    }
                } else {
                    // C# báo lỗi (Sai OTP, Lỗi giỏ hàng,...)
                    Swal.fire('Lỗi tạo đơn!', orderData.message, 'error');
                }
            })
            .catch(err => {
                Swal.fire({
                    icon: 'error',
                    title: 'Giao dịch từ chối',
                    text: err.message || "Không thể kết nối đến máy chủ.",
                    customClass: { backdrop: 'swal-glass-backdrop' }
                });
            });
    }

    if (btnFinishOrder) {
        btnFinishOrder.addEventListener('click', function () {
            Swal.fire({
                title: 'Cảm ơn bạn!',
                text: 'Chúng tôi sẽ kiểm tra giao dịch và giao hàng sớm nhất.',
                icon: 'success',
                confirmButtonText: 'Về trang chủ',
                confirmButtonColor: '#06b6d4',
                customClass: { backdrop: 'swal-glass-backdrop' }
            }).then(() => {
                window.location.href = '/Customers/Index';
            });
        });
    }
});