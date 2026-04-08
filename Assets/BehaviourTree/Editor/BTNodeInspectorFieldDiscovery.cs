using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Shibafu.BehaviourTree.Editor
{
    internal static class BTNodeInspectorFieldDiscovery
    {
        internal readonly struct Binding
        {
            public readonly MemberInfo Member;
            public readonly BTNodeInspectorFieldAttribute Attr;

            public Binding(MemberInfo member, BTNodeInspectorFieldAttribute attr)
            {
                Member = member;
                Attr = attr;
            }

            public Type ValueType => Member switch
            {
                FieldInfo f => f.FieldType,
                PropertyInfo p => p.PropertyType,
                _ => typeof(void)
            };
        }

        internal static Type ResolveClrType(string typeId)
        {
            if (string.IsNullOrWhiteSpace(typeId))
                return null;

            var key = typeId.Trim();
            foreach (var t in TypeCache.GetTypesWithAttribute<BTNodeTypeAttribute>())
            {
                var a = t.GetCustomAttribute<BTNodeTypeAttribute>(false);
                if (a == null)
                    continue;
                if (string.Equals(a.TypeId, key, StringComparison.Ordinal))
                    return t;
            }

            return null;
        }

        /// <summary>自派生类向基类遍历；同键以派生类声明为准。</summary>
        internal static List<Binding> CollectBindings(Type t)
        {
            var list = new List<Binding>();
            if (t == null)
                return list;

            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                       BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var walk = t; walk != null && walk != typeof(object); walk = walk.BaseType)
            {
                var chunk = new List<Binding>();

                foreach (var fi in walk.GetFields(flags))
                {
                    var a = fi.GetCustomAttribute<BTNodeInspectorFieldAttribute>(false);
                    if (a == null)
                        continue;
                    if (string.IsNullOrWhiteSpace(a.JsonKey))
                        continue;
                    chunk.Add(new Binding(fi, a));
                }

                foreach (var pi in walk.GetProperties(flags))
                {
                    if (pi.GetIndexParameters().Length != 0)
                        continue;
                    if (!pi.CanRead)
                        continue;
                    var a = pi.GetCustomAttribute<BTNodeInspectorFieldAttribute>(false);
                    if (a == null)
                        continue;
                    if (string.IsNullOrWhiteSpace(a.JsonKey))
                        continue;
                    chunk.Add(new Binding(pi, a));
                }

                for (var i = chunk.Count - 1; i >= 0; i--)
                {
                    var k = chunk[i].Attr.JsonKey.Trim();
                    if (!seenKeys.Add(k))
                        chunk.RemoveAt(i);
                }

                list.AddRange(chunk);
            }

            return list
                .OrderBy(b => b.Attr.ManualJsonOnly ? 1 : 0)
                .ThenBy(b => b.Attr.JsonKey, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static string GetGuiLabel(Binding b)
        {
            if (!string.IsNullOrEmpty(b.Attr.Label))
                return b.Attr.Label;
            return ObjectNames.NicifyVariableName(b.Attr.JsonKey);
        }
    }
}
