using Newtonsoft.Json.Linq;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 叶子动作基类。从 JSON 加载时：反射无参构造 → <see cref="InitFromJson"/> → 再由加载器设置 <see cref="BTNode.Name"/>。
    /// 带 <see cref="BTNodeInspectorFieldAttribute"/> 的 <see cref="UnityEngine.Object"/> 字段由 <see cref="BTNodeReferenceBinder"/> 自动从 data 绑定。
    /// 委托型动作用 <see cref="BTDelegateAction"/>；新类型继承本类并加 <see cref="BTNodeTypeAttribute"/> 即可被自动注册，无需改 <c>BTDefinitionLoadContext</c>。
    /// </summary>
    public abstract class BTAction : BTNode
    {
        private bool _startedThisVisit;

        protected BTAction(string name = null) : base(name ?? "Action")
        {
        }

        protected sealed override BTStatus TickCore(BTContext context)
        {
            if (!_startedThisVisit)
            {
                OnStart(context);
                _startedThisVisit = true;
            }

            var status = OnTick(context);
            if (status != BTStatus.Running)
                _startedThisVisit = false;
            return status;
        }

        /// <summary>
        /// 每次「进入」本动作节点时调用一次（本轮首次 <see cref="Tick"/>），在 <see cref="OnTick"/> 之前；返回 <see cref="BTStatus.Running"/> 期间后续 Tick 不再调用，直至成功/失败后再次进入会再调用。
        /// </summary>
        protected virtual void OnStart(BTContext context)
        {
        }

        protected abstract BTStatus OnTick(BTContext context);

        /// <summary>
        /// 从 <c>data</c> 读取参数。默认会绑定带 <see cref="BTNodeInspectorFieldAttribute"/> 的场景引用字段；重写时请先 <c>base.InitFromJson(data)</c>。
        /// </summary>
        public virtual void InitFromJson(JObject data)
        {
            BTNodeReferenceBinder.ApplyFromJson(this, data);
        }

        public override void Reset()
        {
            base.Reset();
            _startedThisVisit = false;
        }
    }
}
