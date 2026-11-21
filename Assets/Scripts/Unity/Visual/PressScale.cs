using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Plop.Presentation
{
    /// <summary>Soft tactile press: the control squishes slightly while held. Event-driven, no Update.</summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float PressedScale = HudTheme.PressedScale;
        private Selectable _selectable;

        private void Awake() => _selectable = GetComponent<Selectable>();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.IsInteractable()) return;
            transform.localScale = new Vector3(PressedScale, PressedScale, 1f);
        }

        public void OnPointerUp(PointerEventData eventData) => transform.localScale = Vector3.one;
        public void OnPointerExit(PointerEventData eventData) => transform.localScale = Vector3.one;
    }
}
