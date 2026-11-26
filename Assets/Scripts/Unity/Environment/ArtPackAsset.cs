using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Phase 11.6 custom art pack: references to the hand-delivered sprites in Assets/Art/Phase11_6 and the
    /// art-driven Lava / Frost materials. Created by PLOP → Art Pack → Import Phase 11.6 Sprites; loaded at
    /// runtime through <see cref="ArtPack"/>. The optional Plop body candidates are deliberately not referenced
    /// here, so they are not shipped (the Character Studio rig keeps drawing the Plops).
    /// </summary>
    [CreateAssetMenu(menuName = "PLOP/Art Pack", fileName = "Phase11_6")]
    public sealed class ArtPackAsset : ScriptableObject
    {
        public const string Resource = "ArtPack/Phase11_6";

        [Header("Tiles")]
        public Sprite floor;
        public Sprite pebble;
        public Sprite lava;
        public Sprite frost;

        [Header("Mechanics")]
        public Sprite fanHousing;
        public Sprite fanBlades;
        public Sprite portalFrame;
        public Sprite portalVortex;

        [Header("Nests (Berry, Mint, Honey, Lilac)")]
        public Sprite[] nests = new Sprite[0];

        [Header("Materials")]
        public Material lavaMaterial;
        public Material frostMaterial;
    }
}
