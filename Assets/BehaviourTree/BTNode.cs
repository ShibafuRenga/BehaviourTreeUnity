using UnityEngine;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 行为树节点基类。由 <see cref="BehaviourTreeRunner"/> 构建的树会为每个节点填入 <see cref="RunnerTransform"/>；其它方式加载的树该字段可为 null。
    /// </summary>
    public abstract class BTNode
    {
        public string Name { get; set; }

        /// <summary>与 JSON / 图编辑器节点 <c>id</c> 一致；用于运行时状态映射。</summary>
        public string DebugNodeId { get; set; }

        /// <summary>
        /// 当前行为树所挂载的 <see cref="BehaviourTreeRunner"/> 所在 <c>GameObject</c> 的 Transform；供节点内访问场景坐标等。非 Runner 驱动时为 null。
        /// </summary>
        public Transform RunnerTransform { get; set; }

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
