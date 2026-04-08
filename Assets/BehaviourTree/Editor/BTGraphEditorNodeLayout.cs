using System;

namespace Shibafu.BehaviourTree.Editor
{
    /// <summary>
    /// 与序列化规则一致：仅 sequence / selector / inverter 可挂子节点（显示「子」端口）。
    /// </summary>
    public static class BTGraphEditorNodeLayout
    {
        public static bool ShowsChildPort(string typeId)
        {
            if (string.IsNullOrEmpty(typeId))
                return false;
            return string.Equals(typeId, "sequence", StringComparison.Ordinal)
                   || string.Equals(typeId, "selector", StringComparison.Ordinal)
                   || string.Equals(typeId, "inverter", StringComparison.Ordinal);
        }
    }
}
