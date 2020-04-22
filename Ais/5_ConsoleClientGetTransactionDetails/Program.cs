using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Shared;

namespace _5_ConsoleClientGetTransactionDetails
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var token = await Aspsp.GetToken("accountinformation");
            var consentId = await Consent.CreateConsent(token);
            var consentAuthorisationId = await Consent.StartConsentAuthorisationProcess(token, consentId);
            var url = await Consent.UpdatePsuDataForConsent(token, consentId, consentAuthorisationId);

            Console.WriteLine(url);

            // 9311219639
            // 9311219589
            // 8811215477
            // 8811212862
            // 8311211356

            Console.Write("Code: ");
            var code = Console.ReadLine();

            var consentToken = await Consent.GetToken("accountinformation", code, consentId, consentAuthorisationId);

            Console.WriteLine(consentToken);

            var client = new HttpClient();
            var accountId = "5a59028c-e757-4f22-b88c-3ba90573383c";
            var transactionId = "1630de32-07d4-41b1-b207-b673249a2275";
            var uri = new Uri(
                $"{Settings.ApiUrl}/psd2/accountinformation/v1/accounts/{accountId}/transactions/{transactionId}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", Settings.IpAddress);
            client.DefaultRequestHeaders.Add("X-BicFi", "ESSESESS");
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            client.DefaultRequestHeaders.Add("Consent-ID", consentId);

            var response = await client.GetStringAsync(uri);
            
            Console.WriteLine(response);
        }
    }
}
