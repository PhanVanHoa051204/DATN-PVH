using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebNoiThatHoaHome.Models;

namespace WebNoiThatHoaHome.Areas.Admin.Controllers
{
    [Area("Admin")] 
    public class OrderController : Controller
    {
        private readonly HoaHomeDbContext _context;

        public OrderController(HoaHomeDbContext context)
        {
            _context = context;
        }
        // Hàm hiển thị danh sách tất cả đơn hàng
        public async Task<IActionResult> Index()
        {
            var orders = await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }
        // Hàm xem chi tiết Đơn hàng
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int orderId, string newStatus)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.OrderStatus = newStatus;
                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = $"Cập nhật trạng thái đơn #{orderId} thành công!";
            }
            return RedirectToAction("Index");
        }
        // Cập nhật trạng thái Thanh toán (Payment Status)
        [HttpPost]
        public async Task<IActionResult> UpdatePaymentStatus(int orderId, string newPaymentStatus, string returnUrl = null)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.PaymentStatus = newPaymentStatus;
                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = $"Cập nhật thanh toán đơn #{orderId} thành công!";
            }

            // Nếu form gửi kèm đường dẫn trả về (VD: gửi từ trang Index) thì quay về đó
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }
            // Nếu không có thì mặc định quay về trang Chi tiết
            return RedirectToAction("Details", new { id = orderId });
        }
    }
}