using System;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 标记参与行为树节点 <c>data</c> 序列化的字段或属性；JSON 键与成员名一致（区分大小写）。
    /// 编辑器据此绘制 Inspector；加载时由 <see cref="BTNodeReferenceBinder"/> 按成员名从 <c>data</c> 写入值。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class BTNodeInspectorFieldAttribute : Attribute
    {
        public BTNodeInspectorFieldAttribute()
        {
        }

        /// <summary>
        /// 为 true 时不生成 Inspector 行（适合复杂结构或暂不支持的类型）；data 中对应键需自行维护。
        /// </summary>
        public bool ManualJsonOnly { get; set; }

        /// <summary>可选；为空时用 Unity 对成员名的可读化作为标签。</summary>
        public string Label { get; set; }

        /// <summary>
        /// 字段类型为 <see cref="string"/> 时使用：在面板中显示为 ObjectField，JSON 中仍存 GlobalObjectId 字符串。
        /// </summary>
        public Type ObjectReferenceType { get; set; }
    }
}
