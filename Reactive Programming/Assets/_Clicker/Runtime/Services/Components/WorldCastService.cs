using R3;
using Types;
using Types.Buildings;
using Types.QTE;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Services.Components {
    public class WorldCastService : MonoBehaviour, IService {

        [SerializeField, Tooltip("Camera used to cast mouse clicks into the world.")]
        private Camera _camera;
        [SerializeField, Tooltip("Physics layers that can be clicked as structures.")]
        private LayerMask _clickableLayer;

        private Ray _lastRay;
        private Subject<BuildingState> _structureTypeSubject = new();
        
        private RaycastHit[] _hitBuffer = new RaycastHit[2];
        
        private Subject<QteObject> _qteClickedSubject = new();
        
        public Observable<BuildingState> StructureClicked => _structureTypeSubject;
        public Observable<QteObject> QTEClicked => _qteClickedSubject;
        

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
            .Subscribe(OnClicked)
            .AddTo(this);
        }

        private void OnClicked(GameObject clickedObject) {
            if (clickedObject.TryGetComponent(out Structure structure)) {
                _structureTypeSubject.OnNext(structure.State);
            }
            if (clickedObject.TryGetComponent(out QteObject qteSpawnMarker)) {
                _qteClickedSubject.OnNext(qteSpawnMarker);
            }
        }

        private void OnDrawGizmos() {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(_lastRay.origin, _lastRay.direction * 1000f);
        }
    }
}
