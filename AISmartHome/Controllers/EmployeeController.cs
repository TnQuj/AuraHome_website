using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Models;
using AISmartHome.Data;
using Microsoft.AspNetCore.Http;

namespace AISmartHome.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly AISmartHomeDbContext _context;

        public EmployeeController(AISmartHomeDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // HÀM HỖ TRỢ: KIỂM TRA ĐĂNG NHẬP VÀ LẤY ID NHÂN VIÊN
        // Đặt chung vào 1 chỗ để không phải viết lặp lại ở nhiều hàm
        // =========================================================
        private async Task<int?> ValidateAndGetEmployeeId()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Employee" && role != "Admin") return null;

            // Nếu Session đã có ID thì lấy ra dùng luôn cho nhanh
            string sessionMaNV = HttpContext.Session.GetString("MaNhanVien");
            if (!string.IsNullOrEmpty(sessionMaNV))
            {
                ViewBag.TenNhanVien = HttpContext.Session.GetString("TenNhanVien") ?? "Nhân viên";
                return int.Parse(sessionMaNV);
            }

            // Nếu chưa có (ví dụ mới đăng nhập), thì truy vấn từ Username
            string username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username)) return null;

            var nhanVien = await _context.NhanViens
                .Include(n => n.MaTaiKhoanNavigation)
                .FirstOrDefaultAsync(n => n.MaTaiKhoanNavigation.TenDangNhap == username);

            if (nhanVien != null)
            {
                // Lưu lại Session để dùng cho các trang sau
                HttpContext.Session.SetString("MaNhanVien", nhanVien.MaNhanVien.ToString());
                HttpContext.Session.SetString("TenNhanVien", nhanVien.TenNhanVien ?? "Nhân viên");
                ViewBag.TenNhanVien = nhanVien.TenNhanVien;
                return nhanVien.MaNhanVien;
            }

            return null; // Không tìm thấy
        }


        // =========================================================
        // 1. TRANG CỔNG NHÂN VIÊN (Chỉ hiện việc ĐANG CHỜ / ĐANG XỬ LÝ)
        // =========================================================
        public async Task<IActionResult> Index()
        {
            // 1. Kiểm tra đăng nhập
            int? maNhanVienHienTai = await ValidateAndGetEmployeeId();
            if (maNhanVienHienTai == null) return RedirectToAction("Index", "Home");

            // 2. Lấy danh sách yêu cầu ĐANG CHỜ (Loại bỏ các đơn Đã hoàn thành hoặc Đã/Bị hủy)
            var activeTasks = await _context.YeuCauLapDats
                .Include(y => y.MaDonHangNavigation)
                    .ThenInclude(d => d.MaKhachHangNavigation)
                .Include(y => y.MaDonHangNavigation)
                    .ThenInclude(d => d.ChiTietDonHangs)
                        .ThenInclude(c => c.MaSanPhamNavigation)
                // CHỐT CHẶN: Chỉ lấy đơn chưa hoàn thành và Đơn hàng phải chưa bị hủy
                .Where(y => y.MaNhanVien == maNhanVienHienTai
                         && y.TrangThaiLapDat != "Đã hoàn thành"
                         && y.TrangThaiLapDat != "Bị hủy"
                         && y.MaDonHangNavigation.TrangThaiDonHang != "Đã hủy")
                .OrderByDescending(y => y.NgayLap)
                .ToListAsync();

            return View(activeTasks);
        }

        // =========================================================
        // 2. TRANG LỊCH SỬ (Chỉ hiện việc ĐÃ XONG hoặc BỊ HỦY)
        // =========================================================
        public async Task<IActionResult> History()
        {
            // 1. Kiểm tra đăng nhập
            int? maNhanVienHienTai = await ValidateAndGetEmployeeId();
            if (maNhanVienHienTai == null) return RedirectToAction("Index", "Home");

            // 2. Lấy danh sách yêu cầu ĐÃ XONG HOẶC BỊ HỦY
            var historyRequests = await _context.YeuCauLapDats
                .Include(y => y.MaDonHangNavigation)
                    .ThenInclude(d => d.MaKhachHangNavigation)
                .Include(y => y.MaDonHangNavigation)
                    .ThenInclude(d => d.ChiTietDonHangs)
                        .ThenInclude(c => c.MaSanPhamNavigation)
                // CHỐT CHẶN: Chỉ lấy đơn Đã xong hoặc Đã bị hủy (từ thợ hoặc từ khách)
                .Where(y => y.MaNhanVien == maNhanVienHienTai
                         && (y.TrangThaiLapDat == "Đã hoàn thành"
                          || y.TrangThaiLapDat == "Bị hủy"
                          || y.MaDonHangNavigation.TrangThaiDonHang == "Đã hủy"))
                .OrderByDescending(y => y.NgayLap)
                .ToListAsync();

            return View(historyRequests);
        }

        // =========================================================
        // 3. API BÁO CÁO TIẾN ĐỘ THI CÔNG
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus)
        {
            var yeuCau = await _context.YeuCauLapDats
                .Include(y => y.MaDonHangNavigation)
                .FirstOrDefaultAsync(y => y.MaYeuCauLapDat == id);

            if (yeuCau == null) return NotFound();

            yeuCau.TrangThaiLapDat = newStatus;

            if (yeuCau.MaDonHangNavigation != null)
            {
                if (newStatus == "Đã hoàn thành")
                {
                    // 1. Đơn hàng chuyển sang trạng thái Hoàn thành
                    yeuCau.MaDonHangNavigation.TrangThaiDonHang = "Hoàn thành";

                    // 2. [THÊM DÒNG NÀY]: Tự động gạch nợ, chuyển tài chính thành "Đã thanh toán"
                    yeuCau.MaDonHangNavigation.TrangThaiThanhToan = "Đã thanh toán";
                }
                else if (newStatus == "Bị hủy")
                {
                    yeuCau.MaDonHangNavigation.TrangThaiDonHang = "Đã hủy";
                }
            }

            _context.Update(yeuCau);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật tiến độ thi công mã #{id}";
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // 4. API CẬP NHẬT PHÍ VÀ LỊCH HẸN (Nếu có)
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> UpdateInstallationInfo(int id, decimal phiLapDat, DateTime ngayHen, string newStatus)
        {
            var yeuCau = await _context.YeuCauLapDats.FindAsync(id);
            if (yeuCau == null) return NotFound();

            yeuCau.TrangThaiLapDat = newStatus;
            yeuCau.PhiLapDat = phiLapDat;
            yeuCau.NgayLap = ngayHen;

            _context.Update(yeuCau);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật thông tin lắp đặt cho mã #{id}";
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // 5. ĐĂNG XUẤT
        // =========================================================
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
    }
}