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
        public DbSet<Submission> Submissions { get; set; }
        public DbSet<ReviewAssignment> ReviewAssignments { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Registration> Registrations { get; set; }
        public DbSet<RegistrationType> RegistrationTypes { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Hotel> Hotels { get; set; }
        public DbSet<RoomType> RoomTypes { get; set; }
        public DbSet<TransferOption> TransferOptions { get; set; }
        public DbSet<AccommodationBooking> AccommodationBookings { get; set; }
        public DbSet<SubmissionAuthor> SubmissionAuthors { get; set; }
        public DbSet<SubmissionFile> SubmissionFiles { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            var currentTenantId = _tenantContext.Current?.Id;

            if (currentTenantId != null)
            {
                foreach (var entityType in builder.Model.GetEntityTypes())
                {
                    if (typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType))
                    {
                        var method = SetGlobalQueryMethod.MakeGenericMethod(entityType.ClrType);
                        method.Invoke(this, new object[] { builder, currentTenantId });
                    }
                }
            }

            builder.Entity<Submission>()
                .HasMany(s => s.ReviewAssignments)
                .WithOne(ra => ra.Submission)
                .HasForeignKey(ra => ra.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ReviewAssignment>()
                .HasOne(ra => ra.Reviewer)
                .WithMany()
                .HasForeignKey(ra => ra.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Message>(entity =>
            {
                entity.HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(m => m.Receiver).WithMany().HasForeignKey(m => m.ReceiverId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Registration>()
                .HasOne(r => r.Conference).WithMany(c => c.Registrations).HasForeignKey(r => r.ConferenceId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AccommodationBooking>()
                .HasOne(b => b.RoomType).WithMany().HasForeignKey(b => b.RoomTypeId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ReviewAssignment>()
                .HasOne(ra => ra.Review).WithOne(r => r.ReviewAssignment).HasForeignKey<Review>(r => r.ReviewAssignmentId).OnDelete(DeleteBehavior.Cascade);
        }

        static readonly MethodInfo SetGlobalQueryMethod = typeof(AppDbContext)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(t => t.IsGenericMethod && t.Name == nameof(SetGlobalQuery));

        private void SetGlobalQuery<T>(ModelBuilder builder, Guid tenantId) where T : class, IMustHaveTenant
        {
            builder.Entity<T>().HasQueryFilter(e => e.TenantId == tenantId);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries<IMustHaveTenant>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added && _tenantContext.Current != null)
                {
                    entry.Entity.TenantId = _tenantContext.Current.Id;
                }
            }


            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}