//////////////////////////////////////////////////////////////////////
//
// Open Payments Europe AB 2021
//
// Open Banking Platform - Consent / Account Information Service
// 
// Create Consent / AIS example 
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

namespace CreateConsent
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
            public string PSUContextScope { get; set; }
            public string PSUId { get; set; }
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
            public string AffiliatedASPSPId { get; }
            public string ConsentId { get; set; }
            public string ConsentAuthId { get; set; }
            public SCAMethod ScaMethod { get; set; }
            public SCAData ScaData { get; set; }

            public Consent(string bicFi, string affiliatedASPSPId)
            {
                this.BicFi = bicFi;
                this.AffiliatedASPSPId = affiliatedASPSPId;
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

        private static String _authBaseUri;
        private static String _apiBaseUri;
        private static HttpClientHandler _apiClientHandler;
        private static Settings _settings;
        private static string _accountinformationScope;
        private static string _token;
        private static Consent _consent;

        static async Task Main(string[] args)
        {
            if (args.Length != 1 && args.Length != 2)
            {
                Usage();
                return;
            }
            string bicFi = args[0];
            string affiliatedASPSPId = "";
            if (args.Length == 2)
            {
                affiliatedASPSPId = args[1];
            }
            Init(bicFi, affiliatedASPSPId);

            //
            // Get an API access token from auth server with the scope needed
            //
            _token = await GetToken(_settings.ClientId, _settings.ClientSecret, _accountinformationScope);
            Console.WriteLine($"token: {_token}");
            Console.WriteLine();

            //
            // Create a consent valid for 1 day
            //
            DateTime validUntil = DateTime.Now.AddDays(1);
            _consent.ConsentId = await CreateConsent(_consent.BicFi, _settings.PSUIPAddress, _settings.PSUUserAgent, _settings.PSUCorporateId, _consent.AffiliatedASPSPId, validUntil);
            Console.WriteLine($"consentId: {_consent.ConsentId}");
            Console.WriteLine();

            //
            // Create a consent authorization object to be used for authorizing the consent with the end user
            //
            List<string> authMethodIds = null;
            (_consent.ConsentAuthId, authMethodIds) = await StartConsentAuthorisationProcess(_consent.BicFi, _settings.PSUIPAddress, _settings.PSUUserAgent, _settings.PSUId, _settings.PSUCorporateId, _consent.ConsentId);
            Console.WriteLine($"authId: {_consent.ConsentAuthId}");
            Console.WriteLine();

            string authMethodId = authMethodIds.FirstOrDefault(s => s.Equals("mbid_animated_qr_image"));
            if (authMethodId == null)
                authMethodId = "mbid";

            //
            // Temporary special logic for SWEDSESS until "mbid_animated_qr_image" method is released
            //
            if (_consent.BicFi.Equals("SWEDSESS"))
                authMethodId = "mbid_animated_qr_image";

            Console.WriteLine($"using auth method: {authMethodId}");

            //
            // Start the consent authorization process with the end user
            //
            string scaStatus;
            (_consent.ScaMethod, scaStatus, _consent.ScaData) = await UpdatePSUDataForConsent(_consent.BicFi, _settings.PSUIPAddress, _settings.PSUUserAgent, _settings.PSUId, _settings.PSUCorporateId, _consent.ConsentId, _consent.ConsentAuthId, authMethodId);
            Console.WriteLine($"scaMethod: {_consent.ScaMethod}");
            Console.WriteLine($"data: {_consent.ScaData}");
            Console.WriteLine();

            bool scaSuccess;
            if (_consent.ScaMethod == SCAMethod.OAUTH_REDIRECT || _consent.ScaMethod == SCAMethod.REDIRECT)
            {
                //
                // Bank uses a redirect flow for Strong Customer Authentication
                //
                scaSuccess = await SCAFlowRedirect(_consent, scaStatus, "MyState");
            }
            else if (_consent.ScaMethod == SCAMethod.DECOUPLED)
            {
                //
                // Bank uses a decoupled flow for Strong Customer Authentication
                //
                scaSuccess = await SCAFlowDecoupled(_consent, scaStatus);
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
            string consentStatus = await GetConsentStatus(_consent.BicFi, _settings.PSUIPAddress, _settings.PSUUserAgent, _settings.PSUId, _settings.PSUCorporateId, _consent.ConsentId);
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
            string accountList = await GetAccountList(_consent.BicFi, _settings.PSUIPAddress, _settings.PSUUserAgent, _settings.PSUId, _settings.PSUCorporateId, _consent.ConsentId);
            dynamic parsedJson = JsonConvert.DeserializeObject(accountList);
            accountList = JsonConvert.SerializeObject(parsedJson, Formatting.Indented);
            Console.WriteLine($"accountList: {accountList}");
            Console.WriteLine();
        }

        static void Usage()
        {
            Console.WriteLine("Usage: CreateConsent <BicFi> [<AffiliatedASPSPId>]");
        }

        static void Init(string bicFi, string affiliatedASPSPId)
        {
            //
            // Read configuration
            //
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddJsonFile("appsettings.json", false, false);
            IConfigurationRoot config = configurationBuilder.Build();
            _settings = config.Get<Settings>();

            _accountinformationScope = $"{_settings.PSUContextScope} accountinformation";

            _consent = new Consent(bicFi, affiliatedASPSPId);

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
                string certPassword = ConsoleReadPassword();
                Console.WriteLine();

                X509Certificate2 certificate = new X509Certificate2(_settings.ProductionClientCertificateFile, certPassword);
                _apiClientHandler.ClientCertificates.Add(certificate);
            }
            else
            {
                Console.WriteLine("Using sandbox");
                Console.WriteLine();
                _authBaseUri = "https://auth.sandbox.openbankingplatform.com";
                _apiBaseUri = "https://api.sandbox.openbankingplatform.com";
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
            if (String.IsNullOrEmpty(redirectUri))
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
            string html = $"<html><style>h1 {{text-align: center;}} .center {{ display: block; margin-left: auto; margin-right: auto;}}</style><body><!--TITLEHTML--><!--IMGHTML--></body></html>";
            if (!String.IsNullOrEmpty(title))
            {
                html = html.Replace("<!--TITLEHTML-->", $"<p><h1>{title}</h1></p>");
            }

            if (!String.IsNullOrEmpty(url))
            {
                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
                var imgType = Base64QRCode.ImageType.Png;
                Base64QRCode qrCode = new Base64QRCode(qrCodeData);
                string qrCodeImageAsBase64 = qrCode.GetGraphic(20, Color.Black, Color.White, true, imgType);
                html = html.Replace("<!--IMGHTML-->", $"<img alt=\"Embedded QR Code\" class=\"center\" width=\"500\" height=\"500\" src=\"data:image/{imgType.ToString().ToLower()};base64,{qrCodeImageAsBase64}\"/>");
            }
            else if (!String.IsNullOrEmpty(image))
            {
                html = html.Replace("<!--IMGHTML-->", $"<img alt=\"Embedded QR Code\" class=\"center\" width=\"500px\" height=\"500px\" src=\"{image}\"/>");
            }

            using (StreamWriter outputFile = new StreamWriter(Path.GetFullPath(".") + "/" + QRCodeHtmlFilename))
            {
                outputFile.WriteLine(html);
            }
            string qrCodeUrl = "file://" + Path.GetFullPath(".") + "/" + QRCodeHtmlFilename;
            OpenBrowser(qrCodeUrl);
        }

        //
        // Will poll the SCA status indefinitely with given period until status is either "finalised", "failed" or "exempted"
        //
        private static async Task<bool> PollSCAStatus(Consent consent, string initialSCAStatus, int millisecondsDelay)
        {
            string previousScaStatus = "";
            string scaStatus = initialSCAStatus;
            SCAData scaData = consent.ScaData;
            string psuMessage = GetPSUMessage(scaStatus);

            Console.WriteLine($"scaStatus: {scaStatus}");
            Console.WriteLine();
            while (!scaStatus.Equals("finalised") && !scaStatus.Equals("failed") && !scaStatus.Equals("exempted"))
            {
                Console.WriteLine($"scaStatus: {scaStatus}");
                Console.WriteLine();

                if (!scaStatus.Equals(previousScaStatus))
                {
                    if (scaStatus.Equals("started") || scaStatus.Equals("authenticationStarted"))
                    {
                        string bankIdUrl = FormatBankIdURL("", "");
                        if (!String.IsNullOrEmpty(scaData.Token))
                        {
                            bankIdUrl = FormatBankIdURL(scaData.Token, "");
                            DisplayQRCode(bankIdUrl, "", psuMessage);
                        }
                        else if (!String.IsNullOrEmpty(scaData.Image))
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
                else if (!String.IsNullOrEmpty(scaData.Image))
                {
                    DisplayQRCode("", scaData.Image, psuMessage);
                }
                await Task.Delay(millisecondsDelay);

                (scaStatus, scaData) = await GetConsentAuthorisationSCAStatus(consent.BicFi, _settings.PSUIPAddress, _settings.PSUUserAgent, _settings.PSUId, _settings.PSUCorporateId, consent.ConsentId, consent.ConsentAuthId);
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
            else if (scaStatus.Equals("started"))
            {
                return "Please sign the consent";
            }
            else if (scaStatus.Equals("finalised"))
            {
                return "SCA Finalised";
            }
            else if (scaStatus.Equals("exempted"))
            {
                return "SCA Exempted";
            }
            else if (scaStatus.Equals("failed"))
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
        private static async Task<bool> SCAFlowRedirect(Consent consent, string scaStatus, string state)
        {
            //
            // Fill in the details on the given redirect URL template
            //
            string url = consent.ScaData.RedirectUri.Replace("[CLIENT_ID]", _settings.ClientId).Replace("[TPP_REDIRECT_URI]", WebUtility.UrlEncode(_settings.RedirectURI)).Replace("[TPP_STATE]", WebUtility.UrlEncode(state));
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

                string newToken = await ActivateOAuthConsentAuthorisation(consent.ConsentId, consent.ConsentAuthId, _settings.ClientId, _settings.ClientSecret, _settings.RedirectURI, _accountinformationScope, authCode);
                Console.WriteLine();
                if (String.IsNullOrEmpty(newToken))
                    return false;
            }

            //
            // Wait for a final SCA status
            //
            return await PollSCAStatus(consent, scaStatus, 2000);
        }


        //
        // Handles a decoupled flow by formatting a BankId URL, presenting it as an QR-code to be scanned
        // with BankId, then polling for a final SCA status of the authentication/auhorisation
        //
        private static async Task<bool> SCAFlowDecoupled(Consent consent, string scaStatus)
        {
            return await PollSCAStatus(consent, scaStatus, 1000);
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
            if (!String.IsNullOrEmpty(psuCorporateId))
                apiClient.DefaultRequestHeaders.Add("PSU-Corporate-Id", psuCorporateId);

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
            Console.WriteLine();

            dynamic responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.access_token;
        }

        private static async Task<string> ActivateOAuthConsentAuthorisation(string consentId, string authId, string clientId, string clientSecret, string redirectUri, string scope, string authCode)
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
            Console.WriteLine();

            dynamic responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.access_token;
        }

        private static async Task<String> CreateConsent(string bicFi, string psuIPAddress, string psuUserAgent, string psuCorporateId, string affiliatedASPSPId, DateTime validUntil)
        {
            Console.WriteLine("Create Consent");
            var apiClient = CreateGenericApiClient(bicFi, psuIPAddress, psuUserAgent, psuCorporateId);
            if (affiliatedASPSPId != null)
                apiClient.DefaultRequestHeaders.Add("X-AffiliatedASPSP-ID", affiliatedASPSPId);

            string jsonBody = "{\"access\": {  }, \"recurringIndicator\": true, \"validUntil\": \"" + validUntil.ToString("yyyy-MM-dd") + "\", \"frequencyPerDay\": 4, \"combinedServiceIndicator\": false}";
            var response = await apiClient.PostAsync("/psd2/consent/v1/consents", new StringContent(jsonBody, Encoding.UTF8, "application/json"));
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");
            Console.WriteLine();

            dynamic responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.consentId;
        }

        private static async Task<(string, List<string>)> StartConsentAuthorisationProcess(string bicFi, string psuIPAddress, string psuUserAgent, string psuId, string psuCorporateId, string consentId)
        {
            Console.WriteLine("Start Consent Authorisation Process");
            var apiClient = CreateGenericApiClient(bicFi, psuIPAddress, psuUserAgent, psuCorporateId);
            if (!String.IsNullOrEmpty(psuId))
                apiClient.DefaultRequestHeaders.Add("PSU-ID", psuId);

            string jsonBody = "";
            var response = await apiClient.PostAsync($"/psd2/consent/v1/consents/{consentId}/authorisations", new StringContent(jsonBody, Encoding.UTF8, "application/json"));
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");
            Console.WriteLine();

            dynamic responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);
            List<string> authMethodIds = new List<string>();
            foreach (dynamic item in responseBody.scaMethods)
            {
                authMethodIds.Add(item.authenticationMethodId.ToString());
            }

            return (responseBody.authorisationId, authMethodIds);
        }

        private static async Task<(SCAMethod, string, SCAData)> UpdatePSUDataForConsent(string bicFi, string psuIPAddress, string psuUserAgent, string psuId, string psuCorporateId, string consentId, string authId, string authenticationMethodId)
        {
            Console.WriteLine("Update PSU Data For Consent");
            var apiClient = CreateGenericApiClient(bicFi, psuIPAddress, psuUserAgent, psuCorporateId);
            if (!String.IsNullOrEmpty(psuId))
                apiClient.DefaultRequestHeaders.Add("PSU-ID", psuId);

            string jsonBody = $"{{\"authenticationMethodId\": \"{authenticationMethodId}\"}}";
            var response = await apiClient.PutAsync($"/psd2/consent/v1/consents/{consentId}/authorisations/{authId}", new StringContent(jsonBody, Encoding.UTF8, "application/json"));
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");
            Console.WriteLine();

            dynamic responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            string scaStatus = responseBody.scaStatus;

            SCAData scaData = new SCAData();
            IEnumerable<string> headerValues = response.Headers.GetValues("aspsp-sca-approach");
            string scaApproach = headerValues.FirstOrDefault();
            SCAMethod method = SCAMethod.UNDEFINED;
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

        private static async Task<(string, SCAData)> GetConsentAuthorisationSCAStatus(string bicFi, string psuIPAddress, string psuUserAgent, string psuId, string psuCorporateId, string consentId, string authId)
        {
            Console.WriteLine("Get Consent Authorisation SCA Status");
            var apiClient = CreateGenericApiClient(bicFi, psuIPAddress, psuUserAgent, psuCorporateId);
            if (!String.IsNullOrEmpty(psuId))
                apiClient.DefaultRequestHeaders.Add("PSU-ID", psuId);

            var response = await apiClient.GetAsync($"/psd2/consent/v1/consents/{consentId}/authorisations/{authId}");
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");
            Console.WriteLine();

            dynamic responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);
            SCAData scaData = new SCAData();
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

        private static async Task<String> GetConsentStatus(string bicFi, string psuIPAddress, string psuUserAgent, string psuId, string psuCorporateId, string consentId)
        {
            Console.WriteLine("Get Consent Status");
            var apiClient = CreateGenericApiClient(bicFi, psuIPAddress, psuUserAgent, psuCorporateId);
            if (!String.IsNullOrEmpty(psuId))
                apiClient.DefaultRequestHeaders.Add("PSU-ID", psuId);

            var response = await apiClient.GetAsync($"/psd2/consent/v1/consents/{consentId}/status");
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");
            Console.WriteLine();

            dynamic responseBody = JsonConvert.DeserializeObject<dynamic>(responseContent);

            return responseBody.consentStatus;
        }

        private static async Task<String> GetAccountList(string bicFi, string psuIPAddress, string psuUserAgent, string psuId, string psuCorporateId, string consentId)
        {
            Console.WriteLine("Get Account List");
            var apiClient = CreateGenericApiClient(bicFi, psuIPAddress, psuUserAgent, psuCorporateId);
            apiClient.DefaultRequestHeaders.Add("Consent-ID", consentId);
            if (!String.IsNullOrEmpty(psuId))
                apiClient.DefaultRequestHeaders.Add("PSU-ID", psuId);

            var response = await apiClient.GetAsync("/psd2/accountinformation/v1/accounts?withBalance=true");
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ERROR: statusCode={(int)response.StatusCode} Message={responseContent}");
            }
            Console.WriteLine($"resultStatusCode: {(int)response.StatusCode}");
            Console.WriteLine($"resultBody: {responseContent}");
            Console.WriteLine();

            return responseContent;
        }

    }
}
