using System.Collections.Generic;
using Shibafu.BehaviourTree.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shibafu.BehaviourTree.Editor
{
    /// <summary>
    /// 左侧属性面板：编辑选中 <see cref="BTGraphNode"/> 的类型、名称与 data（Inspector 字段）。
    /// </summary>
    public sealed class BTNodeInspectorPanel : VisualElement
    {
        private BTGraphNode _bound;
        private BTDefinitionScriptableObject _definitionForBindings;

        private readonly Label _headerLabel;
        private readonly DropdownField _typeDropdown;
        private readonly TextField _nameField;
        private readonly IMGUIContainer _dataImGui;
        private readonly Label _idLabel;

        public BTNodeInspectorPanel()
        {
            style.flexShrink = 0;

            BTEditorNodeTypeDiscovery.BuildChoiceLists(out var labels, out _);
            _typeDropdown = new DropdownField("类型", new List<string>(labels), 0);
            _typeDropdown.RegisterValueChangedCallback(_ => OnTypeChanged());

            _nameField = new TextField("名称");
            _nameField.RegisterValueChangedCallback(_ => OnNameChanged());

            _dataImGui = new IMGUIContainer(OnInspectorDataImGui);
            _dataImGui.style.flexShrink = 0;
            _dataImGui.style.minHeight = 4f;

            _headerLabel = new Label("节点属性");
            _headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _headerLabel.style.marginBottom = 8;

            _idLabel = new Label();
            _idLabel.style.fontSize = 10;
            _idLabel.style.color = new Color(0.65f, 0.65f, 0.65f);
            _idLabel.style.whiteSpace = WhiteSpace.Normal;
            _idLabel.style.marginTop = 6;

            var hint = new Label("在图中选中一个节点以编辑。");
            hint.name = "bt-inspector-hint";
            hint.style.color = new Color(0.55f, 0.55f, 0.55f);
            hint.style.whiteSpace = WhiteSpace.Normal;

            Add(_headerLabel);
            Add(hint);
            Add(_typeDropdown);
            Add(_nameField);
            Add(_dataImGui);
            Add(_idLabel);

            Bind(null, null);
        }

        public void Bind(BTGraphNode node, BTDefinitionScriptableObject definitionForBindings = null)
        {
            _bound = node;
            _definitionForBindings = definitionForBindings;
            var hint = this.Q<Label>("bt-inspector-hint");
            var editing = node != null;

            _typeDropdown.SetEnabled(editing);
            _nameField.SetEnabled(editing);
            _dataImGui.SetEnabled(editing);

            if (node == null)
            {
                if (hint != null)
                    hint.style.display = DisplayStyle.Flex;
                _headerLabel.text = "节点属性";
                _idLabel.text = "";
                return;
            }

            if (hint != null)
                hint.style.display = DisplayStyle.None;

            var labels = node.EditorTypeChoiceLabels;
            if (labels.Count != _typeDropdown.choices.Count)
                _typeDropdown.choices = new List<string>(labels);

            var idx = Mathf.Clamp(node.TypeChoiceIndex, 0, _typeDropdown.choices.Count - 1);
            _typeDropdown.SetValueWithoutNotify(_typeDropdown.choices[idx]);
            _nameField.SetValueWithoutNotify(node.NodeNameText);

            _headerLabel.text = $"节点 — {node.title}";
            _idLabel.text = $"id: {node.DebugNodeId}";

            _dataImGui.MarkDirtyRepaint();
        }

        private void OnTypeChanged()
        {
            if (_bound == null)
                return;
            _bound.TypeChoiceIndex = _typeDropdown.index;
            _headerLabel.text = $"节点 — {_bound.title}";
            _dataImGui.MarkDirtyRepaint();
        }

        private void OnNameChanged()
        {
            if (_bound == null)
                return;
            _bound.NodeNameText = _nameField.value;
            _headerLabel.text = $"节点 — {_bound.title}";
        }

        private void OnInspectorDataImGui()
        {
            if (_bound == null)
                return;

            var json = _bound.DataJsonText ?? "";
            BTNodeInspectorDataImGui.Draw(_bound.EffectiveType, ref json, out var changed, _definitionForBindings);
            if (changed)
                _bound.DataJsonText = json;
        }
    }
}
