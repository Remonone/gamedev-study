using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace Views.Models {
    [Serializable]
    public struct ArtifactViewData {
        public string Id;
        public string Name;
        public string Description;
        public Sprite Icon;
        public bool IsUnlocked;
    }

    public class ArtifactsTabViewModel {
        public ReactiveProperty<IReadOnlyList<ArtifactViewData>> Artifacts = new(Array.Empty<ArtifactViewData>());
        public ReactiveProperty<ArtifactViewData> SelectedArtifact = new(new ArtifactViewData());

        public void RequestInitialState() {
        }

        public void SelectArtifact(string artifactId) {
        }
    }
}
