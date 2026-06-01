using Bus;
using R3;
using Types.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Components {
    public class WorldCastService : ServiceBase {

        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _clickableLayer;

        private Ray _lastRay;
        
        private RaycastHit[] _hitBuffer = new RaycastHit[2];

        private void Start() {
            Observable.EveryUpdate()
            .Where(_ => Mouse.current.leftButton.wasPressedThisFrame)
            .Select(_ => Mouse.current.position.ReadValue())
            .Select(screenPosition => _camera.ScreenPointToRay(screenPosition))
            .Select(ray => {
                _lastRay = ray;
                int hitCount = Physics.RaycastNonAlloc(ray, _hitBuffer, 1000f, _clickableLayer);
                return hitCount > 0 ? _hitBuffer[0].collider.gameObject : null;
            })
            .Where(clickedObject => clickedObject != null)
            .Subscribe(clickedObject => EventBus<ClickEvent>.Raise(new ClickEvent(clickedObject)))
            .AddTo(this);
        }

        private void OnDrawGizmos() {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(_lastRay.origin, _lastRay.direction * 1000f);
        }
    }
}
