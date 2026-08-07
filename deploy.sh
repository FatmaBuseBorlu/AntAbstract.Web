#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# AntAbstract.Web — Production Deploy Script (Turhost / Plesk)
#
# Kritik ayarlar (Turhost zorunlu kombinasyonu):
#   -r win-x86          → App pool 32-bit
#   --self-contained    → Turhost'ta .NET runtime yok
#   hostingModel=InProcess → OutOfProcess 502.5 verir
#
# Kullanım: ./deploy.sh
# Çıktı:    ~/Desktop/AntAbstract-Plesk-x86-YYYYMMDD.zip
# ─────────────────────────────────────────────────────────────────────────────
set -e

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$ROOT_DIR/AntAbstract.Web/AntAbstract.Web.csproj"
INFRA="$ROOT_DIR/AntAbstract.Infrastructure/AntAbstract.Infrastructure.csproj"
OUTPUT="$ROOT_DIR/deploy_output"
ZIP_NAME="AntAbstract-Plesk-x86-$(date +%Y%m%d).zip"
ZIP_PATH="$HOME/Desktop/$ZIP_NAME"

echo ""
echo "╔══════════════════════════════════════════════╗"
echo "║        AntAbstract.Web — Deploy Script       ║"
echo "║        Turhost/Plesk: win-x86 InProcess      ║"
echo "╚══════════════════════════════════════════════╝"
echo ""

# ── 1. Restore ────────────────────────────────────────────────────────────────
echo "▶ [1/5] Restore..."
dotnet restore "$ROOT_DIR/AntAbstract.Web.sln" --verbosity quiet
dotnet restore "$PROJECT" -r win-x86 --verbosity quiet

# ── 2. Build (Release) ────────────────────────────────────────────────────────
echo "▶ [2/5] Build (Release)..."
dotnet build "$PROJECT" -c Release --no-restore --verbosity quiet

# ── 3. Publish — win-x86 self-contained ──────────────────────────────────────
echo "▶ [3/5] Publish → $OUTPUT"
rm -rf "$OUTPUT"
dotnet publish "$PROJECT" \
  -c Release \
  --no-restore \
  -r win-x86 \
  --self-contained true \
  -p:PublishReadyToRun=false \
  -o "$OUTPUT" \
  --verbosity quiet

echo "✅ Publish tamamlandı."
echo ""

# ── web.config: InProcess kontrolü ───────────────────────────────────────────
WEB_CONFIG="$OUTPUT/web.config"
if grep -q 'hostingModel="OutOfProcess"' "$WEB_CONFIG" 2>/dev/null; then
  sed -i '' 's/hostingModel="OutOfProcess"/hostingModel="InProcess"/g' "$WEB_CONFIG"
  echo "⚠️  web.config: OutOfProcess → InProcess olarak düzeltildi."
else
  echo "✅ web.config: hostingModel=InProcess (OK)"
fi

# ── Sunucuya ait ayar dosyalarını pakete koyma ───────────────────────────────
# appsettings.Production.json gerçek bağlantı dizesini ve anahtarları tutar ve
# yalnızca sunucuda yaşar. Pakete girerse "üzerine yaz" ile açıldığında
# sunucudaki gerçek değerleri #{TOKEN}# placeholder'larıyla ezer ve site 500 verir.
for f in appsettings.Production.json appsettings.Development.json; do
  if [ -f "$OUTPUT/$f" ]; then
    rm -f "$OUTPUT/$f"
    echo "🔒 $f pakete dahil edilmedi (sunucudaki ayarlar korunsun)."
  fi
done

# ── 4. Duplicate dosyaları temizle ───────────────────────────────────────────
echo "▶ [4/5] Duplicate dosyalar temizleniyor..."
DUPES=$(find "$OUTPUT" -name "* [0-9]*.*" 2>/dev/null | wc -l | tr -d ' ')
if [ "$DUPES" -gt 0 ]; then
  find "$OUTPUT" -name "* [0-9]*.*" -delete
  echo "   $DUPES duplicate dosya silindi."
else
  echo "   Duplicate yok."
fi

# ── Migration SQL üretimi ─────────────────────────────────────────────────────
echo "▶ [5/5] Migration SQL oluşturuluyor..."
export PATH="$PATH:$HOME/.dotnet/tools"
if ! dotnet ef --version >/dev/null 2>&1; then
  echo "   dotnet-ef bulunamadı, kuruluyor..."
  dotnet tool install --global dotnet-ef --version "8.*" >/dev/null
fi

dotnet ef migrations script \
  --idempotent \
  --project "$INFRA" \
  --startup-project "$PROJECT" \
  --configuration Release \
  --no-build \
  --output "$OUTPUT/migration.sql" 2>/dev/null && \
  echo "✅ Migration SQL hazır: deploy_output/migration.sql" || \
  echo "⚠️  Migration SQL oluşturulamadı (devam ediliyor)"

echo ""

# ── ZIP oluştur ───────────────────────────────────────────────────────────────
echo "▶ ZIP oluşturuluyor: $ZIP_NAME"
rm -f "$ZIP_PATH"
python3 -c "
import zipfile, pathlib
src = pathlib.Path('$OUTPUT')
out = pathlib.Path('$ZIP_PATH')
with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as zf:
    for f in src.rglob('*'):
        if f.is_file():
            zf.write(f, f.relative_to(src))
mb = out.stat().st_size / 1024 / 1024
print(f'✅ ZIP hazır: {out.name} ({mb:.1f} MB)')
"

echo ""
echo "─────────────────────────────────────────────────────────────────────"
echo "Plesk yükleme adımları:"
echo "  1. ~/Desktop/$ZIP_NAME dosyasını al"
echo "  2. Plesk → antabstract.com.tr → Dosyalar → httpdocs/"
echo "  3. Eski .exe .dll web.config appsettings.* sil"
echo "     (logs/ private-uploads/ wwwroot/uploads/ dokunma!)"
echo "  4. ZIP'i yükle → Sağ tık → Arşivi Aç → üzerine yaz seç"
echo "  5. ZIP'i sil"
echo "  6. appsettings.Production.json pakette YOK — sunucudaki dosya korunur."
echo "     (Bağlantı dizesi ve anahtarlar orada; ezilmemesi için kasıtlı)"
echo "  7. Plesk → .NET → Restart App"
echo "  8. migration.sql varsa Plesk MSSQL aracında çalıştır"
echo "─────────────────────────────────────────────────────────────────────"
echo ""
