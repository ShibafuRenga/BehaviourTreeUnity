namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 仅为编辑器 TypeCache 提供 <c>action</c> 项；实际解析仍由 <c>handler</c> 的 <see cref="BTDelegateAction"/> 完成。
    /// </summary>
    [BTNodeType("action", "动作 Action (handler)")]
    internal static class BTActionJsonTypeMarker
    {
    }
}
