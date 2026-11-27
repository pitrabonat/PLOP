# Hypothetical revenue model (illustration only)

> **Not a forecast and not real data.** PLOP! has no live ads, no AdMob account, no store listing and no players.
> Every number below is an **assumption** chosen to show how the frequency policy limits ad volume. eCPM values
> vary by country, season, audience treatment and fill. Child-directed treatment (likely for this game; see
> AUDIENCE_ASSESSMENT.md) means non-personalized ads, which usually earn much less.

## How many ads can a player see? (derived from the policy, not assumed)

- Levels 1–10: **0** interstitials.
- After that, at most one interstitial per **4 new levels and 180 s of active play**, capped at **4 per 30 min**.
  Replays never trigger one.
- Rewarded: only when the player chooses. Bonus Dust is capped at **3 per day**. Hint ads only when a hint would
  cost Dust.

Upper bound for one session of 30 active minutes past level 10, at about 45 s per level (≈40 new levels): spacing
allows ≤ 10 by levels and ≤ 10 by time, and the rolling cap reduces that to **≤ 4**. A typical relaxed session
(10–15 new levels) gives **2–3**.

## Illustrative scenarios (all inputs are assumptions)

| Input (assumed) | Low | Mid | High |
|---|---|---|---|
| Daily active players past level 10 | 1,000 | 1,000 | 1,000 |
| Interstitials per DAU per day | 0.8 | 1.5 | 2.5 |
| Rewarded views per DAU per day | 0.2 | 0.5 | 1.0 |
| Fill rate | 60 % | 80 % | 90 % |
| Interstitial eCPM (USD) | 1.00 | 3.00 | 6.00 |
| Rewarded eCPM (USD) | 3.00 | 8.00 | 15.00 |

Revenue/day = DAU × (interstitials × fill × eCPM_i + rewarded × fill × eCPM_r) / 1000

| | Low | Mid | High |
|---|---|---|---|
| Interstitial | $0.48 | $3.60 | $13.50 |
| Rewarded | $0.36 | $3.20 | $13.50 |
| **Total per 1,000 DAU per day** | **≈ $0.84** | **≈ $6.80** | **≈ $27.00** |

The "Low" column is closest to a child-directed (non-personalized) configuration. These figures exist only to
size the decision, and must not be quoted as expected revenue.

## Economy check (why 15 Dust and 3 per day)

- Campaign Dust from completions and Perfects: ≈ 1,920 in total. Cosmetics cost 2,750 in total.
- Ads at 15 × 3 = 45/day reach the cosmetic total in about 61 days without playing. At the brief's maximum
  (25 × 3 = 75/day), it would take about 37 days. 15 keeps play the main source and ads a small top-up.
