# wiley-co-web

Wiley Widget for AWS

## Amplify Hosting

This app is hosted on AWS Amplify in `us-east-2`.

- App name: `wiley-co-web`
- App id: `d2ellat1y3ljd9`
- Production branch: `main`
- Default domain: `d2ellat1y3ljd9.amplifyapp.com`

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
