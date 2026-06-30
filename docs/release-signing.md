# Android release signing

The `Release Android (signed APK + AAB)` workflow
(`.github/workflows/release-apk.yml`) builds **signed** Android artifacts when a
GitHub Release is published:

- a signed `.apk` for direct sideloading / out-of-store distribution, and
- a signed `.aab` (Android App Bundle) required for Google Play uploads.

Signing is driven entirely by repository secrets. If the keystore secret is
missing the workflow fails fast rather than shipping a debug-signed build.

## Required repository secrets

Add these under **Settings -> Secrets and variables -> Actions** (repository
secrets):

| Secret | Description |
| --- | --- |
| `ANDROID_KEYSTORE_BASE64` | The release keystore (`.jks`/`.keystore`), base64-encoded. Decoded to a file at build time. |
| `ANDROID_KEYSTORE_PASSWORD` | The keystore (store) password. |
| `ANDROID_KEY_ALIAS` | The alias of the signing key inside the keystore. |
| `ANDROID_KEY_PASSWORD` | The password for that key alias. |

## Generating a keystore

Use the JDK `keytool` (RSA 2048, 10000-day validity is the Play-recommended
minimum for an upload/signing key):

```bash
keytool -genkeypair -v \
  -keystore release.keystore \
  -alias sharpclient \
  -keyalg RSA -keysize 2048 -validity 10000
```

You will be prompted for the store password, the key password, and a
distinguished name. Keep the keystore and both passwords safe and backed up —
losing the signing key means you can no longer publish updates under the same
app identity (unless enrolled in Play App Signing).

## Uploading the keystore as a secret

Base64-encode the keystore and store the result in `ANDROID_KEYSTORE_BASE64`:

```bash
# macOS / Linux: -w0 keeps it on a single line (GNU coreutils);
# on macOS use `base64 -i release.keystore | tr -d '\n'`.
base64 -w0 release.keystore > release.keystore.b64

# Then paste the contents of release.keystore.b64 into the secret value,
# or with the GitHub CLI:
gh secret set ANDROID_KEYSTORE_BASE64 < release.keystore.b64
gh secret set ANDROID_KEYSTORE_PASSWORD
gh secret set ANDROID_KEY_ALIAS
gh secret set ANDROID_KEY_PASSWORD
```

## Notes

- The workflow trigger is GitHub `release: published` (plus manual
  `workflow_dispatch`, which uploads to the workflow run only). Do **not** run
  the release workflow until the secrets above are configured.
- The decoded keystore file lives only on the ephemeral runner and is removed at
  the end of the job.
