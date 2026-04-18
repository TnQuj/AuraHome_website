using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Data;
using AISmartHome.Models;

namespace AISmartHome.Controllers
{
    public class DonHangsController : Controller
    {
        private readonly AISmartHomeDbContext _context;

        public DonHangsController(AISmartHomeDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Include thêm bảng YeuCauLapDats để biết đơn nào có lắp đặt
            var donHangs = _context.DonHangs
                .Include(d => d.MaKhachHangNavigation)
                .Include(d => d.YeuCauLapDats) // Kéo theo thông tin lắp đặt
                .OrderByDescending(d => d.NgayDatHang);

            return View(await donHangs.ToListAsync());
        }
        // GET: DonHangs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var donHang = await _context.DonHangs
                .Include(d => d.MaKhachHangNavigation)
                .Include(d => d.ChiTietDonHangs)
                    .ThenInclude(c => c.MaSanPhamNavigation)
                .Include(d => d.YeuCauLapDats)
                    .ThenInclude(y => y.MaNhanVienNavigation)
                .FirstOrDefaultAsync(m => m.MaDonHang == id);

            if (donHang == null) return NotFound();

            return View(donHang);
        }

        // GET: DonHangs/Create
        public IActionResult Create()
        {
            ViewData["MaKhachHang"] = new SelectList(_context.KhachHangs, "MaKhachHang", "TenKhachHang");
            return View();
        }

        // POST: DonHangs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaDonHang,MaKhachHang,TenKhachHang,SoDienThoai,Email,DiaChiGiaoHang,GhiChu,NgayDatHang,TongTien,TrangThaiDonHang")] DonHang donHang)
        {
            if (ModelState.IsValid)
            {
                _context.Add(donHang);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["MaKhachHang"] = new SelectList(_context.KhachHangs, "MaKhachHang", "TenKhachHang", donHang.MaKhachHang);
            return View(donHang);
        }

        // GET: DonHangs/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var donHang = await _context.DonHangs.FindAsync(id);
            if (donHang == null)
            {
                return NotFound();
            }
            ViewData["MaKhachHang"] = new SelectList(_context.KhachHangs, "MaKhachHang", "TenKhachHang", donHang.MaKhachHang);
            return View(donHang);
        }

        // POST: DonHangs/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaDonHang,MaKhachHang,TenKhachHang,SoDienThoai,Email,DiaChiGiaoHang,GhiChu,NgayDatHang,TongTien,TrangThaiDonHang")] DonHang donHang)
        {
            if (id != donHang.MaDonHang)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(donHang);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DonHangExists(donHang.MaDonHang))
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
            ViewData["MaKhachHang"] = new SelectList(_context.KhachHangs, "MaKhachHang", "TenKhachHang", donHang.MaKhachHang);
            return View(donHang);
        }

        // GET: DonHangs/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var donHang = await _context.DonHangs
                .Include(d => d.MaKhachHangNavigation)
                .FirstOrDefaultAsync(m => m.MaDonHang == id);
            if (donHang == null)
            {
                return NotFound();
            }

            return View(donHang);
        }

        // POST: DonHangs/Delete/5
        // POST: DonHangs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // 1. Phải Include() để lôi cả các dữ liệu con lên
            var donHang = await _context.DonHangs
                .Include(d => d.YeuCauLapDats)     // Lấy các Yêu cầu lắp đặt
                .Include(d => d.ChiTietDonHangs)   // Lấy các Chi tiết đơn hàng
                .FirstOrDefaultAsync(m => m.MaDonHang == id);

            if (donHang != null)
            {
                // 2. Xóa tất cả Yêu cầu lắp đặt của đơn này (Dọn dẹp ngọn 1)
                if (donHang.YeuCauLapDats.Any())
                {
                    _context.YeuCauLapDats.RemoveRange(donHang.YeuCauLapDats);
                }

                // 3. Xóa tất cả Chi tiết đơn hàng (Dọn dẹp ngọn 2)
                if (donHang.ChiTietDonHangs.Any())
                {
                    _context.ChiTietDonHangs.RemoveRange(donHang.ChiTietDonHangs);
                }

                // 4. Khi con cái đã bị xóa sạch, giờ mới Xóa Đơn Hàng (Bứng gốc)
                _context.DonHangs.Remove(donHang);

                // 5. Lưu lại toàn bộ thay đổi
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đã xóa thành công đơn hàng và các dữ liệu liên quan!";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool DonHangExists(int id)
        {
            return _context.DonHangs.Any(e => e.MaDonHang == id);
        }
    }
}
