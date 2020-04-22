using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shared;

namespace _8_ConsoleClientUpdatePsuDataForConsent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var token = await Aspsp.GetToken("accountinformation");
            var consentId = await Consent.CreateConsent(token);
            var consentAuthorisationId = await Consent.StartConsentAuthorisationProcess(token, consentId);

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

            Console.WriteLine(json);
        }
    }
}
