#!/usr/bin/env bash
# Block git push from Cursor agents unless ./Scripts/run-ci-local.sh succeeded for HEAD.
# Bypass: SKIP_LOCAL_CI=1 (emergency only).
set -euo pipefail

input="$(cat)"
command="$(printf '%s' "$input" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("command") or "")' 2>/dev/null || true)"

# Non-push commands: allow.
if [[ ! "$command" =~ (^|[[:space:];|&])git[[:space:]]+push ]]; then
  printf '%s\n' '{"permission":"allow"}'
  exit 0
fi

if [[ "${SKIP_LOCAL_CI:-}" == "1" ]]; then
  printf '%s\n' '{"permission":"allow","agent_message":"SKIP_LOCAL_CI=1 — pushing without local CI stamp."}'
  exit 0
fi

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
STAMP="$ROOT/.local-ci-ok"

if [[ ! -f "$STAMP" ]]; then
  printf '%s\n' '{"permission":"deny","user_message":"Run ./Scripts/run-ci-local.sh before git push (creates .local-ci-ok).","agent_message":"Blocked git push: no .local-ci-ok stamp. Run ./Scripts/run-ci-local.sh (pre-push gate) first, then push."}'
  exit 0
fi

stamp_sha="$(awk -F= '/^sha=/{print $2; exit}' "$STAMP" 2>/dev/null || true)"
head_sha="$(git -C "$ROOT" rev-parse HEAD 2>/dev/null || true)"

if [[ -z "$stamp_sha" || -z "$head_sha" || "$stamp_sha" != "$head_sha" ]]; then
  printf '%s\n' "{\"permission\":\"deny\",\"user_message\":\"Local CI stamp is stale (stamp=$stamp_sha head=$head_sha). Re-run ./Scripts/run-ci-local.sh.\",\"agent_message\":\"Blocked git push: .local-ci-ok SHA does not match HEAD. Run ./Scripts/run-ci-local.sh again after your latest commit.\"}"
  exit 0
fi

printf '%s\n' '{"permission":"allow"}'
exit 0
