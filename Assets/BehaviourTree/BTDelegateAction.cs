using System;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 由委托驱动的动作；对应 JSON <c>type: "action"</c> + <c>data.handler</c>，不走特性自动注册。
    /// </summary>
    public sealed class BTDelegateAction : BTAction
    {
        private readonly Func<BTContext, BTStatus> _onTick;

        public BTDelegateAction(Func<BTContext, BTStatus> onTick, string name = null) : base(name ?? "Action")
        {
            _onTick = onTick ?? throw new ArgumentNullException(nameof(onTick));
        }

        protected override BTStatus OnTick(BTContext context) => _onTick(context);
    }
}
