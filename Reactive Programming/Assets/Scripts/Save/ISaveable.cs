namespace Save {
    public interface ISaveable {
        public string SaveKey { get; }
        public string Save();
        public void Load(object data);
    }
}