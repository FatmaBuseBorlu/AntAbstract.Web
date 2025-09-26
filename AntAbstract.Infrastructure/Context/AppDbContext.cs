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
                .HasOne(ra => ra.Reviewer)
                .WithMany()
                .HasForeignKey(ra => ra.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}