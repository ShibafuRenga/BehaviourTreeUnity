using NUnit.Framework;
using Shibafu.BehaviourTree;

namespace Shibafu.BehaviourTree.Tests
{
    public class BehaviourTreeRunnerTests
    {
        [Test]
        public void Tick_DelegatesToRoot()
        {
            var ctx = new BTContext();
            var root = new BTCondition(_ => true);
            var tree = new BehaviourTree(root);
            Assert.AreEqual(BTStatus.Success, tree.Tick(ctx));
        }

        [Test]
        public void Reset_DelegatesToRoot()
        {
            var ctx = new BTContext();
            var seq = new BTSequence();
            seq.AddChild(new BTDelegateAction(_ => BTStatus.Running));
            var tree = new BehaviourTree(seq);
            tree.Tick(ctx);
            tree.Reset();
            Assert.AreEqual(BTStatus.Running, tree.Tick(ctx));
        }
    }
}
