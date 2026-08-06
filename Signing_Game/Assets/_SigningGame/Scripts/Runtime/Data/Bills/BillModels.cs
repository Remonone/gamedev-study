using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Utils;

namespace Data.Bills {
    public sealed class BillRequirementSnapshot {
        public string TemplateId { get; }
        public BillRequirementKind Kind { get; }
        public int NumericTarget { get; }
        public string UpgradeId { get; }
        public BillRequirementBalance Balance { get; }

        public BillRequirementSnapshot(
            string templateId,
            BillRequirementKind kind,
            int numericTarget,
            string upgradeId,
            BillRequirementBalance balance) {
            TemplateId = templateId;
            Kind = kind;
            NumericTarget = numericTarget;
            UpgradeId = upgradeId;
            Balance = balance;
        }
    }

    public sealed class GeneratedBillOption {
        private readonly ReadOnlyCollection<BillRequirementSnapshot> _requirements;

        public long OptionId { get; }
        public BillRewardDefinition Reward { get; }
        public IReadOnlyList<BillRequirementSnapshot> Requirements => _requirements;
        public Value RawCost { get; }
        public double RequiredProgress { get; }
        public float SignatureThreshold { get; }

        public GeneratedBillOption(
            long optionId,
            BillRewardDefinition reward,
            IReadOnlyList<BillRequirementSnapshot> requirements,
            Value rawCost,
            double requiredProgress,
            float signatureThreshold) {
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            OptionId = optionId;
            Reward = reward;
            var copy = new BillRequirementSnapshot[requirements?.Count ?? 0];
            for (int index = 0; index < copy.Length; index++) copy[index] = requirements[index];
            _requirements = Array.AsReadOnly(copy);
            RawCost = rawCost;
            RequiredProgress = requiredProgress;
            SignatureThreshold = signatureThreshold;
        }
    }

    public sealed class PendingBillState {
        public GeneratedBillOption Option { get; }
        public Value PaidCost { get; }

        public PendingBillState(GeneratedBillOption option, Value paidCost) {
            Option = option ?? throw new ArgumentNullException(nameof(option));
            PaidCost = paidCost;
        }
    }

    public sealed class ActiveBillState {
        public long InstanceId { get; }
        public GeneratedBillOption Option { get; }
        public double Progress { get; internal set; }
        public int Weight { get; internal set; }
        public long SchedulerCurrentWeight { get; internal set; }
        public long ActivationOrder { get; }
        public double SavedBaseRewardStrength { get; }

        public ActiveBillState(
            long instanceId,
            GeneratedBillOption option,
            double progress,
            int weight,
            long schedulerCurrentWeight,
            long activationOrder,
            double savedBaseRewardStrength) {
            InstanceId = instanceId;
            Option = option ?? throw new ArgumentNullException(nameof(option));
            Progress = progress;
            Weight = weight;
            SchedulerCurrentWeight = schedulerCurrentWeight;
            ActivationOrder = activationOrder;
            SavedBaseRewardStrength = savedBaseRewardStrength;
        }

        internal ActiveBillState Clone() {
            return new ActiveBillState(
                InstanceId,
                Option,
                Progress,
                Weight,
                SchedulerCurrentWeight,
                ActivationOrder,
                SavedBaseRewardStrength);
        }
    }

    public sealed class BillCompletionRecord {
        public BillRewardDefinition Reward { get; }
        public double SavedBaseRewardStrength { get; }
        public long CompletionOrder { get; }

        public BillCompletionRecord(
            BillRewardDefinition reward,
            double savedBaseRewardStrength,
            long completionOrder) {
            Reward = reward ?? throw new ArgumentNullException(nameof(reward));
            SavedBaseRewardStrength = savedBaseRewardStrength;
            CompletionOrder = completionOrder;
        }
    }
}
