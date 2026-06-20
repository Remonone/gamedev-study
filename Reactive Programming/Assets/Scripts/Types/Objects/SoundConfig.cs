using System;
using System.Collections.Generic;
using Audio;
using UnityEngine;

namespace Types.Modifiers.Definitions.Objects {
    [CreateAssetMenu(fileName = "Sound Config", menuName = "Clicker/Sound Config", order = 0)]
    public class StructureSoundConfig : ScriptableObject {
        
        [SerializeField, Tooltip("Audio cues used for structure interactions, grouped by interaction type.")]
        public List<AudioConfig> Clips;


        [Serializable]
        public struct AudioConfig {
            [Tooltip("Interaction type that triggers this cue.")]
            public GovernmentInteractionType Type;
            [Tooltip("Audio cue played for this interaction type.")]
            public AudioCue Cue;
        }
    }
}
