# iOS status (Final Sprint 04)

| Item | Status | Detail |
|---|---|---|
| Xcode export with the SDK | **TESTED** | `PlopAdsBuild.ExportAdsIOSSimulator` → `/Volumes/PLOP_DEV/Builds/FinalSprint/04_Monetization/04_iOS/Xcode-Simulator-TestAds` (test-ad configuration, `PLOP_ADS_TEST`) |
| Dependency delivery | **IMPLEMENTED** | The EDM in Unity 6 adds the Google SDKs as **Swift packages**: `GoogleMobileAds` **13.13.0** and `GoogleUserMessagingPlatform` **3.1.0**, from Google's official `github.com/googleads/swift-package-manager-*` repos. The Podfile has no pods, and **CocoaPods is not needed**. |
| Simulator build | **TESTED** | `xcodebuild -scheme Unity-iPhone -configuration Release -sdk iphonesimulator CODE_SIGNING_ALLOWED=NO` → **BUILD SUCCEEDED**. Log: `05_QA/logs/xcodebuild_sim.log`. Products in `/Volumes/PLOP_DEV/Cache/DerivedData/PLOP-S04` and packages in `/Volumes/PLOP_DEV/Cache/SPM/PLOP-S04` (caches, safe to delete later) |
| Simulator launch | **TESTED** (partly) | Launches in Test mode and shows the UMP consent form before any request (`05_QA/screens/30_ios_sim_launch.png`). Answering the form and showing test ads were **not** done, because the Claude app lacks the macOS Screen Recording permission it needs to tap in the Simulator. |
| Device build / archive | **BLOCKED** | Needs an Apple Developer team and signing. Not created or requested, by rule. |
| ATT | **IMPLEMENTED: not used** | No `NSUserTrackingUsageDescription`, no prompt, no `ATTrackingManager` call. The plugin's ATT text setting is left empty. GMA declares Device ID "used for tracking" in its manifest. Without ATT authorization iOS returns no IDFA. See `STORE_PRIVACY_DECLARATIONS_DRAFT.md` § C for the publisher decision. |
| Privacy manifests | **TESTED** (present) | The built app contains `UnityFramework.framework/PrivacyInfo.xcprivacy`, and the GMA and UMP xcframeworks carry their own. Xcode's privacy report needs an archive (blocked, see above). |
| SKAdNetwork | **TESTED** (present) | The plugin's post-processor wrote **50** `SKAdNetworkItems` into Info.plist, plus `GADApplicationIdentifier` = Google's sample iOS ID `ca-app-pub-3940256099942544~1458002511` |
| `SetiOSAppPauseOnBackground(true)` | **IMPLEMENTED** | Set in `AdMobAdService` before initialization |

## Unapproved CocoaPods install attempt (honest record)

During the Unity export, the External Dependency Manager tried on its own to install CocoaPods with
`gem install cocoapods`. Its iOS Resolver setting `autoPodToolInstallInEditor` was on by default. The install
**failed**, because system Ruby's gem directory (`/Library/Ruby/Gems/2.6.0`) is not writable. No `sudo` was used,
nothing was installed, and `pod` is still absent. The failed run left a gem download cache of about 35 MB in
`~/.gem` (created 27 Nov, 18:05–18:06). Nothing was deleted; the owner can remove that folder.

To stop it happening again, `autoPodToolInstallInEditor` is now **0** in
`ProjectSettings/ExternalDependencyManagerResolverSettings.asset`.

## Next steps (Sprint 05, publisher)

1. In Xcode, select the team and signing for a device build. Use the publisher's own App ID (AdMob) in Google
   Mobile Ads settings for production.
2. Archive, then use Generate Privacy Report and fill in App Privacy from it.
3. Tap through the consent, rewarded and interstitial checks of `DEVICE_QA.md` on an iPhone, with test ads first.
