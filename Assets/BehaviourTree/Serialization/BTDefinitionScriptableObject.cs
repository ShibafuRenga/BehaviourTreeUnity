using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        public string targetGlobalObjectId;
        public string targetScenePath;
        public string targetHierarchyPath;
        public string targetTypeName;
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

            ctx.TryGetBoundObjectById = id =>
            {
                if (string.IsNullOrEmpty(id))
                    return null;
                foreach (var e in _objectBindings)
                {
                    if (e == null || e.bindingId != id)
                        continue;
                    return ResolveObjectBindingTarget(e, ctx.UnityObjectResolveRoot);
                }

                return null;
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
                obj = ResolveObjectBindingTarget(e, null);
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

        private static Object ResolveObjectBindingTarget(BTObjectBindingEntry entry, Transform hierarchyRoot)
        {
            if (entry == null)
                return null;

            if (entry.target != null)
                return entry.target;

#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(entry.targetGlobalObjectId) &&
                UnityEditor.GlobalObjectId.TryParse(entry.targetGlobalObjectId, out var gid))
            {
                var ids = new[] { gid };
                var objs = new Object[1];
                UnityEditor.GlobalObjectId.GlobalObjectIdentifiersToObjectsSlow(ids, objs);
                if (TryCoerceToRecordedType(objs[0], entry.targetTypeName, out var resolved))
                    return resolved;
            }
#endif

            if (TryResolveSceneHierarchyPath(entry.targetHierarchyPath, entry.targetScenePath,
                    entry.targetTypeName, hierarchyRoot, out var sceneObject))
                return sceneObject;

            return null;
        }

        private static bool TryResolveSceneHierarchyPath(string hierarchyPath, string scenePath, string targetTypeName,
            Transform hierarchyRoot, out Object obj)
        {
            obj = null;
            if (string.IsNullOrWhiteSpace(hierarchyPath))
                return false;

            var segments = hierarchyPath.Split('/');
            if (segments.Length == 0)
                return false;

            if (hierarchyRoot != null &&
                TryFindTransformByHierarchyPath(hierarchyRoot, segments, out var rootedTransform) &&
                TryCoerceTransformToRecordedType(rootedTransform, targetTypeName, out obj))
                return true;

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;
                if (!string.IsNullOrEmpty(scenePath) &&
                    !string.Equals(scene.path, scenePath, StringComparison.Ordinal))
                    continue;
                if (TryFindTransformInScene(scene, segments, out var sceneTransform) &&
                    TryCoerceTransformToRecordedType(sceneTransform, targetTypeName, out obj))
                    return true;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded &&
                TryFindTransformInScene(activeScene, segments, out var activeSceneTransform) &&
                TryCoerceTransformToRecordedType(activeSceneTransform, targetTypeName, out obj))
                return true;

            return false;
        }

        private static bool TryFindTransformInScene(Scene scene, string[] segments, out Transform transform)
        {
            transform = null;
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                if (TryFindTransformByHierarchyPath(root.transform, segments, out transform))
                    return true;
            }

            return false;
        }

        private static bool TryFindTransformByHierarchyPath(Transform root, string[] segments, out Transform transform)
        {
            transform = null;
            if (root == null || segments == null || segments.Length == 0)
                return false;

            if (root.name == segments[0])
            {
                transform = FindChildPath(root, segments, 1);
                if (transform != null)
                    return true;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                if (TryFindTransformByHierarchyPath(root.GetChild(i), segments, out transform))
                    return true;
            }

            return false;
        }

        private static Transform FindChildPath(Transform root, string[] segments, int index)
        {
            if (root == null)
                return null;
            if (index >= segments.Length)
                return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name != segments[index])
                    continue;
                return FindChildPath(child, segments, index + 1);
            }

            return null;
        }

        private static bool TryCoerceToRecordedType(Object raw, string targetTypeName, out Object obj)
        {
            obj = null;
            if (raw == null)
                return false;

            var targetType = ResolveType(targetTypeName);
            if (targetType == null || targetType.IsInstanceOfType(raw))
            {
                obj = raw;
                return true;
            }

            switch (raw)
            {
                case GameObject go:
                    return TryCoerceGameObjectToRecordedType(go, targetType, out obj);
                case Component component:
                    return TryCoerceGameObjectToRecordedType(component.gameObject, targetType, out obj);
                default:
                    return false;
            }
        }

        private static bool TryCoerceTransformToRecordedType(Transform transform, string targetTypeName, out Object obj)
        {
            obj = null;
            if (transform == null)
                return false;

            var targetType = ResolveType(targetTypeName);
            if (targetType == null || targetType == typeof(Transform))
            {
                obj = transform;
                return true;
            }

            return TryCoerceGameObjectToRecordedType(transform.gameObject, targetType, out obj);
        }

        private static bool TryCoerceGameObjectToRecordedType(GameObject gameObject, Type targetType, out Object obj)
        {
            obj = null;
            if (gameObject == null || targetType == null)
                return false;

            if (targetType == typeof(GameObject))
            {
                obj = gameObject;
                return true;
            }

            if (targetType == typeof(Transform))
            {
                obj = gameObject.transform;
                return true;
            }

            if (typeof(Component).IsAssignableFrom(targetType))
            {
                obj = gameObject.GetComponent(targetType);
                return obj != null;
            }

            return false;
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;
            return Type.GetType(typeName);
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            EditorResolveMissingObjectBindingTargets();
        }

        private void OnValidate()
        {
            EditorRefreshObjectBindingMetadata();
            EditorResolveMissingObjectBindingTargets();
        }

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
            {
                _objectBindings[idx].target = target;
                EditorUpdateObjectBindingMetadata(_objectBindings[idx], target);
            }
            else
            {
                var entry = new BTObjectBindingEntry { bindingId = id, target = target };
                EditorUpdateObjectBindingMetadata(entry, target);
                _objectBindings.Add(entry);
            }

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

        private void EditorRefreshObjectBindingMetadata()
        {
            var changed = false;
            foreach (var entry in _objectBindings)
            {
                if (entry?.target == null)
                    continue;
                changed |= EditorUpdateObjectBindingMetadata(entry, entry.target);
            }

            if (changed)
                UnityEditor.EditorUtility.SetDirty(this);
        }

        private void EditorResolveMissingObjectBindingTargets()
        {
            foreach (var entry in _objectBindings)
            {
                if (entry == null || entry.target != null)
                    continue;
                entry.target = ResolveObjectBindingTarget(entry, null);
            }
        }

        private static bool EditorUpdateObjectBindingMetadata(BTObjectBindingEntry entry, Object target)
        {
            if (entry == null || target == null)
                return false;

            var changed = false;
            SetIfChanged(ref entry.targetTypeName, target.GetType().AssemblyQualifiedName, ref changed);
            SetIfChanged(ref entry.targetGlobalObjectId, EditorGetGlobalObjectIdString(target), ref changed);

            if (EditorTryGetSceneObject(target, out var gameObject))
            {
                SetIfChanged(ref entry.targetScenePath, gameObject.scene.path, ref changed);
                SetIfChanged(ref entry.targetHierarchyPath, EditorBuildHierarchyPath(gameObject.transform), ref changed);
            }
            else
            {
                SetIfChanged(ref entry.targetScenePath, null, ref changed);
                SetIfChanged(ref entry.targetHierarchyPath, null, ref changed);
            }

            return changed;
        }

        private static string EditorGetGlobalObjectIdString(Object target)
        {
            var objsIn = new[] { target };
            var idsOut = new UnityEditor.GlobalObjectId[1];
            UnityEditor.GlobalObjectId.GetGlobalObjectIdsSlow(objsIn, idsOut);
            return idsOut[0].ToString();
        }

        private static bool EditorTryGetSceneObject(Object target, out GameObject gameObject)
        {
            gameObject = null;
            switch (target)
            {
                case GameObject go:
                    gameObject = go;
                    break;
                case Component component:
                    gameObject = component.gameObject;
                    break;
            }

            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static string EditorBuildHierarchyPath(Transform transform)
        {
            if (transform == null)
                return null;

            var names = new Stack<string>();
            for (var t = transform; t != null; t = t.parent)
                names.Push(t.name);
            return string.Join("/", names);
        }

        private static void SetIfChanged(ref string current, string next, ref bool changed)
        {
            if (string.Equals(current, next, StringComparison.Ordinal))
                return;
            current = next;
            changed = true;
        }
#endif
    }
}
