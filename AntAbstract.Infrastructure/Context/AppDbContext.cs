using AntAbstract.Domain;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Context
{
    public class AppDbContext : IdentityDbContext<AppUser, IdentityRole, string>
    {
        public AppDbContext(DbContextOptions options) : base(options)
        {
        }
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<Conference> Conferences => Set<Conference>();
        public DbSet<Submission> Submissions => Set<Submission>();

        // Hakemlik Modülü Tabloları
        public DbSet<Reviewer> Reviewers { get; set; }
        public DbSet<ReviewAssignment> ReviewAssignments { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<ScientificField> ScientificFields { get; set; }
        public DbSet<CongressType> CongressTypes { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<ReviewForm> ReviewForms { get; set; }
        public DbSet<ReviewCriterion> ReviewCriteria { get; set; }
        public DbSet<ReviewAnswer> ReviewAnswers { get; set; }
        public DbSet<RegistrationType> RegistrationTypes { get; set; }
        public DbSet<Registration> Registrations { get; set; }
        public DbSet<Payment> Payments { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Submission>()
                .HasMany(s => s.ReviewAssignments)
                .WithOne(ra => ra.Submission)
                .HasForeignKey(ra => ra.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Submission>()
                .HasOne(s => s.Author)
                .WithMany()
                .HasForeignKey(s => s.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ReviewAssignment>()
                .HasOne(ra => ra.AppUser)
                .WithMany()
                .HasForeignKey(ra => ra.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            // DÜZELTİLDİ: Message ilişkileri tek bir blokta birleştirildi.
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

            builder.Entity<Session>()
                .HasOne(s => s.Conference)
                .WithMany()
                .HasForeignKey(s => s.ConferenceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Review>()
                .HasMany(r => r.Answers)
                .WithOne(a => a.Review)
                .HasForeignKey(a => a.ReviewId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ReviewForm>()
                .HasMany(f => f.Criteria)
                .WithOne(c => c.Form)
                .HasForeignKey(c => c.FormId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Conference>()
                .HasMany<Registration>()
                .WithOne(r => r.Conference)
                .HasForeignKey(r => r.ConferenceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Conference>()
                .HasMany<RegistrationType>()
                .WithOne(rt => rt.Conference)
                .HasForeignKey(rt => rt.ConferenceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(18, 2)");

        }
    }
}