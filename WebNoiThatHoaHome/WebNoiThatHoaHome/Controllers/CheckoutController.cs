using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebNoiThatHoaHome.Models;
using WebNoiThatHoaHome.Services;
using Microsoft.AspNetCore.SignalR;
using WebNoiThatHoaHome.Hubs;
using Microsoft.AspNetCore.Authorization;
namespace WebNoiThatHoaHome.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly HoaHomeDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<OrderHub> _hubContext;
        
        public CheckoutController(HoaHomeDbContext context, IConfiguration configuration, IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _configuration = configuration;
            _hubContext = hubContext;
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
            var expressShipping = await _context.ServiceTypes
            .FirstOrDefaultAsync(s => s.TypeName.Contains("hoả tốc") && s.IsDeleted != true);
            decimal expressFee = expressShipping != null ? (expressShipping.BasePrice ?? 0) : 200000;

            ViewBag.ExpressFee = expressFee;
            return View(user);
        }
        // Hàm xử lý đặt hàng, tạo đơn và thanh toán
        [HttpPost]
        public async Task<IActionResult> PlaceOrder(string CustomerName, string Phone, string City, string Ward, string AddressDetail, string OrderNote, string PaymentMethod, string ShippingMethod, int? AppliedVoucherId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");
            int userId = int.Parse(userIdString);

            // Dùng Transaction để bảo vệ dữ liệu
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Lấy giỏ hàng
                    var cart = await _context.Carts
                        .Include(c => c.CartItems).ThenInclude(ci => ci.Product)
                        .FirstOrDefaultAsync(c => c.UserId == userId);

                    if (cart == null || !cart.CartItems.Any()) return RedirectToAction("Index", "Cart");

                    // 2. Tính toán tiền và Check Voucher lần cuối 
                    decimal subTotal = cart.CartItems.Sum(i => (i.Product?.Price ?? 0) * i.Quantity);
                    decimal shippingFee = 0;
                    if (ShippingMethod == "Hỏa tốc")
                    {
                        var express = await _context.ServiceTypes.FirstOrDefaultAsync(s => s.TypeName.Contains("hoả tốc") && s.IsDeleted != true);
                        shippingFee = express?.BasePrice ?? 200000;
                    }

                    decimal discountAmount = 0;
                    if (AppliedVoucherId.HasValue)
                    {
                        var voucher = await _context.Vouchers.FindAsync(AppliedVoucherId.Value);
                        if (voucher != null && voucher.IsActive == true && (voucher.UsageLimit == 0 || voucher.UsedCount < voucher.UsageLimit))
                        {
                            discountAmount = (voucher.DiscountType == "Percent") ? subTotal * (voucher.DiscountValue / 100) : voucher.DiscountValue;
                            voucher.UsedCount += 1; // Trừ lượt voucher
                            _context.Update(voucher);
                        }
                    }

                    decimal totalFinal = subTotal + shippingFee - discountAmount;

                    // 3. Tạo đơn hàng chính
                    var newOrder = new Order
                    {
                        UserId = userId,
                        OrderDate = DateTime.Now,
                        TotalAmount = totalFinal,
                        ShippingFee = shippingFee,
                        ShippingAddress = $"{CustomerName} - {Phone} - {AddressDetail}, {Ward}, {City}",
                        CustomerNote = $"[{ShippingMethod}] {OrderNote}",
                        PaymentMethod = PaymentMethod,
                        PaymentStatus = "Pending",
                        OrderStatus = "New",
                        VoucherId = AppliedVoucherId,
                        UpdatedAt = DateTime.Now
                    };

                    _context.Orders.Add(newOrder);
                    await _context.SaveChangesAsync(); // Lưu để lấy ID cho OrderItem
                    // 4. LƯU CHI TIẾT ĐƠN VÀ TRỪ TỒN KHO 
                    foreach (var item in cart.CartItems)
                    {
                        if (item.Product == null) continue;

                        // KIỂM TRA KHO: Đề phòng khách treo giỏ hàng từ hôm qua, nay mới đặt mà hàng đã hết
                        
                        if (item.Product.StockQuantity < item.Quantity)
                        {
                            await transaction.RollbackAsync(); // Hủy toàn bộ giao dịch
                            TempData["ErrorMsg"] = $"Rất tiếc! Sản phẩm '{item.Product.ProductName}' chỉ còn {item.Product.StockQuantity} cái trong kho. Vui lòng điều chỉnh lại giỏ hàng.";
                            return RedirectToAction("Index", "Cart"); // Đẩy về trang giỏ hàng
                        }
                        // LƯU CHI TIẾT
                        _context.OrderItems.Add(new OrderItem
                        {
                            OrderId = newOrder.OrderId,
                            ProductId = item.ProductId ?? 0,
                            Quantity = item.Quantity,
                            UnitPrice = item.Product.Price
                        });
                        // TRỪ KHO NGAY LẬP TỨC
                        item.Product.StockQuantity = (item.Product.StockQuantity ?? 0) - item.Quantity;
                        _context.Products.Update(item.Product);
                    }

                    // 5. Xóa giỏ hàng
                    _context.CartItems.RemoveRange(cart.CartItems);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    // 6. Thông báo SignalR và Xử lý thanh toán
                    await _hubContext.Clients.All.SendAsync("ReceiveNewOrder", newOrder.OrderId, newOrder.TotalAmount);

                    if (PaymentMethod == "BANK")
                    {
                        return Redirect(CreateVnpayPaymentUrl(newOrder.OrderId, (double)totalFinal));
                    }
                    TempData["OrderSuccess"] = "Tuyệt vời! Đơn hàng của bạn đã được đặt thành công.";
                    return RedirectToAction("OrderSuccess", new { orderId = newOrder.OrderId });
                }
                catch (Exception ex)
                {
                    // Nếu có bất kỳ lỗi gì, Rollback lại toàn bộ (Voucher không bị trừ, giỏ hàng không bị xóa, kho không bị trừ)
                    await transaction.RollbackAsync();
                    TempData["ErrorMsg"] = "Có lỗi xảy ra trong quá trình đặt hàng. Vui lòng thử lại!";
                    return RedirectToAction("Index");
                }
            }
        }
        // HÀM TẠO URL THANH TOÁN 
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

            long amount = (long)(totalAmount * 100);
            pay.AddRequestData("vnp_Amount", amount.ToString());
            pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", "VND");
            pay.AddRequestData("vnp_IpAddr", "127.0.0.1");
            pay.AddRequestData("vnp_Locale", "vn");
            pay.AddRequestData("vnp_OrderInfo", "ThanhToanDonHang" + orderId);
            pay.AddRequestData("vnp_OrderType", "other");
            pay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);

            string tick = DateTime.Now.ToString("HHmmss");
            pay.AddRequestData("vnp_TxnRef", orderId.ToString() + "_" + tick);

            return pay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
        }
        // HÀM THANH TOÁN LẠI
        [Authorize]
        public async Task<IActionResult> RePayment(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);

            if (order == null || order.PaymentStatus == "Success")
            {
                return RedirectToAction("Orders", "Account");
            }

            string vnp_Returnurl = _configuration["Vnpay:ReturnUrl"];
            string vnp_Url = _configuration["Vnpay:BaseUrl"];
            string vnp_TmnCode = _configuration["Vnpay:TmnCode"];
            string vnp_HashSecret = _configuration["Vnpay:HashSecret"];

            VnPayLibrary vnpay = new VnPayLibrary();
            long amount = (long)(order.TotalAmount * 100);

            vnpay.AddRequestData("vnp_Version", "2.1.0");
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
            vnpay.AddRequestData("vnp_Amount", amount.ToString());
            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", "VND");
            vnpay.AddRequestData("vnp_Locale", "vn");
            vnpay.AddRequestData("vnp_OrderInfo", "Thanh toan lai cho don hang #" + orderId);
            vnpay.AddRequestData("vnp_OrderType", "other");
            vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
            ;
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            vnpay.AddRequestData("vnp_IpAddr", ipAddress);
            // GẮN ĐUÔI THỜI GIAN CHỐNG TRÙNG MÃ
            string tick = DateTime.Now.ToString("HHmmss");
            vnpay.AddRequestData("vnp_TxnRef", orderId.ToString() + "_" + tick);

            string paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
            return Redirect(paymentUrl);
        }
        // Hàm xử lý callback từ VNPAY sau khi khách thanh toán xong (hoặc hủy)
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
                // TÌM ĐÚNG ID GỐC
                string txnRef = pay.GetResponseData("vnp_TxnRef");
                int orderId = int.Parse(txnRef.Split('_')[0]);

                string vnp_ResponseCode = pay.GetResponseData("vnp_ResponseCode");

                var paymentLog = new PaymentLog
                {
                    OrderId = orderId,
                    Method = "VNPAY",
                    Amount = decimal.Parse(pay.GetResponseData("vnp_Amount")) / 100,
                    ResponseCode = vnp_ResponseCode,
                    Message = vnp_ResponseCode == "00" ? "Giao dịch thành công" : "Giao dịch lỗi hoặc bị hủy",
                    RawData = Request.QueryString.Value,
                    CreatedAt = DateTime.Now
                };
                _context.PaymentLogs.Add(paymentLog);

                var order = await _context.Orders.FindAsync(orderId);
                if (order != null)
                {
                    if (vnp_ResponseCode == "00")
                    {
                        order.PaymentStatus = "Success";
                        await _context.SaveChangesAsync();
                        await _hubContext.Clients.All.SendAsync("ReceiveNewOrder", order.OrderId, order.TotalAmount);

                        TempData["OrderSuccess"] = "Thanh toán thành công qua VNPAY! Đơn hàng đã được ghi nhận.";
                        return RedirectToAction("OrderSuccess", new { orderId = orderId });
                    }
                    else
                    {
                        // ĐÓNG DẤU FAILED CHO ĐƠN HÀNG LỖI
                        order.PaymentStatus = "Failed";
                        await _context.SaveChangesAsync();
                    }
                }
            }

            TempData["ErrorMsg"] = "Thanh toán không thành công hoặc đã bị hủy.";
            return RedirectToAction("Orders", "Account"); // Đẩy về trang Đơn hàng của tôi để khách bấm "Thanh toán lại"
        }
        // Trang hiển thị sau khi đặt hàng thành công
        public IActionResult OrderSuccess(int orderId)
        {
            ViewBag.OrderId = orderId;
            return View();
        }
        // Hàm xử lý AJAX kiểm tra voucher khi khách nhập mã giảm giá ở trang Checkout
        [HttpPost]
        public async Task<IActionResult> CheckVoucher(string code, decimal totalAmount)
        {
            var voucher = await _context.Vouchers
                .FirstOrDefaultAsync(v => v.Code == code && v.IsActive == true);
            // 1. Kiểm tra tồn tại
            if (voucher == null) return Json(new { success = false, message = "Mã giảm giá không tồn tại!" });
            // 2. Kiểm tra hạn sử dụng
            if (voucher.EndDate < DateTime.Now) return Json(new { success = false, message = "Mã này đã hết hạn sử dụng!" });
            // 3. Kiểm tra số lượt dùng
            if (voucher.UsageLimit > 0 && voucher.UsedCount >= voucher.UsageLimit)
                return Json(new { success = false, message = "Mã này đã hết lượt sử dụng!" });
            // 4. Kiểm tra đơn hàng tối thiểu
            if (totalAmount < voucher.MinOrderValue)
                return Json(new { success = false, message = $"Đơn hàng tối thiểu {voucher.MinOrderValue:N0}đ để dùng mã này!" });
            // 5. Tính toán số tiền giảm
            decimal discount = 0;
            if (voucher.DiscountType == "Percent")
            {
                discount = totalAmount * (voucher.DiscountValue / 100);
            }
            else
            {
                discount = voucher.DiscountValue;
            }

            return Json(new
            {
                success = true,
                discountAmount = discount,
                voucherId = voucher.VoucherId,
                message = $"Đã áp dụng mã {code} thành công!"
            });
        }

    }
}