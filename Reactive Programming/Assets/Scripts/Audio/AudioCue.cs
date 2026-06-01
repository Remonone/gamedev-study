using System;
using UnityEngine;

namespace Audio {
    [Serializable]
    public struct AudioCue {
        public AudioClip Clip;
        
        public AudioCue(AudioClip clip) {
            Clip = clip;
        }
    }
}