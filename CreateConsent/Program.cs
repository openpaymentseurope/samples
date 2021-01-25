//////////////////////////////////////////////////////////////////////
//
// Open Payments Europe AB 2021
//
// Open Banking Platform - Consent / Account Information Service
// 
// Create Consent example application
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
using System.Drawing.Imaging;
using Microsoft.Extensions.Configuration;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using QRCoder;

namespace CreateConsent
{
    class Program
    {
        private const string QRCodeImageFilename = "QRCode.png";

        //
        // Configuration settings
        //
        public class Settings
        {
            public string ClientId { get; set; }
            public string RedirectURI { get; set; }
            public string PSUContextScope { get; set; }
            public string PSUCorporateId { get; set; }
            public bool UseProductionEnvironment { get; set; }
            public string ProductionClientCertificateFile { get; set; }
            public string PSUIPAddress { get; set; }
            public string PSUUserAgent { get; set; }
        }

        //
        // Consent context
        //
        public class Consent
        {
            public string BicFi { get; }
            public string ConsentId { get; set; }
            public string ConsentAuthId { get; set; }
            public SCAMethod ScaMethod { get; set; }
            public string ScaData { get; set; }

            public Consent(string bicFi)
            {
                this.BicFi = bicFi;
            }
        }

        public enum SCAMethod
        {
            UNDEFINED = 1,
            OAUTH_REDIRECT,
            REDIRECT,
            DECOUPLED
        }

        private static String _authUri;
        private static String _apiUri;
        private static HttpClientHandler _apiClientHandler;
        private static string _accountinformationScope;
        private static string _psuIPAddress;
        private static string _psuUserAgent;
        private static string _psuCorporateId;
        private static string _clientId;
        private static string _clientSecret;
        private static string _redirectUri;
        private static string _token;
        private static Consent _consent;

        static async Task Main(string[] args)
        {
            if (args.Length != 1)
            {
                Usage();
                return;
            }
            string bicFi = args[0];

            Init(bicFi);

            //
            // Get an API access token from auth server with the scope needed
            //
            _token = await GetToken(_clientId, _clientSecret, _accountinformationScope);
            Console.WriteLine($"token: {_token}");
            Console.WriteLine();

            //
            // Create a consent valid for 1 day
            //
            DateTime validUntil = DateTime.Now.AddDays(1);
            _consent.ConsentId = await CreateConsent(_consent.BicFi, validUntil);
            Console.WriteLine($"consentId: {_consent.ConsentId}");
            Console.WriteLine();

            //
            // Create a consent authorization object to be used for authorizing the consent with the end user
            //
            _consent.ConsentAuthId = await StartConsentAuthorisationProcess(_consent.BicFi, _consent.ConsentId);
            Console.WriteLine($"authId: {_consent.ConsentAuthId}");
            Console.WriteLine();

            //
            // Start the consent authorization process with the end user
            //
            (_consent.ScaMethod, _consent.ScaData) = await UpdatePSUDataForConsent(_consent.BicFi, _consent.ConsentId, _consent.ConsentAuthId);
            Console.WriteLine($"scaMethod: {_consent.ScaMethod}");
            Console.WriteLine($"data: {_consent.ScaData}");
            Console.WriteLine();

            bool scaSuccess;
            if (_consent.ScaMethod == SCAMethod.OAUTH_REDIRECT || _consent.ScaMethod == SCAMethod.REDIRECT)
            {
                //
                // Bank uses a redirect flow for Strong Customer Authentication
                //
                scaSuccess = await SCAFlowRedirect(_consent, "MyState");
            }
            else if (_consent.ScaMethod == SCAMethod.DECOUPLED)
            {
                //
                // Bank uses a decoupled flow for Strong Customer Authentication
                //
                scaSuccess = await SCAFlowDecoupled(_consent);
            }
            else
            {
                throw new Exception($"ERROR: unknown SCA method {_consent.ScaMethod}");
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
            // Check the status of the consent, which should be "valid" after a successful SCA 
            //
            string consentStatus = await GetConsentStatus(_consent.BicFi, _consent.ConsentId);
            Console.WriteLine($"consentStatus: {consentStatus}");
            Console.WriteLine();

            if (!consentStatus.Equals("valid"))
            {
                Console.WriteLine("Consent is not valid");
                Console.WriteLine();
                return;
            }

            //
            // Use the valid consent to call AIS service "Get Account List"
            // that will list the bank accounts of the end user
            //
            string accountList = await GetAccountList(_consent.BicFi, _consent.ConsentId);
            Console.WriteLine($"accountList: {accountList}");
            Console.WriteLine();
        }

        static void Usage()
        {
            Console.WriteLine("Usage: CreateConsent <BicFi>");
        }

        static void Init(string bicFi)
        {
            //
            // Read configuration
            //
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddJsonFile("appsettings.json", false, false);
            IConfigurationRoot config = configurationBuilder.Build();
            var settings = config.Get<Settings>();

            _clientId = settings.ClientId;
            _redirectUri = settings.RedirectURI;
            _accountinformationScope = $"{settings.PSUContextScope} accountinformation";
            _psuCorporateId = settings.PSUContextScope.Equals("corporate") ? settings.PSUCorporateId : null;
            _psuIPAddress = settings.PSUIPAddress;
            _psuUserAgent = settings.PSUUserAgent;

            _consent = new Consent(bicFi);

            //
            // Prompt for client secret
            //
            Console.Write("Enter your Client Secret: ");
            _clientSecret = ConsoleReadPassword();
            Console.WriteLine();

            _apiClientHandler = new HttpClientHandler();

            //
            // Set up for different environments
            //
            if (settings.UseProductionEnvironment)
            {
                Console.WriteLine("Using production");
                _authUri = "https://auth.openbankingplatform.com";
                _apiUri = "https://api.openbankingplatform.com";

                Console.Write("Enter Certificate Password: ");
                string certPassword = ConsoleReadPassword();
                Console.WriteLine();

                X509Certificate2 certificate = new X509Certificate2(settings.ProductionClientCertificateFile, certPassword);
                _apiClientHandler.ClientCertificates.Add(certificate);
            }
            else
            {
                Console.WriteLine("Using sandbox");
                _authUri = "https://auth.sandbox.openbankingplatform.com";
                _apiUri = "https://api.sandbox.openbankingplatform.com";
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

        private static string FormatBankIdURL(string autostartToken, string redirectUri)
        {
            return $"bankid:///?autostarttoken={autostartToken}&redirect={redirectUri}";
        }

        //
        // Generates a QR-code image from a character string and opens it with default application
        //
        private static void DisplayQRCode(string url)
        {
            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            QRCode qrCode = new QRCode(qrCodeData);
            Bitmap qrCodeImage = qrCode.GetGraphic(20);
            qrCodeImage.Save(QRCodeImageFilename, ImageFormat.Png);
            string qrCodeUrl = "file://" + Path.GetFullPath(".") + "/" + QRCodeImageFilename;
            Console.WriteLine($"qrCodeUrl: {qrCodeUrl}");
            Console.WriteLine();
            OpenBrowser(qrCodeUrl);
        }

        //
        // Will poll the SCA status indefinitely until status is either "finalised" or "failed"
        //
        private static async Task<bool> PollSCAStatus(Consent consent, int millisecondsDelay)
        {
            string scaStatus = await GetConsentAuthorisationSCAStatus(consent.BicFi, consent.ConsentId, consent.ConsentAuthId);
            Console.WriteLine($"scaStatus: {scaStatus}");
            Console.WriteLine();
            while (!scaStatus.Equals("finalised") && !scaStatus.Equals("failed"))
            {
                await Task.Delay(millisecondsDelay);
                scaStatus = await GetConsentAuthorisationSCAStatus(consent.BicFi, consent.ConsentId, consent.ConsentAuthId);
                Console.WriteLine($"scaStatus: {scaStatus}");
                Console.WriteLine();
            }
            if (scaStatus.Equals("failed"))
                return false;

            return true;
        }

        //
        // Starts a redirect flow for SCA by opening SCA URL in default browser (for end user to authenticate),
        // then prompts for authorisation code returned in final redirect query parameter "code".
        // (prompting for this is because of the simplicity of this example application that is not implementing a http server)
        //
        private static async Task<bool> SCAFlowRedirect(Consent consent, string state)
        {
            //
            // Fill in the details on the given redirect URL template
            //
            string url = consent.ScaData.Replace("[CLIENT_ID]", _clientId).Replace("[TPP_REDIRECT_URI]", WebUtility.UrlEncode(_redirectUri)).Replace("[TPP_STATE]", WebUtility.UrlEncode(state));
            Console.WriteLine($"URL: {url}");
            Console.WriteLine();

            OpenBrowser(url);

            //
            // If flow is OAuthRedirect, authorisation code needs to be activated
            //
            if (consent.ScaMethod == SCAMethod.OAUTH_REDIRECT)
            {
                Console.Write("Enter authorisation code returned by redirect query param: ");
                string authCode = Console.ReadLine();
                Console.WriteLine();

                string newToken = await ActivateOAuthPaymentAuthorisation(consent.ConsentId, consent.ConsentAuthId, _clientId, _clientSecret, _redirectUri, _accountinformationScope, authCode);
                Console.WriteLine();
                if (String.IsNullOrEmpty(newToken))
                    return false;
            }

            //
            // Wait for a final SCA status
            //
            return await PollSCAStatus(consent, 2000);
        }

        //
        // Handles a decoupled flow by formatting a BankId URL, presenting it as an QR-code to be scanned
        // with BankId, then polling for a final SCA status of the authentication/auhorisation
        //
        private static async Task<bool> SCAFlowDecoupled(Consent consent)
        {
            string bankIdUrl = FormatBankIdURL(consent.ScaData, WebUtility.UrlEncode("https://openpayments.io"));
            DisplayQRCode(bankIdUrl);

            return await PollSCAStatus(consent, 2000);
        }

        //
        // Create a http client with the basic common attributes set for a request to auth server
        //
        private static HttpClient CreateGenericAuthClient()
        {
            var authClient = new HttpClient();
            authClient.BaseAddress = new Uri(_authUri);
            authClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return authClient;
        }

        //
        // Create a http client with the basic common attributes set for a request to API:s
        //
        private static HttpClient CreateGenericApiClient(string bicFi)
        {
            var apiClient = new HttpClient(_apiClientHandler);
            apiClient.BaseAddress = new Uri(_apiUri);
            apiClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            apiClient.DefaultRequestHeaders.Add("X-BicFi", bicFi);
            apiClient.DefaultRequestHeaders.Add("PSU-IP-Address", _psuIPAddress);
            if (!String.IsNullOrEmpty(_psuCorporateId))
                apiClient.DefaultRequestHeaders.Add("PSU-Corporate-Id", _psuCorporateId);

            return apiClient;
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

        private static async Task<string> ActivateOAuthPaymentAuthorisation(string consentId, string authId, string clientId, string clientSecret, string redirectUri, string scope, string authCode)
        {
            Console.WriteLine("Activate OAuth Consent Authorisation");
            var authClient = CreateGenericAuthClient();
            authClient.DefaultRequestHeaders.Add("X-ConsentId", consentId);
            authClient.DefaultRequestHeaders.Add("X-ConsentAuthorisationId", authId);

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

        private static async Task<string> CreateConsent(string bicFi, DateTime validUntil)
        {
            Console.WriteLine("Create Consent");
            var apiClient = CreateGenericApiClient(bicFi);
            apiClient.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            apiClient.DefaultRequestHeaders.Add("PSU-User-Agent", _psuUserAgent);

            string jsonBody = "{\"access\": {  }, \"recurringIndicator\": true, \"validUntil\": \"" + validUntil.ToString("yyyy-MM-dd") + "\", \"frequencyPerDay\": 4, \"combinedServiceIndicator\": false}";
            var response = await apiClient.PostAsync("/psd2/consent/v1/consents", new StringContent(jsonBody, Encoding.UTF8, "application/json"));
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");

            dynamic responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.consentId;
        }

        private static async Task<string> StartConsentAuthorisationProcess(string bicFi, string consentId)
        {
            Console.WriteLine("Start Consent Authorisation Process");
            var apiClient = CreateGenericApiClient(bicFi);
            apiClient.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());

            string jsonBody = "";
            var response = await apiClient.PostAsync($"/psd2/consent/v1/consents/{consentId}/authorisations", new StringContent(jsonBody, Encoding.UTF8, "application/json"));
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

        private static async Task<(SCAMethod, string)> UpdatePSUDataForConsent(string bicFi, string consentId, string authId)
        {
            Console.WriteLine("Update PSU Data For Consent");
            var apiClient = CreateGenericApiClient(bicFi);
            apiClient.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());

            string jsonBody = "{\"authenticationMethodId\": \"mbid\"}";
            var response = await apiClient.PutAsync($"/psd2/consent/v1/consents/{consentId}/authorisations/{authId}", new StringContent(jsonBody, Encoding.UTF8, "application/json"));
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

        private static async Task<string> GetConsentAuthorisationSCAStatus(string bicFi, string consentId, string authId)
        {
            Console.WriteLine("Get Consent Authorisation SCA Status");
            var apiClient = CreateGenericApiClient(bicFi);
            apiClient.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());

            var response = await apiClient.GetAsync($"/psd2/consent/v1/consents/{consentId}/authorisations/{authId}");
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

        private static async Task<string> GetConsentStatus(string bicFi, string consentId)
        {
            Console.WriteLine("Get Consent Status");
            var apiClient = CreateGenericApiClient(bicFi);
            apiClient.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());

            var response = await apiClient.GetAsync($"/psd2/consent/v1/consents/{consentId}/status");
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");

            dynamic responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.consentStatus;
        }

        private static async Task<string> GetAccountList(string bicFi, string consentId)
        {
            Console.WriteLine("Get Account List");
            var apiClient = CreateGenericApiClient(bicFi);
            apiClient.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString());
            apiClient.DefaultRequestHeaders.Add("Consent-ID", consentId);

            var response = await apiClient.GetAsync("/psd2/accountinformation/v1/accounts?withBalance=true");
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");

            return responseContent;
        }

    }
}
