using System;
using System.Collections.Generic;
using Core.Utilities.Notifications;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.Utilities.Tests
{
    public class NoticesTests
    {
        private readonly List<(Notice notice, NoticeRemoval removal)> removed = new();
        private readonly List<Notice> posted = new();

        [SetUp]
        public void SetUp()
        {
            Notices.ResetForTests();
            removed.Clear();
            posted.Clear();
            Notices.Posted += posted.Add;
            Notices.Removed += (notice, removal) => removed.Add((notice, removal));
        }

        [TearDown]
        public void TearDown()
        {
            Notices.ResetForTests();
        }

        private static NoticeAction Option(string key, Action callback = null) => new(key, key, callback ?? (() => { }));

        private static Notice Toast(string id = null) => Notice.Toast(id, NoticeSeverity.Info, "toast", "Toast");

        private static Notice Ack(string id = null) =>
            Notice.Acknowledge(id, NoticeSeverity.Warning, "ack", "Acknowledge");

        private static Notice Choice(string id, Action onA = null, Action onB = null) =>
            Notice.Choice(id, NoticeSeverity.Warning, "choice", "Choose", null, Option("a", onA), Option("b", onB));

        [Test]
        public void PostQueuesInOrderAndRaisesPosted()
        {
            var first = Toast();
            var second = Ack();
            Notices.Post(first);
            Notices.Post(second);

            CollectionAssert.AreEqual(new[] { first, second }, Notices.Pending);
            CollectionAssert.AreEqual(new[] { first, second }, posted);
            Assert.AreNotEqual(default(DateTime), first.PostedUtc);
        }

        [Test]
        public void PostingTheSameInstanceTwiceQueuesItOnce()
        {
            var notice = Toast();
            Notices.Post(notice);
            Notices.Post(notice);

            Assert.AreEqual(1, Notices.Pending.Count);
            Assert.AreEqual(1, posted.Count);
        }

        [Test]
        public void SameIdReplacesInPlace()
        {
            var first = Toast("a");
            var other = Toast("b");
            var replacement = Toast("a");
            Notices.Post(first);
            Notices.Post(other);
            Notices.Post(replacement);

            CollectionAssert.AreEqual(new[] { replacement, other }, Notices.Pending);
            CollectionAssert.AreEqual(new[] { (first, NoticeRemoval.Replaced) }, removed);
        }

        [Test]
        public void NullIdsNeverReplace()
        {
            Notices.Post(Toast());
            Notices.Post(Toast());

            Assert.AreEqual(2, Notices.Pending.Count);
        }

        [Test]
        public void TryTakeRemovesToastsAndAcknowledges()
        {
            var toast = Toast();
            var ack = Ack();
            Notices.Post(toast);
            Notices.Post(ack);

            Assert.IsTrue(Notices.TryTake(out var taken));
            Assert.AreSame(toast, taken);
            Assert.IsTrue(Notices.TryTake(out taken));
            Assert.AreSame(ack, taken);
            Assert.IsFalse(Notices.TryTake(out taken));
            Assert.IsNull(taken);
            Assert.AreEqual(0, Notices.Pending.Count);
            CollectionAssert.AreEqual(new[] { (toast, NoticeRemoval.Taken), (ack, NoticeRemoval.Taken) }, removed);
        }

        [Test]
        public void TryTakeHonoursTheFilter()
        {
            var ack = Ack();
            var toast = Toast();
            Notices.Post(ack);
            Notices.Post(toast);

            Assert.IsTrue(Notices.TryTake(out var taken, n => n.Response == NoticeResponse.None));
            Assert.AreSame(toast, taken);
            CollectionAssert.AreEqual(new[] { ack }, Notices.Pending);
        }

        [Test]
        public void TakenChoiceStaysPendingButIsNotHandedOutTwice()
        {
            var choice = Choice("conflict");
            Notices.Post(choice);

            Assert.IsTrue(Notices.TryTake(out var taken));
            Assert.AreSame(choice, taken);
            Assert.IsTrue(choice.IsTaken);
            CollectionAssert.AreEqual(new[] { choice }, Notices.Pending);
            Assert.IsFalse(Notices.TryTake(out _));
            Assert.IsEmpty(removed);
        }

        [Test]
        public void ReleasedChoiceCanBeTakenAgain()
        {
            var choice = Choice("conflict");
            Notices.Post(choice);
            Notices.TryTake(out _);

            Notices.Release(choice);

            Assert.IsFalse(choice.IsTaken);
            Assert.IsTrue(Notices.TryTake(out var taken));
            Assert.AreSame(choice, taken);
        }

        [Test]
        public void RunResolvesChoiceOnce()
        {
            var aRuns = 0;
            var bRuns = 0;
            var choice = Choice("conflict", () => aRuns++, () => bRuns++);
            Notices.Post(choice);
            Notices.TryTake(out _);

            Assert.IsTrue(Notices.Run(choice, choice.Actions[0]));
            Assert.IsFalse(Notices.Run(choice, choice.Actions[1]));

            Assert.AreEqual(1, aRuns);
            Assert.AreEqual(0, bRuns);
            Assert.AreEqual(0, Notices.Pending.Count);
            Assert.IsFalse(choice.IsTaken);
            CollectionAssert.AreEqual(new[] { (choice, NoticeRemoval.Resolved) }, removed);
        }

        [Test]
        public void RunOnAToastActionRunsItEveryTime()
        {
            var runs = 0;
            var toast = Notice.Toast(null, NoticeSeverity.Info, "synced", "Synced", null, Option("show", () => runs++));
            Notices.Post(toast);
            Notices.TryTake(out _);

            Assert.IsTrue(Notices.Run(toast, toast.Actions[0]));
            Assert.IsTrue(Notices.Run(toast, toast.Actions[0]));
            Assert.AreEqual(2, runs);
        }

        [Test]
        public void RunRejectsAForeignAction()
        {
            var choice = Choice("conflict");
            Notices.Post(choice);

            Assert.Throws<ArgumentException>(() => Notices.Run(choice, Option("other")));
            Assert.AreEqual(1, Notices.Pending.Count);
        }

        [Test]
        public void ThrowingActionIsLoggedAndStillResolves()
        {
            var choice = Choice("conflict", () => throw new InvalidOperationException("boom"));
            Notices.Post(choice);

            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("boom"));
            Assert.IsTrue(Notices.Run(choice, choice.Actions[0]));
            Assert.AreEqual(0, Notices.Pending.Count);
        }

        [Test]
        public void DismissWithdrawsByIdEvenWhenTaken()
        {
            var choice = Choice("conflict");
            Notices.Post(choice);
            Notices.TryTake(out _);

            Assert.IsTrue(Notices.Dismiss("conflict"));
            Assert.IsFalse(Notices.Dismiss("conflict"));
            Assert.IsFalse(Notices.Dismiss(null));

            Assert.AreEqual(0, Notices.Pending.Count);
            Assert.IsFalse(choice.IsTaken);
            CollectionAssert.AreEqual(new[] { (choice, NoticeRemoval.Dismissed) }, removed);
            Assert.IsFalse(Notices.Run(choice, choice.Actions[0]));
        }

        [Test]
        public void RepostingATakenChoiceHandsItOutAgain()
        {
            var first = Choice("conflict");
            Notices.Post(first);
            Notices.TryTake(out _);

            var second = Choice("conflict");
            Notices.Post(second);

            CollectionAssert.AreEqual(new[] { (first, NoticeRemoval.Replaced) }, removed);
            Assert.IsTrue(Notices.TryTake(out var taken));
            Assert.AreSame(second, taken);
        }

        [Test]
        public void ResponseRulesAreValidated()
        {
            Assert.Throws<ArgumentException>(() => new Notice(null, NoticeSeverity.Info, NoticeResponse.Acknowledge,
                "k", "f", null, new[] { Option("a") }));
            Assert.Throws<ArgumentException>(() => new Notice(null, NoticeSeverity.Info, NoticeResponse.Choice,
                "k", "f", null, new[] { Option("a") }));
            Assert.Throws<ArgumentException>(() => new Notice(null, NoticeSeverity.Info, NoticeResponse.None,
                null, null));
            Assert.Throws<ArgumentException>(() => new Notice(null, NoticeSeverity.Info, NoticeResponse.None,
                "k", "f", null, new NoticeAction[] { null }));
            Assert.Throws<ArgumentNullException>(() => Notices.Post(null));
        }

        [Test]
        public void FormatFallbackFillsArgs()
        {
            var notice = Notice.Toast(null, NoticeSeverity.Info, "k", "Synced {0} saves", new object[] { 3 });
            Assert.AreEqual("Synced 3 saves", notice.FormatFallback());
            Assert.AreEqual("Toast", Toast().FormatFallback());
        }
    }
}
