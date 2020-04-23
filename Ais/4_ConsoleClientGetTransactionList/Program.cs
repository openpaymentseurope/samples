using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Shared;

namespace _4_ConsoleClientGetTransactionList
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
            Console.Write("Code: ");
            var code = Console.ReadLine();
            var consentToken = await Consent.GetToken("accountinformation", code, consentId, consentAuthorisationId);
            var client = new HttpClient();
            var accountId = Settings.AccountId;
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
            
            Console.WriteLine(response);
        }
    }
}
