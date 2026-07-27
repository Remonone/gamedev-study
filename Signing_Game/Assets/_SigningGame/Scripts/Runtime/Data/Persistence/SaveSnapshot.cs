using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Data.Persistence {
    [Serializable]
    public sealed class SaveSnapshot {
        public const int CurrentVersion = 1;

        [JsonProperty("version")]
        public int Version { get; }

        [JsonProperty("sections")]
        public Dictionary<string, JToken> Sections { get; }

        public SaveSnapshot() : this(CurrentVersion, new Dictionary<string, JToken>(StringComparer.Ordinal)) { }

        [JsonConstructor]
        public SaveSnapshot(int version, Dictionary<string, JToken> sections) {
            Version = version;
            Sections = sections ?? new Dictionary<string, JToken>(StringComparer.Ordinal);
        }
    }
}
