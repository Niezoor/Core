using UnityEngine;

namespace Core.Bootstrap
{
    public enum BootPhase
    {
        Initializer,
        Service,
        Splash,
    }

    public enum BootStepResult
    {
        Running,
        Succeeded,
        /// <summary>A required splash task, or an initializer or service that threw.</summary>
        Failed,
        /// <summary>An optional splash task that failed; the splash went on without it.</summary>
        Skipped,
        TimedOut,
        /// <summary>The splash stopped before the task finished (restart, quit, another scene loaded).</summary>
        Cancelled,
    }

    /// <summary>One run of one boot step, for the Boot Monitor and for diagnostics in a build.</summary>
    public sealed class BootStepRecord
    {
        public BootPhase Phase { get; }
        public string Name { get; }
        /// <summary><see cref="Time.realtimeSinceStartupAsDouble"/> when the step started.</summary>
        public double StartedAt { get; }
        public BootStepResult Result { get; private set; } = BootStepResult.Running;
        /// <summary>0..1 a running splash task reported.</summary>
        public float Progress { get; internal set; }
        /// <summary>Text key a running splash task reported.</summary>
        public string Status { get; internal set; }
        public string Error { get; private set; }

        /// <summary>Seconds the step took, or has taken so far while running.</summary>
        public double Duration => Result == BootStepResult.Running ? Time.realtimeSinceStartupAsDouble - StartedAt : duration;

        private double duration;

        internal BootStepRecord(BootPhase phase, string name)
        {
            Phase = phase;
            Name = name;
            StartedAt = Time.realtimeSinceStartupAsDouble;
        }

        internal void Finish(BootStepResult result, string error = null)
        {
            if (Result != BootStepResult.Running) return;
            duration = Time.realtimeSinceStartupAsDouble - StartedAt;
            Result = result;
            Error = error;
            if (result == BootStepResult.Succeeded) Progress = 1f;
        }

        public override string ToString() => $"{Phase} {Name}: {Result} in {Duration * 1000.0:0.0} ms";
    }
}
