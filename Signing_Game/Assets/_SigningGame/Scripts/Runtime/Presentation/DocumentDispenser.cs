using System;
using Data.Documents;
using Services;
using Services.Locator;
using UnityEngine;
using UnityEngine.Serialization;

namespace Presentation {
    public sealed class DocumentDispenser : MonoBehaviour, IService {
        [FormerlySerializedAs("_documentPrefab")]
        [SerializeField] private DocumentView _normalDocumentPrefab;
        [SerializeField] private DocumentView _upgradeDocumentPrefab;
        [SerializeField] private DocumentView _clerkHireDocumentPrefab;
        [SerializeField] private DocumentView _clerkSalaryReviewDocumentPrefab;
        [SerializeField] private DocumentView _billDocumentPrefab;
        [SerializeField] private DocumentView _practiceDocumentPrefab;
        [SerializeField] private RectTransform _parent;
        [SerializeField] private Vector2 _anchoredSpawnPosition;

        private DocumentViewModel.DocumentViewModelBuilder _builder;

        private void Awake() {
            _builder = new DocumentViewModel.DocumentViewModelBuilder();
        }

        public DocumentView SpawnPreview(DispensedDocumentPresentation presentation) {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (_parent == null) throw new InvalidOperationException("DocumentDispenser requires a parent RectTransform.");

            DocumentView prefab = ResolvePrefab(presentation.Kind);
            DocumentView document = Instantiate(prefab, _parent, false);
            try {
                ((RectTransform)document.transform).anchoredPosition = _anchoredSpawnPosition;
                document.ShowPreview(presentation);
                return document;
            }
            catch {
                Destroy(document.gameObject);
                throw;
            }
        }

        public void Bind(
            DocumentView document,
            IDocumentContext context,
            DispensedDocumentPresentation presentation) {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));

            DocumentViewModel viewModel = null;
            SignatureGuidanceService guidanceService = ServiceLocator.For(this).Get<SignatureGuidanceService>();
            bool forceProgressiveGuidance = presentation.Kind == DocumentKind.SignatureGuidance;
            bool hasGuidanceSnapshot = guidanceService.TryGetSnapshot(
                forceProgressiveGuidance,
                out SignatureGuidanceSnapshot guidance);
            try {
                ServiceLocator.For(this).Get(out PlayerSignatureAcceptor acceptor);
                viewModel = _builder
                    .SetContext(context)
                    .SetAcceptor(acceptor)
                    .Build();
                document.Init(viewModel, presentation, hasGuidanceSnapshot ? guidance : null);
                if (hasGuidanceSnapshot && guidance.IsSessionReminder) {
                    guidanceService.ConsumeSessionReminder();
                }
            }
            catch {
                viewModel?.Dispose();
                _builder.Reset();
                context.Dispose();
                throw;
            }
        }

        // Kept so the currently disabled DocumentSpawnerService continues to compile.
        // A context-free call remains unsupported and throws, as it did before this flow was introduced.
        [ContextMenu("Spawn")]
        public DocumentView Spawn(IDocumentContext context = null) {
            if (context == null) throw new ArgumentNullException(nameof(context), "A document context is required.");

            var offer = new DocumentOffer(new DocumentOfferKey(DocumentKind.Normal, "legacy-normal"), true);
            var presentation = new DispensedDocumentPresentation(
                offer,
                -1,
                0,
                unchecked((ulong)UnityEngine.Random.Range(int.MinValue, int.MaxValue)),
                Color.HSVToRGB(1f - 0.225f, 0.8f, 0.8f));
            DocumentView document = null;
            try {
                document = SpawnPreview(presentation);
                Bind(document, context, presentation);
                return document;
            }
            catch {
                if (document != null) Destroy(document.gameObject);
                throw;
            }
        }

        public void Dispose() { }

        private DocumentView ResolvePrefab(DocumentKind kind) {
            DocumentView prefab = kind switch {
                DocumentKind.Normal => _normalDocumentPrefab,
                DocumentKind.Upgrade => _upgradeDocumentPrefab,
                DocumentKind.ClerkHire => _clerkHireDocumentPrefab,
                DocumentKind.ClerkSalaryReview => _clerkSalaryReviewDocumentPrefab,
                DocumentKind.Bill => _billDocumentPrefab,
                DocumentKind.Practice => _practiceDocumentPrefab,
                DocumentKind.SignatureGuidance => _normalDocumentPrefab,
                _ => null
            };
            if (prefab == null) {
                throw new InvalidOperationException($"DocumentDispenser has no prefab assigned for {kind} documents.");
            }

            return prefab;
        }
    }
}
