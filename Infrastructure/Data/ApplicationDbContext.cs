using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
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
    public DbSet<Domain.Entities.Application> Applications { get; set; }
    public DbSet<Deal> Deals { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Notification> Notifications { get; set; }

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
            entity.Property(e => e.TelegramLinkToken).HasMaxLength(20);
            entity.Property(e => e.IsAdmin).HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.NotificationsEnabled).HasDefaultValue(true);
            entity.Property(e => e.FailedLoginAttempts).HasDefaultValue(0);
            entity.HasIndex(e => e.Login).IsUnique();
            entity.HasIndex(e => e.TelegramLinkToken).IsUnique().HasFilter("\"TelegramLinkToken\" IS NOT NULL");
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
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.LearnedAt).HasMaxLength(150);
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
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Account)
                .WithMany(e => e.VerificationRequests)
                .HasForeignKey(e => e.AccountID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Proof)
                .WithMany(e => e.VerificationRequests)
                .HasForeignKey(e => e.ProofID)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Application (отклик) configuration
        modelBuilder.Entity<Domain.Entities.Application>(entity =>
        {
            entity.HasKey(e => e.ApplicationID);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Applicant)
                .WithMany(e => e.Applications)
                .HasForeignKey(e => e.ApplicantID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SkillOffer)
                .WithMany(e => e.Applications)
                .HasForeignKey(e => e.OfferID)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.SkillRequest)
                .WithMany(e => e.Applications)
                .HasForeignKey(e => e.SkillRequestID)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Deal)
                .WithOne(e => e.Application)
                .HasForeignKey<Deal>(e => e.ApplicationID)
                .OnDelete(DeleteBehavior.Restrict);

            // Partial unique indexes: one application per applicant per offer/request
            entity.HasIndex(e => new { e.ApplicantID, e.OfferID })
                .IsUnique()
                .HasFilter("\"OfferID\" IS NOT NULL");

            entity.HasIndex(e => new { e.ApplicantID, e.SkillRequestID })
                .IsUnique()
                .HasFilter("\"SkillRequestID\" IS NOT NULL");
        });

        // Deal configuration
        modelBuilder.Entity<Deal>(entity =>
        {
            entity.HasKey(e => e.DealID);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Initiator)
                .WithMany(e => e.InitiatedDeals)
                .HasForeignKey(e => e.InitiatorID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Partner)
                .WithMany(e => e.PartnerDeals)
                .HasForeignKey(e => e.PartnerID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Review configuration
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.ReviewID);
            entity.Property(e => e.Rating).IsRequired();
            entity.Property(e => e.Comment).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Deal)
                .WithMany(e => e.Reviews)
                .HasForeignKey(e => e.DealID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Author)
                .WithMany(e => e.ReviewsGiven)
                .HasForeignKey(e => e.AuthorID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Target)
                .WithMany(e => e.ReviewsReceived)
                .HasForeignKey(e => e.TargetID)
                .OnDelete(DeleteBehavior.Restrict);

            // One review per author per deal
            entity.HasIndex(e => new { e.DealID, e.AuthorID }).IsUnique();
        });

        // Notification configuration
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationID);
            entity.Property(e => e.Message).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Account)
                .WithMany()
                .HasForeignKey(e => e.AccountID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.AccountID, e.IsRead });
        });
    }
}
