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

            builder.Entity<Submission>()
                .HasMany(s => s.SubmissionAuthors)
                .WithOne(sa => sa.Submission)
                .HasForeignKey(sa => sa.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Submission>()
                .HasMany(s => s.Files)
                .WithOne(f => f.Submission)
                .HasForeignKey(f => f.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            // HAKEM ATAMA İLİŞKİSİ
            builder.Entity<ReviewAssignment>()
                .HasOne(ra => ra.AppUser) // Hakem (AppUser)
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

            // KAYIT İLİŞKİSİ
            builder.Entity<Conference>()
                .HasMany<Registration>()
                .WithOne(r => r.Conference)
                .HasForeignKey(r => r.ConferenceId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}