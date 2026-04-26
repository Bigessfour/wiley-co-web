#!/usr/bin/env bash
set -euo pipefail

npm run playwright:test:ci
node ./Scripts/assert-playwright-results.mjs