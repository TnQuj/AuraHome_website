using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Data;
using AISmartHome.Models;
using Microsoft.AspNetCore.Http; // Thêm để dùng Session

namespace AISmartHome.Controllers
{
    public class YeuCauLapDatsController : Controller
    {
        private readonly AISmartHomeDbContext _context;

        public YeuCauLapDatsController(AISmartHomeDbContext context)
        {
            _context = context;
        }

        // =========================================================================
        // BẢO VỆ CONTROLLER: CHỈ ADMIN MỚI ĐƯỢC VÀO QUẢN LÝ
        // =========================================================================
        public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                TempData["LoginError"] = "Bạn không có quyền truy cập khu vực Quản lý Yêu Cầu Lắp Đặt!";
                TempData["ShowLoginModal"] = "true";
                context.Result = new RedirectToActionResult("Index", "Home", null);
            }
            base.OnActionExecuting(context);
        }

        // GET: YeuCauLapDats
        public async Task<IActionResult> Index()
        {
            // Nạp sẵn thông tin Đơn Hàng -> Khách Hàng và Nhân Viên để hiển thị ngoài View
            var data = await _context.YeuCauLapDats
                .Include(y => y.MaNhanVienNavigation)
                .Include(y => y.MaDonHangNavigation)
                    .ThenInclude(d => d.MaKhachHangNavigation)
                .ToListAsync();

            return View(data);
        }

        // GET: YeuCauLapDats/Details/5
        // GET: YeuCauLapDats/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var yeuCauLapDat = await _context.YeuCauLapDats
                // 1. Phải có dòng này để lấy thông tin Đơn Hàng và Khách Hàng
                .Include(y => y.MaDonHangNavigation)
                    .ThenInclude(d => d.MaKhachHangNavigation)

                // 2. Phải có dòng này để lấy thông tin Nhân Viên phụ trách
                .Include(y => y.MaNhanVienNavigation)

                .FirstOrDefaultAsync(m => m.MaYeuCauLapDat == id);

            if (yeuCauLapDat == null)
            {
                return NotFound();
            }

            return View(yeuCauLapDat);
        }

        // =========================================================================
        // GET: YeuCauLapDats/Create
        // =========================================================================
        public IActionResult Create()
        {
            // 1. Tạo danh sách Đơn Hàng (Ghép "Đơn #ID - Tên Khách Hàng")
            var danhSachDonHang = _context.DonHangs
                .Include(d => d.MaKhachHangNavigation)
                .Select(d => new
                {
                    MaDonHang = d.MaDonHang,
                    HienThi = $"Đơn #{d.MaDonHang} - " + (d.MaKhachHangNavigation != null ? d.MaKhachHangNavigation.TenKhachHang : "Khách lẻ")
                }).ToList();
            ViewData["MaDonHang"] = new SelectList(danhSachDonHang, "MaDonHang", "HienThi");

            // 2. Tạo danh sách Nhân Viên (Hiển thị Tên Nhân Viên)
            ViewData["MaNhanVien"] = new SelectList(_context.NhanViens, "MaNhanVien", "TenNhanVien");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaYeuCauLapDat,MaDonHang,DiaChiLapDat,NgayLap,TrangThaiLapDat,MaNhanVien,PhiLapDat,GhiChuBaoGia")] YeuCauLapDat yeuCauLapDat)
        {
            // Loại bỏ kiểm tra ModelState.IsValid (Vì Form Create có thể thiếu một vài trường phụ)
            // Miễn là có Đơn Hàng và Địa Chỉ là được phép tạo Yêu cầu

            _context.Add(yeuCauLapDat);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã tạo yêu cầu lắp đặt thành công!";
            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // GET: YeuCauLapDats/Edit/5
        // =========================================================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var yeuCauLapDat = await _context.YeuCauLapDats.FindAsync(id);
            if (yeuCauLapDat == null) return NotFound();

            // 1. Tạo danh sách Đơn Hàng (Ghép "Đơn #ID - Tên Khách Hàng")
            var danhSachDonHang = _context.DonHangs
                .Include(d => d.MaKhachHangNavigation)
                .Select(d => new
                {
                    MaDonHang = d.MaDonHang,
                    HienThi = $"Đơn #{d.MaDonHang} - " + (d.MaKhachHangNavigation != null ? d.MaKhachHangNavigation.TenKhachHang : "Khách lẻ")
                }).ToList();
            ViewData["MaDonHang"] = new SelectList(danhSachDonHang, "MaDonHang", "HienThi", yeuCauLapDat.MaDonHang);

            // 2. Tạo danh sách Nhân Viên (Hiển thị Tên Nhân Viên)
            ViewData["MaNhanVien"] = new SelectList(_context.NhanViens, "MaNhanVien", "TenNhanVien", yeuCauLapDat.MaNhanVien);

            return View(yeuCauLapDat);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, YeuCauLapDat yeuCauLapDat)
        {
            if (id != yeuCauLapDat.MaYeuCauLapDat)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // 1. Cập nhật bảng Yêu Cầu Lắp Đặt trước
                    _context.Update(yeuCauLapDat);

                    // 2. TÌM VÀ CẬP NHẬT ĐƠN HÀNG LIÊN QUAN (LOGIC TỰ ĐỘNG)
                    var donHang = await _context.DonHangs.FindAsync(yeuCauLapDat.MaDonHang);
                    if (donHang != null)
                    {
                        // TRƯỜNG HỢP 1: Admin báo giá và phân công NV
                        // Nếu có nhân viên, có phí lắp đặt và trạng thái không phải là hoàn thành/hủy
                        if (yeuCauLapDat.MaNhanVien != null && yeuCauLapDat.PhiLapDat > 0 && yeuCauLapDat.TrangThaiLapDat == "Đã phân công")
                        {
                            donHang.TrangThaiDonHang = "Đang giao";
                        }

                        // TRƯỜNG HỢP 2: Kỹ thuật viên báo đang thi công
                        else if (yeuCauLapDat.TrangThaiLapDat == "Đang thi công")
                        {
                            donHang.TrangThaiDonHang = "Đang lắp đặt";
                        }

                        // TRƯỜNG HỢP 3: Hoàn tất
                        else if (yeuCauLapDat.TrangThaiLapDat == "Đã hoàn thành")
                        {
                            donHang.TrangThaiDonHang = "Đã hoàn tất";
                            // Tùy chọn: Tự động đánh dấu đơn hàng đã thanh toán luôn
                            // donHang.TrangThaiThanhToan = "Đã thanh toán"; 
                        }

                        // Cập nhật Đơn hàng vào Database
                        _context.Update(donHang);
                    }

                    // Lưu tất cả thay đổi cùng lúc
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!YeuCauLapDatExists(yeuCauLapDat.MaYeuCauLapDat))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            // Nếu Form lỗi thì load lại Dropdown
            ViewData["MaDonHang"] = new SelectList(_context.DonHangs, "MaDonHang", "MaDonHang", yeuCauLapDat.MaDonHang);
            ViewData["MaNhanVien"] = new SelectList(_context.NhanViens, "MaNhanVien", "TenNhanVien", yeuCauLapDat.MaNhanVien);
            return View(yeuCauLapDat);
        }

        // GET: YeuCauLapDats/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var yeuCauLapDat = await _context.YeuCauLapDats
                .Include(y => y.MaDonHangNavigation)
                    .ThenInclude(d => d.MaKhachHangNavigation)
                .Include(y => y.MaNhanVienNavigation)
                .FirstOrDefaultAsync(m => m.MaYeuCauLapDat == id);

            if (yeuCauLapDat == null) return NotFound();

            return View(yeuCauLapDat);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var yeuCauLapDat = await _context.YeuCauLapDats.FindAsync(id);
            if (yeuCauLapDat != null)
            {
                _context.YeuCauLapDats.Remove(yeuCauLapDat);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool YeuCauLapDatExists(int id)
        {
            return _context.YeuCauLapDats.Any(e => e.MaYeuCauLapDat == id);
        }
    }
}