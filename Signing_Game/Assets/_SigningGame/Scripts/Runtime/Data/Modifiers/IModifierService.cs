namespace Data.Modifiers {
    public interface IModifierService {
        T Apply<T>(T value) where T : struct;
    }
}