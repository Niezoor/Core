#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Core.Utilities
{
    /// <summary>
    /// Makes Addressables start every Play Mode session fresh when domain reload is off. Addressables only arms its
    /// own reset through <c>EditorApplication.delayCall</c> on exit, which an Editor in the background doesn't run
    /// before the next Play: that session then reuses the previous one's instance, whose update hook died with it,
    /// and every load waiting on an update never completes. The late call also resets Addressables mid-session.
    /// </summary>
    internal static class AddressablesPlayMode
    {
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ReinitializeOnEnter()
        {
            if (!EditorSettings.enterPlayModeOptionsEnabled) return;

            var reinitialize = typeof(Addressables).GetField("reinitializeAddressables", StaticMembers);
            var armReinitialize = typeof(Addressables).GetMethod("EnableReinitializeAddressablesFlag", StaticMembers);
            if (reinitialize == null || armReinitialize == null)
            {
                Debug.LogWarning("[Addressables] Couldn't force a reset on entering Play Mode (internals changed) - " +
                                 "with domain reload off, loads may hang every other session.");
                return;
            }

            // The pending call would reset Addressables again while this session is using it.
            EditorApplication.delayCall -= (EditorApplication.CallbackFunction)Delegate.CreateDelegate(
                typeof(EditorApplication.CallbackFunction), armReinitialize);
            reinitialize.SetValue(null, true);
        }
    }
}
#endif
