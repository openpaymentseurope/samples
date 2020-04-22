using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Shared;

namespace ConsoleClientCitiesDetails
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var token = await Aspsp.GetToken("aspspinformation");
            var client = new HttpClient();
            var cityCode = "37efa883-c8ad-4ff7-927b-b11b02beb923";
            var uri = new Uri($"{Settings.ApiUrl}/psd2/aspspinformation/v1/cities/{cityCode}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            var response = await client.GetAsync(uri);
            var json = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine(json);
        }
    }
}