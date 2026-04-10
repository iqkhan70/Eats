#!/usr/bin/env node
/**
 * Prints signing cert from a release/debug APK (SHA-1 / SHA-256) using keytool.
 * Add the SHA-1 to the matching Android OAuth client in Google Cloud (Play = App signing cert from Console).
 *
 * Usage: node scripts/print-apk-signing-sha.js <path-to.apk>
 *    or: npm run google:apk-sha1 -- path/to.apk
 */
const { spawnSync } = require("child_process");
const apk = process.argv[2];
if (!apk) {
  console.error("Usage: npm run google:apk-sha1 -- <path-to.apk>");
  process.exit(1);
}
const r = spawnSync("keytool", ["-printcert", "-jarfile", apk], {
  encoding: "utf8",
});
if (r.status !== 0) {
  console.error(
    r.stderr ||
      "keytool failed. Use JDK keytool (e.g. export JAVA_HOME for Android Studio’s JBR).",
  );
  process.exit(r.status || 1);
}
process.stdout.write(r.stdout);
