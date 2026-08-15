using System;
using System.Globalization;
using Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Utils.Text.Generator;

namespace Services {
    public sealed class SignatureCriticalRandomService : IService, ISaveable {
        public const string SaveSectionId = "signature_critical_random";

        private const ulong ManualStream = 0x9D1F4C7A22E3B501UL;
        private const ulong OfficeStream = 0xC87532A91D04FE6BUL;

        private StableRandom _manual;
        private StableRandom _office;

        public SignatureCriticalRandomService() : this(unchecked((ulong)DateTime.UtcNow.Ticks)) { }

        internal SignatureCriticalRandomService(ulong seed) {
            _manual = new StableRandom(SeedUtility.Derive(seed, ManualStream));
            _office = new StableRandom(SeedUtility.Derive(seed, OfficeStream));
        }

        public string SaveId => SaveSectionId;

        public bool RollManual(float chance) => Roll(ref _manual, chance);

        public bool RollOffice(float chance) => Roll(ref _office, chance);

        public JToken Serialize() {
            return new JObject {
                ["manualState"] = _manual.State,
                ["officeState"] = _office.State
            };
        }

        public void Deserialize(JToken state) {
            if (state is not JObject root ||
                !TryReadState(root["manualState"], out ulong manualState) ||
                !TryReadState(root["officeState"], out ulong officeState)) {
                throw new JsonSerializationException("Signature critical random state is incomplete.");
            }

            _manual = new StableRandom(manualState);
            _office = new StableRandom(officeState);
        }

        public void Dispose() { }

        public static double NormalizeMultiplier(double multiplier) {
            if (double.IsNaN(multiplier) || multiplier < 1d) return 1d;
            return double.IsPositiveInfinity(multiplier) ? double.MaxValue : multiplier;
        }

        private static bool Roll(ref StableRandom random, float chance) {
            if (float.IsNaN(chance) || float.IsInfinity(chance)) return false;
            return random.Chance(Math.Clamp(chance, 0f, 1f));
        }

        private static bool TryReadState(JToken token, out ulong value) {
            value = default;
            return token?.Type == JTokenType.Integer &&
                   ulong.TryParse(token.ToString(), NumberStyles.None,
                       CultureInfo.InvariantCulture, out value);
        }
    }
}
