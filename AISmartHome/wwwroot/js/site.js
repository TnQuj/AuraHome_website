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
        const totalVal = parseFloat(depositAmountDisplay.getAttribute('data-total'));
        const depositVal = parseFloat(depositAmountDisplay.getAttribute('data-deposit'));

        function updatePricingDisplay() {
            const checkedRadio = document.querySelector('input[name="PaymentMode"]:checked');
            if (!checkedRadio) return;

            const selectedMode = checkedRadio.value;
            if (selectedMode === "30") {
                depositAmountDisplay.innerText = new Intl.NumberFormat('vi-VN').format(depositVal) + " ";
                depositAmountDisplay.setAttribute('data-value', depositVal);
                if (badgePaymentType) badgePaymentType.innerText = "Cọc 30% giữ đơn";
                if (btnCheckout) btnCheckout.innerHTML = '<i class="fa-solid fa-shield-halved"></i> Đặt cọc & Xác nhận';
            } else {
                depositAmountDisplay.innerText = new Intl.NumberFormat('vi-VN').format(totalVal) + " ";
                depositAmountDisplay.setAttribute('data-value', totalVal);
                if (badgePaymentType) badgePaymentType.innerText = "Thanh toán 100%";
                if (btnCheckout) btnCheckout.innerHTML = '<i class="fa-solid fa-credit-card"></i> Xác nhận thanh toán';
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
                    Swal.fire({
                        title: 'Tin nhắn giả lập (Test)',
                        html: `Mã OTP của bạn là: <br><br><b class="text-4xl text-brand-cyan tracking-widest">${data.otp}</b>`,
                        icon: 'info',
                        confirmButtonText: 'Đã hiểu',
                        confirmButtonColor: '#06b6d4',
                        customClass: { backdrop: 'swal-glass-backdrop' }
                    });
                } else {
                    Swal.fire('Lỗi gửi SMS!', data.message, 'error');
                }
            })
            .catch(err => console.log("Lưu ý: API OTP chưa được bật hoặc chưa cấu hình."));

        startCountdown();
    }

    // BƯỚC A: Ấn Đặt hàng -> Chặn submit -> Hiện Modal OTP -> Bắn SMS
    form.addEventListener('submit', function (e) {
        e.preventDefault();
        const phoneInput = document.querySelector('input[name="Phone"]');
        const phoneValue = phoneInput ? phoneInput.value : '';

        const displayPhone = document.getElementById('displayPhone');
        if (displayPhone) displayPhone.innerText = phoneValue;

        if (otpModal) {
            otpModal.classList.remove('hidden');
            otpModal.classList.add('flex');
            setTimeout(() => { if (otpInput) otpInput.focus(); }, 100);
        }
        sendSmsToPhone(phoneValue);
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

    // BƯỚC B: Xác thực OTP và Tiến hành thanh toán
    if (btnVerifyOtp) {
        btnVerifyOtp.addEventListener('click', function () {
            const enteredOtp = otpInput ? otpInput.value.trim() : '';
            const phoneInput = document.querySelector('input[name="Phone"]');
            const phoneValue = phoneInput ? phoneInput.value : '';

            // LẤY SỐ TIỀN VÀ CHẾ ĐỘ THANH TOÁN (100 hay 30) CHUẨN XÁC TỪ GIAO DIỆN
            const finalAmount = parseFloat(depositAmountDisplay.getAttribute('data-value'));
            const paymentMode = document.querySelector('input[name="PaymentMode"]:checked').value;
            const isDeposit = (paymentMode === "30");

            if (!enteredOtp || enteredOtp.length < 6) {
                return Swal.fire('Cảnh báo', 'Vui lòng nhập đủ mã OTP gồm 6 chữ số.', 'warning');
            }

            Swal.fire({
                title: 'Đang xử lý giao dịch...',
                didOpen: () => Swal.showLoading()
            });

            // 1. Gọi API Verify OTP (Bây giờ đã truyền đủ số tiền để C# không chửi 400 nữa)
            fetch('/api/otp/verify', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    phone: phoneValue,
                    otp: enteredOtp,
                    amount: finalAmount,
                    paymentMode: paymentMode
                })
            })
                .then(async res => {
                    if (!res.ok) {
                        const err = await res.json();
                        throw new Error(err.message || "Xác thực OTP thất bại.");
                    }
                    return res.json();
                })
                .then(data => {
                    if (data.success) {
                        clearInterval(countdownInterval);
                        if (otpModal) {
                            otpModal.classList.remove('flex');
                            otpModal.classList.add('hidden');
                        }

                        // 2. Gửi Form tạo Đơn hàng sau khi OTP chuẩn
                        const formData = new FormData(form);
                        return fetch(form.action, { method: 'POST', body: formData });
                    } else {
                        throw new Error(data.message || "Mã OTP không chính xác.");
                    }
                })
                .then(async res => {
                    if (!res) return; // Bị lỗi ở bước trước
                    const orderData = await res.json();

                    if (orderData.success) {
                        Swal.close();

                        // Cập nhật thông tin lên Modal QR Code
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
                        Swal.fire('Lỗi tạo đơn!', orderData.message, 'error');
                    }
                })
                .catch(err => {
                    // Báo lỗi bằng SweetAlert mờ xanh Navy hệt như bạn đã làm
                    Swal.fire({
                        icon: 'error',
                        title: 'Giao dịch từ chối',
                        text: err.message || "Không thể kết nối đến máy chủ.",
                        customClass: { backdrop: 'swal-glass-backdrop' }
                    });
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