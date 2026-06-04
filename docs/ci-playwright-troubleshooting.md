# CI Playwright troubleshooting

## What runs in GitHub Actions

The `playwright-ui` job (`.github/workflows/ci.yml`) runs only:

- `npm run playwright:test:ci:smoke` — `workspace-smoke.spec.ts`
- `npm run playwright:test:ci:highrisk` — three curated specs (Chromium)

Full suite: local `npm run playwright:test:strict` or `npm run playwright:test:docker-proof`.

## Workflow status: cancelled vs skipped vs failed

| GitHub conclusion | Typical cause |
| --- | --- |
| **cancelled** (whole workflow) | Job hit `timeout-minutes: 50` on `playwright-ui`, manual cancel, or platform abort. Run **26954265012** cancelled because **Install Playwright browsers** never finished before the job timeout — tests never started. |
| **skipped** (`playwright-ui`) | Upstream `build-and-publish` (or `test` / `highrisk-tests`) failed. |
| **success** with red test steps | Smoke/highrisk use `continue-on-error`; check `playwright-report/results.json` in the `playwright-ui-report` artifact. |

## Diagnosis checklist

1. `gh run view <run-id>` — confirm whether `playwright-ui` ran or was skipped.
2. Open the **Install Playwright browsers + Linux deps** step — should finish in a few minutes (main run **26474950137** completed `playwright-ui` in ~2.5m).
3. Download artifact `playwright-ui-report` and read `stats.unexpected` in `playwright-report/results.json`.
4. Ensure repo secret `SYNCFUSION_LICENSE_KEY` is set (required for `build-and-publish` on Windows).

## Reference runs (June 2026)

- **26954265012** (`feat/local-windows-machine-support`): upstream green; `playwright-ui` **cancelled** at 50m — stuck on browser install; no Playwright artifacts.
- **26474950137** (`main`): `playwright-ui` **success** — `expected=4`, `unexpected=0`.

## CI hardening (2026-06)

- 12-minute step timeout on Playwright install + cache-clear retry.
- Assert step no longer uses `continue-on-error` (unexpected failures fail the job).
- Job summary step writes result stats to the Actions summary when `results.json` exists.
