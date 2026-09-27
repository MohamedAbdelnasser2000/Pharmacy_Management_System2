using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using IMS.Data;
using IMS.Models;
using IMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IMS.Controllers
{
    public class ReturnCreateItemViewModel
    {
        [Required]
        public int OrderItemId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "الكمية المرتجعة يجب أن تكون أكبر من صفر")]
        public int Quantity { get; set; }

        public string? ItemReason { get; set; }

        public string? Condition { get; set; }

        public bool RestockItem { get; set; } = true;
    }

    public class ReturnCreateViewModel
    {
        [Required(ErrorMessage = "رقم الطلب مطلوب")]
        public int OrderId { get; set; }

        [Required(ErrorMessage = "نوع المرتجع مطلوب")]
        public string ReturnType { get; set; } = string.Empty;

        [Required(ErrorMessage = "سبب المرتجع مطلوب")]
        public string Reason { get; set; } = string.Empty;

        public string? Notes { get; set; }

        public List<ReturnCreateItemViewModel> Items { get; set; } = new();
    }

    [Authorize(Roles = RoleNamesConstants.Admin + "," + RoleNamesConstants.Accountant)]
    public class ReturnsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ReturnProcessingService _returnService;

        public ReturnsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ReturnProcessingService returnService)
        {
            _context = context;
            _userManager = userManager;
            _returnService = returnService;
        }

        public async Task<IActionResult> Index(string status = "", string searchTerm = "")
        {
            var returnsQuery = _context.Returns
                .Include(r => r.Order)
                .Include(r => r.Customer)
                .OrderByDescending(r => r.ReturnDate)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                returnsQuery = returnsQuery.Where(r => r.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                returnsQuery = returnsQuery.Where(r =>
                    r.Id.ToString().Contains(searchTerm) ||
                    r.Customer.Name.Contains(searchTerm) ||
                    r.Customer.PhoneNumber.Contains(searchTerm) ||
                    r.Status.Contains(searchTerm) ||
                    r.ReturnType.Contains(searchTerm) ||
                    r.OrderId.ToString().Contains(searchTerm));
            }

            var returns = await returnsQuery.ToListAsync();
            ViewBag.SelectedStatus = status;
            ViewBag.SearchTerm = searchTerm;
            return View(returns);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var returnRecord = await _context.Returns
                .Include(r => r.Order)
                .Include(r => r.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (returnRecord == null) return NotFound();

            var returnItems = await _context.ReturnItems
                .Include(ri => ri.Product)
                .Include(ri => ri.OrderItem)
                .Where(ri => ri.ReturnId == id)
                .ToListAsync();

            returnRecord.ReturnItems = returnItems;

            return View(returnRecord);
        }

        public async Task<IActionResult> Create(int? orderId)
        {
            var model = new ReturnCreateViewModel();
            if (orderId.HasValue)
            {
                model.OrderId = orderId.Value;
            }

            await PopulateCreateContext(orderId, model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReturnCreateViewModel model)
        {
            var items = model.Items ?? new List<ReturnCreateItemViewModel>();

            if (!items.Any())
            {
                ModelState.AddModelError(string.Empty, "يجب إضافة منتج واحد على الأقل للمرتجع");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var user = await _userManager.GetUserAsync(User);
                    var created = await _returnService.CreateAsync(
                        model.OrderId,
                        model.ReturnType,
                        model.Reason,
                        model.Notes,
                        items.Select(i => new ReturnProcessingService.ReturnCreateItem
                        {
                            OrderItemId = i.OrderItemId,
                            ProductId = i.ProductId,
                            Quantity = i.Quantity,
                            ItemReason = i.ItemReason,
                            Condition = i.Condition,
                            RestockItem = i.RestockItem
                        }),
                        user?.UserName);

                    TempData["SuccessMessage"] = "تم إنشاء المرتجع بنجاح";
                    return RedirectToAction(nameof(Details), new { id = created.Id });
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }

            await PopulateCreateContext(model.OrderId, model);
            return View(model);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var returnRecord = await _context.Returns.FindAsync(id);
            if (returnRecord == null) return NotFound();

            if (returnRecord.Status != ReturnStatusConstants.Pending)
            {
                TempData["ErrorMessage"] = "يمكن تعديل المرتجعات المعلقة فقط";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewData["OrderId"] = new SelectList(_context.Orders, "Id", "Id", returnRecord.OrderId);
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name", returnRecord.CustomerId);
            return View(returnRecord);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Return returnRecord)
        {
            if (id != returnRecord.Id) return NotFound();

            ModelState.Remove("Order");
            ModelState.Remove("Customer");
            ModelState.Remove("ReturnItems");

            if (!ModelState.IsValid)
            {
                ViewData["OrderId"] = new SelectList(_context.Orders, "Id", "Id", returnRecord.OrderId);
                ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Name", returnRecord.CustomerId);
                return View(returnRecord);
            }

            var existing = await _context.Returns.FindAsync(id);
            if (existing == null) return NotFound();

            if (existing.Status != ReturnStatusConstants.Pending)
            {
                TempData["ErrorMessage"] = "يمكن تعديل المرتجعات المعلقة فقط";
                return RedirectToAction(nameof(Details), new { id });
            }

            existing.Reason = returnRecord.Reason;
            existing.Notes = returnRecord.Notes;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث المرتجع بنجاح";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleNamesConstants.Admin)]
        public async Task<IActionResult> Approve(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                await _returnService.ApproveAsync(id, user?.UserName);
                TempData["SuccessMessage"] = "تمت الموافقة على المرتجع وتحديث المخزون";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleNamesConstants.Admin)]
        public async Task<IActionResult> Reject(int id, string rejectionReason)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["ErrorMessage"] = "يجب تحديد سبب الرفض";
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                await _returnService.RejectAsync(id, user?.UserName, rejectionReason);
                TempData["SuccessMessage"] = "تم رفض المرتجع";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessRefund(int id, decimal refundAmount, string refundMethod)
        {
            try
            {
                await _returnService.ProcessRefundAsync(id, refundAmount, refundMethod);
                TempData["SuccessMessage"] = $"تم معالجة استرداد بمبلغ {refundAmount:N2} جنيه";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return Json(new { success = false, message = "الطلب غير موجود" });
            }

            var orderItems = await _context.OrderItems
                .Include(oi => oi.Product)
                .Where(oi => oi.OrderId == orderId)
                .AsNoTracking()
                .ToListAsync();

            var orderItemIds = orderItems.Select(oi => oi.Id).ToList();

            var existingReturns = await _context.ReturnItems
                .Where(ri => ri.OrderItemId != null && orderItemIds.Contains(ri.OrderItemId.Value))
                .Include(ri => ri.Return)
                .Where(ri => ri.Return.Status != ReturnStatusConstants.Rejected)
                .ToListAsync();

            var returnedLookup = existingReturns
                .GroupBy(ri => ri.OrderItemId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(ri => ri.ReturnedQuantity));

            var items = orderItems.Select(oi =>
            {
                var alreadyReturned = returnedLookup.TryGetValue(oi.Id, out var value) ? value : 0;
                var available = Math.Max(0, oi.Quantity - alreadyReturned);

                return new
                {
                    id = oi.Id,
                    productId = oi.ProductId,
                    productName = oi.Product.Name,
                    quantity = oi.Quantity,
                    availableQuantity = available,
                    unitPrice = oi.UnitPrice,
                    discountPercentage = oi.DiscountPercentage
                };
            });

            return Json(new
            {
                success = true,
                customerId = order.CustomerId,
                customerName = order.Customer.Name,
                orderDate = order.OrderDate.ToString("yyyy-MM-dd"),
                totalAmount = order.TotalAmount,
                items
            });
        }

        public async Task<IActionResult> Print(int? id)
        {
            if (id == null) return NotFound();

            var returnRecord = await _context.Returns
                .Include(r => r.Order)
                .Include(r => r.Customer)
                .Include(r => r.ReturnItems)
                    .ThenInclude(ri => ri.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (returnRecord == null) return NotFound();

            return View(returnRecord);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var returnRecord = await _context.Returns
                .Include(r => r.Order)
                .Include(r => r.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (returnRecord == null) return NotFound();
            return View(returnRecord);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleNamesConstants.Admin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var returnRecord = await _context.Returns.FindAsync(id);
            if (returnRecord == null)
            {
                return RedirectToAction(nameof(Index));
            }

            if (returnRecord.Status == ReturnStatusConstants.Approved || returnRecord.Status == ReturnStatusConstants.Completed)
            {
                TempData["ErrorMessage"] = "لا يمكن حذف المرتجعات المعتمدة أو المكتملة";
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.Returns.Remove(returnRecord);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف المرتجع بنجاح";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateCreateContext(int? orderId, ReturnCreateViewModel? model)
        {
            var orders = await _context.Orders
                .Include(o => o.Customer)
                .OrderByDescending(o => o.Id)
                .AsNoTracking()
                .Take(200)
                .ToListAsync();

            ViewData["OrderId"] = new SelectList(orders, "Id", "Id", orderId);

            if (orderId.HasValue)
            {
                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == orderId.Value);

                if (order != null)
                {
                    ViewBag.SelectedOrder = order;

                    var orderItems = await _context.OrderItems
                        .Include(oi => oi.Product)
                        .Where(oi => oi.OrderId == orderId.Value)
                        .AsNoTracking()
                        .ToListAsync();

                    ViewBag.OrderItems = orderItems;
                }
            }

            if (model?.Items != null && model.Items.Any())
            {
                ViewBag.InitialItemsJson = System.Text.Json.JsonSerializer.Serialize(model.Items);
            }
        }

        private bool ReturnExists(int id)
        {
            return _context.Returns.Any(e => e.Id == id);
        }
    }
}
