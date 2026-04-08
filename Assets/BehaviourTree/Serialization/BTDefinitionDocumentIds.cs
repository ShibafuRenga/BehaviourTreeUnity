using System;
using System.Collections.Generic;

namespace Shibafu.BehaviourTree.Serialization
{
    /// <summary>
    /// 保证整棵树内 <see cref="BTNodeDefinition.Id"/> 非空且互不相同。
    /// 重复 id 会导致运行时调试字典与编辑器节点一一映射错乱（多个节点显示同一状态）。
    /// </summary>
    public static class BTDefinitionDocumentIds
    {
        /// <summary>DFS 补全空 id，并将与已出现 id 重复的节点改为新 Guid。</summary>
        /// <returns>是否修改过任意节点的 <see cref="BTNodeDefinition.Id"/>。</returns>
        public static bool NormalizeUniqueIds(BTDefinitionDocument doc)
        {
            if (doc?.Root == null)
                return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return NormalizeRecursive(doc.Root, seen);
        }

        private static bool NormalizeRecursive(BTNodeDefinition node, HashSet<string> seen)
        {
            if (node == null)
                return false;

            var changed = false;
            var id = node.Id?.Trim();
            if (string.IsNullOrEmpty(id))
            {
                node.Id = Guid.NewGuid().ToString("N");
                changed = true;
                id = node.Id;
            }
            else if (seen.Contains(id))
            {
                var baseId = id;
                var n = 2;
                string candidate;
                do
                {
                    candidate = $"{baseId}__{n}";
                    n++;
                } while (seen.Contains(candidate));

                node.Id = candidate;
                changed = true;
                id = node.Id;
            }

            seen.Add(id);

            if (node.Children == null)
                return changed;

            foreach (var c in node.Children)
                changed |= NormalizeRecursive(c, seen);
            return changed;
        }
    }
}
