using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebNoiThatHoaHome.Models;

namespace WebNoiThatHoaHome.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly HoaHomeDbContext _context;

        public UserController(HoaHomeDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. TRANG DANH SÁCH KHÁCH HÀNG
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentSearch"] = searchString;

            // NẠP THÊM BẢNG ROLE ĐỂ HIỂN THỊ TÊN QUYỀN RA GIAO DIỆN
            var usersQuery = _context.Users.Include(u => u.Role).AsQueryable();

            // Nếu Admin có gõ chữ vào thanh tìm kiếm thì mới bắt đầu lọc
            if (!string.IsNullOrEmpty(searchString))
            {
                // 1. Cắt sạch khoảng trắng thừa ở 2 đầu và chuyển hết về chữ thường
                string keyword = searchString.Trim().ToLower();

                // 2. Dùng Contains kết hợp ToLower() để tìm kiếm linh hoạt nhất
                usersQuery = usersQuery.Where(u =>
                    u.FullName.ToLower().Contains(keyword) ||  u.Email.ToLower().Replace("@gmail.com", "").Contains(keyword)
                );
            }

            var users = await usersQuery.OrderBy(u => u.UserId).ToListAsync();
            return View(users);
        }

        // ==========================================
        // 2. TRANG CHI TIẾT (CHỈNH SỬA)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var roles = await _context.Roles.Select(r => new SelectListItem
            {
                Value = r.RoleId.ToString(),
                Text = r.RoleName
            }).ToListAsync();

            var model = new UserDetailViewModel
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Address = user.Address,
                RoleId = user.RoleId,
                IsDeleted = user.IsDeleted ?? false,
                CreatedAt = user.CreatedAt,
                Roles = roles
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Details(UserDetailViewModel model)
        {
            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.Phone = model.Phone;
            user.Address = model.Address;
            user.RoleId = model.RoleId;
            user.IsDeleted = model.IsDeleted;
            user.UpdatedAt = DateTime.Now;

            // ĐỔI MẬT KHẨU NẾU ADMIN NHẬP PASS MỚI
            if (!string.IsNullOrEmpty(model.NewPassword))
            {
                user.PasswordHash = model.NewPassword;
            }

            await _context.SaveChangesAsync();

            TempData["AdminSuccessMsg"] = "Cập nhật thông tin khách hàng thành công!";
            return RedirectToAction("Index");
        }

        // ==========================================
        // 3. THÊM MỚI NGƯỜI DÙNG
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // Lấy danh sách Roles từ Database để đưa vào Thẻ Select của HTML
            var model = new UserCreateViewModel();
            model.Roles = await _context.Roles.Select(r => new SelectListItem
            {
                Value = r.RoleId.ToString(),
                Text = r.RoleName
            }).ToListAsync();

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Create(UserCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra trùng Email
                var emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email);
                if (emailExists)
                {
                    ModelState.AddModelError("Email", "Email này đã được sử dụng!");

                    // Nạp lại danh sách quyền nếu bị lỗi
                    model.Roles = await _context.Roles.Select(r => new SelectListItem { Value = r.RoleId.ToString(), Text = r.RoleName }).ToListAsync();
                    return View(model);
                }

                // TẠO TÀI KHOẢN MỚI
                var newUser = new User
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    PasswordHash = model.Password, // Đồng bộ dùng PasswordHash
                    RoleId = model.RoleId,         // Đồng bộ dùng RoleId thay vì gõ chữ
                    CreatedAt = DateTime.Now,
                    
                    IsDeleted = false
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                TempData["AdminSuccessMsg"] = "Đã thêm người dùng mới thành công!";
                return RedirectToAction("Index");
            }

            // Nạp lại danh sách quyền nếu form không hợp lệ
            model.Roles = await _context.Roles.Select(r => new SelectListItem { Value = r.RoleId.ToString(), Text = r.RoleName }).ToListAsync();
            return View(model);
        }

        // ==========================================
        // 4. XÓA MỀM (VÔ HIỆU HÓA)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                // Vô hiệu hóa tài khoản
                user.IsDeleted = true;
                
                user.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                TempData["AdminSuccessMsg"] = "Đã vô hiệu hóa tài khoản thành công!";
            }
            return RedirectToAction("Index");
        }
    }
}