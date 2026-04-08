using System.Collections.Generic;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 拥有多个子节点的组合节点基类。
    /// </summary>
    public abstract class BTComposite : BTNode
    {
        public IReadOnlyList<BTNode> Children => _children;
        private readonly List<BTNode> _children = new List<BTNode>();

        protected BTComposite(string name = null) : base(name)
        {
        }

        protected abstract override BTStatus TickCore(BTContext context);

        public void AddChild(BTNode child)
        {
            if (child != null)
                _children.Add(child);
        }

        public override void Reset()
        {
            foreach (var c in _children)
                c?.Reset();
            ResetMemory();
        }

        /// <summary>仅清除本节点上的运行记忆，不递归子节点（由 <see cref="Reset"/> 统一递归）。</summary>
        protected abstract void ResetMemory();
    }
}
