using UnityEngine;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 将编辑器写入的 <see cref="UnityEditor.GlobalObjectId"/> 字符串还原为 <see cref="Transform"/>（仅编辑器 / 进入 Play 且场景已加载时可靠）。
    /// </summary>
    internal static class BTUnityObjectIdResolve
    {
        internal static bool TryGetUnityObject(string globalObjectIdString, out Object obj)
        {
            obj = null;
            if (string.IsNullOrWhiteSpace(globalObjectIdString))
                return false;

#if UNITY_EDITOR
            if (!UnityEditor.GlobalObjectId.TryParse(globalObjectIdString.Trim(), out var gid))
                return false;

            var ids = new[] { gid };
            var objs = new Object[1];
            UnityEditor.GlobalObjectId.GlobalObjectIdentifiersToObjectsSlow(ids, objs);
            obj = objs[0];
            return obj != null;
#else
            return false;
#endif
        }

        internal static bool TryGetTransform(string globalObjectIdString, out Transform transform)
        {
            if (!TryGetUnityObject(globalObjectIdString, out var o))
            {
                transform = null;
                return false;
            }

            switch (o)
            {
                case Transform tr:
                    transform = tr;
                    return true;
                case GameObject go:
                    transform = go.transform;
                    return true;
                default:
                    transform = null;
                    return false;
            }
        }
    }
}
