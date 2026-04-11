using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebNoiThatHoaHome.Models;
using WebNoiThatHoaHome.Services; 

namespace WebNoiThatHoaHome.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly HoaHomeDbContext _context;
        private readonly IConfiguration _configuration;

        public CheckoutController(HoaHomeDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");
            int userId = int.Parse(userIdString);

            var user = await _context.Users
                .Include(u => u.Cart)
                    .ThenInclude(c => c.CartItems)
                        .ThenInclude(ci => ci.Product)
                            .ThenInclude(p => p.ProductImages)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user?.Cart == null || !user.Cart.CartItems.Any())
            {
                TempData["ErrorMsg"] = "Giỏ hàng của bạn đang trống!";
                return RedirectToAction("Index", "Product");
            }

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> PlaceOrder(string CustomerName, string Phone, string City, string Ward, string AddressDetail, string OrderNote, string PaymentMethod)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");
            int userId = int.Parse(userIdString);

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null || !cart.CartItems.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            // 1. Tạo địa chỉ đầy đủ và tính tiền
            string fullShippingAddress = $"{CustomerName} - SĐT: {Phone} - {AddressDetail}, {Ward}, {City}";
            decimal totalAmount = cart.CartItems.Sum(i => (i.Product?.Price ?? 0) * i.Quantity);

            // 2. Tạo đơn hàng mới
            var newOrder = new Order
            {
                UserId = userId,
                OrderDate = DateTime.Now,
                TotalAmount = totalAmount,
                ShippingFee = 0,
                ShippingAddress = fullShippingAddress,
                CustomerNote = OrderNote,
                PaymentMethod = PaymentMethod,
                PaymentStatus = "Pending",
                OrderStatus = "New",
                UpdatedAt = DateTime.Now
            };

            _context.Orders.Add(newOrder);
            await _context.SaveChangesAsync();

            // 3. Chuyển CartItems sang OrderItems
            foreach (var item in cart.CartItems)
            {
                var orderDetail = new OrderItem
                {
                    OrderId = newOrder.OrderId,
                    ProductId = item.ProductId ?? 0,
                    Quantity = item.Quantity,
                    UnitPrice = item.Product?.Price ?? 0
                };
                _context.OrderItems.Add(orderDetail);
            }

            // 4. Xóa giỏ hàng
            _context.CartItems.RemoveRange(cart.CartItems);
            await _context.SaveChangesAsync();

            // 5. XỬ LÝ THANH TOÁN
            if (PaymentMethod == "BANK")
            {
                // Gọi hàm tạo URL thanh toán VNPAY
                string vnpayUrl = CreateVnpayPaymentUrl(newOrder.OrderId, (double)totalAmount);
                return Redirect(vnpayUrl);
            }

            // Nếu là COD
            return RedirectToAction("OrderSuccess", new { orderId = newOrder.OrderId });
        }

        private string CreateVnpayPaymentUrl(int orderId, double totalAmount)
        {
            var vnp_TmnCode = _configuration["Vnpay:TmnCode"];
            var vnp_HashSecret = _configuration["Vnpay:HashSecret"];
            var vnp_Url = _configuration["Vnpay:BaseUrl"];
            var vnp_Returnurl = _configuration["Vnpay:ReturnUrl"];

            var pay = new VnPayLibrary();
            pay.AddRequestData("vnp_Version", "2.1.0");
            pay.AddRequestData("vnp_Command", "pay");
            pay.AddRequestData("vnp_TmnCode", vnp_TmnCode);

            // Tiền phải là số nguyên (long)
            long amount = (long)(totalAmount * 100);
            pay.AddRequestData("vnp_Amount", amount.ToString());

            pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", "VND");
            pay.AddRequestData("vnp_IpAddr", "127.0.0.1");
            pay.AddRequestData("vnp_Locale", "vn");

            // TUYỆT ĐỐI: Không dấu, không cách, không ký tự đặc biệt
            pay.AddRequestData("vnp_OrderInfo", "ThanhToanDonHang" + orderId);

            pay.AddRequestData("vnp_OrderType", "other");
            pay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
            pay.AddRequestData("vnp_TxnRef", orderId.ToString());

            return pay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
        }

        [HttpGet]
        public async Task<IActionResult> PaymentCallback()
        {
            var vnpayData = Request.Query;
            var pay = new VnPayLibrary();
            var vnp_HashSecret = _configuration["Vnpay:HashSecret"];

            foreach (string s in vnpayData.Keys)
            {
                if (!string.IsNullOrEmpty(s) && s.StartsWith("vnp_"))
                {
                    pay.AddResponseData(s, vnpayData[s]);
                }
            }

            string vnp_SecureHash = Request.Query["vnp_SecureHash"];
            bool checkSignature = pay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);

            if (checkSignature)
            {
                int orderId = int.Parse(pay.GetResponseData("vnp_TxnRef"));
                string vnp_ResponseCode = pay.GetResponseData("vnp_ResponseCode");

                if (vnp_ResponseCode == "00")
                {
                    var order = await _context.Orders.FindAsync(orderId);
                    if (order != null)
                    {
                        order.PaymentStatus = "Success";
                        await _context.SaveChangesAsync();
                        TempData["SuccessMsg"] = "Thanh toán thành công qua VNPAY!";
                        return RedirectToAction("OrderSuccess", new { orderId = orderId });
                    }
                }
            }

            TempData["ErrorMsg"] = "Thanh toán không thành công hoặc đã bị hủy.";
            return RedirectToAction("Index", "Cart");
        }

        public IActionResult OrderSuccess(int orderId)
        {
            ViewBag.OrderId = orderId;
            return View();
        }
    }
}