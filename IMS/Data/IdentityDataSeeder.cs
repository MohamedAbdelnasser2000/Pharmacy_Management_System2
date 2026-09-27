using IMS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace IMS.Data
{
    public static class IdentityDataSeeder
    {
        private static readonly string[] RoleNames = new[]
        {
            RoleNamesConstants.Admin,
            RoleNamesConstants.Accountant,
            RoleNamesConstants.WarehouseStaff,
            RoleNamesConstants.Pharmacist,
            RoleNamesConstants.Cashier,
            RoleNamesConstants.DataEntry
        };

        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            foreach (var roleName in RoleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            var adminEmail = "admin@ims.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FullName = "Administrator",
                    BranchName = "Main Branch"
                };

                var createResult = await userManager.CreateAsync(adminUser, "Admin123!");
                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException("Failed to create default administrator user." + string.Join(";", createResult.Errors.Select(e => e.Description)));
                }
            }

            if (!await userManager.IsInRoleAsync(adminUser, RoleNamesConstants.Admin))
            {
                await userManager.AddToRoleAsync(adminUser, RoleNamesConstants.Admin);
            }

            // Seed Accountant User
            var accountantEmail = "accountant@ims.com";
            var accountantUser = await userManager.FindByEmailAsync(accountantEmail);
            if (accountantUser == null)
            {
                accountantUser = new ApplicationUser
                {
                    UserName = accountantEmail,
                    Email = accountantEmail,
                    EmailConfirmed = true,
                    FullName = "Accountant User",
                    BranchName = "Main Branch"
                };

                var createResult = await userManager.CreateAsync(accountantUser, "Accountant123!");
                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException("Failed to create default accountant user." + string.Join(";", createResult.Errors.Select(e => e.Description)));
                }
            }

            if (!await userManager.IsInRoleAsync(accountantUser, RoleNamesConstants.Accountant))
            {
                await userManager.AddToRoleAsync(accountantUser, RoleNamesConstants.Accountant);
            }

            // Seed Warehouse Staff User
            var warehouseStaffEmail = "warehouse@ims.com";
            var warehouseStaffUser = await userManager.FindByEmailAsync(warehouseStaffEmail);
            if (warehouseStaffUser == null)
            {
                warehouseStaffUser = new ApplicationUser
                {
                    UserName = warehouseStaffEmail,
                    Email = warehouseStaffEmail,
                    EmailConfirmed = true,
                    FullName = "Warehouse Staff User",
                    BranchName = "Main Branch"
                };

                var createResult = await userManager.CreateAsync(warehouseStaffUser, "Warehouse123!");
                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException("Failed to create default warehouse staff user." + string.Join(";", createResult.Errors.Select(e => e.Description)));
                }
            }

            if (!await userManager.IsInRoleAsync(warehouseStaffUser, RoleNamesConstants.WarehouseStaff))
            {
                await userManager.AddToRoleAsync(warehouseStaffUser, RoleNamesConstants.WarehouseStaff);
            }

            // Seed Pharmacist User
            await SeedUserAsync(userManager, "pharmacist@ims.com", "Pharmacist123!", "Pharmacist User", RoleNamesConstants.Pharmacist);

            // Seed Cashier User
            await SeedUserAsync(userManager, "cashier@ims.com", "Cashier123!", "Cashier User", RoleNamesConstants.Cashier);

            // Seed DataEntry User
            await SeedUserAsync(userManager, "dataentry@ims.com", "DataEntry123!", "DataEntry User", RoleNamesConstants.DataEntry);
        }

        private static async Task SeedUserAsync(UserManager<ApplicationUser> userManager, string email, string password, string fullName, string role)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName,
                    BranchName = "Main Branch"
                };

                var createResult = await userManager.CreateAsync(user, password);
                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to create default {role} user." + string.Join(";", createResult.Errors.Select(e => e.Description)));
                }
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}