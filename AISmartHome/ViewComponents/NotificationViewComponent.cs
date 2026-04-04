using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AISmartHome.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AISmartHome.ViewComponents
{
    public class NotificationViewComponent : ViewComponent
    {
        private readonly AISmartHomeDbContext _context;

        public NotificationViewComponent(AISmartHomeDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // 1. Lấy 5 Đơn hàng MỚI NHẤT (Đang chờ xử lý)
            var newOrders = await _context.DonHangs
                .Include(d => d.MaKhachHangNavigation)
                .Where(d => d.TrangThaiDonHang == "Chờ xử lý")
                .OrderByDescending(d => d.NgayDatHang)
                .Take(5)
                .Select(d => new NotificationItem
                {
                    Id = d.MaDonHang,
                    Type = "Order",
                    Title = $"Đơn hàng mới! #{d.MaDonHang}",
                    Description = $"Khách hàng {d.TenKhachHang ?? "ẩn danh"} vừa đặt mua.",
                    Time = d.NgayDatHang ?? DateTime.Now,
                    Link = $"/DonHangs/Details/{d.MaDonHang}"
                })
                .ToListAsync();

            // 2. Lấy 5 Yêu cầu lắp đặt MỚI NHẤT (Chờ báo giá)
            var newRequests = await _context.YeuCauLapDats
                .Where(y => y.TrangThaiLapDat == "Chờ báo giá")
                .OrderByDescending(y => y.NgayLap)
                .Take(5)
                .Select(y => new NotificationItem
                {
                    Id = y.MaYeuCauLapDat,
                    Type = "Request",
                    Title = $"Yêu cầu lắp đặt #{y.MaYeuCauLapDat}",
                    Description = "Cần khảo sát và báo giá thi công.",
                    Time = y.NgayLap ?? DateTime.Now,
                    Link = $"/YeuCauLapDats/Edit/{y.MaYeuCauLapDat}"
                })
                .ToListAsync();

            // 3. Gộp lại và sắp xếp theo thời gian mới nhất
            var allNotifications = newOrders.Concat(newRequests)
                .OrderByDescending(n => n.Time)
                .Take(10) // Lấy tối đa 10 thông báo trên chuông
                .ToList();

            return View(allNotifications);
        }
    }

    // Class phụ để chứa dữ liệu
    public class NotificationItem
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Time { get; set; }
        public string Link { get; set; }

        // Hàm tính toán "Vài giây trước", "5 phút trước"
        public string GetTimeAgo()
        {
            TimeSpan timeSince = DateTime.Now.Subtract(Time);
            if (timeSince.TotalMinutes < 1) return "Vừa xong";
            if (timeSince.TotalMinutes < 60) return $"{(int)timeSince.TotalMinutes} phút trước";
            if (timeSince.TotalHours < 24) return $"{(int)timeSince.TotalHours} giờ trước";
            return $"{(int)timeSince.TotalDays} ngày trước";
        }
    }
}