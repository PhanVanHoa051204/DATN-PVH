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
        // 1. TRANG DANH SÁCH NGƯỜI DÙNG (CÓ BỘ LỌC)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index(string searchString, int? roleFilter)
        {
            ViewData["CurrentSearch"] = searchString;
            ViewBag.CurrentFilter = roleFilter; // Lưu lại để View biết tab nào đang Active

            // NẠP THÊM BẢNG ROLE ĐỂ HIỂN THỊ TÊN QUYỀN RA GIAO DIỆN
            var usersQuery = _context.Users.Include(u => u.Role).AsQueryable();

            // Nếu Admin có gõ chữ vào thanh tìm kiếm
            if (!string.IsNullOrEmpty(searchString))
            {
                string keyword = searchString.Trim().ToLower();
                usersQuery = usersQuery.Where(u =>
                    u.FullName.ToLower().Contains(keyword) ||
                    u.Email.ToLower().Replace("@gmail.com", "").Contains(keyword)
                );
            }

            // LỌC THEO PHÂN QUYỀN (Admin/Nhân viên/Khách hàng)
            if (roleFilter.HasValue)
            {
                usersQuery = usersQuery.Where(u => u.RoleId == roleFilter.Value);
            }

            // Sắp xếp người mới nhất lên đầu
            var users = await usersQuery.OrderBy(u => u.CreatedAt).ToListAsync();
            return View(users);
        }

        // ==========================================
        // 2. TRANG CHI TIẾT (CHỈNH SỬA & CẤP QUYỀN)
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

            // --- LOGIC TỰ ĐỘNG TẠO NHÂN SỰ NẾU CẤP QUYỀN LÀ EMPLOYEE (ID = 16) ---
            if (model.RoleId == 16)
            {
                bool isEmployeeExist = await _context.Employees.AnyAsync(e => e.UserId == user.UserId);
                if (!isEmployeeExist)
                {
                    var newEmployee = new Employee
                    {
                        UserId = user.UserId,
                        Specialization = "Chưa phân công", // Sếp có thể vào bên Quản lý NS sửa sau
                        Status = "Available",
                        IsDeleted = false
                    };
                    _context.Employees.Add(newEmployee);
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = "Cập nhật thông tin và phân quyền thành công!";
            return RedirectToAction("Index");
        }


        // ==========================================
        // 3. THÊM MỚI NGƯỜI DÙNG
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
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
                    model.Roles = await _context.Roles.Select(r => new SelectListItem { Value = r.RoleId.ToString(), Text = r.RoleName }).ToListAsync();
                    return View(model);
                }

                // TẠO TÀI KHOẢN MỚI
                var newUser = new User
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    PasswordHash = model.Password,
                    RoleId = model.RoleId,
                    CreatedAt = DateTime.Now,
                    IsDeleted = false
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync(); // Lưu để SQL Server đẻ ra được UserId

                // --- LOGIC TỰ ĐỘNG TẠO NHÂN SỰ NẾU CHỌN LUÔN QUYỀN LÀ EMPLOYEE (ID = 16) ---
                if (newUser.RoleId == 16)
                {
                    var newEmployee = new Employee
                    {
                        UserId = newUser.UserId, // Đã lấy được UserId sau lệnh SaveChanges ở trên
                        Specialization = "Chưa phân công",
                        Status = "Available",
                        IsDeleted = false
                    };
                    _context.Employees.Add(newEmployee);
                    await _context.SaveChangesAsync(); // Lưu thêm phát nữa cho bảng Employee
                }

                TempData["SuccessMsg"] = "Đã thêm người dùng mới thành công!";
                return RedirectToAction("Index");
            }

            model.Roles = await _context.Roles.Select(r => new SelectListItem { Value = r.RoleId.ToString(), Text = r.RoleName }).ToListAsync();
            return View(model);
        }

        // ==========================================
        // 4. KHÓA / MỞ KHÓA TÀI KHOẢN
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user != null)
            {
                if (user.IsDeleted == true)
                {
                    user.IsDeleted = false; // Mở khóa
                    TempData["SuccessMsg"] = "Đã mở khóa tài khoản khách hàng!";
                }
                else
                {
                    user.IsDeleted = true; // Khóa
                    TempData["SuccessMsg"] = "Đã vô hiệu hóa tài khoản thành công!";
                }
                await _context.SaveChangesAsync();
            }
            else
            {
                TempData["ErrorMsg"] = "Lỗi: Không tìm thấy khách hàng!";
            }

            return RedirectToAction("Index");
        }
    }
}