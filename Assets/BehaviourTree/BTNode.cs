namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 行为树节点基类（与 Unity 组件无关，可在任意处 Tick）。
    /// </summary>
    public abstract class BTNode
    {
        public string Name { get; set; }

        /// <summary>与 JSON / 图编辑器节点 <c>id</c> 一致；用于运行时状态映射。</summary>
        public string DebugNodeId { get; set; }

        protected BTNode(string name = null)
        {
            Name = name ?? GetType().Name;
        }

        public BTStatus Tick(BTContext context)
        {
            var s = TickCore(context);
            var sink = context?.DebugStatusSink;
            if (sink != null && !string.IsNullOrEmpty(DebugNodeId))
                sink.RecordNodeStatus(DebugNodeId, s);
            return s;
        }

        protected abstract BTStatus TickCore(BTContext context);

        /// <summary>
        /// 中断 Running 态时重置内部记忆（如组合子当前子节点下标）。
        /// </summary>
        public virtual void Reset()
        {
        }
    }
}
