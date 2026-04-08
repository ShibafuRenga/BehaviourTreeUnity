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
        public BehaviourTreeGraphView()
        {
            style.flexGrow = 1;

            Insert(0, new GridBackground());
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var miniMap = new MiniMap { anchored = true };
            miniMap.SetPosition(new Rect(12, 48, 200, 120));
            Add(miniMap);

            this.AddManipulator(new ContextualMenuManipulator(BuildBackgroundMenu));

            focusable = true;
            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        public void OnDrop(GraphView graphView, Edge edge)
        {
            if (edge == null || edge.input == null || edge.output == null)
                return;
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
                ? BTEditorNodeTypeDiscovery.GetOrderedEntries()
                : BTEditorNodeTypeDiscovery.GetOrderedEntriesWithChildPortOnly();

            if (!fromOutput && entries.Count == 0)
            {
                EditorUtility.DisplayDialog("行为树编辑器", "没有可作为父节点的类型。", "确定");
                return;
            }

            foreach (var e in entries)
            {
                var tid = e.TypeId;
                menu.AddItem(new GUIContent(e.DisplayName), false,
                    () => ScheduleWireSpawn(anchor, fromOutput, graphLocal, tid, false));
            }

            if (fromOutput)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("自定义类型…"), false,
                    () => ScheduleWireSpawn(anchor, fromOutput, graphLocal, null, true));
            }

            menu.ShowAsContext();
        }

        private void ScheduleWireSpawn(Port anchor, bool fromOutput, Vector2 graphLocal, string typeId, bool customSlot)
        {
            EditorApplication.delayCall += () =>
            {
                var spawnPos = graphLocal + new Vector2(28f, 28f);
                var node = CreateNodeAt(spawnPos);
                if (customSlot)
                    node.ApplySpawnCustomSlot();
                else
                    node.ApplySpawnPresetType(typeId);
                node.RefreshChildOutputPort(this);

                if (fromOutput)
                    ConnectPorts(anchor, node.InputPort);
                else if (node.OutputPort != null)
                    ConnectPorts(node.OutputPort, anchor);
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

            evt.menu.AppendAction("添加节点", _ => CreateNodeAt(spawnLocal));
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList().Where(p =>
                p.node != startPort.node &&
                p.direction != startPort.direction).ToList();
        }

        public void ClearGraph()
        {
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

        public BTGraphNode CreateNodeAt(Vector2 graphLocalPosition)
        {
            var node = new BTGraphNode();
            node.SetPosition(new Rect(
                graphLocalPosition.x,
                graphLocalPosition.y,
                BTGraphNode.DefaultWidth,
                200));
            AddElement(node);
            node.RefreshChildOutputPort(this);
            return node;
        }

        public void ConnectPorts(Port output, Port input)
        {
            if (output == null || input == null)
                return;
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
                200));
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
