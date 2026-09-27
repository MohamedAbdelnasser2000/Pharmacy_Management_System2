using System;
using System.Linq;
using System.Threading.Tasks;
using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMS.Controllers
{
    [Authorize(Roles = RoleNamesConstants.Admin)]
    public class DiscountCodesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DiscountCodesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: DiscountCodes
        public async Task<IActionResult> Index()
        {
            return View(await _context.DiscountCodes.OrderByDescending(d => d.EndDate).ToListAsync());
        }

        // GET: DiscountCodes/Create
        public IActionResult Create()
        {
            var model = new DiscountCode
            {
                StartDate = AppClock.UtcNow,
                EndDate = AppClock.UtcNow.AddMonths(1)
            };
            return View(model);
        }

        // POST: DiscountCodes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DiscountCode discountCode)
        {
            if (ModelState.IsValid)
            {
                // Basic validation: must have either percentage or fixed amount
                if (!discountCode.Percentage.HasValue && !discountCode.FixedAmount.HasValue)
                {
                    ModelState.AddModelError("", "يجب تحديد نسبة خصم أو مبلغ خصم ثابت");
                    return View(discountCode);
                }

                _context.Add(discountCode);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة كود الخصم بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(discountCode);
        }

        // GET: DiscountCodes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var discountCode = await _context.DiscountCodes.FindAsync(id);
            if (discountCode == null) return NotFound();

            return View(discountCode);
        }

        // POST: DiscountCodes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DiscountCode discountCode)
        {
            if (id != discountCode.Id) return NotFound();

            if (ModelState.IsValid)
            {
                if (!discountCode.Percentage.HasValue && !discountCode.FixedAmount.HasValue)
                {
                    ModelState.AddModelError("", "يجب تحديد نسبة خصم أو مبلغ خصم ثابت");
                    return View(discountCode);
                }

                try
                {
                    _context.Update(discountCode);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "تم تحديث كود الخصم بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DiscountCodeExists(discountCode.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(discountCode);
        }

        // POST: DiscountCodes/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var discountCode = await _context.DiscountCodes.FindAsync(id);
            if (discountCode != null)
            {
                _context.DiscountCodes.Remove(discountCode);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف كود الخصم بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        // AJAX: Validate Discount Code
        [HttpGet]
        public async Task<IActionResult> ValidateCode(string code, decimal currentTotal)
        {
            if (string.IsNullOrWhiteSpace(code)) return Json(new { success = false, message = "الكود غير صالح" });

            var discount = await _context.DiscountCodes
                .FirstOrDefaultAsync(d => d.Code == code && d.IsActive && d.StartDate <= AppClock.UtcNow && d.EndDate >= AppClock.UtcNow);

            if (discount == null) return Json(new { success = false, message = "كود الخصم غير موجود أو منتهي الصلاحية" });

            if (discount.CurrentUsage >= discount.UsageLimit) return Json(new { success = false, message = "لقد وصل هذا الكود للحد الأقصى للاستخدام" });

            if (currentTotal < discount.MinimumOrderAmount) return Json(new { success = false, message = $"الحد الأدنى للطلب لاستخدام هذا الكود هو {discount.MinimumOrderAmount:N2}" });

            decimal discountAmount = 0;
            if (discount.Percentage.HasValue)
            {
                discountAmount = currentTotal * (discount.Percentage.Value / 100);
            }
            else if (discount.FixedAmount.HasValue)
            {
                discountAmount = discount.FixedAmount.Value;
            }

            return Json(new { 
                success = true, 
                discountId = discount.Id, 
                discountAmount = Math.Round(discountAmount, 2),
                message = "تم تطبيق الخصم بنجاح"
            });
        }

        private bool DiscountCodeExists(int id)
        {
            return _context.DiscountCodes.Any(e => e.Id == id);
        }
    }
}
