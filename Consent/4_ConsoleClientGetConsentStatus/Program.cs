using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Shared;

namespace _4_ConsoleClientGetConsentStatus
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var token = await Aspsp.GetToken("accountinformation");
            var consentId = await Consent.CreateConsent(token);

            var client = new HttpClient();
            var uri = new Uri($"{Settings.ApiUrl}/psd2/consent/v1/consents/{consentId}/status");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", Settings.IpAddress);
            client.DefaultRequestHeaders.Add("X-BicFi", "ESSESESS");
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("Accept", "*/*");

            var response = await client.GetAsync(uri);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine(json);
        }
    }
}
