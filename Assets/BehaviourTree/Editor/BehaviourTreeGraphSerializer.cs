using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Shibafu.BehaviourTree.Serialization;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Shibafu.BehaviourTree.Editor
{
    /// <summary>
    /// GraphView 与 <see cref="BTDefinitionDocument"/> 互转。
    /// </summary>
    public static class BehaviourTreeGraphSerializer
    {
        /// <summary>与 <see cref="BTGraphNode.CompactNodeHeight"/> 一致，用于无坐标时的自动布局估算。</summary>
        private static readonly float NodeLayoutHeight = BTGraphNode.CompactNodeHeight;

        /// <summary>父节点顶到第一个子节点顶的垂直间距。</summary>
        private const float VerticalGap = 112f;

        /// <summary>同一父节点下兄弟子树之间的垂直间距。</summary>
        private const float SiblingVerticalGap = 28f;

        public static BTDefinitionDocument GraphToDocument(BehaviourTreeGraphView graph, out string error)
        {
            error = null;
            var nodes = graph.nodes.ToList().OfType<BTGraphNode>().ToList();
            if (nodes.Count == 0)
            {
                error = "图中没有节点。";
                return null;
            }

            var roots = FindRootNodes(graph, nodes);
            if (roots.Count != 1)
            {
                error = roots.Count == 0
                    ? "找不到根节点：应恰好有一个节点没有父连线（父节点的「子」口应连到子节点的「父」口）。"
                    : "只能有一个根节点（没有任何父连线的节点只能有一个）。";
                return null;
            }

            foreach (var n in nodes)
            {
                var v = n.ValidateForSave();
                if (v != null)
                {
                    error = v;
                    return null;
                }
            }

            try
            {
                var rootDef = BuildDefinition(roots[0]);
                return new BTDefinitionDocument
                {
                    FormatVersion = BTDefinitionLoader.SupportedFormatVersion,
                    Root = rootDef
                };
            }
            catch (JsonException ex)
            {
                error = ex.Message;
                return null;
            }
            catch (System.InvalidOperationException ex)
            {
                error = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// 根据边推断根：作为某条边 <c>input</c> 端点的节点视为有父；其余为根。
        /// （比仅依赖 Port.connections 枚举更稳妥，避免部分 Unity 版本下端口枚举不一致。）
        /// </summary>
        private static List<BTGraphNode> FindRootNodes(BehaviourTreeGraphView graph, List<BTGraphNode> nodes)
        {
            var hasParent = new HashSet<BTGraphNode>();
            foreach (var edge in graph.edges.ToList())
            {
                if (edge?.input?.node is BTGraphNode child)
                    hasParent.Add(child);
            }

            var roots = new List<BTGraphNode>();
            foreach (var n in nodes)
            {
                if (!hasParent.Contains(n))
                    roots.Add(n);
            }

            return roots;
        }

        private static BTNodeDefinition BuildDefinition(BTGraphNode node)
        {
            var type = node.EffectiveType;
            var children = new List<BTNodeDefinition>();
            if (node.OutputPort != null)
            {
                var ordered = node.OutputPort.connections
                    .Select(c => c.input.node as BTGraphNode)
                    .Where(n => n != null)
                    .OrderBy(n => n.GetPosition().yMin)
                    .ThenBy(n => n.GetPosition().xMin)
                    .ToList();
                var seenChild = new HashSet<BTGraphNode>();
                foreach (var child in ordered)
                {
                    if (!seenChild.Add(child))
                        continue;
                    children.Add(BuildDefinition(child));
                }
            }

            if (string.Equals(type, "sequence", System.StringComparison.Ordinal) ||
                string.Equals(type, "selector", System.StringComparison.Ordinal))
            {
                // 任意数量子节点
            }
            else if (string.Equals(type, "inverter", System.StringComparison.Ordinal))
            {
                if (children.Count != 1)
                    throw new System.InvalidOperationException("inverter 只能有一个子节点。");
            }
            else
            {
                if (children.Count != 0)
                    throw new System.InvalidOperationException($"{type} 不能有子节点。");
            }

            var rect = node.GetPosition();
            return new BTNodeDefinition
            {
                Type = type,
                Id = string.IsNullOrEmpty(node.DebugNodeId) ? null : node.DebugNodeId,
                Name = string.IsNullOrEmpty(node.BtName) ? null : node.BtName,
                EditorX = rect.xMin,
                EditorY = rect.yMin,
                Data = node.ParseDataObject(),
                Children = children.Count > 0 ? children : null
            };
        }

        /// <returns>是否为缺失节点补全了新的 <c>id</c>（可据此写回资产）。</returns>
        public static bool DocumentToGraph(BTDefinitionDocument doc, BehaviourTreeGraphView graph)
        {
            graph.ClearGraph();
            if (doc?.Root == null)
                return false;
            var changed = BTDefinitionDocumentIds.NormalizeUniqueIds(doc);
            PlaceRecursive(doc.Root, new Vector2(320, 48), graph, null, out _, out _, out _);
            return changed;
        }

        private static Vector2 ResolveEditorPosition(BTNodeDefinition def, Vector2 fallback)
        {
            if (def != null && def.EditorX.HasValue && def.EditorY.HasValue)
                return new Vector2(def.EditorX.Value, def.EditorY.Value);
            return fallback;
        }

        /// <param name="fallbackPosition">当 JSON 无 <see cref="BTNodeDefinition.EditorX"/>/<c>EditorY</c> 时使用的自动布局位置。</param>
        /// <param name="subtreeWidth">从本节点左边到子树最右端的水平跨度。</param>
        /// <param name="subtreeHeight">从本节点顶边到子树最底边的垂直跨度。</param>
        /// <param name="resolvedPosition">本节点实际采用的左上角坐标。</param>
        private static BTGraphNode PlaceRecursive(
            BTNodeDefinition def,
            Vector2 fallbackPosition,
            BehaviourTreeGraphView graph,
            BTGraphNode parent,
            out float subtreeWidth,
            out float subtreeHeight,
            out Vector2 resolvedPosition)
        {
            var pos = ResolveEditorPosition(def, fallbackPosition);
            resolvedPosition = pos;
            var node = graph.CreateLinkedNode(def, pos, parent);
            var list = def.Children;
            if (list == null || list.Count == 0)
            {
                subtreeWidth = BTGraphNode.DefaultWidth;
                subtreeHeight = NodeLayoutHeight;
                return node;
            }

            var autoCursorY = pos.y + VerticalGap;
            var maxRight = pos.x + BTGraphNode.DefaultWidth;
            var maxBottom = pos.y + NodeLayoutHeight;

            foreach (var child in list)
            {
                var childFallback = new Vector2(pos.x, autoCursorY);
                PlaceRecursive(child, childFallback, graph, node, out var childW, out var childH, out var childPos);
                maxRight = Mathf.Max(maxRight, childPos.x + childW);
                maxBottom = Mathf.Max(maxBottom, childPos.y + childH);
                autoCursorY = Mathf.Max(autoCursorY, childPos.y + childH + SiblingVerticalGap);
            }

            subtreeWidth = maxRight - pos.x;
            subtreeHeight = maxBottom - pos.y;
            return node;
        }
    }
}
