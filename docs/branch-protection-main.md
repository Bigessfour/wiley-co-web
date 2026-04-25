# Branch Protection for main

## Required settings

- Require a pull request before merging
- Require 1 approval
- Dismiss stale approvals
- Require status checks to pass before merging
- Require branches to be up to date before merging
- Include administrators
- Disable force pushes
- Disable branch deletions

## Required status checks

- Wiley.co CI / test (component)
- test (integration)
- test (widget)
- test (e2e)
- build-and-publish
- playwright-ui

## Workflow mapping

- The checks above map to `.github/workflows/ci.yml`.
- `deployment-guard` runs only on pushes to `main` and is a post-merge safety check, not a pull-request gate.

## Note

Apply these settings manually in GitHub repository settings.
