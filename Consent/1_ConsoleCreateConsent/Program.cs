using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shared;

namespace _1_ConsoleCreateConsent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var token = await Aspsp.GetToken();
            var client = new HttpClient();

            var uri = new Uri($"{Settings.ApiUrl}/psd2/consent/v1/consents");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", "37.247.14.79");
            client.DefaultRequestHeaders.Add("X-BicFi", "ESSESESS");
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());

            var jsonObj = JsonConvert.SerializeObject(new
            {
                access = new {},
                recurringIndicator = true,
                validUntil = "2020-04-26",
                frequencyPerDay = 20,
                combinedServiceIndicator = true
            });

            var response = await client.PostAsync(uri, new StringContent(jsonObj, Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine(json);
        }
    }
}
