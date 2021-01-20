using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Configuration;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using QRCoder;

namespace PaymentInitiation
{
    class Program
    {
        private const string QRCodeImageFilename = "QRCode.png";

        public class Settings
        {
            public string ClientId { get; set; }
            public string RedirectURI { get; set; }
            public bool UseProductionEnvironment { get; set; }
            public string ProductionClientCertificateFile { get; set; }
            public string PSUIPAddress { get; set; }
            public string PSUUserAgent { get; set; }
        }

        public class Payment
        {
            public string bicFi;
            public string paymentBody;
            public string paymentService;
            public string paymentProduct;
            public string paymentId;
            public string paymentAuthId;
            public SCAMethod scaMethod;
            public string scaData;
        }

        public enum SCAMethod
        {
            UNDEFINED,
            OAUTH_REDIRECT,
            REDIRECT,
            DECOUPLED
        }

        private static String _authUri;
        private static String _apiUri;
        private static HttpClientHandler _apiClientHandler;
        private static string _paymentinitiationScope;
        private static string _psuIPAddress;
        private static string _psuUserAgent;
        private static string _psuCorporateId;
        private static string _clientId = "";
        private static string _clientSecret = "";
        private static string _redirectUri = "";
        private static string _token = "";
        private static Payment _payment;


        static void Init(string paymentName)
        {
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddJsonFile("appsettings.json", false, false);
            IConfigurationRoot config = configurationBuilder.Build();
            var settings = config.Get<Settings>();

            _clientId = settings.ClientId;
            _redirectUri = settings.RedirectURI;
            _psuIPAddress = settings.PSUIPAddress;
            _psuUserAgent = settings.PSUUserAgent;

            Console.WriteLine($"_psuIPAddress: {_psuIPAddress}");
            Console.WriteLine($"_psuUserAgent: {_psuUserAgent}");


            _payment = new Payment();

            var jsonString = File.ReadAllText("payments.json");
            dynamic payments = JsonConvert.DeserializeObject<dynamic>(jsonString);

            foreach (var item in payments)
            {
                string name = item.Name;
                if (name.Equals(paymentName, StringComparison.OrdinalIgnoreCase))
                {
                    _payment.bicFi = item.BICFI;
                    _paymentinitiationScope = $"{item.PSUContextScope} paymentinitiation";
                    _psuCorporateId = item.PSUContextScope.Equals("corporate") ? item.PSUContextScope : null;
                    _payment.paymentService = item.PaymentService;
                    _payment.paymentProduct = item.PaymentProduct;
                    _payment.paymentBody = JsonConvert.SerializeObject(item.Payment, Newtonsoft.Json.Formatting.None);
                    break;
                }
            }
            if (_payment.paymentBody == null)
            {
                throw new Exception($"ERROR: payment {paymentName} not found");
            }

            Console.Write("Enter your Client Secret: ");
            _clientSecret = ConsoleReadPassword();
            Console.WriteLine();

            _apiClientHandler = new HttpClientHandler();

            if (settings.UseProductionEnvironment)
            {
                Console.WriteLine("Using production");
                _authUri = "https://auth.openbankingplatform.com";
                _apiUri = "https://api.openbankingplatform.com";

                Console.Write("Enter Certificate Password: ");
                string certPassword = ConsoleReadPassword();
                Console.WriteLine();

                X509Certificate2 certificate = new X509Certificate2(settings.ProductionClientCertificateFile, certPassword,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet);
                _apiClientHandler.ClientCertificates.Add(certificate);
            }
            else
            {
                Console.WriteLine("Using sandbox");
                _authUri = "https://auth.sandbox.openbankingplatform.com";
                _apiUri = "https://api.sandbox.openbankingplatform.com";
            }

        }

        static void Usage()
        {
            Console.WriteLine("Usage: PaymentInitiation <payment name>");
        }

        static async Task Main(string[] args)
        {
            if (args.Length != 1)
            {
                Usage();
                return;
            }
            string paymentName = args[0];

            Init(paymentName);

            _token = await GetToken(_clientId, _clientSecret, _paymentinitiationScope);
            Console.WriteLine($"token: {_token}");
            Console.WriteLine();

            _payment.paymentId = await CreatePaymentInitiation(_payment.bicFi, _payment.paymentService, _payment.paymentProduct, _payment.paymentBody);
            Console.WriteLine($"paymentId: {_payment.paymentId}");
            Console.WriteLine();

            _payment.paymentAuthId = await StartPaymentInitiationAuthorisationProcess(_payment.bicFi, _payment.paymentService, _payment.paymentProduct, _payment.paymentId);
            Console.WriteLine($"authId: {_payment.paymentAuthId}");
            Console.WriteLine();

            (_payment.scaMethod, _payment.scaData) = await UpdatePSUDataForPaymentInitiation(_payment.bicFi, _payment.paymentService, _payment.paymentProduct, _payment.paymentId, _payment.paymentAuthId);
            Console.WriteLine($"scaMethod: {_payment.scaMethod}");
            Console.WriteLine($"data: {_payment.scaData}");
            Console.WriteLine();

            bool scaSuccess = false;
            if (_payment.scaMethod == SCAMethod.OAUTH_REDIRECT || _payment.scaMethod == SCAMethod.REDIRECT)
            {
                scaSuccess = await SCAFlowRedirect(_payment, "MyState");
            }
            else if (_payment.scaMethod == SCAMethod.DECOUPLED)
            {
                scaSuccess = await SCAFlowDecoupled(_payment);
            }
            else
            {
                throw new Exception("ERROR: unknown SCA method");
            }

            if (scaSuccess)
            {
                Console.WriteLine("SCA completed successfully");
                Console.WriteLine();

                string transactionStatus = "RCVD";
                while (transactionStatus.Equals("RCVD"))
                {
                    transactionStatus = await GetPaymentInitiationStatus(_payment.bicFi, _payment.paymentService, _payment.paymentProduct, _payment.paymentId);
                    Console.WriteLine($"transactionStatus: {transactionStatus}");
                    Console.WriteLine();
                    if (transactionStatus.Equals("RCVD"))
                        await Task.Delay(2000);
                }
            }
            else
            {
                Console.WriteLine("SCA failed");
                Console.WriteLine();
            }
        }

        private static string ConsoleReadPassword()
        {
            var password = "";
            ConsoleKeyInfo ch = Console.ReadKey(true);
            while (ch.Key != ConsoleKey.Enter)
            {
                password += ch.KeyChar;
                Console.Write('*');
                ch = Console.ReadKey(true);
            }
            return password;
        }

        private static void OpenBrowser(string url)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }

        private static string GenerateBankIdURL(string autostartToken, string redirectUri)
        {
            return $"bankid:///?autostarttoken={autostartToken}&redirect={redirectUri}";
        }

        private static void DisplayQRCode(string url)
        {
            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            QRCode qrCode = new QRCode(qrCodeData);
            Bitmap qrCodeImage = qrCode.GetGraphic(20);
            qrCodeImage.Save(QRCodeImageFilename, ImageFormat.Png);
            string qrCodeUrl = "file://" + Path.GetFullPath(".") + "/" + QRCodeImageFilename;
            Console.WriteLine($"qrCodeUrl: {qrCodeUrl}");
            OpenBrowser(qrCodeUrl);
        }

        private static async Task<bool> PollSCAStatus(Payment payment, int millisecondsDelay)
        {
            string scaStatus = "";
            while (!scaStatus.Equals("finalised") && !scaStatus.Equals("failed"))
            {
                scaStatus = await GetPaymentInitiationAuthorisationSCAStatus(_payment.bicFi, _payment.paymentService, _payment.paymentProduct, _payment.paymentId, _payment.paymentAuthId);
                Console.WriteLine($"scaStatus: {scaStatus}");
                Console.WriteLine();
                if (!scaStatus.Equals("finalised") && !scaStatus.Equals("failed"))
                    await Task.Delay(millisecondsDelay);
            }
            if (scaStatus.Equals("failed"))
                return false;

            return true;
        }

        private static async Task<bool> SCAFlowRedirect(Payment payment, string state)
        {
            string url = _payment.scaData.Replace("[CLIENT_ID]", _clientId).Replace("[TPP_REDIRECT_URI]", WebUtility.UrlEncode(_redirectUri)).Replace("[TPP_STATE]", WebUtility.UrlEncode(state));
            Console.WriteLine($"URL: {url}");
            Console.WriteLine();

            OpenBrowser(url);

            if (_payment.scaMethod == SCAMethod.OAUTH_REDIRECT)
            {
                Console.Write("Enter authentication code returned by redirect query param: ");
                string authCode = Console.ReadLine();
                Console.WriteLine();

                string newToken = await ActivateOAuthPaymentAuthorisation(_authUri, _payment.paymentId, _payment.paymentAuthId, _clientId, _clientSecret, _redirectUri, _paymentinitiationScope, authCode);
                Console.WriteLine();
                if (String.IsNullOrEmpty(newToken))
                    return false;
            }

            return await PollSCAStatus(payment, 2000);
        }

        private static async Task<bool> SCAFlowDecoupled(Payment payment)
        {
            string bankIdUrl = GenerateBankIdURL(_payment.scaData, WebUtility.UrlEncode("https://openpayments.io"));
            DisplayQRCode(bankIdUrl);

            return await PollSCAStatus(payment, 2000);
        }

        private static HttpClient CreateGenericAuthClient()
        {
            var authClient = new HttpClient();
            authClient.BaseAddress = new Uri(_authUri);
            authClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return authClient;
        }

        private static HttpClient CreateGenericApiClient(string bicFi)
        {
            var apiClient = new HttpClient(_apiClientHandler);
            apiClient.BaseAddress = new Uri(_apiUri);
            apiClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            apiClient.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            apiClient.DefaultRequestHeaders.Add("X-BicFi", bicFi);
            apiClient.DefaultRequestHeaders.Add("PSU-IP-Address", _psuIPAddress);
            if (!String.IsNullOrEmpty(_psuCorporateId))
                apiClient.DefaultRequestHeaders.Add("PSU-Corporate-Id", _psuCorporateId);

            return apiClient;
        }

        private static async Task<String> GetToken(string clientId, string clientSecret, string scope)
        {
            Console.WriteLine("Get Token");
            var authClient = CreateGenericAuthClient();

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", scope)
            });

            var response = await authClient.PostAsync("/connect/token", content);
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"statusCode: {(int)response.StatusCode}");
            Console.WriteLine($"responseBody: {responseContent}");

            dynamic responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.access_token;
        }

        private static async Task<string> ActivateOAuthPaymentAuthorisation(string authUri, string paymentId, string authId, string clientId, string clientSecret, string redirectUri, string scope, string authCode)
        {
            Console.WriteLine("Activate OAuth Consent Authorisation");
            var authClient = CreateGenericAuthClient();
            authClient.DefaultRequestHeaders.Add("X-PaymentId", paymentId);
            authClient.DefaultRequestHeaders.Add("X-PaymentAuthorisationId", authId);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("scope", scope),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", authCode)
            });

            var response = await authClient.PostAsync("/connect/token", content);
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");

            dynamic responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.access_token;
        }

        private static async Task<String> CreatePaymentInitiation(string bicFi, string paymentService, string paymentProduct, string jsonPaymentBody)
        {
            Console.WriteLine("Create Payment Initiation");
            var apiClient = CreateGenericApiClient(bicFi);
            apiClient.DefaultRequestHeaders.Add("PSU-User-Agent", _psuUserAgent);

            Console.WriteLine($"requestBody: {jsonPaymentBody}");
            var response = await apiClient.PostAsync($"/psd2/paymentinitiation/v1/{paymentService}/{paymentProduct}", new StringContent(jsonPaymentBody, Encoding.UTF8, "application/json"));
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");

            dynamic responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.paymentId;
        }

        private static async Task<String> StartPaymentInitiationAuthorisationProcess(string bicFi, string paymentService, string paymentProduct, string paymentId)
        {
            Console.WriteLine("Start Payment Initiation Authorisation Process");
            var apiClient = CreateGenericApiClient(bicFi);

            string jsonBody = "";
            var response = await apiClient.PostAsync($"/psd2/paymentinitiation/v1/{paymentService}/{paymentProduct}/{paymentId}/authorisations", new StringContent(jsonBody, Encoding.UTF8, "application/json"));
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");

            dynamic responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.authorisationId;
        }

        private static async Task<(SCAMethod, string)> UpdatePSUDataForPaymentInitiation(string bicFi, string paymentService, string paymentProduct, string paymentId, string authId)
        {
            Console.WriteLine("Update PSU Data For Payment Initiation");
            var apiClient = CreateGenericApiClient(bicFi);

            string jsonBody = "{\"authenticationMethodId\": \"mbid\"}";
            var response = await apiClient.PutAsync($"/psd2/paymentinitiation/v1/{paymentService}/{paymentProduct}/{paymentId}/authorisations/{authId}", new StringContent(jsonBody, Encoding.UTF8, "application/json"));
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");

            dynamic responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            string data = "";
            IEnumerable<string> headerValues = response.Headers.GetValues("aspsp-sca-approach");
            string scaApproach = headerValues.FirstOrDefault();
            SCAMethod method = SCAMethod.UNDEFINED;
            if (scaApproach.Equals("REDIRECT"))
            {
                try
                {
                    data = responseBody._links.scaOAuth.href;
                    method = SCAMethod.OAUTH_REDIRECT;
                }
                catch (RuntimeBinderException)
                {
                    try
                    {
                        data = responseBody._links.scaRedirect.href;
                        method = SCAMethod.REDIRECT;
                    }
                    catch (RuntimeBinderException)
                    {
                    }
                }
            }
            else if (scaApproach.Equals("DECOUPLED"))
            {
                data = responseBody.challengeData.data[0];
                method = SCAMethod.DECOUPLED;
            }

            return (method, data);
        }

        private static async Task<String> GetPaymentInitiationAuthorisationSCAStatus(string bicFi, string paymentService, string paymentProduct, string paymentId, string authId)
        {
            Console.WriteLine("Get Payment Initiation Authorisation SCA Status");
            var apiClient = CreateGenericApiClient(bicFi);

            var response = await apiClient.GetAsync($"/psd2/paymentinitiation/v1/{paymentService}/{paymentProduct}/{paymentId}/authorisations/{authId}");
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");

            dynamic responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.scaStatus;
        }

        private static async Task<String> GetPaymentInitiationStatus(string bicFi, string paymentService, string paymentProduct, string paymentId)
        {
            Console.WriteLine("Get Payment Initiation Status");
            var apiClient = CreateGenericApiClient(bicFi);

            var response = await apiClient.GetAsync($"/psd2/paymentinitiation/v1/{paymentService}/{paymentProduct}/{paymentId}/status");
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");

            dynamic responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.transactionStatus;
        }
    }
}
