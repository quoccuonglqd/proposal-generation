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
            var accounting = await GetOrCreateService(context, null, ServiceLevel.Main, "Accounting & Tax", 2);
            
            var incorporation = await GetOrCreateService(context, entitySetup.Id, ServiceLevel.Sub, "Company Incorporation", 1);
            var workPermit = await GetOrCreateService(context, entitySetup.Id, ServiceLevel.Sub, "Work Permit", 2);

            if (vnRegion != null)
            {
                await EnsurePrice(context, incorporation.Id, vnRegion.Id, 25000000, 1000);
                await EnsurePrice(context, workPermit.Id, vnRegion.Id, 12000000, 500);
                await EnsurePrice(context, accounting.Id, vnRegion.Id, 5000000, 200);
            }
            if (thRegion != null)
            {
                await EnsurePrice(context, incorporation.Id, thRegion.Id, 35000, 1000);
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
                    
                    proposal.Sections.Add(new ProposalSection 
                    { 
                        SectionKey = "Executive_Summary", 
                        Order = 1, 
                        ContentJson = "{\"en\": \"This is a seeded executive summary for the demo client.\"}" 
                    });
                    proposal.Sections.Add(new ProposalSection 
                    { 
                        SectionKey = "Regional_Pricing", 
                        Order = 2, 
                        ContentJson = "{\"en\": \"Pricing overview for Vietnam region.\"}" 
                    });
                    proposal.Sections.Add(new ProposalSection 
                    { 
                        SectionKey = "Next_Steps", 
                        Order = 3, 
                        ContentJson = "{\"en\": \"Contact your relationship manager to proceed.\"}" 
                    });

                    context.Proposals.Add(proposal);
                    await context.SaveChangesAsync();
                    Console.WriteLine("+++ Seeded Demo Proposal with default sections.");
                }
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

        private static async Task<Service> GetOrCreateService(CherryDbContext context, Guid? parentId, ServiceLevel level, string name, int order)
        {
            var s = await context.Services.FirstOrDefaultAsync(x => x.Name == name && x.ParentId == parentId);
            if (s == null)
            {
                s = new Service { ParentId = parentId, Level = level, Name = name, SortOrder = order, IsActive = true };
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
