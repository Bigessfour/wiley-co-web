# wiley-co-web

Wiley Widget for AWS

## Amplify Hosting

This app is hosted on AWS Amplify in `us-east-2`.

- App name: `wiley-co-web`
- App id: `d2ellat1y3ljd9`
- Production branch: `main`
- Default domain: `d2ellat1y3ljd9.amplifyapp.com`

### Syncfusion License In Amplify

AWS Amplify Gen 1 stores encrypted hosting secrets in AWS Systems Manager Parameter Store, not AWS Secrets Manager. The AWS docs specify that Amplify environment secrets are exposed to the build as `process.env.secrets`.

For this app, the build now supports either of these sources before `dotnet publish` runs:

1. AWS Secrets Manager, using the secret name `SYNCFUSION_LICENSE_KEY`.
2. Amplify Gen 1 environment secrets from Systems Manager Parameter Store as a fallback.
3. A local ignored file named `appsettings.Syncfusion.local.json` in the repository root.

Amplify production builds now fail fast if `SYNCFUSION_LICENSE_KEY` is missing after those lookup steps. This prevents deploying a client bundle that would show the Syncfusion license popup at runtime.

Amplify builds now also validate Cognito secret consistency. If any of `COGNITO_USER_POOL_ID`, `COGNITO_APP_CLIENT_ID`, or `COGNITO_REGION` is set, all three must be present or the build fails.

If you are using AWS Secrets Manager:

1. Store the secret either as a raw string or as JSON containing `SYNCFUSION_LICENSE_KEY` or `SyncfusionLicenseKey`.
2. Ensure the Amplify build role can call `secretsmanager:GetSecretValue` for the secret named `SYNCFUSION_LICENSE_KEY`.
3. Redeploy the Amplify branch.

If you are using Amplify Gen 1 environment secrets instead:

1. In Systems Manager Parameter Store, create a `SecureString` parameter named `/amplify/d2ellat1y3ljd9/<backend-environment-name>/SYNCFUSION_LICENSE_KEY`.
2. Use the default AWS KMS key for the account so Amplify can decrypt it.
3. Redeploy the Amplify branch.

If you are enabling hosted auth:

1. Add `COGNITO_USER_POOL_ID`, `COGNITO_APP_CLIENT_ID`, and `COGNITO_REGION` as Amplify environment secrets.
2. Keep them in sync per branch; partial values now fail the build to prevent inconsistent auth configuration in production.

If you are working locally on macOS and user secrets are not being surfaced reliably, create an ignored file named `appsettings.Syncfusion.local.json` in the repository root with this shape:

```json
{"SyncfusionLicenseKey":"<your-license-key>"}
```

The build copies that file into `wwwroot/appsettings.json`, and that generated file is already ignored by git.

Important: this app is a static Blazor WebAssembly site. That means the Syncfusion license is injected at build time and then included in the published client assets so `Program.cs` can read it from configuration at startup. This is not a private server-side runtime secret path.

### Grok And xAI Secrets

The xAI Grok API key is a backend runtime secret, not an Amplify frontend build secret.

The active browser client in [Program.cs](Program.cs) only talks to the thin API host. Grok participation happens server-side in the API and service layer, so keep the xAI key out of Amplify static hosting unless you intentionally redesign the app to call xAI directly from the browser.

For AWS deployment:

1. Store the Grok secret in AWS Secrets Manager as `Grok` or set `XAI:SecretName` / `XAI_SECRET_NAME` to the secret name you choose.
2. The secret value can be either a raw API key string or JSON containing `XAI_API_KEY`, `ApiKey`, `XaiApiKey`, or `GrokApiKey`.
3. Give the API host IAM role `secretsmanager:GetSecretValue` permission for that secret.
4. Set the runtime region with `WILEY_AWS_REGION`, `AWS_REGION`, or `AWS_DEFAULT_REGION`. The API defaults to `us-east-2`.

The API host now attempts to load the Grok secret from AWS Secrets Manager at startup only when `XAI_API_KEY` is not already present in configuration or the environment. Local `.NET user secrets` remain a development-only path.

## Aurora Database

The private Aurora PostgreSQL database is now provisioned in the dedicated `wiley-co-aurora-vpc` network and uses the canonical schema in [docs/amplify-db-schema.sql](docs/amplify-db-schema.sql).

- Layout note: [docs/aws-aurora-private-layout.md](docs/aws-aurora-private-layout.md)
- Cluster: `wiley-co-aurora-db`
- Writer instance: `wiley-co-aurora-db-1`
- Database name: `wileyco`

## UI Rebuild Roadmap

The restored Wiley Widget rebuild plan is documented in [docs/wileyco-ui-rebuild-plan.md](docs/wileyco-ui-rebuild-plan.md).

- Focus: Syncfusion panel-first rate study workspace
- Backend: shared models, Aurora persistence, thin API, AI recommendations
- First slice: enterprise selector + break-even panel + scenario save/load

## Local Snapshot Host

The workspace client prefers a live snapshot endpoint at `api/workspace/snapshot` when it is available. Set `WILEY_WORKSPACE_API_BASE_ADDRESS` to point the Blazor client at the thin API host during local development, for example when running `WileyCoWeb.Api` separately.

## Local Secrets On macOS

This project now has a `.NET User Secrets` identity for local development.

- Initialize or inspect secrets from the repo root with `dotnet user-secrets list --project WileyCoWeb.csproj`.
- Store non-committed development secrets with `dotnet user-secrets set <key> <value> --project WileyCoWeb.csproj`.

Examples:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "<your-key>" --project WileyCoWeb.csproj
dotnet user-secrets set "QuickBooks:ClientSecret" "<your-secret>" --project WileyCoWeb.csproj
```

Syncfusion keys remain environment-variable based by policy:

```bash
launchctl setenv SYNCFUSION_LICENSE_KEY "<your-runtime-license-key>"
launchctl setenv SYNCFUSION_API_KEY_PATH "$HOME/.config/syncfusion/documentsdk.key"
```

Restart VS Code after changing `launchctl` values so GUI-launched tools pick them up.

The workspace MCP config points `Syncfusion_API_Key_Path` at `/Users/stephenmckitrick/.config/syncfusion/documentsdk.key`, so keep the API key in that file as a single line of text.

## Amplify CLI Reference

### Local Amplify CLI

- `amplify init` initializes a new Amplify project and must be run from an empty directory.
- `amplify add hosting` adds hosting resources to an Amplify backend project.
- `amplify configure hosting` configures hosting resources such as S3, CloudFront, and publish ignore rules.
- `amplify publish` builds and publishes the backend and frontend.
- `amplify pull <app-id>` pulls an existing Amplify app into a local workspace.
- `amplify push` deploys backend changes from the local project.
- `amplify status` shows the local and cloud backend status.

### AWS CLI Hosting Commands

- `aws amplify create-app` creates an Amplify hosting app from the AWS CLI.
- `aws amplify create-branch` creates the production or preview branch for a hosted app.
- `aws amplify update-app` updates app-level hosting settings and environment variables.
- `aws amplify list-apps` and `aws amplify list-branches` are useful for confirming hosted app state.
