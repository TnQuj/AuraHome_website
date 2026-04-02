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
            var orders = await _context.DonHangs.ToListAsync();

            foreach (var order in orders)
            {
                // LOGIC TỰ ĐỘNG: Nếu đơn hàng quá 3 ngày chưa giao -> Tự động Hoàn thành (Ví dụ vậy)
                if (order.TrangThaiDonHang == "Đang giao" &&
                    order.NgayDatHang.HasValue &&
                    (DateTime.Now - order.NgayDatHang.Value).TotalDays > 3)
                {
                    order.TrangThaiDonHang = "Hoàn thành";
                }

                // LOGIC TỰ ĐỘNG: Nếu quá 15 phút khách không thanh toán (với đơn online) -> Tự hủy
                // (Áp dụng nếu bạn có cột thời gian cụ thể)
            }

            // Lưu lại các thay đổi tự động nếu có
            await _context.SaveChangesAsync();

            return View(orders);
        }

        // GET: DonHangs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var donHang = await _context.DonHangs
                .Include(d => d.MaKhachHangNavigation)
                .Include(d => d.ChiTietDonHangs) // Lấy danh sách chi tiết
                    .ThenInclude(ct => ct.MaSanPhamNavigation) // Lấy thông tin sản phẩm (tên, hình ảnh)
                .FirstOrDefaultAsync(m => m.MaDonHang == id);

            if (donHang == null) return NotFound();

            return View(donHang);
        }

        // GET: DonHangs/Create
        public IActionResult Create()
        {
            ViewData["MaKhachHang"] = new SelectList(_context.KhachHangs, "MaKhachHang", "MaKhachHang");
            return View();
        }

        // POST: DonHangs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaDonHang,MaKhachHang,TenKhachHang,SoDienThoai,DiaChiGiaoHang,NgayDatHang,TongTien,TrangThaiDonHang")] DonHang donHang)
        {
            if (ModelState.IsValid)
            {
                _context.Add(donHang);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["MaKhachHang"] = new SelectList(_context.KhachHangs, "MaKhachHang", "MaKhachHang", donHang.MaKhachHang);
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
            ViewData["MaKhachHang"] = new SelectList(_context.KhachHangs, "MaKhachHang", "MaKhachHang", donHang.MaKhachHang);
            return View(donHang);
        }

        // POST: DonHangs/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaDonHang,MaKhachHang,TenKhachHang,SoDienThoai,DiaChiGiaoHang,NgayDatHang,TongTien,TrangThaiDonHang")] DonHang donHang)
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
            ViewData["MaKhachHang"] = new SelectList(_context.KhachHangs, "MaKhachHang", "MaKhachHang", donHang.MaKhachHang);
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
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // 1. Tìm tất cả các chi tiết đơn hàng (bảng Con) liên quan đến đơn hàng này
            var chiTietDonHangs = _context.ChiTietDonHangs
                                          .Where(ct => ct.MaDonHang == id);

            // 2. Xóa toàn bộ danh sách chi tiết đơn hàng trước
            if (chiTietDonHangs.Any())
            {
                _context.ChiTietDonHangs.RemoveRange(chiTietDonHangs);
            }

            // 3. Bây giờ mới tìm và xóa Đơn hàng chính (bảng Cha)
            var donHang = await _context.DonHangs.FindAsync(id);
            if (donHang != null)
            {
                _context.DonHangs.Remove(donHang);
            }

            // 4. Lưu thay đổi vào Database
            await _context.SaveChangesAsync();

            // Quay lại danh sách sau khi xóa thành công
            return RedirectToAction(nameof(Index));
        }

        private bool DonHangExists(int id)
        {
            return _context.DonHangs.Any(e => e.MaDonHang == id);
        }
    }
}
