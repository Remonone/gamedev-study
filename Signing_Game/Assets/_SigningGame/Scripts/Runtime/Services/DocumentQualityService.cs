using System;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using R3;
using Services.Locator;

namespace Services {
    public sealed class DocumentQualityService : IService, IInitialize, IPostInitialize, ISaveable {
        public const int MinimumQualityLevel = 0;
        private const int AbsoluteMaximumQualityLevel = 9;
        public const string SaveSectionId = "document_quality";

        private readonly Subject<Unit> _changed = new();
        private readonly CompositeDisposable _subscriptions = new();

        private IReadOnlyCacheData<DocumentEntries> _documents;
        private ICacheInvalidator _cacheInvalidator;
        private CacheVersionService _cacheVersions;

        private int _selectedQualityLevel;
        private int _pendingQualityLevel;
        private int _maximumQualityLevel;
        private bool _hasPendingQualityLevel;
        private bool _postInitialized;
        private bool _handlingInvalidation;

        public string SaveId => SaveSectionId;
        public Observable<Unit> Changed => _changed;
        public int SelectedQualityLevel => _selectedQualityLevel;
        public int SelectedDocumentQualityLevel => _selectedQualityLevel;
        public int MaximumQualityLevel => _maximumQualityLevel;
        public bool IsAvailable => _maximumQualityLevel > MinimumQualityLevel;

        public UniTask InitializeAsync(IServiceScope scope) {
            return UniTask.CompletedTask;
        }

        public UniTask PostInitializeAsync(IServiceScope scope) {
            _documents = scope.Get<PlayerStatStash>().Documents;
            _cacheInvalidator = scope.Get<ICacheInvalidator>();
            _cacheVersions = scope.Get<CacheVersionService>();
            _postInitialized = true;

            if (_hasPendingQualityLevel) {
                _selectedQualityLevel = _pendingQualityLevel;
                _hasPendingQualityLevel = false;
            }

            _cacheVersions.Invalidated.Subscribe(OnCacheInvalidated).AddTo(_subscriptions);
            ReconcileWithDocumentCache(true);
            return UniTask.CompletedTask;
        }

        public bool MoveSelection(int delta) {
            if (delta == 0) return false;

            int next = Math.Clamp(_selectedQualityLevel + delta,
                MinimumQualityLevel,
                _postInitialized ? _maximumQualityLevel : AbsoluteMaximumQualityLevel);
            return SetSelection(next);
        }

        public bool SetSelection(int qualityLevel) {
            int maximum = _postInitialized ? _maximumQualityLevel : AbsoluteMaximumQualityLevel;
            int next = Math.Clamp(qualityLevel, MinimumQualityLevel, maximum);
            if (next == _selectedQualityLevel) return false;

            _selectedQualityLevel = next;
            if (_postInitialized) {
                _cacheInvalidator.Invalidate<DocumentEntries>();
            }
            else {
                _pendingQualityLevel = next;
                _hasPendingQualityLevel = true;
            }
            _changed.OnNext(Unit.Default);
            return true;
        }

        public JToken Serialize() {
            return new JObject {
                ["selectedLevel"] = _hasPendingQualityLevel ? _pendingQualityLevel : _selectedQualityLevel
            };
        }

        public void Deserialize(JToken state) {
            int selected = ReadSelectedLevel(state);
            if (_postInitialized) {
                _selectedQualityLevel = selected;
                ReconcileWithDocumentCache(true);
                return;
            }

            _pendingQualityLevel = selected;
            _hasPendingQualityLevel = true;
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _changed.Dispose();
            _documents = null;
            _cacheInvalidator = null;
            _cacheVersions = null;
            _postInitialized = false;
            _handlingInvalidation = false;
        }

        private void OnCacheInvalidated(Type type) {
            if (!_postInitialized || type != typeof(DocumentEntries) || _handlingInvalidation) return;

            _handlingInvalidation = true;
            try {
                ReconcileWithDocumentCache(true);
            }
            finally {
                _handlingInvalidation = false;
            }
        }

        private void ReconcileWithDocumentCache(bool invalidateWhenSelectionChanges) {
            if (_documents == null) return;

            DocumentEntries entries = _documents.Value;
            int nextMaximum = Math.Clamp(entries.DocumentQualityLevel,
                MinimumQualityLevel,
                AbsoluteMaximumQualityLevel);
            int nextSelected = Math.Clamp(_selectedQualityLevel,
                MinimumQualityLevel,
                nextMaximum);
            bool maximumChanged = nextMaximum != _maximumQualityLevel;
            bool selectionChanged = nextSelected != _selectedQualityLevel;

            _maximumQualityLevel = nextMaximum;
            if (!selectionChanged && !maximumChanged) return;

            if (selectionChanged) {
                _selectedQualityLevel = nextSelected;
                if (invalidateWhenSelectionChanges) _cacheInvalidator.Invalidate<DocumentEntries>();
            }

            _changed.OnNext(Unit.Default);
        }

        private static int ReadSelectedLevel(JToken state) {
            if (state is not JObject data || data["selectedLevel"]?.Type != JTokenType.Integer) {
                throw new JsonSerializationException(
                    "Document quality save data must contain an integer selectedLevel.");
            }

            int selected = data["selectedLevel"].Value<int>();
            if (selected < MinimumQualityLevel || selected > AbsoluteMaximumQualityLevel) {
                throw new JsonSerializationException(
                    $"Document quality selectedLevel must be between {MinimumQualityLevel} and {AbsoluteMaximumQualityLevel}.");
            }

            return selected;
        }
    }
}
