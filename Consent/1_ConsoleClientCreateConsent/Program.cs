using System;
using System.Collections.Generic;
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
            var token = await Aspsp.GetToken("accountinformation");
            var client = new HttpClient();
            var uri = new Uri($"{Settings.ApiUrl}/psd2/consent/v1/consents");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", Settings.IpAddress);
            client.DefaultRequestHeaders.Add("X-BicFi", Settings.BicFi);
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("Accept", "*/*");

            var jsonObj = JsonConvert.SerializeObject(new
            {
                access = new {},
                recurringIndicator = true,
                validUntil = DateTime.Now.AddDays(4).ToString("yyyy-MM-dd"),
                frequencyPerDay = 500,
                combinedServiceIndicator = true
            }, Formatting.Indented);

            var response = await client.PostAsync(uri, new StringContent(jsonObj, Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine(json);
        }
    }
}
