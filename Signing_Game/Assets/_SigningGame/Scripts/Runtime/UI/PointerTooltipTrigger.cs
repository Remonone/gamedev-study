using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI {
    public sealed class PointerTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        private Action<Vector2, Camera> _show;
        private Action _hide;

        public void Bind(Action<Vector2, Camera> show, Action hide) {
            Unbind();
            _show = show ?? throw new ArgumentNullException(nameof(show));
            _hide = hide ?? throw new ArgumentNullException(nameof(hide));
        }

        public void Unbind() {
            _hide?.Invoke();
            _show = null;
            _hide = null;
        }

        public void OnPointerEnter(PointerEventData eventData) {
            _show?.Invoke(eventData.position, eventData.enterEventCamera);
        }

        public void OnPointerExit(PointerEventData eventData) {
            _hide?.Invoke();
        }

        private void OnDisable() {
            _hide?.Invoke();
        }

        private void OnDestroy() {
            Unbind();
        }
    }
}
