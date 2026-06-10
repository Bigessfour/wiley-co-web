#!/usr/bin/env bash
# Validate (activate) an xAI API key per https://docs.x.ai/developers/quickstart
# Uses GET https://api.x.ai/v1/models with Authorization: Bearer <key>.
# Key source: XAI_API_KEY env var, or WileyCoWeb.Api/appsettings.Development.local.json
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOCAL_SETTINGS="$ROOT_DIR/WileyCoWeb.Api/appsettings.Development.local.json"
XAI_ENDPOINT="${XAI_ENDPOINT:-https://api.x.ai/v1}"

resolve_key() {
	if [[ -n ${XAI_API_KEY:-} ]]; then
		return 0
	fi
	if [[ -f $LOCAL_SETTINGS ]]; then
		export XAI_API_KEY
		XAI_API_KEY="$(
			python3 - "$LOCAL_SETTINGS" <<'PY'
import json, sys
path = sys.argv[1]
with open(path) as f:
    data = json.load(f)
print(data.get("XAI_API_KEY", "") or "")
PY
		)"
	fi
}

resolve_key

if [[ -z ${XAI_API_KEY:-} ]]; then
	echo "ERROR: No xAI key found. Set XAI_API_KEY or save via Jarvis dev key panel / appsettings.Development.local.json" >&2
	exit 1
fi

echo ">>> Validating xAI key against $XAI_ENDPOINT/models (key value not printed)"

HTTP_CODE="$(curl -sS -o /tmp/wiley-xai-models.json -w "%{http_code}" \
	"$XAI_ENDPOINT/models" \
	-H "Authorization: Bearer $XAI_API_KEY")"

if [[ $HTTP_CODE == "200" ]]; then
	MODEL_COUNT="$(python3 -c "import json; d=json.load(open('/tmp/wiley-xai-models.json')); print(len(d.get('data', d.get('models', []))))" 2>/dev/null || echo "?")"
	echo "SUCCESS: xAI key is active (HTTP 200, models returned: $MODEL_COUNT)."
	echo "Restart WileyCoWeb.Api so Semantic Kernel picks up the key for Jarvis."
	exit 0
fi

echo "FAILED: xAI key validation returned HTTP $HTTP_CODE" >&2
if [[ -f /tmp/wiley-xai-models.json ]]; then
	python3 -c "import json; d=json.load(open('/tmp/wiley-xai-models.json')); print(d.get('error', d))" 2>/dev/null || cat /tmp/wiley-xai-models.json
fi
exit 1
