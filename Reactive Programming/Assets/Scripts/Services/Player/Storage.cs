using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using R3;
using Save;
using Types.Enums;
using Types.Enums.Cost;

namespace Services.Player {
    public class Storage : IService, ISaveable {
        private static readonly IReadOnlyDictionary<StructureType, WalletAccessor> WalletAccessors =
            new Dictionary<StructureType, WalletAccessor> {
                [StructureType.MayorOffice] = new(
                    wallet => wallet.MayorWallet,
                    (wallet, value) => {
                        wallet.MayorWallet = value;
                        return wallet;
                    }),
                [StructureType.Court] = new(
                    wallet => wallet.CourtWallet,
                    (wallet, value) => {
                        wallet.CourtWallet = value;
                        return wallet;
                    }),
                [StructureType.FireFighterStation] = new(
                    wallet => wallet.FirefighterWallet,
                    (wallet, value) => {
                        wallet.FirefighterWallet = value;
                        return wallet;
                    }),
                [StructureType.PoliceStation] = new(
                    wallet => wallet.PoliceWallet,
                    (wallet, value) => {
                        wallet.PoliceWallet = value;
                        return wallet;
                    }),
                [StructureType.Hospital] = new(
                    wallet => wallet.AmbulanceWallet,
                    (wallet, value) => {
                        wallet.AmbulanceWallet = value;
                        return wallet;
                    }),
                [StructureType.Archive] = new(
                    wallet => wallet.ArchiveWallet,
                    (wallet, value) => {
                        wallet.ArchiveWallet = value;
                        return wallet;
                    })
            };

        private readonly ReactiveProperty<Wallet> _structureMoney = new();

        public Observable<Wallet> StructureMoney => _structureMoney;

        public void AddMoney(StructureType type, long amount){
            SetByType(type, GetByType(type) + amount);
        }

        public long GetByType(StructureType type) => GetByType(_structureMoney.Value, type);

        public Observable<long> ObserveByType(StructureType type) {
            return _structureMoney.Select(wallet => GetByType(wallet, type));
        }

        public bool CanAfford(Price price) {
            return price.Entries == null || price.Entries.All(entry =>
                GetByType(entry.StructureType) >= NormalizePrice(entry.Price));
        }

        public void Spend(Price price) {
            if (price.Entries == null) return;

            foreach (var entry in price.Entries) {
                AddMoney(entry.StructureType, -NormalizePrice(entry.Price));
            }
        }

        private void SetByType(StructureType type, long amount) {
            var accessor = GetAccessor(type);
            _structureMoney.Value = accessor.Set(_structureMoney.Value, amount);
        }

        public string SaveKey => "Storage";
        public int Priority => 99;
        
        public JToken Save() {
            return new JObject(
                new JProperty("Money", JObject.FromObject(_structureMoney.Value))
            );
        }

        public void Load(JToken data) {
            if (data?["Money"] is JObject money) {
                _structureMoney.Value = money.ToObject<Wallet>();
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
                if (!Enum.TryParse(typeName, out StructureType structureType)) continue;
                if (!WalletAccessors.ContainsKey(structureType)) continue;

                var accessor = GetAccessor(structureType);
                wallet = accessor.Set(wallet, token.Value<long?>("amount") ?? 0);
            }

            _structureMoney.Value = wallet;
        }

        private static long GetByType(Wallet wallet, StructureType type) {
            return GetAccessor(type).Get(wallet);
        }

        private static WalletAccessor GetAccessor(StructureType type) {
            if (WalletAccessors.TryGetValue(type, out var accessor)) return accessor;
            throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        private static long NormalizePrice(decimal price) {
            return (long)Math.Ceiling(price);
        }

        private readonly struct WalletAccessor {
            public readonly Func<Wallet, long> Get;
            public readonly Func<Wallet, long, Wallet> Set;

            public WalletAccessor(Func<Wallet, long> get, Func<Wallet, long, Wallet> set) {
                Get = get;
                Set = set;
            }
        }
    }
}
