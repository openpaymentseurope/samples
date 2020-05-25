using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Shared;

namespace ConsoleClientGetTokenWithCertificate
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var cert = new X509Certificate2(Settings.CertificateFile, Settings.CertificatePassword);
            var handler = new HttpClientHandler
            {
                SslProtocols = SslProtocols.Tls13, 
                ClientCertificateOptions = ClientCertificateOption.Manual
            };

            handler.ClientCertificates.Add(cert);

            var client = new HttpClient();
            var uri = new Uri($"{Settings.AuthUrl}/connect/token");
            var response = await client.PostAsync(uri, new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", Settings.ClientId),
                new KeyValuePair<string, string>("client_secret", Settings.Secret),
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", "aspspinformation private"),
            }));

            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine(json);
        }
    }
}
