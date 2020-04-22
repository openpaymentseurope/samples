using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Shared
{
    public class Consent
    {
        public static async Task<string> CreateConsent(string token)
        {
            var client = new HttpClient();
            var uri = new Uri($"{Settings.ApiUrl}/psd2/consent/v1/consents");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", Settings.IpAddress);
            client.DefaultRequestHeaders.Add("X-BicFi", "ESSESESS");
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("Accept", "*/*");

            var jsonObj = JsonConvert.SerializeObject(new
            {
                access = new { },
                recurringIndicator = true,
                validUntil = DateTime.Now.AddDays(4).ToString("yyyy-MM-dd"),
                frequencyPerDay = 500,
                combinedServiceIndicator = true
            }, Formatting.Indented);

            var response = await client.PostAsync(uri, new StringContent(jsonObj, Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<JObject>(json);

            return obj.GetValue("consentId").Value<string>();
        }

        public static async Task<string> StartConsentAuthorisationProcess(string token, string consentId)
        {
            var client = new HttpClient();
            var uri = new Uri($"{Settings.ApiUrl}/psd2/consent/v1/consents/{consentId}/authorisations");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", Settings.IpAddress);
            client.DefaultRequestHeaders.Add("X-BicFi", "ESSESESS");
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("Accept", "*/*");

            var response = await client.PostAsync(uri, new StringContent("", Encoding.UTF8, "application/json"));

            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<JObject>(json);

            return obj.GetValue("authorisationId").Value<string>();
        }

        public static async Task<string> UpdatePsuDataForConsent(string token, string consentId, string consentAuthorisationId)
        {
            var client = new HttpClient();
            var uri = new Uri($"{Settings.ApiUrl}/psd2/consent/v1/consents/{consentId}/authorisations/{consentAuthorisationId}");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", Settings.IpAddress);
            client.DefaultRequestHeaders.Add("X-BicFi", "ESSESESS");
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("Accept", "*/*");

            var jsonObj = JsonConvert.SerializeObject(new
            {
                authenticationMethodId = "mbid",
            }, Formatting.Indented);

            var response = await client.PutAsync(uri, new StringContent(jsonObj, Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadAsStringAsync();

            var obj = JsonConvert.DeserializeObject<JObject>(json);

            var url = obj["_links"]["scaOAuth"]["href"].Value<string>();

            return url.Replace("[CLIENT_ID]", Settings.ClientId)
                .Replace("[TPP_REDIRECT_URI]", HttpUtility.UrlEncode(Settings.RedirectUrl))
                .Replace("[TPP_STATE]", "data");
        }

        public static async Task<string> GetToken(string scope, string code, string consentId, string consentAuthorisationId)
        {
            var client = new HttpClient();
            var uri = new Uri($"{Settings.AuthUrl}/connect/token");
            client.DefaultRequestHeaders.Add("X-ConsentId", consentId);
            client.DefaultRequestHeaders.Add("X-ConsentAuthorisationId", consentAuthorisationId);

            var response = await client.PostAsync(uri, new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", Settings.ClientId),
                new KeyValuePair<string, string>("client_secret", Settings.Secret),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("scope", scope),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", Settings.RedirectUrl),
            }));

            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<JObject>(json);

            return obj.GetValue("access_token").Value<string>();
        }
    }
}