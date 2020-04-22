using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Shared;

namespace _5_ConsoleClientStartPaymentInitiationAuthorisationProcess
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var paymentToken = await Aspsp.GetToken("paymentinitiation");
            var client = new HttpClient();
            var paymentProduct = "domestic";
            var paymentId = await Pis.CreatePaymentInitiation(paymentToken);
            var uri = new Uri($"{Settings.ApiUrl}/psd2/paymentinitiation/v1/payments/{paymentProduct}/{paymentId}/authorisations");
            
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", paymentToken);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", Settings.IpAddress);
            client.DefaultRequestHeaders.Add("X-BicFi", "ESSESESS");
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            var response = await client.PostAsync(uri, new StringContent("", Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine(json);
        }
    }
}
