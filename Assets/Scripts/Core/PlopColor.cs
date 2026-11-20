namespace Plop.Core
{
    /// <summary>Plop / Nest color. A Plop sleeps only on a Nest of the same color.</summary>
    public enum PlopColor : byte
    {
        Red = 0,
        Blue = 1,
        Yellow = 2,
        Green = 3,
        Purple = 4,
        Orange = 5
    }

    public static class PlopColorUtil
    {
        public const int Count = 6;
    }
}
