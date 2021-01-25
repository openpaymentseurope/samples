# Description 
C# .NET Core example implementation of creation, authorization and use of an AIS
consent for Open Payments Europe AB:s NextGen REST API:s. 

# Prerequisites

###Download and install Microsoft .Net Core 
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
```json
{
  "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",   An API client id created in our Developer Portal
  "RedirectURI": "https://acme.com/",
  "PSUContextScope": "private",
  "PSUCorporateId": "",
  "UseProductionEnvironment": false,
  "ProductionClientCertificateFile": "acme.com.pfx",
  "PSUIPAddress": "192.168.0.1",
  "PSUUserAgent": "mozilla/5.0"
}
```

# Running
```
> dotnet run <BicFi>
```
Where BicFi is the identifier of any of the available banks in the chosen environment (e.g. ESSESESS, HANDSESS, NDESESS, SWEDSESS, etc.)