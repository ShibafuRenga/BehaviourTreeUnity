using System.Collections.Generic;
using System.Linq;
using Shibafu.BehaviourTree;
using Shibafu.BehaviourTree.Serialization;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shibafu.BehaviourTree.Editor
{
    /// <summary>
    /// 行为树 GraphView：父子连线；空白处松线可选类型创建节点。
    /// </summary>
    public sealed class BehaviourTreeGraphView : GraphView, IEdgeConnectorListener
    {
        /// <summary>
        /// <see cref="ClearGraph"/> 时递增。延迟创建节点（<see cref="EditorApplication.delayCall"/>）若晚于图被清空/重载，则世代已变，应丢弃以免多出幽灵节点。
        /// </summary>
        private int _graphContentEpoch;

        public BehaviourTreeGraphView()
        {
            style.flexGrow = 1;

            Insert(0, new GridBackground());
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new BTRectangleSelector());

            this.AddManipulator(new ContextualMenuManipulator(BuildBackgroundMenu));

            focusable = true;
            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        public void OnDrop(GraphView graphView, Edge edge)
        {
            if (edge == null || edge.input == null || edge.output == null)
                return;

            // 与 Port 默认 DefaultEdgeConnectorListener 一致：Single 端口上新连线前删掉旧边。
            // 否则 m_Connections 里会叠多条边，保存时同一子节点会在 children 里出现多次。
            var toDelete = new List<GraphElement>();
            if (edge.input.capacity == Port.Capacity.Single)
            {
                foreach (var e in edge.input.connections.ToList())
                {
                    if (e != edge)
                        toDelete.Add(e);
                }
            }

            if (edge.output.capacity == Port.Capacity.Single)
            {
                foreach (var e in edge.output.connections.ToList())
                {
                    if (e != edge)
                        toDelete.Add(e);
                }
            }

            if (toDelete.Count > 0)
                graphView.DeleteElements(toDelete);

            graphView.AddElement(edge);
            edge.input.Connect(edge);
            edge.output.Connect(edge);
        }

        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            if (edge == null)
                return;

            RemoveElement(edge);

            Port anchor;
            bool fromOutput;
            if (edge.output != null)
            {
                anchor = edge.output;
                fromOutput = true;
            }
            else if (edge.input != null)
            {
                anchor = edge.input;
                fromOutput = false;
            }
            else
                return;

            var graphLocal = contentViewContainer.WorldToLocal(position);
            BuildAndShowWireMenu(anchor, fromOutput, graphLocal);
        }

        private void BuildAndShowWireMenu(Port anchor, bool fromOutput, Vector2 graphLocal)
        {
            var menu = new GenericMenu();
            var entries = fromOutput
                ? BTEditorNodeTypeDiscovery.GetEditorTypeEntries()
                : BTEditorNodeTypeDiscovery.GetEditorTypeEntriesWithChildPortOnly();

            if (!fromOutput && entries.Count == 0)
            {
                EditorUtility.DisplayDialog("行为树编辑器", "没有可作为父节点的类型。", "确定");
                return;
            }

            BTEditorNodeTypeDiscovery.PopulateGenericMenuCreateNodes(
                menu,
                tid => ScheduleWireSpawn(anchor, fromOutput, graphLocal, tid),
                entries);
            menu.ShowAsContext();
        }

        private void ScheduleWireSpawn(Port anchor, bool fromOutput, Vector2 graphLocal, string typeId)
        {
            var epochAtSchedule = _graphContentEpoch;
            EditorApplication.delayCall += () =>
            {
                if (epochAtSchedule != _graphContentEpoch)
                    return;
                if (anchor?.node == null || !nodes.Contains(anchor.node))
                    return;

                var spawnPos = graphLocal + new Vector2(28f, 28f);
                var node = CreateNodeAt(spawnPos, typeId);

                if (fromOutput)
                    ConnectPorts(anchor, node.InputPort);
                else if (node.OutputPort != null)
                    ConnectPorts(node.OutputPort, anchor);

                ClearSelection();
                AddToSelection(node);
            };
        }

        private bool IsTextEditingFocused()
        {
            var ve = panel?.focusController?.focusedElement as VisualElement;
            if (ve == null)
                return false;
            if (ve is TextField || ve.GetFirstAncestorOfType<TextField>() != null)
                return true;
            if (ve is DropdownField || ve.GetFirstAncestorOfType<DropdownField>() != null)
                return true;
            return false;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
                return;
            if (IsTextEditingFocused())
                return;

            var toRemove = new HashSet<GraphElement>();
            foreach (var sel in selection.ToList())
            {
                switch (sel)
                {
                    case BTGraphNode n:
                        toRemove.Add(n);
                        foreach (var e in n.InputPort.connections.ToList())
                            toRemove.Add(e);
                        if (n.OutputPort != null)
                        {
                            foreach (var e in n.OutputPort.connections.ToList())
                                toRemove.Add(e);
                        }

                        break;
                    case Edge ed:
                        toRemove.Add(ed);
                        break;
                }
            }

            if (toRemove.Count == 0)
                return;

            evt.StopPropagation();
            evt.PreventDefault();
            DeleteElements(toRemove);
        }

        private void BuildBackgroundMenu(ContextualMenuPopulateEvent evt)
        {
            var targetVe = evt.target as VisualElement;
            if (evt.target is BTGraphNode ||
                (targetVe != null && targetVe.GetFirstAncestorOfType<BTGraphNode>() != null))
                return;

            Vector2 spawnLocal;
            if (targetVe != null)
            {
                var world = targetVe.LocalToWorld(evt.localMousePosition);
                spawnLocal = contentViewContainer.WorldToLocal(world);
            }
            else
                spawnLocal = new Vector2(400f, 200f);

            BTEditorNodeTypeDiscovery.AppendCreateNodeActions(
                evt.menu,
                tid =>
                {
                    var epochAtPick = _graphContentEpoch;
                    EditorApplication.delayCall += () =>
                    {
                        if (epochAtPick != _graphContentEpoch)
                            return;
                        CreateNodeAt(spawnLocal, tid);
                    };
                });
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList().Where(p =>
                p.node != startPort.node &&
                p.direction != startPort.direction).ToList();
        }

        public void ClearGraph()
        {
            _graphContentEpoch++;
            ApplyRuntimeDebugStatuses(null);
            edges.ToList().ForEach(RemoveElement);
            nodes.ToList().ForEach(RemoveElement);
        }

        /// <summary>Play 模式下根据 Runner 上报的 id→状态刷新节点左侧条；<paramref name="statuses"/> 为 null 时清除高亮。</summary>
        public void ApplyRuntimeDebugStatuses(IReadOnlyDictionary<string, BTStatus> statuses)
        {
            foreach (var n in nodes.ToList().OfType<BTGraphNode>())
            {
                if (statuses == null || string.IsNullOrEmpty(n.DebugNodeId))
                {
                    n.SetRuntimeDebugStatus(null);
                    continue;
                }

                if (statuses.TryGetValue(n.DebugNodeId, out var s))
                    n.SetRuntimeDebugStatus(s);
                else
                    n.SetRuntimeDebugStatus(null);
            }
        }

        /// <param name="presetTypeId">为 null 或空白时保留节点构造默认（一般为 sequence）。</param>
        public BTGraphNode CreateNodeAt(Vector2 graphLocalPosition, string presetTypeId = null)
        {
            var node = new BTGraphNode();
            node.SetPosition(new Rect(
                graphLocalPosition.x,
                graphLocalPosition.y,
                BTGraphNode.DefaultWidth,
                BTGraphNode.CompactNodeHeight));
            AddElement(node);
            if (!string.IsNullOrWhiteSpace(presetTypeId))
                node.ApplySpawnPresetType(presetTypeId);
            node.RefreshChildOutputPort(this);
            ClearSelection();
            AddToSelection(node);
            return node;
        }

        public void ConnectPorts(Port output, Port input)
        {
            if (output == null || input == null)
                return;
            if (input.capacity == Port.Capacity.Single)
            {
                var existing = input.connections.ToList();
                if (existing.Count > 0)
                    DeleteElements(existing);
            }

            if (output.capacity == Port.Capacity.Single)
            {
                var existing = output.connections.ToList();
                if (existing.Count > 0)
                    DeleteElements(existing);
            }

            var edge = new Edge { output = output, input = input };
            output.Connect(edge);
            input.Connect(edge);
            AddElement(edge);
        }

        /// <summary>创建节点并可选用边接到父节点（父 → 本节点）。</summary>
        public BTGraphNode CreateLinkedNode(BTNodeDefinition def,
            Vector2 graphLocalPosition,
            BTGraphNode parent)
        {
            var node = new BTGraphNode();
            node.SetPosition(new Rect(
                graphLocalPosition.x,
                graphLocalPosition.y,
                BTGraphNode.DefaultWidth,
                BTGraphNode.CompactNodeHeight));
            AddElement(node);
            node.ApplyFromDefinition(def);
            node.RefreshChildOutputPort(this);
            if (parent != null)
            {
                if (parent.OutputPort == null)
                    throw new System.InvalidOperationException(
                        "父节点类型没有「子」端口，无法建立父子连线。");
                ConnectPorts(parent.OutputPort, node.InputPort);
            }

            return node;
        }
    }
}
