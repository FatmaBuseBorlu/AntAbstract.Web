namespace AntAbstract.Web.Models.ViewModels.Shared
{
    /// <summary>
    /// Yönetici listelerindeki sayfalama (_AdminPager). Filtreler sorgu
    /// dizesinde kaldığı için bağlantılar yalnızca "page"i değiştirir.
    /// </summary>
    public class PagerViewModel
    {
        public int Page { get; }
        public int PageSize { get; }
        public int TotalCount { get; }

        public PagerViewModel(int page, int pageSize, int totalCount)
        {
            PageSize = pageSize;
            TotalCount = totalCount;
            Page = Math.Clamp(page, 1, Math.Max(1, TotalPages));
        }

        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public int Skip => (Page - 1) * PageSize;
        public int From => TotalCount == 0 ? 0 : Skip + 1;
        public int To => Math.Min(Page * PageSize, TotalCount);

        /// <summary>Sayfa numarasını istek aralığına sıkıştırır; sayfa boyutu sabit.</summary>
        public static PagerViewModel For(int? page, int totalCount, int pageSize = 50) =>
            new(page ?? 1, pageSize, totalCount);
    }
}
