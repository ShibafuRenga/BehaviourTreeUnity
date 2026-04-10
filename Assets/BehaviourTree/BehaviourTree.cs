using UnityEngine;

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

        /// <summary>为根及以下所有节点设置 <see cref="BTNode.RunnerTransform"/>（通常为 <see cref="BehaviourTreeRunner.transform"/>）。</summary>
        public void AssignRunnerTransform(Transform runnerTransform)
        {
            AssignRunnerTransformRecursive(Root, runnerTransform);
        }

        private static void AssignRunnerTransformRecursive(BTNode node, Transform runnerTransform)
        {
            if (node == null)
                return;
            node.RunnerTransform = runnerTransform;
            switch (node)
            {
                case BTComposite composite:
                    foreach (var child in composite.Children)
                        AssignRunnerTransformRecursive(child, runnerTransform);
                    break;
                case BTDecorator decorator:
                    AssignRunnerTransformRecursive(decorator.Child, runnerTransform);
                    break;
            }
        }
    }
}
