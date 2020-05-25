using System;
using System.Threading.Tasks;

namespace _3_ConsoleClientCancelPaymentInitiation
{
    class Program
    {
        static async Task Main(string[] args)
        {
            throw new NotImplementedException("Not yet supported");
            // var paymentToken = await Aspsp.GetToken("paymentinitiation");
            // var client = new HttpClient();
            // var paymentProduct = "domestic";
            // var paymentId = await Pis.CreatePaymentInitiation(paymentToken);
            // var uri = new Uri($"{Settings.ApiUrl}/psd2/paymentinitiation/v1/payments/{paymentProduct}/{paymentId}");
            //
            // client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", paymentToken);
            // client.DefaultRequestHeaders.Add("PSU-IP-Address", Settings.IpAddress);
            // client.DefaultRequestHeaders.Add("X-BicFi", Settings.BicFi);
            // client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            // client.DefaultRequestHeaders.Add("Accept", "*/*");
            // var response = await client.DeleteAsync(uri);
            // var json = await response.Content.ReadAsStringAsync();
            //
            // Console.WriteLine(json);
        }
    }
}