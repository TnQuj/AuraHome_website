using AISmartHome.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore; // Thêm dòng này để dùng ToListAsync()
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AISmartHome.Services // Bao bọc trong namespace để quản lý tốt hơn
{
    public class CleanupGuestCartService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public CleanupGuestCartService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AISmartHomeDbContext>();

                    try
                    {
                        // 👇 Cột mốc: Đã 30 ngày trôi qua kể từ LẦN CUỐI CÙNG khách truy cập
                        var thoiGianHetHan = DateTime.Now.AddDays(-30);

                        // Truy lùng "Khách vãng lai" đã không truy cập trong 30 ngày qua
                        var khachRac = await context.KhachHangs
                            .Where(k => k.TenKhachHang == "Khách truy cập"
                                     && k.ThoiGianTruyCap != null
                                     && k.ThoiGianTruyCap < thoiGianHetHan)
                            .ToListAsync(stoppingToken);

                        if (khachRac.Any())
                        {
                            // Xóa sổ (SQL Server sẽ tự động xóa luôn Giỏ Hàng của khách này nếu có Cascade Delete)
                            context.KhachHangs.RemoveRange(khachRac);
                            await context.SaveChangesAsync(stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        // TODO: Thêm ILogger để ghi log lỗi thực tế thay vì bỏ trống
                        Console.WriteLine($"Lỗi khi dọn dẹp giỏ hàng: {ex.Message}");
                    }
                }

                // Ngủ đông 24 tiếng rồi mai dậy quét tiếp
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}