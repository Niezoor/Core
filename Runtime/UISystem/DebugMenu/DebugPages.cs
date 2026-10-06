using System.Collections.Generic;
using System.Linq;
using Core.Display;
using Core.Utilities.Inspector;
using UnityEngine;

namespace Core.UISystem
{
    /// <summary>Debug page: the screen profile, with ready-made screens to try the layouts on.</summary>
    internal sealed class ScreenDebug
    {
        [Inspect(ReadOnly = true)] private string Profile => ScreenProfile.Current.ToString();
        [Inspect(ReadOnly = true)] private SizeClass SizeClass => ScreenProfile.Current.SizeClass;
        [Inspect(ReadOnly = true)] private Orientation Orientation => ScreenProfile.Current.Orientation;
        [Inspect(ReadOnly = true)] private bool Overridden => ScreenProfile.IsOverridden;

        [Inspect(Label = "UI Scale", Min = ScreenProfile.MinUserScale, Max = ScreenProfile.MaxUserScale)]
        private float UserScale
        {
            get => ScreenProfile.UserScale;
            set => ScreenProfile.UserScale = value;
        }

        [Inspect(Label = "Input Mode")]
        private InputMode Input
        {
            get => UIInput.Mode;
            set => UIInput.SetMode(value);
        }

        [Button("Phone, portrait")]
        private void PhonePortrait() => ScreenProfile.Override(new ScreenMetrics(1080, 2400, 2.625f, Notch(1080, 2400, true), true));

        [Button("Phone, landscape")]
        private void PhoneLandscape() => ScreenProfile.Override(new ScreenMetrics(2400, 1080, 2.625f, Notch(2400, 1080, false), true));

        [Button("Tablet")]
        private void Tablet() => ScreenProfile.Override(new ScreenMetrics(2560, 1600, 2f, default, true));

        [Button("Small window")]
        private void SmallWindow() => ScreenProfile.Override(new ScreenMetrics(800, 500, 1f, default, false));

        [Button("Desktop")]
        private void Desktop() => ScreenProfile.Override(new ScreenMetrics(1920, 1080, 1f, default, false));

        [Button("Real screen")]
        private void RealScreen() => ScreenProfile.ClearOverride();

        // A notch and a home indicator, like most current phones.
        private static Rect Notch(int width, int height, bool portrait) => portrait
            ? new Rect(0f, 60f, width, height - 60f - 130f)
            : new Rect(130f, 60f, width - 260f, height - 60f);
    }

    /// <summary>Debug page: time scale and frame rate.</summary>
    internal sealed class TimeDebug
    {
        [Inspect(Label = "Time Scale", Min = 0f, Max = 4f)]
        private float TimeScale
        {
            get => Time.timeScale;
            set => Time.timeScale = value;
        }

        [Inspect(Label = "Target Frame Rate")]
        private int TargetFrameRate
        {
            get => Application.targetFrameRate;
            set => Application.targetFrameRate = value;
        }

        [Inspect(ReadOnly = true)] private float Fps => Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;
        [Inspect(ReadOnly = true, Label = "Frame Time (ms)")] private float FrameTime => Time.unscaledDeltaTime * 1000f;
        [Inspect(ReadOnly = true)] private float Realtime => Time.realtimeSinceStartup;

        [Button("Pause")]
        private void Pause() => Time.timeScale = 0f;

        [Button("Normal speed")]
        private void Normal() => Time.timeScale = 1f;
    }

    /// <summary>Debug page: the screen stack.</summary>
    internal sealed class StackDebug
    {
        [Inspect(ReadOnly = true)] private string Top => Screens.Top?.GetType().Name ?? "none";

        [Inspect(ReadOnly = true)]
        private List<string> Stack => Screens.Stack.Select(s => s.GetType().Name).Reverse().ToList();

        [Inspect(ReadOnly = true)]
        private List<string> Overlays => Screens.Overlays.Select(s => s.GetType().Name).ToList();

        [Button("Back")]
        private void Back() => Screens.Back();

        [Button("Clear")]
        private void Clear() => Screens.Clear();
    }
}
