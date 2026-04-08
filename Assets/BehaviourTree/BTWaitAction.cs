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
        private float _seconds;
        private bool _useUnscaledTime;
        private float? _endTime;

        public BTWaitAction() : base("Wait")
        {
        }

        public BTWaitAction(float seconds, bool unscaledTime = false, string name = null) : base(name ?? "Wait")
        {
            _seconds = Math.Max(0f, seconds);
            _useUnscaledTime = unscaledTime;
        }

        public override void InitFromJson(JObject data)
        {
            if (data == null)
                return;
            var s = data["seconds"] ?? data["duration"];
            _seconds = s != null ? Math.Max(0f, s.Value<float>()) : 0f;
            _useUnscaledTime = data["unscaled"]?.Value<bool>() ?? false;
        }

        public override void Reset()
        {
            _endTime = null;
        }

        protected override BTStatus OnTick(BTContext context)
        {
            if (_seconds <= 0f)
                return BTStatus.Success;

            var now = _useUnscaledTime ? Time.unscaledTime : Time.time;
            if (!_endTime.HasValue)
                _endTime = now + _seconds;

            if (now < _endTime.Value)
                return BTStatus.Running;

            _endTime = null;
            return BTStatus.Success;
        }
    }
}
