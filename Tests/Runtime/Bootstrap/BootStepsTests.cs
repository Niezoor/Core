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
        [Serializable] private sealed class AInitializer : BootInitializer { public override void Initialize() { } }
        [Serializable] private sealed class BInitializer : BootInitializer { public override void Initialize() { } }

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
        private readonly List<string> removed = new();
        private BootSettings settings;

        [SetUp]
        public void SetUp()
        {
            added.Clear();
            removed.Clear();
            settings = ScriptableObject.CreateInstance<BootSettings>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(settings);

        private bool Merge(Dictionary<Type, BootStepAttribute> steps) => BootSteps.Merge(settings, steps, added, removed);

        [Test]
        public void AddsEachStepToItsPhaseInOrder()
        {
            var steps = new Dictionary<Type, BootStepAttribute>
            {
                [typeof(LateInitializer)] = new() { Order = 10 },
                [typeof(EarlyInitializer)] = new() { Order = -10 },
                [typeof(SomeService)] = new(),
                [typeof(SomeTask)] = new(),
            };

            Assert.IsTrue(Merge(steps));

            Assert.AreEqual(2, settings.Initializers.Count);
            Assert.IsInstanceOf<EarlyInitializer>(settings.Initializers[0]);
            Assert.IsInstanceOf<LateInitializer>(settings.Initializers[1]);
            Assert.IsInstanceOf<SomeService>(settings.Services[0]);
            Assert.IsInstanceOf<SomeTask>(settings.SplashTasks[0]);
            Assert.AreEqual(4, added.Count);
        }

        [Test]
        public void EqualOrdersGoByTypeName()
        {
            var steps = new Dictionary<Type, BootStepAttribute>
            {
                [typeof(BInitializer)] = new(),
                [typeof(AInitializer)] = new(),
            };

            Merge(steps);

            Assert.IsInstanceOf<AInitializer>(settings.Initializers[0]);
            Assert.IsInstanceOf<BInitializer>(settings.Initializers[1]);
        }

        [Test]
        public void ReusesTheInstanceAndChangesNothingTheSecondTime()
        {
            var steps = new Dictionary<Type, BootStepAttribute> { [typeof(EarlyInitializer)] = new() };
            Merge(steps);
            var instance = settings.Initializers[0];
            added.Clear();

            Assert.IsFalse(Merge(steps));

            Assert.AreSame(instance, settings.Initializers[0]);
            CollectionAssert.IsEmpty(added);
        }

        [Test]
        public void ConfigurationComesFromTheAttribute()
        {
            settings.Initializers.Add(new EarlyInitializer { Enabled = false });
            settings.SplashTasks.Add(new SomeTask());
            var steps = new Dictionary<Type, BootStepAttribute>
            {
                [typeof(EarlyInitializer)] = new() { Platforms = BootPlatforms.Mobile },
                [typeof(SomeTask)] = new() { Required = false, Timeout = 5f },
            };

            Assert.IsTrue(Merge(steps));

            Assert.IsTrue(settings.Initializers[0].Enabled);
            Assert.AreEqual(BootPlatforms.Mobile, settings.Initializers[0].Platforms);
            Assert.IsFalse(settings.SplashTasks[0].Required);
            Assert.AreEqual(5f, settings.SplashTasks[0].Timeout);
        }

        [Test]
        public void StepsWithoutTheAttributeAndEmptyEntriesAreRemoved()
        {
            settings.Initializers.Add(new AInitializer());
            settings.Initializers.Add(null);
            settings.Initializers.Add(new EarlyInitializer());
            settings.Initializers.Add(new EarlyInitializer());
            var steps = new Dictionary<Type, BootStepAttribute> { [typeof(EarlyInitializer)] = new() };

            Assert.IsTrue(Merge(steps));

            Assert.AreEqual(1, settings.Initializers.Count);
            Assert.IsInstanceOf<EarlyInitializer>(settings.Initializers[0]);
            CollectionAssert.AreEquivalent(new[] { nameof(AInitializer), nameof(EarlyInitializer) }, removed);
        }

        [Test]
        public void StepThatCantBeCreatedIsSkippedWithAWarning()
        {
            var steps = new Dictionary<Type, BootStepAttribute> { [typeof(NoDefaultConstructorInitializer)] = new() };

            LogAssert.Expect(LogType.Warning, new Regex("NoDefaultConstructorInitializer.*parameterless constructor"));
            Assert.IsFalse(Merge(steps));

            CollectionAssert.IsEmpty(settings.Initializers);
        }
    }
}
#endif
