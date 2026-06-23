using System;
using Newtonsoft.Json.Linq;
using R3;
using Save;
using Types.Modifiers.Definitions;
using Types.Modifiers.Definitions.Buildings;

namespace Services.Player {
    public class PlayerEffectService : IService, IStartable, IDisposable, ISaveable {
        private readonly SessionContext _context;
        private readonly BuildingUpgradeService _buildingUpgradeService;
        private readonly InvalidationService _invalidationService;
        
        private CompositeDisposable _disposable = new();
        
        public PlayerEffectService(SessionContext context, 
            BuildingUpgradeService buildingUpgradeService, 
            InvalidationService invalidationService) {
            _context = context;
            _buildingUpgradeService = buildingUpgradeService;
            _invalidationService = invalidationService;
        }


        public void StartService() {
            _buildingUpgradeService.OnBuildingUpgrade.Subscribe(OnBuildingUpgrade).AddTo(_disposable);
        }

        private void OnBuildingUpgrade(BuildingUpgrade upgrade) {
            var definition = upgrade.Building.Definition;
            var level = upgrade.Building.Level;

            var typeInfluence = _context.GetInfluenceInternalValue(definition.Type);
            var influenceWithoutBuilding = typeInfluence - definition.Influence * level;
            if (influenceWithoutBuilding <= 0) {
                Invalidate();
                _context.SetInfluence(definition.Type, upgrade.Building.Level * definition.Influence);
            }
            else {
                var buildingInfluence = upgrade.Building.Level * definition.Influence;
                _context.SetInfluence(definition.Type, influenceWithoutBuilding + buildingInfluence);
            }

            VerifyInfluenceForUpdate(upgrade.Building);
        }

        private void Invalidate() {
            _invalidationService.InvalidateAll();
        }

        private void VerifyInfluenceForUpdate(BuildingState state) {
            var type = state.Definition.Type;
            var internalValue = _context.GetInfluenceInternalValue(type);
            var externalValue = _context.GetInfluenceValue(type);
            if (externalValue == 0) {
                _context.UpdateInfluence(type);
                return;
            }
            var ratio = (internalValue - externalValue) / externalValue;
            if (ratio < 0.05) return;
            _context.UpdateInfluence(type);
            Invalidate();
        }

        public void Dispose() {
            _disposable.Dispose();
        }

        public string SaveKey => "Influence";
        public int Priority => 93;
        public JToken Save() {
            var values = new JObject();
            var property = new JProperty("Influence", values);
            foreach (var influenceType in (GovernmentInteractionType[])Enum.GetValues(typeof(GovernmentInteractionType))) {
                values.Add(new JProperty(influenceType.ToString(), new JObject(
                    new JProperty("Internal", _context.GetInfluenceInternalValue(influenceType)),
                    new JProperty("External", _context.GetInfluenceValue(influenceType))
                    )
                ));
            }
            return new JObject(property);
        }

        public void Load(JToken data) {
            if (data["Influence"] is not JObject values) {
                return;
            }
            foreach (var value in values) {
                if (!Enum.TryParse(value.Key, out GovernmentInteractionType influenceType)) {
                    continue;
                }
                var internalValue = value.Value?["Internal"]?.ToObject<int>() ?? 0;
                var externalValue = value.Value?["External"]?.ToObject<int>() ?? 0;
                _context.SetInfluence(influenceType, externalValue);
                _context.UpdateInfluence(influenceType);
                _context.SetInfluence(influenceType, internalValue);
            }
        }
    }
}