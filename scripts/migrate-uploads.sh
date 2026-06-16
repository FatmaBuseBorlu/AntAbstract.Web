#!/usr/bin/env bash
# =============================================================================
# migrate-uploads.sh
# Eski wwwroot/uploads/{submissions,receipts,templates} dosyalarını
# private-uploads/{submissions,receipts,templates} klasörüne taşır.
#
# Kullanım:
#   ./scripts/migrate-uploads.sh [--dry-run] [--app-root /path/to/app]
#
# Parametreler:
#   --dry-run     Dosyaları taşımaz, ne yapılacağını ekrana yazar.
#   --app-root    Uygulamanın kök dizini (varsayılan: script'in üst dizini)
#
# Gereksinimler: bash 4+, rsync
# =============================================================================

set -euo pipefail

DRY_RUN=false
APP_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

# ── Argüman ayrıştırma ──────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
    case "$1" in
        --dry-run)   DRY_RUN=true; shift ;;
        --app-root)  APP_ROOT="$2"; shift 2 ;;
        *)           echo "Bilinmeyen parametre: $1"; exit 1 ;;
    esac
done

# ── Yollar ──────────────────────────────────────────────────────────────────
WWWROOT="$APP_ROOT/AntAbstract.Web/wwwroot"
PRIVATE="$APP_ROOT/AntAbstract.Web/private-uploads"

PAIRS=(
    "uploads/submissions:submissions"
    "uploads/receipts:receipts"
    "uploads/templates:templates"
)

# ── Yardımcılar ─────────────────────────────────────────────────────────────
log()  { echo "[$(date '+%H:%M:%S')] $*"; }
info() { echo "  ▸ $*"; }

check_deps() {
    for cmd in rsync; do
        if ! command -v "$cmd" &>/dev/null; then
            echo "Hata: '$cmd' bulunamadı. Lütfen kurun." >&2; exit 1
        fi
    done
}

count_files() { find "$1" -type f 2>/dev/null | wc -l | tr -d ' '; }

# ── Ana ─────────────────────────────────────────────────────────────────────
check_deps

if [[ "$DRY_RUN" == "true" ]]; then
    log "DRY-RUN modu — hiçbir şey taşınmayacak."
fi

log "Uygulama kökü : $APP_ROOT"
log "wwwroot       : $WWWROOT"
log "private-uploads: $PRIVATE"
echo ""

TOTAL_MOVED=0
TOTAL_SKIPPED=0

for pair in "${PAIRS[@]}"; do
    SRC_REL="${pair%%:*}"
    DST_REL="${pair##*:}"
    SRC="$WWWROOT/$SRC_REL"
    DST="$PRIVATE/$DST_REL"

    if [[ ! -d "$SRC" ]]; then
        log "Kaynak klasör bulunamadı, atlanıyor: $SRC"
        continue
    fi

    FILE_COUNT=$(count_files "$SRC")
    log "İşleniyor: $SRC_REL → private-uploads/$DST_REL ($FILE_COUNT dosya)"

    if [[ "$DRY_RUN" == "true" ]]; then
        find "$SRC" -type f | while read -r f; do
            info "TAŞINACAK: ${f#"$WWWROOT/"}"
        done
        TOTAL_MOVED=$((TOTAL_MOVED + FILE_COUNT))
        continue
    fi

    # Hedef klasörü oluştur
    mkdir -p "$DST"

    # rsync: kaynak dosyaları hedefe kopyala (çakışırsa atla)
    rsync -a --ignore-existing "$SRC/" "$DST/"
    MOVED=$(count_files "$DST")
    info "$MOVED dosya hedefe kopyalandı."

    # Kaynak dosyaların hedefte var olduğunu doğrula, sonra sil
    SKIPPED=0
    while IFS= read -r -d '' src_file; do
        rel="${src_file#"$SRC/"}"
        dst_file="$DST/$rel"
        if [[ -f "$dst_file" ]]; then
            rm -f "$src_file"
        else
            info "UYARI: Hedefte bulunamadı, kaynak korundu: $rel"
            SKIPPED=$((SKIPPED + 1))
        fi
    done < <(find "$SRC" -type f -print0)

    # Boş kalan kaynak klasörü temizle
    find "$SRC" -type d -empty -delete 2>/dev/null || true

    TOTAL_MOVED=$((TOTAL_MOVED + FILE_COUNT - SKIPPED))
    TOTAL_SKIPPED=$((TOTAL_SKIPPED + SKIPPED))
    log "Tamamlandı: $SRC_REL"
    echo ""
done

echo ""
log "=== Özet ==="
if [[ "$DRY_RUN" == "true" ]]; then
    log "Taşınacak toplam dosya: $TOTAL_MOVED (dry-run, gerçek işlem yapılmadı)"
else
    log "Taşınan: $TOTAL_MOVED dosya"
    [[ $TOTAL_SKIPPED -gt 0 ]] && log "UYARI: $TOTAL_SKIPPED dosya taşınamadı (manuel kontrol edin)"
    log ""
    log "DB güncellemesi:"
    log "  SubmissionFiles.FilePath: '/uploads/submissions/...' değerlerini"
    log "  'private-uploads/submissions/...' ile güncelleyin."
    log "  Registrations.ReceiptFilePath: '/uploads/receipts/...' → 'private-uploads/receipts/...'"
    log "  Conferences.TemplateFilePath (varsa): '/uploads/templates/...' → 'private-uploads/templates/...'"
    log ""
    log "SQL örneği (önce yedek alın!):"
    cat <<'SQL'
    UPDATE SubmissionFiles
       SET FilePath = REPLACE(FilePath, '/uploads/submissions/', 'private-uploads/submissions/')
     WHERE FilePath LIKE '/uploads/submissions/%';

    UPDATE Registrations
       SET ReceiptFilePath = REPLACE(ReceiptFilePath, '/uploads/receipts/', 'private-uploads/receipts/')
     WHERE ReceiptFilePath LIKE '/uploads/receipts/%';

    -- Konferans şablonları için tablo/kolon adını doğrulayın:
    -- UPDATE Conferences
    --    SET TemplateFilePath = REPLACE(TemplateFilePath, '/uploads/templates/', 'private-uploads/templates/')
    --  WHERE TemplateFilePath LIKE '/uploads/templates/%';
SQL
fi
