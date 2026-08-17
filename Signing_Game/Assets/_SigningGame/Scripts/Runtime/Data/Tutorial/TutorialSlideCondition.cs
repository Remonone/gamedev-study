using System;

namespace Data.Tutorial {
    public enum TutorialInteractionKind {
        PlayerClick = 0,
        TabOpened = 1,
        SignatureStroke = 2,
        DocumentCollected = 3
    }

    public readonly struct TutorialInteractionEvent {
        public TutorialInteractionKind Kind { get; }
        public string TabId { get; }

        public TutorialInteractionEvent(TutorialInteractionKind kind, string tabId = null) {
            Kind = kind;
            TabId = tabId ?? string.Empty;
        }
    }

    [Serializable]
    public abstract class TutorialSlideCondition {
        /// <summary>
        /// False when the condition is satisfied by a plain player click and the gameplay input
        /// should stay blocked while the slide awaits advancement.
        /// </summary>
        public virtual bool RequiresInteraction => true;

        /// <summary>
        /// Identifier of the UI target that should be highlighted (brought to the foreground)
        /// while the slide awaits the interaction. Null/empty when there is no focus target.
        /// </summary>
        public virtual string FocusTargetId => null;

        public abstract bool IsSatisfiedBy(in TutorialInteractionEvent interactionEvent);
    }

    [Serializable]
    public sealed class ClickCondition : TutorialSlideCondition {
        public override bool RequiresInteraction => false;

        public override bool IsSatisfiedBy(in TutorialInteractionEvent interactionEvent) {
            return interactionEvent.Kind == TutorialInteractionKind.PlayerClick;
        }
    }

    [Serializable]
    public sealed class OpenTabCondition : TutorialSlideCondition {
        public string TabId;

        public override string FocusTargetId => TabId;

        public override bool IsSatisfiedBy(in TutorialInteractionEvent interactionEvent) {
            return interactionEvent.Kind == TutorialInteractionKind.TabOpened &&
                   string.Equals(interactionEvent.TabId, TabId, StringComparison.Ordinal);
        }
    }

    [Serializable]
    public sealed class SignatureStrokeCondition : TutorialSlideCondition {
        public override bool IsSatisfiedBy(in TutorialInteractionEvent interactionEvent) {
            return interactionEvent.Kind == TutorialInteractionKind.SignatureStroke;
        }
    }

    [Serializable]
    public sealed class DragDocumentCondition : TutorialSlideCondition {
        public override string FocusTargetId => Constants.TutorialIds.DocumentCollector;

        public override bool IsSatisfiedBy(in TutorialInteractionEvent interactionEvent) {
            return interactionEvent.Kind == TutorialInteractionKind.DocumentCollected;
        }
    }
}
