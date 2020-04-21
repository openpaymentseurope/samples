﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shared;

namespace ConsoleClientCitiesDetails
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var token = await GetToken();
            var client = new HttpClient();
            var cityCode = "37efa883-c8ad-4ff7-927b-b11b02beb923";
            var uri = new Uri($"{Settings.ApiUrl}/psd2/aspspinformation/v1/cities/{cityCode}");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            var response = await client.GetAsync(uri);
            var json = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine(json);
        }

        private static async Task<string> GetToken()
        {
            var client = new HttpClient();
            var uri = new Uri($"{Settings.AuthUrl}/connect/token");
            var response = await client.PostAsync(uri, new FormUrlEncodedContent(
                new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("client_id", Settings.ClientId),
                    new KeyValuePair<string, string>("client_secret", Settings.Secret),
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("scope", "aspspinformation"),
                }));

            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<dynamic>(json);
            
            return obj.access_token;
        }
    }
}