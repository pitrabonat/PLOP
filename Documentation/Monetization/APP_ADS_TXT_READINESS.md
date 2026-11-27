# app-ads.txt readiness

Status: **AWAITING PUBLISHER.** It cannot be completed without the publisher's AdMob account. **No ID was invented.**

## What it is

`app-ads.txt` is a public text file at the root of the **developer website** listed in the Google Play and App Store
listings (for example `https://[DEVELOPER-DOMAIN]/app-ads.txt`). It names the ad systems allowed to sell the app's
inventory. AdMob limits or blocks serving while it is missing or does not match.

## The line AdMob will ask for

AdMob shows the exact line in **AdMob ▸ Apps ▸ View all apps ▸ app-ads.txt**. Its form is:

```
google.com, pub-[YOUR 16-DIGIT PUBLISHER ID], DIRECT, f08c47fec0942fa0
```

- `pub-…` is the publisher ID of the **publisher's own** AdMob account. It is not in this project, and nobody
  should copy one from elsewhere.
- `f08c47fec0942fa0` is Google's public TAG certification ID and is the same for every AdMob publisher.
- `DIRECT` means the publisher's own account. Add one line per extra ad network only if mediation is added later
  (none is in Sprint 04).

## Checklist

| Step | Owner | Status |
|---|---|---|
| Developer website domain exists and serves HTTPS | publisher | unknown |
| Same domain entered as "Website" in Play Console and App Store Connect | publisher | not done (no store listing was edited) |
| AdMob account created; app registered and linked to its store listing | publisher | **not done** (no account was created in Sprint 04) |
| `app-ads.txt` uploaded at the domain root (not in a subfolder; plain text; HTTP 200) | publisher | not done |
| AdMob shows "app-ads.txt verified" (can take up to 24 h after the store listing is live) | publisher | not done |

## Template file

Template only; it must not be uploaded until the placeholder is replaced:

```
# app-ads.txt for PLOP! — Sleepy Puzzle
google.com, pub-REPLACE_WITH_PUBLISHER_ID, DIRECT, f08c47fec0942fa0
```
