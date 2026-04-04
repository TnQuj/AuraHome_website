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

        public async Task<IActionResult> Index()
        {
            // BƯỚC CHẶN: Chỉ cho phép Nhân viên (hoặc Admin) vào xem
            var role = HttpContext.Session.GetString("Role");
            if (role != "Employee" && role != "Admin")
            {
                return RedirectToAction("Index", "Home");
            }

            // 1. Lấy tên đăng nhập từ Session
            string username = HttpContext.Session.GetString("Username");

            // 2. Tìm thông tin Nhân viên dựa vào Tên đăng nhập
            var nhanVien = await _context.NhanViens
                .Include(n => n.MaTaiKhoanNavigation) // Kết nối qua bảng Tài khoản
                .FirstOrDefaultAsync(n => n.MaTaiKhoanNavigation.TenDangNhap == username);

            if (nhanVien != null)
            {
                // Nếu tìm thấy, gán Tên thật vào ViewBag
                ViewBag.TenNhanVien = nhanVien.TenNhanVien;

                // Mẹo nhỏ: Lưu luôn Mã Nhân Viên vào Session để sau này dùng lúc cập nhật trạng thái
                HttpContext.Session.SetString("MaNhanVien", nhanVien.MaNhanVien.ToString());
            }
            else
            {
                // Nếu lỗi dữ liệu không tìm thấy, dùng tạm Tên đăng nhập
                ViewBag.TenNhanVien = username;
            }
            // Lấy ID nhân viên đang đăng nhập từ Session
            string maNhanVienStr = HttpContext.Session.GetString("MaNhanVien");
            int maNV = string.IsNullOrEmpty(maNhanVienStr) ? 0 : int.Parse(maNhanVienStr);

            var installRequests = await _context.YeuCauLapDats
                .Include(y => y.MaDonHangNavigation)
                    .ThenInclude(d => d.MaKhachHangNavigation)
                // THÊM 2 DÒNG NÀY ĐỂ LẤY DANH SÁCH SẢN PHẨM KHÁCH MUA
                .Include(y => y.MaDonHangNavigation)
                    .ThenInclude(d => d.ChiTietDonHangs)
                        .ThenInclude(c => c.MaSanPhamNavigation)
                .Where(y => y.MaNhanVien == maNV && y.TrangThaiLapDat != "Đã hoàn thành")
                .OrderByDescending(y => y.NgayLap)
                .ToListAsync();

            return View(installRequests);
        }

        // POST: Xử lý cập nhật trạng thái lắp đặt
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus)
        {
            // 1. Tìm Yêu cầu lắp đặt
            var yeuCau = await _context.YeuCauLapDats
                .Include(y => y.MaDonHangNavigation) // Móc nối sang Đơn Hàng
                .FirstOrDefaultAsync(y => y.MaYeuCauLapDat == id);

            if (yeuCau == null) return NotFound();

            // 2. Cập nhật tiến độ của Kỹ thuật viên
            yeuCau.TrangThaiLapDat = newStatus;

            // =========================================================
            // 3. LOGIC ĐỒNG BỘ ĐƠN HÀNG "2 TRONG 1"
            // =========================================================
            if (newStatus == "Đã hoàn thành" && yeuCau.MaDonHangNavigation != null)
            {
                // Nếu lắp đặt xong -> Mặc định là hàng cũng đã được giao xong
                yeuCau.MaDonHangNavigation.TrangThaiDonHang = "Hoàn thành";

                // Cập nhật luôn ngày hoàn thành đơn (nếu bảng DonHang của bạn có cột này)
                // yeuCau.MaDonHangNavigation.NgayGiaoHang = DateTime.Now; 
            }
            else if (newStatus == "Bị hủy" && yeuCau.MaDonHangNavigation != null)
            {
                // Nếu khách đổi ý hủy lắp đặt giữa chừng và không nhận hàng
                yeuCau.MaDonHangNavigation.TrangThaiDonHang = "Đã hủy";
            }
            // =========================================================

            _context.Update(yeuCau);

            // Lưu một phát là ăn cả 2 bảng (YeuCauLapDat và DonHang)
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật tiến độ thi công mã #{id}";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateInstallationInfo(int id, decimal phiLapDat, DateTime ngayHen, string newStatus)
        {
            var yeuCau = await _context.YeuCauLapDats.FindAsync(id);
            if (yeuCau == null) return NotFound();

            // Cập nhật các thông tin nhân viên vừa chốt với khách
            yeuCau.TrangThaiLapDat = newStatus;
            yeuCau.PhiLapDat = phiLapDat;
            yeuCau.NgayLap = ngayHen; // Cập nhật lại ngày hẹn lắp đặt

            _context.Update(yeuCau);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật thông tin lắp đặt cho mã #{id}";
            return RedirectToAction(nameof(Index));
        }

        // Dùng chung hàm Logout để nhân viên cũng thoát ra được
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
    }
}