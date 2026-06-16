using System;
using System.Collections.Generic;
using Audio;
using Types.Enums;
using UnityEngine;

namespace Types.Enums.Objects {
    [CreateAssetMenu(fileName = "Sound Config", menuName = "Clicker/Sound Config", order = 0)]
    public class StructureSoundConfig : ScriptableObject {
        
        [SerializeField] public List<AudioConfig> Clips;


        [Serializable]
        public struct AudioConfig {
            public StructureType Type;
            public AudioCue Cue;
        }
    }
}