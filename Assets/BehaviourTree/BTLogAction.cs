using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 每次 Tick 使用 Unity 日志输出字符串，并返回 <see cref="BTStatus.Success"/>。
    /// JSON：<c>data.message</c>；可选 <c>data.level</c> 为 <c>warning</c> / <c>error</c>。
    /// </summary>
    [BTNodeType("log", "日志 Log", EditorMenuPath = "Leaf/Debug")]
    public sealed class BTLogAction : BTAction
    {
        [BTNodeInspectorField("message")]
        private string _message = string.Empty;

        [BTNodeInspectorField("level")]
        private LogLevel _level = LogLevel.Log;

        public enum LogLevel
        {
            Log,
            Warning,
            Error
        }

        /// <summary>供 JSON 反射创建。</summary>
        public BTLogAction() : base("Log")
        {
        }

        public BTLogAction(string message, LogLevel level = LogLevel.Log, string name = null) : base(name ?? "Log")
        {
            _message = message ?? string.Empty;
            _level = level;
        }

        public override void InitFromJson(JObject data)
        {
            base.InitFromJson(data);
            if (data == null)
                return;
            _message = data["message"]?.Value<string>() ?? string.Empty;
            var levelRaw = data["level"]?.Value<string>();
            _level = LogLevel.Log;
            if (!string.IsNullOrEmpty(levelRaw))
            {
                if (string.Equals(levelRaw, "warning", StringComparison.OrdinalIgnoreCase))
                    _level = LogLevel.Warning;
                else if (string.Equals(levelRaw, "error", StringComparison.OrdinalIgnoreCase))
                    _level = LogLevel.Error;
            }
        }

        protected override BTStatus OnTick(BTContext context)
        {
            switch (_level)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(_message);
                    break;
                case LogLevel.Error:
                    Debug.LogError(_message);
                    break;
                default:
                    Debug.Log(_message);
                    break;
            }

            return BTStatus.Success;
        }
    }
}
