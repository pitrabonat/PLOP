# Store privacy declarations: drafts (Google Play Data safety, Apple App Privacy)

Status: **AWAITING PUBLISHER APPROVAL.** Nothing was submitted to any store console. Before submitting, check
these answers against Google's current page "Google Mobile Ads SDK: data disclosure" (Android) and "Google Mobile
Ads SDK: Apple App Privacy" (iOS), as they can change.

## Which build is which

| Build | SDK linked | SDK initialized | Ad requests |
|---|---|---|---|
| `01_AdDisabledBuild` APK, `03_Android` AAB (shipping default) | yes | **no** | none |
| `02_TestAdBuild` APK (QA only, never shipped) | yes | yes | Google test ads |
| Future production (blocked) | yes | yes | AdMob |

**Important for the ads-disabled release.** The Google Mobile Ads SDK is **linked** even when PLOP! never
initializes it. The merged Android manifest then contains the SDK's permissions (see `ANDROID_MANIFEST_REVIEW.md`),
including `com.google.android.gms.permission.AD_ID`. Google Play asks about the advertising ID whenever that
permission is present. Two honest ways to release without ads:

1. **Recommended for a no-ads store release:** remove the `com.google.ads.mobile` package from
   `Packages/manifest.json`. `PLOP.Ads.AdMob` is then excluded by its `PLOP_HAS_GMA` define constraint, the game
   runs the Disabled service, and you can declare "No data collected". This was not test-built in Sprint 04.
2. Keep the SDK linked but uninitialized, and declare the AD_ID permission honestly ("the app does not use the
   advertising ID"). This is weaker and invites reviewer questions.

## A. Google Play Data safety: version with ads enabled (draft answers)

| Question | Draft answer |
|---|---|
| Does your app collect or share any required user data types? | **Yes** (through the Google Mobile Ads SDK) |
| Is all user data encrypted in transit? | **Yes** |
| Do you provide a way to request data deletion? | The developer holds no data. Point users to Google's controls and the contact email. [PUBLISHER to confirm the wording] |

| Data type | Collected | Shared | Optional? | Purposes |
|---|---|---|---|---|
| Location ▸ Approximate location (from IP) | Yes | Yes (Google) | Required while ads are on | Advertising or marketing, Analytics, Fraud prevention & security |
| App activity ▸ App interactions (ad impressions, taps, launches) | Yes | Yes | Required | Advertising, Analytics, Fraud prevention |
| App info & performance ▸ Crash logs, Diagnostics | Yes | Yes | Required | Analytics, Fraud prevention |
| Device or other IDs ▸ Advertising ID / app set ID | Yes | Yes | Required | Advertising, Analytics, Fraud prevention |
| Personal info, financial info, messages, photos, contacts, health, files, web history | **No** | No | — | — |

If the audience is mixed or child-directed: every request uses child-directed treatment, so personalized ads are
off. Also complete the **Families** section and the Target audience form.

## B. Google Play Data safety: ads-disabled release

- With the SDK **removed**: "No data collected", "No data shared".
- With the SDK **linked but not initialized** (current builds): the game sends no data, but the manifest declares
  `AD_ID`. Answer the "Advertising ID" question as "the app does not use the advertising ID" and note that ads are
  disabled. [PUBLISHER decision; option 1 above is cleaner.]

## C. Apple App Privacy ("nutrition label"): version with ads enabled (draft)

PLOP! does **not** use App Tracking Transparency and never shows the ATT prompt, so the IDFA is unavailable.

The table below is copied from the SDKs' own privacy manifests in the Sprint 04 Simulator build (Google Mobile Ads
**13.13.0** and UMP **3.1.0**, fetched by Swift Package Manager; read with `plutil` on 27 Nov 2026). It replaces an
earlier hand-written guess, which wrongly marked location and identifiers as "not linked".

| Data type (Apple category) | Collected | Linked to user | Used for tracking | Purposes (as declared by the SDK) |
|---|---|---|---|---|
| Identifiers ▸ Device ID | Yes (GMA) | **Yes** | **Yes** (declared by GMA; without ATT consent iOS returns no IDFA) | Third-party advertising, Developer's advertising, Analytics |
| Location ▸ Coarse location | Yes (GMA, UMP) | **Yes** (GMA); No (UMP) | No | Third-party advertising, Developer's advertising, Analytics |
| Usage data ▸ Advertising data | Yes (GMA) | **Yes** | No | Third-party advertising, Developer's advertising, Analytics |
| Usage data ▸ Product interaction | Yes (GMA, UMP) | **Yes** (GMA); No (UMP) | No | Analytics, Developer's advertising, Third-party advertising |
| Diagnostics ▸ Crash data | Yes (GMA) | No | No | Analytics |
| Diagnostics ▸ Performance data | Yes (GMA, UMP) | No | No | Third-party advertising, Developer's advertising, Analytics |
| Diagnostics ▸ Other diagnostic data | Yes (GMA) | No | No | Third-party advertising, Developer's advertising, Analytics |
| Contact info, health, financial, contacts, user content, browsing, search, sensitive info | **No** | — | — | — |

Because GMA declares Device ID as used for tracking, the publisher must decide on submission day whether the app
needs ATT. PLOP! itself does no tracking and is configured never to show the prompt. With no ATT the IDFA is zeroed,
but the App Privacy answers should still follow Xcode's privacy report and Google's current guidance. [PUBLISHER
decision; for a mixed or child-directed audience, tracking must not happen.]

Ads-disabled iOS release: "Data Not Collected" is accurate only if the SDK is **not linked** or Apple accepts
that an uninitialized SDK collects nothing. As with Android, removing the package is the clean option.

## D. iOS privacy manifest (PrivacyInfo.xcprivacy)

- Google Mobile Ads 13.13.0 and UMP 3.1.0 come in as **Swift packages** (binary xcframeworks). Each ships its own
  `PrivacyInfo.xcprivacy`, and Xcode merges them into the app's privacy report. The GMA manifest declares the
  required-reason APIs SystemBootTime (35F9.1), UserDefaults (CA92.1) and DiskSpace (E174.1).
- Unity 6 generates the engine's `UnityFramework.framework/PrivacyInfo.xcprivacy`; it is present in the built app.
- PLOP! adds no tracking domains, and `NSPrivacyTracking` for the app's own code is `false`.
- Status: the Simulator build (`04_iOS/`, `xcodebuild` **BUILD SUCCEEDED**) contains these manifests. A **device
  archive** and Xcode's "Generate Privacy Report" were not produced: there is no signing team on this Mac.

## E. SKAdNetwork

The plugin's PList post-processor adds Google's `SKAdNetworkItems` list (from `GoogleMobileAdsSKAdNetworkItems.xml`,
plugin 11.7.0) and `GADApplicationIdentifier` to Info.plist. For mixed or child-directed audiences, SKAdNetwork
attribution is still allowed, but the publisher should review it.
