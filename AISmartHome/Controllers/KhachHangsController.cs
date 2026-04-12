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
    public class KhachHangsController : Controller
    {
        private readonly AISmartHomeDbContext _context;

        public KhachHangsController(AISmartHomeDbContext context)
        {
            _context = context;
        }

        // GET: KhachHangs
        public async Task<IActionResult> Index()
        {
            return View(await _context.KhachHangs.ToListAsync());
        }

        // GET: KhachHangs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var khachHang = await _context.KhachHangs
                .FirstOrDefaultAsync(m => m.MaKhachHang == id);
            if (khachHang == null)
            {
                return NotFound();
            }

            return View(khachHang);
        }

        // GET: KhachHangs/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: KhachHangs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaKhachHang,TenKhachHang,SoDienThoai,Email,DiaChi,DiemTichLuy,HangThanhVien")] KhachHang khachHang)
        {
            if (ModelState.IsValid)
            {
                _context.Add(khachHang);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(khachHang);
        }

        // GET: KhachHangs/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var khachHang = await _context.KhachHangs.FindAsync(id);
            if (khachHang == null)
            {
                return NotFound();
            }
            return View(khachHang);
        }

        // POST: KhachHangs/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaKhachHang,TenKhachHang,SoDienThoai,Email,DiaChi,DiemTichLuy,HangThanhVien")] KhachHang khachHang)
        {
            if (id != khachHang.MaKhachHang)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(khachHang);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!KhachHangExists(khachHang.MaKhachHang))
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
            return View(khachHang);
        }

        // GET: KhachHangs/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var khachHang = await _context.KhachHangs
                .FirstOrDefaultAsync(m => m.MaKhachHang == id);
            if (khachHang == null)
            {
                return NotFound();
            }

            return View(khachHang);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var khachHang = await _context.KhachHangs.FindAsync(id);
            if (khachHang != null)
            {
                // BƯỚC 1: Kiểm tra Hóa đơn mua hàng (Giữ nguyên)
                var coDonHang = await _context.DonHangs.AnyAsync(d => d.MaKhachHang == id);
                if (coDonHang)
                {
                    TempData["Error"] = "Không thể xóa! Khách hàng này đã có lịch sử mua hàng.";
                    return RedirectToAction(nameof(Index));
                }

                // BƯỚC 2: Dọn dẹp Giỏ Hàng và CHI TIẾT GIỎ HÀNG
                var gioHangs = _context.GioHangs.Where(g => g.MaKhachHang == id).ToList();
                if (gioHangs.Any())
                {
                    // Lấy danh sách các Mã Giỏ Hàng của khách này
                    var maGioHangs = gioHangs.Select(g => (int?)g.MaGioHang).ToList();
                    // 2.1: Xóa các Sản phẩm nằm trong Giỏ hàng (ChiTietGioHang) TRƯỚC
                    var chiTietGioHangs = _context.ChiTietGioHangs.Where(ct => maGioHangs.Contains(ct.MaGioHang));
                    if (chiTietGioHangs.Any())
                    {
                        _context.ChiTietGioHangs.RemoveRange(chiTietGioHangs);
                    }

                    // 2.2: Sau khi giỏ đã trống, tiến hành xóa Giỏ Hàng
                    _context.GioHangs.RemoveRange(gioHangs);
                }

                // BƯỚC 3: Dọn dẹp lịch sử Voucher (Giữ nguyên)
                var voucherHistories = _context.VoucherHistories.Where(v => v.MaKhachHang == id);
                if (voucherHistories.Any())
                {
                    _context.VoucherHistories.RemoveRange(voucherHistories);
                }

                // BƯỚC 4: Cuối cùng, bứng gốc Khách hàng
                _context.KhachHangs.Remove(khachHang);

                // Lưu toàn bộ tiến trình dọn dẹp xuống Database 1 lần duy nhất
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đã xóa khách hàng thành công!";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool KhachHangExists(int id)
        {
            return _context.KhachHangs.Any(e => e.MaKhachHang == id);
        }
    }
}
