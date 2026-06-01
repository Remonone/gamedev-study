using System;
using System.Collections.Generic;
using Bases;
using Components;
using Components.Instances;
using DG.Tweening;
using R3;
using Types;
using UnityEngine;

namespace Animators {
    public class StructureAnimator : MonoBehaviour {
        [SerializeField] private GameObject[] _structures;

        private Dictionary<StructureType, GameObject> _structuresByType;
        
        private Vector3 _scale = new Vector3(1f, 0.8f, 1f);
        
        private void Awake() {
            _structuresByType = new Dictionary<StructureType, GameObject>();

            foreach (var structure in _structures) {
                if(ReferenceEquals(structure, null) || !structure.TryGetComponent<IStructure>(out _)) {
                    continue;
                }
                
                if(!structure.TryGetComponent<IStructure>(out var structureType)) {
                    continue;
                }

                Debug.Log("Adding a structure: " + structureType.Type + " to the dictionary");
                _structuresByType.Add(structureType.Type, structure);
            }
        }

        private void Start() {
            var service = ServiceLocator.Instance.GetService<StructureClickService>();

            service.StructureInteraction
                .Where(interaction => _structuresByType.ContainsKey(interaction.Structure))
                .Select(interaction => _structuresByType[interaction.Structure])
                .Where(structure => structure != null)
                .Subscribe(HandleClick);
        }
        
        private void HandleClick(GameObject structure) {
            var initialScale = structure.transform.localScale;
            structure.transform.DOScale(Vector3.Scale(initialScale, _scale), 0.1f).SetEase(Ease.OutQuad).OnComplete(() => structure.transform.DOScale(initialScale, 0.1f).SetEase(Ease.InQuad));
        }
    }
}