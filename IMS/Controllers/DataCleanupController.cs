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
    public class DataCleanupController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DataCleanupController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: DataCleanup/CheckDuplicates
        public async Task<IActionResult> CheckDuplicates()
        {
            var duplicateOrderItems = await _context.OrderItems
                .GroupBy(oi => new { oi.OrderId, oi.ProductId })
                .Where(g => g.Count() > 1)
                .Select(g => new
                {
                    OrderId = g.Key.OrderId,
                    ProductId = g.Key.ProductId,
                    Count = g.Count(),
                    Items = g.OrderBy(oi => oi.Id)
                        .Select(oi => new
                        {
                            oi.Id,
                            oi.Quantity,
                            oi.UnitPrice,
                            oi.DiscountPercentage
                        })
                })
                .ToListAsync();

            var duplicatePurchaseOrderItems = await _context.PurchaseOrderItems
                .GroupBy(poi => new { poi.PurchaseOrderId, poi.ProductId })
                .Where(g => g.Count() > 1)
                .Select(g => new
                {
                    PurchaseOrderId = g.Key.PurchaseOrderId,
                    ProductId = g.Key.ProductId,
                    Count = g.Count(),
                    Items = g.OrderBy(poi => poi.Id)
                        .Select(poi => new
                        {
                            poi.Id,
                            poi.Quantity,
                            poi.UnitPrice
                        })
                })
                .ToListAsync();

            ViewBag.DuplicateOrderItems = duplicateOrderItems;
            ViewBag.DuplicatePurchaseOrderItems = duplicatePurchaseOrderItems;

            return View();
        }

        // POST: DataCleanup/RemoveDuplicateOrderItems
        [HttpPost]
        public async Task<IActionResult> RemoveDuplicateOrderItems()
        {
            var idsToRemove = await _context.OrderItems
                .GroupBy(oi => new { oi.OrderId, oi.ProductId })
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.OrderBy(oi => oi.Id).Skip(1).Select(oi => oi.Id))
                .ToListAsync();

            if (!idsToRemove.Any())
            {
                TempData["SuccessMessage"] = "لا توجد عناصر مكررة في طلبات البيع";
                return RedirectToAction(nameof(CheckDuplicates));
            }

            var itemsToRemove = await _context.OrderItems
                .Where(oi => idsToRemove.Contains(oi.Id))
                .ToListAsync();

            _context.OrderItems.RemoveRange(itemsToRemove);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"تم حذف {itemsToRemove.Count} عنصر مكرر من طلبات البيع";
            return RedirectToAction(nameof(CheckDuplicates));
        }

        // POST: DataCleanup/RemoveDuplicatePurchaseOrderItems
        [HttpPost]
        public async Task<IActionResult> RemoveDuplicatePurchaseOrderItems()
        {
            var idsToRemove = await _context.PurchaseOrderItems
                .GroupBy(poi => new { poi.PurchaseOrderId, poi.ProductId })
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.OrderBy(poi => poi.Id).Skip(1).Select(poi => poi.Id))
                .ToListAsync();

            if (!idsToRemove.Any())
            {
                TempData["SuccessMessage"] = "لا توجد عناصر مكررة في أوامر الشراء";
                return RedirectToAction(nameof(CheckDuplicates));
            }

            var itemsToRemove = await _context.PurchaseOrderItems
                .Where(poi => idsToRemove.Contains(poi.Id))
                .ToListAsync();

            _context.PurchaseOrderItems.RemoveRange(itemsToRemove);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"تم حذف {itemsToRemove.Count} عنصر مكرر من أوامر الشراء";
            return RedirectToAction(nameof(CheckDuplicates));
        }

        // POST: DataCleanup/RemoveAllDuplicates
        [HttpPost]
        public async Task<IActionResult> RemoveAllDuplicates()
        {
            var orderIdsToRemove = await _context.OrderItems
                .GroupBy(oi => new { oi.OrderId, oi.ProductId })
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.OrderBy(oi => oi.Id).Skip(1).Select(oi => oi.Id))
                .ToListAsync();

            var purchaseIdsToRemove = await _context.PurchaseOrderItems
                .GroupBy(poi => new { poi.PurchaseOrderId, poi.ProductId })
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.OrderBy(poi => poi.Id).Skip(1).Select(poi => poi.Id))
                .ToListAsync();

            var orderItemsToRemove = await _context.OrderItems
                .Where(oi => orderIdsToRemove.Contains(oi.Id))
                .ToListAsync();

            var purchaseOrderItemsToRemove = await _context.PurchaseOrderItems
                .Where(poi => purchaseIdsToRemove.Contains(poi.Id))
                .ToListAsync();

            var totalRemoved = orderItemsToRemove.Count + purchaseOrderItemsToRemove.Count;

            if (totalRemoved == 0)
            {
                TempData["SuccessMessage"] = "لا توجد عناصر مكررة في قاعدة البيانات";
                return RedirectToAction(nameof(CheckDuplicates));
            }

            _context.OrderItems.RemoveRange(orderItemsToRemove);
            _context.PurchaseOrderItems.RemoveRange(purchaseOrderItemsToRemove);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✅ تم حذف {totalRemoved} عنصر مكرر بنجاح من قاعدة البيانات";

            return RedirectToAction(nameof(CheckDuplicates));
        }

        // GET: DataCleanup/VerifyQueries
        public async Task<IActionResult> VerifyQueries(int? orderId, int? purchaseOrderId, int? productId)
        {
            ViewBag.OrderId = orderId;
            ViewBag.PurchaseOrderId = purchaseOrderId;
            ViewBag.ProductId = productId;

            if (orderId.HasValue)
            {
                // Test Order query
                var order = await _context.Orders
                    .AsNoTracking()
                    .Include(o => o.Customer)
                    .FirstOrDefaultAsync(m => m.Id == orderId);

                var orderItems = await _context.OrderItems
                    .AsNoTracking()
                    .Include(oi => oi.Product)
                    .Where(oi => oi.OrderId == orderId)
                    .ToListAsync();

                ViewBag.OrderItemsCount = orderItems.Count;
                ViewBag.OrderItems = orderItems;
            }

            if (purchaseOrderId.HasValue)
            {
                // Test PurchaseOrder query
                var purchaseOrder = await _context.PurchaseOrders
                    .AsNoTracking()
                    .Include(po => po.Supplier)
                    .FirstOrDefaultAsync(m => m.Id == purchaseOrderId);

                var purchaseOrderItems = await _context.PurchaseOrderItems
                    .AsNoTracking()
                    .Include(poi => poi.Product)
                    .Where(poi => poi.PurchaseOrderId == purchaseOrderId)
                    .ToListAsync();

                ViewBag.PurchaseOrderItemsCount = purchaseOrderItems.Count;
                ViewBag.PurchaseOrderItems = purchaseOrderItems;
            }

            if (productId.HasValue)
            {
                // Test Product query
                var product = await _context.Products
                    .AsNoTracking()
                    .Include(p => p.Supplier)
                    .FirstOrDefaultAsync(m => m.Id == productId);

                var inventories = await _context.ProductInventories
                    .AsNoTracking()
                    .Include(pi => pi.Warehouse)
                    .Where(pi => pi.ProductId == productId)
                    .ToListAsync();

                ViewBag.InventoriesCount = inventories.Count;
                ViewBag.Inventories = inventories;
            }

            return View();
        }
    }
}
