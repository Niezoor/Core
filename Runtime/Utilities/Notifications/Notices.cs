using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Core.Utilities.Notifications
{
    public enum NoticeRemoval
    {
        /// <summary>Handed out by <see cref="Notices.TryTake"/>; the view now owns it.</summary>
        Taken,
        /// <summary>One of a <see cref="NoticeResponse.Choice"/>'s actions ran.</summary>
        Resolved,
        /// <summary>The posting system withdrew it - the condition no longer holds.</summary>
        Dismissed,
        /// <summary>A notice with the same Id was posted.</summary>
        Replaced,
    }

    /// <summary>
    /// The one queue of things the player should be told. Any system posts at any time, even before the first scene;
    /// the game takes them when it is ready to show them - toasts possibly right away, dialogs at a safe moment such
    /// as the main menu. Main thread only.
    /// </summary>
    public static class Notices
    {
        /// <summary>A hint that something is waiting; the view still takes it with <see cref="TryTake"/>.</summary>
        public static event Action<Notice> Posted;

        /// <summary>A notice left the queue. A view showing a taken Choice closes it on Dismissed or Replaced.</summary>
        public static event Action<Notice, NoticeRemoval> Removed;

        private static readonly List<Notice> pending = new();
        private static readonly ReadOnlyCollection<Notice> pendingView = pending.AsReadOnly();

        /// <summary>Oldest first, including Choice notices that are taken but not yet resolved.</summary>
        public static IReadOnlyList<Notice> Pending => pendingView;

        public static void Post(Notice notice)
        {
            if (notice == null) throw new ArgumentNullException(nameof(notice));
            if (pending.Contains(notice)) return;

            notice.PostedUtc = DateTime.UtcNow;
            notice.IsTaken = false;

            var index = notice.Id == null ? -1 : pending.FindIndex(p => p.Id == notice.Id);
            if (index >= 0)
            {
                var replaced = pending[index];
                pending[index] = notice;
                Removed?.Invoke(replaced, NoticeRemoval.Replaced);
            }
            else
            {
                pending.Add(notice);
            }

            Posted?.Invoke(notice);
        }

        /// <summary>
        /// The oldest notice that matches <paramref name="filter"/> and is not already taken. Toasts and Acknowledge
        /// notices leave the queue; a Choice stays until <see cref="Run"/> resolves it or <see cref="Release"/> hands
        /// it back, so closing the view without a decision never loses the question.
        /// </summary>
        public static bool TryTake(out Notice notice, Predicate<Notice> filter = null)
        {
            for (var i = 0; i < pending.Count; i++)
            {
                var candidate = pending[i];
                if (candidate.IsTaken || (filter != null && !filter(candidate))) continue;

                notice = candidate;
                if (notice.Response == NoticeResponse.Choice)
                {
                    notice.IsTaken = true;
                }
                else
                {
                    pending.RemoveAt(i);
                    Removed?.Invoke(notice, NoticeRemoval.Taken);
                }

                return true;
            }

            notice = null;
            return false;
        }

        /// <summary>Hands a taken Choice back, e.g. when its view closed without a decision.</summary>
        public static void Release(Notice notice)
        {
            if (notice != null && pending.Contains(notice)) notice.IsTaken = false;
        }

        /// <summary>
        /// Runs <paramref name="action"/> of <paramref name="notice"/>. For a Choice this resolves it; a second call
        /// after that does nothing, so a double click can't run two answers.
        /// </summary>
        /// <returns>Whether the action ran.</returns>
        public static bool Run(Notice notice, NoticeAction action)
        {
            if (notice == null) throw new ArgumentNullException(nameof(notice));
            if (action == null) throw new ArgumentNullException(nameof(action));

            var actionIndex = -1;
            for (var i = 0; i < notice.Actions.Count; i++)
            {
                if (notice.Actions[i] == action) actionIndex = i;
            }

            if (actionIndex < 0) throw new ArgumentException($"The action is not one of {notice}'s.", nameof(action));

            if (notice.Response == NoticeResponse.Choice)
            {
                if (!pending.Remove(notice)) return false;
                notice.IsTaken = false;
                Removed?.Invoke(notice, NoticeRemoval.Resolved);
            }

            try
            {
                action.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            return true;
        }

        /// <summary>Withdraws the pending notice with this Id, taken or not.</summary>
        /// <returns>Whether there was one.</returns>
        public static bool Dismiss(string id)
        {
            if (id == null) return false;
            var index = pending.FindIndex(p => p.Id == id);
            if (index < 0) return false;

            var notice = pending[index];
            pending.RemoveAt(index);
            notice.IsTaken = false;
            Removed?.Invoke(notice, NoticeRemoval.Dismissed);
            return true;
        }

        internal static void ResetForTests() => ResetStatics();

        // Domain reload is off in Enter Play Mode settings, so nothing static may survive into the next session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pending.Clear();
            Posted = null;
            Removed = null;
        }
    }
}
