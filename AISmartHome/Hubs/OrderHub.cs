using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AISmartHome.Hubs
{
    // Đây là trạm trung chuyển dữ liệu giữa Khách và Admin
    [AllowAnonymous]
    public class OrderHub : Hub
    {
        // Bạn có thể để trống, vì chúng ta chỉ cần dùng nó để gửi tín hiệu 1 chiều từ Server xuống Client
    }
}