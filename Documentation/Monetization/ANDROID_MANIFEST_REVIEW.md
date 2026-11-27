# Android merged manifest review (Sprint 04)

Source: `aapt2 dump permissions` / `dump xmltree` on the built APKs. Full dumps are in
`/Volumes/PLOP_DEV/Builds/FinalSprint/04_Monetization/03_Android/manifest/`.
Package `com.plop.sleepypuzzle`, versionCode 100, versionName 1.0.0, minSdk 26, targetSdk 36 (compileSdk 36).

## Permissions: before and after

| Permission | Sprint 02 (no SDK) | Sprint 04 (SDK linked: ads-disabled **and** test-ad builds) | Added by |
|---|---|---|---|
| `android.permission.INTERNET` | yes | yes | Unity (already present) |
| `android.permission.ACCESS_NETWORK_STATE` | — | **yes** | play-services-ads |
| `android.permission.WAKE_LOCK` | — | **yes** | play-services-ads / WorkManager |
| `android.permission.FOREGROUND_SERVICE` | — | **yes** | WorkManager (androidx.work) |
| `com.google.android.gms.permission.AD_ID` | — | **yes** | play-services-ads-identifier |
| `android.permission.ACCESS_ADSERVICES_AD_ID` | — | **yes** | Privacy Sandbox (play-services-ads) |
| `android.permission.ACCESS_ADSERVICES_ATTRIBUTION` | — | **yes** | Privacy Sandbox |
| `android.permission.ACCESS_ADSERVICES_TOPICS` | — | **yes** | Privacy Sandbox |
| `<package>.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION` | yes | yes | AndroidX (signature-level, internal) |

No dangerous (runtime) permission was added: no location, camera, contacts, storage or phone state.

## Components added by the SDK

- `com.google.android.gms.ads.MobileAdsInitProvider` (content provider, authority
  `com.plop.sleepypuzzle.mobileadsinitprovider`). It runs at process start even when PLOP! never calls
  `MobileAds.Initialize`. With `OPTIMIZE_INITIALIZATION` set it defers the work to a background thread.
- `AdActivity` (full-screen ad host) and the UMP consent form activity.
- AndroidX WorkManager / Startup components, `play-services` measurement service stubs, Play Core
  `HsdpShimActivity`.
- `<meta-data>`: `com.google.android.gms.ads.APPLICATION_ID` = Google's **sample** ID
  `ca-app-pub-3940256099942544~3347511713`, `com.google.android.gms.ads.flag.OPTIMIZE_INITIALIZATION` = true,
  `com.google.android.gms.ads.flag.OPTIMIZE_AD_LOADING` = true.
- `<property android.adservices.AD_SERVICES_CONFIG>` and `android.ext.adservices` uses-library (optional).

## Findings

1. **The ads-disabled build still declares AD_ID and the Privacy Sandbox permissions**, because the SDK is
   linked. Play Console will ask about advertising-ID use. For a release that must be truly ad-free, remove the
   `com.google.ads.mobile` package. `PLOP.Ads.AdMob` has a define constraint (`PLOP_HAS_GMA`, set by a
   versionDefine on the package), so the project then compiles to the Disabled service with no other code change.
2. If the audience is **child-directed**, Google Play requires that the advertising ID is not sent for children.
   Child-directed treatment in the request configuration covers the SDK. Removing `AD_ID` with a manifest
   `tools:node="remove"` entry is the conservative extra step. It was **not** added, because the audience is
   undecided.
3. The App ID in the manifest is Google's sample ID. A production build must replace it with the publisher's own
   ID (`AD_CONFIGURATION.md`, "Going to production").
4. `minSdk` stays 26, which is above the plugin's minimum of 24. `targetSdk` 36 meets Google Play's current target
   requirement.
