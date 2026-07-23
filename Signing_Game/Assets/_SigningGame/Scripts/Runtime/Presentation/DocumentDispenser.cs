using System;
using Services;
using Services.Locator;
using UnityEngine;

namespace Presentation {
    public sealed class DocumentDispenser : MonoBehaviour, IService {
        [SerializeField] private DocumentView _documentPrefab;
        [SerializeField] private RectTransform _parent;
        [SerializeField] private Vector2 _anchoredSpawnPosition;

        [ContextMenu("Spawn")]
        public DocumentView Spawn() {
            if (_documentPrefab == null) {
                throw new InvalidOperationException("DocumentDispenser requires a document prefab.");
            }
            if (_parent == null) {
                throw new InvalidOperationException("DocumentDispenser requires a parent RectTransform.");
            }
            DocumentView document = Instantiate(_documentPrefab, _parent, false);
            ((RectTransform)document.transform).anchoredPosition = _anchoredSpawnPosition;
            ServiceLocator.For(this).Get(out PlayerSignatureAcceptor acceptor);
            document.Init(new DocumentViewModel(new SignatureRecorder(), acceptor));
            return document;
        }

        public void Dispose() { }
    }
}
