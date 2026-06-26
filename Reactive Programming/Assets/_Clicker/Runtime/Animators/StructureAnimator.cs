using System.Collections.Generic;
using Services.Components;
using Services.Components.Instances;
using DG.Tweening;
using R3;
using Types;
using Types.Enums;
using UnityEngine;

namespace Animators {
    public class StructureAnimator : MonoBehaviour {

        private Dictionary<GovernmentInteractionType, StructureConfig> _structuresByType;
        
        private Vector3 _scale = new Vector3(1f, 0.8f, 1f);

        readonly struct StructureConfig {
            public readonly GameObject Reference;
            public readonly Vector3 Scale;
            
            public StructureConfig(GameObject reference, Vector3 scale) {
                Reference = reference;
                Scale = scale;
            }
        }

        private void Start() {
            Init();
            var service = ServiceLocator.Instance.GetService<StructureClickService>();

            service.StructureInteraction
                .Where(interaction => _structuresByType.ContainsKey(interaction.GovernmentInteraction))
                .Select(interaction => _structuresByType[interaction.GovernmentInteraction])
                .Subscribe(HandleClick).AddTo(this);
        }

        private void Init() {
            _structuresByType = new Dictionary<GovernmentInteractionType, StructureConfig>();

            foreach (var structure in FindObjectsByType<Structure>(FindObjectsSortMode.InstanceID)) {
                _structuresByType.Add(structure.Definition.Type, new StructureConfig(structure.gameObject, structure.transform.localScale));
            }
        }

        private void HandleClick(StructureConfig structure) {
            var reference = structure.Reference;
            reference.transform.DOScale(Vector3.Scale(structure.Scale, _scale), 0.1f).SetEase(Ease.OutQuad).OnComplete(() => reference.transform.DOScale(structure.Scale, 0.1f).SetEase(Ease.InQuad));
        }
    }
}