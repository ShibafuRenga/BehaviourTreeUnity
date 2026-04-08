using NUnit.Framework;
using Shibafu.BehaviourTree;

namespace Shibafu.BehaviourTree.Tests
{
    public class BTContextTests
    {
        [Test]
        public void SetGet_RoundTripsValue()
        {
            var ctx = new BTContext();
            ctx.Set("n", 42);
            Assert.AreEqual(42, ctx.Get<int>("n"));
        }

        [Test]
        public void TryGet_ReturnsFalse_WhenMissing()
        {
            var ctx = new BTContext();
            Assert.IsFalse(ctx.TryGet<int>("x", out _));
        }

        [Test]
        public void GetOrDefault_ReturnsDefault_WhenMissing()
        {
            var ctx = new BTContext();
            Assert.AreEqual(7, ctx.GetOrDefault("missing", 7));
        }

        [Test]
        public void Remove_ThenGet_Throws()
        {
            var ctx = new BTContext();
            ctx.Set("k", 1);
            Assert.IsTrue(ctx.Remove("k"));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => ctx.Get<int>("k"));
        }

        [Test]
        public void Clear_RemovesAll()
        {
            var ctx = new BTContext();
            ctx.Set("a", 1);
            ctx.Clear();
            Assert.IsFalse(ctx.TryGet<int>("a", out _));
        }
    }
}
