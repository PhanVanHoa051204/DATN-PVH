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

        public async Task<IActionResult> Index(string searchString, string paymentType = "All")
        {
            ViewData["CurrentSearch"] = searchString;
            ViewBag.CurrentPaymentType = paymentType;

            var query = _context.Orders.Include(o => o.User).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                string keyword = searchString.Trim().ToLower();
                query = query.Where(o =>
                    o.OrderId.ToString().Contains(keyword) ||
                    (o.User != null && o.User.FullName.ToLower().Contains(keyword))
                );
            }

            if (paymentType == "COD") query = query.Where(o => o.PaymentMethod == "COD");
            else if (paymentType == "VNPAY") query = query.Where(o => o.PaymentMethod == "BANK");

            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();
            return View(orders);
        }

        // 2. CẬP NHẬT TRẠNG THÁI (Danh sách)
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int orderId, string newStatus)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order != null)
            {
                // KIỂM TRA CẢ 2 TRƯỜNG HỢP TIẾNG ANH/VIỆT CHO CHẮC
                if ((newStatus == "Đã hủy" || newStatus == "Cancelled" || newStatus == "Canceled") && order.OrderStatus != "Đã hủy")
                {
                    foreach (var item in order.OrderItems)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity = (product.StockQuantity ?? 0) + item.Quantity;
                            _context.Products.Update(product);
                        }
                    }
                }

                order.OrderStatus = newStatus;
                order.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveStatusChange", orderId, newStatus);
                TempData["SuccessMsg"] = $"Đã chuyển trạng thái đơn hàng #{orderId} sang [{newStatus}].";
            }
            return RedirectToAction("Index");
        }

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

        // 4. XỬ LÝ YÊU CẦU HỦY ĐƠN (Khách gửi yêu cầu)
        [HttpPost]
        public async Task<IActionResult> ProcessCancelRequest(int orderId, string actionType, string adminNote)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null) return NotFound();

            if (order.OrderStatus == "PendingCancel")
            {
                if (!string.IsNullOrWhiteSpace(adminNote))
                    order.CustomerNote += "\n[Phản hồi Admin]: " + adminNote;

                if (actionType == "Approve")
                {
                    // ĐỒNG BỘ: Dùng "Đã hủy" cho giống các hàm khác
                    order.OrderStatus = "Đã hủy";

                    foreach (var item in order.OrderItems)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity = (product.StockQuantity ?? 0) + item.Quantity;
                            _context.Products.Update(product);
                        }
                    }
                }
                else if (actionType == "Reject")
                {
                    order.OrderStatus = "Processing";
                }

                order.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveStatusChange", orderId, actionType);
                TempData["SuccessMsg"] = "Đã xử lý yêu cầu hủy đơn thành công!";
            }
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Product).ThenInclude(p => p.ProductImages)
                .Include(o => o.Voucher)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null) return NotFound();
            return View(order);
        }

        // 6. CẬP NHẬT TỪ CHI TIẾT
        [HttpPost]
        public async Task<IActionResult> EditOrder(int orderId, string OrderStatus, string adminNote)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order != null)
            {
                // KIỂM TRA ĐIỀU KIỆN HỦY (Chấp nhận cả Canceled/Đã hủy)
                if ((OrderStatus == "Đã hủy" || OrderStatus == "Canceled" || OrderStatus == "Cancelled") && order.OrderStatus != "Đã hủy")
                {
                    foreach (var item in order.OrderItems)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity = (product.StockQuantity ?? 0) + item.Quantity;
                            _context.Products.Update(product);
                        }
                    }
                }

                order.OrderStatus = OrderStatus;
                if (!string.IsNullOrWhiteSpace(adminNote))
                    order.CustomerNote += "\n\n[Phản hồi Admin]: " + adminNote;

                order.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveStatusChange", orderId, OrderStatus);
                TempData["SuccessMsg"] = "Đã cập nhật trạng thái đơn hàng!";
            }
            return RedirectToAction("Details", new { id = orderId });
        }
    }
}