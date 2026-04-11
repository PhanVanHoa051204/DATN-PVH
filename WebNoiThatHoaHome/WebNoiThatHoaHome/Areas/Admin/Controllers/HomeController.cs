using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebNoiThatHoaHome.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace WebNoiThatHoaHome.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")] // Khóa trang này lại, chỉ tài khoản có Role là Admin mới vào được
    public class HomeController : Controller
    {
        private readonly HoaHomeDbContext _context;

        public HomeController(HoaHomeDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Tổng số khách hàng
            var totalUsers = await _context.Users.Where(u => u.IsDeleted == false).CountAsync();

            // 2. Tổng số đơn hàng
            var totalOrders = await _context.Orders.CountAsync();

            // 3. Tổng doanh thu (Hoàn thành hoặc Đã thanh toán)
            var totalRevenue = await _context.Orders
                                    .Where(o => o.OrderStatus == "Completed" || o.PaymentStatus == "Success")
                                    .SumAsync(o => o.TotalAmount ?? 0);

            // 4. Lấy 5 đơn mới nhất
            var recentOrders = await _context.Orders
                                    .Include(o => o.User)
                                    .OrderByDescending(o => o.OrderDate)
                                    .Take(5)
                                    .ToListAsync();

            // ==========================================================
            // LOGIC MỚI: TÍNH DATA CHO BIỂU ĐỒ ĐƯỜNG (Doanh thu 7 ngày qua)
            // ==========================================================
            var sevenDaysAgo = DateTime.Now.Date.AddDays(-6);

            // Lấy doanh thu trong 7 ngày, nhóm theo ngày
            var revenueData = await _context.Orders
                .Where(o => o.OrderDate >= sevenDaysAgo && (o.OrderStatus == "Completed" || o.PaymentStatus == "Success"))
                .GroupBy(o => o.OrderDate.Value.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(o => o.TotalAmount ?? 0) })
                .ToListAsync();

            // Tạo mảng 7 ngày liên tiếp để hiển thị (kể cả ngày không có đơn cũng hiện 0đ)
            var last7Days = Enumerable.Range(0, 7).Select(i => sevenDaysAgo.AddDays(i)).ToList();
            var chartLabels = last7Days.Select(d => d.ToString("dd/MM")).ToArray(); // VD: "10/04", "11/04"
            var chartData = last7Days.Select(d => revenueData.FirstOrDefault(r => r.Date == d)?.Total ?? 0).ToArray();

            ViewBag.ChartLabels = System.Text.Json.JsonSerializer.Serialize(chartLabels);
            ViewBag.ChartData = System.Text.Json.JsonSerializer.Serialize(chartData);

            // ==========================================================
            // LOGIC MỚI: TÍNH DATA CHO BIỂU ĐỒ TRÒN (Trạng thái đơn hàng)
            // ==========================================================
            var statusCounts = await _context.Orders
                .GroupBy(o => o.OrderStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var newCount = statusCounts.FirstOrDefault(s => s.Status == "New")?.Count ?? 0;
            var processingCount = statusCounts.FirstOrDefault(s => s.Status == "Processing")?.Count ?? 0;
            var deliveringCount = statusCounts.FirstOrDefault(s => s.Status == "Delivering")?.Count ?? 0;
            var completedCount = statusCounts.FirstOrDefault(s => s.Status == "Completed")?.Count ?? 0;
            var cancelledCount = statusCounts.FirstOrDefault(s => s.Status == "Cancelled")?.Count ?? 0;

            // Thứ tự: [Mới, Xử lý, Đang giao, Hoàn thành, Hủy]
            var doughnutData = new[] { newCount, processingCount, deliveringCount, completedCount, cancelledCount };
            ViewBag.DoughnutData = System.Text.Json.JsonSerializer.Serialize(doughnutData);

            // Truyền sang ViewModel
            var viewModel = new AdminDashboardViewModel
            {
                TotalUsers = totalUsers,
                TotalOrders = totalOrders,
                TotalRevenue = totalRevenue,
                RecentOrders = recentOrders
            };

            return View(viewModel);
        }
        // --- THÊM VÀO DƯỚI HÀM INDEX HIỆN TẠI ---

        // 1. HIỆN GIAO DIỆN CHI TIẾT
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            // Lấy danh sách Roles từ DB để đưa vào Dropdown
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

        // 2. LƯU THÔNG TIN KHI ADMIN BẤM NÚT CẬP NHẬT
        [HttpPost]
        public async Task<IActionResult> Details(UserDetailViewModel model)
        {
            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null) return NotFound();

            // Cập nhật thông tin
            user.FullName = model.FullName;
            user.Phone = model.Phone;
            user.Address = model.Address;
            user.RoleId = model.RoleId;
            user.IsDeleted = model.IsDeleted; // Khóa hoặc mở khóa
            user.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // Thông báo thành công và quay lại trang danh sách
            TempData["AdminSuccessMsg"] = "Cập nhật thông tin khách hàng thành công!";
            return RedirectToAction("Index");
        }
    }
}