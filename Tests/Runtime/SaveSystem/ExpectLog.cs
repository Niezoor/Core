using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.SaveSystem.Tests
{
    internal static class ExpectLog
    {
        /// <summary>Declares an error the store is expected to log; call before the action that logs it.</summary>
        public static void SaveError(SaveErrorKind kind)
        {
            LogAssert.Expect(LogType.Error, new Regex($@"^\[Save\] {kind}:"));
        }

        public static void Exception<T>() where T : System.Exception
        {
            LogAssert.Expect(LogType.Exception, new Regex(typeof(T).Name));
        }
    }
}
