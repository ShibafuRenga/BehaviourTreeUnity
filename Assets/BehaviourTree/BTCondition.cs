using System;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 叶子：条件为真返回 Success，否则 Failure（瞬时，不维持 Running）。
    /// </summary>
    [BTNodeType("condition", "条件 Condition", EditorMenuPath = "Condition")]
    public class BTCondition : BTNode
    {
        private readonly Func<BTContext, bool> _predicate;

        public BTCondition(Func<BTContext, bool> predicate, string name = null) : base(name ?? "Condition")
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        protected override BTStatus TickCore(BTContext context) =>
            _predicate(context) ? BTStatus.Success : BTStatus.Failure;
    }
}
