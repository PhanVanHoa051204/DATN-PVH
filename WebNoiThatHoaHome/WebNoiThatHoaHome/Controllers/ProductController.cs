using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebNoiThatHoaHome.Models;

namespace WebNoiThatHoaHome.Controllers
{
    public class ProductController : Controller
    {
        private readonly HoaHomeDbContext _context;

        public ProductController(HoaHomeDbContext context)
        {
            _context = context;
        }

        // Action này sẽ xử lý khi khách hàng gõ từ khóa và ấn Enter
        // ĐÃ THÊM: tham số sortOrder
        public async Task<IActionResult> Search(string? keyword, string? sortOrder)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return RedirectToAction("Index");
            }

            ViewData["CategoryName"] = "Kết quả tìm kiếm cho: \"" + keyword + "\"";

            // 1. Lấy truy vấn gốc
            var query = _context.Products
                .Include(p => p.ProductImages)
                .Where(p => p.IsDeleted == false && p.IsActive == true && p.ProductName.Contains(keyword));

            // 2. Lọc theo giá
            if (sortOrder == "price_asc") { query = query.OrderBy(p => p.Price); }
            else if (sortOrder == "price_desc") { query = query.OrderByDescending(p => p.Price); }
            else { query = query.OrderByDescending(p => p.ProductId); }

            // 3. Đẩy ra View
            var products = await query.Select(p => new ProductListViewModel
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                Price = p.Price,
                MainImageUrl = p.ProductImages.Where(i => i.IsMain == true).Select(i => i.ImageUrl).FirstOrDefault() ?? "/images/no-image.png"
            }).ToListAsync();

            return View("Category", products);
        }

        // Action này sẽ bắt đường link /Product/Category/1
        // ĐÃ THÊM: tham số sortOrder
        public async Task<IActionResult> Category(int id, string? sortOrder)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null || category.IsDeleted == true)
            {
                return NotFound();
            }

            ViewData["CategoryName"] = category.CategoryName;

            // 1. Lấy truy vấn gốc
            var query = _context.Products
                .Include(p => p.ProductImages)
                .Where(p => p.CategoryId == id && p.IsDeleted == false && p.IsActive == true);

            // 2. Lọc theo giá
            if (sortOrder == "price_asc") { query = query.OrderBy(p => p.Price); }
            else if (sortOrder == "price_desc") { query = query.OrderByDescending(p => p.Price); }
            else { query = query.OrderByDescending(p => p.ProductId); }

            // 3. Đẩy ra View
            var products = await query.Select(p => new ProductListViewModel
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                Price = p.Price,
                MainImageUrl = p.ProductImages.FirstOrDefault(i => i.IsMain == true).ImageUrl ?? "/images/no-image.png"
            }).ToListAsync();

            return View(products);
        }

        // Action này sẽ xử lý khi khách hàng bấm vào nút "Sản phẩm mới"
        // ĐÃ THÊM: tham số sortOrder
        public async Task<IActionResult> NewProducts(string? sortOrder)
        {
            var targetCategories = new List<string> { "Tủ bếp", "Hàng trang trí", "Ngoại thất" };
            ViewData["CategoryName"] = "Sản phẩm mới";

            // 1. Lấy truy vấn gốc
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Where(p => p.IsDeleted == false && p.IsActive == true &&
                            targetCategories.Any(c => p.Category.CategoryName.Contains(c)));

            // 2. Lọc theo giá
            if (sortOrder == "price_asc") { query = query.OrderBy(p => p.Price); }
            else if (sortOrder == "price_desc") { query = query.OrderByDescending(p => p.Price); }
            else { query = query.OrderByDescending(p => p.ProductId); }

            // 3. Đẩy ra View
            var products = await query.Select(p => new ProductListViewModel
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                Price = p.Price,
                MainImageUrl = p.ProductImages.FirstOrDefault(i => i.IsMain == true).ImageUrl ?? "/images/no-image.png"
            }).ToListAsync();

            return View("Category", products);
        }

        // Action này sẽ xử lý khi khách hàng bấm vào chữ "SẢN PHẨM" trên menu
        // ĐÃ THÊM: tham số sortOrder
        public async Task<IActionResult> Index(string? sortOrder)
        {
            ViewData["CategoryName"] = "Tất cả sản phẩm";

            // 1. Lấy truy vấn gốc
            var query = _context.Products
                .Include(p => p.ProductImages)
                .Where(p => p.IsDeleted == false && p.IsActive == true);

            // 2. Lọc theo giá
            if (sortOrder == "price_asc") { query = query.OrderBy(p => p.Price); }
            else if (sortOrder == "price_desc") { query = query.OrderByDescending(p => p.Price); }
            else { query = query.OrderByDescending(p => p.ProductId); }

            // 3. Đẩy ra View
            var products = await query.Select(p => new ProductListViewModel
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                Price = p.Price,
                MainImageUrl = p.ProductImages.FirstOrDefault(i => i.IsMain == true).ImageUrl ?? "/images/no-image.png"
            }).ToListAsync();

            return View("Category", products);
        }

        // Hàm hiển thị trang chi tiết sản phẩm (Giữ nguyên vì không cần lọc)
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductId == id && p.IsDeleted == false && p.IsActive == true);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }
    }
}