using NUnit.Framework;
using Shibafu.BehaviourTree;

namespace Shibafu.BehaviourTree.Tests
{
    public class BTCompositeTests
    {
        [Test]
        public void Sequence_AllSuccess_ReturnsSuccess()
        {
            var ctx = new BTContext();
            var seq = new BTSequence();
            seq.AddChild(new BTCondition(_ => true));
            seq.AddChild(new BTDelegateAction(_ => BTStatus.Success));
            Assert.AreEqual(BTStatus.Success, seq.Tick(ctx));
        }

        [Test]
        public void Sequence_StopsOnFirstFailure()
        {
            var ctx = new BTContext();
            var seq = new BTSequence();
            var secondCalled = false;
            seq.AddChild(new BTCondition(_ => false));
            seq.AddChild(new BTDelegateAction(_ =>
            {
                secondCalled = true;
                return BTStatus.Success;
            }));
            Assert.AreEqual(BTStatus.Failure, seq.Tick(ctx));
            Assert.IsFalse(secondCalled);
        }

        [Test]
        public void Sequence_ResumesAfterRunning()
        {
            var ctx = new BTContext();
            var seq = new BTSequence();
            var calls = 0;
            seq.AddChild(new BTDelegateAction(_ =>
            {
                calls++;
                return calls >= 2 ? BTStatus.Success : BTStatus.Running;
            }));
            seq.AddChild(new BTDelegateAction(_ => BTStatus.Success));
            Assert.AreEqual(BTStatus.Running, seq.Tick(ctx));
            Assert.AreEqual(BTStatus.Success, seq.Tick(ctx));
            Assert.AreEqual(2, calls);
        }

        [Test]
        public void Selector_FirstSuccess_ShortCircuits()
        {
            var ctx = new BTContext();
            var sel = new BTSelector();
            var secondCalled = false;
            sel.AddChild(new BTCondition(_ => true));
            sel.AddChild(new BTDelegateAction(_ =>
            {
                secondCalled = true;
                return BTStatus.Success;
            }));
            Assert.AreEqual(BTStatus.Success, sel.Tick(ctx));
            Assert.IsFalse(secondCalled);
        }

        [Test]
        public void Selector_AllFailure_ReturnsFailure()
        {
            var ctx = new BTContext();
            var sel = new BTSelector();
            sel.AddChild(new BTCondition(_ => false));
            sel.AddChild(new BTCondition(_ => false));
            Assert.AreEqual(BTStatus.Failure, sel.Tick(ctx));
        }

        [Test]
        public void Selector_ResumesAfterRunning()
        {
            var ctx = new BTContext();
            var sel = new BTSelector();
            var calls = 0;
            sel.AddChild(new BTDelegateAction(_ =>
            {
                calls++;
                return calls >= 2 ? BTStatus.Failure : BTStatus.Running;
            }));
            sel.AddChild(new BTCondition(_ => true));
            Assert.AreEqual(BTStatus.Running, sel.Tick(ctx));
            Assert.AreEqual(BTStatus.Success, sel.Tick(ctx));
        }

        [Test]
        public void Reset_ClearsSequenceProgress()
        {
            var ctx = new BTContext();
            var seq = new BTSequence();
            seq.AddChild(new BTDelegateAction(_ => BTStatus.Running));
            seq.AddChild(new BTDelegateAction(_ => BTStatus.Success));
            Assert.AreEqual(BTStatus.Running, seq.Tick(ctx));
            seq.Reset();
            Assert.AreEqual(BTStatus.Running, seq.Tick(ctx));
        }
    }
}
