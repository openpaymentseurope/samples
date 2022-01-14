﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Shared;

namespace ConsoleClientGetToken
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var client = new HttpClient();
            var uri = new Uri($"{Settings.AuthUrl}/connect/token");
            var response = await client.PostAsync(uri, new FormUrlEncodedContent(
                new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("client_id", Settings.ClientId),
                    new KeyValuePair<string, string>("client_secret", Settings.Secret),
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("scope", "private aspspinformation"),
                }));

            var token = await response.RequireToken();

            Console.WriteLine(token);
        }
    }
}