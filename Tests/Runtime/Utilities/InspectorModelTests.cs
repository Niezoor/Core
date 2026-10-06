using System;
using System.Collections.Generic;
using System.Linq;
using Core.Utilities.Inspector;
using NUnit.Framework;
using UnityEngine;

namespace Core.Utilities.Tests
{
    public class InspectorModelTests
    {
        [Serializable]
        private struct Stats
        {
            public int strength;
            public float speed;
        }

        private class Base
        {
            public int baseValue = 1;
        }

#pragma warning disable CS0414, CS0649, CS0169
        private sealed class Sample : Base
        {
            public int level = 3;
            [SerializeField] private float health = 10f;
            [NonSerialized] public int notSerialized;
            [HideInInspector] public int hidden;
            public static int staticValue;
            public readonly int readOnlyField = 5;
            private int privateField;
            [field: SerializeField] public string Title { get; private set; } = "t";
            [Inspect] public int Doubled => level * 2;
            [Inspect(ReadOnly = true)] public int locked = 7;
            [Inspect(OnChanged = nameof(Changed))] public int watched;
            public Stats stats;
            public List<int> numbers = new() { 1, 2, 3 };
            public Stats[] statsArray = { new() { strength = 1 } };
            [Inspect] public int Throws => throw new InvalidOperationException("broken getter");
            public int changedCalls;

            [Button] private void Reset() => level = 0;

            private void Changed() => changedCalls++;

            public float Health => health;
        }
#pragma warning restore CS0414, CS0649, CS0169

        private static InspectorMember Find(IEnumerable<InspectorMember> members, string name) =>
            members.Single(m => m.Name == name);

        [Test]
        public void ShowsWhatUnitysInspectorShowsPlusInspectAndButtons()
        {
            var names = InspectorModel.GetMembers(new Sample()).Select(m => m.Name).ToList();

            CollectionAssert.AreEqual(new[]
            {
                "baseValue", "level", "health", "<Title>k__BackingField", "locked", "watched", "stats", "numbers",
                "statsArray", "changedCalls", "Doubled", "Throws", "Reset",
            }, names);
        }

        [Test]
        public void LabelsAreNicified()
        {
            var members = InspectorModel.GetMembers(new Sample());

            Assert.AreEqual("Title", Find(members, "<Title>k__BackingField").Label);
            Assert.AreEqual("Base Value", Find(members, "baseValue").Label);
            Assert.AreEqual("Max Health", InspectorModel.Nicify("m_maxHealth"));
            Assert.AreEqual("Speed", InspectorModel.Nicify("_speed"));
            Assert.AreEqual("HTTP Server", InspectorModel.Nicify("HTTPServer"));
            Assert.AreEqual("Level 2 Boss", InspectorModel.Nicify("level2Boss"));
            Assert.AreEqual("Snake Case", InspectorModel.Nicify("snake_case"));
        }

        [Test]
        public void ReadOnlyComesFromAttributeAndMissingSetter()
        {
            var members = InspectorModel.GetMembers(new Sample());

            Assert.IsTrue(Find(members, "locked").IsReadOnly);
            Assert.IsTrue(Find(members, "Doubled").IsReadOnly);
            Assert.IsFalse(Find(members, "level").IsReadOnly);
            Assert.IsFalse(Find(members, "locked").SetValue(1));
        }

        [Test]
        public void SetValueWritesAndCallsOnChanged()
        {
            var sample = new Sample();
            var members = InspectorModel.GetMembers(sample);

            Assert.IsTrue(Find(members, "health").SetValue(42f));
            Assert.IsTrue(Find(members, "watched").SetValue(9));

            Assert.AreEqual(42f, sample.Health);
            Assert.AreEqual(9, sample.watched);
            Assert.AreEqual(1, sample.changedCalls);
            Assert.AreEqual(sample.level * 2, Find(members, "Doubled").GetValue());
        }

        [Test]
        public void NestedStructEditsInPlace()
        {
            var sample = new Sample();
            var stats = Find(InspectorModel.GetMembers(sample), "stats");

            Assert.IsTrue(stats.HasChildren);
            Assert.IsTrue(Find(stats.GetChildren(), "strength").SetValue(12));

            Assert.AreEqual(12, sample.stats.strength);
        }

        [Test]
        public void ListElementsFollowTheList()
        {
            var sample = new Sample();
            var numbers = Find(InspectorModel.GetMembers(sample), "numbers");

            Assert.AreEqual(3, numbers.GetChildren().Count);
            Assert.IsTrue(numbers.GetChildren()[1].SetValue(20));
            sample.numbers.Add(4);

            Assert.AreEqual(20, sample.numbers[1]);
            Assert.AreEqual(4, numbers.GetChildren().Count);
            Assert.AreEqual("Element 3", numbers.GetChildren()[3].Label);
        }

        [Test]
        public void StructInsideArrayEditsInPlace()
        {
            var sample = new Sample();
            var element = Find(InspectorModel.GetMembers(sample), "statsArray").GetChildren()[0];

            Assert.IsTrue(Find(element.GetChildren(), "speed").SetValue(3.5f));

            Assert.AreEqual(3.5f, sample.statsArray[0].speed);
            Assert.AreEqual(1, sample.statsArray[0].strength);
        }

        [Test]
        public void ThrowingGetterShowsTheException()
        {
            var value = Find(InspectorModel.GetMembers(new Sample()), "Throws").GetValue();

            Assert.IsInstanceOf<InvalidOperationException>(value);
        }

        [Test]
        public void ButtonsInvokeTheMethod()
        {
            var sample = new Sample();
            var button = Find(InspectorModel.GetMembers(sample), "Reset");

            Assert.AreEqual(InspectorMemberKind.Button, button.Kind);
            button.Invoke();

            Assert.AreEqual(0, sample.level);
        }

        [Test]
        public void CustomRowsReadAndWriteThroughDelegates()
        {
            var value = 1;
            var row = InspectorMember.Custom("Value", typeof(int), () => value, v => value = (int)v);

            Assert.IsTrue(row.SetValue(5));
            Assert.AreEqual(5, row.GetValue());
            Assert.IsTrue(InspectorMember.Custom("Read", typeof(int), () => value).IsReadOnly);
        }
    }
}
