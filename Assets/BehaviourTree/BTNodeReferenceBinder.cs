using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 将 JSON <c>data</c> 中的 GlobalObjectId 字符串绑定到带 <see cref="BTNodeInspectorFieldAttribute"/> 的
    /// <see cref="Object"/> 派生字段。
    /// </summary>
    internal static class BTNodeReferenceBinder
    {
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                                BindingFlags.DeclaredOnly;

        internal static void ApplyFromJson(BTAction target, JObject data)
        {
            if (target == null)
                return;

            data ??= new JObject();

            var seenJsonKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var walk = target.GetType();
                 walk != null && walk != typeof(object);
                 walk = walk.BaseType)
            {
                foreach (var fi in walk.GetFields(FieldFlags))
                {
                    var attr = fi.GetCustomAttribute<BTNodeInspectorFieldAttribute>(false);
                    if (attr == null || string.IsNullOrWhiteSpace(attr.JsonKey))
                        continue;

                    var jsonKey = attr.JsonKey.Trim();
                    if (!seenJsonKeys.Add(jsonKey))
                        continue;
                    if (attr.ManualJsonOnly)
                        continue;

                    var ft = fi.FieldType;
                    if (!typeof(Object).IsAssignableFrom(ft))
                        continue;

                    TryBindUnityField(target, fi, attr, data);
                }
            }
        }

        private static void TryBindUnityField(
            BTAction target,
            FieldInfo field,
            BTNodeInspectorFieldAttribute attr,
            JObject data)
        {
            var jsonKey = attr.JsonKey.Trim();
            var idStr = data[jsonKey]?.Value<string>();
            if (string.IsNullOrWhiteSpace(idStr) ||
                !BTUnityObjectIdResolve.TryGetUnityObject(idStr.Trim(), out var raw) ||
                !TryCoerceUnityObject(raw, field.FieldType, out var coerced))
                return;

            field.SetValue(target, coerced);
        }

        private static bool TryCoerceUnityObject(Object raw, Type fieldType, out Object coerced)
        {
            coerced = null;
            if (raw == null)
                return false;

            if (fieldType.IsInstanceOfType(raw))
            {
                coerced = raw;
                return true;
            }

            if (fieldType == typeof(Transform))
            {
                switch (raw)
                {
                    case Transform tr:
                        coerced = tr;
                        return true;
                    case GameObject go:
                        coerced = go.transform;
                        return true;
                    case Component c:
                        coerced = c.transform;
                        return true;
                    default:
                        return false;
                }
            }

            if (fieldType == typeof(GameObject))
            {
                switch (raw)
                {
                    case GameObject go:
                        coerced = go;
                        return true;
                    case Transform tr:
                        coerced = tr.gameObject;
                        return true;
                    case Component c:
                        coerced = c.gameObject;
                        return true;
                    default:
                        return false;
                }
            }

            if (typeof(Component).IsAssignableFrom(fieldType))
            {
                if (fieldType.IsInstanceOfType(raw))
                {
                    coerced = raw;
                    return true;
                }

                var go = raw as GameObject ?? (raw as Component)?.gameObject;
                if (go == null)
                    return false;

                var comp = go.GetComponent(fieldType);
                if (comp != null)
                {
                    coerced = comp;
                    return true;
                }
            }

            return false;
        }
    }
}
