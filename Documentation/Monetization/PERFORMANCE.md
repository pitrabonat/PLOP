# Performance and size: before and after the ad SDK (Final Sprint 04)

All numbers were measured on 27 Nov 2026. Device: Android Emulator `emulator-5554` (`sdk_gphone64_arm64`,
1080×2400, Apple-silicon host). These are **emulator** numbers. They compare builds against each other and do not
predict a phone. No physical device, battery or thermal measurement was made.

## Package size (bytes, measured on disk)

| Artifact | Sprint 02 (no SDK) | Sprint 04 | Change |
|---|---|---|---|
| Release APK (test-signed) | 48,456,870 | 53,747,369 (ads disabled) | **+5,290,499 (+10.9 %)** |
| Release AAB | 48,084,879 | 53,195,382 (ads disabled) | **+5,110,503 (+10.6 %)** |
| Development APK | 58,868,304 | 65,036,131 (test ads) | +6,167,827 (+10.5 %) |

Where the release APK grows (compressed sizes): Java/Kotlin dex **2.36 → 6.32 MB** (play-services-ads, UMP,
AndroidX WorkManager and friends), `resources.arsc` 0.25 → 0.57 MB, `res/` 0.51 → 0.57 MB, and IL2CPP code and
metadata about +0.3 MB (the C# plugin and `PLOP.Ads.AdMob`). The download from Google Play will be smaller than the
APK (split APKs per ABI and density). That was not measured: bundletool is not installed.

iOS: the Sprint 04 Simulator app (Release) is 236 MB on disk. The only Sprint 02 iOS build is a **Debug** Simulator
build (376 MB), so the two are not comparable, and no iOS size change is claimed.

## Cold start (`am start -W`, 5 runs after 1 warm-up, force-stop between runs, existing save kept)

Release pair, measured while the host was idle (`05_QA/logs/coldstart_quiet.txt`):

| Build | TotalTime median (range) | "game scene ready" median (range) | PSS after start |
|---|---|---|---|
| S02 release | 214 ms (164–280) | 677 ms (661–772) | ≈ 240.5 MB |
| S04 release, ads disabled | 212 ms (134–326) | 668 ms (639–754) | ≈ 251.3 MB |

Development pair (`05_QA/logs/coldstart.txt`):

| Build | TotalTime median | "game scene ready" median | PSS after start |
|---|---|---|---|
| S02 dev | 245 ms | 799 ms | ≈ 284 MB |
| S04 dev, **test ads** (SDK initialized, ads preloaded) | 353 ms | 729 ms | ≈ 380–395 MB |

Reading the results:
- **Ads disabled: no measurable start-up cost.** Start-up times are the same within noise. Memory is about
  +11 MB, from the linked SDK's init provider and classes.
- **Ads enabled: about +95 MB PSS** once the SDK has started and an interstitial and a rewarded ad are preloaded
  (WebView processes). The scene-ready time is unchanged, because consent and the SDK start only after the menu is
  shown. `TotalTime` rose by about 100 ms; this was not investigated further.
- A first release run made while `xcodebuild` was compiling on the same Mac was much noisier (up to 2.6 s). It is
  kept in `coldstart.txt` for transparency, but superseded by the idle-host run.

## Frame pacing

No frame-time capture was made in Sprint 04. Ads are full-screen, with no banner, and are shown only between levels,
so gameplay frames run with no ad view on screen. The Phase 11.5 frame-pacing numbers in `Documentation/TESTING.md`
remain the latest gameplay measurement.
