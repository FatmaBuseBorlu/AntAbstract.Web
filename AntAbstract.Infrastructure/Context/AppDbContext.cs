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

            // Submission-Author ilişkisi için zincirleme silmeyi devre dışı bırak
            builder.Entity<Submission>()
                .HasOne(s => s.Author)
                .WithMany() // AppUser'da Submissions listesi olmadığı için WithMany() boş
                .HasForeignKey(s => s.AuthorId)
                .OnDelete(DeleteBehavior.Restrict); // Veya .NoAction

            // ReviewAssignment-Submission ilişkisi için zincirleme silmeyi devre dışı bırak
            builder.Entity<ReviewAssignment>()
                .HasOne(ra => ra.Submission)
                .WithMany()
                .HasForeignKey(ra => ra.SubmissionId)
                .OnDelete(DeleteBehavior.Restrict);

            // ReviewAssignment-Reviewer ilişkisi için zincirleme silmeyi devre dışı bırak
            builder.Entity<ReviewAssignment>()
                .HasOne(ra => ra.Reviewer)
                .WithMany()
                .HasForeignKey(ra => ra.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}