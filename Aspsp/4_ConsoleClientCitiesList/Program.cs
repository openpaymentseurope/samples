using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Shared;

namespace ConsoleClientCityList
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var token = await Aspsp.GetToken();
            var client = new HttpClient();
            var uri = new Uri($"{Settings.ApiUrl}/psd2/aspspinformation/v1/cities");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            var response = await client.GetAsync(uri);
            var json = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine(json);
        }
    }
}