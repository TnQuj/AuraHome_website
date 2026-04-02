// Hàm mở hộp thoại đăng nhập
function openLoginModal() {
    const modal = document.getElementById('adminLoginModal');
    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

// Hàm đóng hộp thoại đăng nhập
function closeLoginModal() {
    const modal = document.getElementById('adminLoginModal');
    modal.classList.remove('flex');
    modal.classList.add('hidden');
}