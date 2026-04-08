using NUnit.Framework;
using Shibafu.BehaviourTree;

namespace Shibafu.BehaviourTree.Tests
{
    public class BTLeafTests
    {
        [Test]
        public void Condition_ReturnsSuccess_WhenTrue()
        {
            var ctx = new BTContext();
            var node = new BTCondition(_ => true);
            Assert.AreEqual(BTStatus.Success, node.Tick(ctx));
        }

        [Test]
        public void Condition_ReturnsFailure_WhenFalse()
        {
            var ctx = new BTContext();
            var node = new BTCondition(_ => false);
            Assert.AreEqual(BTStatus.Failure, node.Tick(ctx));
        }

        [Test]
        public void Action_ReturnsDelegateResult()
        {
            var ctx = new BTContext();
            var node = new BTDelegateAction(_ => BTStatus.Running);
            Assert.AreEqual(BTStatus.Running, node.Tick(ctx));
        }

        [Test]
        public void DelegateAction_CallsOnStartOncePerVisit_ThenOnTickUntilTerminal()
        {
            var ctx = new BTContext();
            var starts = 0;
            var ticks = 0;
            var returnRunning = true;
            var node = new BTDelegateAction(
                _ =>
                {
                    ticks++;
                    return returnRunning ? BTStatus.Running : BTStatus.Success;
                },
                onStart: _ => starts++);

            Assert.AreEqual(BTStatus.Running, node.Tick(ctx));
            Assert.AreEqual(1, starts);
            Assert.AreEqual(1, ticks);

            Assert.AreEqual(BTStatus.Running, node.Tick(ctx));
            Assert.AreEqual(1, starts);
            Assert.AreEqual(2, ticks);

            returnRunning = false;
            Assert.AreEqual(BTStatus.Success, node.Tick(ctx));
            Assert.AreEqual(1, starts);
            Assert.AreEqual(3, ticks);

            Assert.AreEqual(BTStatus.Success, node.Tick(ctx));
            Assert.AreEqual(2, starts);
            Assert.AreEqual(4, ticks);
        }

        [Test]
        public void Inverter_SwapsSuccessAndFailure()
        {
            var ctx = new BTContext();
            var ok = new BTInverter(new BTCondition(_ => true));
            var bad = new BTInverter(new BTCondition(_ => false));
            Assert.AreEqual(BTStatus.Failure, ok.Tick(ctx));
            Assert.AreEqual(BTStatus.Success, bad.Tick(ctx));
        }

        [Test]
        public void Inverter_PassesRunning()
        {
            var ctx = new BTContext();
            var node = new BTInverter(new BTDelegateAction(_ => BTStatus.Running));
            Assert.AreEqual(BTStatus.Running, node.Tick(ctx));
        }
    }
}
