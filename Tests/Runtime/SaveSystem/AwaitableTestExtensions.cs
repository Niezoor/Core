using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace Core.SaveSystem.Tests
{
    internal static class AwaitableTestExtensions
    {
        /// <summary>Result of an awaitable that must already be done - with zero latency nothing should yield.</summary>
        public static T Completed<T>(this Awaitable<T> awaitable)
        {
            var awaiter = awaitable.GetAwaiter();
            Assert.IsTrue(awaiter.IsCompleted, "expected the operation to finish without yielding");
            return awaiter.GetResult();
        }

        /// <summary>Lets a [UnityTest] wait for an awaitable frame by frame.</summary>
        public static IEnumerator Wait<T>(this Awaitable<T> awaitable, Action<T> onResult, float timeout = 5f)
        {
            var awaiter = awaitable.GetAwaiter();
            var deadline = Time.realtimeSinceStartup + timeout;
            while (!awaiter.IsCompleted)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("timed out waiting for the operation");
                yield return null;
            }

            onResult(awaiter.GetResult());
        }
    }
}
