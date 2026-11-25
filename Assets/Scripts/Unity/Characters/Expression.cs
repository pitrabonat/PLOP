namespace Plop.Presentation
{
    /// <summary>Facial expressions built from a few eye, mouth and brow shapes.</summary>
    public enum Expression
    {
        Neutral, Happy, Curious, Surprised, Worried, Sleepy, Sleeping, Excited, Confused, Collision
    }

    public enum EyeShape { Open, Wide, Half, Happy, Closed, SqueezeL, SqueezeR }
    public enum MouthShape { Neutral, Grin, Open, Small, Flat, Wavy, Frown, Sleep }
    public enum BrowPose { None, Raised, Worried, LeftUp, RightUp }

    /// <summary>One expression's feature choice. Kept as data so new faces never need new animation code.</summary>
    public readonly struct FaceFeatures
    {
        public readonly EyeShape Left, Right;
        public readonly MouthShape Mouth;
        public readonly BrowPose Brows;
        public readonly float Cheeks;   // multiplier on the face style's blush

        public FaceFeatures(EyeShape left, EyeShape right, MouthShape mouth, BrowPose brows = BrowPose.None, float cheeks = 1f)
        {
            Left = left; Right = right; Mouth = mouth; Brows = brows; Cheeks = cheeks;
        }

        public bool Blinks => Left == EyeShape.Open || Left == EyeShape.Wide || Left == EyeShape.Half;

        public static FaceFeatures For(Expression e)
        {
            switch (e)
            {
                case Expression.Happy: return new FaceFeatures(EyeShape.Happy, EyeShape.Happy, MouthShape.Grin, BrowPose.None, 1.35f);
                case Expression.Curious: return new FaceFeatures(EyeShape.Open, EyeShape.Wide, MouthShape.Small, BrowPose.RightUp);
                case Expression.Surprised: return new FaceFeatures(EyeShape.Wide, EyeShape.Wide, MouthShape.Open, BrowPose.Raised, 0.8f);
                case Expression.Worried: return new FaceFeatures(EyeShape.Open, EyeShape.Open, MouthShape.Frown, BrowPose.Worried, 0.7f);
                case Expression.Sleepy: return new FaceFeatures(EyeShape.Half, EyeShape.Half, MouthShape.Flat, BrowPose.None, 1.1f);
                case Expression.Sleeping: return new FaceFeatures(EyeShape.Closed, EyeShape.Closed, MouthShape.Sleep, BrowPose.None, 1.15f);
                case Expression.Excited: return new FaceFeatures(EyeShape.Wide, EyeShape.Wide, MouthShape.Grin, BrowPose.Raised, 1.45f);
                case Expression.Confused: return new FaceFeatures(EyeShape.Open, EyeShape.Half, MouthShape.Wavy, BrowPose.LeftUp, 0.9f);
                case Expression.Collision: return new FaceFeatures(EyeShape.SqueezeL, EyeShape.SqueezeR, MouthShape.Open, BrowPose.None, 1.2f);
                default: return new FaceFeatures(EyeShape.Open, EyeShape.Open, MouthShape.Neutral);
            }
        }
    }

    /// <summary>Studio tap reactions. Purely visual, rate-limited by the caller.</summary>
    public enum Reaction { Bounce, Surprised, Wobble, HappyBlink, Squash, Sleepy }
}
