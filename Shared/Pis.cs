using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Shared
{
    public class Pis
    {
        public static async Task<string> CreatePaymentInitiation(string paymentToken)
        {
            var client = new HttpClient();
            var paymentProduct = "domestic";
            var uri = new Uri($"{Settings.ApiUrl}/psd2/paymentinitiation/v1/payments/{paymentProduct}");
            
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", paymentToken);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", Settings.IpAddress);
            client.DefaultRequestHeaders.Add("X-BicFi", Settings.BicFi);
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            // creditor -> debtor
            var message = new
            {
                instructedAmount = new
                {
                    currency = "SEK",
                    amount = "10.0",
                },
                creditorAccount = new
                {
                    iban = Settings.CreditorAccount,
                    currency = "SEK"
                },                
                debtorAccount = new
                {
                    iban = Settings.DebtorAccount,
                    currency = "SEK"
                },
                creditorName = "Enterprise Inc",
                remittanceInformationUnstructured = "message",
            };
            var messageJson = JsonConvert.SerializeObject(message);
            var response =
                await client.PostAsync(uri, new StringContent(messageJson, Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<JObject>(json);

            return obj.GetValue("paymentId").Value<string>();            
        }

        public static async Task<(string authorisationId, string authenticationMethodId)>
            StartPaymentInitiationAuthorisationProcess(string paymentToken, string paymentId)
        {
            var client = new HttpClient();
            var paymentProduct = "domestic";
            var uri = new Uri($"{Settings.ApiUrl}/psd2/paymentinitiation/v1/payments/{paymentProduct}/{paymentId}/authorisations");
            
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", paymentToken);
            client.DefaultRequestHeaders.Add("PSU-IP-Address", Settings.IpAddress);
            client.DefaultRequestHeaders.Add("X-BicFi", Settings.BicFi);
            client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            var response = await client.PostAsync(uri, new StringContent("", Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<JObject>(json);
            var authorisationId = obj.GetValue("authorisationId").Value<string>();
            var authenticationMethodId = obj["scaMethods"][0]["authenticationMethodId"].Value<string>();
            
            return (authorisationId, authenticationMethodId); 
        }
    }
}