using System;
using System.Collections.Generic;
using BottleSort.Services;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class PushTests
    {
        sealed class FakeBackend : IPushBackend
        {
            public readonly List<string> Calls = new List<string>();
            public bool StartsOk = true;
            public bool AnswerLater;
            Action<bool> _pending;

            public void Start(Action<bool> ready)
            {
                Calls.Add("start");
                if (AnswerLater)
                    _pending = ready;
                else
                    ready(StartsOk);
            }

            public void Finish() => _pending?.Invoke(StartsOk);
            public void RequestPermission() => Calls.Add("permission");
            public void Subscribe(string topic) => Calls.Add("+" + topic);
            public void Unsubscribe(string topic) => Calls.Add("-" + topic);
        }

        FakeBackend _backend;
        PushController _push;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeBackend();
            _push = new PushController(_backend);
        }

        [Test]
        public void NothingBeforeLevelFour()
        {
            _push.Sync(1, true);
            _push.Sync(3, true);
            CollectionAssert.IsEmpty(_backend.Calls);
        }

        [Test]
        public void AfterTutorial_StartsAsksAndSubscribes()
        {
            _push.Sync(4, true);
            CollectionAssert.AreEqual(new[] { "start", "permission", "+all_users" }, _backend.Calls);
            Assert.IsTrue(_push.IsReady);
        }

        [Test]
        public void ToggleOff_Unsubscribes_ToggleOn_SubscribesAgain()
        {
            _push.Sync(10, true);
            _push.Sync(10, false);
            _push.Sync(10, true);
            CollectionAssert.AreEqual(new[] { "start", "permission", "+all_users", "-all_users", "+all_users" }, _backend.Calls);
        }

        [Test]
        public void RepeatedSync_DoesNotSpamFirebase()
        {
            _push.Sync(10, true);
            for (int i = 0; i < 5; i++)
                _push.Sync(11 + i, true);
            Assert.AreEqual(1, _backend.Calls.FindAll(c => c == "+all_users").Count);
            Assert.AreEqual(1, _backend.Calls.FindAll(c => c == "start").Count);
        }

        [Test]
        public void DisabledFromStart_UnsubscribesLikeFlutter()
        {
            _push.Sync(10, false);
            CollectionAssert.AreEqual(new[] { "start", "permission", "-all_users" }, _backend.Calls);
        }

        [Test]
        public void ToggleWhileStarting_UsesLatestSetting()
        {
            _backend.AnswerLater = true;
            _push.Sync(10, true);
            _push.Sync(10, false);
            _backend.Finish();
            CollectionAssert.AreEqual(new[] { "start", "permission", "-all_users" }, _backend.Calls);
        }

        [Test]
        public void FailedStart_StaysQuiet()
        {
            _backend.StartsOk = false;
            _push.Sync(10, true);
            _push.Sync(10, false);
            CollectionAssert.AreEqual(new[] { "start" }, _backend.Calls);
            Assert.IsFalse(_push.IsReady);
        }
    }
}
