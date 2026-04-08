using System;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 由委托驱动的动作；对应 JSON <c>type: "action"</c> + <c>data.handler</c>，不走特性自动注册。
    /// </summary>
    public sealed class BTDelegateAction : BTAction
    {
        private readonly Func<BTContext, BTStatus> _onTick;
        private readonly Action<BTContext> _onStart;

        /// <param name="onStart">可选；在每次进入节点时于 <paramref name="onTick"/> 之前调用一次。</param>
        public BTDelegateAction(
            Func<BTContext, BTStatus> onTick,
            string name = null,
            Action<BTContext> onStart = null) : base(name ?? "Action")
        {
            _onTick = onTick ?? throw new ArgumentNullException(nameof(onTick));
            _onStart = onStart;
        }

        protected override void OnStart(BTContext context) => _onStart?.Invoke(context);

        protected override BTStatus OnTick(BTContext context) => _onTick(context);
    }
}
