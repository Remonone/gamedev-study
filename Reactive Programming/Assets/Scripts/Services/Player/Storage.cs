using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using R3;
using Save;
using Types.Enums;
using Types.Enums.Cost;
using Types.Enums.Values;

namespace Services.Player {
    public class Storage : IService, ISaveable {
        private static readonly IReadOnlyDictionary<GovernmentInteractionType, WalletAccessor> WalletAccessors =
            new Dictionary<GovernmentInteractionType, WalletAccessor> {
                [GovernmentInteractionType.MayorOffice] = new(
                    wallet => wallet.MayorWallet,
                    (wallet, value) => {
                        wallet.MayorWallet = value;
                        return wallet;
                    }),
                [GovernmentInteractionType.Court] = new(
                    wallet => wallet.CourtWallet,
                    (wallet, value) => {
                        wallet.CourtWallet = value;
                        return wallet;
                    }),
                [GovernmentInteractionType.FireFighterStation] = new(
                    wallet => wallet.FirefighterWallet,
                    (wallet, value) => {
                        wallet.FirefighterWallet = value;
                        return wallet;
                    }),
                [GovernmentInteractionType.PoliceStation] = new(
                    wallet => wallet.PoliceWallet,
                    (wallet, value) => {
                        wallet.PoliceWallet = value;
                        return wallet;
                    }),
                [GovernmentInteractionType.Hospital] = new(
                    wallet => wallet.AmbulanceWallet,
                    (wallet, value) => {
                        wallet.AmbulanceWallet = value;
                        return wallet;
                    }),
                [GovernmentInteractionType.Archive] = new(
                    wallet => wallet.ArchiveWallet,
                    (wallet, value) => {
                        wallet.ArchiveWallet = value;
                        return wallet;
                    })
            };

        private readonly ReactiveProperty<Wallet> _structureMoney = new();

        public Observable<Wallet> StructureMoney => _structureMoney;

        public void AddMoney(GovernmentInteractionType type, Value amount){
            SetByType(type, GetByType(type) + amount);
        }

        public Value GetByType(GovernmentInteractionType type) => GetByType(_structureMoney.Value, type);

        public Observable<Value> ObserveByType(GovernmentInteractionType type) {
            return _structureMoney.Select(wallet => GetByType(wallet, type));
        }

        public bool CanAfford(Price price) {
            return price.Entries == null || price.Entries.All(entry =>
                GetByType(entry.GovernmentInteractionType) >= entry.Price);
        }

        public void Spend(Price price) {
            if (price.Entries == null) return;

            foreach (var entry in price.Entries) {
                var accessor = GetAccessor(entry.GovernmentInteractionType);
                var currentWallet = _structureMoney.Value;
                var updatedAmount = accessor.Get(currentWallet) - entry.Price;
                _structureMoney.Value = accessor.Set(currentWallet, updatedAmount ?? Value.Zero);
            }
        }

        private void SetByType(GovernmentInteractionType type, Value amount) {
            var accessor = GetAccessor(type);
            _structureMoney.Value = accessor.Set(_structureMoney.Value, amount);
        }

        public string SaveKey => "Storage";
        public int Priority => 99;
        
        public JToken Save() {
            return new JObject(
                new JProperty("Money", SaveWallet(_structureMoney.Value))
            );
        }

        public void Load(JToken data) {
            if (data?["Money"] is JObject money) {
                _structureMoney.Value = LoadWallet(money);
                return;
            }

            if (data?["Money"] is JArray legacyMoney) {
                LoadLegacyMoney(legacyMoney);
            }
        }

        private void LoadLegacyMoney(JArray money) {
            var wallet = new Wallet();

            foreach (var token in money.OfType<JObject>()) {
                var typeName = token.Value<string>("type");
                if (!Enum.TryParse(typeName, out GovernmentInteractionType structureType)) continue;
                if (!WalletAccessors.ContainsKey(structureType)) continue;

                var accessor = GetAccessor(structureType);
                wallet = accessor.Set(wallet, new Value(token.Value<double?>("amount") ?? 0d));
            }

            _structureMoney.Value = wallet;
        }

        private static Value GetByType(Wallet wallet, GovernmentInteractionType type) {
            return GetAccessor(type).Get(wallet);
        }

        private static WalletAccessor GetAccessor(GovernmentInteractionType type) {
            if (WalletAccessors.TryGetValue(type, out var accessor)) return accessor;
            throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        private static JObject SaveWallet(Wallet wallet) {
            return new JObject(
                new JProperty(nameof(Wallet.MayorWallet), SaveValue(wallet.MayorWallet)),
                new JProperty(nameof(Wallet.CourtWallet), SaveValue(wallet.CourtWallet)),
                new JProperty(nameof(Wallet.FirefighterWallet), SaveValue(wallet.FirefighterWallet)),
                new JProperty(nameof(Wallet.PoliceWallet), SaveValue(wallet.PoliceWallet)),
                new JProperty(nameof(Wallet.AmbulanceWallet), SaveValue(wallet.AmbulanceWallet)),
                new JProperty(nameof(Wallet.ArchiveWallet), SaveValue(wallet.ArchiveWallet))
            );
        }

        private static Wallet LoadWallet(JObject money) {
            var wallet = new Wallet();
            wallet.MayorWallet = LoadValue(money[nameof(Wallet.MayorWallet)]);
            wallet.CourtWallet = LoadValue(money[nameof(Wallet.CourtWallet)]);
            wallet.FirefighterWallet = LoadValue(money[nameof(Wallet.FirefighterWallet)]);
            wallet.PoliceWallet = LoadValue(money[nameof(Wallet.PoliceWallet)]);
            wallet.AmbulanceWallet = LoadValue(money[nameof(Wallet.AmbulanceWallet)]);
            wallet.ArchiveWallet = LoadValue(money[nameof(Wallet.ArchiveWallet)]);
            return wallet;
        }

        private static JToken SaveValue(Value value) {
            return new JObject(
                new JProperty("stored", value.Stored),
                new JProperty("degree", value.Base.Degree)
            );
        }

        private static Value LoadValue(JToken token) {
            if (token == null || token.Type == JTokenType.Null) {
                return Value.Zero;
            }

            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) {
                return new Value(token.Value<double>());
            }

            if (token is not JObject valueObject) {
                return Value.Zero;
            }

            var stored = valueObject.Value<double?>("stored")
                         ?? valueObject.Value<double?>("Stored")
                         ?? 0d;
            var degree = valueObject.Value<int?>("degree")
                         ?? valueObject["base"]?.Value<int?>("Degree")
                         ?? valueObject["Base"]?.Value<int?>("Degree")
                         ?? 0;

            return new Value(stored, new Base { Degree = degree });
        }

        private readonly struct WalletAccessor {
            public readonly Func<Wallet, Value> Get;
            public readonly Func<Wallet, Value, Wallet> Set;

            public WalletAccessor(Func<Wallet, Value> get, Func<Wallet, Value, Wallet> set) {
                Get = get;
                Set = set;
            }
        }
    }
}
