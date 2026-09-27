using System;
using System.Linq;
using System.Threading.Tasks;
using IMS.Data;
using IMS.Models;
using Microsoft.EntityFrameworkCore;

namespace IMS.Services
{
    public class InventoryService
    {
        private readonly ApplicationDbContext _context;

        public InventoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ProductInventory> AdjustStockAsync(int productId, int warehouseId, decimal quantityDelta, string reason)
        {
            var inventory = await _context.ProductInventories
                .FirstOrDefaultAsync(pi => pi.ProductId == productId && pi.WarehouseId == warehouseId);

            if (inventory == null)
            {
                inventory = new ProductInventory
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    OnHandQuantity = 0,
                    ReservedQuantity = 0,
                    ReorderPoint = 0,
                    LastUpdated = AppClock.UtcNow
                };
                await _context.ProductInventories.AddAsync(inventory);
            }

            inventory.OnHandQuantity += quantityDelta;
            inventory.LastUpdated = AppClock.UtcNow;

            var movement = new StockMovement
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                Quantity = quantityDelta,
                MovementType = quantityDelta >= 0 ? "IN" : "OUT",
                MovementDate = AppClock.UtcNow,
                Reason = reason
            };

            await _context.StockMovements.AddAsync(movement);
            await _context.SaveChangesAsync();

            return inventory;
        }
    }
}
