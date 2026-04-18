using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Models;
using AISmartHome.Data;
using Microsoft.AspNetCore.Http; // Bắt buộc để dùng ISession
using System.Text.Json;          // Bắt buộc để dùng JsonSerializer
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.SignalR;
using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.AspNetCore.Authentication;

namespace AISmartHome.Controllers
{
    public class CustomersController : Controller
    {
        private readonly AISmartHomeDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IHubContext<Hubs.OrderHub> _hubContext;

        public CustomersController(AISmartHomeDbContext context, IMemoryCache cache, IHubContext<Hubs.OrderHub> hubContext)
        {
            _context = context;
            _cache = cache;
            _hubContext = hubContext;
        }

        private async Task<int> GetOrSetGuestCustomerId()
        {
            int? validCustomerId = null;

            // 1. Đọc ID từ Cookie hoặc Session
            if (Request.Cookies.TryGetValue("GuestCustomerId", out string cookieId) && int.TryParse(cookieId, out int parsedCookieId))
            {
                validCustomerId = parsedCookieId;
            }
            else if (int.TryParse(HttpContext.Session.GetString("GuestCustomerId"), out int parsedSessionId))
            {
                validCustomerId = parsedSessionId;
            }

            // 2. KIỂM TRA KHÁCH CŨ & GIA HẠN THỜI GIAN TRUY CẬP
            if (validCustomerId.HasValue)
            {
                var existingGuest = await _context.KhachHangs.FindAsync(validCustomerId.Value);
                if (existingGuest != null)
                {
                    // 👇 Khách quay lại -> Cập nhật mốc thời gian truy cập mới nhất
                    existingGuest.ThoiGianTruyCap = DateTime.Now;
                    await _context.SaveChangesAsync();

                    return validCustomerId.Value;
                }
            }

            // 3. TẠO KHÁCH MỚI TOANH NẾU CHƯA CÓ
            var newGuest = new KhachHang
            {
                TenKhachHang = "Khách truy cập",
                SoDienThoai = "",
                DiaChi = "",
                ThoiGianTruyCap = DateTime.Now // 👇 Đánh dấu thời gian truy cập lần đầu
            };

            _context.KhachHangs.Add(newGuest);
            await _context.SaveChangesAsync();

            // 4. Cấp Cookie (Sống 30 ngày trên trình duyệt)
            CookieOptions options = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(30),
                HttpOnly = true,
                IsEssential = true,
                Path = "/"
            };
            Response.Cookies.Append("GuestCustomerId", newGuest.MaKhachHang.ToString(), options);
            HttpContext.Session.SetString("GuestCustomerId", newGuest.MaKhachHang.ToString());

            return newGuest.MaKhachHang;
        }

        private async Task<GioHang> GetOrCreateCartAsync(int maKhachHang)
        {
            var cart = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs)
                    .ThenInclude(c => c.MaSanPhamNavigation)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHang);

            if (cart == null)
            {
                cart = new GioHang
                {
                    MaKhachHang = maKhachHang,
                    NgayTao = DateTime.Now,
                    TongTien = 0
                };
                _context.GioHangs.Add(cart);
                await _context.SaveChangesAsync();
            }
            return cart;
        }

        // =======================================================
        // TRANG DANH SÁCH & TRANG CHỦ
        // =======================================================
        public async Task<IActionResult> Index(int? category, string sortOrder)
        {
            ViewBag.CurrentCategory = category;

            var viewModel = new HomeViewModel
            {
                DanhMucs = await _context.DanhMucSanPhams.ToListAsync()
            };

            var query = _context.SanPhams.AsQueryable();

            if (category.HasValue)
            {
                query = query.Where(sp => sp.MaDanhMuc == category.Value);
            }

            switch (sortOrder)
            {
                case "price_asc": query = query.OrderBy(sp => sp.GiaBan); break;
                case "price_desc": query = query.OrderByDescending(sp => sp.GiaBan); break;
                case "name_asc": query = query.OrderBy(sp => sp.TenSanPham); break;
                case "name_desc": query = query.OrderByDescending(sp => sp.TenSanPham); break;
                default: query = query.OrderByDescending(sp => sp.MaSanPham); break;
            }

            viewModel.SanPhams = await query.ToListAsync();
            return View(viewModel);
        }

        // =======================================================
        // TÌM KIẾM SẢN PHẨM
        // =======================================================
        [Route("Customers/Search")]
        public async Task<IActionResult> Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return RedirectToAction("Index");
            }

            ViewBag.Keyword = keyword;

            var searchResults = await _context.SanPhams
                .Where(s => s.TenSanPham.Contains(keyword) || s.MoTa.Contains(keyword))
                .OrderByDescending(s => s.MaSanPham)
                .ToListAsync();

            return View(searchResults);
        }

        // =======================================================
        // TRANG CHI TIẾT SẢN PHẨM
        // =======================================================
        public async Task<IActionResult> Detail(int? id)
        {
            if (id == null) return NotFound();

            var sanPham = await _context.SanPhams
                .Include(sp => sp.HinhAnhSanPhams)
                .FirstOrDefaultAsync(m => m.MaSanPham == id);

            if (sanPham == null) return NotFound();

            ViewBag.SanPhamsTuongTu = await _context.SanPhams
                .Where(x => x.MaDanhMuc == sanPham.MaDanhMuc && x.MaSanPham != id)
                .Take(4)
                .ToListAsync();

            return View(sanPham);
        }

        // =======================================================
        // QUẢN LÝ GIỎ HÀNG (AJAX & TRANG CHÍNH)
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> Cart() // (Hoặc tên hàm giỏ hàng của bạn)
        {
            // 1. Lấy ID từ Cookie cực chuẩn xác
            int guestId = await GetOrSetGuestCustomerId();

            // 2. Tìm giỏ hàng của đúng ID này
            var cart = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs)
                    .ThenInclude(c => c.MaSanPhamNavigation) // Nhớ Include Sản phẩm để hiển thị tên, ảnh
                .FirstOrDefaultAsync(g => g.MaKhachHang == guestId);

            // 3. Nếu chưa có giỏ thì tạo giỏ rỗng truyền ra View
            if (cart == null)
            {
                cart = new GioHang { MaKhachHang = guestId, ChiTietGioHangs = new List<ChiTietGioHang>() };
            }

            return View(cart);
        }

        [HttpPost]
        public async Task<IActionResult> AddToCartAjax(int id, int quantity = 1)
        {
            int maKhachHangHienTai = await GetOrSetGuestCustomerId();
            var cart = await GetOrCreateCartAsync(maKhachHangHienTai);
            var sp = await _context.SanPhams.FindAsync(id);

            if (sp == null) return Json(new { success = false, message = "Không tìm thấy sản phẩm" });

            // 👇 1. LẤY SỐ LƯỢNG KHÁCH ĐÃ CÓ TRONG GIỎ
            var chiTiet = cart.ChiTietGioHangs.FirstOrDefault(c => c.MaSanPham == id);
            int soLuongTrongGio = chiTiet != null ? (chiTiet.SoLuong ?? 0) : 0;

            // 👇 2. KIỂM TRA TỒN KHO GẮT GAO
            // (Thay sp.SoLuongTon bằng đúng tên thuộc tính tồn kho của bạn)
            if (soLuongTrongGio + quantity > sp.SoLuong)
            {
                return Json(new
                {
                    success = false,
                    message = $"Rất tiếc! Trong kho chỉ còn {sp.SoLuong} sản phẩm. Bạn đã có {soLuongTrongGio} cái trong giỏ."
                });
            }

            // 👇 3. Nếu qua ải kiểm tra, tiến hành thêm bình thường
            if (chiTiet != null)
            {
                chiTiet.SoLuong += quantity;
            }
            else
            {
                _context.ChiTietGioHangs.Add(new ChiTietGioHang
                {
                    MaGioHang = cart.MaGioHang,
                    MaSanPham = id,
                    SoLuong = quantity,
                    Gia = sp.GiaBan
                });
            }
            await _context.SaveChangesAsync();

            cart.TongTien = cart.ChiTietGioHangs.Sum(c => c.SoLuong * c.Gia);
            _context.GioHangs.Update(cart);
            await _context.SaveChangesAsync();

            var updatedCart = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs).ThenInclude(c => c.MaSanPhamNavigation)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            var items = updatedCart.ChiTietGioHangs.Select(c => new {
                maSanPham = c.MaSanPham,
                tenSanPham = c.MaSanPhamNavigation.TenSanPham,
                hinhAnh = string.IsNullOrEmpty(c.MaSanPhamNavigation.HinhAnh) ? "https://via.placeholder.com/150" : $"/img/{c.MaSanPhamNavigation.HinhAnh}",
                soLuong = c.SoLuong,
                gia = c.Gia,
                thanhTien = c.SoLuong * c.Gia
            });

            return Json(new
            {
                success = true,
                // ĐỔI TỪ: totalItems = items.Sum(x => x.soLuong),
                totalItems = items.Count(), // Lấy số lượng loại sản phẩm
                totalPrice = updatedCart.TongTien,
                items = items
            });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromCartAjax(int id)
        {
            int maKhachHangHienTai = await GetOrSetGuestCustomerId(); // Đã bảo mật
            var cart = await GetOrCreateCartAsync(maKhachHangHienTai);

            var chiTiet = cart.ChiTietGioHangs.FirstOrDefault(c => c.MaSanPham == id);
            if (chiTiet != null)
            {
                _context.ChiTietGioHangs.Remove(chiTiet);
                await _context.SaveChangesAsync();

                cart.TongTien = cart.ChiTietGioHangs.Where(c => c.MaSanPham != id).Sum(c => (c.SoLuong ?? 0) * (c.Gia ?? 0));
                _context.GioHangs.Update(cart);
                await _context.SaveChangesAsync();
            }

            var updatedCart = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs).ThenInclude(c => c.MaSanPhamNavigation)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            if (updatedCart == null || updatedCart.ChiTietGioHangs == null || !updatedCart.ChiTietGioHangs.Any())
            {
                return Json(new { success = true, totalItems = 0, totalPrice = 0, items = new object[0] });
            }

            var items = updatedCart.ChiTietGioHangs.Select(c => new {
                maSanPham = c.MaSanPham,
                tenSanPham = c.MaSanPhamNavigation?.TenSanPham ?? "",
                hinhAnh = string.IsNullOrEmpty(c.MaSanPhamNavigation?.HinhAnh) ? "https://via.placeholder.com/150" : $"/img/{c.MaSanPhamNavigation?.HinhAnh}",
                soLuong = c.SoLuong,
                gia = c.Gia,
                thanhTien = (c.SoLuong ?? 0) * (c.Gia ?? 0)
            }).ToList();

            return Json(new
            {
                success = true,
                // ĐỔI TỪ: totalItems = items.Sum(x => x.soLuong) ?? 0,
                totalItems = items.Count, // Lấy số lượng loại sản phẩm
                totalPrice = updatedCart.TongTien ?? 0,
                items = items
            });
        }

        [HttpGet]
        public async Task<IActionResult> RemoveFromCart(int id)
        {
            int maKhachHangHienTai = await GetOrSetGuestCustomerId(); // Đã bảo mật
            var cart = await GetOrCreateCartAsync(maKhachHangHienTai);

            var chiTiet = cart.ChiTietGioHangs.FirstOrDefault(c => c.MaSanPham == id);
            if (chiTiet != null)
            {
                _context.ChiTietGioHangs.Remove(chiTiet);
                await _context.SaveChangesAsync();

                cart.TongTien = cart.ChiTietGioHangs.Where(c => c.MaSanPham != id).Sum(c => (c.SoLuong ?? 0) * (c.Gia ?? 0));
                _context.GioHangs.Update(cart);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Cart");
        }

        [HttpGet]
        public async Task<IActionResult> GetMiniCartAjax()
        {
            int maKhachHangHienTai = await GetOrSetGuestCustomerId(); // Đã bảo mật
            var cart = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs).ThenInclude(c => c.MaSanPhamNavigation)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            if (cart == null || cart.ChiTietGioHangs == null || !cart.ChiTietGioHangs.Any())
            {
                return Json(new { success = true, totalItems = 0, totalPrice = 0, items = new object[0] });
            }

            var items = cart.ChiTietGioHangs.Select(c => new {
                maSanPham = c.MaSanPham,
                tenSanPham = c.MaSanPhamNavigation?.TenSanPham ?? "",
                hinhAnh = string.IsNullOrEmpty(c.MaSanPhamNavigation?.HinhAnh) ? "https://via.placeholder.com/150" : $"/img/{c.MaSanPhamNavigation?.HinhAnh}",
                soLuong = c.SoLuong,
                gia = c.Gia,
                thanhTien = (c.SoLuong ?? 0) * (c.Gia ?? 0)
            }).ToList();

            return Json(new
            {
                success = true,
                // ĐỔI TỪ: totalItems = items.Sum(x => x.soLuong) ?? 0,
                totalItems = items.Count, // Lấy số lượng loại sản phẩm
                totalPrice = cart.TongTien ?? 0,
                items = items
            });
        }

        public class CartUpdateModel
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCart([FromBody] CartUpdateModel model)
        {
            int maKhachHangHienTai = await GetOrSetGuestCustomerId(); // Đã bảo mật

            // BẮT BUỘC: Phải Include ChiTietGioHangs để có thể tính lại tổng tiền
            var gioHang = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            if (gioHang == null) return Json(new { success = false, message = "Lỗi giỏ hàng" });

            var cartItem = gioHang.ChiTietGioHangs
                .FirstOrDefault(c => c.MaSanPham == model.ProductId);

            if (cartItem != null)
            {
                // 1. Cập nhật số lượng mới
                cartItem.SoLuong = model.Quantity;
                _context.ChiTietGioHangs.Update(cartItem);

                // 2. TÍNH LẠI TỔNG TIỀN CHO TOÀN BỘ GIỎ HÀNG NGAY TẠI ĐÂY
                gioHang.TongTien = gioHang.ChiTietGioHangs.Sum(c => (c.SoLuong ?? 0) * (c.Gia ?? 0));
                _context.GioHangs.Update(gioHang);

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Không tìm thấy sản phẩm" });
        }

        [HttpPost]
        public async Task<IActionResult> RestoreCartAjax(string phone, string otp, string name)
        {
            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(otp))
            {
                return Json(new { success = false, message = "Lỗi: Thiếu số điện thoại hoặc mã OTP!" });
            }

            // 1. LÀM SẠCH SĐT (Giống hệt bên OtpController)
            string cleanPhone = phone.Replace(" ", "").Replace(".", "").Replace("-", "").Trim();
            if (cleanPhone.StartsWith("84")) cleanPhone = "0" + cleanPhone.Substring(2);

            // 2. TÌM MÃ OTP TRONG CACHE VÀ ĐỐI CHIẾU
            _cache.TryGetValue($"OTP_{cleanPhone}", out string savedOtp);

            string codeTuKhachHang = otp.Trim();

            // Chẩn đoán lỗi
            if (string.IsNullOrEmpty(savedOtp))
            {
                return Json(new { success = false, message = $"Lỗi Cache: Đã hết hạn hoặc mất mã của SĐT {cleanPhone}!" });
            }

            if (savedOtp != codeTuKhachHang)
            {
                return Json(new { success = false, message = $"Lỗi Lệch Mã: Bạn nhập {codeTuKhachHang}, nhưng mã đúng là {savedOtp}!" });
            }

            // 3. NẾU MÃ ĐÚNG -> Xóa mã đi để bảo mật
            _cache.Remove($"OTP_{cleanPhone}");

            // =========================================================
            // ĐOẠN DƯỚI ĐÂY LÀ LOGIC KHÔI PHỤC GIỎ HÀNG CŨ CỦA BẠN 
            // (Chỉ tham khảo, bạn hãy giữ nguyên phần xử lý DB cũ của bạn nhé)
            // =========================================================

            // Ví dụ: Lưu Session cho khách vãng lai
            HttpContext.Session.SetString("CustomerName", name);
            HttpContext.Session.SetString("CustomerPhone", cleanPhone);
            HttpContext.Session.SetString("VerifiedPhone", cleanPhone); // Cấp quyền miễn OTP lúc thanh toán

            // Xử lý giỏ hàng trong Database...
            // ...

            return Json(new { success = true });
        }
        // =======================================================
        // MUA NGAY & THANH TOÁN
        // =======================================================
        
        [Route("Customers/BuyNow")]
        public async Task<IActionResult> BuyNow(int id, int quantity = 1)
        {
            int maKhachHangHienTai = await GetOrSetGuestCustomerId();

            var sanPham = await _context.SanPhams.FindAsync(id);
            if (sanPham == null) return NotFound("Sản phẩm không tồn tại.");
            if (quantity > sanPham.SoLuong)
            {
                TempData["ErrorMessage"] = $"Rất tiếc! Sản phẩm này hiện chỉ còn {sanPham.SoLuong} chiếc trong kho.";

                // Trả khách về lại trang xem chi tiết hoặc trang chủ
                return RedirectToAction("Detail", new { id = id });
            }
            var gioHang = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            if (gioHang == null)
            {
                gioHang = new GioHang { MaKhachHang = maKhachHangHienTai };
                _context.GioHangs.Add(gioHang);
                await _context.SaveChangesAsync();
            }
            else
            {
                if (gioHang.ChiTietGioHangs != null && gioHang.ChiTietGioHangs.Any())
                {
                    _context.ChiTietGioHangs.RemoveRange(gioHang.ChiTietGioHangs);
                    await _context.SaveChangesAsync();
                }
            }

            var newItem = new ChiTietGioHang
            {
                MaGioHang = gioHang.MaGioHang,
                MaSanPham = id,
                SoLuong = quantity,
                Gia = sanPham.GiaBan
            };

            _context.ChiTietGioHangs.Add(newItem);
            await _context.SaveChangesAsync();

            return RedirectToAction("CheckOut");
        }

        [Route("Customers/Checkout")]
        // THÊM THAM SỐ: List<int> selectedProducts
        public async Task<IActionResult> Checkout([FromQuery] List<int> selectedProducts)
        {
            int maKhachHangHienTai = await GetOrSetGuestCustomerId();

            var gioHang = await _context.GioHangs
                .Include(g => g.ChiTietGioHangs)
                    .ThenInclude(c => c.MaSanPhamNavigation)
                .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

            if (gioHang == null || gioHang.ChiTietGioHangs == null || !gioHang.ChiTietGioHangs.Any())
            {
                return RedirectToAction("Index");
            }

            // LỌC: CHỈ LẤY NHỮNG SẢN PHẨM KHÁCH HÀNG ĐÃ TICK CHỌN
            if (selectedProducts != null && selectedProducts.Any())
            {
                gioHang.ChiTietGioHangs = gioHang.ChiTietGioHangs
                    .Where(c => selectedProducts.Contains(c.MaSanPham ?? 0))
                    .ToList();

                // Nếu chọn xong mà mảng rỗng (khách hack F12), đẩy về giỏ hàng
                if (!gioHang.ChiTietGioHangs.Any()) return RedirectToAction("Cart");
            }
            else
            {
                // Bắt lỗi nếu khách cố tình gõ link trực tiếp mà không chọn gì
                return RedirectToAction("Cart");
            }

            // =======================================================
            // 👇 CHÈN THÊM ĐOẠN NÀY ĐỂ ÉP THÔNG TIN TÀI KHOẢN VIP VÀO FORM
            // =======================================================
            bool isVerified = false;
            string autoPhone = "";

            // 1. Khách VIP (Đã đăng nhập)
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var identityName = User.Identity.Name;
                var loggedInUser = await _context.KhachHangs.FirstOrDefaultAsync(k =>
                    k.Email == identityName || k.SoDienThoai == identityName);

                if (loggedInUser != null)
                {
                    ViewBag.VipName = loggedInUser.TenKhachHang;
                    autoPhone = loggedInUser.SoDienThoai; // Lấy SĐT từ DB
                    ViewBag.VipEmail = loggedInUser.Email;

                    isVerified = true; // Cấp giấy thông hành!
                }
            }
            // 2. Khách Vãng Lai (Đã từng nhập OTP trước đó)
            else
            {
                string cookiePhone = Request.Cookies["VerifiedPhone"] ?? "";
                if (!string.IsNullOrEmpty(cookiePhone))
                {
                    autoPhone = cookiePhone; // Lấy SĐT từ Cookie

                    isVerified = true; // Cấp giấy thông hành!
                }
            }

            // Truyền dữ liệu xuống giao diện HTML
            ViewBag.VipPhone = autoPhone;
            ViewBag.IsVerified = isVerified; // Biến quan trọng nhất: Báo cho JS biết để bỏ qua OTP!

            return View(gioHang);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(
            string FullName, string Phone, string Email,
            string Province, string District, string Address,
            string Note, string PaymentMethod, string PaymentMode,
            string VoucherCode, string OtpCode, string GhiChu,
            List<int> SelectedProducts, bool CanLapDat = false)
        {
            // 1. Ổ KHÓA CHỐNG SPAM (Dùng biến guestId)
            int guestId = await GetOrSetGuestCustomerId();
            string orderLockKey = $"Lock_PlaceOrder_{guestId}";

            if (_cache.TryGetValue(orderLockKey, out _))
                return Json(new { success = false, message = "Hệ thống đang xử lý, vui lòng đợi giây lát!" });

            _cache.Set(orderLockKey, true, TimeSpan.FromSeconds(15));

            // Dùng Transaction để bảo vệ dữ liệu toàn vẹn
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Chỉ khai báo isNewAccount 1 lần ở đây
                    bool isNewAccount = false;

                    string cleanPhone = Phone?.Replace(" ", "").Replace(".", "").Replace("-", "").Trim() ?? "";
                    if (cleanPhone.StartsWith("84")) cleanPhone = "0" + cleanPhone.Substring(2);

                    // =======================================================
                    // 2. XỬ LÝ SANG TÊN ĐỔI CHỦ TÀI KHOẢN & GIỎ HÀNG
                    // =======================================================
                    var khachHang = await _context.KhachHangs
                        .FirstOrDefaultAsync(k => k.SoDienThoai == cleanPhone || (k.Email == Email && !string.IsNullOrEmpty(Email)));

                    if (khachHang == null)
                    {
                        // 🌟 TRƯỜNG HỢP 1: KHÁCH MỚI TOANH
                        isNewAccount = true;

                        // Lấy lại tài khoản tạm (Guest) mà hệ thống cấp lúc thêm vào giỏ
                        khachHang = await _context.KhachHangs.FindAsync(guestId);

                        if (khachHang != null)
                        {
                            // Nâng cấp tài khoản tạm thành CHÍNH THỨC
                            khachHang.TenKhachHang = FullName;
                            khachHang.SoDienThoai = cleanPhone;
                            khachHang.Email = Email;
                            khachHang.DiaChi = $"{Address}, {District}, {Province}";
                            _context.KhachHangs.Update(khachHang);
                        }
                        else
                        {
                            khachHang = new KhachHang { TenKhachHang = FullName, SoDienThoai = cleanPhone, Email = Email, DiaChi = $"{Address}, {District}, {Province}", ThoiGianTruyCap = DateTime.Now };
                            _context.KhachHangs.Add(khachHang);
                        }
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        // 🌟 TRƯỜNG HỢP 2: KHÁCH CŨ QUAY LẠI
                        khachHang.TenKhachHang = FullName;
                        khachHang.DiaChi = $"{Address}, {District}, {Province}";
                        if (!string.IsNullOrWhiteSpace(Email))
                        {
                            khachHang.Email = Email;
                        }
                        _context.KhachHangs.Update(khachHang);

                        // Gom giỏ hàng tạm (nếu có) về tài khoản chính thức
                        var gioHangTam = await _context.GioHangs
                            .Include(g => g.ChiTietGioHangs) // Phải Include ruột
                            .FirstOrDefaultAsync(g => g.MaKhachHang == guestId);

                        if (gioHangTam != null && gioHangTam.MaKhachHang != khachHang.MaKhachHang)
                        {
                            // Tìm xem khách cũ đã có giỏ hàng gốc nào chưa
                            var gioHangGoc = await _context.GioHangs.FirstOrDefaultAsync(g => g.MaKhachHang == khachHang.MaKhachHang);

                            if (gioHangGoc != null)
                            {
                                // Nếu khách đã có giỏ hàng gốc -> Trút hết đồ từ giỏ tạm sang giỏ gốc
                                if (gioHangTam.ChiTietGioHangs != null)
                                {
                                    foreach (var item in gioHangTam.ChiTietGioHangs)
                                    {
                                        item.MaGioHang = gioHangGoc.MaGioHang;
                                        _context.ChiTietGioHangs.Update(item);
                                    }
                                }
                                // Trút đồ xong thì xóa luôn cái xác giỏ tạm đi cho rảnh nợ DB
                                _context.GioHangs.Remove(gioHangTam);
                            }
                            else
                            {
                                // Nếu khách cũ chưa có giỏ hàng nào (đã bị xóa) -> Đổi tên chủ giỏ tạm là xong
                                gioHangTam.MaKhachHang = khachHang.MaKhachHang;
                                _context.GioHangs.Update(gioHangTam);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // =======================================================
                    // 3. LẤY GIỎ HÀNG VÀ TÍNH TIỀN (Khắc phục lỗi mất biến)
                    // =======================================================
                    var gioHang = await _context.GioHangs.Include(g => g.ChiTietGioHangs).FirstOrDefaultAsync(g => g.MaKhachHang == khachHang.MaKhachHang);
                    var itemsToBuy = gioHang?.ChiTietGioHangs.ToList() ?? new List<ChiTietGioHang>();

                    if (SelectedProducts != null && SelectedProducts.Any())
                        itemsToBuy = itemsToBuy.Where(c => SelectedProducts.Contains(c.MaSanPham ?? 0)).ToList();

                    if (!itemsToBuy.Any())
                    {
                        await transaction.RollbackAsync(); // Giỏ trống thì lùi lại, mở khóa

                        // 👇 ĐÃ THÊM emptyCart = true VÀO ĐÂY
                        return Json(new { success = false, emptyCart = true, message = "Giỏ hàng trống hoặc đã được thanh toán!" });
                    }

                    decimal tongTien = itemsToBuy.Sum(c => (c.SoLuong ?? 0) * (c.Gia ?? 0));

                    // =======================================================
                    // 3.5. XỬ LÝ VOUCHER: TRỪ TIỀN & ĐÁNH DẤU ĐÃ SỬ DỤNG
                    // =======================================================
                    VoucherHistory lichSuVoucher = null;
                    decimal soTienGiam = 0;

                    if (!string.IsNullOrWhiteSpace(VoucherCode) && !string.IsNullOrWhiteSpace(Email))
                    {
                        var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == VoucherCode && v.TrangThai == true);

                        if (voucher != null && DateTime.Now <= voucher.NgayHetHan && tongTien >= voucher.GiaTriDonToiThieu)
                        {
                            // 🛡️ BƯỚC 1: Lấy TOÀN BỘ các đơn hàng trong quá khứ của Khách hàng này (Theo ID hoặc Số điện thoại)
                            var danhSachDonHangCu = await _context.DonHangs
                                .Where(d => d.MaKhachHang == khachHang.MaKhachHang || d.SoDienThoai == cleanPhone)
                                .Select(d => d.MaDonHang)
                                .ToListAsync();

                            // 🛡️ BƯỚC 2: Soi xem trong các đơn hàng cũ đó, có đơn nào từng xài mã Voucher này chưa?
                            bool daTungSuDung = await _context.VoucherHistories
                                .AnyAsync(vh => vh.MaVoucher == voucher.MaVoucher && vh.MaDonHang != null && danhSachDonHangCu.Contains(vh.MaDonHang.Value));

                            if (daTungSuDung)
                            {
                                await transaction.RollbackAsync();
                                return Json(new { success = false, message = "Bạn đã sử dụng mã giảm giá này cho một đơn hàng trước đó rồi! Mỗi số điện thoại/tài khoản chỉ được dùng mã ưu đãi này 1 lần duy nhất." });
                            }

                            // ===============================================
                            // NẾU QUA ĐƯỢC BƯỚC KIỂM TRA THÌ MỚI CHO TRỪ TIỀN
                            // ===============================================
                            lichSuVoucher = await _context.VoucherHistories
                                .FirstOrDefaultAsync(vh => vh.Email == Email && vh.MaVoucher == voucher.MaVoucher && vh.MaDonHang == null);

                            if (lichSuVoucher != null)
                            {
                                if (voucher.LoaiGiamGia == "PhanTram")
                                {
                                    soTienGiam = tongTien * (voucher.GiaTriGiam / 100);
                                    if (voucher.GiamToiDa > 0 && soTienGiam > voucher.GiamToiDa) soTienGiam = voucher.GiamToiDa;
                                }
                                else
                                {
                                    soTienGiam = voucher.GiaTriGiam;
                                }

                                if (soTienGiam > tongTien) soTienGiam = tongTien;
                                tongTien -= soTienGiam;
                            }
                        }
                    }

                    // =======================================================
                    // 4. KHỞI TẠO ĐƠN HÀNG VÀ CHI TIẾT
                    // =======================================================
                    var donHang = new DonHang
                    {
                        MaKhachHang = khachHang.MaKhachHang,
                        NgayDatHang = DateTime.Now,
                        TongTien = tongTien, // Số tiền đã được trừ Voucher ở trên
                        TenKhachHang = FullName,
                        SoDienThoai = cleanPhone,
                        DiaChiGiaoHang = $"{Address}, {District}, {Province}",
                        PhuongThucThanhToan = PaymentMethod ?? "Chuyển khoản",
                        TrangThaiDonHang = "Chờ xử lý",
                        TrangThaiThanhToan = (CanLapDat && PaymentMode == "30") ? "Đã cọc" : "Đã thanh toán",
                        Email = Email,
                        GhiChu = GhiChu
                    };
                    _context.DonHangs.Add(donHang);
                    await _context.SaveChangesAsync();

                    // 👇 NẾU CÓ DÙNG VOUCHER -> CẬP NHẬT LỊCH SỬ CHO VOUCHER ĐÓ NGAY
                    if (lichSuVoucher != null)
                    {
                        // Gắn ID đơn hàng vào để đánh dấu là mã này đã bị "xài" rồi
                        lichSuVoucher.MaDonHang = donHang.MaDonHang;
                        _context.VoucherHistories.Update(lichSuVoucher);

                        // Tăng số lượng đã dùng của chiến dịch Voucher đó lên 1
                        var vToUpdate = await _context.Vouchers.FindAsync(lichSuVoucher.MaVoucher);
                        if (vToUpdate != null)
                        {
                            vToUpdate.SoLuongDaDung = vToUpdate.SoLuongDaDung += 1;
                            _context.Vouchers.Update(vToUpdate);
                        }
                    }

                    foreach (var item in itemsToBuy)
                    {
                        _context.ChiTietDonHangs.Add(new ChiTietDonHang
                        {
                            MaDonHang = donHang.MaDonHang,
                            MaSanPham = item.MaSanPham,
                            SoLuong = item.SoLuong,
                            Gia = item.Gia
                        });

                        // Trừ kho
                        var sp = await _context.SanPhams.FindAsync(item.MaSanPham);
                        if (sp != null)
                        {
                            sp.SoLuong -= item.SoLuong;
                            if (sp.SoLuong < 0) sp.SoLuong = 0;
                        }
                    }

                    if (CanLapDat)
                    {
                        _context.YeuCauLapDats.Add(new YeuCauLapDat
                        {
                            MaDonHang = donHang.MaDonHang,
                            DiaChiLapDat = donHang.DiaChiGiaoHang,
                            NgayLap = DateTime.Now,
                            TrangThaiLapDat = "Chưa lắp đặt",
                            GhiChuBaoGia = Note
                        });
                    }

                    // Dọn giỏ hàng & Lưu toàn bộ vào DB
                    _context.ChiTietGioHangs.RemoveRange(itemsToBuy);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // =======================================================
                    // 5. HOÀN TẤT & PHÁT TÍN HIỆU
                    // =======================================================
                    // Lưu Cookie để xác thực luôn cho khách
                    Response.Cookies.Append("VerifiedPhone", cleanPhone, new CookieOptions { Expires = DateTime.Now.AddDays(30), Path = "/" });

                    // Báo cho Admin
                    await _hubContext.Clients.All.SendAsync("NhanDonHangMoi", donHang.MaDonHang, donHang.TenKhachHang, donHang.TongTien);

                    return Json(new
                    {
                        success = true,
                        orderId = donHang.MaDonHang,
                        guestName = FullName,
                        guestPhone = cleanPhone,
                        guestEmail = Email, // 👇 ĐÃ THÊM EMAIL Ở ĐÂY
                        isNewAccount = isNewAccount
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
                }
                finally
                {
                    // Giải phóng ổ khóa để khách được thao tác lại nếu rớt mạng
                    _cache.Remove(orderLockKey);
                }
            }
        }
        // =======================================================
        // TRA CỨU & HỦY ĐƠN HÀNG
        [Route("Customers/OrderHistory")]
        public async Task<IActionResult> OrderHistory(string phone) // <-- Nhận tham số phone từ Popup Javascript chuyển sang
        {
            string searchPhone = "";
            int? searchCustomerId = null;

            // Lớp 1: Kiểm tra xem khách có đang Đăng nhập tài khoản không?
            if (User.Identity.IsAuthenticated)
            {
                var user = await _context.KhachHangs.FirstOrDefaultAsync(k => k.Email == User.Identity.Name || k.SoDienThoai == User.Identity.Name);
                if (user != null)
                {
                    searchCustomerId = user.MaKhachHang;
                    searchPhone = user.SoDienThoai;
                }
            }

            // Lớp 2: Nếu chưa Login, kiểm tra Cookie xác thực (dành cho tài khoản tự động/vãng lai)
            if (searchCustomerId == null)
            {
                // Vẫn giữ nguyên logic đọc Cookie cũ của bạn
                searchPhone = Request.Cookies["VerifiedPhone"];

                // ==============================================================
                // BỔ SUNG: KIỂM TRA MÃ OTP TỪ KHÁCH VÃNG LAI TRUYỀN SANG
                // ==============================================================
                string verifiedSessionPhone = HttpContext.Session.GetString("GuestTrackingPhone");

                // Nếu trên URL có truyền SĐT (?phone=...) VÀ SĐT đó trùng với SĐT vừa nhập OTP thành công
                if (!string.IsNullOrEmpty(phone) && phone == verifiedSessionPhone)
                {
                    searchPhone = phone; // Chấp nhận SĐT này để đi tra cứu

                    // Tự động cấp Cookie luôn để khách F5 không bắt nhập OTP lại nữa
                    Response.Cookies.Append("VerifiedPhone", phone, new CookieOptions { Expires = DateTime.Now.AddDays(7) });
                }
                // ==============================================================
            }

            // Làm sạch SĐT (Giữ nguyên của bạn)
            if (!string.IsNullOrEmpty(searchPhone))
            {
                searchPhone = searchPhone.Replace(" ", "").Replace(".", "").Replace("-", "").Trim();
                if (searchPhone.StartsWith("84")) searchPhone = "0" + searchPhone.Substring(2);
            }

            // 🛑 CHẶN TRUY CẬP TRÁI PHÉP (Giữ nguyên của bạn)
            if (searchCustomerId == null && string.IsNullOrEmpty(searchPhone))
            {
                // Khách chưa có thông tin gì -> Trả về View rỗng để hiện Form nhập SĐT
                return View(null);
            }

            // TRUY VẤN: Lấy đơn hàng khớp ID HOẶC khớp Số điện thoại (Giữ nguyên của bạn)
            var query = _context.DonHangs
                .Include(d => d.ChiTietDonHangs).ThenInclude(c => c.MaSanPhamNavigation)
                .Include(d => d.YeuCauLapDats)
                .AsQueryable();

            if (searchCustomerId.HasValue)
            {
                // Ưu tiên tìm theo ID khách hàng (Bảo mật nhất)
                query = query.Where(d => d.MaKhachHang == searchCustomerId.Value || d.SoDienThoai == searchPhone);
            }
            else
            {
                // Tìm theo Số điện thoại đã xác thực
                query = query.Where(d => d.SoDienThoai == searchPhone);
            }

            var orders = await query.OrderByDescending(d => d.NgayDatHang).ToListAsync();
            ViewBag.Phone = searchPhone;

            return View(orders);
        }

        [Route("Customers/HuyDonHangXacNhan")]
        public async Task<IActionResult> HuyDonHangXacNhan(int maDonHang, string phone)
        {
            var order = await _context.DonHangs
                .Include(d => d.YeuCauLapDats)
                .FirstOrDefaultAsync(d => d.MaDonHang == maDonHang && (d.SoDienThoai == phone || d.MaKhachHangNavigation!.SoDienThoai == phone));

            if (order == null) return NotFound();

            bool choPhepHuy = order.TrangThaiDonHang == "Chờ xử lý"
                           && (order.YeuCauLapDats == null || !order.YeuCauLapDats.Any() || order.YeuCauLapDats.First().TrangThaiLapDat == "Chưa lắp đặt");

            if (choPhepHuy)
            {
                order.TrangThaiDonHang = "Đã hủy";

                if (order.YeuCauLapDats != null && order.YeuCauLapDats.Any())
                {
                    var lapDat = order.YeuCauLapDats.First();
                    lapDat.TrangThaiLapDat = "Bị hủy";
                    _context.Update(lapDat);
                }

                _context.Update(order);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("OrderHistory", new { phone = phone });
        }

        [HttpPost]
        public async Task<IActionResult> CheckVoucher(string code, decimal totalAmount)
        {
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == code && v.TrangThai == true);

            if (voucher == null || DateTime.Now > voucher.NgayHetHan || voucher.SoLuongDaDung >= voucher.SoLuongToiDa)
            {
                return Json(new { success = false, message = "Mã giảm giá không tồn tại hoặc đã hết hạn." });
            }

            if (totalAmount < voucher.GiaTriDonToiThieu)
            {
                return Json(new { success = false, message = $"Mã này chỉ áp dụng cho đơn hàng từ {voucher.GiaTriDonToiThieu:N0}đ" });
            }

            decimal discountAmount = 0;
            if (voucher.LoaiGiamGia == "PhanTram")
            {
                discountAmount = totalAmount * (voucher.GiaTriGiam / 100);
                if (discountAmount > voucher.GiamToiDa) discountAmount = voucher.GiamToiDa;
            }
            else
            {
                discountAmount = voucher.GiaTriGiam;
            }

            return Json(new { success = true, discount = discountAmount, newTotal = totalAmount - discountAmount });
        }

        // Thêm tham số phone vào hàm
        [HttpPost("Cart/ApplyVoucher")]
        public async Task<IActionResult> ApplyVoucher(string code, decimal totalAmount, string email, string phone)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    return Json(new { success = false, message = "Vui lòng nhập Email giao hàng trước khi áp mã!" });

                var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == code);
                if (voucher == null)
                    return Json(new { success = false, message = "Mã giảm giá không tồn tại!" });

                // Làm sạch số điện thoại từ Front-end gửi lên
                string cleanPhone = phone?.Replace(" ", "").Replace(".", "").Replace("-", "").Trim() ?? "";
                if (cleanPhone.StartsWith("84")) cleanPhone = "0" + cleanPhone.Substring(2);

                int maKhachHangHienTai = await GetOrSetGuestCustomerId();

                // 🛡️ LỌC NHƯ BÊN PLACE ORDER
                var danhSachDonHangCu = await _context.DonHangs
                    .Where(d => d.MaKhachHang == maKhachHangHienTai || (!string.IsNullOrEmpty(cleanPhone) && d.SoDienThoai == cleanPhone))
                    .Select(d => d.MaDonHang)
                    .ToListAsync();

                bool daTungSuDung = await _context.VoucherHistories
                    .AnyAsync(vh => vh.MaVoucher == voucher.MaVoucher && vh.MaDonHang != null && danhSachDonHangCu.Contains(vh.MaDonHang.Value));

                if (daTungSuDung)
                    return Json(new { success = false, message = "Tài khoản / Số điện thoại này đã sử dụng mã ưu đãi này rồi!" });

                // Kiểm tra logic hạn sử dụng và sở hữu như bình thường
                var lichSu = await _context.VoucherHistories
                    .FirstOrDefaultAsync(vh => vh.Email == email && vh.MaVoucher == voucher.MaVoucher);

                if (lichSu == null)
                    return Json(new { success = false, message = "Email này không sở hữu mã giảm giá này!" });

                if (lichSu.MaDonHang != null)
                    return Json(new { success = false, message = "Bạn đã sử dụng mã này cho một đơn hàng khác rồi!" });

                if (DateTime.Now > voucher.NgayHetHan || voucher.TrangThai == false)
                {
                    if (voucher.TrangThai == true)
                    {
                        voucher.TrangThai = false;
                        await _context.SaveChangesAsync();
                    }
                    return Json(new { success = false, message = "Rất tiếc, mã giảm giá này đã hết hạn sử dụng!" });
                }

                // Tính tiền giảm...
                decimal discountAmount = 0;
                if (voucher.LoaiGiamGia == "PhanTram")
                {
                    discountAmount = totalAmount * (voucher.GiaTriGiam / 100);
                    if (voucher.GiamToiDa > 0 && discountAmount > voucher.GiamToiDa) discountAmount = voucher.GiamToiDa;
                }
                else if (voucher.LoaiGiamGia == "SoTien")
                {
                    discountAmount = voucher.GiaTriGiam;
                }

                if (discountAmount > totalAmount) discountAmount = totalAmount;

                return Json(new { success = true, message = "Áp dụng mã thành công!", discountAmount = discountAmount, newTotal = totalAmount - discountAmount });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpGet("Cart/GetMyVouchers")]
        public async Task<IActionResult> GetMyVouchers(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Json(new { success = false, message = "Vui lòng nhập email." });

            // Tìm tất cả mã của email này mà CHƯA DÙNG (MaDonHang == null)
            var myVouchers = await _context.VoucherHistories
                .Include(vh => vh.VoucherNavigation)
                .Where(vh => vh.Email == email && vh.MaDonHang == null)
                .Select(vh => new
                {
                    code = vh.VoucherNavigation.Code,
                    moTa = vh.VoucherNavigation.MoTa,
                    ngayHetHan = vh.VoucherNavigation.NgayHetHan,
                    // Kiểm tra xem đã quá hạn chưa
                    isExpired = DateTime.Now > vh.VoucherNavigation.NgayHetHan || vh.VoucherNavigation.TrangThai == false
                })
                .ToListAsync();

            // Chỉ lấy những mã còn "Sống"
            var validVouchers = myVouchers.Where(v => !v.isExpired).ToList();

            if (!validVouchers.Any())
                return Json(new { success = false, message = "Không tìm thấy ưu đãi nào khả dụng cho email này. Hoặc mã của bạn đã hết hạn!" });

            return Json(new { success = true, data = validVouchers });
        }


        // =======================================================
        // TIN TỨC
        // =======================================================
        // GET: /Customers/TinTuc
        public async Task<IActionResult> News(int? danhMucId)
        {
            // 1. Lấy danh sách danh mục để làm Menu lọc
            ViewBag.Categories = await _context.DanhMucBaiViets.ToListAsync();
            ViewBag.CurrentCategory = danhMucId;

            // 2. Lấy các bài viết ĐÃ ĐƯỢC DUYỆT
            var query = _context.BaiViets
                .Include(b => b.MaDanhMucBaiVietNavigation)
                .Where(b => b.IsApproved == true);

            // 3. Lọc theo danh mục nếu người dùng có bấm chọn
            if (danhMucId.HasValue)
            {
                query = query.Where(b => b.MaDanhMucBaiViet == danhMucId.Value);
            }

            var danhSachBaiViet = await query.OrderByDescending(b => b.NgayDang).ToListAsync();
            return View(danhSachBaiViet);
        }

        // GET: /Customers/TinTucDetails/5
        public async Task<IActionResult> NewDetail(int? id)
        {
            if (id == null) return NotFound();

            var baiViet = await _context.BaiViets
                .Include(b => b.MaDanhMucBaiVietNavigation)
                .FirstOrDefaultAsync(m => m.MaBaiViet == id && m.IsApproved == true);

            if (baiViet == null) return NotFound();

            // Lấy thêm tin liên quan (cùng danh mục, trừ bài hiện tại)
            ViewBag.RelatedPosts = await _context.BaiViets
                .Where(b => b.MaDanhMucBaiViet == baiViet.MaDanhMucBaiViet && b.MaBaiViet != id && b.IsApproved == true)
                .Take(3)
                .ToListAsync();

            return View(baiViet);
        }

        // =======================================================
        // TRUNG TÂM HƯỚNG DẪN & HỖ TRỢ (VIDEO GUIDES)
        // =======================================================
        public async Task<IActionResult> Guide()
        {
            ViewData["Title"] = "Hướng dẫn sử dụng - AuraHome";

            // Lấy toàn bộ danh sách Hướng dẫn, kèm theo thông tin Sản phẩm (MaSanPhamNavigation)
            var videoGuides = await _context.HuongDanSuDungs
                .Include(h => h.MaSanPhamNavigation)
                .ToListAsync();

            return View(videoGuides);
        }

        public async Task<IActionResult> GuideDetail(int? id)
        {
            if (id == null) return NotFound();

            var guide = await _context.HuongDanSuDungs
                .Include(h => h.MaSanPhamNavigation)
                // SỬA DÒNG NÀY: Tìm theo MaHuongDan thay vì MaSanPham
                .FirstOrDefaultAsync(b => b.MaHuongDan == id);

            if (guide == null) return NotFound();

            return View(guide);
        }


        // =======================================================
        // XÁC NHẬN MÃ OPT SỐ ĐIỆN THOẠI
        // =======================================================
        [HttpPost]
        [Route("Customers/LogoutAjax")]
        public async Task<IActionResult> LogoutAjax()
        {
            // 1. Đăng xuất tài khoản VIP (Xóa Claims Cookie)
            await HttpContext.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);

            // 2. Xóa sạch Session (Xóa giỏ hàng tạm thời)
            HttpContext.Session.Clear();

            // 3. XÓA CỐT LÕI: Xóa Cookie VerifiedPhone (Thủ phạm gây tự điền SĐT)
            if (Request.Cookies["VerifiedPhone"] != null)
            {
                Response.Cookies.Delete("VerifiedPhone");
            }

            // 4. Nếu bạn có lưu thông tin khách hàng vào Cookie khác, hãy xóa nốt
            Response.Cookies.Delete(".AspNetCore.Session");

            return Ok(new { success = true });
        }

        // =======================================================
        // TRA CỨU ĐƠN HÀNG
        // =======================================================
        // =========================================================
        // API MÃ GIẢ (MOCK) ĐỂ TRA CỨU ĐƠN HÀNG
        // =========================================================

        // =========================================================
        // API OTP RANDOM (CÓ LƯU SESSION ĐỂ XÁC THỰC)
        // =========================================================

        [HttpGet]
        [Route("Customers/SendTrackingOTP")]
        public IActionResult SendTrackingOTP(string phone)
        {
            if (string.IsNullOrEmpty(phone))
            {
                return Json(new { success = false, message = "Vui lòng nhập số điện thoại!" });
            }

            // Tạo mã ngẫu nhiên 6 số
            string randomOtp = new Random().Next(100000, 999999).ToString();

            // Lưu vào Session để lát nữa khách nhập vào thì lấy ra so sánh
            HttpContext.Session.SetString($"GuestOTP_{phone}", randomOtp);

            return Json(new
            {
                success = true,
                otp = randomOtp // Gửi kèm ra Frontend để Demo hiển thị lên màn hình
            });
        }

        [HttpGet]
        [Route("Customers/VerifyTrackingOTP")]
        public IActionResult VerifyTrackingOTP(string phone, string otp)
        {
            // Lấy mã OTP từ Session ra
            string savedOtp = HttpContext.Session.GetString($"GuestOTP_{phone}");

            if (string.IsNullOrEmpty(savedOtp))
            {
                return Json(new { success = false, message = "Mã OTP đã hết hạn hoặc chưa được yêu cầu!" });
            }

            // So sánh mã khách nhập với mã đã lưu
            if (savedOtp == otp.Trim())
            {
                // Đúng mã -> Xóa Session OTP đi cho bảo mật
                HttpContext.Session.Remove($"GuestOTP_{phone}");

                // Ghi nhận khách này đã xác thực thành công
                HttpContext.Session.SetString("GuestTrackingPhone", phone);

                return Json(new { success = true, message = "Xác thực thành công!" });
            }

            return Json(new { success = false, message = "Mã OTP không đúng! Vui lòng kiểm tra lại." });
        }
    }
}