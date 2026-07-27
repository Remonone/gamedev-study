using Newtonsoft.Json.Linq;

namespace Contracts {
    public interface ISaveable {
        string SaveId { get; }

        JToken Serialize();

        void Deserialize(JToken state);
    }
}
