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

namespace AISmartHome.Controllers
{
    public class CustomersController : Controller
    {
        private readonly AISmartHomeDbContext _context;
        private readonly IMemoryCache _cache;

        public CustomersController(AISmartHomeDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }
        private async Task<int> GetOrSetGuestCustomerId()
        {
            // 1. ƯU TIÊN ĐỌC TỪ COOKIE (Vì nó sống 30 ngày)
            if (Request.Cookies.TryGetValue("GuestCustomerId", out string cookieId) && int.TryParse(cookieId, out int parsedCookieId))
            {
                // Cập nhật lại Session cho đồng bộ với Cookie
                HttpContext.Session.SetString("GuestCustomerId", parsedCookieId.ToString());
                return parsedCookieId;
            }

            // 2. NẾU KHÔNG CÓ COOKIE, THỬ ĐỌC TỪ SESSION
            string guestIdStr = HttpContext.Session.GetString("GuestCustomerId");
            if (!string.IsNullOrEmpty(guestIdStr) && int.TryParse(guestIdStr, out int parsedSessionId))
            {
                return parsedSessionId;
            }

            // 3. KHÁCH MỚI TINH -> TẠO KHÁCH VÃNG LAI
            var newGuest = new KhachHang
            {
                TenKhachHang = "Khách vãng lai",
                SoDienThoai = "",
                DiaChi = ""
            };

            _context.KhachHangs.Add(newGuest);
            await _context.SaveChangesAsync();

            // Lưu Cookie
            CookieOptions options = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(30),
                HttpOnly = true,
                IsEssential = true,
                Path = "/"
            };
            Response.Cookies.Append("GuestCustomerId", newGuest.MaKhachHang.ToString(), options);

            // Lưu Session đồng bộ
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
            int maKhachHangHienTai = await GetOrSetGuestCustomerId(); // Đã bảo mật
            var cart = await GetOrCreateCartAsync(maKhachHangHienTai);
            var sp = await _context.SanPhams.FindAsync(id);

            if (sp == null) return Json(new { success = false, message = "Không tìm thấy sản phẩm" });

            var chiTiet = cart.ChiTietGioHangs.FirstOrDefault(c => c.MaSanPham == id);
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
        [Route("Customers/RestoreCartAjax")]
        public async Task<IActionResult> RestoreCartAjax(string phone, string otp, string name)
        {
            try
            {
                string formattedPhone = phone.StartsWith("0") ? "84" + phone.Substring(1) : phone;

                if (!_cache.TryGetValue($"OTP_{formattedPhone}", out string savedOtp) || savedOtp != otp)
                {
                    return Json(new { success = false, message = "Mã OTP không chính xác!" });
                }
                _cache.Remove($"OTP_{formattedPhone}");

                HttpContext.Session.SetString("VerifiedPhone", phone);
                HttpContext.Session.SetString("CustomerPhone", phone);

                var khachCu = await _context.KhachHangs
                     .OrderByDescending(k => k.MaKhachHang)
                     .FirstOrDefaultAsync(k => k.SoDienThoai == phone);

                int currentGuestId = await GetOrSetGuestCustomerId();

                if (khachCu != null)
                {
                    khachCu.TenKhachHang = name;
                    _context.Update(khachCu);

                    // 1. ÉP COOKIE DÙNG ID KHÁCH CŨ
                    CookieOptions options = new CookieOptions { Expires = DateTime.Now.AddDays(30), HttpOnly = true, IsEssential = true, Path = "/" };
                    Response.Cookies.Append("GuestCustomerId", khachCu.MaKhachHang.ToString(), options);

                    // 2. ÉP SESSION DÙNG ID KHÁCH CŨ (ĐÂY LÀ DÒNG QUAN TRỌNG NHẤT VỪA THÊM)
                    HttpContext.Session.SetString("GuestCustomerId", khachCu.MaKhachHang.ToString());

                    // --- Logic Gộp Giỏ Hàng ---
                    var gioHangHienTai = await _context.GioHangs.Include(g => g.ChiTietGioHangs).FirstOrDefaultAsync(g => g.MaKhachHang == currentGuestId);
                    var gioHangCu = await GetOrCreateCartAsync(khachCu.MaKhachHang);

                    if (gioHangHienTai != null && gioHangHienTai.ChiTietGioHangs.Any() && currentGuestId != khachCu.MaKhachHang)
                    {
                        foreach (var item in gioHangHienTai.ChiTietGioHangs)
                        {
                            var tonTai = gioHangCu.ChiTietGioHangs.FirstOrDefault(c => c.MaSanPham == item.MaSanPham);
                            if (tonTai != null)
                            {
                                tonTai.SoLuong += item.SoLuong;
                                _context.ChiTietGioHangs.Update(tonTai); // Đảm bảo lưu thay đổi
                            }
                            else
                            {
                                item.MaGioHang = gioHangCu.MaGioHang;
                                _context.ChiTietGioHangs.Update(item);
                            }
                        }
                        gioHangCu.TongTien = gioHangCu.ChiTietGioHangs.Sum(c => c.Gia * c.SoLuong);
                        _context.GioHangs.Update(gioHangCu);

                        // Xóa giỏ tạm sau khi gộp xong
                        _context.ChiTietGioHangs.RemoveRange(gioHangHienTai.ChiTietGioHangs.Where(c => c.MaGioHang == gioHangHienTai.MaGioHang));
                        _context.GioHangs.Remove(gioHangHienTai);
                    }
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Khôi phục giỏ hàng thành công!" });
                }
                else
                {
                    // SỐ ĐIỆN THOẠI MỚI TINH
                    var currentGuest = await _context.KhachHangs.FindAsync(currentGuestId);
                    if (currentGuest != null)
                    {
                        if (!string.IsNullOrEmpty(currentGuest.SoDienThoai) && currentGuest.SoDienThoai != phone)
                        {
                            var newCustomer = new KhachHang { TenKhachHang = name, SoDienThoai = phone };
                            _context.KhachHangs.Add(newCustomer);
                            await _context.SaveChangesAsync();

                            CookieOptions opt = new CookieOptions { Expires = DateTime.Now.AddDays(30), HttpOnly = true, IsEssential = true, Path = "/" };
                            Response.Cookies.Append("GuestCustomerId", newCustomer.MaKhachHang.ToString(), opt);

                            // ĐỒNG BỘ SESSION (THÊM DÒNG NÀY)
                            HttpContext.Session.SetString("GuestCustomerId", newCustomer.MaKhachHang.ToString());
                        }
                        else
                        {
                            currentGuest.SoDienThoai = phone;
                            currentGuest.TenKhachHang = name;
                            _context.Update(currentGuest);
                            await _context.SaveChangesAsync();
                        }
                    }
                    return Json(new { success = true, message = "Xác thực thành công!" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
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
                    .Where(c => selectedProducts.Contains(c.MaSanPham ?? 0 ))
                    .ToList();

                // Nếu chọn xong mà mảng rỗng (khách hack F12), đẩy về giỏ hàng
                if (!gioHang.ChiTietGioHangs.Any()) return RedirectToAction("Cart");
            }
            else
            {
                // Bắt lỗi nếu khách cố tình gõ link trực tiếp mà không chọn gì
                return RedirectToAction("Cart");
            }

            return View(gioHang);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(
            string FullName, string Phone, string Email,
            string Province, string District, string Address,
            string Note, string PaymentMethod, string PaymentMode,
            string VoucherCode,
            string OtpCode,
            List<int> SelectedProducts,
            bool CanLapDat = false)
        {
            try
            {
                // =======================================================
                // 1. LÀM SẠCH SỐ ĐIỆN THOẠI (Xóa khoảng trắng, quy về đầu 0)
                // =======================================================
                string cleanPhone = Phone?.Replace(" ", "").Replace(".", "").Replace("-", "").Trim() ?? "";
                if (cleanPhone.StartsWith("84")) cleanPhone = "0" + cleanPhone.Substring(2);

                // Lấy SĐT từ Session (Tìm trong cả 2 túi: VerifiedPhone và CustomerPhone)
                string sessionPhone = HttpContext.Session.GetString("VerifiedPhone") ?? HttpContext.Session.GetString("CustomerPhone") ?? "";
                string cleanSessionPhone = sessionPhone.Replace(" ", "").Replace(".", "").Replace("-", "").Trim();
                if (cleanSessionPhone.StartsWith("84")) cleanSessionPhone = "0" + cleanSessionPhone.Substring(2);

                // =======================================================
                // 2. KIỂM TRA QUYỀN MIỄN TRỪ OTP
                // =======================================================
                // Nếu SĐT nhập vào TRÙNG với SĐT đang đăng nhập -> Cấp quyền Miễn OTP
                bool isAlreadyVerified = (!string.IsNullOrEmpty(cleanSessionPhone) && cleanSessionPhone == cleanPhone);

                if (!isAlreadyVerified)
                {
                    if (string.IsNullOrEmpty(cleanPhone))
                        return Json(new { success = false, message = "Vui lòng nhập số điện thoại!" });

                    // Nếu SĐT chưa xác thực, tạo thêm bản copy đầu "84" để quét tìm Cache
                    string phoneFormat84 = cleanPhone.Length > 1 ? "84" + cleanPhone.Substring(1) : cleanPhone;

                    // Tìm OTP trong Cache (Bao quét cả dạng 09... và 849...)
                    if (string.IsNullOrEmpty(OtpCode) ||
                        (!_cache.TryGetValue($"OTP_{cleanPhone}", out string savedOtp) &&
                         !_cache.TryGetValue($"OTP_{phoneFormat84}", out savedOtp)) ||
                        savedOtp != OtpCode)
                    {
                        return Json(new { success = false, message = "Mã OTP không chính xác hoặc yêu cầu đăng nhập!" });
                    }

                    // Nhập đúng -> Xóa Cache, cấp quyền miễn trừ cho các lần sau
                    _cache.Remove($"OTP_{cleanPhone}");
                    _cache.Remove($"OTP_{phoneFormat84}");
                    HttpContext.Session.SetString("VerifiedPhone", cleanPhone);
                }

                // =======================================================
                // Lấy giỏ hàng, lọc sản phẩm và tạo đơn hàng (Giữ nguyên code bên dưới của bạn)
                // =======================================================
                int maKhachHangHienTai = await GetOrSetGuestCustomerId();
                // ... var gioHang = await _context.GioHangs...

                // =======================================================
                // 1. MỚI THÊM: ĐỒNG BỘ THÔNG TIN KHÁCH HÀNG XUỐNG ADMIN
                // =======================================================
                var khachHang = await _context.KhachHangs.FindAsync(maKhachHangHienTai);
                if (khachHang != null)
                {
                    khachHang.TenKhachHang = FullName;
                    khachHang.SoDienThoai = Phone;
                    khachHang.Email = Email; // Lưu lại Email khách vừa nhập
                    khachHang.DiaChi = $"{Address}, {District}, {Province}";
                    _context.KhachHangs.Update(khachHang);
                    // Thông tin này sẽ lập tức hiện lên trang Quản lý Khách Hàng của Admin
                }

                var gioHang = await _context.GioHangs
                    .Include(g => g.ChiTietGioHangs)
                    .FirstOrDefaultAsync(g => g.MaKhachHang == maKhachHangHienTai);

                // BƯỚC LỌC: CHỈ LẤY CÁC MÓN KHÁCH ĐÃ CHỌN TỪ BƯỚC TRƯỚC
                var itemsToBuy = gioHang.ChiTietGioHangs
                    .Where(c => SelectedProducts.Contains(c.MaSanPham ?? 0))
                    .ToList();

                if (gioHang == null || !itemsToBuy.Any())
                {
                    return Json(new { success = false, message = "Không có sản phẩm nào để thanh toán!" });
                }

                // TÍNH TỔNG TIỀN DỰA TRÊN CÁC MÓN ĐƯỢC CHỌN THÔI
                decimal tongTien = itemsToBuy.Sum(c => (c.SoLuong ?? 0) * (c.Gia ?? 0));
                // =======================================================
                // 2. MỚI THÊM: XỬ LÝ VOUCHER (Tính tiền & Khóa mã)
                // =======================================================
                VoucherHistory lichSuVoucherDung = null;

                if (!string.IsNullOrEmpty(VoucherCode) && !string.IsNullOrEmpty(Email))
                {
                    var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == VoucherCode && v.TrangThai == true);
                    if (voucher != null)
                    {
                        // Kiểm tra chéo: Email này có thực sự sở hữu mã này không? Mã đã bị dùng chưa?
                        lichSuVoucherDung = await _context.VoucherHistories.FirstOrDefaultAsync(vh => vh.Email == Email && vh.MaVoucher == voucher.MaVoucher && vh.MaDonHang == null);

                        if (lichSuVoucherDung != null)
                        {
                            // Tính toán số tiền được giảm
                            decimal soTienGiam = 0;
                            if (voucher.LoaiGiamGia == "PhanTram")
                            {
                                soTienGiam = tongTien * ((decimal)voucher.GiaTriGiam / 100m);
                                if (voucher.GiamToiDa > 0 && soTienGiam > voucher.GiamToiDa)
                                {
                                    soTienGiam = (decimal)voucher.GiamToiDa;
                                }
                            }
                            else
                            {
                                soTienGiam = (decimal)voucher.GiaTriGiam;
                            }

                            // Không cho giảm âm tiền
                            if (soTienGiam > tongTien) soTienGiam = tongTien;

                            // Trừ tiền vào tổng hóa đơn
                            tongTien -= soTienGiam;

                            // Tăng số lượt đã dùng của Voucher lên 1
                            voucher.SoLuongDaDung += 1;
                            _context.Vouchers.Update(voucher);
                        }
                    }
                }

                // =======================================================
                // 3. TẠO ĐƠN HÀNG (Giữ nguyên logic của bạn)
                // =======================================================
                var donHang = new DonHang
                {
                    MaKhachHang = maKhachHangHienTai,
                    NgayDatHang = DateTime.Now,
                    TongTien = tongTien, // Lúc này tongTien đã được trừ Voucher (nếu có)
                    TenKhachHang = FullName,
                    SoDienThoai = Phone,
                    DiaChiGiaoHang = $"{Address}, {District}, {Province}",
                    PhuongThucThanhToan = PaymentMethod ?? "Chuyển khoản",
                    TrangThaiDonHang = "Chờ xử lý"
                };

                if (CanLapDat == true && PaymentMode == "30")
                {
                    donHang.TrangThaiThanhToan = "Đã cọc";
                }
                else
                {
                    donHang.TrangThaiThanhToan = "Đã thanh toán";
                }

                _context.DonHangs.Add(donHang);
                await _context.SaveChangesAsync(); // Cần Save để sinh ra MaDonHang

                // =======================================================
                // 4. MỚI THÊM: GẮN MÃ ĐƠN HÀNG VÀO LỊCH SỬ VOUCHER
                // =======================================================
                if (lichSuVoucherDung != null)
                {
                    lichSuVoucherDung.MaDonHang = donHang.MaDonHang; // Đánh dấu mã đã dùng cho đơn này
                    lichSuVoucherDung.NgaySuDung = DateTime.Now;
                    _context.VoucherHistories.Update(lichSuVoucherDung);
                    await _context.SaveChangesAsync();
                }

                // --- XỬ LÝ YÊU CẦU LẮP ĐẶT VÀ CHI TIẾT ĐƠN HÀNG (Giữ nguyên) ---
                if (CanLapDat == true)
                {
                    var yeuCauMoi = new YeuCauLapDat
                    {
                        MaDonHang = donHang.MaDonHang,
                        DiaChiLapDat = donHang.DiaChiGiaoHang,
                        NgayLap = DateTime.Now,
                        TrangThaiLapDat = "Chưa lắp đặt",
                        PhiLapDat = null,
                        GhiChuBaoGia = Note,
                        MaNhanVien = null
                    };
                    _context.YeuCauLapDats.Add(yeuCauMoi);
                }

                foreach (var item in itemsToBuy)
                {
                    // 1. Tạo chi tiết đơn hàng
                    _context.ChiTietDonHangs.Add(new ChiTietDonHang
                    {
                        MaDonHang = donHang.MaDonHang,
                        MaSanPham = item.MaSanPham,
                        SoLuong = item.SoLuong,
                        Gia = item.Gia
                    });

                    // =======================================================
                    // 2. MỚI THÊM: TRỪ SỐ LƯỢNG TỒN KHO CỦA SẢN PHẨM
                    // =======================================================
                    var sanPhamTrongKho = await _context.SanPhams.FindAsync(item.MaSanPham);
                    if (sanPhamTrongKho != null)
                    {
                        // Trừ số lượng khách mua
                        sanPhamTrongKho.SoLuong = sanPhamTrongKho.SoLuong - item.SoLuong;

                        // Đảm bảo kho không bao giờ bị âm (rớt xuống dưới 0)
                        if (sanPhamTrongKho.SoLuong < 0) sanPhamTrongKho.SoLuong = 0;

                        _context.SanPhams.Update(sanPhamTrongKho);
                    }
                }

                // CHỈ XÓA NHỮNG MÓN ĐÃ MUA KHỎI GIỎ HÀNG (Món nào khách chưa mua vẫn ở lại)
                _context.ChiTietGioHangs.RemoveRange(itemsToBuy);

                // Cập nhật lại tổng tiền của giỏ hàng cho những món còn sót lại
                gioHang.TongTien = gioHang.ChiTietGioHangs.Except(itemsToBuy).Sum(c => (c.SoLuong ?? 0) * (c.Gia ?? 0));
                _context.GioHangs.Update(gioHang);

                await _context.SaveChangesAsync();

                HttpContext.Session.SetString("CustomerPhone", Phone);

                return Json(new { success = true, orderId = donHang.MaDonHang });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // =======================================================
        // TRA CỨU & HỦY ĐƠN HÀNG
        // =======================================================
        [Route("Customers/OrderHistory")]
        public async Task<IActionResult> OrderHistory(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return View(null);

            var orders = await _context.DonHangs
                .Include(d => d.ChiTietDonHangs)
                    .ThenInclude(c => c.MaSanPhamNavigation)
                .Include(d => d.YeuCauLapDats)
                .Where(d => d.SoDienThoai == phone || d.MaKhachHangNavigation!.SoDienThoai == phone)
                .OrderByDescending(d => d.NgayDatHang)
                .ToListAsync();

            if (!orders.Any())
            {
                ViewBag.Error = $"Không tìm thấy đơn hàng nào với số điện thoại {phone}";
                return View(null);
            }

            ViewBag.Phone = phone;
            HttpContext.Session.SetString("CustomerPhone", phone);

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

        [HttpPost("Cart/ApplyVoucher")]
        public async Task<IActionResult> ApplyVoucher(string code, decimal totalAmount, string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    return Json(new { success = false, message = "Vui lòng nhập Email giao hàng trước khi áp mã!" });

                var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == code);
                if (voucher == null)
                    return Json(new { success = false, message = "Mã giảm giá không tồn tại!" });

                var lichSu = await _context.VoucherHistories
                    .FirstOrDefaultAsync(vh => vh.Email == email && vh.MaVoucher == voucher.MaVoucher);

                if (lichSu == null)
                    return Json(new { success = false, message = "Email này không sở hữu mã giảm giá này!" });

                if (lichSu.MaDonHang != null)
                    return Json(new { success = false, message = "Bạn đã sử dụng mã này cho một đơn hàng khác rồi!" });

                // LOGIC MỚI: TỰ ĐỘNG HỦY MÃ NẾU HẾT HẠN
                if (DateTime.Now > voucher.NgayHetHan || voucher.TrangThai == false)
                {
                    // Nếu phát hiện quá hạn mà trạng thái vẫn là true, thì tắt nó đi
                    if (voucher.TrangThai == true)
                    {
                        voucher.TrangThai = false;
                        await _context.SaveChangesAsync();
                    }
                    return Json(new { success = false, message = "Rất tiếc, mã giảm giá này đã hết hạn sử dụng và đã bị hệ thống hủy bỏ!" });
                }

                // Tính toán tiền giảm
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
        public IActionResult LogoutAjax()
        {
            // BẮT BUỘC PHẢI CÓ Path = "/" THÌ TRÌNH DUYỆT MỚI CHỊU XÓA COOKIE
            Response.Cookies.Delete("GuestCustomerId", new CookieOptions { Path = "/" });

            // Xóa sạch toàn bộ trí nhớ của phiên làm việc hiện tại
            HttpContext.Session.Clear();

            return Json(new { success = true });
        }
    }
}