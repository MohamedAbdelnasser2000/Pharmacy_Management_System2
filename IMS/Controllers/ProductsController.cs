using System.Linq;
using System.Threading.Tasks;
using IMS.Data;
using IMS.Models;
using IMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using System;

namespace IMS.Controllers
{
    [Authorize(Roles = RoleNamesConstants.Admin + "," + RoleNamesConstants.Pharmacist + "," + RoleNamesConstants.WarehouseStaff + "," + RoleNamesConstants.DataEntry)]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly BarcodeService _barcodeService;
        private readonly IAuditService _auditService;
        private readonly IWebHostEnvironment _hostEnvironment;

        public ProductsController(ApplicationDbContext context, BarcodeService barcodeService, IAuditService auditService, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _barcodeService = barcodeService;
            _auditService = auditService;
            _hostEnvironment = hostEnvironment;
        }

        public async Task<IActionResult> Index(string searchString, int? categoryId)
        {
            var products = _context.Products
                .Include(p => p.Supplier)
                .Include(p => p.Category)
                .Include(p => p.Inventories)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                products = products.Where(p => 
                    p.Name.Contains(searchString) || 
                    (p.Code != null && p.Code.Contains(searchString)) ||
                    (p.Barcode != null && p.Barcode.Contains(searchString)) ||
                    (p.Supplier != null && p.Supplier.Name.Contains(searchString)) ||
                    (p.Category != null && p.Category.Name.Contains(searchString)));
            }

            if (categoryId.HasValue)
            {
                products = products.Where(p => p.CategoryId == categoryId);
            }

            ViewData["CurrentFilter"] = searchString;
            ViewData["CategoryId"] = new SelectList(_context.Categories.Where(c => c.IsActive), "Id", "Name", categoryId);
            return View(await products.OrderByDescending(p => p.Id).ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Load product with supplier and category
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Supplier)
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            // Load inventories separately
            var inventories = await _context.ProductInventories
                .AsNoTracking()
                .Include(pi => pi.Warehouse)
                .Where(pi => pi.ProductId == id)
                .ToListAsync();

            product.Inventories = inventories;

            // Load purchase price history (Select automatically loads required navigation properties)
            var priceHistory = await _context.PurchaseOrderItems
                .AsNoTracking()
                .Where(poi => poi.ProductId == id)
                .OrderByDescending(poi => poi.PurchaseOrder.OrderDate)
                .Select(poi => new
                {
                    OrderDate = poi.PurchaseOrder.OrderDate,
                    UnitPrice = poi.UnitPrice,
                    Quantity = poi.Quantity,
                    SupplierName = poi.PurchaseOrder.Supplier.Name,
                    OrderId = poi.PurchaseOrder.Id,
                    Status = poi.PurchaseOrder.Status
                })
                .Take(10) // Last 10 purchases
                .ToListAsync();

            ViewBag.PriceHistory = priceHistory;

            return View(product);
        }

        /// <summary>
        /// التحقق من الباركود وإنشاء منتج جديد إذا لم يكن موجوداً
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CheckBarcodeAndCreate(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                return Json(new { success = false, message = "الرجاء إدخال الباركود" });
            }

            // التحقق من وجود المنتج
            var existingProduct = await _context.Products
                .FirstOrDefaultAsync(p => p.Barcode == barcode.Trim());

            if (existingProduct != null)
            {
                return Json(new 
                { 
                    success = false, 
                    exists = true,
                    message = "هذا الباركود موجود بالفعل للمنتج: " + existingProduct.Name,
                    productId = existingProduct.Id
                });
            }

            // إعادة التوجيه إلى صفحة الإنشاء مع الباركود
            return Json(new 
            { 
                success = true, 
                redirectUrl = Url.Action("Create", new { barcode = barcode.Trim() })
            });
        }

        public IActionResult Create(string barcode = null)
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories.Where(c => c.IsActive), "Id", "Name");
            ViewData["SupplierId"] = new SelectList(_context.Suppliers, "Id", "Name");
            ViewData["WarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name");
            
            // إذا تم تمرير باركود، قم بإنشاء منتج جديد بهذا الباركود
            var product = new Product();
            if (!string.IsNullOrWhiteSpace(barcode))
            {
                product.Barcode = barcode.Trim();
                ViewBag.BarcodeScanned = true;
                TempData["InfoMessage"] = "تم مسح الباركود بنجاح. الرجاء إكمال بيانات المنتج.";
            }
            
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, int? InitialWarehouseId, decimal InitialQuantity, IFormFile? productImage)
        {
            // Remove validation for navigation properties
            ModelState.Remove("Category");
            ModelState.Remove("Supplier");
            ModelState.Remove("Units");
            ModelState.Remove("Inventories");

            // التحقق من عدم تكرار الباركود إذا تم إدخاله
            if (!string.IsNullOrWhiteSpace(product.Barcode))
            {
                var existingProduct = await _context.Products
                    .FirstOrDefaultAsync(p => p.Barcode == product.Barcode.Trim());
                
                if (existingProduct != null)
                {
                    ModelState.AddModelError("Barcode", "هذا الباركود موجود بالفعل للمنتج: " + existingProduct.Name);
                }
            }

            if (ModelState.IsValid)
            {
                // Handle Image Upload
                if (productImage != null && productImage.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads", "products");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                    
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + productImage.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await productImage.CopyToAsync(fileStream);
                    }
                    
                    product.ImageUrl = "/uploads/products/" + uniqueFileName;
                }

                // توليد باركود تلقائي إذا لم يكن موجوداً
                if (string.IsNullOrWhiteSpace(product.Barcode))
                {
                    // حفظ المنتج أولاً للحصول على ID
                    _context.Add(product);
                    await _context.SaveChangesAsync();
                    
                    // توليد باركود فريد
                    product.Barcode = _barcodeService.GenerateUniqueBarcode(product.Id);
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // حفظ المنتج مع الباركود الممسوح
                    _context.Add(product);
                    await _context.SaveChangesAsync();
                }

                // Add initial inventory if warehouse and quantity are provided
                if (InitialWarehouseId.HasValue && InitialQuantity > 0)
                {
                    var inventory = new ProductInventory
                    {
                        Product = product, // Use navigation property
                        WarehouseId = InitialWarehouseId.Value,
                        OnHandQuantity = InitialQuantity
                    };
                    _context.ProductInventories.Add(inventory);

                    // Add stock movement record
                    var movement = new StockMovement
                    {
                        Product = product, // Use navigation property
                        WarehouseId = InitialWarehouseId.Value,
                        MovementType = "إدخال",
                        Quantity = InitialQuantity,
                        MovementDate = AppClock.UtcNow,
                        Reason = "كمية ابتدائية عند إنشاء المنتج"
                    };
                    _context.StockMovements.Add(movement);
                    
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "تم إضافة المنتج بنجاح";
                
                // إعادة التوجيه إلى صفحة تحميل الباركود
                return RedirectToAction(nameof(DownloadBarcodes), new { id = product.Id, quantity = (int)InitialQuantity });
            }
            ViewData["SupplierId"] = new SelectList(_context.Suppliers, "Id", "Name", product.SupplierId);
            ViewData["WarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name");
            return View(product);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FindAsync(id);
            ViewData["CategoryId"] = new SelectList(_context.Categories.Where(c => c.IsActive), "Id", "Name", product.CategoryId);
            ViewData["SupplierId"] = new SelectList(_context.Suppliers, "Id", "Name", product.SupplierId);
            ViewData["WarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name");
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, int? InitialWarehouseId, decimal InitialQuantity, IFormFile? productImage)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            // Remove validation for navigation properties
            ModelState.Remove("Category");
            ModelState.Remove("Supplier");
            ModelState.Remove("Units");
            ModelState.Remove("Inventories");

            if (ModelState.IsValid)
            {
                try
                {
                    // Handle Image Upload
                    if (productImage != null && productImage.Length > 0)
                    {
                        // Delete old image if exists
                        if (!string.IsNullOrEmpty(product.ImageUrl))
                        {
                            string oldPath = Path.Combine(_hostEnvironment.WebRootPath, product.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                        }

                        string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads", "products");
                        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + productImage.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await productImage.CopyToAsync(fileStream);
                        }

                        product.ImageUrl = "/uploads/products/" + uniqueFileName;
                    }

                    _context.Update(product);
                    await _context.SaveChangesAsync();
                    
                    // Add inventory if warehouse and quantity are provided
                    if (InitialWarehouseId.HasValue && InitialQuantity > 0)
                    {
                        // Check if inventory already exists for this warehouse
                        var existingInventory = await _context.ProductInventories
                            .FirstOrDefaultAsync(pi => pi.ProductId == product.Id && pi.WarehouseId == InitialWarehouseId.Value);
                        
                        if (existingInventory != null)
                        {
                            // Update existing inventory
                            existingInventory.OnHandQuantity += InitialQuantity;
                            _context.Update(existingInventory);
                        }
                        else
                        {
                            // Create new inventory
                            var inventory = new ProductInventory
                            {
                                ProductId = product.Id,
                                WarehouseId = InitialWarehouseId.Value,
                                OnHandQuantity = InitialQuantity
                            };
                            _context.ProductInventories.Add(inventory);
                        }

                        // Add stock movement record
                        var movement = new StockMovement
                        {
                            ProductId = product.Id,
                            WarehouseId = InitialWarehouseId.Value,
                            MovementType = "إدخال",
                            Quantity = InitialQuantity,
                            MovementDate = AppClock.UtcNow,
                            Reason = "إضافة كمية عند تعديل المنتج"
                        };
                        _context.StockMovements.Add(movement);
                        
                        await _context.SaveChangesAsync();
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                
                TempData["SuccessMessage"] = "تم تعديل المنتج بنجاح";
                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories.Where(c => c.IsActive), "Id", "Name", product.CategoryId);
            ViewData["SupplierId"] = new SelectList(_context.Suppliers, "Id", "Name", product.SupplierId);
            ViewData["WarehouseId"] = new SelectList(_context.Warehouses, "Id", "Name");
            return View(product);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Load product with supplier and category
            var product = await _context.Products
                .Include(p => p.Supplier)
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.Id == id);
            
            if (product != null)
            {
                // Load inventories separately
                var inventories = await _context.ProductInventories
                    .Include(pi => pi.Warehouse)
                    .Where(pi => pi.ProductId == id)
                    .ToListAsync();
                product.Inventories = inventories;
            }
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleNamesConstants.Admin)]
        public async Task<IActionResult> DeleteConfirmed(int id, bool forceDelete = false)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                try
                {
                    // التحقق من وجود طلبات أو حركات مرتبطة
                    var hasOrders = await _context.OrderItems.AnyAsync(oi => oi.ProductId == id);
                    var hasInventoryMovements = await _context.StockMovements.AnyAsync(sm => sm.ProductId == id);
                    var ordersCount = await _context.OrderItems.CountAsync(oi => oi.ProductId == id);
                    var movementsCount = await _context.StockMovements.CountAsync(sm => sm.ProductId == id);

                    // إذا لم يتم تأكيد الحذف القسري، عرض رسالة تحذيرية
                    if (!forceDelete && (hasOrders || hasInventoryMovements))
                    {
                        var warningMessage = "⚠️ تحذير: هذا المنتج مرتبط بـ ";
                        if (hasOrders)
                            warningMessage += $"{ordersCount} عنصر في الطلبات";
                        if (hasOrders && hasInventoryMovements)
                            warningMessage += " و ";
                        if (hasInventoryMovements)
                            warningMessage += $"{movementsCount} حركة مخزون";
                        
                        warningMessage += ". حذفه سيؤثر على البيانات التاريخية.";
                        
                        TempData["WarningMessage"] = warningMessage;
                        TempData["ProductIdToDelete"] = id;
                        TempData["ProductNameToDelete"] = product.Name;
                        return RedirectToAction(nameof(Delete), new { id = id });
                    }

                    // حذف البيانات المرتبطة أولاً
                    // حذف المخزون
                    var inventories = await _context.ProductInventories.Where(pi => pi.ProductId == id).ToListAsync();
                    if (inventories.Any())
                    {
                        _context.ProductInventories.RemoveRange(inventories);
                    }

                    // حذف حركات المخزون
                    var movements = await _context.StockMovements.Where(sm => sm.ProductId == id).ToListAsync();
                    if (movements.Any())
                    {
                        _context.StockMovements.RemoveRange(movements);
                    }

                    // حذف عناصر الطلبات
                    var orderItems = await _context.OrderItems.Where(oi => oi.ProductId == id).ToListAsync();
                    if (orderItems.Any())
                    {
                        _context.OrderItems.RemoveRange(orderItems);
                    }

                    // حذف المنتج
                    _context.Products.Remove(product);
                    await _context.SaveChangesAsync();
                    
                    await _auditService.LogAsync("Delete", "Product", id.ToString(), product, null);
                    
                    TempData["SuccessMessage"] = "تم حذف المنتج وجميع البيانات المرتبطة به بنجاح";
                }
                catch (DbUpdateException ex)
                {
                    TempData["ErrorMessage"] = "حدث خطأ أثناء حذف المنتج. يرجى المحاولة مرة أخرى.";
                    return RedirectToAction(nameof(Index));
                }
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// صفحة تحميل الباركودات بعد إضافة المنتج
        /// </summary>
        public async Task<IActionResult> DownloadBarcodes(int? id, int quantity = 1)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            ViewBag.Quantity = quantity > 0 ? quantity : 1;
            return View(product);
        }

        /// <summary>
        /// تحميل ملف PDF يحتوي على الباركودات
        /// </summary>
        public async Task<IActionResult> GenerateBarcodePDF(int? id, int quantity = 1)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);
            
            if (product == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(product.Barcode))
            {
                return BadRequest("المنتج لا يحتوي على باركود");
            }

            try
            {
                // توليد ملف PDF
                var pdfBytes = _barcodeService.GenerateBarcodePDF(
                    product.Name,
                    product.Barcode,
                    quantity > 0 ? quantity : 1,
                    product.Category?.Name
                );

                // إرجاع الملف للتحميل
                var fileName = $"Barcodes_{product.Name}_{AppClock.UtcNow:yyyyMMdd_HHmmss}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"حدث خطأ أثناء توليد ملف PDF: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id = product.Id });
            }
        }

        /// <summary>
        /// تحميل صورة باركود واحدة
        /// </summary>
        public async Task<IActionResult> GenerateBarcodeImage(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(product.Barcode))
            {
                return BadRequest("المنتج لا يحتوي على باركود");
            }

            try
            {
                var imageBytes = _barcodeService.GenerateBarcodeImage(product.Barcode);
                return File(imageBytes, "image/png");
            }
            catch (Exception ex)
            {
                return BadRequest($"خطأ في توليد الباركود: {ex.Message}");
            }
        }

        /// <summary>
        /// تصدير قائمة المنتجات إلى PDF
        /// </summary>
        public async Task<IActionResult> ExportToPDF(string searchString, int? categoryId)
        {
            var products = _context.Products
                .Include(p => p.Supplier)
                .Include(p => p.Category)
                .Include(p => p.Inventories)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                products = products.Where(p =>
                    p.Name.Contains(searchString) ||
                    (p.Code != null && p.Code.Contains(searchString)) ||
                    (p.Barcode != null && p.Barcode.Contains(searchString)));
            }

            if (categoryId.HasValue)
            {
                products = products.Where(p => p.CategoryId == categoryId);
            }

            var productList = await products.OrderBy(p => p.Name).ToListAsync();

            try
            {
                using (var ms = new MemoryStream())
                {
                    var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4.Rotate(), 20, 20, 30, 30);
                    var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, ms);
                    document.Open();

                    // Add title
                    var titleFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 18);
                    var title = new iTextSharp.text.Paragraph($"Products List - {AppClock.UtcNow:yyyy-MM-dd}", titleFont)
                    {
                        Alignment = iTextSharp.text.Element.ALIGN_CENTER,
                        SpacingAfter = 20
                    };
                    document.Add(title);

                    // Create table
                    var table = new iTextSharp.text.pdf.PdfPTable(7) { WidthPercentage = 100 };
                    table.SetWidths(new float[] { 5f, 20f, 15f, 15f, 12f, 12f, 10f });

                    // Add headers
                    var headerFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 10);
                    string[] headers = { "#", "Product Name", "Code", "Barcode", "Category", "Price (EGP)", "Stock" };
                    foreach (var header in headers)
                    {
                        var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(header, headerFont))
                        {
                            BackgroundColor = new iTextSharp.text.BaseColor(220, 220, 220),
                            HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER,
                            Padding = 5
                        };
                        table.AddCell(cell);
                    }

                    // Add data
                    var dataFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 9);
                    int rowNum = 1;
                    foreach (var product in productList)
                    {
                        table.AddCell(new iTextSharp.text.Phrase(rowNum++.ToString(), dataFont));
                        table.AddCell(new iTextSharp.text.Phrase(product.Name ?? "", dataFont));
                        table.AddCell(new iTextSharp.text.Phrase(product.Code ?? "-", dataFont));
                        table.AddCell(new iTextSharp.text.Phrase(product.Barcode ?? "-", dataFont));
                        table.AddCell(new iTextSharp.text.Phrase(product.Category?.Name ?? "-", dataFont));
                        table.AddCell(new iTextSharp.text.Phrase(product.SalePrice?.ToString("N2") ?? "-", dataFont));
                        table.AddCell(new iTextSharp.text.Phrase(product.TotalQuantity.ToString(), dataFont));
                    }

                    document.Add(table);
                    document.Close();
                    writer.Close();

                    var fileName = $"Products_{AppClock.UtcNow:yyyyMMdd_HHmmss}.pdf";
                    return File(ms.ToArray(), "application/pdf", fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"حدث خطأ أثناء تصدير PDF: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// تصدير قائمة المنتجات إلى Excel
        /// </summary>
        public async Task<IActionResult> ExportToExcel(string searchString, int? categoryId)
        {
            var products = _context.Products
                .Include(p => p.Supplier)
                .Include(p => p.Category)
                .Include(p => p.Inventories)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                products = products.Where(p =>
                    p.Name.Contains(searchString) ||
                    (p.Code != null && p.Code.Contains(searchString)) ||
                    (p.Barcode != null && p.Barcode.Contains(searchString)));
            }

            if (categoryId.HasValue)
            {
                products = products.Where(p => p.CategoryId == categoryId);
            }

            var productList = await products.OrderBy(p => p.Name).ToListAsync();

            try
            {
                OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

                using (var package = new OfficeOpenXml.ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Products");

                    // Add headers
                    worksheet.Cells[1, 1].Value = "#";
                    worksheet.Cells[1, 2].Value = "اسم المنتج";
                    worksheet.Cells[1, 3].Value = "الكود";
                    worksheet.Cells[1, 4].Value = "الباركود";
                    worksheet.Cells[1, 5].Value = "الفئة";
                    worksheet.Cells[1, 6].Value = "المورد";
                    worksheet.Cells[1, 7].Value = "سعر الشراء";
                    worksheet.Cells[1, 8].Value = "سعر البيع";
                    worksheet.Cells[1, 9].Value = "الكمية";
                    worksheet.Cells[1, 10].Value = "الوحدة";

                    // Style headers
                    using (var range = worksheet.Cells[1, 1, 1, 10])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                        range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    }

                    // Add data
                    int row = 2;
                    int num = 1;
                    foreach (var product in productList)
                    {
                        worksheet.Cells[row, 1].Value = num++;
                        worksheet.Cells[row, 2].Value = product.Name;
                        worksheet.Cells[row, 3].Value = product.Code ?? "-";
                        worksheet.Cells[row, 4].Value = product.Barcode ?? "-";
                        worksheet.Cells[row, 5].Value = product.Category?.Name ?? "-";
                        worksheet.Cells[row, 6].Value = product.Supplier?.Name ?? "-";
                        worksheet.Cells[row, 7].Value = product.Price ?? 0;
                        worksheet.Cells[row, 8].Value = product.SalePrice ?? 0;
                        worksheet.Cells[row, 9].Value = product.TotalQuantity;
                        worksheet.Cells[row, 10].Value = product.Unit ?? "-";
                        row++;
                    }

                    // Auto-fit columns
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                    // Add borders
                    using (var range = worksheet.Cells[1, 1, row - 1, 10])
                    {
                        range.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        range.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        range.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        range.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    }

                    var fileName = $"Products_{AppClock.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                    var fileBytes = package.GetAsByteArray();
                    return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"حدث خطأ أثناء تصدير Excel: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}