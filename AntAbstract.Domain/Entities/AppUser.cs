using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace AntAbstract.Domain.Entities
{
    // Bu sınıf VERİTABANI tablosunun karşılığıdır.
    public class AppUser : IdentityUser
    {
        // --- KİMLİK BİLGİLERİ ---
        [StringLength(50)]
        public string? FirstName { get; set; }

        [StringLength(50)]
        public string? LastName { get; set; }

        [StringLength(20)]
        public string? IdentityNumber { get; set; } // TC veya Pasaport

        // --- İLETİŞİM & LOKASYON ---
        public string? AlternativeEmail { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }

        // --- AKADEMİK / MESLEKİ ---
        public string? University { get; set; } // Üniversite

        [StringLength(200)]
        public string? Institution { get; set; } // Kurum (Üniversite ile benzer ama ayrı tutulabilir)

        [StringLength(100)]
        public string? Title { get; set; } // Ünvan (Prof. Dr.) - TEK BİR TANE OLMALI

        public string? Profession { get; set; } // Meslek/Uzmanlık

        [StringLength(500)]
        public string? ExpertiseAreas { get; set; } // Uzmanlık Alanları (Hatanın çözümü)

        // --- SİSTEM ---
        public string? DisplayName { get; set; } // Görünen Ad (Ad Soyad birleşik)

        // DİKKAT: Veritabanında dosyanın kendisi değil, YOLU (Path) tutulur.
        public string? ProfileImagePath { get; set; }

        // Not: Email, PhoneNumber, PasswordHash zaten IdentityUser'dan geliyor.
        // Buraya tekrar yazılmaz.
    }
}