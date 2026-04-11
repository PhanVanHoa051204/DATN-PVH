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

        // --- 1. XỬ LÝ ĐĂNG NHẬP ---
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Email == model.Email && u.PasswordHash == model.Password);

                if (user != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                        new Claim(ClaimTypes.Name, user.FullName ?? ""),
                        new Claim(ClaimTypes.Email, user.Email ?? ""),
                        new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "Customer")
                    };

                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

                    if (user.Role?.RoleName == "Admin")
                        return RedirectToAction("Index", "Home", new { area = "Admin" });

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

        // --- 2. XỬ LÝ ĐĂNG KÝ ---
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

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // CÁC HÀM CŨ CỦA SẾP (GIỮ NGUYÊN KHÔNG ĐỔI)
        // ==========================================

        public async Task<IActionResult> Profile()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");

            int userId = int.Parse(userIdString);
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(string FirstName, string LastName, string CurrentPassword, string NewPassword, string ConfirmPassword)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");

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

        [HttpPost]
        public async Task<IActionResult> UpdateAddress(string DeliveryName, string Phone, string City, string Ward, string AddressDetail)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");

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

            return View(orders);
        }

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
    }
}