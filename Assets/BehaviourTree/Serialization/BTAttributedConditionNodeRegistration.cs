using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Shibafu.BehaviourTree;

namespace Shibafu.BehaviourTree.Serialization
{
    /// <summary>
    /// 扫描带 <see cref="BTNodeTypeAttribute"/> 的 <see cref="BTSimpleConditionNode"/> 具体子类（需 public 无参构造），并注册到上下文（不覆盖已存在的 typeId）。
    /// </summary>
    internal static class BTAttributedConditionNodeRegistration
    {
        internal static void RegisterAll(BTDefinitionLoadContext ctx)
        {
            foreach (var (typeId, clrType) in DiscoverAttributedConditionTypes())
            {
                if (ctx.IsNodeTypeRegistered(typeId))
                    continue;

                var capturedType = clrType;
                ctx.RegisterNodeType(typeId,
                    (def, loadContext, children) =>
                        BuildAttributedCondition(capturedType, def, children, loadContext));
            }
        }

        private static BTNode BuildAttributedCondition(Type clrType, BTNodeDefinition def,
            IReadOnlyList<BTNode> children, BTDefinitionLoadContext loadContext)
        {
            if (children.Count != 0)
                throw new InvalidOperationException($"\"{def.Type}\" cannot have children.");

            object raw;
            try
            {
                raw = Activator.CreateInstance(clrType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Cannot create instance of {clrType.Name}: public parameterless constructor is required for JSON-loaded condition nodes.",
                    ex);
            }

            if (raw is not BTSimpleConditionNode node)
                throw new InvalidOperationException($"{clrType.Name} must inherit {nameof(BTSimpleConditionNode)}.");

            node.InitFromJson(def.Data ?? new JObject(), loadContext);
            return node;
        }

        private static IEnumerable<(string TypeId, Type ClrType)> DiscoverAttributedConditionTypes()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asm in GetCandidateAssemblies())
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || !typeof(BTSimpleConditionNode).IsAssignableFrom(t))
                        continue;
                    if (t == typeof(BTSimpleConditionNode))
                        continue;
                    if (t.GetConstructor(Type.EmptyTypes) == null)
                        continue;

                    var attr = t.GetCustomAttribute<BTNodeTypeAttribute>(false);
                    if (attr == null)
                        continue;

                    var id = attr.TypeId?.Trim();
                    if (string.IsNullOrEmpty(id))
                        continue;

                    if (!seen.Add(id))
                        continue;

                    yield return (id, t);
                }
            }
        }

        private static IEnumerable<Assembly> GetCandidateAssemblies()
        {
            var core = typeof(BTSimpleConditionNode).Assembly;
            yield return core;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic || asm == core)
                    continue;

                var name = asm.GetName().Name ?? "";
                if (name.StartsWith("System.", StringComparison.Ordinal) ||
                    name.StartsWith("mscorlib", StringComparison.Ordinal) ||
                    name.StartsWith("netstandard", StringComparison.Ordinal) ||
                    name.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                    name.StartsWith("UnityEditor", StringComparison.Ordinal) ||
                    name.StartsWith("Unity.", StringComparison.Ordinal) ||
                    name.StartsWith("Mono.", StringComparison.Ordinal) ||
                    name.StartsWith("Microsoft.", StringComparison.Ordinal) ||
                    name.StartsWith("Newtonsoft", StringComparison.Ordinal))
                    continue;

                yield return asm;
            }
        }
    }
}
