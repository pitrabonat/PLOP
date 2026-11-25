using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Colour maths for <see cref="BoardDecor"/>: a soft, capped blend so the World always shows through.</summary>
    public static class BoardDecorTint
    {
        public const float MaxAmount = 0.6f;

        public static Color Blend(Color world, Color decor, float amount)
        {
            var c = Color.Lerp(world, decor, Mathf.Clamp(amount, 0f, MaxAmount));
            c.a = 1f;
            return c;
        }
    }
}
