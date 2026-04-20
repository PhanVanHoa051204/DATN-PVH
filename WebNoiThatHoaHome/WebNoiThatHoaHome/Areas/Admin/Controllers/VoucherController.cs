using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebNoiThatHoaHome.Models;

namespace WebNoiThatHoaHome.Areas.Admin.Controllers;

[Area("Admin")]
public class VoucherController : Controller
{
    private readonly HoaHomeDbContext _context;

    public VoucherController(HoaHomeDbContext context) => _context = context;
    // 1. Hiển thị danh sách Voucher
    public async Task<IActionResult> Index() =>
        View(await _context.Vouchers.OrderByDescending(v => v.VoucherId).ToListAsync());
    // 2. Mở form thêm mới(gán sẵn ngày)
    public IActionResult Create() => View(new Voucher
    {
        StartDate = DateTime.Now,
        EndDate = DateTime.Now.AddDays(7),
        IsActive = true
    });
    // 3. Xử lý lưu Voucher mới
    [HttpPost]
    public async Task<IActionResult> Create(Voucher model)
    {
        if (!ModelState.IsValid) return View(model);

        model.Code = model.Code.ToUpper().Trim();
        // Check trùng mã
        if (await _context.Vouchers.AnyAsync(v => v.Code == model.Code))
        {
            ModelState.AddModelError("Code", "Mã Voucher này đã tồn tại!");
            return View(model);
        }
        _context.Vouchers.Add(model);
        await _context.SaveChangesAsync();
        TempData["SuccessMsg"] = "Thêm mã giảm giá thành công!";
        return RedirectToAction(nameof(Index));
    }
    // 4. Khóa Voucher 
    [HttpPost]
    public async Task<IActionResult> LockVoucher(int id)
    {
        var voucher = await _context.Vouchers.FindAsync(id);
        if (voucher != null)
        {
            voucher.IsActive = !voucher.IsActive; 
            await _context.SaveChangesAsync();
            TempData["SuccessMsg"] = voucher.IsActive ? "Đã mở khóa mã!" : "Đã khóa mã giảm giá!";
        }
        return RedirectToAction(nameof(Index));
    }
    // 5. MỞ FORM CHỈNH SỬA VOUCHER (GET)
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var voucher = await _context.Vouchers.FindAsync(id);
        if (voucher == null) return NotFound();

        return View(voucher);
    }
    // 6. LƯU DỮ LIỆU SAU KHI SỬA (POST)
    [HttpPost]
    public async Task<IActionResult> Edit(int id, Voucher model)
    {
        
        if (id != model.VoucherId) return NotFound();

        if (!ModelState.IsValid) return View(model);

        model.Code = model.Code.ToUpper().Trim();

        // Check trùng mã 
        bool isExist = await _context.Vouchers.AnyAsync(v => v.Code == model.Code && v.VoucherId != model.VoucherId);
        if (isExist)
        {
            ModelState.AddModelError("Code", "Mã Voucher này đã bị trùng với một mã khác!");
            return View(model);
        }

        try
        {
            _context.Update(model);
            await _context.SaveChangesAsync();
            TempData["SuccessMsg"] = "Cập nhật mã giảm giá thành công!";
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.Vouchers.AnyAsync(e => e.VoucherId == model.VoucherId)) return NotFound();
            else throw;
        }

        return RedirectToAction(nameof(Index));
    }
}