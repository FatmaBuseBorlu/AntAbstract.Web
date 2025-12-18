using AntAbstract.Domain;
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
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<Conference> Conferences => Set<Conference>();
        public DbSet<Submission> Submissions => Set<Submission>();
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

        // --- TEMEL TABLOLAR ---
        public DbSet<Tenant> Tenants => Set<Tenant>(); // Çoklu Kongre Altyapısı
        public DbSet<Conference> Conferences => Set<Conference>(); // Kongreler
        public DbSet<Submission> Submissions => Set<Submission>(); // Bildiriler
        public DbSet<SubmissionAuthor> SubmissionAuthors { get; set; } // Bildiri Yazarları
        public DbSet<SubmissionFile> SubmissionFiles { get; set; } // Bildiri Dosyaları

        // --- DEĞERLENDİRME & HAKEMLİK ---
        public DbSet<ReviewAssignment> ReviewAssignments { get; set; } // Hakem Atamaları
        public DbSet<Review> Reviews { get; set; } // Puanlama ve Yorumlar

        // --- İLETİŞİM & KAYIT ---
        public DbSet<Message> Messages { get; set; } // Mesajlaşma
        public DbSet<Notification> Notifications { get; set; } // Bildirimler (Yeni Ekledik!)
        public DbSet<Registration> Registrations { get; set; } // Kongre Kayıtları

        // --- PROGRAM & DİĞER ---
        public DbSet<Session> Sessions { get; set; } // Kongre Programı (İleride Kullanılacak)

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // BİLDİRİ İLİŞKİLERİ
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

            // MESAJLAŞMA İLİŞKİSİ
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

            // PROGRAM (SESSION) İLİŞKİSİ
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
        }
    }
}