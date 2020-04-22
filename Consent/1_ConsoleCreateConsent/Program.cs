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
            var token = await Aspsp.GetToken();
            token =
                "eyJhbGciOiJSUzI1NiIsImtpZCI6IjhBMzA3RTcxMkJFQzUwNTM1MjQ4Njk0QjEyRTJFOEFDMDBEMjdCNUEiLCJ0eXAiOiJKV1QiLCJ4NXQiOiJpakItY1N2c1VGTlNTR2xMRXVMb3JBRFNlMW8ifQ.eyJuYmYiOjE1ODc1NDk3MDcsImV4cCI6MTU4NzU1MzMwNywiaXNzIjoiaHR0cHM6Ly9hdXRoLnNhbmRib3gub3BlbmJhbmtpbmdwbGF0Zm9ybS5jb20iLCJhdWQiOlsiaHR0cHM6Ly9hdXRoLnNhbmRib3gub3BlbmJhbmtpbmdwbGF0Zm9ybS5jb20vcmVzb3VyY2VzIiwiYXNwc3BpbmZvcm1hdGlvbiJdLCJjbGllbnRfaWQiOiI5N2U1YTVlMS1lZDgxLTQxYzYtYTJhMC02OTVhOTYxN2FhNTUiLCJzY29wZSI6WyJhY2NvdW50aW5mb3JtYXRpb24iXX0.IZyuq8X8HcIAWgX3dt4uRHB4rAimdtfhaEBcjx9NtM9q6nKkWlJL9_st1iz0NRx6XXNZxrfGHlr3wB5CJCldJ8NxbrValRspOn9qE8omUfYgQej-1Ki1b0QsJD3MA7G-Xs-n7VRr62eUvjnPro42PyupiGOb588dRss4aSFqr1IHfHN5mCvtBwYCZJBf7k5FbMYMs2ixxXXpWDL3HsW_FBXBMPcZgfDcZAM2J52azHh8-NXEmibGaqyIKjoD9VNhazN4WqHr5zWl5FxfrL6ogb6AbMZziV1Tatbb640aqHXvtxrGhxaENxyrSW4DkxG5A3a-MbrblOR0OHtfzlr2Wg"; 
            var client = new HttpClient();

            var uri = new Uri($"{Settings.ApiUrl}/psd2/consent/v1/consents");
            // var uri = new Uri($"http://192.168.1.2:5000/psd2/consent/v1/consents");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", "83.226.130.89");
            client.DefaultRequestHeaders.Add("X-BicFi", "ESSESESS");
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("Accept", "*/*");

            var jsonObj = JsonConvert.SerializeObject(new
            {
                access = new
                {
                    accounts = new List<object>(),
                    balances = new List<object>(),
                    transactions = new List<object>(),
                },
                recurringIndicator = true,
                validUntil = "2020-04-30",
                frequencyPerDay = 500,
                combinedServiceIndicator = true
            }, Formatting.Indented);

            var response = await client.PostAsync(uri, new StringContent(jsonObj, Encoding.UTF8, "application/json"));
            // var response = await client.PostAsync(uri, new StringContent(jsonObj));
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine(json);
        }
    }
}
