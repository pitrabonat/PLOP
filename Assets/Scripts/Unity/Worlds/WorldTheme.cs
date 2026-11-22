using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Authored world appearance and campaign mapping. Player progress lives in LevelProgress.</summary>
    [CreateAssetMenu(fileName = "World Theme", menuName = "PLOP/World Theme")]
    public sealed class WorldTheme : ScriptableObject
    {
        [SerializeField] private string _id = "";
        [SerializeField] private string _displayName = "";
        [SerializeField, Min(1)] private int _firstLevel = 1;
        [SerializeField, Min(1)] private int _lastLevel = 20;
        [SerializeField] private Color _backgroundColor = new Color(0.98f, 0.91f, 0.85f, 1f);
        [SerializeField] private Color _accentColor = new Color(0.90f, 0.75f, 0.67f, 1f);
        [SerializeField] private Color _trayTint = Color.white;
        [SerializeField] private Color _tileTint = Color.white;
        [SerializeField] private Color _pebbleTint = Color.white;
        [SerializeField] private Color _keyLight = new Color(1f, 0.95f, 0.89f, 1f);
        [SerializeField, Range(0f, 1f)] private float _lightStrength = 0.25f;
        [SerializeField] private WorldAmbience _ambience = WorldAmbience.CozyNest;
        [SerializeField] private Sprite _preview = null;
        [Tooltip("Sprite resource path without extension, relative to a Resources folder.")]
        [SerializeField] private string _backdropResource = "";
        [SerializeField, Range(0f, 0.5f)] private float _revealSeconds = 0.28f;

        public string Id => _id;
        public string DisplayName => _displayName;
        public int FirstLevel => _firstLevel;
        public int LastLevel => _lastLevel;
        public Color BackgroundColor => _backgroundColor;
        public Color AccentColor => _accentColor;
        public Color TrayTint => _trayTint;
        public Color TileTint => _tileTint;
        public Color PebbleTint => _pebbleTint;
        public Color KeyLight => _keyLight;
        public float LightStrength => _lightStrength;
        public WorldAmbience Ambience => _ambience;
        public Sprite Preview => _preview;

        internal float RevealSeconds => _revealSeconds;
        internal bool HasValidRange => _firstLevel >= 1 && _lastLevel >= _firstLevel;
        internal Sprite LoadBackdrop() => string.IsNullOrEmpty(_backdropResource)
            ? null
            : Resources.Load<Sprite>(_backdropResource);

        public bool ContainsLevelIndex(int zeroBasedIndex) =>
            zeroBasedIndex >= _firstLevel - 1 && zeroBasedIndex < _lastLevel;
    }
}
