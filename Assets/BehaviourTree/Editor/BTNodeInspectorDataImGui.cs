using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shibafu.BehaviourTree;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shibafu.BehaviourTree.Editor
{
    /// <summary>
    /// 在 IMGUI 中编辑 <c>data</c>：根据节点 CLR 类型上的 <see cref="BTNodeInspectorFieldAttribute"/> 生成控件。
    /// </summary>
    internal static class BTNodeInspectorDataImGui
    {
        internal static void Draw(string typeId, ref string dataJsonText, out bool changed)
        {
            changed = false;
            var clr = BTNodeInspectorFieldDiscovery.ResolveClrType(typeId);
            var bindings = BTNodeInspectorFieldDiscovery.CollectBindings(clr);
            var auto = bindings.FindAll(b => !b.Attr.ManualJsonOnly);
            var manualOnly = bindings.FindAll(b => b.Attr.ManualJsonOnly);

            if (auto.Count == 0 && manualOnly.Count == 0)
            {
                if (clr != null)
                    EditorGUILayout.HelpBox("此类型未声明 BTNodeInspectorField；data 需通过代码或其它工具维护。", MessageType.Info);
                return;
            }

            JObject root;
            try
            {
                root = ParseDataObject(dataJsonText);
            }
            catch (Exception ex)
            {
                EditorGUILayout.HelpBox($"data JSON 无法解析：{ex.Message}", MessageType.Error);
                return;
            }

            if (manualOnly.Count > 0)
            {
                var keys = string.Join(", ", manualOnly.ConvertAll(b => b.Attr.JsonKey));
                EditorGUILayout.HelpBox($"以下键标记为仅手动编辑（ManualJsonOnly），当前面板不展示：{keys}", MessageType.None);
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Data 字段", EditorStyles.boldLabel);
            foreach (var b in auto)
            {
                var label = BTNodeInspectorFieldDiscovery.GetGuiLabel(b);
                DrawBinding(root, b, label);
            }

            if (EditorGUI.EndChangeCheck())
            {
                dataJsonText = root.ToString(Formatting.Indented);
                changed = true;
            }
        }

        private static JObject ParseDataObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new JObject();

            var token = JToken.Parse(text);
            if (token is JObject o)
                return o;

            throw new JsonException("data 必须是 JSON 对象 { ... }。");
        }

        private static void DrawBinding(JObject root, BTNodeInspectorFieldDiscovery.Binding b, string label)
        {
            var key = b.Attr.JsonKey.Trim();
            var t = b.ValueType;

            if (t == typeof(string))
            {
                if (b.Attr.ObjectReferenceType != null &&
                    typeof(Object).IsAssignableFrom(b.Attr.ObjectReferenceType))
                {
                    DrawUnityObjectField(root, key, label, b.Attr.ObjectReferenceType);
                    return;
                }

                var cur = root[key]?.Value<string>() ?? "";
                var next = EditorGUILayout.DelayedTextField(label, cur);
                if (next != cur)
                {
                    if (string.IsNullOrEmpty(next))
                        root.Remove(key);
                    else
                        root[key] = next;
                }

                return;
            }

            if (t == typeof(float))
            {
                var cur = root[key]?.Value<float>() ?? 0f;
                var next = EditorGUILayout.FloatField(label, cur);
                if (!Mathf.Approximately(cur, next))
                    root[key] = next;
                return;
            }

            if (t == typeof(double))
            {
                var cur = root[key]?.Value<double>() ?? 0d;
                var next = EditorGUILayout.DoubleField(label, cur);
                if (Math.Abs(cur - next) > 1e-9)
                    root[key] = next;
                return;
            }

            if (t == typeof(int))
            {
                var cur = root[key]?.Value<int>() ?? 0;
                var next = EditorGUILayout.IntField(label, cur);
                if (cur != next)
                    root[key] = next;
                return;
            }

            if (t == typeof(long))
            {
                var cur = root[key]?.Value<long>() ?? 0L;
                var next = EditorGUILayout.LongField(label, cur);
                if (cur != next)
                    root[key] = next;
                return;
            }

            if (t == typeof(bool))
            {
                var cur = root[key]?.Value<bool>() ?? false;
                var next = EditorGUILayout.Toggle(label, cur);
                if (cur != next)
                    root[key] = next;
                return;
            }

            if (t.IsEnum)
            {
                var cur = ReadEnumFromToken(root[key], t);
                var next = EditorGUILayout.EnumPopup(label, cur);
                if (!Equals(cur, next))
                    root[key] = EnumToJsonString(t, (Enum)next);
                return;
            }

            if (typeof(Object).IsAssignableFrom(t))
            {
                DrawUnityObjectField(root, key, label, t);
                return;
            }

            EditorGUILayout.LabelField(label, $"（{t.Name} 无内置控件，请用 JSON）");
        }

        private static Enum ReadEnumFromToken(JToken tok, Type enumType)
        {
            if (tok == null || tok.Type == JTokenType.Null)
                return (Enum)Enum.ToObject(enumType, 0);

            if (tok.Type == JTokenType.Integer)
                return (Enum)Enum.ToObject(enumType, tok.Value<long>());

            var s = tok.Value<string>();
            if (string.IsNullOrEmpty(s))
                return (Enum)Enum.ToObject(enumType, 0);

            if (Enum.TryParse(enumType, s, true, out var parsed) && parsed is Enum e)
                return e;

            return (Enum)Enum.ToObject(enumType, 0);
        }

        private static string EnumToJsonString(Type enumType, Enum value)
        {
            var name = Enum.GetName(enumType, value);
            if (string.IsNullOrEmpty(name))
                return value.ToString().ToLowerInvariant();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static void DrawUnityObjectField(JObject root, string key, string label, Type objectType)
        {
            var idStr = root[key]?.Type == JTokenType.String ? root[key].Value<string>() : null;
            Object obj = null;
            if (!string.IsNullOrEmpty(idStr) && GlobalObjectId.TryParse(idStr, out var gid))
            {
                var ids = new[] { gid };
                var objs = new Object[1];
                GlobalObjectId.GlobalObjectIdentifiersToObjectsSlow(ids, objs);
                obj = objs[0];
            }

            if (obj != null && objectType != typeof(Object) && !objectType.IsInstanceOfType(obj))
                obj = null;

            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.ObjectField(label, obj, objectType, true);
            if (!EditorGUI.EndChangeCheck())
                return;

            if (next == null)
            {
                root.Remove(key);
                return;
            }

            var objsIn = new[] { next };
            var idsOut = new GlobalObjectId[1];
            GlobalObjectId.GetGlobalObjectIdsSlow(objsIn, idsOut);
            root[key] = idsOut[0].ToString();
        }
    }
}
