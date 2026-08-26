using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Presentation;
using R3;
using Services;
using Utils;

namespace Tests.EditMode {
    public sealed class WalletServiceTests {
        [Test]
        public void ReplenishWallet_EmitsCreditDelta() {
            var wallet = new WalletService();
            var credited = new List<Value>();
            using IDisposable creditedSubscription = wallet.Credited.Subscribe(credited.Add);

            Assert.That(wallet.ReplenishWallet(new Value(25d)), Is.True);
            Assert.That(credited, Is.EqualTo(new[] { new Value(25d) }));

            wallet.Dispose();
        }

        [Test]
        public void ZeroAndInsignificantCredits_EmitNoCredit() {
            var wallet = new WalletService();
            wallet.ReplenishWallet(Value.FromLog10(15d));
            var credited = new List<Value>();
            using IDisposable creditedSubscription = wallet.Credited.Subscribe(credited.Add);

            Assert.That(wallet.ReplenishWallet(Value.Zero), Is.False);
            Assert.That(wallet.ReplenishWallet(Value.One), Is.False);
            Assert.That(credited, Is.Empty);

            wallet.Dispose();
        }

        [Test]
        public void Deserialize_EmitsNoCredit() {
            var wallet = new WalletService();
            var credited = new List<Value>();
            using IDisposable creditedSubscription = wallet.Credited.Subscribe(credited.Add);

            wallet.Deserialize(new JObject { ["stored"] = 100d, ["degree"] = 0 });

            Assert.That(credited, Is.Empty);
            wallet.Dispose();
        }

        [Test]
        public void WalletViewModel_ProxiesCreditsAndIgnoresDeserialize() {
            var wallet = new WalletService();
            using var viewModel = new WalletViewModel(wallet);
            var credited = new List<Value>();
            using IDisposable subscription = viewModel.Credited.Subscribe(credited.Add);

            Assert.That(wallet.ReplenishWallet(new Value(25d)), Is.True);
            wallet.Deserialize(new JObject { ["stored"] = 100d, ["degree"] = 0 });

            Assert.That(credited, Is.EqualTo(new[] { new Value(25d) }));
            wallet.Dispose();
        }
    }
}
