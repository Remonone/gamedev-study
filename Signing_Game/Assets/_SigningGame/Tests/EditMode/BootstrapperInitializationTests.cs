using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Bootstrap;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Services;
using Services.Locator;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode {
    public sealed class BootstrapperInitializationTests {
        private GameObject _gameObject;

        [TearDown]
        public void TearDown() {
            if (_gameObject != null) UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void Bootstrapper_ReportsInstallerFailureAsCompletedError() {
            TestBootstrapper bootstrapper = CreateBootstrapper(out ThrowingInstaller installer);
            installer.ThrowDuringInstall = true;
            LogAssert.Expect(LogType.Exception, new Regex("installer failure"));

            bootstrapper.BootstrapOnDemand().GetAwaiter().GetResult();

            Assert.That(bootstrapper.Container.IsInitializationComplete, Is.True);
            Assert.That(bootstrapper.Container.IsReady, Is.False);
            Assert.That(bootstrapper.Container.InitializationException, Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Bootstrapper_ReportsServiceFailureAsCompletedError() {
            TestBootstrapper bootstrapper = CreateBootstrapper(out ThrowingInstaller installer);
            installer.RegisterThrowingService = true;
            LogAssert.Expect(LogType.Exception, new Regex("service failure"));

            bootstrapper.BootstrapOnDemand().GetAwaiter().GetResult();

            Assert.That(bootstrapper.Container.IsInitializationComplete, Is.True);
            Assert.That(bootstrapper.Container.IsReady, Is.False);
            Assert.That(bootstrapper.Container.InitializationException, Is.TypeOf<InvalidOperationException>());
        }

        private TestBootstrapper CreateBootstrapper(out ThrowingInstaller installer) {
            _gameObject = new GameObject("Bootstrapper Test");
            TestBootstrapper bootstrapper = _gameObject.AddComponent<TestBootstrapper>();
            installer = _gameObject.AddComponent<ThrowingInstaller>();
            FieldInfo installers = typeof(Bootstrapper).GetField("_installers", BindingFlags.Instance | BindingFlags.NonPublic);
            installers.SetValue(bootstrapper, new List<MonoInstaller> { installer });
            return bootstrapper;
        }
    }

    public sealed class TestBootstrapper : Bootstrapper {
        protected override void Configure() { }
    }

    public sealed class ThrowingInstaller : MonoInstaller {
        public bool ThrowDuringInstall { get; set; }
        public bool RegisterThrowingService { get; set; }

        public override void Install(ServiceLocator container) {
            if (ThrowDuringInstall) throw new InvalidOperationException("installer failure");
            if (RegisterThrowingService) container.Register(new ThrowingInitializeService());
            else container.Register(new EmptyService());
        }
    }

    internal sealed class ThrowingInitializeService : IService, IPreInitialize {
        public UniTask PreInitializeAsync(IServiceScope scope) => throw new InvalidOperationException("service failure");
        public void Dispose() { }
    }

    internal sealed class EmptyService : IService {
        public void Dispose() { }
    }
}
