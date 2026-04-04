using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
                ModelState.AddModelError("LoginError", "Email hoặc mật khẩu không chính xác.");
            }
            // Nếu đăng nhập lỗi, quay về trang chủ để Script tự bật Modal báo lỗi
            return View("~/Views/Home/Index.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email);
                if (emailExists)
                {
                    
                    // Thêm dòng này nếu muốn hiện chữ đỏ ngay dưới ô nhập Email:
                    ModelState.AddModelError("Email", "Email đã tồn tại.");
                    return View("~/Views/Home/Index.cshtml", model);
                }

                var customerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Customer");

                var newUser = new User
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    PasswordHash = model.Password,
                    RoleId = customerRole?.RoleId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now, // FIX LỖI TẠI ĐÂY
                    IsDeleted = false
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                TempData["SuccessMsg"] = "Đăng ký thành công! Vui lòng đăng nhập.";
                return RedirectToAction("Index", "Home");
            }

            // Nếu dữ liệu không hợp lệ (VD: Pass không khớp), trả về trang chủ kèm lỗi
            return View("~/Views/Home/Index.cshtml", model);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}