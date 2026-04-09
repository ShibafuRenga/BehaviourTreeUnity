using Newtonsoft.Json.Linq;
using Shibafu.BehaviourTree.Serialization;
using UnityEngine;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 在 <see cref="OnStart"/> 记录起点/终点，随后在 <see cref="OnTick"/> 内按 <c>duration</c> 秒插值移动 <c>subject</c> 到 <c>target</c> 世界坐标。
    /// 引用为 <see cref="Transform"/>；推荐在定义 SO 上登记后 JSON 使用 <c>btref:</c>（发布包可用）；或编辑器 GlobalObjectId；或相对 <see cref="BTDefinitionLoadContext.UnityObjectResolveRoot"/> 的路径。
    /// </summary>
    [BTNodeType("moveToTarget", "移动到目标 Move To Target", EditorMenuPath = "Leaf")]
    public sealed class BTMoveToTargetAction : BTAction
    {
        [BTNodeInspectorField(Label = "被移动物体")]
        private Transform subject;

        [BTNodeInspectorField(Label = "目标物体")]
        private Transform target;

        [BTNodeInspectorField(Label = "持续时间（秒）")]
        private float duration = 3f;

        private float _elapsed;
        private Vector3 _startPosition;
        private Vector3 _endPosition;

        public BTMoveToTargetAction() : base("MoveToTarget")
        {
        }

        public override void InitFromJson(JObject data, BTDefinitionLoadContext loadContext = null)
        {
            base.InitFromJson(data, loadContext);
            duration = Mathf.Max(0f, duration);
        }

        protected override void OnStart(BTContext context)
        {
            if (subject == null || this.target == null)
            {
                Debug.LogWarning(
                    "[moveToTarget] subject 或 target 未解析：请在定义资产上登记 btref，或配置 Runner 的引用根与层级路径。");
                return;
            }

            _elapsed = 0f;
            _startPosition = subject.position;
            _endPosition = this.target.position;
        }

        protected override BTStatus OnTick(BTContext context)
        {
            if (subject == null || this.target == null)
                return BTStatus.Failure;

            if (duration <= 0f)
            {
                subject.position = _endPosition;
                return BTStatus.Success;
            }

            _elapsed += Time.deltaTime;
            if (_elapsed >= duration)
            {
                subject.position = _endPosition;
                return BTStatus.Success;
            }

            subject.position = Vector3.Lerp(_startPosition, _endPosition, _elapsed / duration);
            return BTStatus.Running;
        }
    }
}
