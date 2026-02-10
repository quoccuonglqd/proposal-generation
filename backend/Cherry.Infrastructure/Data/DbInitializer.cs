using Cherry.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Cherry.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(CherryDbContext context, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, IConfiguration configuration)
        {
            // Seed Roles
            var roles = new[] { "Admin", "ProposalCreator", "Viewer" };
            foreach (var roleName in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new ApplicationRole(roleName));
                }
            }


            // Seed Admin User
            await EnsureUser(userManager, 
                configuration["SeedData:Admin:Email"] ?? "admin@cherry.com", 
                configuration["SeedData:Admin:Password"] ?? "Admin123!", 
                configuration["SeedData:Admin:DisplayName"] ?? "System Administrator", 
                "Admin");

            // Seed Proposal Creator User
            await EnsureUser(userManager, 
                configuration["SeedData:Creator:Email"] ?? "creator@cherry.com", 
                configuration["SeedData:Creator:Password"] ?? "Creator123!", 
                configuration["SeedData:Creator:DisplayName"] ?? "Proposal Creator", 
                "ProposalCreator");

            // Seed Viewer User
            await EnsureUser(userManager, 
                configuration["SeedData:Viewer:Email"] ?? "viewer@cherry.com", 
                configuration["SeedData:Viewer:Password"] ?? "Viewer123!", 
                configuration["SeedData:Viewer:DisplayName"] ?? "Guest Viewer", 
                "Viewer");

            // Seed initial Region
            if (!context.Regions.Any())
            {
                var vn = new Region { Code = "VN", Name = "Vietnam", LocalCurrency = "VND" };
                var th = new Region { Code = "TH", Name = "Thailand", LocalCurrency = "THB" };
                context.Regions.AddRange(vn, th);
                await context.SaveChangesAsync();
            }

            var vnRegion = await context.Regions.FirstOrDefaultAsync(r => r.Code == "VN");
            var thRegion = await context.Regions.FirstOrDefaultAsync(r => r.Code == "TH");

            // Seed Services and Prices with "Upsert" logic
            var entitySetup = await GetOrCreateService(context, null, ServiceLevel.Main, "Entity Setup", 1);
            var accounting = await GetOrCreateService(context, null, ServiceLevel.Main, "Accounting & Tax", 2, "Month");
            
            var incorporation = await GetOrCreateService(context, entitySetup.Id, ServiceLevel.Sub, "Company Incorporation", 1);
            var workPermit = await GetOrCreateService(context, entitySetup.Id, ServiceLevel.Sub, "Work Permit", 2);

            var incBasic = await GetOrCreateService(context, incorporation.Id, ServiceLevel.LineItem, "Incorporation Basic", 1, "Project");
            var incPremium = await GetOrCreateService(context, incorporation.Id, ServiceLevel.LineItem, "Incorporation Premium", 2, "Project");

            if (vnRegion != null)
            {
                await EnsurePrice(context, incBasic.Id, vnRegion.Id, 25000000, 1000);
                await EnsurePrice(context, incPremium.Id, vnRegion.Id, 45000000, 1800);
                await EnsurePrice(context, workPermit.Id, vnRegion.Id, 12000000, 500);
                await EnsurePrice(context, accounting.Id, vnRegion.Id, 5000000, 200);
            }
            if (thRegion != null)
            {
                await EnsurePrice(context, incBasic.Id, thRegion.Id, 35000, 1000);
                await EnsurePrice(context, accounting.Id, thRegion.Id, 8000, 250);
            }

            await context.SaveChangesAsync();

            // Seed Template
            if (!context.Templates.Any())
            {
                var template = new Template
                {
                    Name = "Standard Proposal v1",
                    Version = 1,
                    StorageKey = "templates/standard_v1.pptx",
                    Status = TemplateStatus.Active,
                    Category = "Standard",
                    RegionId = vnRegion?.Id
                };
                context.Templates.Add(template);

                var storagePath = Path.Combine(Directory.GetCurrentDirectory(), "Storage", "templates");
                if (!Directory.Exists(storagePath)) Directory.CreateDirectory(storagePath);
                var filePath = Path.Combine(storagePath, "standard_v1.pptx");
                if (!File.Exists(filePath))
                {
                    File.WriteAllText(filePath, "DUMMY PPTX CONTENT");
                }
                await context.SaveChangesAsync();
            }

            // Seed a Demo Proposal for the Admin
            var adminUser = await userManager.FindByEmailAsync(configuration["SeedData:Admin:Email"] ?? "admin@cherry.com");
            if (adminUser != null && vnRegion != null)
            {
                var existingProposal = await context.Proposals.AnyAsync(p => p.ClientName == "Demo Corporate Client");
                if (!existingProposal)
                {
                    var proposal = new Proposal
                    {
                        ClientName = "Demo Corporate Client",
                        RegionId = vnRegion.Id,
                        CreatedBy = adminUser.Id.ToString(),
                        DefaultLanguage = "en",
                        Status = ProposalStatus.Draft,
                        UpdatedAt = DateTime.UtcNow
                    };
                    

                    context.Proposals.Add(proposal);
                    await context.SaveChangesAsync();
                    Console.WriteLine("+++ Seeded Demo Proposal with default sections.");
                }
            }

            // Seed Master Sections
            if (!context.MasterSections.Any())
            {
                context.MasterSections.AddRange(
                    new MasterSection { 
                        SectionKey = "Executive_Summary", 
                        Name = "Executive Summary", 
                        SortOrder = 1, 
                        DefaultContentJson = "{\"en\": [{\"title\": \"Executive Summary\", \"content\": \"{\\\"type\\\":\\\"doc\\\",\\\"content\\\":[{\\\"type\\\":\\\"paragraph\\\",\\\"content\\\":[{\\\"type\\\":\\\"text\\\",\\\"text\\\":\\\"Our proposal is \\\"},{\\\"type\\\":\\\"text\\\",\\\"marks\\\":[{\\\"type\\\":\\\"bold\\\"}],\\\"text\\\":\\\"tailored\\\"},{\\\"type\\\":\\\"text\\\",\\\"text\\\":\\\" to your needs.\\\"}]},{\\\"type\\\":\\\"bulletList\\\",\\\"content\\\":[{\\\"type\\\":\\\"listItem\\\",\\\"content\\\":[{\\\"type\\\":\\\"paragraph\\\",\\\"content\\\":[{\\\"type\\\":\\\"text\\\",\\\"text\\\":\\\"Innovative solutions\\\"}]}]},{\\\"type\\\":\\\"listItem\\\",\\\"content\\\":[{\\\"type\\\":\\\"paragraph\\\",\\\"content\\\":[{\\\"type\\\":\\\"text\\\",\\\"text\\\":\\\"Expert team\\\"}]}]}]}]}\", \"backgroundAssetId\": \"assets/bg_exec_summary.jpg\"}], \"vn\": [{\"title\": \"Tóm tắt điều hành\", \"content\": \"{\\\"type\\\":\\\"doc\\\",\\\"content\\\":[{\\\"type\\\":\\\"paragraph\\\",\\\"content\\\":[{\\\"type\\\":\\\"text\\\",\\\"text\\\":\\\"Đề xuất của chúng tôi được \\\"},{\\\"type\\\":\\\"text\\\",\\\"marks\\\":[{\\\"type\\\":\\\"bold\\\"}],\\\"text\\\":\\\"tùy chỉnh\\\"},{\\\"type\\\":\\\"text\\\",\\\"text\\\":\\\" cho nhu cầu của bạn.\\\"}]},{\\\"type\\\":\\\"bulletList\\\",\\\"content\\\":[{\\\"type\\\":\\\"listItem\\\",\\\"content\\\":[{\\\"type\\\":\\\"paragraph\\\",\\\"content\\\":[{\\\"type\\\":\\\"text\\\",\\\"text\\\":\\\"Giải pháp sáng tạo\\\"}]}]},{\\\"type\\\":\\\"listItem\\\",\\\"content\\\":[{\\\"type\\\":\\\"paragraph\\\",\\\"content\\\":[{\\\"type\\\":\\\"text\\\",\\\"text\\\":\\\"Đội ngũ chuyên gia\\\"}]}]}]}]}\", \"backgroundAssetId\": \"assets/bg_exec_summary.jpg\"}]}" 
                    },
                    new MasterSection { 
                        SectionKey = "Regional_Pricing", 
                        Name = "Regional Pricing", 
                        SortOrder = 2, 
                        DefaultContentJson = "{\"en\": [{\"title\": \"Regional Pricing\", \"content\": \"Pricing details for the region...\"}], \"vn\": [{\"title\": \"Chi tiết giá\", \"content\": \"Chi tiết giá cho khu vực...\"}]}" 
                    },
                    new MasterSection { 
                        SectionKey = "Next_Steps", 
                        Name = "Next Steps", 
                        SortOrder = 3, 
                        DefaultContentJson = "{\"en\": [{\"title\": \"Next Steps\", \"content\": \"Contact your relationship manager to proceed.\"}], \"vn\": [{\"title\": \"Các bước tiếp theo\", \"content\": \"Liên hệ với người quản lý quan hệ của bạn để tiếp tục.\"}]}" 
                    }
                );
                await context.SaveChangesAsync();
            }
        }

        private static async Task EnsureUser(UserManager<ApplicationUser> userManager, string email, string password, string displayName, string role)
        {
            if (await userManager.FindByEmailAsync(email) == null)
            {
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    DisplayName = displayName,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }
        }

        private static async Task<Service> GetOrCreateService(CherryDbContext context, Guid? parentId, ServiceLevel level, string name, int order, string? unit = null)
        {
            var s = await context.Services.FirstOrDefaultAsync(x => x.Name == name && x.ParentId == parentId);
            if (s == null)
            {
                s = new Service { ParentId = parentId, Level = level, Name = name, SortOrder = order, Unit = unit, IsActive = true };
                context.Services.Add(s);
                await context.SaveChangesAsync();
            }
            return s;
        }

        private static async Task EnsurePrice(CherryDbContext context, Guid serviceId, Guid regionId, decimal local, decimal usd)
        {
            var exists = await context.ServicePrices.AnyAsync(p => p.ServiceId == serviceId && p.RegionId == regionId && p.Status == "Active");
            if (!exists)
            {
                context.ServicePrices.Add(new ServicePrice
                {
                    ServiceId = serviceId,
                    RegionId = regionId,
                    LocalPrice = local,
                    UsdReferencePrice = usd,
                    Status = "Active",
                    EffectiveFrom = DateTime.UtcNow
                });
            }
        }
    }
}
