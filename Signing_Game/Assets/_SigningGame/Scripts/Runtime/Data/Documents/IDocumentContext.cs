namespace Data.Documents {
    public interface IDocumentContext {
        void GetBehavior<T>(out T behavior) where T : notnull;
    }
}