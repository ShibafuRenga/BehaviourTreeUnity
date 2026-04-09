using System;
using System.Collections.Generic;
using Shibafu.BehaviourTree.Serialization;
using UnityEngine;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 在场景/Prefab 上引用行为树定义资产；可选每帧自动 Tick，也可关闭后手动 <see cref="TickOnce"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BehaviourTreeRunner : MonoBehaviour, IBTDebugStatusSink
    {
        [Tooltip("Create → Shibafu → Behaviour Tree Definition 创建的资产，与编辑器 JSON 格式一致。")]
        [SerializeField]
        private BTDefinitionScriptableObject _definition;

        [Tooltip("勾选后仅在运行模式下于 Update 中每帧执行行为树；关闭后不自动 Tick，可调用 TickOnce 或自行驱动。")]
        [SerializeField]
        private bool _autoTick = true;

        [Tooltip(
            "未使用 btref: 时，解析相对层级路径的 Transform 根（Transform.Find）。为空则用本物体 Transform。场景引用请优先在定义资产的 Object Bindings 中登记（图编辑器绑定 SO）。")]
        [SerializeField]
        private Transform _objectReferenceRoot;

        private BehaviourTree _tree;
        private BTContext _context;
        private readonly Dictionary<string, BTStatus> _runtimeNodeStatuses = new Dictionary<string, BTStatus>();

        public BTDefinitionScriptableObject Definition => _definition;

        /// <summary>是否由本组件在 Update 中自动 Tick（仅 Play 模式生效）。</summary>
        public bool AutoTick
        {
            get => _autoTick;
            set => _autoTick = value;
        }

        /// <summary>与当前运行时树绑定的黑板；自动 Tick 与 <see cref="TickOnce"/> 共用同一份实例。</summary>
        public BTContext Context => _context;

        /// <summary>由定义构建并缓存在此组件上的树；未配置或 JSON 无效时为 null。</summary>
        public BehaviourTree RuntimeTree => _tree;

        /// <summary>上一完整根 Tick 内各 <see cref="BTNode.DebugNodeId"/> 的返回状态（供编辑器叠加）。</summary>
        public IReadOnlyDictionary<string, BTStatus> RuntimeNodeStatuses => _runtimeNodeStatuses;

        private void Awake()
        {
            _context = new BTContext { DebugStatusSink = this };
            RebuildTree();
        }

        private void Update()
        {
            if (!Application.isPlaying || !_autoTick || _tree == null)
                return;
            TickTreeInternal();
        }

        /// <summary>从资产新建一棵行为树（不写入本组件缓存，与 <see cref="RuntimeTree"/> 无关）。</summary>
        public BehaviourTree CreateRuntimeTree(BTDefinitionLoadContext context = null)
        {
            if (_definition == null)
                return null;
            context ??= BTDefinitionLoadContext.CreateWithBuiltIns();
            ApplyResolveRoot(context);
            _definition.EnsureBindingResolverOnContext(context);
            return BTDefinitionLoader.LoadTree(_definition.Json, context);
        }

        /// <summary>手动执行一次 Tick（与自动 Tick 共用 <see cref="Context"/>）。</summary>
        public BTStatus TickOnce()
        {
            if (_tree == null)
                return BTStatus.Failure;
            return TickTreeInternal();
        }

        /// <summary>重置运行时树根及以下节点记忆（不打断 Context 中的黑板数据）。</summary>
        public void ResetRuntimeTree() => _tree?.Reset();

        /// <summary>重新从当前 <see cref="Definition"/> 解析并替换 <see cref="RuntimeTree"/>（会先 Reset 旧树）。</summary>
        public void ReloadFromDefinition()
        {
            _tree?.Reset();
            RebuildTree();
        }

        void IBTDebugStatusSink.BeginTickFrame() => BeginTickFrame();

        void IBTDebugStatusSink.RecordNodeStatus(string nodeId, BTStatus status)
        {
            if (string.IsNullOrEmpty(nodeId))
                return;
            _runtimeNodeStatuses[nodeId] = status;
        }

        private BTStatus TickTreeInternal()
        {
            BeginTickFrame();
            return _tree.Tick(_context);
        }

        private void BeginTickFrame() => _runtimeNodeStatuses.Clear();

        private void RebuildTree()
        {
            _tree = null;
            if (_definition == null)
                return;

            var json = _definition.Json;
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                var ctx = BTDefinitionLoadContext.CreateWithBuiltIns();
                ApplyResolveRoot(ctx);
                _definition.EnsureBindingResolverOnContext(ctx);
                _tree = BTDefinitionLoader.LoadTree(json, ctx);
            }
            catch (Exception ex)
            {
                Debug.LogError($"BehaviourTreeRunner on '{name}': 无法从定义构建行为树 — {ex.Message}", this);
            }
        }

        private void ApplyResolveRoot(BTDefinitionLoadContext ctx)
        {
            if (ctx == null)
                return;
            ctx.UnityObjectResolveRoot = _objectReferenceRoot != null ? _objectReferenceRoot : transform;
        }
    }
}
