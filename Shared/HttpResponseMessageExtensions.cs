using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Shared
{
    public static class HttpResponseMessageExtensions
    {
        /// <summary>
        /// Ensures that the response was successful (HTTP status code within the 200 range) and returns the access_token.
        /// If the response was not successful the program terminates.
        /// </summary>
        public async static Task<string> RequireToken(this HttpResponseMessage response)
        {
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to get token. Response from the API:{Environment.NewLine}{json}");
                Environment.Exit(0);
            }

            var obj = JsonConvert.DeserializeObject<dynamic>(json);
            return obj.access_token.ToString();
        }
    }
}
