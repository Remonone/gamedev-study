namespace Data.Cache {
    public interface ICacheCalculator<out T> {
        T Calculate();
    }
}