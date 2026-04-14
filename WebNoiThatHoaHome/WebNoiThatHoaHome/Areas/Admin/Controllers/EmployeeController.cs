using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebNoiThatHoaHome.Models;

namespace WebNoiThatHoaHome.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class EmployeeController : Controller
    {
        private readonly HoaHomeDbContext _context;

        public EmployeeController(HoaHomeDbContext context)
        {
            _context = context;
        }

        // =======================================================
        // 1. DANH SÁCH NHÂN VIÊN
        // =======================================================
        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees
                .Include(e => e.User) // Lấy thông tin User (Tên, SĐT, Email)
                .Where(e => e.IsDeleted != true)
                .ToListAsync();

            return View(employees);
        }

        // =======================================================
        // 2. FORM THÊM NHÂN VIÊN MỚI (GET)
        // =======================================================
        public IActionResult Create()
        {
            // Lấy danh sách các User chưa tồn tại trong bảng Employee
            var availableUsers = _context.Users
                .Where(u => !_context.Employees.Any(e => e.UserId == u.UserId && e.IsDeleted != true))
                .Select(u => new
                {
                    u.UserId,
                    // Format lại hiển thị, nếu không có SĐT thì báo "Chưa có SĐT"
                    DisplayInfo = $"{u.FullName} - {(string.IsNullOrEmpty(u.Phone) ? "Chưa có SĐT" : u.Phone)}"
                })
                .ToList();

            ViewBag.UserId = new SelectList(availableUsers, "UserId", "DisplayInfo");

            return View();
        }

        // =======================================================
        // 3. XỬ LÝ LƯU NHÂN VIÊN MỚI (POST)
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee employee)
        {
            if (ModelState.IsValid)
            {
                // 1. Kiểm tra xem User này đã từng có hồ sơ chưa (để khôi phục nếu cần)
                var existingEmployee = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == employee.UserId);

                if (existingEmployee != null)
                {
                    existingEmployee.IsDeleted = false;
                    existingEmployee.Specialization = employee.Specialization;
                    existingEmployee.Status = employee.Status;
                    _context.Employees.Update(existingEmployee);
                }
                else
                {
                    employee.IsDeleted = false;
                    _context.Employees.Add(employee);
                }

                // ===============================================================
                // BỔ SUNG: ĐỒNG BỘ QUYỀN (ROLE) NGƯỢC LẠI BẢNG USER LÀ NHÂN VIÊN
                // ===============================================================
                var userToUpdate = await _context.Users.FindAsync(employee.UserId);
                if (userToUpdate != null)
                {
                    userToUpdate.RoleId = 16; // Gắn mác Nhân viên cho User này
                }
                // ===============================================================

                // Lưu tất cả thay đổi (cả Employee và User) vào Database cùng lúc
                await _context.SaveChangesAsync();

                TempData["SuccessMsg"] = "Đã thêm nhân sự và cấp quyền thành công!";
                return RedirectToAction(nameof(Index));
            }

            // Nếu form lỗi, load lại dropdown
            var availableUsers = _context.Users
                .Where(u => !_context.Employees.Any(e => e.UserId == u.UserId && e.IsDeleted != true))
                .Select(u => new
                {
                    u.UserId,
                    DisplayInfo = $"{u.FullName} - {(string.IsNullOrEmpty(u.Phone) ? "Chưa có SĐT" : u.Phone)}"
                })
                .ToList();

            ViewBag.UserId = new SelectList(availableUsers, "UserId", "DisplayInfo", employee.UserId);

            return View(employee);
        }
        // =======================================================
        // 4. CẬP NHẬT TRẠNG THÁI NHANH TỪ DROPDOWN
        // =======================================================
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var emp = await _context.Employees.FindAsync(id);
            if (emp != null)
            {
                emp.Status = status;
                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = "Đã cập nhật trạng thái làm việc!";
            }
            return RedirectToAction(nameof(Index));
        }

        // =======================================================
        // 5. FORM CHỈNH SỬA NHÂN VIÊN (GET)
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            // Cần Include(User) để lấy tên hiển thị ra Form cho đẹp
            var emp = await _context.Employees
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.EmployeeId == id);

            if (emp == null) return NotFound();
            return View(emp);
        }

        // =======================================================
        // 6. LƯU CHỈNH SỬA NHÂN VIÊN (POST)
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int EmployeeId, string Specialization, string Status)
        {
            var emp = await _context.Employees.FindAsync(EmployeeId);
            if (emp == null) return NotFound();

            emp.Specialization = Specialization;
            emp.Status = Status;
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = "Cập nhật thông tin nhân sự thành công!";
            return RedirectToAction(nameof(Index));
        }

        // =======================================================
        // 7. XÓA NHÂN SỰ (Khôi phục quyền về Khách hàng)
        // =======================================================
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var emp = await _context.Employees.FindAsync(id);
            if (emp != null)
            {
                emp.IsDeleted = true; // Xóa mềm khỏi danh sách nhân sự

                // Tùy chọn cực hay: Giáng chức ông này về lại Khách hàng (RoleId = 2)
                var user = await _context.Users.FindAsync(emp.UserId);
                if (user != null)
                {
                    user.RoleId = 2; // Trả về làm "dân thường"
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = "Đã xóa nhân sự và thu hồi quyền thành công!";
            }
            else
            {
                TempData["ErrorMsg"] = "Không tìm thấy nhân sự!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}