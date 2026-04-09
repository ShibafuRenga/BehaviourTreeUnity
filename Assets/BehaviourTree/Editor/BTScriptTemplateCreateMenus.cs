using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace Shibafu.BehaviourTree.Editor
{
    /// <summary>
    /// Project 窗口右键 Create：先进入重命名，确认后用最终文件名生成脚本内容（类名、type、显示名与文件名一致，经合法化）。
    /// </summary>
    internal static class BTScriptTemplateCreateMenus
    {
        private const string MenuRoot = "Assets/Create/Shibafu/Behaviour Tree/";

        [MenuItem(MenuRoot + "Behaviour Tree Action", false, 81)]
        private static void CreateActionScript()
        {
            StartCreateWithRename("NewBehaviourTreeAction", ActionTemplate);
        }

        [MenuItem(MenuRoot + "Behaviour Tree Condition", false, 82)]
        private static void CreateConditionScript()
        {
            StartCreateWithRename("NewBehaviourTreeCondition", ConditionTemplate);
        }

        private static void StartCreateWithRename(string defaultBaseName, string template)
        {
            var dir = GetActiveFolderPath();
            var uniquePath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dir, defaultBaseName + ".cs"));
            var end = ScriptableObject.CreateInstance<BTScriptTemplateEndNameEditAction>();
            end.Template = template;
            end.FallbackStem = defaultBaseName;
            Texture2D icon = null;
            var iconContent = EditorGUIUtility.IconContent("cs Script Icon");
            if (iconContent?.image is Texture2D t)
                icon = t;
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, end, uniquePath, icon, null);
        }

        internal static string BuildScriptBodyFromAssetPath(string assetPath, string template, string fallbackStem)
        {
            assetPath = assetPath.Replace('\\', '/');
            var fileStem = Path.GetFileNameWithoutExtension(assetPath);
            var className = SanitizeCSharpIdentifier(fileStem, fallbackStem);
            var typeId = ToCamelTypeId(className);
            var displayName = className;
            return template
                .Replace("__CLASS_NAME__", className)
                .Replace("__TYPE_ID__", typeId)
                .Replace("__DISPLAY_NAME__", displayName);
        }

        private static string GetActiveFolderPath()
        {
            var obj = Selection.activeObject;
            if (obj == null)
                return "Assets";
            var p = AssetDatabase.GetAssetPath(obj);
            return Directory.Exists(p) ? p : Path.GetDirectoryName(p) ?? "Assets";
        }

        /// <summary>首字母小写，用作 JSON <c>type</c> 与 <see cref="BTNodeTypeAttribute.TypeId"/> 的默认约定。</summary>
        private static string ToCamelTypeId(string className)
        {
            if (string.IsNullOrEmpty(className))
                return "custom";
            return char.ToLowerInvariant(className[0]) + className.Substring(1);
        }

        /// <summary>去掉空格等非法字符，避免 <c>Foo 1.cs</c> 生成非法类名。</summary>
        private static string SanitizeCSharpIdentifier(string raw, string fallback)
        {
            if (string.IsNullOrEmpty(raw))
                raw = fallback;
            var filtered = new string(raw.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            if (string.IsNullOrEmpty(filtered))
                filtered = new string(fallback.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            if (string.IsNullOrEmpty(filtered))
                filtered = "GeneratedBehaviourTreeNode";
            if (char.IsDigit(filtered[0]))
                filtered = "_" + filtered;
            return filtered;
        }

        private const string ActionTemplate = @"using Newtonsoft.Json.Linq;
using Shibafu.BehaviourTree.Serialization;
using Shibafu.BehaviourTree;
using UnityEngine;


/// <summary>JSON 中节点的 <c>type</c> 为 <c>__TYPE_ID__</c>。</summary>
[BTNodeType(""__TYPE_ID__"", ""__DISPLAY_NAME__"", EditorMenuPath = ""Leaf"")]
public sealed class __CLASS_NAME__ : BTAction
{
    public __CLASS_NAME__() : base(""__DISPLAY_NAME__"")
    {
    }

    protected override BTStatus OnTick(BTContext context)
    {
        return BTStatus.Success;
    }
}
";

        private const string ConditionTemplate = @"using Newtonsoft.Json.Linq;
using Shibafu.BehaviourTree.Serialization;
using Shibafu.BehaviourTree;

/// <summary>JSON 中节点的 <c>type</c> 为 <c>__TYPE_ID__</c>（与内置 <c>condition</c> 不同）。</summary>
[BTNodeType(""__TYPE_ID__"", ""__DISPLAY_NAME__"", EditorMenuPath = ""Condition"")]
public sealed class __CLASS_NAME__ : BTSimpleConditionNode
{
    public __CLASS_NAME__() : base(""__DISPLAY_NAME__"")
    {
    }

    public override void InitFromJson(JObject data, BTDefinitionLoadContext loadContext = null)
    {
        base.InitFromJson(data, loadContext);
    }

    protected override bool Evaluate(BTContext context)
    {
        return true;
    }
}
";
    }

    /// <summary>用户完成 Project 窗口内联重命名后写入 .cs 内容。</summary>
    internal sealed class BTScriptTemplateEndNameEditAction : EndNameEditAction
    {
        public string Template;
        public string FallbackStem;

        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            try
            {
                if (string.IsNullOrEmpty(pathName))
                    return;
                pathName = pathName.Replace('\\', '/');
                if (!pathName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    pathName += ".cs";
                pathName = AssetDatabase.GenerateUniqueAssetPath(pathName);

                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                if (string.IsNullOrEmpty(projectRoot))
                    return;
                var fullPath = Path.GetFullPath(Path.Combine(projectRoot, pathName.Replace('/', Path.DirectorySeparatorChar)));
                var parentDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(parentDir))
                    Directory.CreateDirectory(parentDir);

                var body = BTScriptTemplateCreateMenus.BuildScriptBodyFromAssetPath(pathName, Template, FallbackStem);
                File.WriteAllText(fullPath, body);
                AssetDatabase.ImportAsset(pathName);
                AssetDatabase.Refresh();

                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(pathName);
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }
            finally
            {
                DestroyImmediate(this);
            }
        }

        public override void Cancelled(int instanceId, string pathName, string resourceFile)
        {
            DestroyImmediate(this);
        }
    }
}
