using Cherry.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;

namespace Cherry.Infrastructure.Data
{
    public class CherryDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
    {
        public CherryDbContext(DbContextOptions<CherryDbContext> options) : base(options)
        {
        }

        public DbSet<Region> Regions { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<ServicePrice> ServicePrices { get; set; }
        public DbSet<ExchangeRateRule> ExchangeRateRules { get; set; }
        public DbSet<Proposal> Proposals { get; set; }
        public DbSet<ProposalSection> ProposalSections { get; set; }
        public DbSet<ProposalServiceSelection> ProposalServiceSelections { get; set; }
        public DbSet<ProposalVersion> ProposalVersions { get; set; }
        public DbSet<ProposalArtifact> ProposalArtifacts { get; set; }
        public DbSet<Template> Templates { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Service>(entity =>
            {
                entity.HasOne(s => s.Parent)
                    .WithMany(s => s.Children)
                    .HasForeignKey(s => s.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ServicePrice>(entity =>
            {
                entity.HasOne(sp => sp.Service)
                    .WithMany(s => s.Prices)
                    .HasForeignKey(sp => sp.ServiceId);

                entity.HasOne(sp => sp.Region)
                    .WithMany()
                    .HasForeignKey(sp => sp.RegionId);

                entity.Property(sp => sp.LocalPrice).HasPrecision(18, 2);
                entity.Property(sp => sp.UsdReferencePrice).HasPrecision(18, 2);
            });

            builder.Entity<ExchangeRateRule>(entity =>
            {
                entity.Property(e => e.Rate).HasPrecision(18, 6);
            });

            builder.Entity<Proposal>(entity =>
            {
                entity.HasOne(p => p.Region)
                    .WithMany()
                    .HasForeignKey(p => p.RegionId);
            });

            builder.Entity<ProposalSection>(entity =>
            {
                entity.HasOne(ps => ps.Proposal)
                    .WithMany(p => p.Sections)
                    .HasForeignKey(ps => ps.ProposalId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ProposalServiceSelection>(entity =>
            {
                entity.HasOne(pss => pss.Proposal)
                    .WithMany(p => p.ServiceSelections)
                    .HasForeignKey(pss => pss.ProposalId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(pss => pss.Service)
                    .WithMany()
                    .HasForeignKey(pss => pss.ServiceId);

                entity.Property(pss => pss.Quantity).HasPrecision(18, 2);
            });

            builder.Entity<ProposalVersion>(entity =>
            {
                entity.HasOne(pv => pv.Proposal)
                    .WithMany(p => p.Versions)
                    .HasForeignKey(pv => pv.ProposalId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(pv => pv.ExchangeRateUsed).HasPrecision(18, 6);
            });

            builder.Entity<ProposalArtifact>(entity =>
            {
                entity.HasOne(pa => pa.ProposalVersion)
                    .WithMany(pv => pv.Artifacts)
                    .HasForeignKey(pa => pa.ProposalVersionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
