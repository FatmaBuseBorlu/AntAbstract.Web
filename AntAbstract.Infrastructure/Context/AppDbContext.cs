using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Infrastructure.Context
{
    public class AppDbContext : IdentityDbContext<AppUser, IdentityRole, string>
    {
        public AppDbContext(DbContextOptions options) : base(options)
        {
        }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<ScientificField> ScientificFields { get; set; }
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

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

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

            builder.Entity<AccommodationBooking>()
                .HasOne(b => b.RoomType)
                .WithMany()
                .HasForeignKey(b => b.RoomTypeId)
                .OnDelete(DeleteBehavior.Restrict); 

        }
    }
}