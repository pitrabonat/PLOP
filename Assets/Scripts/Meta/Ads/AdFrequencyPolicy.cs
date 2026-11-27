using System;
using System.Collections.Generic;
using Plop.Core.Json;

namespace Plop.Meta.Ads
{
    /// <summary>
    /// Conservative interstitial frequency policy (Sprint 04). Every condition must hold; none is an alternative:
    /// past the ad-free opening levels, enough newly completed levels and active foreground play since the last
    /// interstitial, under the rolling impression cap, past the new-player protection, not a tutorial, not right after
    /// a rewarded ad, and only after a first-time completion. Counts real presentations, never failed requests.
    /// State is local (no identifiers) and persists across launches; nothing ever resets a counter to show more ads.
    /// </summary>
    public sealed class AdFrequencyPolicy
    {
        public const string StoreKey = "plop.ads.frequency.v1";
        private readonly IKeyValueStore _store;
        private readonly AdConfiguration _config;
        private readonly List<long> _impressions = new List<long>(); // UTC unix seconds of shown interstitials
        private bool _dirty;

        /// <summary>Total active foreground gameplay since ads were introduced (new-player protection).</summary>
        public double TotalActiveSeconds { get; private set; }
        public double ActiveSecondsSinceInterstitial { get; private set; }
        public int CompletionsSinceInterstitial { get; private set; }
        public int CompletionsSinceRewarded { get; private set; }
        /// <summary>UTC unix seconds of the last rewarded ad presentation (0 = never).</summary>
        public long LastRewardedAt { get; private set; }
        public int InterstitialsShown { get; private set; }
        public IReadOnlyList<long> Impressions => _impressions;

        public AdFrequencyPolicy(IKeyValueStore store, AdConfiguration config)
        {
            _store = store;
            _config = config;
            // A player without a record (new, or updating from a build without ads) starts like right after an ad:
            // no completions or play time count yet, and the new-player protection applies from now.
            CompletionsSinceRewarded = int.MaxValue / 2;
            Load();
        }

        /// <summary>Adds foreground gameplay time (the caller passes only time spent on an active board).</summary>
        public void AddActiveSeconds(double seconds)
        {
            if (!(seconds > 0)) return;
            seconds = Math.Min(seconds, 1.0); // a stalled frame or a resumed clock never counts as minutes of play
            TotalActiveSeconds += seconds;
            ActiveSecondsSinceInterstitial += seconds;
            _dirty = true;
        }

        /// <summary>A campaign level was completed for the first time. Replays, restarts and Undo never call this.</summary>
        public void RecordNewCompletion()
        {
            CompletionsSinceInterstitial++;
            if (CompletionsSinceRewarded < int.MaxValue / 2) CompletionsSinceRewarded++;
            _dirty = true;
            Save();
        }

        public AdEligibility Evaluate(InterstitialContext context, DateTime utcNow)
        {
            if (!_config.InterstitialsEnabled) return AdEligibility.No("interstitials disabled");
            if (context.CompletedLevelNumber <= _config.AdFreeLevels) return AdEligibility.No("ad-free opening levels");
            if (!context.NewlyCompleted) return AdEligibility.No("replayed level");
            if (context.Tutorial) return AdEligibility.No("tutorial");
            if (TotalActiveSeconds < _config.NewPlayerProtectionSeconds) return AdEligibility.No("new-player protection");
            if (CompletionsSinceInterstitial < _config.LevelsBetweenInterstitials) return AdEligibility.No("too few new levels");
            if (ActiveSecondsSinceInterstitial < _config.MinActiveSecondsBetween) return AdEligibility.No("too little play time");
            if (ImpressionsInWindow(utcNow) >= _config.MaxInterstitialsPerWindow) return AdEligibility.No("rolling cap reached");
            long now = ToUnix(utcNow);
            if (LastRewardedAt != 0 && (CompletionsSinceRewarded < 1 ||
                                         Math.Abs(now - LastRewardedAt) < _config.RewardedCooldownSeconds))
                return AdEligibility.No("just after a rewarded ad");
            return AdEligibility.Yes;
        }

        /// <summary>
        /// Interstitials shown inside the rolling window. Entries stamped in the future (the device clock went back)
        /// still count until they are more than a day ahead, so changing the clock can only reduce ads.
        /// </summary>
        public int ImpressionsInWindow(DateTime utcNow)
        {
            long now = ToUnix(utcNow);
            long window = (long)(_config.WindowMinutes * 60);
            int n = 0;
            foreach (var t in _impressions)
                if (now - t < window && t - now < 86400) n++;
            return n;
        }

        /// <summary>The SDK reported the interstitial on screen. Failed loads and failed shows are never recorded.</summary>
        public void RecordInterstitialShown(DateTime utcNow)
        {
            long now = ToUnix(utcNow);
            _impressions.Add(now);
            Prune(now);
            InterstitialsShown++;
            CompletionsSinceInterstitial = 0;
            ActiveSecondsSinceInterstitial = 0;
            _dirty = true;
            Save();
        }

        /// <summary>A rewarded ad was presented (whether or not it was watched to the end).</summary>
        public void RecordRewardedShown(DateTime utcNow)
        {
            LastRewardedAt = ToUnix(utcNow);
            CompletionsSinceRewarded = 0;
            _dirty = true;
            Save();
        }

        public void Save()
        {
            if (!_dirty) return;
            var w = new JsonWriter(false);
            w.BeginObject();
            w.Key("version").Value(1);
            w.Key("active").Value(Math.Round(TotalActiveSeconds, 1));
            w.Key("sinceAd").Value(Math.Round(ActiveSecondsSinceInterstitial, 1));
            w.Key("levels").Value(CompletionsSinceInterstitial);
            w.Key("afterRewarded").Value(Math.Min(CompletionsSinceRewarded, int.MaxValue / 2));
            w.Key("rewardedAt").Value(LastRewardedAt);
            w.Key("shown").Value(InterstitialsShown);
            w.Key("impressions").BeginArray();
            foreach (var t in _impressions) w.Value(t);
            w.EndArray();
            w.EndObject();
            _store.SetString(StoreKey, w.ToString());
            _store.Save();
            _dirty = false;
        }

        private void Load()
        {
            string json = _store.GetString(StoreKey, "");
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var o = MiniJson.AsObject(MiniJson.Parse(json), "ads");
                TotalActiveSeconds = Math.Max(0, Number(o, "active"));
                ActiveSecondsSinceInterstitial = Math.Max(0, Number(o, "sinceAd"));
                CompletionsSinceInterstitial = Math.Max(0, MiniJson.GetInt(o, "levels", 0));
                CompletionsSinceRewarded = Math.Max(0, MiniJson.GetInt(o, "afterRewarded", int.MaxValue / 2));
                LastRewardedAt = (long)Math.Max(0, Number(o, "rewardedAt"));
                InterstitialsShown = Math.Max(0, MiniJson.GetInt(o, "shown", 0));
                if (o.TryGetValue("impressions", out var list) && list is List<object> items)
                    foreach (var item in items) if (item is double d) _impressions.Add((long)d); else if (item is long l) _impressions.Add(l);
            }
            catch (FormatException)
            {
                // A damaged record must never make ads more frequent: treat it as a fresh start right after an ad
                // (new-player protection, level spacing and play time all apply again).
                TotalActiveSeconds = 0;
                ActiveSecondsSinceInterstitial = 0;
                CompletionsSinceInterstitial = 0;
                _impressions.Clear();
                _dirty = true;
                Save();
            }
        }

        private static double Number(Dictionary<string, object> o, string key)
        {
            if (!o.TryGetValue(key, out var v) || v == null) return 0;
            if (v is double d) return double.IsNaN(d) || double.IsInfinity(d) ? 0 : d;
            if (v is long l) return l;
            if (v is int i) return i;
            return 0;
        }

        private void Prune(long now)
        {
            long window = (long)(_config.WindowMinutes * 60);
            _impressions.RemoveAll(t => now - t >= window || t - now >= 86400);
        }

        private static long ToUnix(DateTime utc) =>
            (long)Math.Floor((utc.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
    }
}
