using System;
using Authoring;
using Contracts;
using Data.Rules;
using Services.Locator;
using UnityEngine;

namespace Presentation {
    public sealed class DocumentDispenser : MonoBehaviour {
        [SerializeField] private DocumentView _documentPrefab;
        [SerializeField] private RectTransform _parent;
        [SerializeField] private Vector2 _anchoredSpawnPosition;
        [SerializeField] private SignaturePresetDefinition _signaturePreset;
        [SerializeField] private SignatureDifficultyProfileDefinition _difficultyProfile;

        [ContextMenu("Spawn")]
        public DocumentView Spawn() {
            if (_documentPrefab == null) {
                throw new InvalidOperationException("DocumentDispenser requires a document prefab.");
            }

            if (_parent == null) {
                throw new InvalidOperationException("DocumentDispenser requires a parent RectTransform.");
            }

            bool hasPreset = _signaturePreset != null;
            bool hasDifficulty = _difficultyProfile != null;
            if (hasPreset != hasDifficulty)
                throw new InvalidOperationException("DocumentDispenser evaluation configuration is partial; assign both Signature Preset and Difficulty Profile, or neither.");

            DocumentView document = Instantiate(_documentPrefab, _parent, false);
            ((RectTransform)document.transform).anchoredPosition = _anchoredSpawnPosition;
            if (hasPreset) {
                ServiceLocator.For(this).Get(out ISignatureEvaluator evaluator);
                document.Init(new DocumentViewModel(new Services.SignatureRecorder(), evaluator, _signaturePreset,
                    _difficultyProfile.ToRules(), SignatureRuleModifiers.None));
            } else {
                document.Init(new DocumentViewModel());
            }
            return document;
        }
    }
}
