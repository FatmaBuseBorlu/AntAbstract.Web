using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Services.Payment
{
    public interface IPayTRService
    {
        bool IsConfigured { get; }

        /// <summary>
        /// PayTR API'den iframe token alır.
        /// </summary>
        Task<PayTRTokenResult> GetIframeTokenAsync(PayTRPaymentRequest request);

        /// <summary>
        /// PayTR callback hash doğrulaması.
        /// </summary>
        bool VerifyCallback(string merchantOid, string status, string totalAmount, string hash);
    }

    public class PayTRPaymentRequest
    {
        public string MerchantOid { get; set; } = null!;
        public string Email { get; set; } = null!;
        public long AmountKurus { get; set; }
        public string Currency { get; set; } = "TL";
        public string UserName { get; set; } = null!;
        public string UserAddress { get; set; } = "Adres belirtilmedi";
        public string UserPhone { get; set; } = "05000000000";
        public string UserIp { get; set; } = "127.0.0.1";
        public string OkUrl { get; set; } = null!;
        public string FailUrl { get; set; } = null!;
        /// <summary>Sipariş özeti (JSON array formatında)</summary>
        public string BasketJson { get; set; } = "[]";
    }

    public class PayTRTokenResult
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? Error { get; set; }
    }
}
