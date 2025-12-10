using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<SkillsCatalog> SkillsCatalog { get; set; }
    public DbSet<UserSkill> UserSkills { get; set; }
    public DbSet<Education> Educations { get; set; }
    public DbSet<Proof> Proofs { get; set; }
    public DbSet<SkillOffer> SkillOffers { get; set; }
    public DbSet<SkillRequest> SkillRequests { get; set; }
    public DbSet<VerificationRequest> VerificationRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Account configuration
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountID);
            entity.Property(e => e.Login).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.TelegramID).HasMaxLength(50);
            entity.Property(e => e.IsAdmin).HasDefaultValue(false);
            entity.HasIndex(e => e.Login).IsUnique();
        });

        // UserProfile configuration
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.AccountID);
            entity.Property(e => e.FullName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.PhotoURL).HasMaxLength(255);
            entity.Property(e => e.ContactInfo).HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(3000);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(e => e.Account)
                .WithOne(e => e.UserProfile)
                .HasForeignKey<UserProfile>(e => e.AccountID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SkillsCatalog configuration
        modelBuilder.Entity<SkillsCatalog>(entity =>
        {
            entity.HasKey(e => e.SkillID);
            entity.Property(e => e.SkillName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Epithet).IsRequired();
            entity.HasIndex(e => e.SkillName).IsUnique();
        });

        // UserSkill configuration
        modelBuilder.Entity<UserSkill>(entity =>
        {
            entity.HasKey(e => new { e.AccountID, e.SkillID });
            entity.Property(e => e.SkillLevel).IsRequired();
            entity.Property(e => e.IsVerified).HasDefaultValue(false);

            entity.HasOne(e => e.Account)
                .WithMany(e => e.UserSkills)
                .HasForeignKey(e => e.AccountID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SkillsCatalog)
                .WithMany(e => e.UserSkills)
                .HasForeignKey(e => e.SkillID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Education configuration
        modelBuilder.Entity<Education>(entity =>
        {
            entity.HasKey(e => e.EducationID);
            entity.Property(e => e.InstitutionName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.DegreeField).HasMaxLength(100);

            entity.HasOne(e => e.Account)
                .WithMany(e => e.Educations)
                .HasForeignKey(e => e.AccountID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Proof configuration
        modelBuilder.Entity<Proof>(entity =>
        {
            entity.HasKey(e => e.ProofID);
            entity.Property(e => e.FileURL).HasMaxLength(255).IsRequired();
            entity.Property(e => e.IsVerified).HasDefaultValue(false);

            entity.HasOne(e => e.Account)
                .WithMany(e => e.Proofs)
                .HasForeignKey(e => e.AccountID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SkillsCatalog)
                .WithMany()
                .HasForeignKey(e => e.SkillID)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // SkillOffer configuration
        modelBuilder.Entity<SkillOffer>(entity =>
        {
            entity.HasKey(e => e.OfferID);
            entity.Property(e => e.Title).HasMaxLength(100).IsRequired();

            entity.HasOne(e => e.Account)
                .WithMany(e => e.SkillOffers)
                .HasForeignKey(e => e.AccountID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SkillsCatalog)
                .WithMany(e => e.SkillOffers)
                .HasForeignKey(e => e.SkillID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // SkillRequest configuration
        modelBuilder.Entity<SkillRequest>(entity =>
        {
            entity.HasKey(e => e.RequestID);
            entity.Property(e => e.Title).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Status).IsRequired();

            entity.HasOne(e => e.Account)
                .WithMany(e => e.SkillRequests)
                .HasForeignKey(e => e.AccountID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SkillsCatalog)
                .WithMany(e => e.SkillRequests)
                .HasForeignKey(e => e.SkillID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // VerificationRequest configuration
        modelBuilder.Entity<VerificationRequest>(entity =>
        {
            entity.HasKey(e => e.RequestID);
            entity.Property(e => e.RequestType).IsRequired();
            entity.Property(e => e.Status).IsRequired();

            entity.HasOne(e => e.Account)
                .WithMany(e => e.VerificationRequests)
                .HasForeignKey(e => e.AccountID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Proof)
                .WithMany(e => e.VerificationRequests)
                .HasForeignKey(e => e.ProofID)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}


