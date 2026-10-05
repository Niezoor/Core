using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.Bootstrap.Tests
{
    /// <summary>
    /// <see cref="Boot"/> driven by hand: settings built in code, the splash started without a scene and with no
    /// first scene, so finishing it just returns to <see cref="SplashState.None"/>.
    /// </summary>
    public class BootTests
    {
        private sealed class RecordingInitializer : BootInitializer
        {
            private readonly List<string> log;
            private readonly string name;
            private readonly Action action;

            public RecordingInitializer(List<string> log, string name, Action action = null)
            {
                this.log = log;
                this.name = name;
                this.action = action;
            }

            public override string DisplayName => name;

            public override void Initialize()
            {
                log.Add(name);
                action?.Invoke();
            }
        }

        private sealed class RecordingService : BootService
        {
            private readonly List<string> log;
            public CancellationToken Lifetime;

            public RecordingService(List<string> log) => this.log = log;

            public override void Start(CancellationToken appLifetime)
            {
                log.Add("service");
                Lifetime = appLifetime;
            }
        }

        private sealed class TestTask : SplashTask
        {
            public int Runs;
            public int FramesToRun;
            public bool Fail;
            public bool NeverEnds;
            private readonly List<string> log;
            private readonly string name;

            public TestTask(List<string> log, string name)
            {
                this.log = log;
                this.name = name;
            }

            public override string DisplayName => name;

            public override async Awaitable RunAsync(SplashContext context, CancellationToken cancellationToken)
            {
                Runs++;
                log.Add(name);
                for (var frame = 0; NeverEnds || frame < FramesToRun; frame++)
                {
                    if (FramesToRun > 0) context.ReportProgress((float)frame / FramesToRun);
                    await Awaitable.NextFrameAsync(cancellationToken);
                }

                if (Fail) throw new InvalidOperationException($"{name} broke");
            }
        }

        private readonly List<string> log = new();
        private BootSettings settings;

        [SetUp]
        public void SetUp()
        {
            log.Clear();
            settings = ScriptableObject.CreateInstance<BootSettings>();
            settings.MinSplashDuration = 0f;
            settings.InitializerBudgetMs = 10000f;
            Boot.ResetForTests(null);
        }

        [TearDown]
        public void TearDown()
        {
            Boot.ResetForTests(null);
            UnityEngine.Object.DestroyImmediate(settings);
        }

        private TestTask AddTask(string name, int frames = 1)
        {
            var task = new TestTask(log, name) { FramesToRun = frames };
            settings.SplashTasks.Add(task);
            return task;
        }

        private void StartSplash()
        {
            Boot.ResetForTests(settings);
            Boot.StartSplash(-1);
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeout = 5f)
        {
            var deadline = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("timed out");
                yield return null;
            }
        }

        [Test]
        public void InitializersRunInOrderThenServicesStart()
        {
            settings.Initializers.Add(new RecordingInitializer(log, "a"));
            settings.Initializers.Add(new RecordingInitializer(log, "b"));
            var service = new RecordingService(log);
            settings.Services.Add(service);

            Boot.ResetForTests(settings);

            CollectionAssert.AreEqual(new[] { "a", "b", "service" }, log);
            Assert.IsTrue(Boot.IsInitialized);
            Assert.AreEqual(Application.exitCancellationToken, service.Lifetime);
        }

        [Test]
        public void InitializeRunsOnce()
        {
            settings.Initializers.Add(new RecordingInitializer(log, "a"));
            Boot.ResetForTests(settings);
            Boot.Initialize(settings);

            CollectionAssert.AreEqual(new[] { "a" }, log);
        }

        [Test]
        public void DisabledAndOtherPlatformEntriesAreSkipped()
        {
            settings.Initializers.Add(new RecordingInitializer(log, "off") { Enabled = false });
            settings.Initializers.Add(new RecordingInitializer(log, "elsewhere") { Platforms = BootPlatforms.None });
            settings.Initializers.Add(new RecordingInitializer(log, "here") { Platforms = BootPlatformsExtensions.Current });
            settings.Initializers.Add(null);

            Boot.ResetForTests(settings);

            CollectionAssert.AreEqual(new[] { "here" }, log);
        }

        [Test]
        public void ThrowingInitializerIsLoggedAndTheRestStillRun()
        {
            settings.Initializers.Add(new RecordingInitializer(log, "a", () => throw new InvalidOperationException("boom")));
            settings.Initializers.Add(new RecordingInitializer(log, "b"));

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            Boot.ResetForTests(settings);

            CollectionAssert.AreEqual(new[] { "a", "b" }, log);
        }

        [Test]
        public void InitializersOverBudgetWarn()
        {
            settings.InitializerBudgetMs = 0f;
            settings.Initializers.Add(new RecordingInitializer(log, "slow", () => Thread.Sleep(2)));

            LogAssert.Expect(LogType.Warning, new Regex(@"\[Boot\] Initializers took [\s\S]*slow"));
            Boot.ResetForTests(settings);
        }

        [UnityTest]
        public IEnumerator SplashRunsTasksInOrderAndLeaves()
        {
            AddTask("a", 2);
            AddTask("b", 2);
            var states = new List<SplashState>();
            Boot.ResetForTests(settings);
            Boot.SplashStateChanged += states.Add;
            Boot.StartSplash(-1);

            Assert.AreEqual(SplashState.Loading, Boot.SplashState);
            yield return WaitFor(() => Boot.SplashState == SplashState.None);

            CollectionAssert.AreEqual(new[] { "a", "b" }, log);
            CollectionAssert.AreEqual(new[] { SplashState.Loading, SplashState.Ready, SplashState.None }, states);
            Assert.AreEqual(1f, Boot.SplashProgress);
        }

        [UnityTest]
        public IEnumerator ProgressGrowsAcrossTasks()
        {
            AddTask("a", 4);
            AddTask("b", 4);
            var progress = new List<float>();
            Boot.ResetForTests(settings);
            Boot.SplashProgressChanged += progress.Add;
            Boot.StartSplash(-1);

            yield return WaitFor(() => Boot.SplashState == SplashState.None);

            Assert.Greater(progress.Count, 3);
            for (var i = 1; i < progress.Count; i++) Assert.GreaterOrEqual(progress[i], progress[i - 1]);
            Assert.AreEqual(1f, progress[^1]);
        }

        [UnityTest]
        public IEnumerator WithoutAutoContinueTheSplashWaitsForContinue()
        {
            settings.AutoContinue = false;
            AddTask("a");
            StartSplash();

            yield return WaitFor(() => Boot.SplashState == SplashState.Ready);
            for (var i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(SplashState.Ready, Boot.SplashState);

            Boot.Continue();
            yield return WaitFor(() => Boot.SplashState == SplashState.None);
        }

        [UnityTest]
        public IEnumerator MinDurationHoldsTheSplashUnlessContinued()
        {
            settings.MinSplashDuration = 60f;
            AddTask("a");
            StartSplash();

            yield return WaitFor(() => Boot.SplashState == SplashState.Ready);
            yield return null;
            Assert.AreEqual(SplashState.Ready, Boot.SplashState);

            Boot.Continue();
            yield return WaitFor(() => Boot.SplashState == SplashState.None, 1f);
        }

        [UnityTest]
        public IEnumerator ContinueWhileLoadingLeavesAsSoonAsReady()
        {
            settings.MinSplashDuration = 60f;
            AddTask("a", 5);
            StartSplash();

            Boot.Continue();
            yield return WaitFor(() => Boot.SplashState == SplashState.None, 1f);
            Assert.AreEqual(1, log.Count);
        }

        [UnityTest]
        public IEnumerator RequiredFailureStopsAndRetryResumesFromIt()
        {
            var first = AddTask("a");
            var broken = AddTask("b");
            broken.Fail = true;
            var last = AddTask("c");
            StartSplash();

            LogAssert.Expect(LogType.Error, new Regex(@"\[Boot\] b failed"));
            yield return WaitFor(() => Boot.SplashState == SplashState.Failed);
            Assert.AreSame(broken, Boot.FailedTask);
            Assert.IsInstanceOf<InvalidOperationException>(Boot.Failure);
            Assert.AreEqual(0, last.Runs);

            broken.Fail = false;
            Boot.Retry();
            yield return WaitFor(() => Boot.SplashState == SplashState.None);

            Assert.AreEqual(1, first.Runs);
            Assert.AreEqual(2, broken.Runs);
            Assert.AreEqual(1, last.Runs);
            Assert.IsNull(Boot.FailedTask);
        }

        [UnityTest]
        public IEnumerator OptionalFailureIsSkipped()
        {
            var broken = AddTask("a");
            broken.Fail = true;
            broken.Required = false;
            var next = AddTask("b");
            StartSplash();

            LogAssert.Expect(LogType.Warning, new Regex(@"\[Boot\] a failed, going on"));
            yield return WaitFor(() => Boot.SplashState == SplashState.None);
            Assert.AreEqual(1, next.Runs);
        }

        [UnityTest]
        public IEnumerator TimeoutFailsTheTask()
        {
            var stuck = AddTask("stuck");
            stuck.NeverEnds = true;
            stuck.Timeout = 0.2f;
            StartSplash();

            LogAssert.Expect(LogType.Error, new Regex(@"\[Boot\] stuck failed: System.TimeoutException"));
            yield return WaitFor(() => Boot.SplashState == SplashState.Failed);
            Assert.IsInstanceOf<TimeoutException>(Boot.Failure);
        }

        [UnityTest]
        public IEnumerator InactiveSplashTasksAreSkipped()
        {
            AddTask("off").Enabled = false;
            AddTask("elsewhere").Platforms = BootPlatforms.None;
            AddTask("on");
            StartSplash();

            yield return WaitFor(() => Boot.SplashState == SplashState.None);
            CollectionAssert.AreEqual(new[] { "on" }, log);
        }

        [UnityTest]
        public IEnumerator SplashLowersLoadingPriorityAndRestoresIt()
        {
            var before = Application.backgroundLoadingPriority;
            AddTask("a", 3);
            StartSplash();

            Assert.AreEqual(UnityEngine.ThreadPriority.Low, Application.backgroundLoadingPriority);
            yield return WaitFor(() => Boot.SplashState == SplashState.None);
            Assert.AreEqual(before, Application.backgroundLoadingPriority);
        }

        [UnityTest]
        public IEnumerator BootSceneMarkerStartsTheSplash()
        {
            AddTask("a");
            Boot.ResetForTests(settings);

            var host = new GameObject("[Boot]");
            try
            {
                host.AddComponent<BootScene>();
                Assert.AreEqual(SplashState.Loading, Boot.SplashState);
                yield return WaitFor(() => Boot.SplashState == SplashState.None);
                CollectionAssert.AreEqual(new[] { "a" }, log);
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }
        }

        [Test]
        public void BootSceneWithoutSettingsOnlyWarns()
        {
            var host = new GameObject("[Boot]");
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex(@"\[Boot\] The scene has a BootScene but the project has no BootSettings"));
                host.AddComponent<BootScene>();
                Assert.AreEqual(SplashState.None, Boot.SplashState);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void InitializersAndServicesAreRecorded()
        {
            settings.Initializers.Add(new RecordingInitializer(log, "ok"));
            settings.Initializers.Add(new RecordingInitializer(log, "broken", () => throw new InvalidOperationException("boom")));
            settings.Services.Add(new RecordingService(log));

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            Boot.ResetForTests(settings);

            Assert.AreEqual(3, Boot.Records.Count);
            Assert.AreEqual(BootPhase.Initializer, Boot.Records[0].Phase);
            Assert.AreEqual(BootStepResult.Succeeded, Boot.Records[0].Result);
            Assert.AreEqual("broken", Boot.Records[1].Name);
            Assert.AreEqual(BootStepResult.Failed, Boot.Records[1].Result);
            Assert.AreEqual("boom", Boot.Records[1].Error);
            Assert.AreEqual(BootPhase.Service, Boot.Records[2].Phase);
            Assert.IsTrue(Boot.Records.All(r => r.Duration >= 0.0));
        }

        [UnityTest]
        public IEnumerator SplashStepsAndMilestonesAreRecorded()
        {
            AddTask("a", 2);
            var optional = AddTask("optional");
            optional.Fail = true;
            optional.Required = false;
            var broken = AddTask("broken");
            broken.Fail = true;
            StartSplash();

            Assert.AreEqual(BootStepResult.Running, Boot.Records.Single(r => r.Name == "a").Result);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Boot\] optional failed, going on"));
            LogAssert.Expect(LogType.Error, new Regex(@"\[Boot\] broken failed"));
            yield return WaitFor(() => Boot.SplashState == SplashState.Failed);

            Assert.AreEqual(BootStepResult.Succeeded, Boot.Records.Single(r => r.Name == "a").Result);
            Assert.AreEqual(BootStepResult.Skipped, Boot.Records.Single(r => r.Name == "optional").Result);
            Assert.AreEqual(BootStepResult.Failed, Boot.Records.Single(r => r.Name == "broken").Result);
            Assert.IsTrue(double.IsNaN(Boot.SplashReadyAt));

            broken.Fail = false;
            Boot.Retry();
            yield return WaitFor(() => Boot.SplashState == SplashState.None);

            var brokenRuns = Boot.Records.Where(r => r.Name == "broken").Select(r => r.Result).ToArray();
            CollectionAssert.AreEqual(new[] { BootStepResult.Failed, BootStepResult.Succeeded }, brokenRuns);
            Assert.LessOrEqual(Boot.SplashStartedAt, Boot.SplashReadyAt);
            Assert.LessOrEqual(Boot.SplashReadyAt, Boot.SplashLeftAt);
        }

        [UnityTest]
        public IEnumerator TimedOutTaskIsRecordedAsTimedOut()
        {
            var stuck = AddTask("stuck");
            stuck.NeverEnds = true;
            stuck.Timeout = 0.1f;
            StartSplash();

            LogAssert.Expect(LogType.Error, new Regex(@"\[Boot\] stuck failed"));
            yield return WaitFor(() => Boot.SplashState == SplashState.Failed);
            Assert.AreEqual(BootStepResult.TimedOut, Boot.Records.Single(r => r.Name == "stuck").Result);
        }

        [UnityTest]
        public IEnumerator StartAndFinishAreLogged()
        {
            AddTask("a");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Boot\] Starting at"));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Boot\] Initialized in [\d.,]+ ms"));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Boot\] Splash loaded in [\d.,]+ s:[\s\S]*a: "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Boot\] Finished at [\d.,]+ s since launch"));
            StartSplash();

            yield return WaitFor(() => Boot.SplashState == SplashState.None);
        }

        [Test]
        public void ContinueAndRetryOutsideTheSplashDoNothing()
        {
            Boot.ResetForTests(settings);
            Boot.Continue();
            Boot.Retry();

            Assert.AreEqual(SplashState.None, Boot.SplashState);
        }
    }
}
