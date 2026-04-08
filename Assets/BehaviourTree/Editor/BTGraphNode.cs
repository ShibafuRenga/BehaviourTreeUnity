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
    /// 图中的一个行为树节点：类型（由 <see cref="BTNodeTypeAttribute"/> 扫描）、名称、data JSON、父端口；仅组合类显示「子」端口。
    /// </summary>
    public sealed class BTGraphNode : Node
    {
        public const float DefaultWidth = 240f;

        private readonly List<string> _choiceLabels;
        private readonly List<string> _typeIds;
        /// <summary>构造时下拉默认项（通常为 sequence）；<see cref="DropdownField.index"/> 常为 -1 时不得用 Clamp 成 0。</summary>
        private readonly int _defaultSequenceIndex;
        private int CustomSlotIndex => _choiceLabels.Count - 1;

        private readonly Port _inputPort;
        private Port _outputPort;

        private readonly DropdownField _typeDropdown;
        private readonly TextField _customTypeField;
        private readonly TextField _nameField;
        private readonly TextField _dataField;

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
                if (_typeIds[i] != null && _typeIds[i].Equals("sequence", System.StringComparison.Ordinal))
                {
                    defaultIndex = i;
                    break;
                }
            }

            _defaultSequenceIndex = defaultIndex;

            _typeDropdown = new DropdownField("类型", _choiceLabels, defaultIndex);
            _typeDropdown.RegisterValueChangedCallback(_ =>
            {
                RefreshCustomTypeVisibility();
                SyncTitleWithNameOrType();
                RequestRefreshChildPort();
            });

            _customTypeField = new TextField("自定义 type");
            _customTypeField.RegisterValueChangedCallback(_ =>
            {
                SyncTitleWithNameOrType();
                RequestRefreshChildPort();
            });
            _customTypeField.style.display = DisplayStyle.None;

            _nameField = new TextField("名称");
            _nameField.RegisterValueChangedCallback(evt =>
            {
                title = string.IsNullOrWhiteSpace(evt.newValue) ? EffectiveType : evt.newValue.Trim();
            });

            _dataField = new TextField("data (JSON)")
            {
                multiline = true
            };
            _dataField.style.minHeight = 64;
            _dataField.style.whiteSpace = WhiteSpace.Normal;

            extensionContainer.Add(_typeDropdown);
            extensionContainer.Add(_customTypeField);
            extensionContainer.Add(_nameField);
            extensionContainer.Add(_dataField);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(_ => StopRuntimeDebugFade(resetStrip: true));

            RefreshPorts();
            RefreshExpandedState();
            RefreshCustomTypeVisibility();
        }

        public Port InputPort => _inputPort;
        public Port OutputPort => _outputPort;

        public string DebugNodeId => _debugNodeId;

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

        /// <summary>挂到面板后绑定连线监听；若当时还拿不到 GraphView 则下一帧重试。</summary>
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

        /// <summary>
        /// 解析当前选中的类型下标。部分 Unity 版本里 <see cref="DropdownField.index"/> 在创建后或
        /// <see cref="DropdownField.SetValueWithoutNotify"/> 之后仍为 -1；若用 <c>Clamp(-1)=0</c> 会落到排序后的第一项（常为 action），
        /// <see cref="RefreshChildOutputPort"/> 会误判为叶子并拆掉「子」口。优先有效 index，否则用 value 对齐 choices，再回退 sequence 默认项。
        /// </summary>
        private int SelectedTypeIndex()
        {
            var n = _choiceLabels.Count;
            if (n == 0)
                return 0;
            var idx = _typeDropdown.index;
            if (idx >= 0 && idx < n)
                return idx;
            var v = _typeDropdown.value;
            if (!string.IsNullOrEmpty(v))
            {
                var byLabel = _choiceLabels.IndexOf(v);
                if (byLabel >= 0)
                    return byLabel;
            }

            return Mathf.Clamp(_defaultSequenceIndex, 0, n - 1);
        }

        private bool IsCustomTypeSelected() => SelectedTypeIndex() == CustomSlotIndex;

        public string EffectiveType
        {
            get
            {
                if (IsCustomTypeSelected())
                {
                    var c = _customTypeField.value?.Trim();
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

        public string BtName => _nameField.value?.Trim() ?? "";

        public string DataJsonText => _dataField.value?.Trim() ?? "";

        private void SyncTitleWithNameOrType()
        {
            title = string.IsNullOrWhiteSpace(_nameField.value) ? EffectiveType : _nameField.value.Trim();
        }

        /// <summary>保存前校验；通过返回 null。</summary>
        public string ValidateForSave()
        {
            if (IsCustomTypeSelected() && string.IsNullOrWhiteSpace(_customTypeField.value))
                return $"选择「{BTEditorNodeTypeDiscovery.CustomTypeMenuLabel}」时必须填写「自定义 type」。";
            return null;
        }

        /// <summary>从已知 typeId 设置下拉（用于拖线创建）。</summary>
        public void ApplySpawnPresetType(string typeId)
        {
            ApplyTypeIdString(string.IsNullOrWhiteSpace(typeId) ? "sequence" : typeId.Trim());
            SyncTitleWithNameOrType();
            RequestRefreshChildPort();
        }

        /// <summary>拖线创建时切到「自定义」槽位，由用户在面板填写 type。</summary>
        public void ApplySpawnCustomSlot()
        {
            _typeDropdown.SetValueWithoutNotify(_choiceLabels[CustomSlotIndex]);
            _customTypeField.SetValueWithoutNotify("");
            RefreshCustomTypeVisibility();
            SyncTitleWithNameOrType();
            RequestRefreshChildPort();
        }

        private void ApplyTypeIdString(string t)
        {
            var idx = -1;
            for (var i = 0; i < _typeIds.Count - 1; i++)
            {
                var id = _typeIds[i];
                if (id != null && id.Equals(t, System.StringComparison.Ordinal))
                {
                    idx = i;
                    break;
                }
            }

            if (idx >= 0)
            {
                _typeDropdown.SetValueWithoutNotify(_choiceLabels[idx]);
                _customTypeField.SetValueWithoutNotify("");
            }
            else
            {
                _typeDropdown.SetValueWithoutNotify(_choiceLabels[CustomSlotIndex]);
                _customTypeField.SetValueWithoutNotify(t);
            }

            RefreshCustomTypeVisibility();
        }

        public void ApplyFromDefinition(BTNodeDefinition def)
        {
            if (def == null)
                return;

            _debugNodeId = string.IsNullOrWhiteSpace(def.Id) ? Guid.NewGuid().ToString("N") : def.Id.Trim();

            var t = string.IsNullOrWhiteSpace(def.Type) ? "sequence" : def.Type.Trim();
            ApplyTypeIdString(t);

            _nameField.SetValueWithoutNotify(def.Name ?? "");
            title = string.IsNullOrWhiteSpace(def.Name) ? EffectiveType : def.Name;

            if (def.Data == null || !def.Data.HasValues)
                _dataField.SetValueWithoutNotify("");
            else
                _dataField.SetValueWithoutNotify(def.Data.ToString(Formatting.Indented));

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

        private void RefreshCustomTypeVisibility()
        {
            _customTypeField.style.display =
                IsCustomTypeSelected() ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
