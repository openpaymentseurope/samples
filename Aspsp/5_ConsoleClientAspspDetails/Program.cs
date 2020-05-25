using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Shared;

namespace ConsoleClientAspspDetails
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var token = await Aspsp.GetToken("aspspinformation private");
            var client = new HttpClient();
            var aspspCode = Settings.BicFi;
            var uri = new Uri($"{Settings.ApiUrl}/psd2/aspspinformation/v1/aspsps/{aspspCode}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            var response = await client.GetAsync(uri);
            var json = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine(json);
        }
    }
}