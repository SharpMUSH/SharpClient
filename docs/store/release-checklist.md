# First Google Play release — checklist

End-to-end sequence to get SharpClient onto the Play Store (internal testing
first). Ordered; each step's prerequisites are the steps above it.

## 0. Prerequisites (one-time)

- [ ] A Google Play Developer account (one-time US$25 registration).
- [ ] Decide the **package name is final**: `com.sharpmush.sharpclient`. This is
      permanent once uploaded and can never be changed.

## 1. Land the code changes

- [ ] Merge the branch carrying: applicationId rename, `ForegroundScale="0.65"`
      icon fix, and `docs/store/` (this checklist, privacy policy, data-safety,
      512 icon). CI (tests + Android head build) must be green.

## 2. Create the signing keystore + configure secrets  ⚠️ REQUIRED before any release

The release workflow (`.github/workflows/release-apk.yml`) **fails fast** if the
keystore secret is absent — it will never ship an unsigned/debug build. So this
must be done before tagging. Full details in
[`../release-signing.md`](../release-signing.md).

- [ ] Generate an upload keystore:
      ```bash
      keytool -genkeypair -v -keystore release.keystore \
        -alias sharpclient -keyalg RSA -keysize 2048 -validity 10000
      ```
- [ ] Store it + passwords as repository secrets (Settings → Secrets → Actions):
      `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`,
      `ANDROID_KEY_PASSWORD`.
- [ ] **Back up the keystore + passwords somewhere safe.** With Play App Signing
      (below) a lost *upload* key is recoverable; without it, losing the key means
      you can't publish updates under this app identity.

## 3. Pick the first version and tag

`release-apk.yml` derives the Android `versionName`/`versionCode` from the release
tag (`vMAJOR.MINOR.PATCH`):
`versionCode = major*100000000 + minor*10000 + patch`.

- [ ] Choose the version by **continuing the existing release-tag line**, not
      restarting. The GitHub release tags run `v0.1 … v1.5` (v1.5 =
      `versionName 1.5 / versionCode 100050000`), so the next release is **`v1.6`**
      (→ versionCode `100060000`). A `v0.x` reset would derive a *lower* code and is
      wrong. A Play `versionCode` can never be reused or decreased.
- [ ] Publish a **GitHub Release** with that tag. This triggers the workflow,
      which builds and attaches a **signed `.aab`** (for Play) and a signed `.apk`
      (for sideloading) to the release.

## 4. Play Console — create the app + upload

- [ ] Create the app in Play Console (default language, app name "SharpClient",
      app/not game — though it's a game *client*, "App" is fine).
- [ ] Accept **Play App Signing** (recommended): Google holds the app signing
      key; your keystore from step 2 is the *upload* key.
- [ ] Create an **Internal testing** release and upload the `.aab` from step 3.
      Add tester email addresses.

## 5. Play Console — required listing content

- [ ] **App icon:** `docs/store/play-listing-icon-512.png` (512×512).
- [ ] **Feature graphic:** `docs/store/play-feature-graphic-1024x500.png`
      (1024×500, required) — green M on near-black with a terminal-prompt tagline.
- [ ] **Screenshots:** at least 2 phone screenshots (existing shots live in
      `.github/assets/` and may be reused/resized).
- [ ] **Short + full description.**
- [ ] **Privacy policy URL:** host `docs/store/privacy-policy.md` publicly (see
      that file / the data-safety doc for options) and paste the URL.
- [ ] **Data safety form:** fill per [`play-data-safety.md`](./play-data-safety.md).
- [ ] **Content rating** questionnaire.
- [ ] **Target audience**, **ads declaration** (no ads), **news/COVID** (no).

## 6. Submit

- [ ] Send the internal-testing release for review; once approved, testers can
      install via the opt-in link. Promote to closed/open testing → production
      when ready.

---

### Toolchain pinning (why the release build is reproducible)

`global.json` pins the SDK **feature band**, not just the major version:

```json
{ "sdk": { "version": "10.0.300", "rollForward": "latestPatch" } }
```

`latestPatch` accepts 10.0.3xx and nothing higher. This matters because
`dotnet workload restore` resolves the MAUI manifest per band: a band with no
published workload set yet falls back to the SDK's *baseline* manifest, which can
be far older than the band suggests. That is exactly how `v1.5` failed — the
10.0.400 band appeared mid-afternoon, its baseline maui manifest (10.0.0)
regressed XAML source generation (`MAUIX2000: Cannot resolve type ...:Routes`),
and the release build broke minutes after CI had been green on 10.0.302.

To move bands, bump `version` **and** get a green Android head build on the new
band first — never by loosening `rollForward`.

### What's already done in-repo

- Signed `.aab` + `.apk` CI pipeline (`release-apk.yml`), tag→version derivation.
- Keystore generated + repository secrets uploaded (step 2).
- Target API 35 (net10 android default), minSdk 24, `allowBackup=false`.
- Final package name `com.sharpmush.sharpclient`.
- Adaptive icon fixed (`ForegroundScale=0.65`) + 512 listing icon.
- Privacy policy + data-safety content.
- Releases `v1.0` … `v1.5` tagged and built (step 3).

### Still needs a human decision / action

- Play Console account, listing copy, feature graphic, content rating (steps 4–6).
