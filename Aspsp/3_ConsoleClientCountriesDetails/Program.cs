using System;
using System.Net.Http;
using System.Threading.Tasks;
using Shared;

namespace ConsoleClientCountriesDEtails
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var token = await Aspsp.GetToken();
            var client = new HttpClient();
            var countryCode = "SE";
            var uri = new Uri($"{Settings.ApiUrl}/psd2/aspspinformation/v1/countries/{countryCode}");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            var response = await client.GetAsync(uri);
            var json = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine(json);
        }
    }
}