using System.Collections.Generic;

namespace AntAbstract.Web.Models.WebsiteBlocks
{
    /// <summary>
    /// Kongre Konuları bloğu — bildiri gönderilebilecek başlıklar.
    /// </summary>
    public class TopicsBlockContent
    {
        public string Description { get; set; } = "";
        public List<TopicItem> Items { get; set; } = new();
    }

    public class TopicItem
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }

    /// <summary>
    /// Önemli Tarihler bloğu — son başvuru, bildirim, kongre tarihleri.
    /// </summary>
    public class ImportantDatesBlockContent
    {
        public List<ImportantDateItem> Items { get; set; } = new();
    }

    public class ImportantDateItem
    {
        public string Label { get; set; } = "";
        public string Date { get; set; } = "";
        public string Description { get; set; } = "";

        /// <summary>Geçmiş tarihleri soluk göstermek için işaretlenir.</summary>
        public bool IsPassed { get; set; }
    }

    /// <summary>
    /// Katılım Ücretleri bloğu — kayıt türlerine göre fiyat tablosu.
    /// </summary>
    public class FeesBlockContent
    {
        public string Description { get; set; } = "";
        public string Currency { get; set; } = "TRY";
        public List<FeeItem> Items { get; set; } = new();
    }

    public class FeeItem
    {
        public string Name { get; set; } = "";
        public string Price { get; set; } = "";
        public string Deadline { get; set; } = "";
        public string Description { get; set; } = "";

        /// <summary>Tabloda öne çıkarılacak satır.</summary>
        public bool IsHighlighted { get; set; }
    }

    /// <summary>
    /// Kurullar bloğu — bilim kurulu, düzenleme kurulu gibi gruplar.
    /// </summary>
    public class CommitteesBlockContent
    {
        public List<CommitteeGroup> Groups { get; set; } = new();
    }

    public class CommitteeGroup
    {
        public string Name { get; set; } = "";
        public List<CommitteeMember> Members { get; set; } = new();
    }

    public class CommitteeMember
    {
        public string FullName { get; set; } = "";
        public string Title { get; set; } = "";
        public string Institution { get; set; } = "";
    }

    /// <summary>
    /// İletişim bloğu — kongre sekretaryası bilgileri.
    /// </summary>
    public class ContactBlockContent
    {
        public string Description { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Address { get; set; } = "";
        public string MapEmbedUrl { get; set; } = "";
    }

    /// <summary>
    /// Kongreye Çağrı bloğu — bildiri çağrısı metni ve kurallar.
    /// </summary>
    public class CallForPapersBlockContent
    {
        public string Description { get; set; } = "";
        public string Deadline { get; set; } = "";
        public List<string> Guidelines { get; set; } = new();
    }
}
