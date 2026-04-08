using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 等待指定秒数：期间返回 <see cref="BTStatus.Running"/>，结束后返回 <see cref="BTStatus.Success"/>。
    /// JSON：<c>data.seconds</c> 或 <c>data.duration</c>（秒，浮点）；可选 <c>data.unscaled</c> 为 true 时使用 <see cref="Time.unscaledTime"/>。
    /// </summary>
    [BTNodeType("wait", "等待 Wait", EditorMenuPath = "Leaf")]
    public sealed class BTWaitAction : BTAction
    {
        [BTNodeInspectorField(Label = "秒（可与 duration 键互换）")]
        private float seconds;

        [BTNodeInspectorField(Label = "使用非缩放时间")]
        private bool unscaled;
        private float? _endTime;

        public BTWaitAction() : base("Wait")
        {
        }

        public BTWaitAction(float seconds, bool unscaledTime = false, string name = null) : base(name ?? "Wait")
        {
            this.seconds = Math.Max(0f, seconds);
            unscaled = unscaledTime;
        }

        public override void InitFromJson(JObject data)
        {
            base.InitFromJson(data);
            if (data == null)
                return;
            if (data["seconds"] == null && data["duration"] != null)
                seconds = Math.Max(0f, data["duration"].Value<float>());
            seconds = Math.Max(0f, seconds);
        }

        public override void Reset()
        {
            base.Reset();
            _endTime = null;
        }

        protected override BTStatus OnTick(BTContext context)
        {
            if (seconds <= 0f)
                return BTStatus.Success;

            var now = unscaled ? Time.unscaledTime : Time.time;
            if (!_endTime.HasValue)
                _endTime = now + seconds;

            if (now < _endTime.Value)
                return BTStatus.Running;

            _endTime = null;
            return BTStatus.Success;
        }
    }
}
