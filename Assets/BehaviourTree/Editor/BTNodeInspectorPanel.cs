using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shibafu.BehaviourTree.Editor
{
    /// <summary>
    /// 左侧属性面板：编辑选中 <see cref="BTGraphNode"/> 的类型、名称、data JSON。
    /// </summary>
    public sealed class BTNodeInspectorPanel : VisualElement
    {
        private BTGraphNode _bound;

        private readonly Label _headerLabel;
        private readonly DropdownField _typeDropdown;
        private readonly TextField _customTypeField;
        private readonly TextField _nameField;
        private readonly TextField _dataField;
        private readonly Label _idLabel;

        public BTNodeInspectorPanel()
        {
            style.flexShrink = 0;

            BTEditorNodeTypeDiscovery.BuildChoiceLists(out var labels, out _);
            _typeDropdown = new DropdownField("类型", new List<string>(labels), 0);
            _typeDropdown.RegisterValueChangedCallback(_ => OnTypeChanged());

            _customTypeField = new TextField("自定义 type");
            _customTypeField.RegisterValueChangedCallback(_ => OnCustomTypeChanged());

            _nameField = new TextField("名称");
            _nameField.RegisterValueChangedCallback(_ => OnNameChanged());

            _dataField = new TextField("data (JSON)")
            {
                multiline = true
            };
            _dataField.style.minHeight = 120;
            _dataField.style.whiteSpace = WhiteSpace.Normal;
            _dataField.RegisterValueChangedCallback(_ => OnDataChanged());

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
            Add(_customTypeField);
            Add(_nameField);
            var dataScroll = new ScrollView { verticalScrollerVisibility = ScrollerVisibility.Auto };
            dataScroll.style.maxHeight = 280;
            dataScroll.Add(_dataField);
            Add(dataScroll);
            Add(_idLabel);

            Bind(null);
        }

        public void Bind(BTGraphNode node)
        {
            _bound = node;
            var hint = this.Q<Label>("bt-inspector-hint");
            var editing = node != null;

            _typeDropdown.SetEnabled(editing);
            _customTypeField.SetEnabled(editing);
            _nameField.SetEnabled(editing);
            _dataField.SetEnabled(editing);

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
            _customTypeField.SetValueWithoutNotify(node.CustomTypeText);
            _nameField.SetValueWithoutNotify(node.NodeNameText);
            _dataField.SetValueWithoutNotify(node.DataJsonText);

            _headerLabel.text = $"节点 — {node.title}";
            _idLabel.text = $"id: {node.DebugNodeId}";

            RefreshCustomVisibility();
        }

        private void RefreshCustomVisibility()
        {
            if (_bound == null)
            {
                _customTypeField.style.display = DisplayStyle.None;
                return;
            }

            var isCustom = _bound.TypeChoiceIndex == _typeDropdown.choices.Count - 1;
            _customTypeField.style.display = isCustom ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnTypeChanged()
        {
            if (_bound == null)
                return;
            _bound.TypeChoiceIndex = _typeDropdown.index;
            RefreshCustomVisibility();
            _headerLabel.text = $"节点 — {_bound.title}";
        }

        private void OnCustomTypeChanged()
        {
            if (_bound == null)
                return;
            _bound.CustomTypeText = _customTypeField.value;
            _headerLabel.text = $"节点 — {_bound.title}";
        }

        private void OnNameChanged()
        {
            if (_bound == null)
                return;
            _bound.NodeNameText = _nameField.value;
            _headerLabel.text = $"节点 — {_bound.title}";
        }

        private void OnDataChanged()
        {
            if (_bound == null)
                return;
            _bound.DataJsonText = _dataField.value;
        }
    }
}
