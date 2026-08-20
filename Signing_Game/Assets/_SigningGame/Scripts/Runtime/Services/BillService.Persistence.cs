using System;
using System.Collections.Generic;
using Data.Bills;
using Data.Cache;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Utils;

namespace Services {
    public sealed partial class BillService {
        public JToken Serialize() {
            MaterializePendingContributions(false);
            var catalog = new JArray();
            for (int index = 0; index < _catalog.Count; index++) catalog.Add(SerializeOption(_catalog[index]));

            var active = new JArray();
            for (int index = 0; index < _active.Count; index++) {
                ActiveBillState bill = _active[index];
                active.Add(new JObject {
                    ["instanceId"] = bill.InstanceId,
                    ["option"] = SerializeOption(bill.Option),
                    ["progress"] = bill.Progress,
                    ["weight"] = bill.Weight,
                    ["schedulerCurrentWeight"] = bill.SchedulerCurrentWeight,
                    ["activationOrder"] = bill.ActivationOrder,
                    ["baseRewardStrength"] = bill.SavedBaseRewardStrength,
                    ["paidStored"] = bill.PaidCost.Stored,
                    ["paidDegree"] = bill.PaidCost.Base.Degree,
                    ["elapsedWorkSeconds"] = bill.ElapsedWorkSeconds,
                    ["processedDocumentCount"] = bill.ProcessedDocumentCount,
                    ["hasCompleteWorkStatistics"] = bill.HasCompleteWorkStatistics
                });
            }

            var completed = new JArray();
            for (int index = 0; index < _completed.Count; index++) {
                BillCompletionRecord completion = _completed[index];
                var data = new JObject {
                    ["rewardId"] = completion.Reward.Id,
                    ["baseRewardStrength"] = completion.SavedBaseRewardStrength,
                    ["completionOrder"] = completion.CompletionOrder,
                    ["additionalGeneratedDocuments"] = completion.AdditionalGeneratedDocuments,
                    ["additionalIncomeStored"] = completion.AdditionalIncome.Stored,
                    ["additionalIncomeDegree"] = completion.AdditionalIncome.Base.Degree,
                    ["option"] = completion.Option == null ? JValue.CreateNull() : SerializeOption(completion.Option),
                    ["paidStored"] = completion.PaidCost.Stored,
                    ["paidDegree"] = completion.PaidCost.Base.Degree,
                    ["elapsedWorkSeconds"] = completion.ElapsedWorkSeconds,
                    ["processedDocumentCount"] = completion.ProcessedDocumentCount,
                    ["hasCompleteWorkStatistics"] = completion.HasCompleteWorkStatistics,
                    ["payoutStored"] = completion.ActualCompletionPayout.Stored,
                    ["payoutDegree"] = completion.ActualCompletionPayout.Base.Degree,
                    ["hasCompletionPayout"] = completion.HasCompletionPayout
                };
                completed.Add(data);
            }

            JObject pending = null;
            if (_pending != null) {
                pending = new JObject {
                    ["option"] = SerializeOption(_pending.Option),
                    ["paidStored"] = _pending.PaidCost.Stored,
                    ["paidDegree"] = _pending.PaidCost.Base.Degree
                };
            }

            return new JObject {
                ["requirementsVersion"] = 2,
                ["randomState"] = _random.State,
                ["nextOptionId"] = _nextOptionId,
                ["nextInstanceId"] = _nextInstanceId,
                ["nextActivationOrder"] = _nextActivationOrder,
                ["nextCompletionOrder"] = _nextCompletionOrder,
                ["catalog"] = catalog,
                ["pending"] = pending,
                ["active"] = active,
                ["completed"] = completed
            };
        }

        public void Deserialize(JToken state) {
            if (_isMutating) throw new InvalidOperationException("Cannot restore bills during another bill mutation.");
            RestoreData restored = ParseRestore(state);
            if (!_postInitialized) {
                _deferredRestore = restored;
                return;
            }
            ApplyRestore(restored, true);
        }

        private void ApplyRestore(RestoreData restored, bool notify) {
            BillEntries entries = _billData.Value;
            var random = new BillRandom(restored.RandomState);
            Value refund = Value.Zero;
            var previousCompletionRewards = new HashSet<BillRewardDefinition>();
            for (int index = 0; index < _completed.Count; index++) {
                previousCompletionRewards.Add(_completed[index].Reward);
            }

            var nextCompleted = new List<BillCompletionRecord>();
            var completedOrders = new HashSet<long>();
            for (int index = 0; index < restored.Completed.Count; index++) {
                CompletionRestore data = restored.Completed[index];
                if (!_rewards.TryGetValue(data.RewardId, out BillRewardDefinition reward)) continue;
                if (!completedOrders.Add(data.CompletionOrder)) {
                    throw new JsonSerializationException("Duplicate bill completion order.");
                }
                GeneratedBillOption option = data.Option == null ? null : MaterializeOption(data.Option);
                if (data.Option != null && (option == null || option.Reward != reward)) continue;
                nextCompleted.Add(new BillCompletionRecord(
                    option,
                    reward,
                    data.BaseRewardStrength,
                    data.CompletionOrder,
                    data.PaidCost,
                    data.ElapsedWorkSeconds,
                    data.ProcessedDocumentCount,
                    data.HasCompleteWorkStatistics,
                    data.ActualCompletionPayout,
                    data.HasCompletionPayout,
                    data.AdditionalGeneratedDocuments,
                    data.AdditionalIncome));
            }
            nextCompleted.Sort((left, right) => left.CompletionOrder.CompareTo(right.CompletionOrder));

            var nextActive = new List<ActiveBillState>();
            var activeIds = new HashSet<long>();
            for (int index = 0; index < restored.Active.Count; index++) {
                ActiveRestore data = restored.Active[index];
                GeneratedBillOption option = MaterializeOption(data.Option);
                if (option == null) continue;
                if (!activeIds.Add(data.InstanceId)) {
                    throw new JsonSerializationException("Duplicate active bill instance ID.");
                }
                if (data.Progress >= option.RequiredProgress) {
                    throw new JsonSerializationException(
                        $"Active bill '{data.InstanceId}' already reached its completion target.");
                }
                nextActive.Add(new ActiveBillState(
                    data.InstanceId,
                    option,
                    Math.Min(data.Progress, option.RequiredProgress),
                    Math.Clamp(data.Weight, 1, ResolveMaximumPriorityWeight(entries)),
                    data.SchedulerCurrentWeight,
                    data.ActivationOrder,
                    data.BaseRewardStrength,
                    data.PaidCost,
                    data.ElapsedWorkSeconds,
                    data.ProcessedDocumentCount,
                    data.HasCompleteWorkStatistics));
            }

            PendingBillState nextPending = null;
            if (restored.Pending != null) {
                GeneratedBillOption option = MaterializeOption(restored.Pending.Option);
                if (option == null || ContainsNonRepeatable(nextActive, nextCompleted,
                        restored.Pending.Option.RewardId)) {
                    refund = restored.Pending.PaidCost;
                }
                else {
                    nextPending = new PendingBillState(option, restored.Pending.PaidCost);
                }
            }

            var nextCatalog = new List<GeneratedBillOption>();
            var catalogRewards = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < restored.Catalog.Count; index++) {
                GeneratedBillOption option = MaterializeOption(restored.Catalog[index]);
                if (option == null || !catalogRewards.Add(option.Reward.Id)) continue;
                if (!option.Reward.Repeatable &&
                    ContainsNonRepeatable(nextPending, nextActive, nextCompleted, option.Reward.Id)) continue;
                nextCatalog.Add(option);
            }

            StateView candidate = CreateStateView(nextPending, nextActive, nextCompleted, entries);
            int unlockedQuality = ResolveCandidateUnlockedQuality(nextCompleted, entries);
            ExternalState external = CaptureExternalState(unlockedQuality, candidate.Income);
            int desiredCount = Math.Min(ResolveCatalogSize(entries), CountEligibleRewards(candidate));
            bool suppressed = ShouldSuppressCatalog(candidate, entries);
            bool restoredCatalogValid = suppressed
                ? nextCatalog.Count == 0
                : _rewards.Count == 0
                    ? nextCatalog.Count == 0
                    : nextCatalog.Count == desiredCount &&
                      IsCatalogValidForState(nextCatalog, candidate, entries, external);

            CatalogBuild build;
            if (restoredCatalogValid || suppressed) {
                if (suppressed) nextCatalog.Clear();
                build = new CatalogBuild(nextCatalog, random, restored.NextOptionId, true);
            }
            else if (_rewards.Count == 0) {
                build = new CatalogBuild(new List<GeneratedBillOption>(), random, restored.NextOptionId, true);
            }
            else {
                build = BuildFreshCatalog(candidate, entries, external, random, restored.NextOptionId);
            }

            _isMutating = true;
            bool walletChanged = false;
            try {
                walletChanged = !refund.IsZero && _wallet.ReplenishWallet(refund, false);
                InvalidateClaims();
                _pending = nextPending;
                _active = nextActive;
                _completed = nextCompleted;
                ReplaceCatalog(build);
                _nextInstanceId = restored.NextInstanceId;
                _nextActivationOrder = restored.NextActivationOrder;
                _nextCompletionOrder = restored.NextCompletionOrder;
                InvalidateActiveCaches();
                foreach (BillRewardDefinition reward in previousCompletionRewards) {
                    InvalidateCompletionGroups(reward);
                }
                InvalidateAllCompletionGroups();
                _pendingGeneratedDocumentEquivalents = 0d;
                _pendingCreditedIncome = Value.Zero;
                _contributionDirty = false;
                RebuildAttributionShares();
            }
            finally {
                _isMutating = false;
            }

            if (walletChanged) PublishWalletChanged();
            if (notify) {
                NotifyChanged();
                NotifyDocumentOffersChanged();
            }
        }

        private GeneratedBillOption MaterializeOption(OptionRestore data) {
            if (!_rewards.TryGetValue(data.RewardId, out BillRewardDefinition reward)) return null;
            var requirements = new List<BillRequirementSnapshot>(data.Requirements.Count);
            var kinds = new HashSet<BillRequirementKind>();
            for (int index = 0; index < data.Requirements.Count; index++) {
                RequirementRestore requirement = data.Requirements[index];
                if (!_templates.TryGetValue(requirement.TemplateId, out BillRequirementTemplateDefinition template) ||
                    template.Definition == null || template.Definition.Kind != requirement.Kind ||
                    !kinds.Add(requirement.Kind)) return null;
                BillRequirementSnapshot snapshot = template.Definition switch {
                    OwnedUpgradeRequirementDefinition => new OwnedUpgradeRequirementSnapshot(
                        requirement.TemplateId, requirement.UpgradeId, requirement.Balance),
                    MinimumClerkCountRequirementDefinition => new MinimumClerkCountRequirementSnapshot(
                        requirement.TemplateId, requirement.NumericTarget, requirement.Balance),
                    MinimumDocumentQualityRequirementDefinition => new MinimumDocumentQualityRequirementSnapshot(
                        requirement.TemplateId, requirement.NumericTarget, requirement.Balance),
                    MinimumIncomeRequirementDefinition => new MinimumIncomeRequirementSnapshot(
                        requirement.TemplateId, requirement.IncomeTarget, requirement.Balance),
                    ProcessedDocumentsRequirementDefinition => new ProcessedDocumentsRequirementSnapshot(
                        requirement.TemplateId, requirement.NumericTarget, requirement.Balance),
                    _ => null
                };
                if (snapshot == null) return null;
                requirements.Add(snapshot);
            }
            return new GeneratedBillOption(
                data.OptionId,
                reward,
                requirements,
                data.RawCost,
                data.RequiredProgress,
                data.SignatureThreshold);
        }

        private static bool ContainsNonRepeatable(
            IReadOnlyList<ActiveBillState> active,
            IReadOnlyList<BillCompletionRecord> completed,
            string rewardId) {
            return ContainsNonRepeatable(null, active, completed, rewardId);
        }

        private static bool ContainsNonRepeatable(
            PendingBillState pending,
            IReadOnlyList<ActiveBillState> active,
            IReadOnlyList<BillCompletionRecord> completed,
            string rewardId) {
            if (pending != null && !pending.Option.Reward.Repeatable &&
                string.Equals(pending.Option.Reward.Id, rewardId, StringComparison.Ordinal)) return true;
            for (int index = 0; index < active.Count; index++) {
                if (!active[index].Option.Reward.Repeatable &&
                    string.Equals(active[index].Option.Reward.Id, rewardId, StringComparison.Ordinal)) return true;
            }
            for (int index = 0; index < completed.Count; index++) {
                if (!completed[index].Reward.Repeatable &&
                    string.Equals(completed[index].Reward.Id, rewardId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static JObject SerializeOption(GeneratedBillOption option) {
            var requirements = new JArray();
            for (int index = 0; index < option.Requirements.Count; index++) {
                BillRequirementSnapshot requirement = option.Requirements[index];
                var serialized = new JObject {
                    ["templateId"] = requirement.TemplateId,
                    ["kind"] = (int)requirement.Kind,
                    ["upgradeId"] = requirement.Kind == BillRequirementKind.OwnedUpgrade
                        ? ((OwnedUpgradeRequirementSnapshot)requirement).UpgradeId
                        : JValue.CreateNull(),
                    ["costMultiplier"] = requirement.Balance.CostMultiplier,
                    ["workFactor"] = requirement.Balance.WorkFactor,
                    ["rewardFactor"] = requirement.Balance.RewardFactor,
                    ["difficultyFactor"] = requirement.Balance.DifficultyFactor
                };
                switch (requirement) {
                    case NumericBillRequirementSnapshot numeric:
                        serialized["numericTarget"] = numeric.NumericTarget;
                        break;
                    case MinimumIncomeRequirementSnapshot income:
                        serialized["incomeTargetStored"] = income.IncomeTarget.Stored;
                        serialized["incomeTargetDegree"] = income.IncomeTarget.Base.Degree;
                        break;
                    case OwnedUpgradeRequirementSnapshot:
                        break;
                    default:
                        throw new JsonSerializationException("Unsupported bill requirement snapshot type.");
                }
                requirements.Add(serialized);
            }
            return new JObject {
                ["optionId"] = option.OptionId,
                ["rewardId"] = option.Reward.Id,
                ["rawCostStored"] = option.RawCost.Stored,
                ["rawCostDegree"] = option.RawCost.Base.Degree,
                ["requiredProgress"] = option.RequiredProgress,
                ["signatureThreshold"] = option.SignatureThreshold,
                ["requirements"] = requirements
            };
        }

        private static RestoreData ParseRestore(JToken state) {
            if (state is not JObject root || root["catalog"] is not JArray catalog ||
                root["active"] is not JArray active || root["completed"] is not JArray completed ||
                !TryReadUnsigned(root["randomState"], out ulong randomState) ||
                !TryReadPositiveLong(root["nextOptionId"], out long nextOptionId) ||
                !TryReadPositiveLong(root["nextInstanceId"], out long nextInstanceId) ||
                !TryReadPositiveLong(root["nextActivationOrder"], out long nextActivationOrder) ||
                !TryReadPositiveLong(root["nextCompletionOrder"], out long nextCompletionOrder)) {
                throw new JsonSerializationException("Bill state is missing required arrays or counters.");
            }

            int requirementsVersion = 1;
            JToken requirementsVersionToken = root["requirementsVersion"];
            if (requirementsVersionToken != null) {
                if (requirementsVersionToken.Type != JTokenType.Integer) {
                    throw new JsonSerializationException("Unsupported bill requirement schema version.");
                }
                requirementsVersion = requirementsVersionToken.Value<int>();
                if (requirementsVersion != 1 && requirementsVersion != 2) {
                    throw new JsonSerializationException("Unsupported bill requirement schema version.");
                }
            }

            var restored = new RestoreData(
                randomState,
                nextOptionId,
                nextInstanceId,
                nextActivationOrder,
                nextCompletionOrder);
            var optionIds = new HashSet<long>();
            foreach (JToken token in catalog) {
                OptionRestore option = ParseOption(token, requirementsVersion);
                if (!optionIds.Add(option.OptionId)) throw new JsonSerializationException("Duplicate bill option ID.");
                restored.Catalog.Add(option);
            }

            JToken pendingToken = root["pending"];
            if (pendingToken != null && pendingToken.Type != JTokenType.Null) {
                if (pendingToken is not JObject pendingData ||
                    !TryReadValue(pendingData["paidStored"], pendingData["paidDegree"], false, out Value paid)) {
                    throw new JsonSerializationException("Pending bill state is invalid.");
                }
                OptionRestore option = ParseOption(pendingData["option"], requirementsVersion);
                if (!optionIds.Add(option.OptionId)) throw new JsonSerializationException("Duplicate bill option ID.");
                restored.Pending = new PendingRestore(option, paid);
            }

            var instanceIds = new HashSet<long>();
            foreach (JToken token in active) {
                if (token is not JObject data ||
                    !TryReadPositiveLong(data["instanceId"], out long instanceId) ||
                    !TryReadNumber(data["progress"], out double progress) || progress < 0d ||
                    data["weight"]?.Type != JTokenType.Integer ||
                    !TryReadLong(data["schedulerCurrentWeight"], out long schedulerCurrentWeight) ||
                    !TryReadPositiveLong(data["activationOrder"], out long activationOrder) ||
                    !TryReadNumber(data["baseRewardStrength"], out double strength) || strength <= 0d) {
                    throw new JsonSerializationException("Active bill state is invalid.");
                }
                bool hasAnyWorkField = HasAnyProperty(data,
                    "paidStored", "paidDegree", "elapsedWorkSeconds", "processedDocumentCount",
                    "hasCompleteWorkStatistics");
                Value paidCost = Value.Zero;
                double elapsedWorkSeconds = 0d;
                int processedDocumentCount = 0;
                bool hasCompleteWorkStatistics = false;
                if (hasAnyWorkField &&
                    (!TryReadValue(data["paidStored"], data["paidDegree"], true, out paidCost) ||
                     !TryReadNumber(data["elapsedWorkSeconds"], out elapsedWorkSeconds) ||
                     elapsedWorkSeconds < 0d ||
                     data["processedDocumentCount"]?.Type != JTokenType.Integer ||
                     (processedDocumentCount = data["processedDocumentCount"].Value<int>()) < 0 ||
                     data["hasCompleteWorkStatistics"]?.Type != JTokenType.Boolean)) {
                    throw new JsonSerializationException("Active bill work statistics are incomplete or invalid.");
                }
                if (hasAnyWorkField) {
                    hasCompleteWorkStatistics = data["hasCompleteWorkStatistics"].Value<bool>();
                }
                int weight = data["weight"].Value<int>();
                if (weight < 1 || !instanceIds.Add(instanceId)) {
                    throw new JsonSerializationException("Active bill IDs and weights must be valid and unique.");
                }
                OptionRestore option = ParseOption(data["option"], requirementsVersion);
                if (progress >= option.RequiredProgress) {
                    throw new JsonSerializationException(
                        $"Active bill '{instanceId}' already reached its completion target.");
                }
                if (!optionIds.Add(option.OptionId)) throw new JsonSerializationException("Duplicate bill option ID.");
                restored.Active.Add(new ActiveRestore(
                    instanceId, option, progress, weight, schedulerCurrentWeight,
                    activationOrder, strength, paidCost, elapsedWorkSeconds,
                    processedDocumentCount, hasCompleteWorkStatistics));
            }

            var completionOrders = new HashSet<long>();
            foreach (JToken token in completed) {
                if (token is not JObject data || data["rewardId"]?.Type != JTokenType.String ||
                    !TryReadNumber(data["baseRewardStrength"], out double strength) || strength <= 0d ||
                    !TryReadPositiveLong(data["completionOrder"], out long completionOrder)) {
                    throw new JsonSerializationException("Completed bill state is invalid.");
                }
                string rewardId = data["rewardId"].Value<string>();
                if (string.IsNullOrWhiteSpace(rewardId)) {
                    throw new JsonSerializationException("Completed bill reward ID is empty.");
                }
                if (!completionOrders.Add(completionOrder)) {
                    throw new JsonSerializationException("Duplicate bill completion order.");
                }

                bool hasAnyHistoryField = HasAnyProperty(data,
                    "option", "paidStored", "paidDegree", "elapsedWorkSeconds",
                    "processedDocumentCount", "hasCompleteWorkStatistics", "payoutStored",
                    "payoutDegree", "hasCompletionPayout");
                OptionRestore completionOption = null;
                Value paidCost = Value.Zero;
                double elapsedWorkSeconds = 0d;
                int processedDocumentCount = 0;
                bool hasCompleteWorkStatistics = false;
                Value payout = Value.Zero;
                bool hasCompletionPayout = false;
                if (hasAnyHistoryField) {
                    if (data.Property("option") == null ||
                        !TryReadValue(data["paidStored"], data["paidDegree"], true, out paidCost) ||
                        !TryReadNumber(data["elapsedWorkSeconds"], out elapsedWorkSeconds) ||
                        elapsedWorkSeconds < 0d ||
                        data["processedDocumentCount"]?.Type != JTokenType.Integer ||
                        (processedDocumentCount = data["processedDocumentCount"].Value<int>()) < 0 ||
                        data["hasCompleteWorkStatistics"]?.Type != JTokenType.Boolean ||
                        !TryReadValue(data["payoutStored"], data["payoutDegree"], true, out payout) ||
                        data["hasCompletionPayout"]?.Type != JTokenType.Boolean) {
                        throw new JsonSerializationException(
                            "Completed bill historical statistics are incomplete or invalid.");
                    }
                    if (data["option"]?.Type != JTokenType.Null) {
                        completionOption = ParseOption(data["option"], requirementsVersion);
                        if (!string.Equals(completionOption.RewardId, rewardId, StringComparison.Ordinal) ||
                            !optionIds.Add(completionOption.OptionId)) {
                            throw new JsonSerializationException(
                                "Completed bill option is duplicated or does not match its reward.");
                        }
                    }
                    hasCompleteWorkStatistics = data["hasCompleteWorkStatistics"].Value<bool>();
                    hasCompletionPayout = data["hasCompletionPayout"].Value<bool>();
                }

                bool hasAnyContributionField = HasAnyProperty(data,
                    "additionalGeneratedDocuments", "additionalIncomeStored", "additionalIncomeDegree");
                double additionalGeneratedDocuments = 0d;
                Value additionalIncome = Value.Zero;
                if (hasAnyContributionField &&
                    (!TryReadNumber(data["additionalGeneratedDocuments"], out additionalGeneratedDocuments) ||
                     additionalGeneratedDocuments < 0d ||
                     !TryReadValue(data["additionalIncomeStored"], data["additionalIncomeDegree"], true,
                         out additionalIncome))) {
                    throw new JsonSerializationException(
                        "Completed bill contribution statistics are incomplete or invalid.");
                }

                restored.Completed.Add(new CompletionRestore(
                    rewardId,
                    strength,
                    completionOrder,
                    completionOption,
                    paidCost,
                    elapsedWorkSeconds,
                    processedDocumentCount,
                    hasCompleteWorkStatistics,
                    payout,
                    hasCompletionPayout,
                    additionalGeneratedDocuments,
                    additionalIncome));
            }
            ValidateRestoreCounters(restored);
            return restored;
        }

        private static void ValidateRestoreCounters(RestoreData restored) {
            long maximumOptionId = 0L;
            for (int index = 0; index < restored.Catalog.Count; index++) {
                maximumOptionId = Math.Max(maximumOptionId, restored.Catalog[index].OptionId);
            }
            if (restored.Pending != null) {
                maximumOptionId = Math.Max(maximumOptionId, restored.Pending.Option.OptionId);
            }

            long maximumInstanceId = 0L;
            long maximumActivationOrder = 0L;
            for (int index = 0; index < restored.Active.Count; index++) {
                maximumOptionId = Math.Max(maximumOptionId, restored.Active[index].Option.OptionId);
                maximumInstanceId = Math.Max(maximumInstanceId, restored.Active[index].InstanceId);
                maximumActivationOrder = Math.Max(maximumActivationOrder, restored.Active[index].ActivationOrder);
            }

            long maximumCompletionOrder = 0L;
            for (int index = 0; index < restored.Completed.Count; index++) {
                if (restored.Completed[index].Option != null) {
                    maximumOptionId = Math.Max(maximumOptionId, restored.Completed[index].Option.OptionId);
                }
                maximumCompletionOrder = Math.Max(maximumCompletionOrder, restored.Completed[index].CompletionOrder);
            }

            if (restored.NextOptionId <= maximumOptionId ||
                restored.NextInstanceId <= maximumInstanceId ||
                restored.NextActivationOrder <= maximumActivationOrder ||
                restored.NextCompletionOrder <= maximumCompletionOrder) {
                throw new JsonSerializationException("Bill save counters do not exceed their persisted IDs.");
            }
        }

        private static OptionRestore ParseOption(JToken token, int requirementsVersion) {
            if (token is not JObject data ||
                !TryReadPositiveLong(data["optionId"], out long optionId) ||
                data["rewardId"]?.Type != JTokenType.String ||
                !TryReadValue(data["rawCostStored"], data["rawCostDegree"], false, out Value rawCost) ||
                !TryReadNumber(data["requiredProgress"], out double requiredProgress) || requiredProgress <= 0d ||
                !TryReadNumber(data["signatureThreshold"], out double threshold) || threshold < 0d || threshold > 1d ||
                data["requirements"] is not JArray requirements) {
                throw new JsonSerializationException("Generated bill option is invalid.");
            }
            string rewardId = data["rewardId"].Value<string>();
            if (string.IsNullOrWhiteSpace(rewardId)) throw new JsonSerializationException("Bill reward ID is empty.");
            var result = new OptionRestore(optionId, rewardId, rawCost, requiredProgress, (float)threshold);
            var kinds = new HashSet<BillRequirementKind>();
            foreach (JToken requirementToken in requirements) {
                if (requirementToken is not JObject requirement ||
                    requirement["templateId"]?.Type != JTokenType.String ||
                    requirement["kind"]?.Type != JTokenType.Integer ||
                    !TryReadNumber(requirement["costMultiplier"], out double costMultiplier) || costMultiplier < 1d ||
                    !TryReadNumber(requirement["workFactor"], out double workFactor) || workFactor < 0d ||
                    !TryReadNumber(requirement["rewardFactor"], out double rewardFactor) || rewardFactor < 0d ||
                    !TryReadNumber(requirement["difficultyFactor"], out double difficultyFactor) || difficultyFactor < 0d) {
                    throw new JsonSerializationException("Generated bill requirement is invalid.");
                }
                string templateId = requirement["templateId"].Value<string>();
                var kind = (BillRequirementKind)requirement["kind"].Value<int>();
                if (string.IsNullOrWhiteSpace(templateId) || !Enum.IsDefined(typeof(BillRequirementKind), kind) ||
                    !kinds.Add(kind)) {
                    throw new JsonSerializationException("Bill requirement IDs and kinds must be valid and unique.");
                }
                if (requirementsVersion == 1 && kind > BillRequirementKind.MinimumUnlockedDocumentQuality) {
                    throw new JsonSerializationException("Legacy bill requirements contain an unsupported kind.");
                }

                JToken upgradeToken = requirement["upgradeId"];
                string upgradeId = null;
                if (kind == BillRequirementKind.OwnedUpgrade) {
                    if (upgradeToken?.Type != JTokenType.String ||
                        string.IsNullOrWhiteSpace(upgradeId = upgradeToken.Value<string>())) {
                        throw new JsonSerializationException("Owned-upgrade bill requirement has no upgrade ID.");
                    }
                }
                else {
                    if (requirementsVersion == 2 && requirement.Property("upgradeId") == null) {
                        throw new JsonSerializationException(
                            "Version 2 non-owned bill requirements must contain a null upgrade ID.");
                    }
                    if (upgradeToken != null && upgradeToken.Type != JTokenType.Null) {
                        throw new JsonSerializationException("Non-owned bill requirements must have a null upgrade ID.");
                    }
                }

                int numericTarget = 0;
                Value incomeTarget = Value.Zero;
                if (kind == BillRequirementKind.MinimumIncome) {
                    if (requirementsVersion != 2 ||
                        !TryReadValue(requirement["incomeTargetStored"], requirement["incomeTargetDegree"], false,
                            out incomeTarget)) {
                        throw new JsonSerializationException("Income bill requirement target is invalid.");
                    }
                }
                else if (kind != BillRequirementKind.OwnedUpgrade) {
                    if (requirement["numericTarget"]?.Type != JTokenType.Integer) {
                        throw new JsonSerializationException("Numeric bill requirement target is missing.");
                    }
                    numericTarget = requirement["numericTarget"].Value<int>();
                }

                result.Requirements.Add(new RequirementRestore(
                    templateId,
                    kind,
                    numericTarget,
                    incomeTarget,
                    upgradeId,
                    new BillRequirementBalance {
                        CostMultiplier = costMultiplier,
                        WorkFactor = workFactor,
                        RewardFactor = rewardFactor,
                        DifficultyFactor = difficultyFactor
                    }));
            }
            return result;
        }

        private static bool TryReadValue(
            JToken storedToken,
            JToken degreeToken,
            bool allowZero,
            out Value value) {
            value = default;
            if (!TryReadNumber(storedToken, out double stored) || degreeToken?.Type != JTokenType.Integer) return false;
            int degree = degreeToken.Value<int>();
            if (stored < 0d || stored >= 1000d || degree < 0 || stored == 0d && degree != 0 ||
                !allowZero && stored == 0d || degree > 0 && stored < 1d) return false;
            var candidate = new Value(stored, new BaseValue(degree));
            if (candidate.Stored != stored || candidate.Base.Degree != degree) return false;
            value = candidate;
            return true;
        }

        private static bool HasAnyProperty(JObject data, params string[] names) {
            for (int index = 0; index < names.Length; index++) {
                if (data.Property(names[index]) != null) return true;
            }
            return false;
        }

        private static bool TryReadNumber(JToken token, out double value) {
            if (token?.Type is JTokenType.Integer or JTokenType.Float) {
                value = token.Value<double>();
                return IsFinite(value);
            }
            value = default;
            return false;
        }

        private static bool TryReadPositiveLong(JToken token, out long value) {
            return TryReadLong(token, out value) && value > 0L;
        }

        private static bool TryReadLong(JToken token, out long value) {
            if (token?.Type == JTokenType.Integer) {
                try { value = token.Value<long>(); return true; }
                catch (Exception) { }
            }
            value = default;
            return false;
        }

        private static bool TryReadUnsigned(JToken token, out ulong value) {
            if (token?.Type == JTokenType.Integer) {
                try { value = token.Value<ulong>(); return true; }
                catch (Exception) { }
            }
            value = default;
            return false;
        }

        private sealed class RestoreData {
            public ulong RandomState { get; }
            public long NextOptionId { get; }
            public long NextInstanceId { get; }
            public long NextActivationOrder { get; }
            public long NextCompletionOrder { get; }
            public List<OptionRestore> Catalog { get; } = new();
            public PendingRestore Pending { get; set; }
            public List<ActiveRestore> Active { get; } = new();
            public List<CompletionRestore> Completed { get; } = new();

            public RestoreData(
                ulong randomState,
                long nextOptionId,
                long nextInstanceId,
                long nextActivationOrder,
                long nextCompletionOrder) {
                RandomState = randomState;
                NextOptionId = nextOptionId;
                NextInstanceId = nextInstanceId;
                NextActivationOrder = nextActivationOrder;
                NextCompletionOrder = nextCompletionOrder;
            }
        }

        private sealed class OptionRestore {
            public long OptionId { get; }
            public string RewardId { get; }
            public Value RawCost { get; }
            public double RequiredProgress { get; }
            public float SignatureThreshold { get; }
            public List<RequirementRestore> Requirements { get; } = new();

            public OptionRestore(
                long optionId,
                string rewardId,
                Value rawCost,
                double requiredProgress,
                float signatureThreshold) {
                OptionId = optionId;
                RewardId = rewardId;
                RawCost = rawCost;
                RequiredProgress = requiredProgress;
                SignatureThreshold = signatureThreshold;
            }
        }

        private sealed class PendingRestore {
            public OptionRestore Option { get; }
            public Value PaidCost { get; }
            public PendingRestore(OptionRestore option, Value paidCost) {
                Option = option;
                PaidCost = paidCost;
            }
        }

        private sealed class ActiveRestore {
            public long InstanceId { get; }
            public OptionRestore Option { get; }
            public double Progress { get; }
            public int Weight { get; }
            public long SchedulerCurrentWeight { get; }
            public long ActivationOrder { get; }
            public double BaseRewardStrength { get; }
            public Value PaidCost { get; }
            public double ElapsedWorkSeconds { get; }
            public int ProcessedDocumentCount { get; }
            public bool HasCompleteWorkStatistics { get; }

            public ActiveRestore(
                long instanceId,
                OptionRestore option,
                double progress,
                int weight,
                long schedulerCurrentWeight,
                long activationOrder,
                double baseRewardStrength,
                Value paidCost,
                double elapsedWorkSeconds,
                int processedDocumentCount,
                bool hasCompleteWorkStatistics) {
                InstanceId = instanceId;
                Option = option;
                Progress = progress;
                Weight = weight;
                SchedulerCurrentWeight = schedulerCurrentWeight;
                ActivationOrder = activationOrder;
                BaseRewardStrength = baseRewardStrength;
                PaidCost = paidCost;
                ElapsedWorkSeconds = elapsedWorkSeconds;
                ProcessedDocumentCount = processedDocumentCount;
                HasCompleteWorkStatistics = hasCompleteWorkStatistics;
            }
        }

        private readonly struct RequirementRestore {
            public string TemplateId { get; }
            public BillRequirementKind Kind { get; }
            public int NumericTarget { get; }
            public Value IncomeTarget { get; }
            public string UpgradeId { get; }
            public BillRequirementBalance Balance { get; }

            public RequirementRestore(
                string templateId,
                BillRequirementKind kind,
                int numericTarget,
                Value incomeTarget,
                string upgradeId,
                BillRequirementBalance balance) {
                TemplateId = templateId;
                Kind = kind;
                NumericTarget = numericTarget;
                IncomeTarget = incomeTarget;
                UpgradeId = upgradeId;
                Balance = balance;
            }
        }

        private sealed class CompletionRestore {
            public string RewardId { get; }
            public double BaseRewardStrength { get; }
            public long CompletionOrder { get; }
            public OptionRestore Option { get; }
            public Value PaidCost { get; }
            public double ElapsedWorkSeconds { get; }
            public int ProcessedDocumentCount { get; }
            public bool HasCompleteWorkStatistics { get; }
            public Value ActualCompletionPayout { get; }
            public bool HasCompletionPayout { get; }
            public double AdditionalGeneratedDocuments { get; }
            public Value AdditionalIncome { get; }

            public CompletionRestore(
                string rewardId,
                double baseRewardStrength,
                long completionOrder,
                OptionRestore option,
                Value paidCost,
                double elapsedWorkSeconds,
                int processedDocumentCount,
                bool hasCompleteWorkStatistics,
                Value actualCompletionPayout,
                bool hasCompletionPayout,
                double additionalGeneratedDocuments,
                Value additionalIncome) {
                RewardId = rewardId;
                BaseRewardStrength = baseRewardStrength;
                CompletionOrder = completionOrder;
                Option = option;
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
}
