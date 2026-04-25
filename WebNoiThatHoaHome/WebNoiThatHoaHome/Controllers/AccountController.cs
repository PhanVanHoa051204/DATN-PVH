using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebNoiThatHoaHome.Models;

namespace WebNoiThatHoaHome.Controllers
{
    public class AccountController : Controller
    {
        private readonly HoaHomeDbContext _context;

        public AccountController(HoaHomeDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login() => RedirectToAction("Index", "Home");

        // XỬ LÝ ĐĂNG NHẬP 
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            // Kiểm tra dữ liệu người dùng nhập có hợp lệ không
            if (ModelState.IsValid)
            {
                var user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Email == model.Email && u.PasswordHash == model.Password);

                if (user != null)
                {                   
                    // Kiểm tra xem tk có bị khoá không                  
                    if (user.IsDeleted == true)
                    {
                        // Nếu bị khóa -> Lưu lỗi vào TempData và đẩy về trang chủ
                        TempData["LoginError"] = "Tài khoản của bạn đang bị khóa. Vui lòng liên hệ Admin!";
                        return RedirectToAction("Index", "Home");
                    }

                    // Đăng nhập thành công -> Tạo cookie chứa thông tin user và role để phân quyền
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                        new Claim(ClaimTypes.Name, user.FullName ?? ""),
                        new Claim(ClaimTypes.Email, user.Email ?? ""),
                        new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "Customer")
                    };

                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                    // Kiểm tra xem người dùng có phải là Admin không để đẩy về trang quản trị nếu đúng
                    if (user.Role?.RoleName == "Admin")
                        return RedirectToAction("Index", "Home", new { area = "Admin" });
                    // Nếu không phải Admin thì đẩy về trang chủ
                    return RedirectToAction("Index", "Home");
                }

                // Đăng nhập sai -> Lưu lỗi vào TempData và đẩy về trang chủ
                TempData["LoginError"] = "Email hoặc mật khẩu không chính xác!";
            }
            else
            {
                TempData["LoginError"] = "Vui lòng nhập đầy đủ thông tin!";
            }

            return RedirectToAction("Index", "Home");
        }

        //  XỬ LÝ ĐĂNG KÝ 
        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email);
                if (emailExists)
                {
                    // Trùng Email -> Lưu lỗi vào TempData và đẩy về trang chủ
                    TempData["RegisterError"] = "Email này đã được sử dụng!";
                    return RedirectToAction("Index", "Home");
                }

                var customerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Customer");

                var newUser = new User
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    PasswordHash = model.Password,
                    RoleId = customerRole?.RoleId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsDeleted = false
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                TempData["SuccessMsg"] = "Đăng ký thành công! Vui lòng đăng nhập.";
                return RedirectToAction("Index", "Home");
            }

            TempData["RegisterError"] = "Thông tin đăng ký chưa hợp lệ!";
            return RedirectToAction("Index", "Home");
        }
        // Xử lý đăng xuất: Xóa cookie và đẩy về trang chủ
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
        // Xử lý hiển thị trang thông tin cá nhân của khách
        public async Task<IActionResult> Profile()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");

            int userId = int.Parse(userIdString);
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            return View(user);
        }
        // Xử lý cập nhật thông tin cá nhân và đổi mật khẩu
        [HttpPost]
        public async Task<IActionResult> UpdateProfile(string FirstName, string LastName, string CurrentPassword, string NewPassword, string ConfirmPassword)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");
            // Lấy thông tin user từ database để kiểm tra mật khẩu hiện tại và cập nhật thông tin
            int userId = int.Parse(userIdString);
            var user = await _context.Users.FindAsync(userId);

            if (user != null)
            {
                user.FullName = FirstName + " " + LastName;
                if (!string.IsNullOrEmpty(NewPassword))
                {
                    if (user.PasswordHash != CurrentPassword)
                    {
                        TempData["ErrorMsg"] = "Mật khẩu hiện tại không chính xác!";
                        return RedirectToAction("Profile");
                    }
                    if (NewPassword != ConfirmPassword)
                    {
                        TempData["ErrorMsg"] = "Mật khẩu xác nhận không khớp!";
                        return RedirectToAction("Profile");
                    }
                    user.PasswordHash = NewPassword;
                }
                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = "Cập nhật thông tin thành công!";
            }
            return RedirectToAction("Profile");
        }
        // Xử lý cập nhật địa chỉ giao hàng
        [HttpPost]
        public async Task<IActionResult> UpdateAddress(string DeliveryName, string Phone, string City, string Ward, string AddressDetail)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");
            // Lấy thông tin user từ database để cập nhật địa chỉ giao hàng
            int userId = int.Parse(userIdString);
            var user = await _context.Users.FindAsync(userId);

            if (user != null)
            {
                if (!string.IsNullOrWhiteSpace(DeliveryName)) user.FullName = DeliveryName;
                user.Phone = Phone;
                
                string fullAddress = AddressDetail;
                if (!string.IsNullOrEmpty(Ward) && !string.IsNullOrEmpty(City))
                {
                    fullAddress = $"{AddressDetail}, {Ward}, {City}";
                }
                user.Address = fullAddress;

                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = "Cập nhật địa chỉ giao hàng thành công!";
            }
            return RedirectToAction("Profile");
        }
        // Xử lý hiển thị lịch sử đơn hàng của khách
        [HttpGet]
        public async Task<IActionResult> Orders()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");

            int userId = int.Parse(userIdString);
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            var reviewedIds = await _context.ProductReviews
            .Where(r => r.UserId == userId)
            .Select(r => r.ProductId)
            .ToListAsync();

            ViewBag.ReviewedProductIds = reviewedIds;
            return View(orders);
        }
        // Xử lý hiển thị danh sách yêu thích của khách
        [Authorize]
        public async Task<IActionResult> Wishlist()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdString, out int userId))
            {
                var wishlist = await _context.Wishlists
                    .Include(w => w.Product)
                        .ThenInclude(p => p.ProductImages)
                    .Where(w => w.UserId == userId)
                    .ToListAsync();

                return View(wishlist);
            }
            return RedirectToAction("Login");
        }
        // Xử lý xóa sản phẩm khỏi danh sách yêu thích
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> RemoveWishlist(int productId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdString, out int userId))
            {
                var wishlistItem = await _context.Wishlists
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

                if (wishlistItem != null)
                {
                    _context.Wishlists.Remove(wishlistItem);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMsg"] = "Đã xóa khỏi danh sách yêu thích!";
                }
            }
            return RedirectToAction("Wishlist");
        }
        // Xử lý yêu cầu hủy đơn hàng của khách
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> RequestCancelOrder(int orderId)
        {
            // Lấy ID của khách hàng đang đăng nhập
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return RedirectToAction("Login");
            // Tìm đơn hàng xem có đúng của khách này không
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId);
            if (order == null) return NotFound();
            // Chỉ cho phép hủy nếu đơn hàng đang "Chờ xử lý" hoặc "Chưa thanh toán"
            if (order.OrderStatus == "Đã hoàn thành" || order.OrderStatus == "Đang giao" || order.OrderStatus == "Đã hủy")
            {
                TempData["ErrorMsg"] = "Đơn hàng này đang được xử lý hoặc đã hoàn thành, không thể hủy!";
                return RedirectToAction("Orders");
            }
            //Cập nhật trạng thái thành Chờ duyệt
            order.OrderStatus = "Chờ xác nhận hủy";
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = "Đã gửi yêu cầu hủy! Vui lòng chờ Admin phê duyệt.";
            return RedirectToAction("Orders");
        }
        // Xử lý yêu cầu hủy đơn hàng của khách kèm thêm lý do
        [HttpPost]
        public async Task<IActionResult> RequestCancel(int orderId, string reason)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.OrderStatus = "PendingCancel";
                // Cộng dồn lý do vào CustomerNote
                order.CustomerNote = (order.CustomerNote ?? "") + "\n[Lý do hủy]: " + reason;

                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = "Đã gửi yêu cầu hủy đơn thành công!";
            }
            return RedirectToAction("Orders"); 
        }
        // Xử lý hiển thị lịch sử đặt lịch hẹn của khách
        [HttpGet]
        public async Task<IActionResult> Appointments()
        {
            
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdString))
            {
                return RedirectToAction("Login", "Account");
            }

            int customerId = int.Parse(userIdString);
            // Kéo danh sách lịch hẹn của Khách này
            var myAppointments = await _context.Appointments
                .Include(a => a.ServiceType)
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View(myAppointments);
        }
        // Xử lý hiển thị lịch sử đánh giá sản phẩm của khách
        [Authorize]
        public async Task<IActionResult> MyReviews()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login");
            int userId = int.Parse(userIdString);

            // Lấy lịch sử đánh giá của khách hàng này
            var reviews = await _context.ProductReviews
                .Include(r => r.Product)
                    .ThenInclude(p => p.ProductImages)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(reviews);
        }
    }
}