#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# AntAbstract.Web — Production Deploy Script
# Kullanım: ./deploy.sh
# Çıktı:    deploy_output/  (bu klasörü Plesk'e FTP ile yükle)
# ─────────────────────────────────────────────────────────────────────────────
set -e

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$ROOT_DIR/AntAbstract.Web/AntAbstract.Web.csproj"
INFRA="$ROOT_DIR/AntAbstract.Infrastructure/AntAbstract.Infrastructure.csproj"
OUTPUT="$ROOT_DIR/deploy_output"

echo ""
echo "╔══════════════════════════════════════════════╗"
echo "║        AntAbstract.Web — Deploy Script       ║"
echo "╚══════════════════════════════════════════════╝"
echo ""

# ── 1. Restore ────────────────────────────────────────────────────────────────
echo "▶ [1/5] Restore..."
dotnet restore "$ROOT_DIR/AntAbstract.Web.sln" --verbosity quiet
dotnet restore "$PROJECT" -r linux-x64 --verbosity quiet

# ── 2. Build (Release) ────────────────────────────────────────────────────────
echo "▶ [2/5] Build (Release)..."
dotnet build "$PROJECT" -c Release --no-restore --verbosity quiet

# ── 3. Publish ────────────────────────────────────────────────────────────────
echo "▶ [3/5] Publish → $OUTPUT"
rm -rf "$OUTPUT"
dotnet publish "$PROJECT" \
  -c Release \
  --no-restore \
  -r linux-x64 \
  --self-contained false \
  -p:PublishReadyToRun=false \
  -o "$OUTPUT" \
  --verbosity quiet

echo ""
echo "✅ Publish tamamlandı: $OUTPUT"
echo ""

# ── 4. Migration SQL üretimi ──────────────────────────────────────────────────
echo "▶ [4/5] Migration SQL oluşturuluyor..."
export PATH="$PATH:$HOME/.dotnet/tools"
if ! dotnet ef --version >/dev/null 2>&1; then
  echo "   dotnet-ef bulunamadı, global tool olarak kuruluyor..."
  dotnet tool install --global dotnet-ef --version "8.*" >/dev/null
fi

dotnet ef migrations script \
  --idempotent \
  --project "$INFRA" \
  --startup-project "$PROJECT" \
  --configuration Release \
  --no-build \
  --output "$OUTPUT/migration.sql"

echo "✅ Migration SQL hazır: $OUTPUT/migration.sql"
echo ""

# ── 5. Migration kontrolü ─────────────────────────────────────────────────────
echo "▶ [5/5] Migration dosyalarını kontrol ediyorum..."
MIGRATION_COUNT=$(find "$ROOT_DIR/AntAbstract.Infrastructure/Migrations" -name "*.cs" \
  -not -name "*.Designer.cs" -not -name "AppDbContextModelSnapshot.cs" | wc -l | tr -d ' ')

echo "   $MIGRATION_COUNT migration dosyası bulundu."
echo ""
echo "   ⚠️  ÖNEMLİ: Sunucuda migration uygulanmazsa site AÇILMAZ!"
echo "   Production'da bekleyen migration varsa uygulama başlatmayı reddeder."
echo ""
echo "   Sunucuda şu komutu çalıştır:"
echo "   ──────────────────────────────────────────"
echo "   migration.sql dosyasını Plesk MSSQL aracında veya SQL Server'da çalıştır"
echo "   ──────────────────────────────────────────"

echo ""
echo "─────────────────────────────────────────────────"
echo "Sonraki adımlar:"
echo "  1. deploy_output/ klasörünü Plesk File Manager"
echo "     veya FTP ile sunucuya yükle"
echo "  2. Plesk'te deployment variables'ların ayarlı"
echo "     olduğundan emin ol (bkz. DEPLOYMENT.md)"
echo "  3. deploy_output/migration.sql dosyasını veritabanına uygula"
echo "  4. Uygulamayı Plesk'ten restart et"
echo "─────────────────────────────────────────────────"
echo ""
