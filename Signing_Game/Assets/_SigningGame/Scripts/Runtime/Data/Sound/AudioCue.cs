using UnityEngine;

namespace Data.Sound {
    [CreateAssetMenu(menuName = "Audio/Cue", fileName = "Audio Cue")]
    public class AudioCue : ScriptableObject {
        [SerializeField] private AudioClip[] _clips;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [SerializeField] private Vector2 _pitchRange = Vector2.one;
        
        public float Volume => _volume;
        public AudioClip[] Clips => _clips;
        public Vector2 PitchRange => _pitchRange;
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            _pitchRange.x = Mathf.Clamp(_pitchRange.x, -3f, 3f);
            _pitchRange.y = Mathf.Clamp(_pitchRange.y, -3f, 3f);
        }
#endif
    }
}