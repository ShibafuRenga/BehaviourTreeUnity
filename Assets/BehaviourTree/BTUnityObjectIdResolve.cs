using System;
using Shibafu.BehaviourTree.Serialization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 将 JSON 中的 Unity 引用字符串解析为 <see cref="Object"/>。
    /// 顺序：<c>btref:</c>（定义 SO 上的序列化引用，发布包可用）→ 编辑器 <see cref="UnityEditor.GlobalObjectId"/> → 相对 <paramref name="hierarchyRoot"/> 的层级路径。
    /// </summary>
    internal static class BTUnityObjectIdResolve
    {
        internal static bool TryResolveUnityObject(
            string idOrPath,
            Transform hierarchyRoot,
            Func<string, Object> tryGetBoundObjectById,
            out Object obj)
        {
            obj = null;
            if (string.IsNullOrWhiteSpace(idOrPath))
                return false;

            var s = idOrPath.Trim();

            if (tryGetBoundObjectById != null &&
                s.StartsWith(BTDefinitionScriptableObject.ObjectBindingTokenPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var id = s.Substring(BTDefinitionScriptableObject.ObjectBindingTokenPrefix.Length).Trim();
                if (!string.IsNullOrEmpty(id))
                {
                    var bound = tryGetBoundObjectById(id);
                    if (bound != null)
                    {
                        obj = bound;
                        return true;
                    }
                }

                return false;
            }

#if UNITY_EDITOR
            if (UnityEditor.GlobalObjectId.TryParse(s, out var gid))
            {
                var ids = new[] { gid };
                var objs = new Object[1];
                UnityEditor.GlobalObjectId.GlobalObjectIdentifiersToObjectsSlow(ids, objs);
                obj = objs[0];
                if (obj != null)
                    return true;
            }
#endif
            if (hierarchyRoot == null)
                return false;

            var path = s;
            if (path.StartsWith("path:", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(5).TrimStart();

            if (string.IsNullOrEmpty(path))
                return false;

            var t = hierarchyRoot.Find(path);
            if (t == null)
                return false;

            obj = t;
            return true;
        }
    }
}
