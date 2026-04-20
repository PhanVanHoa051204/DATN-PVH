using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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
        public async Task<IActionResult> Search(string? keyword, string? sortOrder)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return RedirectToAction("Index");
            }

            ViewData["CategoryName"] = "Kết quả tìm kiếm cho: \"" + keyword + "\"";
            // 1. Lấy truy vấn gốc
            var query = _context.Products.Include(p => p.ProductImages).Where(p => p.IsDeleted == false && p.IsActive == true && p.ProductName.Contains(keyword));
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
        // Action này sẽ xử lý khi khách hàng bấm vào tên danh mục trên menu
        public async Task<IActionResult> Category(int id, string? sortOrder)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null || category.IsDeleted == true)
            {
                return NotFound();
            }

            ViewData["CategoryName"] = category.CategoryName;
            // 1. Lấy truy vấn gốc
            var query = _context.Products.Include(p => p.ProductImages).Where(p => p.CategoryId == id && p.IsDeleted == false && p.IsActive == true);

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
        public async Task<IActionResult> NewProducts(string? sortOrder)
        {
            var targetCategories = new List<string> { "Tủ bếp", "Hàng trang trí", "Ngoại thất" };
            ViewData["CategoryName"] = "Sản phẩm mới";
            // 1. Lấy truy vấn gốc
            var query = _context.Products.Include(p => p.Category).Include(p => p.ProductImages).Where(p => p.IsDeleted == false && p.IsActive == true &&
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
        // Action này sẽ xử lý khi khách hàng bấm vào nút "Tất cả sản phẩm"
        public async Task<IActionResult> Index(string? sortOrder)
        {
            ViewData["CategoryName"] = "Tất cả sản phẩm";
            // 1. Lấy truy vấn gốc
            var query = _context.Products.Include(p => p.ProductImages).Where(p => p.IsDeleted == false && p.IsActive == true);
            // 2. Lọc theo giá
            if (sortOrder == "price_asc") { query = query.OrderBy(p => p.Price); }
            else if (sortOrder == "price_desc") { query = query.OrderByDescending(p => p.Price); }
            else { query = query.OrderBy(p => p.ProductId); }
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
        // Action này sẽ xử lý khi khách hàng bấm vào tên sản phẩm để xem chi tiết
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products.Include(p => p.Category).Include(p => p.ProductImages).Include(p => p.ProductReviews.Where(r => r.IsApproved == true)).ThenInclude(r => r.User).FirstOrDefaultAsync(p => p.ProductId == id && p.IsDeleted == false && p.IsActive == true);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }
        // Action này sẽ xử lý khi khách hàng bấm vào nút "Viết đánh giá" trên trang chi tiết sản phẩm
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> WriteReview(int orderId, int productId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int userId = int.Parse(userIdString);
            // BỌC TRẠNG THÁI ĐƠN HÀNG Ở ĐÂY, CHỈ CHO ĐÁNH GIÁ KHI ĐƠN HÀNG Ở TRẠNG THÁI HOÀN THÀNH
            var validStatuses = new List<string> { "Completed", "Đã hoàn thành", "Hoàn thành", "Success" };

            var orderItem = await _context.OrderItems.Include(oi => oi.Product).ThenInclude(p => p.ProductImages).Include(oi => oi.Order)
                // Đảm bảo đơn hàng này thuộc về khách đang đăng nhập và có trạng thái hợp lệ để đánh giá
                .FirstOrDefaultAsync(oi => oi.OrderId == orderId && oi.ProductId == productId && oi.Order.UserId == userId && validStatuses.Contains(oi.Order.OrderStatus));
            if (orderItem == null)
            {
                TempData["ErrorMsg"] = "Đơn hàng chưa hoàn thành hoặc sản phẩm không hợp lệ!";
                return RedirectToAction("Orders", "Account");
            }
            // Kiểm tra xem khách đã đánh giá món này chưa 
            var alreadyReviewed = await _context.ProductReviews
                .AnyAsync(r => r.ProductId == productId && r.UserId == userId);

            if (alreadyReviewed)
            {
                TempData["ErrorMsg"] = "Bạn đã đánh giá sản phẩm này rồi!";
                return RedirectToAction("MyReviews", "Account");
            }

            return View(orderItem);
        }
        // Action này sẽ xử lý khi khách hàng gửi đánh giá sau khi viết xong
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> PostReview(int productId, int rating, string comment)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int userId = int.Parse(userIdString);
            // BỌC TRẠNG THÁI ĐƠN HÀNG Ở ĐÂY, CHỈ CHO ĐÁNH GIÁ KHI ĐƠN HÀNG Ở TRẠNG THÁI HOÀN THÀNH
            var validStatuses = new List<string> { "Completed", "Đã hoàn thành", "Hoàn thành", "Success" };
            var alreadyReviewed = await _context.ProductReviews.AnyAsync(r => r.ProductId == productId && r.UserId == userId);

            if (alreadyReviewed)
            {
                TempData["ErrorMsg"] = "Bạn đã đánh giá sản phẩm này rồi, không thể đánh giá thêm!";
                return RedirectToAction("MyReviews", "Account");
            }
            var hasBought = await _context.OrderItems .AnyAsync(oi => oi.ProductId == productId && oi.Order.UserId == userId && validStatuses.Contains(oi.Order.OrderStatus));
            if (!hasBought)
            {
                TempData["ErrorMsg"] = "Bạn chỉ có thể đánh giá sau khi đơn hàng đã hoàn thành.";
                return RedirectToAction("Details", new { id = productId });
            }
            // Nếu hợp lệ, tạo mới đánh giá và lưu vào data
            var review = new ProductReview
            {
                ProductId = productId,
                UserId = userId,
                Rating = rating,
                Comment = comment,
                CreatedAt = DateTime.Now,
                IsApproved = true 
            };

            _context.ProductReviews.Add(review);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = "Đánh giá của bạn đã được gửi thành công!";
            return RedirectToAction("MyReviews", "Account"); // Đăng xong đẩy về trang Quản lý đánh giá của khách
        }
    }
}