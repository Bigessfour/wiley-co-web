# Syncfusion license

The trial banner goes away when `Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense` runs before the first component renders. The key is not committed.

## Where the client reads it

`ClientStartup` registers the key from the first source that has a value:

1. Configuration `SyncfusionLicenseKey` (including gitignored `wwwroot/appsettings.Syncfusion.local.json`).
2. Environment variable `SYNCFUSION_LICENSE_KEY`.

Amplify `amplify.yml` loads the secret `SYNCFUSION_LICENSE_KEY` (Secrets Manager, then Amplify environment secrets) and writes `wwwroot/appsettings.Syncfusion.local.json` before `dotnet publish`. The build fails if the secret is missing. That file is gitignored.

The API host uses the same environment variable in `WileyCoWeb.Api/Configuration/LicenseBootstrapper.cs`. `/health` includes the Syncfusion license check.

## What not to commit

Do not put the key in `appsettings*.json`, source, or docs. `./Scripts/assert-no-syncfusion-license.sh` fails CI if a key is tracked.
