using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Services.Payment
{
    public class PayTRService : IPayTRService
    {
        private readonly string? _merchantId;
        private readonly string? _merchantKey;
        private readonly string? _merchantSalt;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly bool _testMode;

        private const string ApiUrl = "https://www.paytr.com/odeme/api/get-token";

        public PayTRService(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _merchantId   = config["PayTR:MerchantId"];
            _merchantKey  = config["PayTR:MerchantKey"];
            _merchantSalt = config["PayTR:MerchantSalt"];
            _testMode     = string.Equals(config["PayTR:TestMode"], "true", StringComparison.OrdinalIgnoreCase);
            _httpClientFactory = httpClientFactory;
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_merchantId) &&
            !string.IsNullOrWhiteSpace(_merchantKey) &&
            !string.IsNullOrWhiteSpace(_merchantSalt);

        public async Task<PayTRTokenResult> GetIframeTokenAsync(PayTRPaymentRequest req)
        {
            if (!IsConfigured)
                return new PayTRTokenResult { Success = false, Error = "PayTR yapılandırılmamış." };

            try
            {
                // PayTR hash: HMAC-SHA256(merchantId + userIp + merchantOid + email + amount + currency + testMode, key + salt)
                var hashStr = _merchantId + req.UserIp + req.MerchantOid + req.Email
                    + req.AmountKurus.ToString() + req.Currency + (_testMode ? "1" : "0");

                var paytrToken = ComputeHmacSha256(hashStr, _merchantKey! + _merchantSalt!);

                var formData = new Dictionary<string, string>
                {
                    ["merchant_id"]      = _merchantId!,
                    ["user_ip"]          = req.UserIp,
                    ["merchant_oid"]     = req.MerchantOid,
                    ["email"]            = req.Email,
                    ["payment_amount"]   = req.AmountKurus.ToString(),
                    ["paytr_token"]      = paytrToken,
                    ["user_basket"]      = req.BasketJson,
                    ["debug_on"]         = "1",
                    ["no_installment"]   = "0",
                    ["max_installment"]  = "0",
                    ["user_name"]        = req.UserName,
                    ["user_address"]     = req.UserAddress,
                    ["user_phone"]       = req.UserPhone,
                    ["merchant_ok_url"]  = req.OkUrl,
                    ["merchant_fail_url"]= req.FailUrl,
                    ["timeout_limit"]    = "30",
                    ["currency"]         = req.Currency,
                    ["test_mode"]        = _testMode ? "1" : "0",
                    ["lang"]             = "tr",
                    ["client_lang"]      = "tr",
                };

                var client = _httpClientFactory.CreateClient();
                var response = await client.PostAsync(ApiUrl, new FormUrlEncodedContent(formData));
                var body = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("status", out var statusEl) &&
                    statusEl.GetString() == "success" &&
                    root.TryGetProperty("token", out var tokenEl))
                {
                    return new PayTRTokenResult { Success = true, Token = tokenEl.GetString() };
                }

                var reason = root.TryGetProperty("reason", out var reasonEl) ? reasonEl.GetString() : body;
                return new PayTRTokenResult { Success = false, Error = reason };
            }
            catch (Exception ex)
            {
                return new PayTRTokenResult { Success = false, Error = ex.Message };
            }
        }

        public bool VerifyCallback(string merchantOid, string status, string totalAmount, string hash)
        {
            if (!IsConfigured) return false;

            // PayTR callback hash: HMAC-SHA256(merchantOid + merchantSalt + status + totalAmount, merchantKey)
            var hashStr = merchantOid + _merchantSalt + status + totalAmount;
            var expected = ComputeHmacSha256(hashStr, _merchantKey!);
            return string.Equals(expected, hash, StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeHmacSha256(string data, string key)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash);
        }
    }
}
