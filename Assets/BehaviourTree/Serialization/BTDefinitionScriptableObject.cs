using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shibafu.BehaviourTree.Serialization
{
    /// <summary>
    /// 与 <see cref="BTDefinitionScriptableObject"/> 中某 Object 引用条目对应；由 JSON <c>btref:…</c> 指向。
    /// </summary>
    [Serializable]
    public sealed class BTObjectBindingEntry
    {
        public string bindingId;
        public Object target;
    }

    /// <summary>
    /// 在 Inspector 中维护 JSON 文本（与 <see cref="BTDefinitionIO"/> 同一格式），运行时构建树。
    /// 场景/资源引用可登记在 <see cref="ObjectBindings"/>，节点 data 中写 <c>btref:&lt;id&gt;</c>，发布包亦可解析（仿 AnimationCollection 将引用放在 SO 上）。
    /// </summary>
    [CreateAssetMenu(menuName = "Shibafu/Behaviour Tree Definition", fileName = "BTDefinition")]
    public sealed class BTDefinitionScriptableObject : ScriptableObject
    {
        /// <summary>写入节点 data JSON 的前缀，后接 <see cref="BTObjectBindingEntry.bindingId"/>。</summary>
        public const string ObjectBindingTokenPrefix = "btref:";

        [TextArea(8, 32)]
        [SerializeField]
        private string _json;

        [Tooltip("图编辑器为 Object 字段登记的运行时引用；与 JSON 中 btref: 条目对应。")]
        [SerializeField]
        private List<BTObjectBindingEntry> _objectBindings = new List<BTObjectBindingEntry>();

        public string Json
        {
            get => _json;
            set => _json = value;
        }

        public IReadOnlyList<BTObjectBindingEntry> ObjectBindings => _objectBindings;

        /// <summary>
        /// 若上下文尚未设置绑定解析，则根据本资产上的列表生成 <see cref="BTDefinitionLoadContext.TryGetBoundObjectById"/>。
        /// </summary>
        public void EnsureBindingResolverOnContext(BTDefinitionLoadContext ctx)
        {
            if (ctx == null || ctx.TryGetBoundObjectById != null)
                return;

            var map = new Dictionary<string, Object>(StringComparer.Ordinal);
            foreach (var e in _objectBindings)
            {
                if (e == null || string.IsNullOrEmpty(e.bindingId) || e.target == null)
                    continue;
                map[e.bindingId] = e.target;
            }

            ctx.TryGetBoundObjectById = id =>
            {
                if (string.IsNullOrEmpty(id))
                    return null;
                return map.TryGetValue(id, out var o) ? o : null;
            };
        }

        /// <summary>供编辑器解析 <c>btref:</c> 为预览对象。</summary>
        public bool TryGetBoundObjectByBindingId(string bindingId, out Object obj)
        {
            obj = null;
            if (string.IsNullOrEmpty(bindingId))
                return false;
            foreach (var e in _objectBindings)
            {
                if (e == null || e.bindingId != bindingId)
                    continue;
                obj = e.target;
                return obj != null;
            }

            return false;
        }

        public Shibafu.BehaviourTree.BehaviourTree CreateRuntimeTree(BTDefinitionLoadContext context = null)
        {
            context ??= BTDefinitionLoadContext.CreateWithBuiltIns();
            EnsureBindingResolverOnContext(context);
            return BTDefinitionLoader.LoadTree(_json, context);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 登记引用并返回应写入 data JSON 的 token（含 <see cref="ObjectBindingTokenPrefix"/>）。
        /// 若当前 token 已是 btref 则复用其 id；否则新建 id（例如从 GlobalObjectId 迁移）。
        /// </summary>
        public string EditorAssignObjectBinding(Object target, string currentJsonToken)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            UnityEditor.Undo.RecordObject(this, "Behaviour Tree Object Binding");

            string id;
            var trimmed = currentJsonToken?.Trim() ?? "";
            if (trimmed.StartsWith(ObjectBindingTokenPrefix, StringComparison.OrdinalIgnoreCase))
            {
                id = trimmed.Substring(ObjectBindingTokenPrefix.Length).Trim();
                if (string.IsNullOrEmpty(id))
                    id = Guid.NewGuid().ToString("N");
            }
            else
                id = Guid.NewGuid().ToString("N");

            var idx = _objectBindings.FindIndex(e => e != null && e.bindingId == id);
            if (idx >= 0)
                _objectBindings[idx].target = target;
            else
                _objectBindings.Add(new BTObjectBindingEntry { bindingId = id, target = target });

            UnityEditor.EditorUtility.SetDirty(this);
            return ObjectBindingTokenPrefix + id;
        }

        /// <summary>从列表中移除与某 JSON token 对应的绑定。</summary>
        public void EditorRemoveObjectBinding(string currentJsonToken)
        {
            if (string.IsNullOrEmpty(currentJsonToken) ||
                !currentJsonToken.Trim().StartsWith(ObjectBindingTokenPrefix, StringComparison.OrdinalIgnoreCase))
                return;

            var id = currentJsonToken.Trim().Substring(ObjectBindingTokenPrefix.Length).Trim();
            if (string.IsNullOrEmpty(id))
                return;

            UnityEditor.Undo.RecordObject(this, "Behaviour Tree Object Binding");
            if (_objectBindings.RemoveAll(e => e != null && e.bindingId == id) > 0)
                UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// 按 JSON 全文扫描仍出现的 <c>btref:&lt;id&gt;</c>，移除列表中未被引用的条目（例如已删节点上的绑定）。
        /// 调用方应已通过 <c>Undo.RecordObject(this, …)</c> 登记本资产。
        /// </summary>
        public void EditorPruneUnusedObjectBindings(string json)
        {
            var used = CollectReferencedBindingIds(json);
            var removed = _objectBindings.RemoveAll(e =>
                e == null || string.IsNullOrEmpty(e.bindingId) || !used.Contains(e.bindingId));
            if (removed > 0)
                UnityEditor.EditorUtility.SetDirty(this);
        }

        private static HashSet<string> CollectReferencedBindingIds(string json)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(json))
                return set;

            JToken doc;
            try
            {
                doc = JToken.Parse(json);
            }
            catch
            {
                return set;
            }

            void Visit(JToken t)
            {
                switch (t)
                {
                    case JValue { Type: JTokenType.String } v:
                    {
                        var s = v.Value<string>();
                        if (!string.IsNullOrEmpty(s) &&
                            s.StartsWith(ObjectBindingTokenPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            var id = s.Substring(ObjectBindingTokenPrefix.Length).Trim();
                            if (!string.IsNullOrEmpty(id))
                                set.Add(id);
                        }

                        break;
                    }
                    case JObject o:
                        foreach (var p in o.Properties())
                            Visit(p.Value);
                        break;
                    case JArray a:
                        foreach (var x in a)
                            Visit(x);
                        break;
                }
            }

            Visit(doc);
            return set;
        }
#endif
    }
}
