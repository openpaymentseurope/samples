using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shared;

namespace _1_ConsoleClientCreatePaymentInitiation
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var paymentToken = await Aspsp.GetToken("paymentinitiation");
            var client = new HttpClient();
            var paymentProduct = "domestic";
            var uri = new Uri($"{Settings.ApiUrl}/psd2/paymentinitiation/v1/payments/{paymentProduct}");
            
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", paymentToken);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", Settings.IpAddress);
            client.DefaultRequestHeaders.Add("X-BicFi", "ESSESESS");
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            // creditor -> debtor
            var message = new
            {
                instructedAmount = new
                {
                    currency = "SEK",
                    amount = "10.0",
                },
                creditorAccount = new
                {
                    iban = "SE3150000000054400047989",
                    currency = "SEK"
                },                
                debtorAccount = new
                {
                    iban = "SE0950000000054400047997",
                    currency = "SEK"
                },
                creditorName = "Enterprise Inc",
                remittanceInformationUnstructured = "message",
            };
            var messageJson = JsonConvert.SerializeObject(message);
            var response =
                await client.PostAsync(uri, new StringContent(messageJson, Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine(json);
        }
    }
}
