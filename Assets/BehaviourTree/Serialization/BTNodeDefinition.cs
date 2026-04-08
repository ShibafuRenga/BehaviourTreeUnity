using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Shibafu.BehaviourTree.Serialization
{
    /// <summary>
    /// JSON / 文本描述中的单个节点。自定义节点使用唯一 <see cref="Type"/> 字符串，参数放在 <see cref="Data"/>。
    /// </summary>
    public sealed class BTNodeDefinition
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>稳定节点 id，用于运行时与编辑器节点对应（可视化调试）。</summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>GraphView 内容坐标（与 Unity <c>GetPosition().xMin/yMin</c> 一致）；仅编辑器使用，运行时忽略。</summary>
        [JsonProperty("editorX", NullValueHandling = NullValueHandling.Ignore)]
        public float? EditorX { get; set; }

        [JsonProperty("editorY", NullValueHandling = NullValueHandling.Ignore)]
        public float? EditorY { get; set; }

        [JsonProperty("data")]
        public JObject Data { get; set; }

        [JsonProperty("children")]
        public List<BTNodeDefinition> Children { get; set; }
    }

    /// <summary>
    /// 行为树文档根。可用 <see cref="BTDefinitionIO"/> 读写 JSON 或 UTF-8 字节（与 JSON 同内容）。
    /// </summary>
    public sealed class BTDefinitionDocument
    {
        [JsonProperty("formatVersion")]
        public int FormatVersion { get; set; } = 1;

        [JsonProperty("root")]
        public BTNodeDefinition Root { get; set; }
    }
}
