using System;
using System.Collections.Generic;
using UnityEngine;

namespace Data.Tutorial {
    [Serializable]
    public class TutorialSlide {
        [TextArea(3, 8)] [SerializeField] private string _text;
        [Tooltip("Optional speaker preview shown on the left. Leave empty to keep the space blank.")]
        [SerializeField] private Sprite _icon;
        [SerializeReference] private TutorialSlideCondition _advanceCondition;

        public string Text => _text;
        public Sprite Icon => _icon;
        public TutorialSlideCondition AdvanceCondition => _advanceCondition;
    }

    [CreateAssetMenu(menuName = "Tutorial/Tutorial Popup", fileName = "Tutorial Popup")]
    public class TutorialDefinition : ScriptableObject {
        [SerializeField] private string _id;
        [SerializeReference] private TutorialTriggerDefinition _trigger;
        [SerializeField] private TutorialSlide[] _slides = Array.Empty<TutorialSlide>();

        public string Id => _id;
        public TutorialTriggerDefinition Trigger => _trigger;
        public IReadOnlyList<TutorialSlide> Slides => _slides;
    }
}
