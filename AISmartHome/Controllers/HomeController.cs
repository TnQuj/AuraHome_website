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
                // 1. Kiểm tra email đã tham gia quay chưa
                bool daQuay = await _context.VoucherHistories.AnyAsync(v => v.Email == email);
                if (daQuay)
                {
                    return Json(new { success = false, message = "Email này đã tham gia quay thưởng!" });
                }

                // 2. Logic random giải thưởng (100% Trúng - 6 Ô)
                int prizeIndex = 0;
                string prizeName = "";
                string loaiGiamGia = "";
                decimal giaTriGiam = 0;

                var rand = new Random().Next(1, 101); // Lấy số ngẫu nhiên từ 1 - 100

                // Phân bổ tỷ lệ:
                // 5% trúng Giảm 100k (Ô số 0)
                // 15% trúng Freeship (Ô số 1)
                // 30% trúng Giảm 5% (Ô số 2)
                // 15% trúng Giảm 50k (Ô số 3)
                // 15% trúng Freeship (Ô số 4)
                // 20% trúng Giảm 10% (Ô số 5)

                if (rand <= 5)
                {
                    prizeIndex = 0; prizeName = "Giảm 100k"; loaiGiamGia = "SoTien"; giaTriGiam = 100000;
                }
                else if (rand <= 20)
                {
                    prizeIndex = 1; prizeName = "Freeship (Tối đa 30k)"; loaiGiamGia = "SoTien"; giaTriGiam = 30000; // Quy đổi Freeship thành giảm 30k tiền vận chuyển
                }
                else if (rand <= 50)
                {
                    prizeIndex = 2; prizeName = "Giảm 5%"; loaiGiamGia = "PhanTram"; giaTriGiam = 5;
                }
                else if (rand <= 65)
                {
                    prizeIndex = 3; prizeName = "Giảm 50k"; loaiGiamGia = "SoTien"; giaTriGiam = 50000;
                }
                else if (rand <= 80)
                {
                    prizeIndex = 4; prizeName = "Freeship (Tối đa 30k)"; loaiGiamGia = "SoTien"; giaTriGiam = 30000;
                }
                else
                {
                    prizeIndex = 5; prizeName = "Giảm 10%"; loaiGiamGia = "PhanTram"; giaTriGiam = 10;
                }

                // Sinh mã ngẫu nhiên: AURA-XXXXX
                string voucherCode = $"AURA-{Guid.NewGuid().ToString().Substring(0, 5).ToUpper()}";

                // 3. LƯU VÀO DATABASE (Vì 100% trúng voucher nên không cần check rớt mâm nữa)
                // Trong hàm SpinWheel
                var newVoucher = new Voucher
                {
                    Code = voucherCode,
                    MoTa = prizeName,
                    LoaiGiamGia = loaiGiamGia,
                    GiaTriGiam = giaTriGiam,
                    NgayBatDau = DateTime.Now,
                    // THIẾT LẬP HẾT HẠN: Ví dụ sau 24 giờ
                    NgayHetHan = DateTime.Now.AddHours(24),
                    TrangThai = true,
                    SoLuongToiDa = 1
                };

                _context.Vouchers.Add(newVoucher);
                await _context.SaveChangesAsync(); // Phải lưu trước để có ID Voucher

                _context.VoucherHistories.Add(new VoucherHistory
                {
                    Email = email,
                    MaVoucher = newVoucher.MaVoucher, // Nối khóa ngoại
                    NgaySuDung = DateTime.Now // Lưu thời điểm nhận mã
                });
                await _context.SaveChangesAsync();

                // Trả kết quả về cho Javascript để xoay vòng quay đến đúng ô
                return Json(new { success = true, prizeIndex = prizeIndex, prizeName = prizeName, voucherCode = voucherCode });
            }
            catch (Exception ex)
            {
                string loiChiTiet = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "LỖI HỆ THỐNG: " + loiChiTiet });
            }
        }
    }
}
