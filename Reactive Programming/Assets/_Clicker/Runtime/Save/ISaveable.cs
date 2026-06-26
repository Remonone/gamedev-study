using Newtonsoft.Json.Linq;

namespace Save {
    public interface ISaveable {
        public string SaveKey { get; }
        public int Priority { get; }
        public JToken Save();
        public void Load(JToken data);
    }
}