#!/usr/bin/env bash
# Apply IAM permissions for user `copilot` so AWS CLI can inspect and test the
# Wiley Jarvis / x.ai API Gateway proxy (REST API id w544vrvb3i, us-east-2).
#
# Prerequisites: run with an identity that may iam:PutUserPolicy on `copilot`.
# Typical: AWS_PROFILE=admin ./attach-wiley-copilot-jarvis-tools.sh
#
set -euo pipefail

POLICY_NAME="WileyCopilotJarvisTools"
USER_NAME="${WILEY_COPILOT_USER:-copilot}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
POLICY_DOC="${ROOT}/iam/policies/wiley-copilot-jarvis-tools.json"

if [[ ! -f ${POLICY_DOC} ]]; then
	echo "Missing policy file: ${POLICY_DOC}" >&2
	exit 1
fi

aws iam put-user-policy \
	--user-name "${USER_NAME}" \
	--policy-name "${POLICY_NAME}" \
	--policy-document "file://${POLICY_DOC}"

echo "Attached inline policy ${POLICY_NAME} to IAM user ${USER_NAME}."
echo ""
echo "IMPORTANT: Signed HTTPS calls to execute-api also require the REST API resource policy"
echo "to allow this IAM user. Append the statements in:"
echo "  iam/policies/wiley-jarvis-api-resource-policy-copilot-statements.json"
echo "to the existing WileyJarvisApi (w544vrvb3i) resource policy Statement array."
echo "Do not replace the whole policy or VPC/source allow-lists will break."
echo ""
echo "Verify:"
echo "  aws sts get-caller-identity --profile townofwiley"
echo "  aws apigateway get-rest-api --rest-api-id w544vrvb3i --region us-east-2 --profile townofwiley"
