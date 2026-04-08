using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 将 JSON <c>data</c> 绑定到带 <see cref="BTNodeInspectorFieldAttribute"/> 的实例字段或属性：
    /// 键名与成员名一致；支持 <see cref="Object"/> 派生（GlobalObjectId 字符串）、<see cref="string"/>、常见值类型与枚举。
    /// 仅当 JSON 中存在对应键时才写入；缺键则保留默认值。
    /// </summary>
    internal static class BTNodeReferenceBinder
    {
        private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
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
                foreach (var fi in walk.GetFields(MemberFlags))
                {
                    var attr = fi.GetCustomAttribute<BTNodeInspectorFieldAttribute>(false);
                    if (attr == null)
                        continue;

                    var jsonKey = fi.Name;
                    if (!seenJsonKeys.Add(jsonKey))
                        continue;
                    if (attr.ManualJsonOnly)
                        continue;

                    var ft = fi.FieldType;
                    if (typeof(Object).IsAssignableFrom(ft))
                        TryBindUnityObjectField(target, fi, data, jsonKey);
                    else
                        TryBindScalarField(target, fi, data, jsonKey);
                }

                foreach (var pi in walk.GetProperties(MemberFlags))
                {
                    if (pi.GetIndexParameters().Length != 0 || !pi.CanRead || !pi.CanWrite)
                        continue;
                    if (pi.GetSetMethod(true) == null)
                        continue;

                    var attr = pi.GetCustomAttribute<BTNodeInspectorFieldAttribute>(false);
                    if (attr == null)
                        continue;

                    var jsonKey = pi.Name;
                    if (!seenJsonKeys.Add(jsonKey))
                        continue;
                    if (attr.ManualJsonOnly)
                        continue;

                    var pt = pi.PropertyType;
                    if (typeof(Object).IsAssignableFrom(pt))
                        TryBindUnityObjectProperty(target, pi, data, jsonKey);
                    else
                        TryBindScalarProperty(target, pi, data, jsonKey);
                }
            }
        }

        private static void TryBindUnityObjectField(BTAction target, FieldInfo field, JObject data, string jsonKey)
        {
            var idStr = data[jsonKey]?.Value<string>();
            if (string.IsNullOrWhiteSpace(idStr) ||
                !BTUnityObjectIdResolve.TryGetUnityObject(idStr.Trim(), out var raw) ||
                !TryCoerceUnityObject(raw, field.FieldType, out var coerced))
                return;

            field.SetValue(target, coerced);
        }

        private static void TryBindUnityObjectProperty(BTAction target, PropertyInfo prop, JObject data, string jsonKey)
        {
            var idStr = data[jsonKey]?.Value<string>();
            if (string.IsNullOrWhiteSpace(idStr) ||
                !BTUnityObjectIdResolve.TryGetUnityObject(idStr.Trim(), out var raw) ||
                !TryCoerceUnityObject(raw, prop.PropertyType, out var coerced))
                return;

            prop.SetValue(target, coerced, null);
        }

        private static void TryBindScalarField(BTAction target, FieldInfo field, JObject data, string jsonKey)
        {
            var tok = data[jsonKey];
            if (tok == null || tok.Type == JTokenType.Undefined)
                return;

            var ft = field.FieldType;

            if (tok.Type == JTokenType.Null)
            {
                if (ft == typeof(string))
                    field.SetValue(target, null);
                return;
            }

            if (TryConvertTokenToScalar(tok, ft, out var value))
                field.SetValue(target, value);
        }

        private static void TryBindScalarProperty(BTAction target, PropertyInfo prop, JObject data, string jsonKey)
        {
            var tok = data[jsonKey];
            if (tok == null || tok.Type == JTokenType.Undefined)
                return;

            var pt = prop.PropertyType;

            if (tok.Type == JTokenType.Null)
            {
                if (pt == typeof(string))
                    prop.SetValue(target, null, null);
                return;
            }

            if (TryConvertTokenToScalar(tok, pt, out var value))
                prop.SetValue(target, value, null);
        }

        private static bool TryConvertTokenToScalar(JToken tok, Type ft, out object value)
        {
            value = null;

            if (ft == typeof(string))
            {
                value = tok.Type == JTokenType.String ? tok.Value<string>() : tok.ToString();
                return true;
            }

            if (ft == typeof(bool))
            {
                value = tok.Value<bool>();
                return true;
            }

            if (ft == typeof(int))
            {
                value = tok.Value<int>();
                return true;
            }

            if (ft == typeof(long))
            {
                value = tok.Value<long>();
                return true;
            }

            if (ft == typeof(float))
            {
                value = tok.Value<float>();
                return true;
            }

            if (ft == typeof(double))
            {
                value = tok.Value<double>();
                return true;
            }

            if (ft.IsEnum)
            {
                if (tok.Type == JTokenType.String)
                {
                    var s = tok.Value<string>();
                    if (Enum.TryParse(ft, s, true, out var ev))
                    {
                        value = ev;
                        return true;
                    }
                }
                else if (tok.Type == JTokenType.Integer)
                {
                    value = Enum.ToObject(ft, tok.Value<long>());
                    return true;
                }
            }

            return false;
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
