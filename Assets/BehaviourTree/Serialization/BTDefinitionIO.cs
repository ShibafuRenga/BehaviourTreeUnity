using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Shibafu.BehaviourTree.Serialization
{
    /// <summary>
    /// JSON 文本或 UTF-8 二进制（与 JSON 字节相同，便于扩展为压缩流等）读写。
    /// </summary>
    public static class BTDefinitionIO
    {
        private static readonly JsonSerializerSettings WriteSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static string SerializeDocument(BTDefinitionDocument document)
        {
            if (document == null)
                throw new System.ArgumentNullException(nameof(document));
            return JsonConvert.SerializeObject(document, WriteSettings);
        }

        public static void SaveJson(string filePath, BTDefinitionDocument document)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(filePath, SerializeDocument(document), new UTF8Encoding(false));
        }

        public static BTDefinitionDocument LoadDocumentFromJsonFile(string filePath)
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            return BTDefinitionLoader.ParseDocument(json);
        }

        /// <summary>将 JSON 的 UTF-8 字节写入文件（无 BOM）。与 JSON 内容一一对应，便于当作「二进制」分发同一份结构。</summary>
        public static void SaveUtf8Bytes(string filePath, BTDefinitionDocument document)
        {
            var bytes = Encoding.UTF8.GetBytes(SerializeDocument(document));
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(filePath, bytes);
        }

        public static BTDefinitionDocument LoadDocumentFromUtf8Bytes(string filePath)
        {
            var bytes = File.ReadAllBytes(filePath);
            var json = Encoding.UTF8.GetString(bytes);
            return BTDefinitionLoader.ParseDocument(json);
        }

        public static BehaviourTree LoadTreeFromJsonFile(string filePath, BTDefinitionLoadContext context = null)
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            return BTDefinitionLoader.LoadTree(json, context);
        }

        public static BehaviourTree LoadTreeFromUtf8BytesFile(string filePath, BTDefinitionLoadContext context = null)
        {
            var doc = LoadDocumentFromUtf8Bytes(filePath);
            return BTDefinitionLoader.LoadTree(doc, context);
        }
    }
}
