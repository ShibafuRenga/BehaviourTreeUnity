using System.Collections.Generic;

namespace Shibafu.BehaviourTree.Serialization
{
    /// <summary>
    /// 由 JSON 节点与子树构建运行时 <see cref="BTNode"/>。自定义节点在 <see cref="BTDefinitionLoadContext.RegisterNodeType"/> 注册同名的 factory。
    /// </summary>
    /// <param name="definition">原始定义（含 <c>data</c>）。</param>
    /// <param name="context">加载上下文（可解析 handler 等）。</param>
    /// <param name="children">已递归构建的子节点，顺序与 JSON <c>children</c> 一致。</param>
    public delegate BTNode BTNodeFactory(
        BTNodeDefinition definition,
        BTDefinitionLoadContext context,
        IReadOnlyList<BTNode> children);
}
