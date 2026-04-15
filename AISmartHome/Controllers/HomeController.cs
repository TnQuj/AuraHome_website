using System.Diagnostics;
using AISmartHome.Data;
using AISmartHome.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AISmartHome.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AISmartHomeDbContext _context;

        public HomeController(ILogger<HomeController> logger, AISmartHomeDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            
            var viewModel = new HomeViewModel
            {
                SanPhams = await _context.SanPhams.OrderByDescending(s => s.MaSanPham).Take(8).ToListAsync(),
                DanhMucs = await _context.DanhMucSanPhams.ToListAsync()
            };

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public async Task<IActionResult> SpinWheel(string email)
        {
            try
            {
                email = email.Trim().ToLower();

                // 1. Kiểm tra chống gian lận
                bool daQuay = await _context.VoucherHistories.AnyAsync(v => v.Email == email);
                if (daQuay)
                {
                    return Json(new { success = false, message = "Email này đã tham gia quay thưởng rồi. Hãy nhường cơ hội cho người khác nhé!" });
                }

                // 2. Logic random giải thưởng (100% Trúng - 6 Ô)
                int prizeIndex = 0;
                string prizeName = "";
                string loaiGiamGia = "";
                decimal giaTriGiam = 0;

                var rand = new Random().Next(1, 101); // 1 - 100

                if (rand <= 5) { prizeIndex = 0; prizeName = "Giảm 100k"; loaiGiamGia = "SoTien"; giaTriGiam = 100000; }
                else if (rand <= 20) { prizeIndex = 1; prizeName = "Freeship (Max 30k)"; loaiGiamGia = "SoTien"; giaTriGiam = 30000; }
                else if (rand <= 50) { prizeIndex = 2; prizeName = "Giảm 5%"; loaiGiamGia = "PhanTram"; giaTriGiam = 5; }
                else if (rand <= 65) { prizeIndex = 3; prizeName = "Giảm 50k"; loaiGiamGia = "SoTien"; giaTriGiam = 50000; }
                else if (rand <= 80) { prizeIndex = 4; prizeName = "Freeship (Max 30k)"; loaiGiamGia = "SoTien"; giaTriGiam = 30000; }
                else { prizeIndex = 5; prizeName = "Giảm 10%"; loaiGiamGia = "PhanTram"; giaTriGiam = 10; }

                string voucherCode = $"AURA-{Guid.NewGuid().ToString().Substring(0, 5).ToUpper()}";

                // 3. LƯU VOUCHER MỚI
                var newVoucher = new Voucher
                {
                    Code = voucherCode,
                    MoTa = prizeName,
                    LoaiGiamGia = loaiGiamGia,
                    GiaTriGiam = giaTriGiam,
                    NgayBatDau = DateTime.Now,
                    NgayHetHan = DateTime.Now.AddDays(7), // Cho hạn dùng 7 ngày cho thả ga
                    TrangThai = true,
                    SoLuongToiDa = 1
                };
                _context.Vouchers.Add(newVoucher);
                await _context.SaveChangesAsync();

                // 4. LƯU LỊCH SỬ NHẬN MÃ (Chưa dùng nên NgaySuDung = null)
                _context.VoucherHistories.Add(new VoucherHistory
                {
                    Email = email,
                    MaVoucher = newVoucher.MaVoucher,
                    NgaySuDung = null // <-- Sửa đúng chuẩn Database
                });
                await _context.SaveChangesAsync();

                return Json(new { success = true, prizeIndex = prizeIndex, prizeName = prizeName, voucherCode = voucherCode });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Máy chủ đang quá tải: " + ex.Message });
            }
        }
    }
}
