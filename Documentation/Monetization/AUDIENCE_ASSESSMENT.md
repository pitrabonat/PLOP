# Audience assessment (Final Sprint 04)

Status: **AWAITING PUBLISHER DECISION.** The engineering side does not choose the audience.
`AdConfiguration.Audience = Unconfirmed`, so production advertising stays disabled until the publisher sets it.

## What the product looks like to a store reviewer

| Signal | PLOP! | Effect |
|---|---|---|
| Art style | soft, round, cartoon "sleepy jelly" characters, pastel colours | Appeals to children (Google Play: "may unintentionally appeal to children") |
| Gameplay | swipe-to-slide colour matching, no reading needed, no time pressure | Suitable for all ages, including young children |
| Text and themes | sleep, dreams, cosmetics; no violence, no chat, no user content | Rating-neutral; expected IARC 3+ / PEGI 3 / ESRB E (the publisher completes the questionnaire) |
| Social / accounts | none | no data from accounts |
| Purchases | none (Dream Dust is earned only) | no IAP concerns |

**Honest reading:** PLOP! is very likely to be treated as appealing to children, so a **mixed audience
(children and older users)** is the most realistic store classification. Calling it "teens and adults only" while
it looks like this would risk Families policy enforcement. Whether to target children is a publisher decision with
legal weight (COPPA, GDPR-K / UK Age Appropriate Design Code). This document does not make that decision.

## What each choice means for ads

| Publisher choice | Google Play | Apple App Store | AdMob configuration already built |
|---|---|---|---|
| **General audience (13+ only), not appealing to children** | Standard ads policy. The Target Audience form must not include under-13 groups. Reviewers may still decide the app appeals to children. | Standard ads; ATT applies only if tracking (PLOP! requests no ATT). | `GeneralAudience`: G rating cap, no child tags. |
| **Mixed audience (includes children)** | Families policy: only **Families Self-Certified Ads SDKs** (AdMob qualifies when configured for families); ads served to children must be family-appropriate; child-directed treatment for users who may be children; no AAID / personalised ads for children. Requires an age screen or child treatment for everyone. | Kids Category is optional; outside it, child-directed ads rules still apply under COPPA. | `MixedAudience`: `TagForChildDirectedTreatment = true`, `TagForUnderAgeOfConsent = true`, `AgeRestrictedTreatment.Child`, G rating cap. **No neutral age screen is implemented**: the build treats everyone as a child (safest, lower fill). |
| **Children only (child-directed)** | Families programme; child-directed treatment for every request; consider removing `AD_ID` permission. | **Kids Category: third-party advertising is in practice not permitted** without human-reviewed, contextual-only ads. Strongly consider no ads on iOS. | `ChildDirected`: same tags as mixed for every request. |

The ad-free first 10 levels, the 4-level / 3-minute / 4-per-30-minutes spacing and the G content cap apply in
every case. Rewarded ads are always opt-in.

## Recommendation (engineering view, not a decision)

1. Treat the audience as **mixed** unless the publisher has a documented reason not to. Run the Play Console
   Target Audience and Content questionnaire honestly.
2. If mixed: keep child-directed treatment for **all** users (already built) instead of an age gate, accept the
   lower fill, and keep the content rating cap at **G**.
3. If the iOS version targets the Kids Category: ship iOS **without ads** (Disabled) or remove the SDK from the iOS
   build entirely.
4. Before production: review Google's Families Ads programme requirements and AdMob's "Tag for child-directed
   treatment" guidance on the day of submission (policies change).

## Not done (deliberately)

- No age gate, birthday prompt or parental gate was added; this needs the publisher's audience decision first.
- No store questionnaire was submitted; no audience was declared anywhere.
