using Microsoft.AspNetCore.Hosting;

namespace AntAbstract.Web.Files
{
    /// <summary>
    /// Hassas dosyalar (bildiri, makbuz, şablon) için wwwroot dışında
    /// ContentRoot/private-uploads altında saklama yardımcısı.
    /// Dosyalar yalnızca SecureDownloadController üzerinden servis edilir.
    /// </summary>
    public static class PrivateStorage
    {
        public const string RootFolder = "private-uploads";
        public const string SubmissionsFolder = "submissions";
        public const string ReceiptsFolder = "receipts";
        public const string TemplatesFolder = "templates";

        /// <summary>Fiziksel disk yolu döner ve klasörü oluşturur.</summary>
        public static string EnsureFolder(IWebHostEnvironment env, string subFolder)
        {
            var path = Path.Combine(env.ContentRootPath, RootFolder, subFolder);
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// DB'ye kaydedilecek göreli yol. Format: "private-uploads/{sub}/{file}"
        /// SecureDownloadController bunu ContentRootPath ile birleştirir.
        /// </summary>
        public static string ToRelativePath(string subFolder, string fileName)
            => $"{RootFolder}/{subFolder}/{fileName}";

        // İzin verilen eski wwwroot alt klasörleri (backward compat)
        private static readonly string[] AllowedLegacyPrefixes =
        {
            "/uploads/submissions/",
            "/uploads/receipts/",
            "/uploads/templates/",
        };

        // İzin verilen yeni private-uploads alt klasörleri
        private static readonly string[] AllowedPrivatePrefixes =
        {
            $"{RootFolder}/{SubmissionsFolder}/",
            $"{RootFolder}/{ReceiptsFolder}/",
            $"{RootFolder}/{TemplatesFolder}/",
        };

        /// <summary>
        /// Göreli yolu fiziksel yola çevirir.
        /// Yalnızca izin verilen alt klasörlere erişim verir; diğerleri reddedilir.
        /// Eski /uploads/{submissions|receipts|templates}/... formatıyla da uyumludur.
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">İzin verilmeyen yol.</exception>
        public static string Resolve(IWebHostEnvironment env, string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
                throw new UnauthorizedAccessException("Boş dosya yolu.");

            // Normalize: her zaman forward-slash, önündeki slash olmadan
            var normalized = storedPath.Replace('\\', '/');

            // Eski format: /uploads/{allowedSubfolder}/...
            foreach (var prefix in AllowedLegacyPrefixes)
            {
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var relative = normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var full = Path.GetFullPath(Path.Combine(env.WebRootPath, relative));
                    // Güvenlik: canonical path izin verilen alt klasör içinde kalmalı
                    var allowedFolder = Path.GetFullPath(
                        Path.Combine(env.WebRootPath,
                            prefix.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                    if (!full.StartsWith(allowedFolder, StringComparison.OrdinalIgnoreCase))
                        throw new UnauthorizedAccessException("İzin verilmeyen dosya yolu.");
                    return full;
                }
            }

            // Yeni format: private-uploads/{allowedSubfolder}/...
            var normalizedNoSlash = normalized.TrimStart('/');
            foreach (var prefix in AllowedPrivatePrefixes)
            {
                if (normalizedNoSlash.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var relative = normalizedNoSlash.Replace('/', Path.DirectorySeparatorChar);
                    var full = Path.GetFullPath(Path.Combine(env.ContentRootPath, relative));
                    // Güvenlik: canonical path izin verilen alt klasör içinde kalmalı
                    var allowedFolder = Path.GetFullPath(
                        Path.Combine(env.ContentRootPath,
                            prefix.Replace('/', Path.DirectorySeparatorChar)));
                    if (!full.StartsWith(allowedFolder, StringComparison.OrdinalIgnoreCase))
                        throw new UnauthorizedAccessException("İzin verilmeyen dosya yolu.");
                    return full;
                }
            }

            throw new UnauthorizedAccessException($"İzin verilmeyen dosya yolu: '{storedPath}'");
        }
    }
}
