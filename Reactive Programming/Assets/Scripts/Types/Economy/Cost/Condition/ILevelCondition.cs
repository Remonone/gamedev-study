namespace Types.Enums.Cost.Condition {
    public interface ILevelCondition {
        bool IsMet(int level);
    }
}