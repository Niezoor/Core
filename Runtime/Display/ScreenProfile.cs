using System;
using UnityEngine;

namespace Core.Display
{
    /// <summary>
    /// The current screen in layout terms (<see cref="ScreenState"/>) and events when it changes. In Play Mode it is
    /// refreshed once per frame before other scripts run; UI reacts to <see cref="ClassChanged"/> by switching layout
    /// variants and to <see cref="Changed"/> by resizing, scaling and fitting the safe area.
    /// </summary>
    public static class ScreenProfile
    {
        public const float MinUserScale = 0.5f;
        public const float MaxUserScale = 3f;

        private static ScreenState current;
        private static bool evaluated;
        private static ScreenMetrics? overrideMetrics;
        private static float userScale = 1f;
        private static SizeClassThresholds thresholds = SizeClassThresholds.Default;

        /// <summary>Anything changed: size, density, safe area, user scale or thresholds.</summary>
        public static event Action<ScreenState> Changed;

        /// <summary>
        /// Size class, orientation or device kind changed (<see cref="ScreenState.SameClass"/>). Raised before
        /// <see cref="Changed"/>, so a swapped layout variant is already in place when sizes are applied.
        /// </summary>
        public static event Action<ScreenState> ClassChanged;

        public static ScreenState Current
        {
            get
            {
                // Outside Play Mode nothing refreshes it every frame, and edit-mode tools must not see a stale screen.
                if (!evaluated || !Application.isPlaying)
                {
                    current = Evaluate(ReadMetrics(), thresholds, userScale);
                    evaluated = true;
                }

                return current;
            }
        }

        /// <summary>The player's UI size setting, multiplied into <see cref="ScreenState.Scale"/>.</summary>
        public static float UserScale
        {
            get => userScale;
            set
            {
                value = Mathf.Clamp(value, MinUserScale, MaxUserScale);
                if (value.Equals(userScale)) return;
                userScale = value;
                Refresh();
            }
        }

        public static SizeClassThresholds Thresholds
        {
            get => thresholds;
            set
            {
                if (value.Equals(thresholds)) return;
                thresholds = value;
                Refresh();
            }
        }

        public static bool IsOverridden => overrideMetrics.HasValue;

        /// <summary>Pretends the screen is <paramref name="metrics"/> until <see cref="ClearOverride"/>.</summary>
        public static void Override(ScreenMetrics metrics)
        {
            overrideMetrics = metrics;
            Refresh();
        }

        public static void ClearOverride()
        {
            if (!overrideMetrics.HasValue) return;
            overrideMetrics = null;
            Refresh();
        }

        public static ScreenState Evaluate(in ScreenMetrics metrics, SizeClassThresholds thresholds, float userScale) =>
            new(metrics, thresholds, Mathf.Clamp(userScale, MinUserScale, MaxUserScale));

        /// <summary>Reads the screen again and raises the events if anything changed.</summary>
        public static void Refresh()
        {
            var next = Evaluate(ReadMetrics(), thresholds, userScale);
            if (!evaluated)
            {
                // Nobody has seen a previous state, so there is nothing to report a change from.
                current = next;
                evaluated = true;
                return;
            }

            if (next.Equals(current)) return;
            var previous = current;
            current = next;
            if (!next.SameClass(previous)) Raise(ClassChanged, next);
            Raise(Changed, next);
        }

        private static ScreenMetrics ReadMetrics() => overrideMetrics ?? ScreenMetrics.Read();

        private static void Raise(Action<ScreenState> handlers, ScreenState state)
        {
            if (handlers == null) return;
            // One failing listener must not keep the others on the old layout.
            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<ScreenState>)handler).Invoke(state);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        internal static void ResetForTests() => ResetStatics();

        // Domain reload is off in Enter Play Mode settings, so nothing static may survive into the next session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            current = default;
            evaluated = false;
            overrideMetrics = null;
            userScale = 1f;
            thresholds = SizeClassThresholds.Default;
            Changed = null;
            ClassChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateRunner() => ScreenProfileRunner.Create();
    }
}
