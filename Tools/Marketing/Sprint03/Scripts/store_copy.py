#!/usr/bin/env python3
"""Writes the store listings, localised screenshot captions, video caption files and launch-announcement drafts,
and checks every field against the store limits. Source text: store_text.py, l10n_captions.json, copy.json."""
import sys, os, json, shutil, glob
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from store_text import FEATURES, GP_TAGS, ANNOUNCE

ROOT = "/Volumes/PLOP_DEV/Marketing/FinalSprint03"
S = os.path.dirname(os.path.abspath(__file__))
LIMITS_GP = {"title": 30, "short": 80, "body": 4000, "notes": 500}
LIMITS_AS = {"title": 30, "subtitle": 30, "promo": 170, "body": 4000, "notes": 4000}
REVIEW = "AI-drafted. NATIVE-SPEAKER REVIEW REQUIRED before upload."
NAMES = {"en": "English", "de": "German", "fr": "French", "es": "Spanish", "it": "Italian", "pt-BR": "Portuguese (Brazil)", "ru": "Russian", "uk": "Ukrainian"}
PLAY_CODES = {"en": "en-US", "de": "de-DE", "fr": "fr-FR", "es": "es-ES", "it": "it-IT", "pt-BR": "pt-BR", "ru": "ru-RU", "uk": "uk"}
APPLE_CODES = {"en": "en-US", "de": "de-DE", "fr": "fr-FR", "es": "es-ES", "it": "it", "pt-BR": "pt-BR", "ru": "ru", "uk": "uk"}

problems = []
def check(store, lang, field, text, limit, unit="chars"):
    n = len(text.encode("utf-8")) if unit == "bytes" else len(text)
    if n > limit: problems.append(f"{store} {lang} {field}: {n} {unit} > {limit}")
    return n

# 1. merge caption translations into copy.json (keeps English as edited there)
cp = json.load(open(f"{S}/copy.json")); tr = json.load(open(f"{S}/l10n_captions.json"))
for lang, d in tr.items():
    if lang.startswith("_"): continue
    missing = [k for k in cp["en"] if k not in d]
    if missing: problems.append(f"captions {lang} missing {missing}")
    cp[lang] = d
json.dump(cp, open(f"{S}/copy.json", "w"), ensure_ascii=False, indent=1)

report = ["| Lang | GP title | GP short | GP full | AS name | AS subtitle | AS promo | AS keywords (bytes) | AS description |", "|---|---:|---:|---:|---:|---:|---:|---:|---:|"]
for lang, f in FEATURES.items():
    d = f"{ROOT}/08_Localization/StoreDescriptions/{lang}"; os.makedirs(d, exist_ok=True)
    flag = "" if lang == "en" else f"> **{REVIEW}**\n\n"
    gp = [check("GP", lang, "title", f["title"], 30), check("GP", lang, "short", f["short"], 80), check("GP", lang, "full", f["body"], 4000), check("GP", lang, "notes", f["notes"], 500)]
    ks = f["keywords"]
    if " ," in ks or ", " in ks: problems.append(f"AS {lang} keywords contain spaces after commas")
    asn = [check("AS", lang, "name", f["title"], 30), check("AS", lang, "subtitle", f["subtitle"], 30), check("AS", lang, "promo", f["promo"], 170),
           check("AS", lang, "keywords", ks, 100, "bytes"), check("AS", lang, "description", f["body"], 4000), check("AS", lang, "whatsnew", f["notes"], 4000)]
    report.append(f"| {lang} | {gp[0]}/30 | {gp[1]}/80 | {gp[2]}/4000 | {asn[0]}/30 | {asn[1]}/30 | {asn[2]}/170 | {asn[3]}/100 | {asn[4]}/4000 |")
    open(f"{d}/GooglePlay_{lang}.md", "w").write(f"""# PLOP! — Google Play listing · {NAMES[lang]} ({PLAY_CODES[lang]})

Status: {'DRAFT — AWAITING OWNER APPROVAL' if lang == 'en' else 'DRAFT — ' + REVIEW}

{flag}## App name ({gp[0]}/30)
{f['title']}

## Short description ({gp[1]}/80)
{f['short']}

## Full description ({gp[2]}/4000)
{f['body']}

## Release notes / What's new ({gp[3]}/500)
{f['notes']}
""" + (f"\n## Suggested category and tags\nCategory: Game › Puzzle. Tags to pick in Play Console (choose from Google's list): {', '.join(GP_TAGS)}.\n" if lang == "en" else ""))
    open(f"{d}/AppStore_{lang}.md", "w").write(f"""# PLOP! — App Store listing · {NAMES[lang]} ({APPLE_CODES[lang]})

Status: {'DRAFT — AWAITING OWNER APPROVAL' if lang == 'en' else 'DRAFT — ' + REVIEW}

{flag}## Name ({asn[0]}/30)
{f['title']}

## Subtitle ({asn[1]}/30)
{f['subtitle']}

## Promotional text ({asn[2]}/170)
{f['promo']}

## Keywords ({asn[3]}/100 bytes)
{ks}

## Description ({asn[4]}/4000)
{f['body']}

## What's New ({asn[5]}/4000)
{f['notes']}
""")
    json.dump({"googlePlay": {"title": f["title"], "shortDescription": f["short"], "fullDescription": f["body"], "releaseNotes": f["notes"]},
               "appStore": {"name": f["title"], "subtitle": f["subtitle"], "promotionalText": f["promo"], "keywords": ks, "description": f["body"], "whatsNew": f["notes"]},
               "status": "DRAFT", "review": "owner" if lang == "en" else "native speaker + owner"}, open(f"{d}/listing_{lang}.json", "w"), ensure_ascii=False, indent=1)

# English masters also beside the store assets
shutil.copyfile(f"{ROOT}/08_Localization/StoreDescriptions/en/GooglePlay_en.md", f"{ROOT}/06_StoreAssets/GooglePlay/Listing_GooglePlay_en.md")
shutil.copyfile(f"{ROOT}/08_Localization/StoreDescriptions/en/AppStore_en.md", f"{ROOT}/06_StoreAssets/AppStore/Listing_AppStore_en.md")

# 2. screenshot captions per language
HEAD = ["little_big", "one_swipe", "think_slide", "challenges", "worlds", "own", "daily", "sweet_dreams"]
d = f"{ROOT}/08_Localization/ScreenshotCaptions"; os.makedirs(d, exist_ok=True)
for lang in cp:
    if lang.startswith("_"): continue
    caps = {f"{i:02d}_{k}": cp[lang][k] for i, k in enumerate(HEAD, 1)}
    json.dump({"status": "FINAL (en)" if lang == "en" else "DRAFT — " + REVIEW, "captions": caps}, open(f"{d}/screenshot_captions_{lang}.json", "w"), ensure_ascii=False, indent=1)
open(f"{d}/screenshot_captions_all.tsv", "w").write("key\t" + "\t".join(l for l in cp if not l.startswith("_")) + "\n" +
    "".join(f"{i:02d}_{k}\t" + "\t".join(cp[l][k].replace("\n", " / ") for l in cp if not l.startswith("_")) + "\n" for i, k in enumerate(HEAD, 1)))

# 3. launch announcements (drafts — nothing is posted)
d = f"{ROOT}/07_SocialMedia/Captions"; os.makedirs(d, exist_ok=True)
lines = ["# PLOP! — launch announcement drafts", "", "Status: **DRAFT — AWAITING OWNER APPROVAL. Nothing has been posted.** Fill the `{placeholders}` at launch. Non-English versions: " + REVIEW, ""]
for lang, a in ANNOUNCE.items():
    lines += [f"## {NAMES[lang]}", ""]
    for k, v in a.items(): lines += [f"**{k}**", "", v, ""]
open(f"{d}/Launch_Announcements.md", "w").write("\n".join(lines))
print("\n".join(report)); print("problems:", problems or "none")
json.dump({"limits_report": report, "problems": problems}, open(f"{ROOT}/11_QA/store_copy_limits.json", "w"), ensure_ascii=False, indent=1)
