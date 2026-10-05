using System;
using System.Collections.Generic;
using System.Globalization;

namespace Core.Utilities.Notifications
{
    /// <summary>How bad the situation is - for the icon and colour, not for what the player has to do.</summary>
    public enum NoticeSeverity
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>What the player has to do about a notice, which decides how and when the game shows it.</summary>
    public enum NoticeResponse
    {
        /// <summary>A toast: shown and gone by itself. May carry actions, e.g. "Show".</summary>
        None,

        /// <summary>A dialog the player has to see and confirm; the view adds the OK button itself.</summary>
        Acknowledge,

        /// <summary>
        /// A dialog with only the notice's actions and no way to close it otherwise. Stays pending until one of
        /// them runs through <see cref="Notices.Run"/>.
        /// </summary>
        Choice,
    }

    public sealed class NoticeAction
    {
        public string TextKey { get; }
        public string FallbackText { get; }

        private readonly Action callback;

        public NoticeAction(string textKey, string fallbackText, Action callback)
        {
            TextKey = textKey;
            FallbackText = fallbackText;
            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        internal void Invoke() => callback();

        public override string ToString() => TextKey ?? FallbackText;
    }

    /// <summary>
    /// Something a system wants the player to know. The text is a localization key for the game's view to translate,
    /// with a fallback for when it has no translation. Lives only in memory - a condition that still holds after a
    /// restart is detected and posted again.
    /// </summary>
    public sealed class Notice
    {
        private static readonly object[] NoArgs = Array.Empty<object>();

        /// <summary>
        /// Posting a notice with the Id of a pending one replaces it, so a system can repost a condition without
        /// stacking copies. Null for a notice that is never replaced.
        /// </summary>
        public string Id { get; }
        public NoticeSeverity Severity { get; }
        public NoticeResponse Response { get; }
        public string TextKey { get; }
        /// <summary>A composite format string filled with <see cref="Args"/>.</summary>
        public string FallbackText { get; }
        public IReadOnlyList<object> Args { get; }
        public IReadOnlyList<NoticeAction> Actions { get; }
        public DateTime PostedUtc { get; internal set; }
        /// <summary>A <see cref="NoticeResponse.Choice"/> handed out by <see cref="Notices.TryTake"/> and not yet
        /// resolved or released.</summary>
        public bool IsTaken { get; internal set; }

        public Notice(string id, NoticeSeverity severity, NoticeResponse response, string textKey, string fallbackText,
            object[] args = null, NoticeAction[] actions = null)
        {
            if (string.IsNullOrEmpty(textKey) && string.IsNullOrEmpty(fallbackText))
                throw new ArgumentException("A notice needs a text key or a fallback text.");

            actions ??= Array.Empty<NoticeAction>();
            foreach (var action in actions)
            {
                if (action == null) throw new ArgumentException("Notice actions can't be null.", nameof(actions));
            }

            switch (response)
            {
                case NoticeResponse.Acknowledge when actions.Length > 0:
                    throw new ArgumentException("An Acknowledge notice has no actions - the view adds OK itself.",
                        nameof(actions));
                case NoticeResponse.Choice when actions.Length < 2:
                    throw new ArgumentException("A Choice notice needs at least two actions.", nameof(actions));
            }

            Id = id;
            Severity = severity;
            Response = response;
            TextKey = textKey;
            FallbackText = fallbackText;
            Args = args ?? NoArgs;
            Actions = actions;
        }

        public static Notice Toast(string id, NoticeSeverity severity, string textKey, string fallbackText,
            object[] args = null, params NoticeAction[] actions) =>
            new(id, severity, NoticeResponse.None, textKey, fallbackText, args, actions);

        public static Notice Acknowledge(string id, NoticeSeverity severity, string textKey, string fallbackText,
            object[] args = null) =>
            new(id, severity, NoticeResponse.Acknowledge, textKey, fallbackText, args);

        public static Notice Choice(string id, NoticeSeverity severity, string textKey, string fallbackText,
            object[] args, params NoticeAction[] actions) =>
            new(id, severity, NoticeResponse.Choice, textKey, fallbackText, args, actions);

        /// <summary>The fallback text with <see cref="Args"/> filled in.</summary>
        public string FormatFallback()
        {
            if (FallbackText == null || Args.Count == 0) return FallbackText;
            var args = new object[Args.Count];
            for (var i = 0; i < args.Length; i++) args[i] = Args[i];
            return string.Format(CultureInfo.CurrentCulture, FallbackText, args);
        }

        public override string ToString() => $"{Severity} {Response} {Id ?? TextKey}";
    }
}
