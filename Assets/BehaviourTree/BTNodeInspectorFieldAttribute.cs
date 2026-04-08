using System;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 标记与 JSON <c>data</c> 中某键对应的字段，供行为树图编辑器绘制 Inspector 式控件。
    /// 对 <see cref="UnityEngine.Object"/> 派生类型（<see cref="UnityEngine.Transform"/>、<see cref="UnityEngine.GameObject"/>、组件等）：
    /// 手写代码用强类型字段，JSON 中存 GlobalObjectId 字符串；加载时由 <see cref="BTNodeReferenceBinder"/> 自动赋值。
    /// 其它类型仍按成员类型推断控件（string、数值、枚举等）；<see cref="ObjectReferenceType"/> 仅用于「字段声明为 string、面板仍拖引用」的少数情况。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class BTNodeInspectorFieldAttribute : Attribute
    {
        public BTNodeInspectorFieldAttribute(string jsonKey)
        {
            JsonKey = jsonKey ?? throw new ArgumentNullException(nameof(jsonKey));
        }

        /// <summary>与序列化 JSON 中 <c>data</c> 对象的键名一致。</summary>
        public string JsonKey { get; }

        /// <summary>
        /// 为 true 时不生成 Inspector 行（适合复杂结构或暂不支持的类型）；data 中对应键需自行维护。
        /// </summary>
        public bool ManualJsonOnly { get; set; }

        /// <summary>可选；默认由 <see cref="JsonKey"/> 生成可读标签。</summary>
        public string Label { get; set; }

        /// <summary>
        /// 字段类型为 <see cref="string"/> 时使用：在面板中显示为 ObjectField，JSON 中仍存 GlobalObjectId 字符串。
        /// </summary>
        public Type ObjectReferenceType { get; set; }
    }
}
