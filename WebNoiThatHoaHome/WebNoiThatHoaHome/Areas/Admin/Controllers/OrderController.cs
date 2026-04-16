using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebNoiThatHoaHome.Models;
using Microsoft.AspNetCore.SignalR; 
using WebNoiThatHoaHome.Hubs;       

namespace WebNoiThatHoaHome.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class OrderController : Controller
    {
        private readonly HoaHomeDbContext _context;
        private readonly IHubContext<OrderHub> _hubContext;

        public OrderController(HoaHomeDbContext context, IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // 1. DANH SÁCH ĐƠN HÀNG + TÌM KIẾM THEO USER
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentSearch"] = searchString;

            var query = _context.Orders
                .Include(o => o.User) // Quan trọng: Lấy thông tin khách hàng
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                string keyword = searchString.Trim().ToLower();
                // Tìm theo Mã đơn hoặc Tên khách hàng từ bảng User
                query = query.Where(o =>
                    o.OrderId.ToString().Contains(keyword) ||
                    (o.User != null && o.User.FullName.ToLower().Contains(keyword))
                );
            }

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        // 2. CẬP NHẬT TRẠNG THÁI ĐƠN HÀNG (Dành cho Admin chỉnh sửa nhanh)
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int orderId, string newStatus)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.OrderStatus = newStatus;
                order.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveStatusChange", orderId, newStatus);
                TempData["SuccessMsg"] = $"Đã chuyển trạng thái đơn hàng #{orderId} sang [{newStatus}].";
            }
            return RedirectToAction("Index");
        }

        // 3. CẬP NHẬT THANH TOÁN
        [HttpPost]
        public async Task<IActionResult> UpdatePaymentStatus(int orderId, string newPaymentStatus)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.PaymentStatus = newPaymentStatus;
                order.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveStatusChange", orderId, newPaymentStatus);
                TempData["SuccessMsg"] = $"Đã cập nhật thanh toán đơn #{orderId} thành [{newPaymentStatus}].";
            }
            return RedirectToAction("Index");
        }

        // 4. XỬ LÝ YÊU CẦU HỦY ĐƠN (Approve/Reject)
        [HttpPost]
        public async Task<IActionResult> ProcessCancelRequest(int orderId, string actionType, string adminNote)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound();

            if (order.OrderStatus == "PendingCancel")
            {
                if (!string.IsNullOrWhiteSpace(adminNote))
                    order.CustomerNote += "\n[Phản hồi Admin]: " + adminNote;

                if (actionType == "Approve") order.OrderStatus = "Cancelled";
                else if (actionType == "Reject") order.OrderStatus = "Processing";
                
                order.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveStatusChange", orderId, actionType);
                TempData["SuccessMsg"] = "Đã xử lý yêu cầu hủy đơn thành công!";
            }
            return RedirectToAction("Index");
        }

        // 5. CHI TIẾT
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Product).ThenInclude(p => p.ProductImages)

                .FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return NotFound();
            return View(order);
        }
        [HttpPost]
        public async Task<IActionResult> EditOrder(int orderId, string OrderStatus, string adminNote) 
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.OrderStatus = OrderStatus;

                if (!string.IsNullOrWhiteSpace(adminNote))
                {
                    order.CustomerNote += "\n\n[Phản hồi Admin]: " + adminNote; 
                }

                order.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveStatusChange", orderId, OrderStatus);
                TempData["SuccessMsg"] = "Đã cập nhật trạng thái đơn hàng!";
            }
            return RedirectToAction("Details", new { id = orderId });
        }
    }
}