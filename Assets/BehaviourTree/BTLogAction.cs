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
        [BTNodeInspectorField(Label = "消息文本")]
        private string message = string.Empty;

        [BTNodeInspectorField(Label = "日志级别")]
        private LogLevel level = LogLevel.Log;

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
            this.message = message ?? string.Empty;
            this.level = level;
        }

        protected override BTStatus OnTick(BTContext context)
        {
            switch (level)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(message);
                    break;
                case LogLevel.Error:
                    Debug.LogError(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }

            return BTStatus.Success;
        }
    }
}
