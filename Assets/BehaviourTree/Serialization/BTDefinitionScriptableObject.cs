using UnityEngine;

namespace Shibafu.BehaviourTree.Serialization
{
    /// <summary>
    /// 在 Inspector 中维护 JSON 文本（与 <see cref="BTDefinitionIO"/> 同一格式），运行时构建树。
    /// </summary>
    [CreateAssetMenu(menuName = "Shibafu/Behaviour Tree Definition", fileName = "BTDefinition")]
    public sealed class BTDefinitionScriptableObject : ScriptableObject
    {
        [TextArea(8, 32)]
        [SerializeField]
        private string _json;

        public string Json
        {
            get => _json;
            set => _json = value;
        }

        public BehaviourTree CreateRuntimeTree(BTDefinitionLoadContext context = null) =>
            BTDefinitionLoader.LoadTree(_json, context);
    }
}
