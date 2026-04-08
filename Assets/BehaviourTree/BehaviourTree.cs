namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 行为树：持有根节点，对给定上下文每 Tick 一次。
    /// </summary>
    public class BehaviourTree
    {
        public BTNode Root { get; }

        public BehaviourTree(BTNode root)
        {
            Root = root ?? throw new System.ArgumentNullException(nameof(root));
        }

        public BTStatus Tick(BTContext context) => Root.Tick(context);

        public void Reset() => Root.Reset();
    }
}
