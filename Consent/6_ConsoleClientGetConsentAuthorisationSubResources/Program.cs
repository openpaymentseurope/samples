using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Shared;

namespace _6_ConsoleClientGetConsentAuthorisationSubResources
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var token = await Aspsp.GetToken("accountinformation");
            var consentId = await Consent.CreateConsent(token);
            var _ = await Consent.StartConsentAuthorisationProcess(token, consentId);

            var client = new HttpClient();
            var uri = new Uri($"{Settings.ApiUrl}/psd2/consent/v1/consents/{consentId}/authorisations");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", Settings.IpAddress);
            client.DefaultRequestHeaders.Add("X-BicFi", Settings.BicFi);
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("Accept", "*/*");

            var response = await client.GetStringAsync(uri);

            Console.WriteLine(response);
        }
    }
}
