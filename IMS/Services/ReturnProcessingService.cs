using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IMS.Data;
using IMS.Models;
using Microsoft.EntityFrameworkCore;

namespace IMS.Services
{
    public sealed class ReturnProcessingService
    {
        public sealed class ReturnCreateItem
        {
            public int OrderItemId { get; init; }
            public int ProductId { get; init; }
            public int Quantity { get; init; }
            public string? ItemReason { get; init; }
            public string? Condition { get; init; }
            public bool RestockItem { get; init; }
        }

        private readonly ApplicationDbContext _context;

        public ReturnProcessingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Return> CreateAsync(int orderId, string returnType, string reason, string? notes, IEnumerable<ReturnCreateItem> items, string? processedBy)
        {
            var itemList = items?.Where(i => i != null).ToList() ?? new List<ReturnCreateItem>();
            if (!itemList.Any())
            {
                throw new InvalidOperationException("يجب إضافة منتج واحد على الأقل للمرتجع");
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                throw new InvalidOperationException("الطلب غير موجود");
            }

            var orderItemsLookup = order.OrderItems.ToDictionary(oi => oi.Id);
            if (itemList.Any(i => !orderItemsLookup.ContainsKey(i.OrderItemId)))
            {
                throw new InvalidOperationException("هناك عناصر لا تنتمي إلى الطلب المحدد");
            }

            var orderItemIds = itemList.Select(i => i.OrderItemId).Distinct().ToList();
            var existingReturned = await LoadExistingReturnQuantities(orderItemIds);

            var returnRecord = new Return
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                ReturnDate = AppClock.UtcNow,
                Status = ReturnStatusConstants.Pending,
                ReturnType = returnType,
                Reason = reason,
                Notes = notes,
                ProcessedBy = processedBy,
                ApprovedBy = null,
                ApprovedDate = null,
                RefundMethod = null,
                RefundDate = null,
                RefundedAmount = 0,
                TotalRefundAmount = 0
            };

            decimal totalRefund = 0;

            foreach (var item in itemList)
            {
                if (item.Quantity <= 0)
                {
                    throw new InvalidOperationException("الكمية المرتجعة يجب أن تكون أكبر من صفر");
                }

                var orderItem = orderItemsLookup[item.OrderItemId];
                if (orderItem.ProductId != item.ProductId)
                {
                    throw new InvalidOperationException("تم تمرير منتج غير متطابق مع عنصر الطلب");
                }

                var alreadyReturned = existingReturned.TryGetValue(orderItem.Id, out var value) ? value : 0;
                var remaining = orderItem.Quantity - alreadyReturned;
                if (remaining <= 0)
                {
                    throw new InvalidOperationException("تم إرجاع هذا المنتج بالكامل مسبقاً");
                }

                if (item.Quantity > remaining)
                {
                    throw new InvalidOperationException("الكمية المطلوبة تتجاوز المتاح للمرتجع لهذا المنتج");
                }

                var unitPrice = orderItem.UnitPrice;
                var discount = orderItem.DiscountPercentage;
                var gross = item.Quantity * unitPrice;
                var discountAmount = gross * (discount / 100m);
                var refundAmount = gross - discountAmount;

                existingReturned[orderItem.Id] = alreadyReturned + item.Quantity;

                returnRecord.ReturnItems.Add(new ReturnItem
                {
                    OrderItemId = orderItem.Id,
                    ProductId = orderItem.ProductId,
                    ReturnedQuantity = item.Quantity,
                    UnitPrice = unitPrice,
                    DiscountPercentage = discount,
                    RefundAmount = refundAmount,
                    ItemReason = item.ItemReason,
                    Condition = item.Condition,
                    RestockItem = item.RestockItem
                });

                totalRefund += refundAmount;
            }

            var determinedType = DetermineReturnType(order, existingReturned);
            if (!string.Equals(determinedType, returnType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("نوع المرتجع لا يتوافق مع الكميات المحددة");
            }

            returnRecord.ReturnType = determinedType;
            returnRecord.TotalRefundAmount = totalRefund;

            await _context.Returns.AddAsync(returnRecord);
            await _context.SaveChangesAsync();

            return returnRecord;
        }

        public async Task ApproveAsync(int returnId, string? approvedBy)
        {
            var returnRecord = await _context.Returns
                .Include(r => r.ReturnItems)
                .FirstOrDefaultAsync(r => r.Id == returnId);

            if (returnRecord == null)
            {
                throw new InvalidOperationException("المرتجع غير موجود");
            }

            if (returnRecord.Status != ReturnStatusConstants.Pending)
            {
                throw new InvalidOperationException("لا يمكن الموافقة على مرتجع في هذه الحالة");
            }

            var timestamp = AppClock.UtcNow;
            var referenceNumber = $"ORD-{returnRecord.OrderId}";

            var originalMovements = await _context.StockMovements
                .Where(sm => sm.ReferenceNumber == referenceNumber && sm.MovementType == "إخراج")
                .OrderBy(sm => sm.MovementDate)
                .ToListAsync();

            var allocationLookup = BuildAllocationLookup(originalMovements);

            var allocationPairs = allocationLookup
                .SelectMany(kvp => kvp.Value.Select(a => (kvp.Key, a.WarehouseId)))
                .Distinct()
                .ToList();

            var inventoryLookup = new Dictionary<(int ProductId, int WarehouseId), ProductInventory>();
            if (allocationPairs.Any())
            {
                var productIds = allocationPairs.Select(p => p.Key).Distinct().ToList();
                var warehouseIds = allocationPairs.Select(p => p.Item2).Distinct().ToList();
                var existingInventories = await _context.ProductInventories
                    .Where(pi => productIds.Contains(pi.ProductId) && warehouseIds.Contains(pi.WarehouseId))
                    .ToListAsync();

                foreach (var inventory in existingInventories)
                {
                    inventoryLookup[(inventory.ProductId, inventory.WarehouseId)] = inventory;
                }
            }

            var stockMovements = new List<StockMovement>();
            Warehouse? fallbackWarehouse = null;

            foreach (var item in returnRecord.ReturnItems)
            {
                if (!item.RestockItem)
                {
                    continue;
                }

                var remainingQuantity = (decimal)item.ReturnedQuantity;

                if (allocationLookup.TryGetValue(item.ProductId, out var allocations))
                {
                    foreach (var allocation in allocations)
                    {
                        if (remainingQuantity <= 0)
                        {
                            break;
                        }

                        if (allocation.RemainingQuantity <= 0)
                        {
                            continue;
                        }

                        var quantityToRestore = Math.Min(remainingQuantity, allocation.RemainingQuantity);
                        var inventory = await GetOrCreateInventory(inventoryLookup, item.ProductId, allocation.WarehouseId, timestamp);

                        inventory.OnHandQuantity += quantityToRestore;
                        inventory.LastUpdated = timestamp;

                        allocation.RemainingQuantity -= quantityToRestore;
                        remainingQuantity -= quantityToRestore;

                        stockMovements.Add(new StockMovement
                        {
                            ProductId = item.ProductId,
                            WarehouseId = allocation.WarehouseId,
                            MovementType = "إدخال",
                            Quantity = quantityToRestore,
                            MovementDate = timestamp,
                            Reason = "مرتجع من عميل",
                            ReferenceNumber = $"RET-{returnRecord.Id}"
                        });
                    }
                }

                if (remainingQuantity > 0)
                {
                    if (fallbackWarehouse == null)
                    {
                        fallbackWarehouse = await _context.Warehouses
                            .OrderBy(w => w.Id)
                            .FirstOrDefaultAsync();

                        if (fallbackWarehouse == null)
                        {
                            throw new InvalidOperationException("لا يوجد مستودع متاح لإرجاع الكميات المتبقية");
                        }
                    }

                    var inventory = await GetOrCreateInventory(inventoryLookup, item.ProductId, fallbackWarehouse.Id, timestamp);
                    inventory.OnHandQuantity += remainingQuantity;
                    inventory.LastUpdated = timestamp;

                    stockMovements.Add(new StockMovement
                    {
                        ProductId = item.ProductId,
                        WarehouseId = fallbackWarehouse.Id,
                        MovementType = "إدخال",
                        Quantity = remainingQuantity,
                        MovementDate = timestamp,
                        Reason = "مرتجع من عميل",
                        ReferenceNumber = $"RET-{returnRecord.Id}"
                    });
                }
            }

            if (stockMovements.Any())
            {
                await _context.StockMovements.AddRangeAsync(stockMovements);
            }

            returnRecord.Status = returnRecord.RefundedAmount >= returnRecord.TotalRefundAmount && returnRecord.TotalRefundAmount > 0
                ? ReturnStatusConstants.Completed
                : ReturnStatusConstants.Approved;
            returnRecord.ApprovedBy = approvedBy;
            returnRecord.ApprovedDate = timestamp;

            await _context.SaveChangesAsync();
        }

        public async Task RejectAsync(int returnId, string? approvedBy, string rejectionReason)
        {
            var returnRecord = await _context.Returns.FindAsync(returnId);
            if (returnRecord == null)
            {
                throw new InvalidOperationException("المرتجع غير موجود");
            }

            if (returnRecord.Status != ReturnStatusConstants.Pending)
            {
                throw new InvalidOperationException("لا يمكن رفض مرتجع في هذه الحالة");
            }

            returnRecord.Status = ReturnStatusConstants.Rejected;
            returnRecord.ApprovedBy = approvedBy;
            returnRecord.ApprovedDate = AppClock.UtcNow;
            returnRecord.Notes = string.IsNullOrWhiteSpace(returnRecord.Notes)
                ? $"سبب الرفض: {rejectionReason}"
                : $"{returnRecord.Notes}\nسبب الرفض: {rejectionReason}";

            await _context.SaveChangesAsync();
        }

        public async Task ProcessRefundAsync(int returnId, decimal refundAmount, string refundMethod)
        {
            if (refundAmount <= 0)
            {
                throw new InvalidOperationException("المبلغ المسترد يجب أن يكون أكبر من صفر");
            }

            var returnRecord = await _context.Returns.FindAsync(returnId);
            if (returnRecord == null)
            {
                throw new InvalidOperationException("المرتجع غير موجود");
            }

            if (returnRecord.Status != ReturnStatusConstants.Approved && returnRecord.Status != ReturnStatusConstants.Completed)
            {
                throw new InvalidOperationException("لا يمكن معالجة الاسترداد قبل الموافقة على المرتجع");
            }

            var outstanding = returnRecord.TotalRefundAmount - returnRecord.RefundedAmount;
            if (refundAmount > outstanding)
            {
                throw new InvalidOperationException("المبلغ المسترد يتجاوز المتبقي");
            }

            returnRecord.RefundedAmount += refundAmount;
            returnRecord.RefundMethod = refundMethod;
            returnRecord.RefundDate = AppClock.UtcNow;

            if (returnRecord.RefundedAmount >= returnRecord.TotalRefundAmount)
            {
                returnRecord.Status = ReturnStatusConstants.Completed;
            }

            await _context.SaveChangesAsync();
        }

        private async Task<ProductInventory> GetOrCreateInventory(IDictionary<(int ProductId, int WarehouseId), ProductInventory> cache, int productId, int warehouseId, DateTime timestamp)
        {
            var key = (productId, warehouseId);
            if (cache.TryGetValue(key, out var inventory))
            {
                return inventory;
            }

            inventory = new ProductInventory
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                OnHandQuantity = 0,
                ReservedQuantity = 0,
                ReorderPoint = 0,
                LastUpdated = timestamp
            };

            await _context.ProductInventories.AddAsync(inventory);
            cache[key] = inventory;
            return inventory;
        }

        private static Dictionary<int, List<MovementAllocation>> BuildAllocationLookup(IEnumerable<StockMovement> movements)
        {
            return movements
                .GroupBy(sm => sm.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(sm => new MovementAllocation(sm.WarehouseId, sm.Quantity)).ToList());
        }

        private async Task<Dictionary<int, int>> LoadExistingReturnQuantities(IEnumerable<int> orderItemIds)
        {
            var ids = orderItemIds.Distinct().ToList();
            if (!ids.Any())
            {
                return new Dictionary<int, int>();
            }

            var entries = await _context.ReturnItems
                .Where(ri => ri.OrderItemId != null && ids.Contains(ri.OrderItemId.Value))
                .Include(ri => ri.Return)
                .Where(ri => ri.Return.Status != ReturnStatusConstants.Rejected)
                .ToListAsync();

            return entries
                .GroupBy(ri => ri.OrderItemId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(ri => ri.ReturnedQuantity));
        }

        private static string DetermineReturnType(Order order, IReadOnlyDictionary<int, int> totalReturned)
        {
            foreach (var orderItem in order.OrderItems)
            {
                var returned = totalReturned.TryGetValue(orderItem.Id, out var value) ? value : 0;
                if (returned < orderItem.Quantity)
                {
                    return ReturnTypeConstants.Partial;
                }
            }

            return ReturnTypeConstants.Full;
        }

        private sealed class MovementAllocation
        {
            public MovementAllocation(int warehouseId, decimal quantity)
            {
                WarehouseId = warehouseId;
                RemainingQuantity = quantity;
            }

            public int WarehouseId { get; }
            public decimal RemainingQuantity { get; set; }
        }
    }
}
