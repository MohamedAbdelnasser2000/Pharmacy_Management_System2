using IMS.Models;
using Microsoft.EntityFrameworkCore;

namespace IMS.Data
{
    public static class CoreDataSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // Apply pending migrations automatically (optional, dev friendly)
            if (context.Database.GetPendingMigrations().Any())
            {
                await context.Database.MigrateAsync();
            }

            // Seed Categories
            if (!await context.Categories.AnyAsync())
            {
                var categories = new List<Category>
                {
                    new Category { Name = "الإلكترونيات", Description = "أجهزة إلكترونية ومعدات تقنية", IsActive = true },
                    new Category { Name = "الملابس", Description = "ملابس رجالية ونسائية وأطفال", IsActive = true },
                    new Category { Name = "الطعام والشراب", Description = "منتجات غذائية ومشروبات", IsActive = true },
                    new Category { Name = "المنزل والحديقة", Description = "أدوات منزلية ومعدات حديقة", IsActive = true },
                    new Category { Name = "الكتب والوسائط", Description = "كتب ومجلات ووسائط متعددة", IsActive = true },
                    new Category { Name = "الرياضة واللياقة", Description = "معدات رياضية وأدوات لياقة بدنية", IsActive = true }
                };
                
                await context.Categories.AddRangeAsync(categories);
                await context.SaveChangesAsync();
            }

            // Seed Branch
            if (!await context.Branches.AnyAsync())
            {
                var mainBranch = new Branch
                {
                    Name = "Main Branch",
                    Code = "MAIN",
                    Location = "HQ"
                };
                await context.Branches.AddAsync(mainBranch);
                await context.SaveChangesAsync();

                // Seed Warehouse for main branch
                var mainWarehouse = new Warehouse
                {
                    Name = "Main Warehouse",
                    BranchId = mainBranch.Id,
                    Location = "Main Yard"
                };
                await context.Warehouses.AddAsync(mainWarehouse);
                await context.SaveChangesAsync();
            }
        }
    }
}