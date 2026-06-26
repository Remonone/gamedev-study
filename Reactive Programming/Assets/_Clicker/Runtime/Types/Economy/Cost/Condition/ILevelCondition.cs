namespace Types.Modifiers.Cost.Condition {
    public interface ILevelCondition {
        bool IsMet(int level);
    }
}