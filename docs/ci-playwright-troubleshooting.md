# CI Playwright troubleshooting

## What runs in GitHub Actions

The `playwright-ui` job (`.github/workflows/ci.yml`) runs only:

- `npm run playwright:test:ci:smoke` — `workspace-smoke.spec.ts`
- `npm run playwright:test:ci:highrisk` — three curated specs (Chromium)

Full suite: local `npm run playwright:test:strict` or `npm run playwright:test:docker-proof`.

## Install step timeouts (`--with-deps` / apt hangs)

**Symptom:** `Install Playwright browsers` fails after 8–12 minutes; log stops at `Installing dependencies...` or `Processing triggers for man-db`.

**Cause (upstream, not repo-specific):**

- [actions/runner-images#11347](https://github.com/actions/runner-images/issues/11347) — `playwright install --with-deps` can hang on `apt-get` on `ubuntu-latest`.
- [microsoft/playwright#14434](https://github.com/microsoft/playwright/issues/14434) — `man-db` post-install triggers often take many minutes on GHA runners.

**Fix in this repo (2026-06):** `playwright-ui` runs inside `mcr.microsoft.com/playwright:v1.59.1-noble` with `--ipc=host`. Install step is `npx playwright install chromium` **without** `--with-deps` (OS libraries already in the image). Matches local `Dockerfile.playwright-ci`.

**Fallback if you must use bare `ubuntu-latest`:**

```yaml
- name: Avoid man-db apt hang (playwright#14434)
  run: |
    sudo rm -f /var/lib/man-db/auto-update || true
    sudo apt-get --yes remove --purge man-db || true
- name: Install Playwright browsers
  run: DEBIAN_FRONTEND=noninteractive npx playwright install --with-deps chromium
```

See also [Streamlit’s playwright_install action](https://github.com/streamlit/streamlit/blob/master/.github/actions/playwright_install/action.yml) for the same `man-db` pattern.

## Workflow status: cancelled vs skipped vs failed

| GitHub conclusion | Typical cause |
| --- | --- |
| **cancelled** (whole workflow) | Job hit `timeout-minutes: 50`, manual cancel, or install hung before step timeout. |
| **skipped** (`playwright-ui`) | Upstream `build-and-publish` failed. |
| **success** with red test steps | Smoke/highrisk use `continue-on-error`; check `playwright-report/results.json`. |

## Diagnosis checklist

1. `gh run view <run-id>` — confirm whether `playwright-ui` ran or was skipped.
2. **Install Playwright browsers (container image; no apt)** should finish in under ~2 minutes on cache hit.
3. Download artifact `playwright-ui-report` and read `stats.unexpected` in `playwright-report/results.json`.
4. Ensure repo secret `SYNCFUSION_LICENSE_KEY` is set.

## Reference runs (June 2026)

- **26954265012**: install hung until job timeout (pre-container fix).
- **26474950137** (`main`): `playwright-ui` success — `expected=4`, `unexpected=0`.
