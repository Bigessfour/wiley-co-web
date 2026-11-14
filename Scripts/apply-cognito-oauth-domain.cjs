#!/usr/bin/env node
/**
 * Write wwwroot/appsettings.Auth.local.json so the Hosted UI domain overrides the
 * committed placeholder. The domain is never stored in git.
 *
 * Requires COGNITO_OAUTH_DOMAIN=https://<prefix>.auth.<region>.amazoncognito.com
 */
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const target = path.join(root, "wwwroot", "appsettings.Auth.local.json");
const domain = (process.env.COGNITO_OAUTH_DOMAIN || "").trim().replace(/\/$/, "");
const placeholder = "REPLACE_WITH_COGNITO_HOSTED_UI_PREFIX";

function fail(message) {
  console.error(`ERROR: ${message}`);
  process.exit(1);
}

if (!domain) {
  fail("Set COGNITO_OAUTH_DOMAIN to the Cognito Hosted UI origin.");
}

if (domain.includes(placeholder)) {
  fail("COGNITO_OAUTH_DOMAIN is still the placeholder. Create the Cognito domain first.");
}

let parsed;
try {
  parsed = new URL(domain);
} catch {
  fail("COGNITO_OAUTH_DOMAIN must be an absolute https URL.");
}

if (parsed.protocol !== "https:" || !parsed.hostname.endsWith(".amazoncognito.com")) {
  fail("COGNITO_OAUTH_DOMAIN must be an https://*.amazoncognito.com origin.");
}

let existing = {};
if (fs.existsSync(target)) {
  existing = JSON.parse(fs.readFileSync(target, "utf8"));
}

existing.Auth = existing.Auth || {};
existing.Auth.Cognito = existing.Auth.Cognito || {};
existing.Auth.Cognito.OAuthDomain = domain;

fs.mkdirSync(path.dirname(target), { recursive: true });
fs.writeFileSync(target, `${JSON.stringify(existing, null, 2)}\n`);
console.log(`Wrote Hosted UI domain into ${path.relative(root, target)} (file is gitignored).`);
