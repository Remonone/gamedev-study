using System.Collections.Generic;
using UnityEngine;

namespace Types.QTE {
    [CreateAssetMenu(fileName = "QteConfig", menuName = "Clicker/QTE/Config", order = 0)]
    public class QteConfig : ScriptableObject {
        [SerializeField, Tooltip("Prefab spawned for active QTEs. It must contain QteClickView and a collider for clicks.")]
        private QteObject _prefab;
        [SerializeField, Min(0.01f), Tooltip("Base seconds between QTE spawn attempts.")]
        private float _baseSpawnIntervalSeconds = 30f;
        [SerializeField, Min(0f), Tooltip("Random +/- seconds added to the base spawn interval before modifiers.")]
        private float _spawnIntervalRandomStepSeconds = 5f;
        [SerializeField, Min(0.01f), Tooltip("Seconds each spawned QTE remains active if not clicked enough times.")]
        private float _lifetimeSeconds = 10f;
        [SerializeField, Min(0f), Tooltip("Multiplicative spawn count. 1.45 means 1 guaranteed QTE and 45% chance for another.")]
        private float _spawnMultiplier = 1f;
        [SerializeField, Min(1), Tooltip("Base clicks needed to despawn a QTE by interaction.")]
        private int _baseClicksRequired = 1;
        [SerializeField, Min(0), Tooltip("Random integer [0..step] added to base clicks at spawn.")]
        private int _clicksRandomStep;
        [SerializeField, Tooltip("Reward definitions. One is selected randomly for each spawned QTE.")]
        private List<QteRewardDefinition> _rewards = new();

        public QteObject Prefab => _prefab;
        public float BaseSpawnIntervalSeconds => Mathf.Max(0.01f, _baseSpawnIntervalSeconds);
        public float SpawnIntervalRandomStepSeconds => Mathf.Max(0f, _spawnIntervalRandomStepSeconds);
        public float LifetimeSeconds => Mathf.Max(0.01f, _lifetimeSeconds);
        public float SpawnMultiplier => Mathf.Max(0f, _spawnMultiplier);
        public int BaseClicksRequired => Mathf.Max(1, _baseClicksRequired);
        public int ClicksRandomStep => Mathf.Max(0, _clicksRandomStep);
        public IReadOnlyList<QteRewardDefinition> Rewards => _rewards;
    }
}
