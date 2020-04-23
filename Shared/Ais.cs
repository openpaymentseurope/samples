using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Shared
{
    public class Ais
    {
        public static async Task<IEnumerable<string>> GetAccountList(string token, string consentId)
        {
            var client = new HttpClient();
            var uri = new Uri($"{Settings.ApiUrl}/psd2/accountinformation/v1/accounts");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", Settings.IpAddress);
            client.DefaultRequestHeaders.Add("X-BicFi", Settings.BicFi);
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            client.DefaultRequestHeaders.Add("Consent-ID", consentId);

            var json = await client.GetStringAsync(uri);

            var obj = JsonConvert.DeserializeObject<JObject>(json);

            return obj["accounts"].Select(a => a["resourceId"].Value<string>());
        }

        public static async Task<IEnumerable<string>> GetTransactionList(string token, string consentId, string accountId)
        {
            var client = new HttpClient();
            var bookingStatus = "both";
            var dateFrom = new DateTime(2019, 1, 1).ToString("yyyy-MM-dd");
            var uri = new Uri(
                $"{Settings.ApiUrl}/psd2/accountinformation/v1/accounts/{accountId}/transactions?bookingStatus={bookingStatus}&dateFrom={dateFrom}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", Settings.IpAddress);
            client.DefaultRequestHeaders.Add("X-BicFi", Settings.BicFi);
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            client.DefaultRequestHeaders.Add("Consent-ID", consentId);

            var response = await client.GetStringAsync(uri);

            var obj = JsonConvert.DeserializeObject<JObject>(response);

            var transactions = obj["transactions"];
            var booked = transactions["booked"].Select(b => b["transactionId"].Value<string>());
            var pending = transactions["pending"].Select(b => b["transactionId"].Value<string>());

            return booked.Concat(pending);
        }
    }
}