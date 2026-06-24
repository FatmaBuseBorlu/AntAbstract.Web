using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Common;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace AntAbstract.Infrastructure.Context
{
    public class AppDbContext : IdentityDbContext<AppUser, IdentityRole, string>, IApplicationDbContext
    {
        private readonly TenantContext _tenantContext;

        public AppDbContext(DbContextOptions<AppDbContext> options, TenantContext tenantContext)
            : base(options)
        {
            _tenantContext = tenantContext;
        }

        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<ScientificField> ScientificFields { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<CongressType> CongressTypes { get; set; }
        public DbSet<Conference> Conferences { get; set; }
        public DbSet<ConferencePageBlock> ConferencePageBlocks { get; set; }
        public DbSet<Certificate> Certificates { get; set; }
        public DbSet<ConferenceAttendance> ConferenceAttendances { get; set; }

        public DbSet<ConferenceTopic> ConferenceTopics { get; set; }

        public DbSet<Submission> Submissions { get; set; }
        public DbSet<SubmissionAuthor> SubmissionAuthors { get; set; }
        public DbSet<SubmissionFile> SubmissionFiles { get; set; }
        public DbSet<SiteSectionTemplate> SiteSectionTemplates { get; set; }

        public DbSet<ReviewAssignment> ReviewAssignments { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<ReviewBid> ReviewBids { get; set; }

        public DbSet<Message> Messages { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        public DbSet<Registration> Registrations { get; set; }
        public DbSet<RegistrationType> RegistrationTypes { get; set; }
        public DbSet<Payment> Payments { get; set; }

        public DbSet<Hotel> Hotels { get; set; }
        public DbSet<RoomType> RoomTypes { get; set; }
        public DbSet<TransferOption> TransferOptions { get; set; }
        public DbSet<AccommodationBooking> AccommodationBookings { get; set; }

        public DbSet<SystemParameter> SystemParameters { get; set; }

        public DbSet<EmailTemplate> EmailTemplates { get; set; }
        public DbSet<EmailLog> EmailLogs { get; set; }
        public DbSet<ScheduledBroadcast> ScheduledBroadcasts { get; set; }

        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<PaymentStatusHistory> PaymentStatusHistories { get; set; }
        public DbSet<StripeWebhookEvent> StripeWebhookEvents { get; set; }
        public DbSet<SurveyAnswer> SurveyAnswers { get; set; }
        public DbSet<ReviewCriterion> ReviewCriteria { get; set; }
        public DbSet<ReviewCriterionScore> ReviewCriterionScores { get; set; }
        public DbSet<PlagiarismReport> PlagiarismReports { get; set; }
        public DbSet<ReviewerInvitation> ReviewerInvitations { get; set; }
        public DbSet<InvitedSpeaker> InvitedSpeakers { get; set; }
        public DbSet<Sponsor> Sponsors { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                if (typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType))
                {
                    var method = SetGlobalQueryMethod.MakeGenericMethod(entityType.ClrType);
                    method.Invoke(this, new object[] { builder });
                }
            }

            ApplyRelatedTenantQueryFilters(builder);

            builder.Entity<Conference>(entity =>
            {
                entity.HasIndex(e => e.Slug).IsUnique();
            });

            builder.Entity<Submission>(entity =>
            {
                entity.HasMany(s => s.ReviewAssignments)
                    .WithOne(ra => ra.Submission)
                    .HasForeignKey(ra => ra.SubmissionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.ConferenceTopic)
                    .WithMany()
                    .HasForeignKey(s => s.ConferenceTopicId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(s => s.Topic)
                    .HasMaxLength(150);

                entity.Property(s => s.PresentationType)
                    .HasMaxLength(50);

                entity.Property(s => s.DoiProvider)
                    .HasMaxLength(50);

                entity.Property(s => s.DoiErrorMessage)
                    .HasMaxLength(1000);
            });

            builder.Entity<SiteSectionTemplate>()
                .HasIndex(x => x.BlockType)
                .IsUnique();

            builder.Entity<ConferenceTopic>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Name)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(x => x.NameEn)
                    .HasMaxLength(150);

                entity.Property(x => x.Description)
                    .HasMaxLength(500);

                entity.Property(x => x.DescriptionEn)
                    .HasMaxLength(500);

                entity.Property(x => x.IsActive)
                    .HasDefaultValue(true);

                entity.Property(x => x.SortOrder)
                    .HasDefaultValue(0);

                entity.Property(x => x.CreatedDate)
                    .HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(x => x.Conference)
                    .WithMany()
                    .HasForeignKey(x => x.ConferenceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Certificate>(entity =>
            {
                entity.HasIndex(x => new
                {
                    x.ConferenceId,
                    x.UserId,
                    x.Type
                })
                    .IsUnique();
            });

            builder.Entity<ConferenceAttendance>(entity =>
            {
                entity.HasIndex(x => new
                {
                    x.ConferenceId,
                    x.UserId
                })
                    .IsUnique();

                entity.Property(x => x.UserId)
                    .IsRequired();

                entity.Property(x => x.TotalSeconds)
                    .HasDefaultValue(0);

                entity.Property(x => x.RequiredSeconds)
                    .HasDefaultValue(600);

                entity.Property(x => x.IpAddress)
                    .HasMaxLength(100);

                entity.Property(x => x.UserAgent)
                    .HasMaxLength(500);

                entity.HasOne(x => x.Conference)
                    .WithMany()
                    .HasForeignKey(x => x.ConferenceId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ConferencePageBlock>()
                .HasIndex(x => new
                {
                    x.TenantId,
                    x.ConferenceId,
                    x.Page,
                    x.Culture,
                    x.Order
                });

            builder.Entity<ReviewAssignment>()
                .HasOne(ra => ra.Reviewer)
                .WithMany()
                .HasForeignKey(ra => ra.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Message>(entity =>
            {
                entity.HasOne(m => m.Sender)
                    .WithMany()
                    .HasForeignKey(m => m.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Receiver)
                    .WithMany()
                    .HasForeignKey(m => m.ReceiverId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Registration>()
                .HasOne(r => r.Conference)
                .WithMany(c => c.Registrations)
                .HasForeignKey(r => r.ConferenceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<RegistrationType>()
                .Property(x => x.Description)
                .HasDefaultValue("");

            builder.Entity<AccommodationBooking>()
                .HasOne(b => b.RoomType)
                .WithMany()
                .HasForeignKey(b => b.RoomTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ReviewAssignment>()
                .HasOne(ra => ra.Review)
                .WithOne(r => r.ReviewAssignment)
                .HasForeignKey<Review>(r => r.ReviewAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Performans Index'leri ────────────────────────────────────────────
            // Sık kullanılan sorgulardaki filtre kolonlarını kapsar:
            // TenantId, ConferenceId, UserId, Status, CreatedDate

            builder.Entity<Submission>(entity =>
            {
                entity.HasIndex(x => x.ConferenceId);
                entity.HasIndex(x => x.AuthorId);
                entity.HasIndex(x => new { x.ConferenceId, x.Status });
                entity.HasIndex(x => new { x.TenantId, x.ConferenceId });
            });

            builder.Entity<Registration>(entity =>
            {
                entity.HasIndex(x => x.ConferenceId);
                entity.HasIndex(x => x.AppUserId);
                entity.HasIndex(x => new { x.ConferenceId, x.Status });
            });

            builder.Entity<ReviewAssignment>(entity =>
            {
                entity.HasIndex(x => x.SubmissionId);
                entity.HasIndex(x => x.ReviewerId);
                entity.HasIndex(x => new { x.SubmissionId, x.ReviewerId }).IsUnique();
            });

            builder.Entity<AuditLog>(entity =>
            {
                entity.HasIndex(x => x.UserId);
                entity.HasIndex(x => x.ConferenceId);
                entity.HasIndex(x => x.Category);
                entity.HasIndex(x => x.CreatedAt);
                entity.HasIndex(x => new { x.ConferenceId, x.CreatedAt });
            });

            builder.Entity<Notification>(entity =>
            {
                entity.HasIndex(x => x.UserId);
                entity.HasIndex(x => new { x.UserId, x.IsRead });
            });

            builder.Entity<Conference>(entity =>
            {
                entity.HasIndex(x => x.TenantId);
                entity.HasIndex(x => x.Slug);
            });
        }

        static readonly MethodInfo SetGlobalQueryMethod = typeof(AppDbContext)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(t => t.IsGenericMethod && t.Name == nameof(SetGlobalQuery));

        private void SetGlobalQuery<T>(ModelBuilder builder)
            where T : class, IMustHaveTenant
        {
            builder.Entity<T>().HasQueryFilter(e =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && e.TenantId == CurrentTenantId));
        }

        private Guid? CurrentTenantId => _tenantContext.CurrentTenantId;

        private void ApplyRelatedTenantQueryFilters(ModelBuilder builder)
        {
            builder.Entity<ConferenceTopic>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Conference.TenantId == CurrentTenantId));

            builder.Entity<Session>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Conference.TenantId == CurrentTenantId));

            builder.Entity<InvitedSpeaker>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Conference.TenantId == CurrentTenantId));

            builder.Entity<Sponsor>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Conference.TenantId == CurrentTenantId));

            builder.Entity<Registration>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Conference.TenantId == CurrentTenantId));

            builder.Entity<RegistrationType>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Conference.TenantId == CurrentTenantId));

            builder.Entity<Payment>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Conference != null &&
                entity.Conference.TenantId == CurrentTenantId));

            builder.Entity<PaymentStatusHistory>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Payment != null &&
                entity.Payment.Conference != null &&
                entity.Payment.Conference.TenantId == CurrentTenantId));

            builder.Entity<Certificate>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Conference != null &&
                entity.Conference.TenantId == CurrentTenantId));

            builder.Entity<ConferenceAttendance>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Conference != null &&
                entity.Conference.TenantId == CurrentTenantId));

            builder.Entity<ReviewCriterion>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Conference != null &&
                entity.Conference.TenantId == CurrentTenantId));

            builder.Entity<SurveyAnswer>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Conference != null &&
                entity.Conference.TenantId == CurrentTenantId));

            builder.Entity<Hotel>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Conference.TenantId == CurrentTenantId));

            builder.Entity<RoomType>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Hotel.Conference.TenantId == CurrentTenantId));

            builder.Entity<TransferOption>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Conference.TenantId == CurrentTenantId));

            builder.Entity<AccommodationBooking>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Conference.TenantId == CurrentTenantId));

            builder.Entity<ReviewAssignment>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Submission.TenantId == CurrentTenantId));

            builder.Entity<Review>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.ReviewAssignment.Submission.TenantId == CurrentTenantId));

            builder.Entity<ReviewCriterionScore>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Review != null &&
                entity.Review.ReviewAssignment.Submission.TenantId == CurrentTenantId));

            builder.Entity<SubmissionAuthor>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Submission.TenantId == CurrentTenantId));

            builder.Entity<SubmissionFile>().HasQueryFilter(entity =>
                _tenantContext.IsGlobalContext ||
                (CurrentTenantId.HasValue && entity.Submission.TenantId == CurrentTenantId));
        }

        public override int SaveChanges()
        {
            EnforceTenantIsolation();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            EnforceTenantIsolation();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void EnforceTenantIsolation()
        {
            var entries = ChangeTracker.Entries<IMustHaveTenant>()
                .Where(e =>
                    e.State == EntityState.Added ||
                    e.State == EntityState.Modified ||
                    e.State == EntityState.Deleted);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    if (CurrentTenantId.HasValue)
                    {
                        if (entry.Entity.TenantId == Guid.Empty)
                        {
                            entry.Entity.TenantId = CurrentTenantId.Value;
                        }
                        else if (entry.Entity.TenantId != CurrentTenantId.Value)
                        {
                            throw new InvalidOperationException(
                                "A tenant-bound entity cannot be added to another tenant.");
                        }
                    }
                    else if (entry.Entity.TenantId == Guid.Empty)
                    {
                        throw new InvalidOperationException(
                            "A tenant-bound entity must have a tenant.");
                    }

                    continue;
                }

                var tenantProperty = entry.Property(nameof(IMustHaveTenant.TenantId));

                if (entry.State == EntityState.Modified &&
                    tenantProperty.IsModified &&
                    !Equals(tenantProperty.OriginalValue, tenantProperty.CurrentValue))
                {
                    throw new InvalidOperationException(
                        "The tenant of an existing entity cannot be changed.");
                }

                if (CurrentTenantId.HasValue &&
                    entry.Entity.TenantId != CurrentTenantId.Value)
                {
                    throw new InvalidOperationException(
                        "A tenant-bound entity cannot be changed or deleted from another tenant.");
                }
            }
        }
    }
}
