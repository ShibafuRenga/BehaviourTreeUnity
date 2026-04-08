using Newtonsoft.Json.Linq;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 叶子动作基类。从 JSON 加载时：反射无参构造 → <see cref="InitFromJson"/> → 再由加载器设置 <see cref="BTNode.Name"/>。
    /// 委托型动作用 <see cref="BTDelegateAction"/>；新类型继承本类并加 <see cref="BTNodeTypeAttribute"/> 即可被自动注册，无需改 <c>BTDefinitionLoadContext</c>。
    /// </summary>
    public abstract class BTAction : BTNode
    {
        protected BTAction(string name = null) : base(name ?? "Action")
        {
        }

        protected sealed override BTStatus TickCore(BTContext context) => OnTick(context);

        protected abstract BTStatus OnTick(BTContext context);

        /// <summary>从 <c>data</c> 读取参数；默认无操作。</summary>
        public virtual void InitFromJson(JObject data)
        {
        }
    }
}
