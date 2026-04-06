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
        // Không cần dùng [Bind] nữa vì chúng ta sẽ map dữ liệu thủ công cực kỳ an toàn
        public async Task<IActionResult> Edit(int id, YeuCauLapDat model)
        {
            if (id != model.MaYeuCauLapDat)
            {
                return NotFound();
            }

            try
            {
                // 1. Kéo dữ liệu CŨ từ CSDL lên
                var existingRecord = await _context.YeuCauLapDats.FindAsync(id);
                if (existingRecord == null)
                {
                    return NotFound();
                }

                // 2. Bỏ qua kiểm tra ModelState. Chỉ chép đè những trường MỚI từ form lên đối tượng CŨ
                existingRecord.NgayLap = model.NgayLap;                 // Ngày giờ hẹn CẬP NHẬT TẠI ĐÂY
                existingRecord.PhiLapDat = model.PhiLapDat;             // Phí lắp đặt
                existingRecord.GhiChuBaoGia = model.GhiChuBaoGia;       // Ghi chú thi công
                existingRecord.TrangThaiLapDat = model.TrangThaiLapDat; // Trạng thái
                existingRecord.MaNhanVien = model.MaNhanVien;           // Nhân viên phụ trách

                // (Lưu ý: Không đụng chạm gì đến MaDonHang hay DiaChiLapDat, giữ nguyên như cũ)

                // 3. Lưu lại
                _context.Update(existingRecord);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đã cập nhật thông tin chốt với khách thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!YeuCauLapDatExists(model.MaYeuCauLapDat))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
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