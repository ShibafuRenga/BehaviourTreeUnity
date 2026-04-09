using Newtonsoft.Json.Linq;
using Shibafu.BehaviourTree.Serialization;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 可 JSON 反序列化的条件叶子：<see cref="Evaluate"/> 为真则 Success，否则 Failure。
    /// 与内置 <c>type: condition</c>（data.mode 等）不同，子类通过 <see cref="BTNodeTypeAttribute"/> 使用独立 <c>type</c>。
    /// </summary>
    public abstract class BTSimpleConditionNode : BTNode
    {
        protected BTSimpleConditionNode(string name = null) : base(name ?? "Condition")
        {
        }

        protected sealed override BTStatus TickCore(BTContext context) =>
            Evaluate(context) ? BTStatus.Success : BTStatus.Failure;

        protected abstract bool Evaluate(BTContext context);

        /// <summary>
        /// 从 <c>data</c> 读取参数。默认会绑定带 <see cref="BTNodeInspectorFieldAttribute"/> 的字段/属性；重写时请先 <c>base.InitFromJson(data, loadContext)</c>。
        /// </summary>
        public virtual void InitFromJson(JObject data, BTDefinitionLoadContext loadContext = null)
        {
            BTNodeReferenceBinder.ApplyFromJson(this, data, loadContext);
        }
    }
}
