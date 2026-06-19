using System;
using UnityEngine;

namespace Audio {
    [Serializable]
    public struct AudioCue {
        [Tooltip("Audio clip played for this cue.")]
        public AudioClip Clip;
        
        public AudioCue(AudioClip clip) {
            Clip = clip;
        }
    }
}