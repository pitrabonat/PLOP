# PLOP! Final Sprint 04/06: AdMob integration, monetization architecture, privacy and ad QA (handoff)

**Live ads are off.** Every build in this sprint ships with ads **Disabled**, or with **Google's official test
ads** (QA build only). No AdMob account, payout, payment profile, store listing or agreement was created, accepted
or changed. No revenue exists. Production mode is locked in code (`AdConfiguration.ProductionUnlocked = false`).

Labels used below:
- **IMPLEMENTED**: in the code or config, built.
- **TESTED**: verified by automated tests and/or a run on the emulator or Simulator (named each time).
- **AWAITING PUBLISHER APPROVAL**: needs a publisher decision or account that only the publisher can make.
- **BLOCKED**: cannot be done under the current rules or tools.

Details are in `Documentation/Monetization/`:
- `AD_CONFIGURATION.md`, `AUDIENCE_ASSESSMENT.md`, `PRIVACY_POLICY_DRAFT.md`
- `STORE_PRIVACY_DECLARATIONS_DRAFT.md`, `APP_ADS_TXT_READINESS.md`
- `ANDROID_MANIFEST_REVIEW.md`, `IOS_STATUS.md`, `PERFORMANCE.md`, `DEVICE_QA.md`, `REVENUE_MODEL_HYPOTHETICAL.md`

## 1. Installed SDK version: IMPLEMENTED, TESTED

| Component | Version actually in the builds |
|---|---|
| Google Mobile Ads Unity plugin | **11.7.0** (`com.google.ads.mobile`, OpenUPM scoped registry) |
| External Dependency Manager | 2.1.1 (`com.unity.external-dependency-manager`) |
| Android | `play-services-ads` **25.5.0**, `user-messaging-platform` **4.0.0** (Gradle, via EDM) |
| iOS | `GoogleMobileAds` **13.13.0** and `GoogleUserMessagingPlatform` **3.1.0**, as **Swift packages** (no CocoaPods) |

## 2. Advertising architecture: IMPLEMENTED, TESTED

- **Rules layer**: `Assets/Scripts/Meta/Ads/` (`Plop.Meta.Ads`, engine-free). It holds:
  - `AdConfiguration`, `AdFrequencyPolicy`, `RewardTransactionService`
  - `IAdService`, with `DisabledAdService`, `MockAdService` and `AdServiceRegistry`
  - `HouseAdCatalog`, which is empty and disabled
  - `AdUnitIds`, which contains Google's test IDs only
- **SDK adapter**: `Assets/Scripts/Ads/AdMob/AdMobAdService.cs`, assembly `PLOP.Ads.AdMob`.
  - It compiles only when the plugin package is present (`PLOP_HAS_GMA` versionDefine).
  - It is the only code that references `GoogleMobileAds.*`. A PlayMode test enforces this.
- **Game glue**:
  - `Assets/Scripts/Unity/Ads/PlopAds.cs` (coordinator, audio, watchdog, lifecycle) and `PlopAdsSetup.cs` (mode selection)
  - `PlopGameController.Ads.cs`, `GameHud.Ads.cs`, `MainMenu.Ads.cs`
- **Modes**:
  - **Disabled** is the default: the SDK is never initialized.
  - **Mock** is for tests.
  - **Test** uses official test units, set by the `PLOP_ADS_TEST` per-build define.
  - **Production** always falls back to Disabled.
- **Formats**:
  - Interstitial.
  - Rewarded: bonus Dream Dust and a free verified hint.
  - House ads: framework only, disabled.
  - Not implemented: banner, app-open, rewarded interstitial, native.

## 3. Interstitial frequency: IMPLEMENTED, TESTED

Every condition must hold:
- **No ads in levels 1–10.**
- **≥ 4 new completions and ≥ 180 s of active play** since the last ad.
- **≤ 4 per rolling 30 min.**
- **None in the first 300 s of active play.**
- **Never right after a rewarded ad** (at least 1 completion and 180 s must pass).
- Never in tutorials or replays.
- Only real impressions count.
- State is stored locally without identifiers (`plop.ads.frequency.v1`). A corrupt record is treated strictly.

Flow:
1. The level completes and the result is committed.
2. The victory card appears.
3. The player taps **Next Level**.
4. An ad shows **only if one is already loaded**. Otherwise the game continues immediately.
5. After the ad closes, the next level opens exactly once, including when the SDK reports the close before the app
   resumes. That bug was found on the emulator and is fixed and tested.

## 4. Rewarded configuration: IMPLEMENTED, TESTED

- Voluntary only. Every offer states the exact reward, has an equal "No, thanks", and Back declines.
- A reward is paid **only on the SDK reward event**.
- **Bonus Dream Dust**: main menu, **15 per ad, max 3 per local day**.
- **Free hint**: offered when a hint would cost Dust. The Dust option and its price (15) remain.
  - The earned entitlement is used on the same board only. If the board changed during the ad, it is saved for the
    next hint request.
  - A stale hint is never shown.
  - The existing grace hint is unchanged.

## 5. Dream Dust economy changes: IMPLEMENTED

- No price changed, and no existing reward changed.
- Additions:
  - Ad bonus Dust (≤ 45/day).
  - Ad hint entitlements, which replace a 15-Dust spend.
- 15 per ad was chosen over the brief's maximum of 25. At 25 × 3, ads would outpace campaign Dust (about 1,920
  in total) within a month; see `REVENUE_MODEL_HYPOTHETICAL.md`.
- Wallet API additions:
  - `GrantsWithPrefix`.
  - `MarkSpent`, an atomic multi-key commit.
- The save format is unchanged. The Sprint 02 build was run on the Sprint 04 save without problems during the
  performance runs.

## 6. Reward transaction test results: TESTED

EditMode tests:
- `Dust_PaysOnlyOnRewardEvent_AndOnlyOnce`
- `Dust_DailyCap_ResetsNextLocalDay`
- `Dust_SurvivesRelaunch_AndNeverCrossesProfiles`
- `HintEntitlement_RedeemsOncePerBoard_AndPersists`
- `HintEntitlement_NotSpentOnAlreadyPaidBoard`

PlayMode tests:
- `BonusDust_VoluntaryCard_PaysOnce_AndStopsAtDailyCap`
- `RewardedHint_*` (5 tests)
- `Interstitial_ShowFailure_WithDuplicateCallbacks_AdvancesOnce`

On the emulator with Google test ads:
- The bonus paid +15 once (35 → 50).
- The hint ad gave "Free hint used." with the balance unchanged.

Documented limit: these are client-side bookkeeping checks. Server-side verification (SSV) would need a backend,
which PLOP! does not have.

## 7. Privacy and consent: IMPLEMENTED, TESTED (Android, partly on iOS)

- UMP runs before any request.
- `MobileAds.Initialize` runs only if `CanRequestAds()` is true. Consent starts from the menu after the splash.
- `MaxAdContentRating = G`. Child-directed and under-age tags follow the audience setting.
- Settings ▸ **Privacy Choices** appears only when UMP requires it.
- Android emulator (GDPR region):
  - form shown, "Do not consent" chosen, then non-personalized test ads;
  - Privacy Choices opened the form;
  - consent was remembered on relaunch;
  - offline, the game falls back to the saved consent.
- iOS Simulator: form shown before any request. It was not answered (see 10).
- No ATT, and no analytics SDK.
- Drafts: privacy policy, Google Play Data safety, Apple App Privacy (taken from the SDKs' own manifests).
  **AWAITING PUBLISHER APPROVAL.**

## 8. Audience classification requirements: AWAITING PUBLISHER APPROVAL

- The audience was assessed but not chosen (`AUDIENCE_ASSESSMENT.md`). The engineering view is to treat it as
  **mixed**, with child-directed treatment for everyone and the G rating.
- If the publisher chooses the iOS Kids Category, iOS must ship without ads.
- The Play Console Target Audience form, the Families policy review and the content-rating questionnaire are the
  publisher's to complete.

## 9. Android test-ad results: TESTED (emulator only)

See `DEVICE_QA.md`, runs 1–3, and screenshots in `05_QA/screens/`. Verified:
- consent;
- the bonus card and its reward;
- "No, thanks";
- the rewarded hint;
- Privacy Choices;
- no ad on resume;
- an interstitial after Next Level once 4 new levels were played past level 10;
- the next level opening after the ad closes;
- offline play.

Two bugs were found and fixed (card text overflow, and no advance after the ad) and re-verified.

**Not verified:** a physical Android phone. There is no Android device, battery or thermal data.

An input slip during run 1 claimed the daily gift, played one extra test ad and bought a cosmetic on the
**emulator QA save**. It is recorded in `DEVICE_QA.md`; nothing was deleted.

## 10. iOS status: partly TESTED, device BLOCKED

- Export plus `xcodebuild` Simulator build: **BUILD SUCCEEDED**, with GMA and UMP linked via SPM.
- Launch in Test mode and the consent form: verified.
- Consent answer and test-ad presentation on iOS: **not done**. Driving the Simulator needs the macOS Screen
  Recording permission for the Claude app, which is not granted.
- Device build and archive: **BLOCKED**, because there is no signing team (by rule).
- ATT is not used. Privacy manifests and 50 SKAdNetwork IDs are present.
- The plugin's dependency manager **tried to install CocoaPods by itself**. The attempt failed and nothing was
  installed. Auto-install is now off. See `IOS_STATUS.md`.

## 11. Performance: TESTED (emulator)

- Ads disabled: the same cold start as Sprint 02 (median scene-ready 668 vs 677 ms) and +11 MB PSS.
- Test ads active: +≈95 MB PSS from the SDK and preloaded ads. Scene-ready is unchanged, because the SDK starts
  after the menu.
- Details: `PERFORMANCE.md`.

## 12. Package size impact: TESTED

| | Sprint 02 | Sprint 04 | Change |
|---|---|---|---|
| Release APK | 48,456,870 | 53,747,369 | +5.29 MB (+10.9 %) |
| Release AAB | 48,084,879 | 53,195,382 | +5.11 MB (+10.6 %) |
| Dev APK | 58,868,304 | 65,036,131 | +6.17 MB (+10.5 %) |

## 13. Automated test results: TESTED

| Suite | Result |
|---|---|
| .NET (`DotNet/Plop.Tests`) | **225 / 225** passed (27 Nov, after all changes) |
| Unity EditMode | **225 / 225** passed |
| Unity PlayMode | **181 / 181** passed |

New tests: 22 EditMode and 18 PlayMode in `Sprint04AdsTests.cs`.

## 14. Build output paths: IMPLEMENTED

Root: `/Volumes/PLOP_DEV/Builds/FinalSprint/04_Monetization/` (see its `BUILD_MANIFEST.md`).

| Folder | Content | Configuration |
|---|---|---|
| `01_AdDisabledBuild/` | `PLOP-1.0.0-ads-disabled-release-testsigned.apk` | Release, ads **Disabled**, test-signed (not the production key) |
| `02_TestAdBuild/` | `PLOP-1.0.0-testads-dev.apk` | Development, **Google test ads**. **Never distribute.** |
| `03_Android/` | `PLOP-1.0.0-ads-disabled-release-testsigned.aab`, `manifest/` dumps | Release AAB, ads Disabled, test-signed |
| `04_iOS/` | `Xcode-Simulator-TestAds/` | Xcode project for the Simulator, test ads |
| `05_QA/` | screens, logs, test XML | — |
| `06_Privacy/`, `07_Configuration/`, `08_Documentation/` | copies of the docs | — |

## 15. Production advertising disabled: confirmed, TESTED

- `AdConfiguration.ProductionUnlocked = false`. Production falls back to Disabled even with IDs and an audience
  set. Covered by the `Config_ProductionIsLockedEvenWithIdsAndAudience` test.
- The only app IDs anywhere are Google's sample IDs. No real unit ID exists in the repo.
- The release APK and AAB never initialize the SDK. Covered by `DisabledByDefault_NoAdUiNoRequests`.

## 16. Remaining actions for the publisher (in Sprint 05; none done): AWAITING PUBLISHER APPROVAL

1. Create the AdMob account, accept its terms, register the apps, and create the 6 ad units.
2. Choose the audience. Complete the Target Audience and content-rating forms.
3. Decide whether a no-ads release ships **without the SDK linked** (recommended for a "no data collected"
   label; see the manifest review).
4. Publish the privacy policy at a public URL. Submit Data safety and App Privacy from the drafts.
5. Host `app-ads.txt`.
6. Set the real app IDs and units, register test devices, and change `ProductionUnlocked` in a reviewed commit.
7. Production signing for Android and iOS.

## 17. app-ads.txt readiness: AWAITING PUBLISHER APPROVAL

- A template is ready (`APP_ADS_TXT_READINESS.md`). The publisher ID is a placeholder; no ID was invented.
- Needs a developer website domain, which is unknown, and an AdMob account, which has not been created.

## 18. Store submission blockers: BLOCKED / AWAITING PUBLISHER APPROVAL

- No AdMob account, app IDs or unit IDs.
- Audience not chosen.
- Privacy policy not hosted.
- Data safety and App Privacy not submitted.
- app-ads.txt not hosted.
- No production signing (Android) or Apple team (iOS).
- No physical-device ad QA.
- The ads-disabled build still declares `AD_ID` because the SDK is linked.
- Carried over from earlier sprints:
  - native-speaker review of 7 languages;
  - human listening;
  - final logo, icon and branding choice.

## 19. Git branch and commit

Branch `sprint/04-monetization` from tag `checkpoint/pre-sprint04` (`7a1ad5a`). Handoff commit: COMMIT_HASH.

## 20. Readiness verdict for Sprint 05

**Ready for publisher setup, not ready for live ads.**
- The ad infrastructure is built and was tested with Google test ads on the Android emulator.
- The iOS build links and starts.
- Disabled-by-default and the production lock are in place.
- Sprint 05 can begin publisher-side work: accounts, audience, policy hosting and signing. It must then repeat the
  test-ad QA on physical Android and iOS devices before `ProductionUnlocked` is changed.
