using Newtonsoft.Json;

namespace Shibafu.BehaviourTree.Serialization
{
    /// <summary>
    /// 从 <see cref="BTDefinitionDocument"/> 或 JSON 文本构建运行时 <see cref="BehaviourTree"/>。
    /// </summary>
    public static class BTDefinitionLoader
    {
        public const int SupportedFormatVersion = 1;

        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>解析 JSON 为文档（不构建树）。</summary>
        public static BTDefinitionDocument ParseDocument(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new BTDefinitionException("JSON is empty.");
            try
            {
                return JsonConvert.DeserializeObject<BTDefinitionDocument>(json, SerializerSettings)
                       ?? throw new BTDefinitionException("JSON deserialized to null.");
            }
            catch (JsonException ex)
            {
                throw new BTDefinitionException("Invalid JSON: " + ex.Message, ex);
            }
        }

        /// <summary>校验版本并构建树。context 为 null 时使用内置类型注册表。</summary>
        public static BehaviourTree LoadTree(string json, BTDefinitionLoadContext context = null)
        {
            var doc = ParseDocument(json);
            BTDefinitionDocumentIds.NormalizeUniqueIds(doc);
            return LoadTreeAfterNormalized(doc, context);
        }

        public static BehaviourTree LoadTree(BTDefinitionDocument document, BTDefinitionLoadContext context = null)
        {
            if (document?.Root == null)
                throw new BTDefinitionException("Document or root is null.");

            if (document.FormatVersion != SupportedFormatVersion)
                throw new BTDefinitionException(
                    $"Unsupported formatVersion {document.FormatVersion}; supported: {SupportedFormatVersion}.");

            BTDefinitionDocumentIds.NormalizeUniqueIds(document);
            return LoadTreeAfterNormalized(document, context);
        }

        private static BehaviourTree LoadTreeAfterNormalized(BTDefinitionDocument document, BTDefinitionLoadContext context)
        {
            context ??= BTDefinitionLoadContext.CreateWithBuiltIns();
            var root = context.BuildNode(document.Root);
            return new BehaviourTree(root);
        }
    }
}
