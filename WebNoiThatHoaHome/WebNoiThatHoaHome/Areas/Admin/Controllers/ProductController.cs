using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebNoiThatHoaHome.Models;
using System.IO;
using System;

namespace WebNoiThatHoaHome.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ProductController : Controller
    {
        private readonly HoaHomeDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductController(HoaHomeDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // ==========================================
        // 1. DANH SÁCH SẢN PHẨM
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentSearch"] = searchString;

            // Kết nối 3 bảng: Products + Categories + Product_Images
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Where(p => p.IsDeleted == false)
                .AsQueryable();

            // NÂNG CẤP XỬ LÝ TÌM KIẾM
            if (!string.IsNullOrEmpty(searchString))
            {
                string searchLower = searchString.ToLower();
                query = query.Where(p =>
                    p.ProductName.ToLower().Contains(searchLower) ||
                    (p.Category != null && p.Category.CategoryName.ToLower().Contains(searchLower))
                );
            }

            // Chuyển dữ liệu sang ViewModel
            var products = await query.Select(p => new ProductListViewModel
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                CategoryName = p.Category != null ? p.Category.CategoryName : "Chưa phân loại",
                Price = p.Price,
                StockQuantity = p.StockQuantity ?? 0,
                // Lấy ảnh chính, nếu không có thì lấy ảnh mặc định
                MainImageUrl = p.ProductImages.FirstOrDefault(i => i.IsMain == true).ImageUrl ?? "/images/no-image.png",
                IsActive = p.IsActive ?? false
            })
            .OrderBy(p => p.ProductId)
            .ToListAsync();

            return View(products);
        }

        // ==========================================
        // 2. HIỂN THỊ FORM THÊM MỚI
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // Lấy danh sách Danh mục từ Database nạp vào Dropdown
            var categories = await _context.Categories.Select(c => new SelectListItem
            {
                Value = c.CategoryId.ToString(),
                Text = c.CategoryName
            }).ToListAsync();

            var model = new ProductCreateViewModel
            {
                Categories = categories,
                StockQuantity = 1 // Mặc định số lượng là 1
            };

            return View(model);
        }

        // ==========================================
        // 3. LƯU SẢN PHẨM VÀ ẢNH VÀO DATABASE
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> Create(ProductCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Bước 1: Lưu thông tin Sản phẩm (chưa có ảnh)
                var product = new Product
                {
                    ProductName = model.ProductName,
                    CategoryId = model.CategoryId,
                    Price = model.Price,
                    StockQuantity = model.StockQuantity,
                    Dimensions = model.Dimensions,
                    Material = model.Material,
                    Description = model.Description,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsActive = true,
                    IsDeleted = false
                };

                _context.Products.Add(product);
                await _context.SaveChangesAsync(); // Lưu để SQL cấp mã ProductId mới

                // Bước 2: Xử lý lưu các file ảnh theo Danh mục
                if (model.UploadedImages != null && model.UploadedImages.Count > 0)
                {
                    string categoryFolderName = "Category_" + model.CategoryId;
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products", categoryFolderName);

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    bool isFirstImage = true;

                    foreach (var file in model.UploadedImages)
                    {
                        if (file.Length > 0)
                        {
                            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }

                            var productImage = new ProductImage
                            {
                                ProductId = product.ProductId,
                                ImageUrl = "/uploads/products/" + categoryFolderName + "/" + uniqueFileName,
                                IsMain = isFirstImage,
                                CreatedAt = DateTime.Now
                            };
                            _context.ProductImages.Add(productImage);
                            isFirstImage = false;
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMsg"] = $"Đã thêm sản phẩm [{model.ProductName}] thành công!";
                return RedirectToAction("Index");
            }

            // Nếu có lỗi nhập liệu, nạp lại danh sách danh mục và trả về View
            model.Categories = await _context.Categories.Select(c => new SelectListItem
            {
                Value = c.CategoryId.ToString(),
                Text = c.CategoryName
            }).ToListAsync();

            return View(model);
        }

        // ==========================================
        // 4. HIỂN THỊ FORM CHỈNH SỬA SẢN PHẨM
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null || product.IsDeleted == true) return NotFound();

            var categories = await _context.Categories.Select(c => new SelectListItem
            {
                Value = c.CategoryId.ToString(),
                Text = c.CategoryName
            }).ToListAsync();

            var model = new ProductEditViewModel
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                CategoryId = product.CategoryId ?? 0,
                Price = product.Price,
                StockQuantity = product.StockQuantity ?? 0,
                Dimensions = product.Dimensions,
                Material = product.Material,
                Description = product.Description,
                IsActive = product.IsActive ?? false,
                Categories = categories
            };

            return View(model);
        }

        // ==========================================
        // 5. LƯU THAY ĐỔI VÀO DATABASE
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> Edit(ProductEditViewModel model)
        {
            if (ModelState.IsValid)
            {
                var product = await _context.Products.FindAsync(model.ProductId);
                if (product == null) return NotFound();

                product.ProductName = model.ProductName;
                product.CategoryId = model.CategoryId;
                product.Price = model.Price;
                product.StockQuantity = model.StockQuantity;
                product.Dimensions = model.Dimensions;
                product.Material = model.Material;
                product.Description = model.Description;
                product.IsActive = model.IsActive;
                product.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = $"Cập nhật sản phẩm [{product.ProductName}] thành công!";
                return RedirectToAction("Index");
            }
            // Nếu lỗi, nạp lại Dropdown
            model.Categories = await _context.Categories.Select(c => new SelectListItem { Value = c.CategoryId.ToString(), Text = c.CategoryName }).ToListAsync();
            return View(model);
        }

        // ==========================================
        // 6. XÓA MỀM SẢN PHẨM
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                product.IsDeleted = true; // Chỉ gạt cờ Xóa mềm thành True
                product.IsActive = false; // Ẩn luôn khỏi trang khách hàng
                product.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = $"Đã chuyển sản phẩm [{product.ProductName}] vào thùng rác!";
            }
            return RedirectToAction("Index");
        }

        // ==========================================
        // 7. TRANG DANH SÁCH SẢN PHẨM ĐÃ XÓA (Thùng rác)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Trash()
        {
            // Chỉ lấy những sản phẩm có IsDeleted = true
            var deletedProducts = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Where(p => p.IsDeleted == true)
                .Select(p => new ProductListViewModel
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    CategoryName = p.Category != null ? p.Category.CategoryName : "N/A",
                    Price = p.Price,
                    MainImageUrl = p.ProductImages.FirstOrDefault(i => i.IsMain == true).ImageUrl ?? "/images/no-image.png"
                })
                .OrderByDescending(p => p.ProductId)
                .ToListAsync();

            return View(deletedProducts);
        }

        // ==========================================
        // 8. LỆNH KHÔI PHỤC SẢN PHẨM
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> Restore(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                product.IsDeleted = false; // Đưa về trạng thái chưa xóa
                product.IsActive = true;    // Cho phép hiển thị lại
                product.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = $"Đã khôi phục sản phẩm [{product.ProductName}] thành công!";
            }
            return RedirectToAction("Trash"); // Khôi phục xong thì ở lại trang thùng rác
        }
    }
}