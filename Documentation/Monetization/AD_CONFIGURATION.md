# PLOP! advertising configuration (Final Sprint 04)

Status: **IMPLEMENTED** (Disabled, Mock and Google test ads). **Production: BLOCKED.** It needs publisher
approval, the publisher's own AdMob account and IDs, and a confirmed audience.

## SDK

| Component | Version | Source |
|---|---|---|
| Google Mobile Ads Unity plugin | **11.7.0** (`com.google.ads.mobile`) | OpenUPM scoped registry `package.openupm.com`, scope `com.google.ads.mobile` only |
| External Dependency Manager for Unity | 2.1.1 (`com.unity.external-dependency-manager`) | dependency of the plugin |
| Android: `play-services-ads` | 25.5.0 | Maven, resolved by EDM / Gradle |
| Android: User Messaging Platform (`user-messaging-platform`) | 4.0.0 | Maven |
| iOS: `GoogleMobileAds` | **13.13.0** resolved (requirement ≥ 13.12.0, < 14) | **Swift Package Manager** (`github.com/googleads/swift-package-manager-google-mobile-ads`), added to the exported Xcode project by EDM (`swiftPackageManagerEnabled`). The pod `Google-Mobile-Ads-SDK` is the fallback, but it is not used. |
| iOS: `GoogleUserMessagingPlatform` | 3.1.0 (exact) | Swift Package Manager (`github.com/googleads/swift-package-manager-google-user-messaging-platform`) |

The plugin is the official Google plugin, unmodified. No mediation adapters, analytics, attribution or crash SDKs
were added.

## Where the code lives

| Layer | Path | Engine? | Role |
|---|---|---|---|
| Rules | `Assets/Scripts/Meta/Ads/` (`Plop.Meta.Ads`) | none | `AdConfiguration`, `AdFrequencyPolicy`, `RewardTransactionService`, `IAdService`, `DisabledAdService`, `MockAdService`, `AdServiceRegistry`, `HouseAdCatalog`, `AdUnitIds` |
| SDK adapter | `Assets/Scripts/Ads/AdMob/` (assembly `PLOP.Ads.AdMob`) | Unity + SDK | `AdMobAdService`, the **only** code that references `GoogleMobileAds.*` (enforced by a PlayMode test) |
| Game glue | `Assets/Scripts/Unity/Ads/`, `PlopGameController.Ads.cs`, `GameHud.Ads.cs`, `MainMenu.Ads.cs` | Unity | `PlopAds` coordinator, mode selection, offer cards, privacy entry |

Puzzle rules, the solver, level data and generation do not know ads exist.

## Modes

| Mode | Selected by | What happens |
|---|---|---|
| **Disabled** (default) | every normal build | SDK linked but never initialized: no consent form, no ad request, no ad UI. |
| Mock | `PLOP_ADS_MOCK` define, or dev preference `plop.debug.ads=mock` | Simulated ads, no SDK, no network (automated tests, UI work). |
| Test | `PLOP_ADS_TEST` define (the `PlopAdsBuild.BuildTestAdsApk` entry), or dev preference `plop.debug.ads=test` | Real SDK, **Google's official test unit IDs only**. Test mode ignores any other ID. |
| Production | `AdConfiguration.Mode = Production` | **Always falls back to Disabled** in this sprint (see the gates below). |

The `plop.debug.ads` preference is read only in the Editor and in development builds.

`AdConfiguration.EffectiveMode()` turns Production into Disabled when any of these holds:
1. `AdConfiguration.ProductionUnlocked` is the compile-time constant `false` (Sprint 04).
2. `Audience` is still `Unconfirmed`.
3. Any production app ID or unit ID is empty or is a Google test ID.

All six production ID fields are empty strings in the repository. **No real AdMob ID exists in this project.**

## App IDs in the plugin settings

The Android plugin stops the build when no AdMob app ID is set. `PlopAdsBuild.ConfigureAdMobTestSettings` therefore
writes Google's **published sample app IDs** to `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`:

- Android `ca-app-pub-3940256099942544~3347511713`
- iOS `ca-app-pub-3940256099942544~1458002511`

These IDs are public, not this app's, and earn nothing. If the asset already holds a non-sample ID, the setup step
keeps it. The App Tracking Transparency description stays empty, so no ATT prompt and no
`NSUserTrackingUsageDescription` are added.

## Formats

| Format | Status | Placement |
|---|---|---|
| Interstitial | implemented, test only | Natural break after a finished level, **after the player taps Next Level**. |
| Rewarded: bonus Dream Dust | implemented, test only | Main menu ▸ "Bonus Dream Dust". Voluntary card with the exact amount. |
| Rewarded: free verified hint | implemented, test only | Hint button, when a hint would cost Dust. A choice card: Dust, ad or "No, thanks". |
| House ads | framework only, **empty and disabled** | `HouseAdCatalog`; `HouseAdsEnabled = false`; no catalog file ships. |
| Banner, app open, rewarded interstitial, native | **not implemented** | not allowed by the brief |

## Interstitial policy (`AdFrequencyPolicy`): every condition must hold

| Rule | Value | Notes |
|---|---|---|
| Ad-free opening | levels 1–10 | `AdFreeLevels = 10`; `Validate()` rejects lower values |
| New completions between ads | ≥ 4 | first-time completions only; replays, Restart and Undo never count |
| Active play between ads | ≥ 180 s | foreground, board in play, input in the last 30 s; ≤ 1 s counted per frame |
| Rolling cap | ≤ 4 per 30 min | real presentations only, never failed requests; a clock moved back cannot reduce the count |
| New-player protection | first 300 s of active play | counted from the first launch of an ads build |
| After a rewarded ad | never right after | at least 1 new completion **and** 180 s since the rewarded ad |
| Tutorials | never | when the finished or the next level carries an instruction |
| Replayed levels | never | only a first-time completion is a natural break |

State lives in one local preference, `plop.ads.frequency.v1`: counters and impression timestamps only, no
identifiers. A damaged record restarts conservatively: protection, spacing and play time all apply again.

**Flow:** level complete → rewards committed (wallet commit) → victory card → player taps **Next Level** →
policy check → shown **only if already loaded**. Otherwise the next level loads immediately, with no spinner and
no wait. While an ad is open, gameplay input and navigation are blocked and a second Next does nothing. The next
level loads exactly once: on close, on failure, or after the watchdog (30 s of foreground time with no SDK
callback).

## Rewarded rules

- Voluntary only. Nothing auto-plays, and no reward is ever offered without the player asking.
- Every offer states the exact reward and has a **"No, thanks"** option of the same size and colour. A small
  "Ad" tag marks the option that plays an ad. Back declines.
- Rewards are granted **only on the SDK reward event**. Closing early, failing to load or failing to show grants
  nothing.
- Bonus Dream Dust: **15 per ad, at most 3 per local day** (45/day). The brief allowed up to 25. 15 was chosen
  because 25 × 3 = 75/day would outpace the whole campaign's Dust (about 1,920 in total) within a month; 15 keeps
  ads a small top-up.
- Free hint: the ad earns a **saved free-hint entitlement**. If the board is unchanged when the ad closes, it is
  used at once on the verified route found before the ad. If the board changed, it is kept for the player's next
  hint request. A stale route is never shown. The Dust option keeps its price (15). The existing "first hint free
  after 2 tries" grace is unchanged, and the ad offer is skipped when that grace already covers the player.
- A reward is offered only when an ad is already loaded. Otherwise the existing Dust flow runs unchanged.

## Transactions

- Bonus Dust is paid under the wallet grant key `addust:<local date>:<id>`. A free hint is the entitlement
  `adhint:<id>`; redeeming it writes `adhint-used:<id>` and the board's hint key in **one** atomic wallet commit
  (with `.bak` backup).
- Idempotent: duplicate reward callbacks, a reward arriving after close (0.75 s grace) or both together pay once.
- A transaction is bound to the wallet (profile) it began with. A reward from profile A never pays profile B.
- **Limits:** this is client-side bookkeeping for a small cosmetic currency. It prevents accidental duplicates, not a
  player who edits local storage or replays network traffic. AdMob server-side verification (SSV) needs a
  backend, which PLOP! does not have. Dream Dust buys only cosmetics and hints and has no real-money value, so that
  risk is accepted and documented.

## Consent, audience and request configuration

1. `MobileAds.SetRequestConfiguration`: `MaxAdContentRating = G`. When the audience is MixedAudience or
   ChildDirected: `TagForChildDirectedTreatment` / `TagForUnderAgeOfConsent` = true and
   `AgeRestrictedTreatment.Child`.
2. `ConsentInformation.Update` (UMP), with the under-age tag following the audience.
3. `ConsentForm.LoadAndShowConsentFormIfRequired`.
4. `MobileAds.Initialize` **only if** `ConsentInformation.CanRequestAds()` is true. Without it there is no
   request and no ad UI.
5. Settings ▸ **Privacy Choices** appears only when `PrivacyOptionsRequirementStatus == Required` and opens
   `ConsentForm.ShowPrivacyOptionsForm`.

Consent starts from the main menu after the splash, never during a level.

## Audio and lifecycle

- `PlopAudio.SuspendForFullscreenAd()` stops effects and pauses music and ambience at their position before the ad.
  `ResumeAfterFullscreenAd()` restores them on close, failure or watchdog.
- The SDK follows the game's master mute (`MobileAds.SetApplicationMuted`), so a muted player never hears an ad.
- iOS: `SetiOSAppPauseOnBackground(true)`.
- Pause and resume never show an ad. The frequency record is saved on pause. A loaded ad older than 55 minutes is
  dropped (Google ads expire after 1 hour).
- Offline: loads are skipped, then retried with bounded exponential backoff (30 s base, 4 tries per session).

## Invalid-traffic safety

- Test builds use only Google test units, and production is locked.
- No incentive to click: rewards come for watching, never clicking, and no copy mentions clicking.
- No ad overlaps game buttons; full-screen formats only.
- No ad is reloaded in a loop: one load per format, single-use, bounded retries.
- Developers must not tap live ads on their own devices. When production is approved, register test devices
  (`RequestConfiguration.TestDeviceIds`) before any live-ID testing.

## Build entry points (`Assets/Scripts/Editor/PlopAdsBuild.cs`)

```
-executeMethod Plop.EditorTools.PlopAdsBuild.ConfigureAdMobTestSettings
-executeMethod Plop.EditorTools.PlopAdsBuild.BuildAdsDisabledApk     # 01_AdDisabledBuild
-executeMethod Plop.EditorTools.PlopAdsBuild.BuildTestAdsApk         # 02_TestAdBuild (PLOP_ADS_TEST, development)
-executeMethod Plop.EditorTools.PlopAdsBuild.BuildAdsDisabledBundle  # 03_Android (AAB)
-executeMethod Plop.EditorTools.PlopAdsBuild.ExportAdsIOSSimulator   # 04_iOS
```

Outputs go to `/Volumes/PLOP_DEV/Builds/FinalSprint/04_Monetization/`. `PLOP_ADS_TEST` is passed as a per-build
`extraScriptingDefines`, so Player Settings are never changed.

## Going to production (publisher steps; none were done)

1. The publisher creates their own AdMob account and app entries, and accepts Google's terms themselves.
2. Confirm the audience (see `AUDIENCE_ASSESSMENT.md`) and set `AdConfiguration.Audience`.
3. Put the real app IDs in Google Mobile Ads settings and the six unit IDs in `AdConfiguration`. Register test
   devices.
4. Publish the privacy policy (draft: `PRIVACY_POLICY_DRAFT.md`) and complete Data safety and App Privacy from the
   drafts.
5. Publish `app-ads.txt` on the developer website (see `APP_ADS_TXT_READINESS.md`).
6. Change `ProductionUnlocked` to `true` in a reviewed commit, then re-run every test and the QA checklist.
