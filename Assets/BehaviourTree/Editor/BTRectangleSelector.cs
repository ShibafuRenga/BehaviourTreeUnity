using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shibafu.BehaviourTree.Editor
{
    /// <summary>
    /// 框选：在 <see cref="contentViewContainer"/> 坐标系内绘制矩形并选中相交的 <see cref="GraphElement"/>。
    /// 使用 <see cref="IMouseEvent.mousePosition"/>（面板坐标）换算到内容区，避免 GraphView 嵌套在工具栏/侧栏下时内置 <see cref="RectangleSelector"/> 的偏移。
    /// </summary>
    public sealed class BTRectangleSelector : MouseManipulator
    {
        private const float MinDragPx = 3f;

        private GraphView _graphView;
        private VisualElement _box;
        private Vector2 _startContent;
        private Vector2 _endContent;
        private bool _active;

        public BTRectangleSelector()
        {
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (!CanStartManipulation(evt))
                return;

            _graphView = target as GraphView;
            if (_graphView?.contentViewContainer == null)
                return;

            // 不能只用 evt.target：节点上 PickingMode.Ignore 区域会穿透到父级，误判为背景并 StopPropagation + Capture，导致单击选不中节点。
            if (IsPointerOverNodeOrEdge(_graphView, evt.mousePosition))
                return;

            _active = true;
            _startContent = PanelToContentLocal(_graphView, evt.mousePosition);
            _endContent = _startContent;

            EnsureBox();
            UpdateBoxVisual();
            _box.style.display = DisplayStyle.Flex;
            _box.BringToFront();

            target.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!_active || _graphView == null)
                return;

            _endContent = PanelToContentLocal(_graphView, evt.mousePosition);
            UpdateBoxVisual();
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (!_active)
                return;

            _active = false;
            _endContent = PanelToContentLocal(_graphView, evt.mousePosition);

            if (target.HasMouseCapture())
                target.ReleaseMouse();

            var rect = NormalizedRect(_startContent, _endContent);
            HideBox();

            if (rect.width >= MinDragPx || rect.height >= MinDragPx)
                ApplySelection(rect, evt.shiftKey);
            else if (!evt.shiftKey)
                _graphView.ClearSelection();

            evt.StopPropagation();
        }

        /// <summary>面板坐标下是否与任意节点/连线区域相交（覆盖节点内不参与拾取的空白）。</summary>
        private static bool IsPointerOverNodeOrEdge(GraphView gv, Vector2 panelMouse)
        {
            foreach (var node in gv.Query<Node>().ToList())
            {
                if (node == null || !node.enabledInHierarchy)
                    continue;
                if (node.worldBound.Contains(panelMouse))
                    return true;
            }

            foreach (var edge in gv.Query<Edge>().ToList())
            {
                if (edge == null || !edge.enabledInHierarchy)
                    continue;
                var r = edge.worldBound;
                if (r.width < 8f || r.height < 8f)
                    r = new Rect(r.xMin - 4f, r.yMin - 4f, Mathf.Max(r.width, 8f), Mathf.Max(r.height, 8f));
                if (r.Contains(panelMouse))
                    return true;
            }

            return false;
        }

        private void EnsureBox()
        {
            if (_box != null)
                return;

            _box = new VisualElement { name = "BT-RectangleSelect", pickingMode = PickingMode.Ignore };
            _box.style.position = Position.Absolute;
            _box.style.left = 0;
            _box.style.top = 0;
            _box.style.width = 0;
            _box.style.height = 0;
            _box.style.display = DisplayStyle.None;
            _box.style.backgroundColor = new Color(0.23f, 0.49f, 0.96f, 0.12f);
            _box.style.borderLeftWidth = 1;
            _box.style.borderRightWidth = 1;
            _box.style.borderTopWidth = 1;
            _box.style.borderBottomWidth = 1;
            _box.style.borderLeftColor =
                _box.style.borderRightColor =
                    _box.style.borderTopColor =
                        _box.style.borderBottomColor = new Color(0.23f, 0.49f, 0.96f, 0.85f);

            _graphView.contentViewContainer.Add(_box);
        }

        private void UpdateBoxVisual()
        {
            if (_box == null)
                return;

            var r = NormalizedRect(_startContent, _endContent);
            _box.style.left = r.xMin;
            _box.style.top = r.yMin;
            _box.style.width = r.width;
            _box.style.height = r.height;
        }

        private void HideBox()
        {
            if (_box != null)
                _box.style.display = DisplayStyle.None;
        }

        private static Vector2 PanelToContentLocal(GraphView gv, Vector2 panelMouse)
        {
            return gv.contentViewContainer.WorldToLocal(panelMouse);
        }

        private static Rect NormalizedRect(Vector2 a, Vector2 b)
        {
            var min = Vector2.Min(a, b);
            var max = Vector2.Max(a, b);
            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        private void ApplySelection(Rect selectionContent, bool additive)
        {
            var content = _graphView.contentViewContainer;
            var picked = new List<ISelectable>();

            foreach (var ge in _graphView.Query<GraphElement>().ToList())
            {
                if (ge == null || !ge.IsSelectable() || !ge.enabledInHierarchy)
                    continue;

                if (!SelectionRectOverlapsElement(selectionContent, ge, content))
                    continue;

                if (ge is ISelectable sel)
                    picked.Add(sel);
            }

            if (!additive)
                _graphView.ClearSelection();

            foreach (var s in picked)
                _graphView.AddToSelection(s);
        }

        private static bool SelectionRectOverlapsElement(Rect selectionContent, GraphElement ge, VisualElement contentRoot)
        {
            var wb = ge.worldBound;
            var tl = contentRoot.WorldToLocal(wb.min);
            var br = contentRoot.WorldToLocal(wb.max);
            var r = Rect.MinMaxRect(
                Mathf.Min(tl.x, br.x),
                Mathf.Min(tl.y, br.y),
                Mathf.Max(tl.x, br.x),
                Mathf.Max(tl.y, br.y));

            return selectionContent.Overlaps(r, true);
        }
    }
}
