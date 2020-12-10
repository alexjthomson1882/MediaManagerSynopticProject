using System;

using NUnit.Framework;

namespace MediaManager.UnitTests {

    public sealed class Tests {

        private string root = null;

        [SetUp]
        public void Setup() {

            root = Environment.CurrentDirectory.Replace('\\', '/');
            if (root[^1] != '/') root += '/';

        }

        [Test]
        public void MediaDirectoryGUIDTest() => Assert.AreEqual( // ensure guids are the same for the same directory (but different objects)
            new MediaDirectory(root, null).GetGUID(),
            new MediaDirectory(root, null).GetGUID()
        );

        [TestCase("", "")]
        [TestCase(" ", " ")]
        [TestCase("123", "123")]
        [TestCase("test", "test")]
        public void GUIDEqualTest(string s1, string s2) => Assert.AreEqual(StringUtility.GetGUID(s1), StringUtility.GetGUID(s2));

        [TestCase("", " ")]
        [TestCase(" ", "  ")]
        [TestCase("test1", "test2")]
        [TestCase("1", "2")]
        [TestCase("abcdefg", "0123456")]
        public void GUIDNotEqualTest(string s1, string s2) => Assert.AreNotEqual(StringUtility.GetGUID(s1), StringUtility.GetGUID(s2));

    }

}