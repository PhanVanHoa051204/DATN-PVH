using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; 
using WebNoiThatHoaHome.Models;

namespace WebNoiThatHoaHome.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly HoaHomeDbContext _context; 

        public HomeController(ILogger<HomeController> logger, HoaHomeDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        // Trang chủ hiển thị danh mục và sản phẩm mới nhất
        public async Task<IActionResult> Index()
        {
            // 1. Lấy danh sách danh mục 
            var categories = await _context.Categories
                .Where(c => c.IsDeleted == false)
                .Take(3)
                .ToListAsync();

            // 2. Lấy 8 sản phẩm mới nhất 
            var newProducts = await _context.Products
                .Include(p => p.ProductImages)
                .Where(p => p.IsDeleted == false && p.IsActive == true)
                .OrderByDescending(p => p.ProductId) // ID mới nhất lên đầu
                .Take(8)
                .Select(p => new ProductListViewModel
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    Price = p.Price,
                    // Lấy ảnh chính, nếu không có lấy ảnh mặc định
                    MainImageUrl = p.ProductImages.FirstOrDefault(i => i.IsMain == true).ImageUrl ?? "/images/no-image.png"
                })
                .ToListAsync();

            // 3. Đóng gói vào ViewModel gửi ra ngoài giao diện
            var viewModel = new HomeViewModel
            {
                Categories = categories,
                NewProducts = newProducts
            };

            return View(viewModel); // Truyền dữ liệu ra View
        }

        // Trang chính sách bảo mật
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}