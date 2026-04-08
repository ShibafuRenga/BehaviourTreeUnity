using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Shibafu.BehaviourTree;

namespace Shibafu.BehaviourTree.Serialization
{
    /// <summary>
    /// 解析 JSON 时的可扩展环境：节点类型工厂、具名 Action / Condition 处理器。
    /// </summary>
    public sealed class BTDefinitionLoadContext
    {
        private readonly Dictionary<string, BTNodeFactory> _nodeTypes =
            new Dictionary<string, BTNodeFactory>(StringComparer.Ordinal);

        private readonly Dictionary<string, Func<BTContext, BTStatus>> _actions =
            new Dictionary<string, Func<BTContext, BTStatus>>(StringComparer.Ordinal);

        private readonly Dictionary<string, Func<BTContext, bool>> _conditions =
            new Dictionary<string, Func<BTContext, bool>>(StringComparer.Ordinal);

        public BTDefinitionLoadContext(bool registerBuiltInNodeTypes = false)
        {
            if (registerBuiltInNodeTypes)
                BTBuiltInDefinitionTypes.Register(this);
        }

        /// <summary>注册内置 sequence / selector / inverter / action / condition 解析规则。</summary>
        public static BTDefinitionLoadContext CreateWithBuiltIns() => new BTDefinitionLoadContext(true);

        /// <summary>注册结构节点或自定义节点：<paramref name="typeId"/> 与 JSON 中 <c>type</c> 字段一致。</summary>
        public void RegisterNodeType(string typeId, BTNodeFactory factory)
        {
            if (string.IsNullOrWhiteSpace(typeId))
                throw new ArgumentException("typeId is empty.", nameof(typeId));
            _nodeTypes[typeId.Trim()] = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public bool IsNodeTypeRegistered(string typeId)
        {
            if (string.IsNullOrWhiteSpace(typeId))
                return false;
            return _nodeTypes.ContainsKey(typeId.Trim());
        }

        public void RegisterActionHandler(string id, Func<BTContext, BTStatus> handler)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("id is empty.", nameof(id));
            _actions[id.Trim()] = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void RegisterConditionHandler(string id, Func<BTContext, bool> handler)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("id is empty.", nameof(id));
            _conditions[id.Trim()] = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public bool TryGetActionHandler(string id, out Func<BTContext, BTStatus> handler) =>
            _actions.TryGetValue(id, out handler);

        public bool TryGetConditionHandler(string id, out Func<BTContext, bool> handler) =>
            _conditions.TryGetValue(id, out handler);

        /// <summary>递归构建单棵子树（可从任意 <see cref="BTNodeDefinition"/> 开始）。</summary>
        public BTNode BuildNode(BTNodeDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            var typeKey = definition.Type?.Trim();
            if (string.IsNullOrEmpty(typeKey))
                throw new BTDefinitionException("Node JSON is missing \"type\".");

            if (!_nodeTypes.TryGetValue(typeKey, out var factory))
                throw new BTDefinitionException($"Unknown node type \"{typeKey}\". Register it with RegisterNodeType.");

            var builtChildren = new List<BTNode>();
            if (definition.Children != null)
            {
                foreach (var child in definition.Children)
                    builtChildren.Add(BuildNode(child));
            }

            BTNode node;
            try
            {
                node = factory(definition, this, builtChildren);
            }
            catch (Exception ex)
            {
                throw new BTDefinitionException(
                    $"Factory for type \"{typeKey}\" failed: {ex.Message}", ex);
            }

            if (!string.IsNullOrEmpty(definition.Name))
                node.Name = definition.Name;

            if (!string.IsNullOrEmpty(definition.Id))
                node.DebugNodeId = definition.Id.Trim();

            return node;
        }
    }

    /// <summary>加载定义失败时抛出，携带可读信息。</summary>
    public sealed class BTDefinitionException : Exception
    {
        public BTDefinitionException(string message) : base(message)
        {
        }

        public BTDefinitionException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    internal static class BTBuiltInDefinitionTypes
    {
        internal static void Register(BTDefinitionLoadContext ctx)
        {
            ctx.RegisterNodeType("sequence", BuildSequence);
            ctx.RegisterNodeType("selector", BuildSelector);
            ctx.RegisterNodeType("inverter", BuildInverter);
            ctx.RegisterNodeType("action", BuildAction);
            ctx.RegisterNodeType("condition", BuildCondition);
            BTAttributedActionRegistration.RegisterAll(ctx);
        }

        private static BTNode BuildSequence(BTNodeDefinition def, BTDefinitionLoadContext _, IReadOnlyList<BTNode> children)
        {
            var seq = new BTSequence(def.Name);
            foreach (var c in children)
                seq.AddChild(c);
            return seq;
        }

        private static BTNode BuildSelector(BTNodeDefinition def, BTDefinitionLoadContext _, IReadOnlyList<BTNode> children)
        {
            var sel = new BTSelector(def.Name);
            foreach (var c in children)
                sel.AddChild(c);
            return sel;
        }

        private static BTNode BuildInverter(BTNodeDefinition def, BTDefinitionLoadContext _, IReadOnlyList<BTNode> children)
        {
            if (children.Count != 1)
                throw new InvalidOperationException("\"inverter\" requires exactly one child.");
            return new BTInverter(children[0], def.Name);
        }

        private static BTNode BuildAction(BTNodeDefinition def, BTDefinitionLoadContext ctx, IReadOnlyList<BTNode> children)
        {
            if (children.Count != 0)
                throw new InvalidOperationException("\"action\" cannot have children.");
            var data = def.Data ?? new JObject();
            var handlerId = data["handler"]?.Value<string>();
            if (string.IsNullOrEmpty(handlerId))
                throw new InvalidOperationException("\"action\" requires data.handler (registered id).");

            if (!ctx.TryGetActionHandler(handlerId.Trim(), out var fn))
                throw new InvalidOperationException($"No action handler registered for id \"{handlerId}\".");

            return new BTDelegateAction(fn, def.Name);
        }

        private static BTNode BuildCondition(BTNodeDefinition def, BTDefinitionLoadContext ctx, IReadOnlyList<BTNode> children)
        {
            if (children.Count != 0)
                throw new InvalidOperationException("\"condition\" cannot have children.");
            var data = def.Data ?? new JObject();
            var mode = data["mode"]?.Value<string>() ?? "handler";

            if (string.Equals(mode, "handler", StringComparison.Ordinal))
            {
                var handlerId = data["handler"]?.Value<string>();
                if (string.IsNullOrEmpty(handlerId))
                    throw new InvalidOperationException(
                        "condition with mode \"handler\" requires data.handler.");
                if (!ctx.TryGetConditionHandler(handlerId.Trim(), out var fn))
                    throw new InvalidOperationException(
                        $"No condition handler registered for id \"{handlerId}\".");
                return new BTCondition(fn, def.Name);
            }

            if (string.Equals(mode, "boolKey", StringComparison.Ordinal))
            {
                var key = data["key"]?.Value<string>();
                if (string.IsNullOrEmpty(key))
                    throw new InvalidOperationException("condition boolKey requires data.key.");
                return new BTCondition(c => c.Get<bool>(key), def.Name);
            }

            if (string.Equals(mode, "intCompare", StringComparison.Ordinal))
                return BuildIntCompare(def.Name, data);

            if (string.Equals(mode, "floatCompare", StringComparison.Ordinal))
                return BuildFloatCompare(def.Name, data);

            throw new InvalidOperationException($"Unknown condition mode \"{mode}\".");
        }

        private static BTNode BuildIntCompare(string name, JObject data)
        {
            var key = data["key"]?.Value<string>();
            var op = data["op"]?.Value<string>();
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(op))
                throw new InvalidOperationException("intCompare requires data.key and data.op.");
            var right = data["value"]?.Value<int>() ?? throw new InvalidOperationException("intCompare requires data.value (int).");

            return new BTCondition(ctx =>
            {
                var left = ctx.Get<int>(key);
                return CompareInt(left, right, op);
            }, name);
        }

        private static BTNode BuildFloatCompare(string name, JObject data)
        {
            var key = data["key"]?.Value<string>();
            var op = data["op"]?.Value<string>();
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(op))
                throw new InvalidOperationException("floatCompare requires data.key and data.op.");
            var right = data["value"]?.Value<double>() ?? throw new InvalidOperationException("floatCompare requires data.value (number).");

            return new BTCondition(ctx =>
            {
                var left = Convert.ToDouble(ctx.Get<object>(key));
                return CompareDouble(left, right, op);
            }, name);
        }

        private static bool CompareInt(int left, int right, string op)
        {
            switch (op)
            {
                case "eq": return left == right;
                case "ne": return left != right;
                case "gt": return left > right;
                case "gte": return left >= right;
                case "lt": return left < right;
                case "lte": return left <= right;
                default:
                    throw new InvalidOperationException($"Unknown intCompare op \"{op}\".");
            }
        }

        private static bool CompareDouble(double left, double right, string op)
        {
            switch (op)
            {
                case "eq": return Math.Abs(left - right) < 1e-6;
                case "ne": return Math.Abs(left - right) >= 1e-6;
                case "gt": return left > right;
                case "gte": return left >= right;
                case "lt": return left < right;
                case "lte": return left <= right;
                default:
                    throw new InvalidOperationException($"Unknown floatCompare op \"{op}\".");
            }
        }
    }
}
