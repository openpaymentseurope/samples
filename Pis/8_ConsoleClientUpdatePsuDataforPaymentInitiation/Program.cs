using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shared;

namespace _8_ConsoleClientUpdatePsuDataforPaymentInitiation
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Vi får inte tillbaka något från anropet
            var paymentToken = await Aspsp.GetToken("paymentinitiation");
            var client = new HttpClient();
            var paymentProduct = "domestic";
            var paymentId = await Pis.CreatePaymentInitiation(paymentToken);
            var (authorisationId, authenticationMethodId) = await Pis.StartPaymentInitiationAuthorisationProcess(paymentToken);
            var uri = new Uri($"{Settings.ApiUrl}/psd2/paymentinitiation/v1/payments/{paymentProduct}/{paymentId}/authorisations/{authorisationId}");
            
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", paymentToken);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", Settings.IpAddress);
            client.DefaultRequestHeaders.Add("X-BicFi", "ESSESESS");
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            var message = new
            {
                authenticationMethodId = authenticationMethodId,
            };
            var jsonMessage = JsonConvert.SerializeObject(message);
            var response = await client.PutAsync(uri, new StringContent(jsonMessage, Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine(json);
        }
    }
}
