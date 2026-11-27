# Test-ad QA on the Android emulator (Sprint 04)

Device: Android Emulator `emulator-5554` (`sdk_gphone64_arm64`, 1080×2400, Google Play services), the
"Medium_Phone_API_37.0" AVD. No physical device was used. Screenshots are in
`/Volumes/PLOP_DEV/Builds/FinalSprint/04_Monetization/05_QA/screens/`, and the full logcat is in `05_QA/logs/`.

Build under test: `02_TestAdBuild/PLOP-1.0.0-testads-dev.apk` (`PLOP_ADS_TEST`, Google test units only). It was
installed with `adb install -r` over the existing install, which keeps the existing save; no data was cleared.
Nobody tapped any ad content ("Install" buttons were never touched). Ads were closed only with their X button.

## Run 1: first test-ad build (27 Nov, 16:00–16:16)

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Mode on launch | `[PLOP][Ads] requested Test, running Test` | logcat |
| 2 | Consent before any ad request | UMP form shown on the menu ("Publisher Test Ads", because the sample app ID is used). The emulator's region makes GDPR apply (`IABTCF_gdprApplies=1`). No `Ads` request lines appear before the choice. | `01_consent_form.png` |
| 3 | Choice "Do not consent", then SDK start | `canRequestAds=True` (non-personalized/limited ads remain allowed). The SDK logs "This request is sent from a test device." | logcat |
| 4 | Bonus Dust entry shows only once an ad is loaded | "Watch Ad · +15 Dream Dust" appeared after the load | `02_menu_after_consent.png` |
| 5 | Voluntary card | The card states "15 Dream Dust … up to 3 bonuses a day", with "Watch Ad" and "No, thanks" at the same size and colour. **Bug:** the message ran past the card edges, so the text was not wrapping. **Fixed** (wrap enabled); the fix is shown in run 2. | `03_bonus_card.png` |
| 6 | Rewarded test ad shown, watched to the end | Labelled "Test Ad", "Reward granted" | `04_…`, `05_…` |
| 7 | Reward paid once after close | Balance 35 → **50** (+15); the next ad preloaded | `06_after_reward.png` |
| 8 | Hint offer (level 11, hint costs 15) | "Get a hint" card: "Use 15 Dream Dust", "Ad · Watch Ad · Free Hint", "No, thanks". Same message overflow as #5 (fixed). | `07_hint_offer.png` |
| 9 | Rewarded hint | Test ad → reward → close → "Try swiping left. Free hint used." Balance unchanged. | `08_hint_after_ad.png` |
| 10 | Settings ▸ Privacy Choices | Visible (consent required in this region); opens Google's privacy options form | `09_…`, `10_…` |
| 11 | Background (Home) → resume | No ad shown on resume; the screen was restored as it was | `11_after_resume.png` |
| 12 | Interstitial at the natural break | Played levels 11–14 with real swipes (adb), one move about every 11 s (≈ 6 min of active play, 4 new completions). After level 14's card, **Next Level → "This is an interstitial test ad."** | `12_…`, `13_interstitial_test_ad.png` |
| 13 | Continue after the interstitial closes | **Bug:** the level stayed on 14's completion card. Cause: the SDK reported the close before Unity regained focus, so `TrySelectLevel` refused while "suspended". **Fixed:** the chosen level opens on resume, exactly once. A PlayMode regression test was added (`Interstitial_ClosedBeforeTheAppResumes_OpensTheNextLevelOnResume`). | `14_after_interstitial_level15.png` (shows the bug) |
| 14 | No ad in levels 1–10, during a level, or on completion | No interstitial appeared before #12. Levels 11–13 completed without an ad (new-player protection and spacing). | logcat, play-through |

### Incident during run 1 (honest record)

While automating swipes right after a background/resume step, input landed on the main menu instead of the board.
On the emulator's existing test save it:
- claimed that day's **Daily Gift** (+10);
- opened the bonus card and played a **second rewarded test ad**, which paid +15 (`addust` grant);
- **bought "Bouncy" (body style, 60 Dust)** and equipped Bouncy and the free Bow (log: `Look bouncy/…/bow/…`).

The wallet record confirms it: 50 + 10 + 15 − 60 = 15. No other spend was recorded. Nothing was deleted, the
progress level did not change, and Settings were unchanged (verified). This is the emulator QA profile, not a
person's device, but it is recorded here because it changed that save. The purchase cannot be undone in-game. From
then on, every step was checked with a screenshot before input was sent. The menu navigation could not be
reproduced: closing Settings from inside a level stays in the level.

The log also shows `FileNotFoundException: GoogleMobileAdsNative.Common` in the plugin's internal telemetry
(`RCS JSPB payload`) during `onInitializationComplete`. The plugin catches it, and initialization and ads worked.
The plugin tries to load an optional native-ads assembly that PLOP! does not include, so this is not a PLOP!
error.

## Run 2: rebuilt test-ad build (after the fixes, 27 Nov, 17:58–18:05)

The APK was rebuilt with the text-wrap and post-interstitial fixes and installed with `adb install -r` (save kept).
Every input was checked against a fresh screenshot before it was sent.

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Consent remembered | No form on the second launch; ads requested right after the menu opened | logcat |
| 2 | Bonus card text | The message now wraps inside the card; "Watch Ad" and "No, thanks" are equal | `20_run2_bonus_card_wrapped.png` |
| 3 | "No, thanks" | Closes the card; no ad, no reward, balance unchanged | screenshot check |
| 4 | Spacing and interstitial | Levels 15–18 played with real swipes (4 new completions logged, each `[PLOP] Completed …`). Next Level on level 18's card → interstitial test ad. | `21_run2_interstitial.png` |
| 5 | **Continue after close (fix for run 1 #13)** | Closed with X → **level 19 opened** (`18:04:18 Completed PLOP-EC8D7A6CB346`, `18:04:39 Loaded PLOP-32B6D19B7F92`). Exactly one level load. | `22_run2_after_interstitial.png` |
| 6 | Startup | "game scene ready" 729 ms (run 1: 1131 ms; emulator timings are noisy, not a performance result) | logcat |

Both run 1 bugs are fixed and confirmed on the emulator. Not covered: a physical Android device, an iOS device or
simulator tap-through (macOS Screen Recording permission not granted, see `IOS_STATUS.md`), real (non-test) ads, and slow (rather than absent)
networks.

## Run 3: offline (27 Nov, 18:31)

Emulator airplane mode on (`cmd connectivity airplane-mode enable`), test-ad build, cold launch, no other change.

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Launch offline | Menu as usual, scene ready 721 ms. `Consent update failed: Error making request.`, then the saved consent is used (`canRequestAds=True`). No error dialog. | `23_offline_menu.png`, logcat |
| 2 | Bonus entry | Shows a disabled "Ad unavailable" pill; nothing to tap into | `23_offline_menu.png` |
| 3 | Play | Play ▸ level 19 opened at once | `24_offline_level.png` |

Airplane mode was switched off again afterwards (`airplane_mode_on` = 0). The offline hint path (no ad ready, so
the existing Dust flow) is covered by the PlayMode tests and was not tapped here, so that no Dust was spent on the
QA save.

## iOS Simulator (27 Nov, 18:22)

Build `04_iOS/Xcode-Simulator-TestAds`, compiled with `xcodebuild` (Release, iphonesimulator, unsigned) and
installed over the existing PLOP! app on the "PLOP iPhone 15 Pro Max" simulator (iOS 26.3; data kept).

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Launch and mode | `[PLOP][Ads] requested Test, running Test`, scene ready 283 ms | `logs/ios_sim_run.log` |
| 2 | Consent before requests | UMP form "Publisher Test Ads" shown over the menu | `30_ios_sim_launch.png` |
| 3 | Answer consent, show test ads | **Not done.** Driving the Simulator needs the macOS Screen Recording permission for the Claude app, which is not granted, and the Simulator tool needs a system-wide `xcode-select`, which was not changed. | — |

The app was terminated afterwards. iOS ad presentation, reward and audio restore are covered on iOS only by the
shared C# code paths and the Unity tests, not by a run on iOS.
