using System;
using System.Linq;
using System.Threading.Tasks;
using IMS.Data;
using IMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IMS.Controllers
{
    [Authorize(Roles = RoleNamesConstants.Admin + "," + RoleNamesConstants.WarehouseStaff)]
    public class BarcodeScanController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BarcodeScanController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// صفحة مسح الباركود الرئيسية
        /// </summary>
        public IActionResult Index()
        {
            ViewData["WarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name");
            return View();
        }

        /// <summary>
        /// البحث عن منتج بواسطة الباركود
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SearchByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                return Json(new { success = false, message = "الرجاء إدخال الباركود" });
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Include(p => p.Inventories)
                    .ThenInclude(i => i.Warehouse)
                .FirstOrDefaultAsync(p => p.Barcode == barcode.Trim());

            if (product == null)
            {
                return Json(new { success = false, message = "لم يتم العثور على منتج بهذا الباركود" });
            }

            return Json(new
            {
                success = true,
                product = new
                {
                    id = product.Id,
                    name = product.Name,
                    code = product.Code,
                    barcode = product.Barcode,
                    unit = product.Unit,
                    price = product.Price,
                    salePrice = product.SalePrice,
                    categoryName = product.Category?.Name,
                    supplierName = product.Supplier?.Name,
                    totalQuantity = product.TotalQuantity,
                    inventories = product.Inventories.Select(i => new
                    {
                        warehouseId = i.WarehouseId,
                        warehouseName = i.Warehouse?.Name,
                        quantity = i.OnHandQuantity
                    }).ToList()
                }
            });
        }

        /// <summary>
        /// تسجيل حركة إدخال بضاعة
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockIn(int productId, int warehouseId, decimal quantity, string reason)
        {
            if (quantity <= 0)
            {
                return Json(new { success = false, message = "الكمية يجب أن تكون أكبر من صفر" });
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return Json(new { success = false, message = "المنتج غير موجود" });
            }

            var warehouse = await _context.Warehouses.FindAsync(warehouseId);
            if (warehouse == null)
            {
                return Json(new { success = false, message = "المستودع غير موجود" });
            }

            try
            {
                // البحث عن المخزون الحالي أو إنشاء جديد
                var inventory = await _context.ProductInventories
                    .FirstOrDefaultAsync(pi => pi.ProductId == productId && pi.WarehouseId == warehouseId);

                if (inventory == null)
                {
                    inventory = new ProductInventory
                    {
                        ProductId = productId,
                        WarehouseId = warehouseId,
                        OnHandQuantity = quantity
                    };
                    _context.ProductInventories.Add(inventory);
                }
                else
                {
                    inventory.OnHandQuantity += quantity;
                    _context.ProductInventories.Update(inventory);
                }

                // تسجيل حركة المخزون
                var movement = new StockMovement
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    MovementType = "إدخال",
                    Quantity = quantity,
                    MovementDate = AppClock.UtcNow,
                    Reason = string.IsNullOrWhiteSpace(reason) ? "إدخال عن طريق مسح الباركود" : reason
                };
                _context.StockMovements.Add(movement);

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"تم إدخال {quantity} وحدة من {product.Name} إلى {warehouse.Name}",
                    newQuantity = inventory.OnHandQuantity
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"حدث خطأ: {ex.Message}" });
            }
        }

        /// <summary>
        /// تسجيل حركة إخراج بضاعة
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockOut(int productId, int warehouseId, decimal quantity, string reason)
        {
            if (quantity <= 0)
            {
                return Json(new { success = false, message = "الكمية يجب أن تكون أكبر من صفر" });
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return Json(new { success = false, message = "المنتج غير موجود" });
            }

            var warehouse = await _context.Warehouses.FindAsync(warehouseId);
            if (warehouse == null)
            {
                return Json(new { success = false, message = "المستودع غير موجود" });
            }

            try
            {
                // البحث عن المخزون الحالي
                var inventory = await _context.ProductInventories
                    .FirstOrDefaultAsync(pi => pi.ProductId == productId && pi.WarehouseId == warehouseId);

                if (inventory == null)
                {
                    return Json(new { success = false, message = "المنتج غير موجود في هذا المستودع" });
                }

                if (inventory.OnHandQuantity < quantity)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"الكمية المتاحة ({inventory.OnHandQuantity}) أقل من الكمية المطلوبة ({quantity})"
                    });
                }

                inventory.OnHandQuantity -= quantity;
                _context.ProductInventories.Update(inventory);

                // تسجيل حركة المخزون
                var movement = new StockMovement
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    MovementType = "إخراج",
                    Quantity = quantity,
                    MovementDate = AppClock.UtcNow,
                    Reason = string.IsNullOrWhiteSpace(reason) ? "إخراج عن طريق مسح الباركود" : reason
                };
                _context.StockMovements.Add(movement);

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"تم إخراج {quantity} وحدة من {product.Name} من {warehouse.Name}",
                    newQuantity = inventory.OnHandQuantity
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"حدث خطأ: {ex.Message}" });
            }
        }

        /// <summary>
        /// عرض سجل حركات المخزون
        /// </summary>
        public async Task<IActionResult> MovementHistory(int? productId, int? warehouseId, DateTime? fromDate, DateTime? toDate)
        {
            var movements = _context.StockMovements
                .Include(m => m.Product)
                .Include(m => m.Warehouse)
                .AsQueryable();

            if (productId.HasValue)
            {
                movements = movements.Where(m => m.ProductId == productId);
            }

            if (warehouseId.HasValue)
            {
                movements = movements.Where(m => m.WarehouseId == warehouseId);
            }

            if (fromDate.HasValue)
            {
                movements = movements.Where(m => m.MovementDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                movements = movements.Where(m => m.MovementDate <= toDate.Value.AddDays(1));
            }

            ViewData["ProductId"] = new SelectList(_context.Products, "Id", "Name", productId);
            ViewData["WarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name", warehouseId);
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(await movements.OrderByDescending(m => m.MovementDate).Take(100).ToListAsync());
        }
    }
}
