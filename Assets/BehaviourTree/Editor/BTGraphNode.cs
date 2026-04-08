using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shibafu.BehaviourTree;
using Shibafu.BehaviourTree.Serialization;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shibafu.BehaviourTree.Editor
{
    /// <summary>
    /// 图节点：标题显示名称、父/子端口、运行时状态色条；类型与 data 在左侧属性面板编辑。
    /// </summary>
    public sealed class BTGraphNode : Node
    {
        public const float DefaultWidth = 200f;
        public const float CompactNodeHeight = 88f;

        private readonly List<string> _choiceLabels;
        private readonly List<string> _typeIds;
        private readonly int _defaultSequenceIndex;
        private int CustomSlotIndex => _choiceLabels.Count - 1;

        private readonly Port _inputPort;
        private Port _outputPort;

        /// <summary>下拉项下标，与 <see cref="BTEditorNodeTypeDiscovery.BuildChoiceLists"/> 一致。</summary>
        private int _typeChoiceIndex;

        private string _customTypeText = "";
        private string _nodeName = "";
        private string _dataJsonText = "";

        /// <summary>与 JSON <c>id</c>、运行时 <see cref="BTNode.DebugNodeId"/> 对齐。</summary>
        private string _debugNodeId;

        private bool _runtimeDebugStripActive;
        private Color _debugLastOpaqueColor = Color.clear;
        private IVisualElementScheduledItem _debugFadeSchedule;
        private double _debugFadeStartTime;
        private const float DebugFadeOutSeconds = 0.35f;

        public BTGraphNode()
        {
            _debugNodeId = Guid.NewGuid().ToString("N");
            title = "节点";
            mainContainer.style.minWidth = DefaultWidth;

            _inputPort = InstantiatePort(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(Port));
            _inputPort.portName = "父";
            inputContainer.Add(_inputPort);

            BTEditorNodeTypeDiscovery.BuildChoiceLists(out var labels, out var typeIds);
            _choiceLabels = labels;
            _typeIds = typeIds;

            var defaultIndex = 0;
            for (var i = 0; i < _typeIds.Count - 1; i++)
            {
                if (_typeIds[i] != null && _typeIds[i].Equals("sequence", StringComparison.Ordinal))
                {
                    defaultIndex = i;
                    break;
                }
            }

            _defaultSequenceIndex = defaultIndex;
            _typeChoiceIndex = defaultIndex;

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(_ => StopRuntimeDebugFade(resetStrip: true));

            RefreshPorts();
            RefreshExpandedState();
            SyncTitleWithNameOrType();
        }

        public Port InputPort => _inputPort;
        public Port OutputPort => _outputPort;

        public string DebugNodeId => _debugNodeId;

        /// <summary>供左侧属性面板绑定：当前类型下拉下标。</summary>
        public int TypeChoiceIndex
        {
            get => SelectedTypeIndex();
            set
            {
                var n = _choiceLabels.Count;
                _typeChoiceIndex = n == 0 ? 0 : Mathf.Clamp(value, 0, n - 1);
                SyncTitleWithNameOrType();
                RequestRefreshChildPort();
            }
        }

        /// <summary>自定义 type 文本（仅在选择「自定义类型…」时有效）。</summary>
        public string CustomTypeText
        {
            get => _customTypeText ?? "";
            set
            {
                _customTypeText = value ?? "";
                SyncTitleWithNameOrType();
                RequestRefreshChildPort();
            }
        }

        /// <summary>逻辑名称（JSON name）。</summary>
        public string NodeNameText
        {
            get => _nodeName ?? "";
            set
            {
                _nodeName = value ?? "";
                SyncTitleWithNameOrType();
            }
        }

        /// <summary>data 字段 JSON 文本。</summary>
        public string DataJsonText
        {
            get => _dataJsonText?.Trim() ?? "";
            set => _dataJsonText = value ?? "";
        }

        /// <summary>Play 模式下由 <see cref="BehaviourTreeGraphView.ApplyRuntimeDebugStatuses"/> 驱动左侧条颜色。</summary>
        public void SetRuntimeDebugStatus(BTStatus? status)
        {
            if (status == null)
            {
                if (_debugFadeSchedule != null)
                    return;
                if (_runtimeDebugStripActive)
                    StartRuntimeDebugFadeOut();
                return;
            }

            StopRuntimeDebugFade(resetStrip: false);
            _runtimeDebugStripActive = true;
            _debugLastOpaqueColor = status.Value switch
            {
                BTStatus.Success => new Color(0.2f, 0.72f, 0.35f, 1f),
                BTStatus.Failure => new Color(0.9f, 0.28f, 0.22f, 1f),
                BTStatus.Running => new Color(0.35f, 0.55f, 0.95f, 1f),
                _ => new Color(0.5f, 0.5f, 0.5f, 1f)
            };

            var box = mainContainer;
            box.style.borderLeftWidth = 4;
            box.style.borderLeftColor = _debugLastOpaqueColor;
        }

        private void StopRuntimeDebugFade(bool resetStrip)
        {
            _debugFadeSchedule?.Pause();
            _debugFadeSchedule = null;
            if (resetStrip)
            {
                _runtimeDebugStripActive = false;
                mainContainer.style.borderLeftWidth = 0;
            }
        }

        private void StartRuntimeDebugFadeOut()
        {
            StopRuntimeDebugFade(resetStrip: false);
            var box = mainContainer;
            box.style.borderLeftWidth = 4;
            var c = _debugLastOpaqueColor;
            c.a = 1f;
            box.style.borderLeftColor = c;
            _debugFadeStartTime = EditorApplication.timeSinceStartup;
            _debugFadeSchedule = schedule.Execute(RuntimeDebugFadeStep).Every(16);
        }

        private void RuntimeDebugFadeStep()
        {
            var elapsed = EditorApplication.timeSinceStartup - _debugFadeStartTime;
            if (elapsed >= DebugFadeOutSeconds)
            {
                _debugFadeSchedule?.Pause();
                _debugFadeSchedule = null;
                _runtimeDebugStripActive = false;
                mainContainer.style.borderLeftWidth = 0;
                return;
            }

            var k = 1f - (float)(elapsed / DebugFadeOutSeconds);
            var c = _debugLastOpaqueColor;
            c.a = k;
            mainContainer.style.borderLeftColor = c;
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            WirePortsWhenAttached();
        }

        internal void WirePortsWhenAttached()
        {
            var gv = GetFirstAncestorOfType<BehaviourTreeGraphView>();
            if (gv == null)
            {
                schedule.Execute(() =>
                {
                    var g = GetFirstAncestorOfType<BehaviourTreeGraphView>();
                    if (g == null)
                        return;
                    BTPortEdgeConnectorUtility.ReplaceWithListener(_inputPort, g);
                    RefreshChildOutputPort(g);
                }).ExecuteLater(0);
                return;
            }

            BTPortEdgeConnectorUtility.ReplaceWithListener(_inputPort, gv);
            RefreshChildOutputPort(gv);
        }

        private void RequestRefreshChildPort()
        {
            var gv = GetFirstAncestorOfType<BehaviourTreeGraphView>();
            if (gv != null)
                RefreshChildOutputPort(gv);
            else
            {
                schedule.Execute(() =>
                {
                    var g = GetFirstAncestorOfType<BehaviourTreeGraphView>();
                    if (g != null)
                        RefreshChildOutputPort(g);
                }).ExecuteLater(0);
            }
        }

        /// <summary>同步「子」端口显隐与连线清理。</summary>
        public void RefreshChildOutputPort(BehaviourTreeGraphView gv)
        {
            if (gv == null)
                return;

            var show = BTGraphEditorNodeLayout.ShowsChildPort(EffectiveType);
            if (show)
            {
                if (_outputPort == null)
                {
                    _outputPort = InstantiatePort(Orientation.Vertical, Direction.Output, Port.Capacity.Multi, typeof(Port));
                    _outputPort.portName = "子";
                    outputContainer.Add(_outputPort);
                    BTPortEdgeConnectorUtility.ReplaceWithListener(_outputPort, gv);
                    RefreshPorts();
                }
            }
            else
            {
                if (_outputPort != null)
                {
                    foreach (var e in _outputPort.connections.ToList())
                        gv.RemoveElement(e);
                    outputContainer.Remove(_outputPort);
                    _outputPort = null;
                }
            }
        }

        private int SelectedTypeIndex()
        {
            var n = _choiceLabels.Count;
            if (n == 0)
                return 0;
            return Mathf.Clamp(_typeChoiceIndex, 0, n - 1);
        }

        private bool IsCustomTypeSelected() => SelectedTypeIndex() == CustomSlotIndex;

        public string EffectiveType
        {
            get
            {
                if (IsCustomTypeSelected())
                {
                    var c = _customTypeText?.Trim();
                    if (!string.IsNullOrEmpty(c))
                        return c;
                    return "sequence";
                }

                var idx = SelectedTypeIndex();
                var id = _typeIds[idx];
                return !string.IsNullOrEmpty(id) ? id : FirstRegisteredTypeId() ?? "sequence";
            }
        }

        private static string FirstRegisteredTypeId()
        {
            var list = BTEditorNodeTypeDiscovery.GetOrderedEntries();
            return list.Count > 0 ? list[0].TypeId : null;
        }

        public string BtName => _nodeName?.Trim() ?? "";

        private void SyncTitleWithNameOrType()
        {
            title = string.IsNullOrWhiteSpace(_nodeName) ? EffectiveType : _nodeName.Trim();
        }

        /// <summary>保存前校验；通过返回 null。</summary>
        public string ValidateForSave()
        {
            if (IsCustomTypeSelected() && string.IsNullOrWhiteSpace(_customTypeText))
                return $"选择「{BTEditorNodeTypeDiscovery.CustomTypeMenuLabel}」时必须填写「自定义 type」。";
            return null;
        }

        /// <summary>从已知 typeId 设置（用于拖线创建）。</summary>
        public void ApplySpawnPresetType(string typeId)
        {
            ApplyTypeIdString(string.IsNullOrWhiteSpace(typeId) ? "sequence" : typeId.Trim());
            SyncTitleWithNameOrType();
            RequestRefreshChildPort();
        }

        /// <summary>拖线创建时切到「自定义」槽位。</summary>
        public void ApplySpawnCustomSlot()
        {
            _typeChoiceIndex = CustomSlotIndex;
            _customTypeText = "";
            SyncTitleWithNameOrType();
            RequestRefreshChildPort();
        }

        private void ApplyTypeIdString(string t)
        {
            var idx = -1;
            for (var i = 0; i < _typeIds.Count - 1; i++)
            {
                var id = _typeIds[i];
                if (id != null && id.Equals(t, StringComparison.Ordinal))
                {
                    idx = i;
                    break;
                }
            }

            if (idx >= 0)
            {
                _typeChoiceIndex = idx;
                _customTypeText = "";
            }
            else
            {
                _typeChoiceIndex = CustomSlotIndex;
                _customTypeText = t;
            }
        }

        public void ApplyFromDefinition(BTNodeDefinition def)
        {
            if (def == null)
                return;

            _debugNodeId = string.IsNullOrWhiteSpace(def.Id) ? Guid.NewGuid().ToString("N") : def.Id.Trim();

            var t = string.IsNullOrWhiteSpace(def.Type) ? "sequence" : def.Type.Trim();
            ApplyTypeIdString(t);

            _nodeName = def.Name ?? "";
            if (def.Data == null || !def.Data.HasValues)
                _dataJsonText = "";
            else
                _dataJsonText = def.Data.ToString(Formatting.Indented);

            SyncTitleWithNameOrType();
            RequestRefreshChildPort();
        }

        public JObject ParseDataObject()
        {
            var raw = DataJsonText;
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            try
            {
                var token = JToken.Parse(raw);
                if (token is JObject o)
                    return o;
                throw new JsonException("data 必须是 JSON 对象 { ... }，不能是数组或标量。");
            }
            catch (JsonException ex)
            {
                throw new JsonException($"节点「{title}」的 data JSON 无效: {ex.Message}", ex);
            }
        }

        /// <summary>属性面板用：与 <see cref="TypeChoiceIndex"/> 对应的下拉标签列表（含自定义项）。</summary>
        public IReadOnlyList<string> EditorTypeChoiceLabels => _choiceLabels;
    }
}
