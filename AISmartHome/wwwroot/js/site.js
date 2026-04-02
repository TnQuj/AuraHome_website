document.addEventListener("DOMContentLoaded", function () {

    // --- PHẦN 1: API TỈNH/THÀNH PHỐ ---
    const provinceSelect = document.querySelector("#province");
    const districtSelect = document.querySelector("#district");

    if (provinceSelect && districtSelect) {
        const host = "https://provinces.open-api.vn/api/";
        const callAPI = (api) => fetch(api).then(res => res.json()).then(data => renderData(data, "province")).catch(err => console.error(err));
        callAPI(host + '?depth=1');

        const callApiDistrict = (api) => fetch(api).then(res => res.json()).then(data => renderData(data.districts, "district")).catch(err => console.error(err));

        const renderData = (array, selectId) => {
            let row = '<option value="">Chọn</option>';
            array.forEach(element => row += `<option value="${element.name}" data-id="${element.code}">${element.name}</option>`);
            document.querySelector("#" + selectId).innerHTML = row;
        }

        provinceSelect.addEventListener("change", function () {
            let provinceId = this.options[this.selectedIndex].getAttribute("data-id");
            districtSelect.innerHTML = '<option value="">Chọn Quận/Huyện</option>';
            if (provinceId) callApiDistrict(host + "p/" + provinceId + "?depth=2");
        });
    }

    // --- PHẦN 2: XỬ LÝ OTP VÀ HIỂN THỊ MÃ QR ---
    const form = document.getElementById('checkoutForm');
    const otpModal = document.getElementById('otpModal');
    const qrModal = document.getElementById('qrModal');

    const btnVerifyOtp = document.getElementById('btnVerifyOtp');
    const btnCancelOtp = document.getElementById('btnCancelOtp');
    const btnResendOtp = document.getElementById('btnResendOtp');
    const otpInput = document.getElementById('otpInput');

    let countdownInterval;
    let timeLeft = 60;

    function startCountdown() {
        clearInterval(countdownInterval);
        timeLeft = 60;
        btnResendOtp.disabled = true;

        countdownInterval = setInterval(() => {
            timeLeft--;
            document.getElementById('otpTimer').innerText = timeLeft;
            if (timeLeft <= 0) {
                clearInterval(countdownInterval);
                btnResendOtp.disabled = false;
                btnResendOtp.innerHTML = "Gửi lại mã";
            } else {
                btnResendOtp.innerHTML = `Gửi lại (<span id="otpTimer">${timeLeft}</span>s)`;
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
                    console.log("Mã OTP Test:", data.otp);
                    // Bỏ dòng alert dưới đây nếu bạn đã cấu hình xong SMS thật
                    alert("[Dành cho Dev] Mã OTP là: " + data.otp);
                } else {
                    alert("Lỗi: " + data.message);
                }
            })
            .catch(err => alert("Lỗi mạng: Không thể gọi API C#"));
        startCountdown();
    }

    if (form) {
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            const phoneValue = document.querySelector('input[name="Phone"]').value;
            document.getElementById('displayPhone').innerText = phoneValue;
            otpModal.classList.remove('hidden');
            otpModal.classList.add('flex');
            setTimeout(() => otpInput.focus(), 100);
            sendSmsToPhone(phoneValue);
        });

        btnResendOtp.addEventListener('click', () => sendSmsToPhone(document.querySelector('input[name="Phone"]').value));

        btnCancelOtp.addEventListener('click', () => {
            otpModal.classList.remove('flex');
            otpModal.classList.add('hidden');
            otpInput.value = '';
            clearInterval(countdownInterval);
        });

        btnVerifyOtp.addEventListener('click', function () {
            const enteredOtp = otpInput.value.trim();
            const phoneValue = document.querySelector('input[name="Phone"]').value;

            if (!enteredOtp || enteredOtp.length < 6) return alert("Vui lòng nhập đủ mã OTP gồm 6 chữ số.");

            fetch('/api/otp/verify', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ phone: phoneValue, otp: enteredOtp })
            })
                .then(res => res.json())
                .then(data => {
                    if (data.success) {
                        clearInterval(countdownInterval);
                        otpModal.classList.remove('flex');
                        otpModal.classList.add('hidden');

                        Swal.fire({
                            title: 'Đang tạo đơn hàng...',
                            didOpen: () => Swal.showLoading()
                        });

                        // Gửi dữ liệu form lên Controller
                        const formData = new FormData(form);
                        fetch(form.action, { method: 'POST', body: formData })
                            .then(res => res.json())
                            .then(orderData => {
                                if (orderData.success) {
                                    Swal.close(); // Tắt Loading

                                    // Lấy tổng tiền và tạo mã QR
                                    let totalAmount = document.getElementById('totalAmountDisplay').getAttribute('data-value');

                                    // Nội dung chữ sẽ hiện ra khi quét mã QR bằng camera thường
                                    const transferContent = `AuraHome - Đơn hàng #${orderData.orderId}`;
                                    const qrTextData = `XÁC NHẬN THANH TOÁN\n-----------------\nCửa hàng: AuraHome\nMã đơn: #${orderData.orderId}\nSố tiền: ${new Intl.NumberFormat('vi-VN').format(totalAmount)} VND\n(Đây là mã QR mô phỏng)`;

                                    // Gọi API tạo mã QR chung (không phải QR Ngân hàng)
                                    const qrApiUrl = `https://api.qrserver.com/v1/create-qr-code/?size=300x300&data=${encodeURIComponent(qrTextData)}`;

                                    // Đổ dữ liệu vào Modal QR
                                    document.getElementById('qrOrderId').innerText = "#" + orderData.orderId;
                                    document.getElementById('qrAmount').innerText = new Intl.NumberFormat('vi-VN').format(totalAmount) + " VND";
                                    document.getElementById('qrContent').innerText = transferContent;

                                    const qrImage = document.getElementById('qrImage');
                                    qrImage.onload = () => {
                                        document.getElementById('qrLoading').classList.add('hidden');
                                        qrImage.classList.remove('hidden');
                                    };
                                    qrImage.src = qrApiUrl;

                                    // Hiển thị Modal QR
                                    qrModal.classList.remove('hidden');
                                    qrModal.classList.add('flex');
                                } else {
                                    Swal.fire('Lỗi!', orderData.message, 'error');
                                }
                            });
                    } else {
                        alert(data.message || "Mã OTP không chính xác.");
                    }
                });
        });

        // Nút "Tôi đã thanh toán xong" trong Modal QR
        document.getElementById('btnFinishOrder').addEventListener('click', function () {
            Swal.fire({
                title: 'Cảm ơn bạn!',
                text: 'Chúng tôi sẽ kiểm tra giao dịch và giao hàng sớm nhất.',
                icon: 'success',
                confirmButtonText: 'Về trang chủ',
                confirmButtonColor: '#06b6d4'
            }).then(() => {
                window.location.href = '/Customers/Index';
            });
        });
    }
});