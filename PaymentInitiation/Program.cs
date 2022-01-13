//////////////////////////////////////////////////////////////////////
//
// Open Payments Europe AB 2022
//
// Open Banking Platform - Payment Initiation Service
// 
// Payment Initiation example
//
//////////////////////////////////////////////////////////////////////

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
using Microsoft.Extensions.Configuration;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using QRCoder;

namespace PaymentInitiation
{
    class Program
    {
        private const string QRCodeHtmlFilename = "QRCode.html";

        //
        // Configuration settings
        //
        public class Settings
        {
            public string ClientId { get; set; }
            public string ClientSecret { get; set; }
            public string RedirectURI { get; set; }
            public bool UseProductionEnvironment { get; set; }
            public string ProductionClientCertificateFile { get; set; }
            public string PSUIPAddress { get; set; }
            public string PSUUserAgent { get; set; }
        }

        //
        // Payment context
        //
        public class Payment
        {
            public string BicFi { get; }
            public string AffiliatedASPSPId { get; }
            public string PSUId { get; }
            public string PSUCorporateId { get; }
            public string PaymentService { get; }
            public string PaymentProduct { get; }
            public string AuthenticationMethodId { get; }
            public string PaymentBody { get; }
            public string PaymentId { get; set; }
            public string PaymentAuthId { get; set; }
            public SCAMethod ScaMethod { get; set; }
            public SCAData ScaData { get; set; }

            public Payment(string bicFi, string affiliatedASPSPId, string psuId, string psuCorporateId, string paymentService, string paymentProduct, string authenticationMethodId, string paymentBody)
            {
                BicFi = bicFi;
                AffiliatedASPSPId = affiliatedASPSPId;
                PSUId = psuId;
                PSUCorporateId = psuCorporateId;
                PaymentService = paymentService;
                PaymentProduct = paymentProduct;
                AuthenticationMethodId = authenticationMethodId;
                PaymentBody = paymentBody;
            }
        }

        public class SCAData
        {
            public string RedirectUri { get; set; }
            public string Token { get; set; }
            public string Image { get; set; }
        }

        public enum SCAMethod
        {
            UNDEFINED = 1,
            OAUTH_REDIRECT,
            REDIRECT,
            DECOUPLED
        }

        private static string _authBaseUri;
        private static string _apiBaseUri;
        private static HttpClientHandler _apiClientHandler;
        private static Settings _settings;
        private static string _paymentinitiationScope;
        private static string _token;
        private static Payment _payment;

        static async Task Main(string[] args)
        {
            if (args.Length != 1)
            {
                Usage();
                return;
            }
            var paymentName = args[0];

            Init(paymentName);

            //
            // Get an API access token from auth server with the scope needed
            //
            _token = await GetToken(_settings.ClientId, _settings.ClientSecret, _paymentinitiationScope);
            Console.WriteLine($"token: {_token}");
            Console.WriteLine();

            //
            // Create the payment
            //
            _payment.PaymentId = await CreatePaymentInitiation(_payment.BicFi, _settings.PSUIPAddress, _settings.PSUUserAgent, _payment.PSUCorporateId, _payment.PaymentService, _payment.PaymentProduct, _payment.PaymentBody, _payment.AffiliatedASPSPId);
            Console.WriteLine($"paymentId: {_payment.PaymentId}");
            Console.WriteLine();

            //
            // Create a payment authorization object to be used for authorizing the payment with the end user
            //
            _payment.PaymentAuthId = await StartPaymentInitiationAuthorisationProcess(_payment.BicFi, _settings.PSUIPAddress, _settings.PSUUserAgent, _payment.PSUCorporateId, _payment.PaymentService, _payment.PaymentProduct, _payment.PaymentId);
            Console.WriteLine($"authId: {_payment.PaymentAuthId}");
            Console.WriteLine();

            //
            // Start the payment authorization process with the end user
            //
            string scaStatus;
            (_payment.ScaMethod, scaStatus, _payment.ScaData) = await UpdatePSUDataForPaymentInitiation(_payment.BicFi, _settings.PSUIPAddress, _settings.PSUUserAgent, _payment.PSUId, _payment.PSUCorporateId, _payment.PaymentService, _payment.PaymentProduct, _payment.PaymentId, _payment.PaymentAuthId, _payment.AuthenticationMethodId);
            Console.WriteLine($"scaMethod: {_payment.ScaMethod}");
            Console.WriteLine($"data: {_payment.ScaData}");
            Console.WriteLine();

            bool scaSuccess;
            if (_payment.ScaMethod == SCAMethod.OAUTH_REDIRECT || _payment.ScaMethod == SCAMethod.REDIRECT)
            {
                //
                // Bank uses a redirect flow for Strong Customer Authentication
                //
                scaSuccess = await SCAFlowRedirect(_payment, scaStatus, "MyState");
            }
            else if (_payment.ScaMethod == SCAMethod.DECOUPLED)
            {
                //
                // Bank uses a decoupled flow for Strong Customer Authentication
                //
                scaSuccess = await SCAFlowDecoupled(_payment, scaStatus);
            }
            else
            {
                throw new Exception($"ERROR: unknown SCA method {_payment.ScaMethod}");
            }

            if (!scaSuccess)
            {
                Console.WriteLine("SCA failed");
                Console.WriteLine();
                return;
            }

            Console.WriteLine("SCA completed successfully");
            Console.WriteLine();

            //
            // Check the status of the payment, for this example until it changes from the initial
            // "RCVD" status to anything else
            //
            var transactionStatus = await GetPaymentInitiationStatus(_payment.BicFi, _settings.PSUIPAddress, _settings.PSUUserAgent, _payment.PSUCorporateId, _payment.PaymentService, _payment.PaymentProduct, _payment.PaymentId);
            Console.WriteLine($"transactionStatus: {transactionStatus}");
            Console.WriteLine();
            while (transactionStatus.Equals("RCVD"))
            {
                await Task.Delay(1000);
                transactionStatus = await GetPaymentInitiationStatus(_payment.BicFi, _settings.PSUIPAddress, _settings.PSUUserAgent, _payment.PSUCorporateId, _payment.PaymentService, _payment.PaymentProduct, _payment.PaymentId);
                Console.WriteLine($"transactionStatus: {transactionStatus}");
                Console.WriteLine();
            }
        }

        private static void Usage()
        {
            Console.WriteLine("Usage: PaymentInitiation <payment name>");
            Console.WriteLine();
            Console.WriteLine("Available payment names:");
            
            var paymentNameList = string.Join($"{Environment.NewLine}", GetAvailablePaymentNames().Select(p => $" * {p}"));
            Console.WriteLine(paymentNameList);
        }

        private static void Init(string paymentName)
        {
            //
            // Read configuration
            //
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddJsonFile("appsettings.json", false, false);
            var config = configurationBuilder.Build();
            _settings = config.Get<Settings>();

            _apiClientHandler = new HttpClientHandler();

            //
            // Set up for different environments
            //
            if (_settings.UseProductionEnvironment)
            {
                Console.WriteLine("Using production");
                Console.WriteLine();
                _authBaseUri = "https://auth.openbankingplatform.com";
                _apiBaseUri = "https://api.openbankingplatform.com";

                Console.Write("Enter Certificate Password: ");
                var certPassword = ConsoleReadPassword();
                Console.WriteLine();

                var certificate = new X509Certificate2(_settings.ProductionClientCertificateFile, certPassword);
                _apiClientHandler.ClientCertificates.Add(certificate);
            }
            else
            {
                Console.WriteLine("Using sandbox");
                Console.WriteLine();
                _authBaseUri = "https://auth.sandbox.openbankingplatform.com";
                _apiBaseUri = "https://api.sandbox.openbankingplatform.com";
            }

            //
            // Read payments configuration and pick the chosen payment to initiate 
            //
            var jsonString = File.ReadAllText("payments.json");
            var payments = JsonConvert.DeserializeObject<dynamic>(jsonString);
            foreach (var item in payments)
            {
                string name = item.Name;
                if (name.Equals(paymentName, StringComparison.OrdinalIgnoreCase))
                {
                    _payment = new Payment((string)item.BICFI,
                                           (string)item.AffiliatedASPSPId,
                                           (string)item.PSUId,
                                           (string)item.PSUCorporateId,
                                           (string)item.PaymentService,
                                           (string)item.PaymentProduct,
                                           (string)item.AuthenticationMethodId,
                                           JsonConvert.SerializeObject(item.Payment, Formatting.None));
                    break;
                }
            }
            if (_payment?.PaymentBody == null)
            {
                throw new Exception($"ERROR: payment {paymentName} not found");
            }

            _paymentinitiationScope = string.IsNullOrEmpty(_payment.PSUCorporateId) ? "private paymentinitiation" : "corporate paymentinitiation";
        }

        private static string ConsoleReadPassword()
        {
            var password = "";
            var ch = Console.ReadKey(true);
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

        private static string FormatBankIdURL(string autostartToken, string redirectUri)
        {
            if (string.IsNullOrEmpty(redirectUri))
            {
                return $"https://app.bankid.com/?autostarttoken={autostartToken}&redirect=null";
            }
            return $"https://app.bankid.com/?autostarttoken={autostartToken}&redirect={WebUtility.UrlEncode(redirectUri)}";
        }

        //
        // Generates an html file with embedded QR-code image and opens it with default application
        //
        private static void DisplayQRCode(string url, string image, string title = null)
        {
            var html = "<html><style>h1 {{text-align: center;}} .center {{ display: block; margin-left: auto; margin-right: auto;}}</style><body><!--TITLEHTML--><!--IMGHTML--></body></html>";
            if (!string.IsNullOrEmpty(title))
            {
                html = html.Replace("<!--TITLEHTML-->", $"<p><h1>{title}</h1></p>");
            }

            if (!string.IsNullOrEmpty(url))
            {
                var qrGenerator = new QRCodeGenerator();
                var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
                var imgType = Base64QRCode.ImageType.Png;
                var qrCode = new Base64QRCode(qrCodeData);
                var qrCodeImageAsBase64 = qrCode.GetGraphic(20, Color.Black, Color.White, true, imgType);
                html = html.Replace("<!--IMGHTML-->", $"<img alt=\"Embedded QR Code\" class=\"center\" width=\"500\" height=\"500\" src=\"data:image/{imgType.ToString().ToLower()};base64,{qrCodeImageAsBase64}\"/>");
            }
            else if (!string.IsNullOrEmpty(image))
            {
                html = html.Replace("<!--IMGHTML-->", $"<img alt=\"Embedded QR Code\" class=\"center\" width=\"500px\" height=\"500px\" src=\"{image}\"/>");
            }

            using (var outputFile = new StreamWriter(Path.GetFullPath(".") + "/" + QRCodeHtmlFilename))
            {
                outputFile.WriteLine(html);
            }
            var qrCodeUrl = "file://" + Path.GetFullPath(".") + "/" + QRCodeHtmlFilename;
            OpenBrowser(qrCodeUrl);
        }

        //
        // Will poll the SCA status indefinitely with given period until status is either "finalised", "failed" or "exempted"
        //
        private static async Task<bool> PollSCAStatus(Payment payment, string initialSCAStatus, int millisecondsDelay)
        {
            var previousScaStatus = "";
            var scaStatus = initialSCAStatus;
            var scaData = payment.ScaData;
            var psuMessage = GetPSUMessage(scaStatus);

            Console.WriteLine($"scaStatus: {scaStatus}");
            Console.WriteLine();
            while (!scaStatus.Equals("finalised") && !scaStatus.Equals("failed") && !scaStatus.Equals("exempted"))
            {
                Console.WriteLine($"scaStatus: {scaStatus}");
                Console.WriteLine();

                if (!scaStatus.Equals(previousScaStatus))
                {
                    if (scaStatus.Equals("started") || scaStatus.Equals("authenticationStarted") || scaStatus.Equals("authoriseCreditorAccountStarted"))
                    {
                        var bankIdUrl = FormatBankIdURL("", "");
                        if (!string.IsNullOrEmpty(scaData.Token))
                        {
                            bankIdUrl = FormatBankIdURL(scaData.Token, "");
                            DisplayQRCode(bankIdUrl, "", psuMessage);
                        }
                        else if (!string.IsNullOrEmpty(scaData.Image))
                        {
                            DisplayQRCode("", scaData.Image, psuMessage);
                        }
                        else
                        {
                            DisplayQRCode(bankIdUrl, "", psuMessage);
                        }
                    }
                    previousScaStatus = scaStatus;
                }
                else if (!string.IsNullOrEmpty(scaData.Image))
                {
                    DisplayQRCode("", scaData.Image, psuMessage);
                }
                await Task.Delay(millisecondsDelay);

                (scaStatus, scaData) = await GetPaymentInitiationAuthorisationSCAStatus(payment.BicFi, _settings.PSUIPAddress, _settings.PSUUserAgent, payment.PSUCorporateId, payment.PaymentService, payment.PaymentProduct, payment.PaymentId, payment.PaymentAuthId);
                Console.WriteLine($"scaStatus: {scaStatus}");
                Console.WriteLine();
                psuMessage = GetPSUMessage(scaStatus);
            }
            DisplayQRCode("", "", psuMessage);
            if (scaStatus.Equals("failed"))
            {
                return false;
            }

            return true;
        }

        private static string GetPSUMessage(string scaStatus)
        {
            if (scaStatus.Equals("authenticationStarted"))
            {
                return "Please authenticate";
            }
            if (scaStatus.Equals("authoriseCreditorAccountStarted"))
            {
                return "Please approve the creditor account";
            }
            if (scaStatus.Equals("started"))
            {
                return "Please sign the payment";
            }
            if (scaStatus.Equals("finalised"))
            {
                return "SCA Finalised";
            }
            if (scaStatus.Equals("exempted"))
            {
                return "SCA Exempted";
            }
            if (scaStatus.Equals("failed"))
            {
                return "SCA Failed";
            }
            return "";
        }

        //
        // Starts a redirect flow for SCA by opening SCA URL in default browser (for end user to authenticate),
        // then prompts for authorisation code returned in final redirect query parameter "code".
        // (prompting for this is because of the simplicity of this example application that is not implementing a http server)
        //
        private static async Task<bool> SCAFlowRedirect(Payment payment, string scaStatus, string state)
        {
            //
            // Fill in the details on the given redirect URL template
            //
            var url = payment.ScaData.RedirectUri.Replace("[CLIENT_ID]", _settings.ClientId).Replace("[TPP_REDIRECT_URI]", WebUtility.UrlEncode(_settings.RedirectURI)).Replace("[TPP_STATE]", WebUtility.UrlEncode(state));
            Console.WriteLine($"URL: {url}");
            Console.WriteLine();

            OpenBrowser(url);

            //
            // If flow is OAuthRedirect, authorisation code needs to be activated
            //
            if (payment.ScaMethod == SCAMethod.OAUTH_REDIRECT)
            {
                Console.Write("Enter authorisation code returned by redirect query param: ");
                var authCode = Console.ReadLine();
                Console.WriteLine();

                var newToken = await ActivateOAuthPaymentAuthorisation(payment.PaymentId, payment.PaymentAuthId, _settings.ClientId, _settings.ClientSecret, _settings.RedirectURI, _paymentinitiationScope, authCode);
                Console.WriteLine();
                if (string.IsNullOrEmpty(newToken))
                    return false;
            }

            //
            // Wait for a final SCA status
            //
            return await PollSCAStatus(payment, scaStatus, 2000);
        }

        //
        // Handles a decoupled flow by formatting a BankId URL, presenting it as an QR-code to be scanned
        // with BankId, then polling for a final SCA status of the authentication/auhorisation
        //
        private static async Task<bool> SCAFlowDecoupled(Payment payment, string scaStatus)
        {
            return await PollSCAStatus(payment, scaStatus, 1000);
        }

        //
        // Create a http client with the basic common attributes set for a request to auth server
        //
        private static HttpClient CreateGenericAuthClient()
        {
            var authClient = new HttpClient();
            authClient.BaseAddress = new Uri(_authBaseUri);
            authClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return authClient;
        }

        //
        // Create a http client with the basic common attributes set for a request to API:s
        //
        private static HttpClient CreateGenericApiClient(string bicFi, string psuIPAddress, string psuUserAgent, string psuCorporateId)
        {
            var apiClient = new HttpClient(_apiClientHandler);
            apiClient.BaseAddress = new Uri(_apiBaseUri);
            apiClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            var xRequestId = Guid.NewGuid().ToString();
            Console.WriteLine($"X-Request-ID: {xRequestId}");
            apiClient.DefaultRequestHeaders.Add("X-Request-ID", xRequestId);
            apiClient.DefaultRequestHeaders.Add("X-BicFi", bicFi);
            apiClient.DefaultRequestHeaders.Add("PSU-IP-Address", psuIPAddress);
            apiClient.DefaultRequestHeaders.Add("PSU-User-Agent", psuUserAgent);
            apiClient.DefaultRequestHeaders.Add("TPP-Redirect-Preferred", "false");
            if (!string.IsNullOrEmpty(psuCorporateId))
                apiClient.DefaultRequestHeaders.Add("PSU-Corporate-Id", psuCorporateId);

            return apiClient;
        }

        private static IEnumerable<string> GetAvailablePaymentNames()
        {
            var jsonString = File.ReadAllText("payments.json");
            var payments = JsonConvert.DeserializeObject<dynamic>(jsonString);

            foreach (var payment in payments)
            {
                yield return payment.Name.ToString();
            }
        }

        private static async Task<string> GetToken(string clientId, string clientSecret, string scope)
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
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"statusCode: {(int)response.StatusCode}");
            Console.WriteLine($"responseBody: {responseContent}");
            Console.WriteLine();

            var responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.access_token;
        }

        private static async Task<string> ActivateOAuthPaymentAuthorisation(string paymentId, string authId, string clientId, string clientSecret, string redirectUri, string scope, string authCode)
        {
            Console.WriteLine("Activate OAuth Payment Authorisation");
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
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");
            Console.WriteLine();

            var responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.access_token;
        }

        private static async Task<string> CreatePaymentInitiation(string bicFi, string psuIPAddress, string psuUserAgent, string psuCorporateId, string paymentService, string paymentProduct, string jsonPaymentBody, string affiliatedASPSPId)
        {
            Console.WriteLine("Create Payment Initiation");
            var apiClient = CreateGenericApiClient(bicFi, psuIPAddress, psuUserAgent, psuCorporateId);
            if (affiliatedASPSPId != null)
                apiClient.DefaultRequestHeaders.Add("X-AffiliatedASPSP-ID", affiliatedASPSPId);

            Console.WriteLine($"requestBody: {jsonPaymentBody}");
            var response = await apiClient.PostAsync($"/psd2/paymentinitiation/v1/{paymentService}/{paymentProduct}", new StringContent(jsonPaymentBody, Encoding.UTF8, "application/json"));
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");
            Console.WriteLine();

            var responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.paymentId;
        }

        private static async Task<string> StartPaymentInitiationAuthorisationProcess(string bicFi, string psuIPAddress, string psuUserAgent, string psuCorporateId, string paymentService, string paymentProduct, string paymentId)
        {
            Console.WriteLine("Start Payment Initiation Authorisation Process");
            var apiClient = CreateGenericApiClient(bicFi, psuIPAddress, psuUserAgent, psuCorporateId);

            var jsonBody = "";
            var response = await apiClient.PostAsync($"/psd2/paymentinitiation/v1/{paymentService}/{paymentProduct}/{paymentId}/authorisations", new StringContent(jsonBody, Encoding.UTF8, "application/json"));
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");
            Console.WriteLine();

            var responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.authorisationId;
        }

        private static async Task<(SCAMethod, string, SCAData)> UpdatePSUDataForPaymentInitiation(string bicFi, string psuIPAddress, string psuUserAgent, string psuId, string psuCorporateId, string paymentService, string paymentProduct, string paymentId, string authId, string authenticationMethodId)
        {
            Console.WriteLine("Update PSU Data For Payment Initiation");
            var apiClient = CreateGenericApiClient(bicFi, psuIPAddress, psuUserAgent, psuCorporateId);
            if (!string.IsNullOrEmpty(psuId))
                apiClient.DefaultRequestHeaders.Add("PSU-ID", psuId);

            var jsonBody = $"{{\"authenticationMethodId\": \"{authenticationMethodId}\"}}";

            var response = await apiClient.PutAsync($"/psd2/paymentinitiation/v1/{paymentService}/{paymentProduct}/{paymentId}/authorisations/{authId}", new StringContent(jsonBody, Encoding.UTF8, "application/json"));
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");
            Console.WriteLine();

            var responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            string scaStatus = responseBody.scaStatus;

            var scaData = new SCAData();
            var headerValues = response.Headers.GetValues("aspsp-sca-approach");
            var scaApproach = headerValues.FirstOrDefault();
            var method = SCAMethod.UNDEFINED;
            if (scaApproach.Equals("REDIRECT"))
            {
                try
                {
                    scaData.RedirectUri = responseBody._links.scaOAuth.href;
                    method = SCAMethod.OAUTH_REDIRECT;
                }
                catch (RuntimeBinderException)
                {
                    try
                    {
                        scaData.RedirectUri = responseBody._links.scaRedirect.href;
                        method = SCAMethod.REDIRECT;
                    }
                    catch (RuntimeBinderException)
                    {
                    }
                }
            }
            else if (scaApproach.Equals("DECOUPLED"))
            {
                method = SCAMethod.DECOUPLED;
                try
                {
                    scaData.Token = responseBody.challengeData.data[0];
                }
                catch (RuntimeBinderException)
                {
                    try
                    {
                        scaData.Image = responseBody.challengeData.image;
                    }
                    catch (RuntimeBinderException)
                    {
                    }
                }
            }
            return (method, scaStatus, scaData);
        }

        private static async Task<(string, SCAData)> GetPaymentInitiationAuthorisationSCAStatus(string bicFi, string psuIPAddress, string psuUserAgent, string psuCorporateId, string paymentService, string paymentProduct, string paymentId, string authId)
        {
            Console.WriteLine("Get Payment Initiation Authorisation SCA Status");
            var apiClient = CreateGenericApiClient(bicFi, psuIPAddress, psuUserAgent, psuCorporateId);

            var response = await apiClient.GetAsync($"/psd2/paymentinitiation/v1/{paymentService}/{paymentProduct}/{paymentId}/authorisations/{authId}");
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");
            Console.WriteLine();

            var responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);
            var scaData = new SCAData();
            if (responseBody.challengeData != null && responseBody.challengeData.data != null)
            {
                scaData.Token = responseBody.challengeData.data[0];
            }
            else if (responseBody.challengeData != null && responseBody.challengeData.image != null)
            {
                scaData.Image = responseBody.challengeData.image;
            }
            return (responseBody.scaStatus, scaData);
        }

        private static async Task<string> GetPaymentInitiationStatus(string bicFi, string psuIPAddress, string psuUserAgent, string psuCorporateId, string paymentService, string paymentProduct, string paymentId)
        {
            Console.WriteLine("Get Payment Initiation Status");
            var apiClient = CreateGenericApiClient(bicFi, psuIPAddress, psuUserAgent, psuCorporateId);

            var response = await apiClient.GetAsync($"/psd2/paymentinitiation/v1/{paymentService}/{paymentProduct}/{paymentId}/status");
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");
            Console.WriteLine();

            var responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.transactionStatus;
        }
    }
}
