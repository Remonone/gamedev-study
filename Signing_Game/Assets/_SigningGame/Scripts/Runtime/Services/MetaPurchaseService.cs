using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data.Persistence;
using Newtonsoft.Json.Linq;
using Services.Locator;
using UnityEngine;

namespace Services {
    public sealed class MetaPurchaseService : IService, IInitialize {
        private const string SignatureProgressionSection = "signature_progression";

        private MetaProgressionService _meta;
        private MetaUpgradeTreeService _tree;
        private SaveService _save;
        private AutoSaveService _autoSave;
        private SceneFlowService _sceneFlow;
        private bool _commitInProgress;
        private bool _committed;

        public bool IsCommitInProgress => _commitInProgress;

        public UniTask InitializeAsync(IServiceScope scope) {
            _meta = scope.Get<MetaProgressionService>();
            _tree = scope.Get<MetaUpgradeTreeService>();
            _save = scope.Get<SaveService>();
            _autoSave = scope.Get<AutoSaveService>();
            _sceneFlow = scope.Container.Get<SceneFlowService>();
            return UniTask.CompletedTask;
        }

        public bool TryPurchase(string metaUpgradeId) {
            if (_commitInProgress || _committed || _sceneFlow.IsTransitionInProgress ||
                !_tree.CanPurchase(metaUpgradeId) ||
                !_meta.TryCreatePurchasedState(metaUpgradeId, out JToken purchasedMetaState, out _)) {
                return false;
            }

            if (!_sceneFlow.TryReserveGameReload(GameLaunchMode.Continue, out int reservation)) return false;
            _commitInProgress = true;
            try {
                SaveSnapshot current = _save.CreateSnapshot();
                if (!current.Sections.TryGetValue(SignatureProgressionSection, out JToken signature) || signature == null) {
                    Debug.LogWarning("Meta purchase was cancelled because signature progression could not be preserved.");
                    _sceneFlow.CancelReservedGameReload(reservation);
                    return false;
                }

                var sections = new Dictionary<string, JToken>(StringComparer.Ordinal) {
                    [SignatureProgressionSection] = signature.DeepClone(),
                    [MetaProgressionService.SaveSectionId] = purchasedMetaState.DeepClone()
                };
                var resetSnapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, sections);

                _autoSave.Suspend();
                if (!_save.SaveSnapshotToFile(resetSnapshot)) {
                    _autoSave.Resume();
                    _sceneFlow.CancelReservedGameReload(reservation);
                    return false;
                }

                _committed = true;
                if (_sceneFlow.StartReservedGameReload(reservation)) return true;

                Debug.LogError("The meta reset was committed, but the reserved gameplay reload could not start. " +
                               "Autosave remains suspended to protect the committed reset.");
                _sceneFlow.RecoverCommittedReloadToMainMenu();
                return false;
            } catch (Exception exception) {
                if (!_committed) {
                    _autoSave.Resume();
                    _sceneFlow.CancelReservedGameReload(reservation);
                }
                Debug.LogException(exception);
                return false;
            } finally {
                _commitInProgress = false;
            }
        }

        public void Dispose() {
            if (!_committed && _autoSave != null && _autoSave.IsSuspended) _autoSave.Resume();
        }
    }
}
