namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 装饰器：只包装一个子节点。
    /// </summary>
    public abstract class BTDecorator : BTNode
    {
        public BTNode Child { get; private set; }

        protected BTDecorator(BTNode child, string name = null) : base(name)
        {
            Child = child;
        }

        protected abstract override BTStatus TickCore(BTContext context);

        public void SetChild(BTNode child) => Child = child;

        public override void Reset()
        {
            Child?.Reset();
        }
    }
}
