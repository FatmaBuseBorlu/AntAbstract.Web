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
echo "▶ [1/4] Restore..."
dotnet restore "$ROOT_DIR/AntAbstract.Web.sln" --verbosity quiet

# ── 2. Build (Release) ────────────────────────────────────────────────────────
echo "▶ [2/4] Build (Release)..."
dotnet build "$PROJECT" -c Release --no-restore --verbosity quiet

# ── 3. Publish ────────────────────────────────────────────────────────────────
echo "▶ [3/4] Publish → $OUTPUT"
rm -rf "$OUTPUT"
dotnet publish "$PROJECT" \
  -c Release \
  --no-restore \
  -r linux-x64 \
  --self-contained false \
  -p:PublishReadyToRun=true \
  -o "$OUTPUT" \
  --verbosity quiet

echo ""
echo "✅ Publish tamamlandı: $OUTPUT"
echo ""

# ── 4. Migration kontrolü ─────────────────────────────────────────────────────
echo "▶ [4/4] Bekleyen migration kontrolü..."
PENDING=$(dotnet ef migrations list \
  --project "$INFRA" \
  --startup-project "$PROJECT" \
  --no-build 2>/dev/null | grep "(Pending)" | wc -l | tr -d ' ')

if [ "$PENDING" -gt "0" ]; then
  echo ""
  echo "⚠️  $PENDING bekleyen migration var!"
  echo ""
  echo "   Plesk sunucusunda aşağıdaki komutu çalıştır:"
  echo ""
  echo "   cd /var/www/vhosts/<domain>/httpdocs"
  echo "   dotnet AntAbstract.Web.dll migrate"
  echo ""
  echo "   VEYA sunucuda EF CLI kuruluysa:"
  echo "   dotnet ef database update \\"
  echo "     --project AntAbstract.Infrastructure \\"
  echo "     --startup-project AntAbstract.Web \\"
  echo "     --connection \"<PRODUCTION_CONNECTION_STRING>\""
  echo ""
else
  echo "✅ Bekleyen migration yok."
fi

echo ""
echo "─────────────────────────────────────────────────"
echo "Sonraki adımlar:"
echo "  1. deploy_output/ klasörünü Plesk File Manager"
echo "     veya FTP ile sunucuya yükle"
echo "  2. Plesk'te deployment variables'ların ayarlı"
echo "     olduğundan emin ol (bkz. DEPLOYMENT.md)"
echo "  3. Bekleyen migration varsa yukarıdaki komutu çalıştır"
echo "  4. Uygulamayı Plesk'ten restart et"
echo "─────────────────────────────────────────────────"
echo ""
