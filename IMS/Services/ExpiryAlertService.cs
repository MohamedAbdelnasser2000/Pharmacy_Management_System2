using System;
using System.Threading;
using System.Threading.Tasks;
using IMS.Data;
using IMS.Models;
using Microsoft.EntityFrameworkCore;

namespace IMS.Services
{
    public class ExpiryAlertService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public ExpiryAlertService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<int> GenerateAlertsAsync(CancellationToken cancellationToken = default)
        {
            var inventories = await _context.ProductInventories
                .Include(i => i.Product)
                .Include(i => i.Warehouse)
                .Where(i => i.NextExpiryDate.HasValue && i.OnHandQuantity > 0)
                .ToListAsync(cancellationToken);

            var addedCount = 0;
            var now = AppClock.UtcNow;

            foreach (var inventory in inventories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var expiryDate = inventory.NextExpiryDate!.Value;
                var daysToExpiry = (expiryDate - now).Days;

                string alertStatus;
                if (daysToExpiry < 0)
                {
                    alertStatus = "منتهي";
                }
                else if (daysToExpiry <= 30)
                {
                    alertStatus = "قريب من الانتهاء";
                }
                else if (daysToExpiry <= 90)
                {
                    alertStatus = "تحذير";
                }
                else
                {
                    continue;
                }

                var exists = await _context.ExpiryAlerts
                    .AnyAsync(e => e.ProductId == inventory.ProductId &&
                                   e.WarehouseId == inventory.WarehouseId &&
                                   e.ExpiryDate == expiryDate &&
                                   !e.IsHandled,
                        cancellationToken);

                if (exists)
                {
                    continue;
                }

                var quantity = inventory.OnHandQuantity > 0 ? (int)Math.Ceiling(inventory.OnHandQuantity) : 0;
                if (quantity == 0)
                {
                    continue;
                }

                var alert = new ExpiryAlert
                {
                    ProductId = inventory.ProductId,
                    WarehouseId = inventory.WarehouseId,
                    ExpiryDate = expiryDate,
                    Quantity = quantity,
                    AlertStatus = alertStatus,
                    AlertDate = now,
                    IsHandled = false
                };

                _context.ExpiryAlerts.Add(alert);
                addedCount++;
            }

            if (addedCount > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
                await _notificationService.CreateNotificationAsync(
                    "تنبيهات صلاحية جديدة",
                    $"تم إنشاء {addedCount} تنبيهات جديدة للأدوية منتهية الصلاحية أو قريبة الانتهاء.",
                    "Warning",
                    null,
                    "/Reports/ExpiryReport"
                );
            }

            return addedCount;
        }
    }
}
