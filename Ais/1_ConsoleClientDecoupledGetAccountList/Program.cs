using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Shared;

namespace _1_ConsoleClientGetAccountList
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var token = await Aspsp.GetToken("accountinformation");
            var bicFi = "NDEASESS";
            var consentId = await Consent.CreateConsent(token, bicFi);
            var consentAuthorisationId = await Consent.StartConsentAuthorisationProcess(token, consentId, bicFi, "199311219639");
            var code = await Consent.UpdatePsuDataForConsent(token, consentId, consentAuthorisationId, bicFi);


            while (!await Consent.Until(token, consentId, consentAuthorisationId, bicFi))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
            
            var client = new HttpClient();
            var uri = new Uri($"{Settings.ApiUrl}/psd2/accountinformation/v1/accounts");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", Settings.IpAddress);
            client.DefaultRequestHeaders.Add("X-BicFi", bicFi);
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            client.DefaultRequestHeaders.Add("Consent-ID", consentId);
            
            var response = await client.GetStringAsync(uri);

            Console.WriteLine(response);
        }
    }
}
