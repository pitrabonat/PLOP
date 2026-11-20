using System;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Raises <see cref="Changed"/> when its RectTransform is resized (screen size / safe area changes).
    /// Event-driven alternative to polling Screen.width every frame.</summary>
    public sealed class RectChangeNotifier : MonoBehaviour
    {
        public event Action Changed;
        private void OnRectTransformDimensionsChange() => Changed?.Invoke();
    }
}
