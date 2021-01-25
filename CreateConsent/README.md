# Description 
C# .NET Core example implementation of creation, authorization and use of an AIS
consent for Open Payments Europe AB:s NextGen REST API:s. 

# Prerequisites

### API client credentials
Sign-up in Open Payments Europe's Developer Portal and register an application to acquire your API client credentials.

### Download and install Microsoft .Net Core 
https://dotnet.microsoft.com/download

## macOS specific
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
  "PSUContextScope": "private",                       // Which context to use, private or corporate accounts
  "PSUCorporateId": "",                               // If corporate context, your corporate id with the bank is given here
  "UseProductionEnvironment": false,                  // If true, production environment is used, otherwise sandbox
  "ProductionClientCertificateFile": "acme.com.pfx",  // If production environment, your client certificate filename
  "PSUIPAddress": "192.168.0.1",                      // The PSU IP address to present to the bank
  "PSUUserAgent": "mozilla/5.0"                       // The PSU user agent to present to the bank
}
```

# Running
```
> dotnet run <BicFi>
```
Where BicFi is the identifier of any of the available banks in the chosen environment (e.g. ESSESESS, HANDSESS, NDESESS, SWEDSESS, etc.)
