using AntAbstract.Domain;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
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

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Submission ve ReviewAssignment arasındaki ilişkiyi manuel olarak yapılandır
            // Bu, "SubmissionId1" gibi hayalet sütunların oluşmasını engeller.
            builder.Entity<Submission>()
                .HasMany(s => s.ReviewAssignments) // Bir Submission'ın çok sayıda ReviewAssignment'ı vardır
                .WithOne(ra => ra.Submission)      // Bir ReviewAssignment'ın ise bir tane Submission'ı vardır
                .HasForeignKey(ra => ra.SubmissionId) // Bu ilişki SubmissionId sütunu üzerinden kurulur
                .OnDelete(DeleteBehavior.Cascade); // Bir özet silinirse, ona bağlı tüm atamalar da silinsin.

            // Diğer ilişkiler için zincirleme silmeyi kısıtla (daha önce eklemiştik)
            builder.Entity<Submission>()
                .HasOne(s => s.Author)
                .WithMany()
                .HasForeignKey(s => s.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ReviewAssignment>()
                .HasOne(ra => ra.AppUser) // DÜZELTME BU SATIRDA
                .WithMany()
                .HasForeignKey(ra => ra.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Message-Sender ilişkisi için zincirleme silmeyi kısıtla
            builder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            // Message-Receiver ilişkisi için zincirleme silmeyi kısıtla
            builder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            // OnModelCreating metodu içine ekle
            builder.Entity<Session>()
                .HasOne(s => s.Conference)
                .WithMany() // Conference'da Sessions listesi olmadığı için WithMany() boş
                .HasForeignKey(s => s.ConferenceId)
                .OnDelete(DeleteBehavior.Cascade); // Bir konferans silinirse, tüm oturumları da silinsin.
                                                   // OnModelCreating(ModelBuilder builder) metodu içine eklenecek.

            // OnModelCreating(ModelBuilder builder) metodu içine eklenecek.

            // Bir Review silindiğinde, ona bağlı ReviewAnswer'ların silinmesini engelle (döngüsel çakışmayı önlemek için)
            builder.Entity<Review>()
                .HasMany(r => r.Answers)
                .WithOne(a => a.Review)
                .HasForeignKey(a => a.ReviewId)
                .OnDelete(DeleteBehavior.Restrict); // DİKKAT: Geçen seferki hatayı önlemek için 'Restrict' kullanıyoruz.

            // Bir ReviewForm silindiğinde, ona bağlı tüm ReviewCriterion'lar da silinsin (bu güvenli).
            builder.Entity<ReviewForm>()
                .HasMany(f => f.Criteria)
                .WithOne(c => c.Form)
                .HasForeignKey(c => c.FormId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}