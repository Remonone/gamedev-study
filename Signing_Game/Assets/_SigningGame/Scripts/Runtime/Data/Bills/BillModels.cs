using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using Utils;

namespace Data.Bills {
    [Flags]
    public enum BillPurchaseBlockerKind {
        None = 0,
        PendingSignature = 1 << 0,
        ActiveLimitReached = 1 << 1,
        RequirementNotMet = 1 << 2,
        InsufficientFunds = 1 << 3,
        Unavailable = 1 << 4
    }

    public readonly struct BillPurchaseBlocker {
        public BillPurchaseBlockerKind Kind { get; }
        public BillRequirementSnapshot Requirement { get; }
        public Value MissingFunds { get; }

        public BillPurchaseBlocker(
            BillPurchaseBlockerKind kind,
            BillRequirementSnapshot requirement = null,
            Value missingFunds = default) {
            Kind = kind;
            Requirement = requirement;
            MissingFunds = missingFunds;
        }
    }

    public readonly struct BillRequirementPresentationInfo {
        public string DisplayName { get; }
        public string ShortDescription { get; }
        public Color Color { get; }

        public BillRequirementPresentationInfo(string displayName, string shortDescription, Color color) {
            DisplayName = displayName ?? string.Empty;
            ShortDescription = shortDescription ?? string.Empty;
            Color = color;
        }
    }

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
        public Value PaidCost { get; }
        public double ElapsedWorkSeconds { get; internal set; }
        public int ProcessedDocumentCount { get; internal set; }
        public bool HasCompleteWorkStatistics { get; }

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
            PaidCost = Value.Zero;
            HasCompleteWorkStatistics = false;
        }

        public ActiveBillState(
            long instanceId,
            GeneratedBillOption option,
            double progress,
            int weight,
            long schedulerCurrentWeight,
            long activationOrder,
            double savedBaseRewardStrength,
            Value paidCost,
            double elapsedWorkSeconds,
            int processedDocumentCount,
            bool hasCompleteWorkStatistics) {
            InstanceId = instanceId;
            Option = option ?? throw new ArgumentNullException(nameof(option));
            Progress = progress;
            Weight = weight;
            SchedulerCurrentWeight = schedulerCurrentWeight;
            ActivationOrder = activationOrder;
            SavedBaseRewardStrength = savedBaseRewardStrength;
            PaidCost = paidCost;
            ElapsedWorkSeconds = elapsedWorkSeconds;
            ProcessedDocumentCount = processedDocumentCount;
            HasCompleteWorkStatistics = hasCompleteWorkStatistics;
        }

        internal ActiveBillState Clone() {
            return new ActiveBillState(
                InstanceId,
                Option,
                Progress,
                Weight,
                SchedulerCurrentWeight,
                ActivationOrder,
                SavedBaseRewardStrength,
                PaidCost,
                ElapsedWorkSeconds,
                ProcessedDocumentCount,
                HasCompleteWorkStatistics);
        }
    }

    public sealed class BillCompletionRecord {
        public GeneratedBillOption Option { get; }
        public BillRewardDefinition Reward { get; }
        public double SavedBaseRewardStrength { get; }
        public long CompletionOrder { get; }
        public Value PaidCost { get; }
        public double ElapsedWorkSeconds { get; }
        public int ProcessedDocumentCount { get; }
        public bool HasCompleteWorkStatistics { get; }
        public Value ActualCompletionPayout { get; }
        public bool HasCompletionPayout { get; }
        public double AdditionalGeneratedDocuments { get; internal set; }
        public Value AdditionalIncome { get; internal set; }

        public BillCompletionRecord(
            BillRewardDefinition reward,
            double savedBaseRewardStrength,
            long completionOrder) {
            Reward = reward ?? throw new ArgumentNullException(nameof(reward));
            SavedBaseRewardStrength = savedBaseRewardStrength;
            CompletionOrder = completionOrder;
            HasCompleteWorkStatistics = false;
            HasCompletionPayout = false;
        }

        public BillCompletionRecord(
            GeneratedBillOption option,
            BillRewardDefinition reward,
            double savedBaseRewardStrength,
            long completionOrder,
            Value paidCost,
            double elapsedWorkSeconds,
            int processedDocumentCount,
            bool hasCompleteWorkStatistics,
            Value actualCompletionPayout,
            bool hasCompletionPayout,
            double additionalGeneratedDocuments,
            Value additionalIncome) {
            Option = option;
            Reward = reward ?? option?.Reward ?? throw new ArgumentNullException(nameof(reward));
            SavedBaseRewardStrength = savedBaseRewardStrength;
            CompletionOrder = completionOrder;
            PaidCost = paidCost;
            ElapsedWorkSeconds = elapsedWorkSeconds;
            ProcessedDocumentCount = processedDocumentCount;
            HasCompleteWorkStatistics = hasCompleteWorkStatistics;
            ActualCompletionPayout = actualCompletionPayout;
            HasCompletionPayout = hasCompletionPayout;
            AdditionalGeneratedDocuments = additionalGeneratedDocuments;
            AdditionalIncome = additionalIncome;
        }
    }
}
