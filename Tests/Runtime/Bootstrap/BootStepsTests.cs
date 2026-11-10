#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.Bootstrap.Tests
{
    /// <summary>
    /// <see cref="BootSteps.Merge"/> with types handed in by the test, so the classes here never reach a real
    /// project's BootSettings (they carry no <see cref="BootStepAttribute"/>, and test assemblies are not searched).
    /// </summary>
    public class BootStepsTests
    {
        [Serializable] private sealed class EarlyInitializer : BootInitializer { public override void Initialize() { } }
        [Serializable] private sealed class LateInitializer : BootInitializer { public override void Initialize() { } }
        [Serializable] private sealed class ManualInitializer : BootInitializer { public override void Initialize() { } }
        [Serializable] private sealed class PlainInitializer : BootInitializer { public override void Initialize() { } }
        [Serializable] private sealed class OtherPlainInitializer : BootInitializer { public override void Initialize() { } }

        [Serializable]
        private sealed class NoDefaultConstructorInitializer : BootInitializer
        {
            public NoDefaultConstructorInitializer(int _) { }
            public override void Initialize() { }
        }

        [Serializable]
        private sealed class SomeService : BootService
        {
            public override void Start(CancellationToken appLifetime) { }
        }

        [Serializable]
        private sealed class SomeTask : SplashTask
        {
            public override Awaitable RunAsync(SplashContext context, CancellationToken cancellationToken) => null;
        }

        private readonly List<string> added = new();
        private BootSettings settings;

        [SetUp]
        public void SetUp()
        {
            added.Clear();
            settings = ScriptableObject.CreateInstance<BootSettings>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(settings);

        private static BootStepAttribute Step(int order, bool autoAdd = true) => new() { Order = order, AutoAdd = autoAdd };

        [Test]
        public void AddsEachStepToItsPhaseInOrder()
        {
            var steps = new Dictionary<Type, BootStepAttribute>
            {
                [typeof(LateInitializer)] = Step(10),
                [typeof(EarlyInitializer)] = Step(-10),
                [typeof(SomeService)] = Step(0),
                [typeof(SomeTask)] = Step(0),
            };

            Assert.IsTrue(BootSteps.Merge(settings, steps, added));

            Assert.AreEqual(2, settings.Initializers.Count);
            Assert.IsInstanceOf<EarlyInitializer>(settings.Initializers[0]);
            Assert.IsInstanceOf<LateInitializer>(settings.Initializers[1]);
            Assert.IsInstanceOf<SomeService>(settings.Services[0]);
            Assert.IsInstanceOf<SomeTask>(settings.SplashTasks[0]);
            Assert.AreEqual(4, added.Count);
        }

        [Test]
        public void KeepsTheProjectsInstanceAndChangesNothingTheSecondTime()
        {
            var existing = new EarlyInitializer { Enabled = false };
            settings.Initializers.Add(existing);
            var steps = new Dictionary<Type, BootStepAttribute> { [typeof(EarlyInitializer)] = Step(0) };

            Assert.IsFalse(BootSteps.Merge(settings, steps, added));

            Assert.AreEqual(1, settings.Initializers.Count);
            Assert.AreSame(existing, settings.Initializers[0]);
            Assert.IsFalse(existing.Enabled);
            CollectionAssert.IsEmpty(added);
        }

        [Test]
        public void StepNotAutoAddedIsOnlySorted()
        {
            var plain = new PlainInitializer();
            var manual = new ManualInitializer();
            settings.Initializers.Add(plain);
            settings.Initializers.Add(manual);
            var steps = new Dictionary<Type, BootStepAttribute>
            {
                [typeof(ManualInitializer)] = Step(-5, autoAdd: false),
                [typeof(EarlyInitializer)] = Step(-10),
            };

            Assert.IsTrue(BootSteps.Merge(settings, steps, added));

            Assert.AreEqual(3, settings.Initializers.Count);
            Assert.IsInstanceOf<EarlyInitializer>(settings.Initializers[0]);
            Assert.AreSame(manual, settings.Initializers[1]);
            Assert.AreSame(plain, settings.Initializers[2]);
        }

        [Test]
        public void EqualOrdersKeepTheProjectsOrder()
        {
            var second = new OtherPlainInitializer();
            var first = new PlainInitializer();
            settings.Initializers.Add(second);
            settings.Initializers.Add(null);
            settings.Initializers.Add(first);

            Assert.IsFalse(BootSteps.Merge(settings, new Dictionary<Type, BootStepAttribute>(), added));

            CollectionAssert.AreEqual(new BootInitializer[] { second, null, first }, settings.Initializers);
        }

        [Test]
        public void StepThatCantBeCreatedIsSkippedWithAWarning()
        {
            var steps = new Dictionary<Type, BootStepAttribute> { [typeof(NoDefaultConstructorInitializer)] = Step(0) };

            LogAssert.Expect(LogType.Warning, new Regex("NoDefaultConstructorInitializer.*parameterless constructor"));
            Assert.IsFalse(BootSteps.Merge(settings, steps, added));

            CollectionAssert.IsEmpty(settings.Initializers);
        }
    }
}
#endif
