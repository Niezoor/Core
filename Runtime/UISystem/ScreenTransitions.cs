using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UISystem
{
    /// <summary>Default screen transitions: USS transitions started by the <c>ui-screen--hidden</c> class.</summary>
    public static class ScreenTransitions
    {
        public const string HiddenClass = "ui-screen--hidden";

        /// <summary>A transition longer than this (e.g. a looping effect inside the view) is not waited for.</summary>
        public const float MaxWait = 2f;

        public static async Awaitable ToggleHiddenClassAsync(VisualElement root, bool show,
            CancellationToken cancellationToken)
        {
            if (show)
            {
                root.AddToClassList(HiddenClass);
                // The hidden style has to be resolved once, or there is nothing to transition from.
                await Awaitable.NextFrameAsync(cancellationToken);
                root.RemoveFromClassList(HiddenClass);
            }
            else
            {
                root.AddToClassList(HiddenClass);
            }

            // Styles resolve in the panel update after this frame's scripts; durations are known only then.
            await Awaitable.NextFrameAsync(cancellationToken);
            await WaitUnscaledAsync(Mathf.Min(LongestTransition(root), MaxWait), cancellationToken);
        }

        /// <summary>The longest transition (duration + delay) on the element or inside it, in seconds.</summary>
        public static float LongestTransition(VisualElement root)
        {
            var longest = 0f;
            root.Query<VisualElement>().ForEach(element =>
            {
                var style = element.resolvedStyle;
                longest = Mathf.Max(longest, Longest(style.transitionDuration) + Longest(style.transitionDelay));
            });
            return longest;
        }

        /// <summary>Waits in real time, so a paused game (<c>timeScale</c> 0) does not freeze its menus.</summary>
        public static async Awaitable WaitUnscaledAsync(float seconds, CancellationToken cancellationToken)
        {
            var end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) await Awaitable.NextFrameAsync(cancellationToken);
        }

        private static float Longest(IEnumerable<TimeValue> values)
        {
            var longest = 0f;
            if (values == null) return longest;
            foreach (var value in values)
            {
                var seconds = value.unit == TimeUnit.Millisecond ? value.value / 1000f : value.value;
                longest = Mathf.Max(longest, seconds);
            }

            return longest;
        }
    }
}
