# Description 
C# .NET Core example implementation of creation, authorization and status follow-up of PIS Payment Initiations for Open Payments Europe AB:s NextGen REST API:s. 

This example demonstrates how to implement a full flow of creating, authorising and following up the status of a Payment Initiation, supporting OAuthRedirect, Redirect and Decoupled SCA approaches with the end user.

Please note that this is a simplified implementation with a pedagogic purpose of explaining the flow. A production implementation obviously needs more attention to error handling, dealing with asynchronicity, concurrency of multiple Payment Initiation processing requests and applying your specific needs on payment status follow-up.

# Prerequisites

### API client credentials
Sign-up with Open Payments Europe's Developer Portal and register an application to acquire your API client credentials (Note that you must register your application to be using the Payment Initiation (PIS) API to use it with this demo).
https://developer.openpayments.io/

### Download and install Microsoft .Net Core 
https://dotnet.microsoft.com/download

## macOS specific
If you are running this demo on macOS, you need to install the graphics library `mono-libgdiplus` for the QR-code generation. Please proceed with the following steps.

### Install Homebrew for macOS
https://brew.sh

### Install mono-libgdiplus for QR code image generation
```
> brew install mono-libgdiplus
```

# Building
```
> dotnet build
```

# Configuration
appsettings.json
```json5
{
  "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx", // An API client id created in our Developer Portal
  "RedirectURI": "https://acme.com/",                 // A redirect URI registered with the client in Developer Portal
  "UseProductionEnvironment": false,                  // If true, production environment is used, otherwise sandbox
  "ProductionClientCertificateFile": "acme.com.pfx",  // If production environment, your client certificate filename
  "PSUIPAddress": "192.168.0.1",                      // The PSU IP address to present to the bank
  "PSUUserAgent": "mozilla/5.0"                       // The PSU user agent to present to the bank
}
```
* Edit the `appsettings.json` file and set your specific `ClientId` and `RedirectURI` obtained from Open Payments Developer Portal.
* If you are using the production environment, `UseProductionEnvironment` must be set to `true` and `ProductionClientCertificateFile` must be the filename of your production client certificate (obtained when you onboarded your company to our production environment in Developer Portal) and the certificate file must be in the project directory of this application.

payments.json
```json5
[
  {
      "Name": "ESSESESSpriv",                      // Unique name that is used when selecting which payment to process when running app
      "BICFI": "ESSESESS",                         // Identifier of which bank that will be used for the payment
      "PSUContextScope": "private",                // Which context to use, "private" or "corporate" accounts
      "PSUCorporateId": "",                        // If corporate context, your corporate id with the bank is given here
      "PaymentService": "payments",                // Payment Service to use
      "PaymentProduct": "domestic",                // Payment Product to use ("domestic","swedish-giro","sepa-credit-transfers","international")
      "Payment": {                                 // Payment body, structure of this is free and specific to the Payment Product used
          "instructedAmount": {
              "currency": "SEK",
              "amount": "1.5"
          },
          "debtorAccount": {
              "iban": "SE3750000000054400047881",
              "currency": "SEK"
          },
          "creditorName": "Freddie Gummesson",
          "creditorAccount": {
              "iban": "SE2550000000054400047903",
              "currency": "SEK"
          },
          "remittanceInformationUnstructured": "My Payment"
      }
  }
]
```
* An array of payments may be defined in the `payments.json` configuration file, where each payment is given a unique name that can be used to select which payment to process when running the application (`payments.json` comes pre-configured with private accounts domestic payment definititions for the banks in our sandbox environment).
* When defining new payment entries, please refer to our PIS REST API reference documentation for details on payment services, payment products and payment body structures to use https://docs.openpayments.io/en/openpayments-NextGenPSD2-1.3.3.html#tag/Payment-Initiation-Service-(PIS).

# Running
```
> dotnet run <payment name>
```
Where <payment name> is the identifier of the payment you want to process (set in your `payments.json` file)
