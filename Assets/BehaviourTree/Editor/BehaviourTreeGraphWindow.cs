using System.IO;
using System.Linq;
using Shibafu.BehaviourTree;
using Shibafu.BehaviourTree.Serialization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shibafu.BehaviourTree.Editor
{
    /// <summary>
    /// 行为树可视化编辑：节点、父子连线；可绑定 <see cref="BTDefinitionScriptableObject"/>，与 <see cref="BehaviourTreeRunner"/> 选中共通。
    /// </summary>
    public sealed class BehaviourTreeGraphWindow : EditorWindow
    {
        private const string PrefKeyLastPath = "Shibafu.BehaviourTree.Editor.LastJsonPath";

        private BehaviourTreeGraphView _graphView;
        private BTDefinitionScriptableObject _boundDefinition;
        private BehaviourTreeRunner _boundRunner;

        static BehaviourTreeGraphWindow()
        {
            Selection.selectionChanged += OnGlobalSelectionChanged;
        }

        private static void OnGlobalSelectionChanged()
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<BehaviourTreeGraphWindow>().Where(x => x != null))
                w.SyncBindingFromSelection();
        }

        [MenuItem("Window/Shibafu/Behaviour Tree 编辑器")]
        public static void Open()
        {
            var w = GetWindow<BehaviourTreeGraphWindow>();
            w.titleContent = new GUIContent("行为树编辑器");
            w.minSize = new Vector2(640, 400);
        }

        /// <summary>打开窗口并加载指定定义（Inspector 按钮等）。</summary>
        public static void OpenAndBind(BTDefinitionScriptableObject definition, BehaviourTreeRunner runner = null)
        {
            if (definition == null)
                return;
            var w = GetWindow<BehaviourTreeGraphWindow>();
            w.titleContent = new GUIContent("行为树编辑器");
            w.minSize = new Vector2(640, 400);
            // 双 delayCall：确保晚于本帧 OnEnable 里的 Sync，避免仍选中其他物体时覆盖本次绑定。
            EditorApplication.delayCall += () =>
            {
                if (w == null)
                    return;
                EditorApplication.delayCall += () =>
                {
                    if (w != null)
                        w.BindToDefinition(definition, runner);
                };
            };
        }

        private void OnEnable()
        {
            EditorApplication.delayCall += DelayedSyncFromSelectionOnce;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void DelayedSyncFromSelectionOnce()
        {
            EditorApplication.delayCall -= DelayedSyncFromSelectionOnce;
            SyncBindingFromSelection();
        }

        /// <summary>Play 下将绑定资产对应的 Runner 状态同步到图节点左侧色条。</summary>
        private void OnEditorUpdate()
        {
            if (_graphView == null)
                return;
            if (!EditorApplication.isPlaying)
            {
                _graphView.ApplyRuntimeDebugStatuses(null);
                return;
            }

            if (_boundDefinition == null)
            {
                _graphView.ApplyRuntimeDebugStatuses(null);
                return;
            }

            var runner = ResolveRuntimeDebugRunner();
            if (runner == null)
                _graphView.ApplyRuntimeDebugStatuses(null);
            else
                _graphView.ApplyRuntimeDebugStatuses(runner.RuntimeNodeStatuses);
        }

        private BehaviourTreeRunner ResolveRuntimeDebugRunner()
        {
            if (_boundDefinition == null)
                return null;

            var go = Selection.activeGameObject;
            if (go != null)
            {
                var r = go.GetComponent<BehaviourTreeRunner>();
                if (r != null && r.Definition == _boundDefinition && r.isActiveAndEnabled)
                    return r;
            }

            BehaviourTreeRunner pick = null;
            foreach (var r in Object.FindObjectsOfType<BehaviourTreeRunner>())
            {
                if (r == null || !r.isActiveAndEnabled || r.Definition != _boundDefinition)
                    continue;
                pick ??= r;
            }

            return pick;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.style.flexGrow = 1;

            var toolbar = new Toolbar();

            toolbar.Add(new ToolbarButton(NewGraph) { text = "新建" });
            toolbar.Add(new ToolbarButton(LoadJsonFromFile) { text = "打开 JSON…" });
            toolbar.Add(new ToolbarButton(SavePrimary) { text = "保存" });

            var addMenu = new ToolbarMenu { text = "添加节点" };
            addMenu.menu.AppendAction("在视图中心", _ => SpawnAtViewCenter(), DropdownMenuAction.AlwaysEnabled);
            toolbar.Add(addMenu);

            rootVisualElement.Add(toolbar);

            _graphView = new BehaviourTreeGraphView();
            _graphView.style.flexGrow = 1;
            rootVisualElement.Add(_graphView);

            if (_boundDefinition != null)
                LoadFromBoundDefinition();
        }

        /// <summary>选中带 <see cref="BehaviourTreeRunner"/> 的物体或定义资产时，若窗口已打开则切换绑定。</summary>
        internal void SyncBindingFromSelection()
        {
            if (_graphView == null)
                return;
            var def = TryGetDefinitionFromSelection(out var runner);
            if (def == null)
                return;
            if (def == _boundDefinition)
                return;
            BindToDefinition(def, runner);
        }

        private static BTDefinitionScriptableObject TryGetDefinitionFromSelection(out BehaviourTreeRunner runner)
        {
            runner = null;
            var objs = Selection.objects;
            if (objs == null || objs.Length != 1)
                return null;

            var obj = Selection.activeObject;
            if (obj is BTDefinitionScriptableObject so)
                return so;

            if (obj is GameObject go)
            {
                runner = go.GetComponent<BehaviourTreeRunner>();
                return runner != null ? runner.Definition : null;
            }

            if (obj is BehaviourTreeRunner r)
            {
                runner = r;
                return r.Definition;
            }

            return null;
        }

        public void BindToDefinition(BTDefinitionScriptableObject definition, BehaviourTreeRunner runner = null)
        {
            _boundDefinition = definition;
            _boundRunner = runner;
            if (_graphView != null)
                LoadFromBoundDefinition();
            UpdateTitle();
        }

        private void ClearBinding()
        {
            _boundDefinition = null;
            _boundRunner = null;
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            if (_boundDefinition == null)
                titleContent = new GUIContent("行为树编辑器");
            else if (_boundRunner != null)
                titleContent = new GUIContent($"行为树 — {_boundRunner.gameObject.name} ({_boundDefinition.name})");
            else
                titleContent = new GUIContent($"行为树 — {_boundDefinition.name}");
        }

        private void LoadFromBoundDefinition()
        {
            if (_graphView == null || _boundDefinition == null)
                return;
            var json = _boundDefinition.Json;
            if (string.IsNullOrWhiteSpace(json))
            {
                _graphView.ClearGraph();
                return;
            }

            try
            {
                var doc = BTDefinitionLoader.ParseDocument(json);
                var addedIds = BehaviourTreeGraphSerializer.DocumentToGraph(doc, _graphView);
                if (addedIds)
                {
                    Undo.RecordObject(_boundDefinition, "Assign Behaviour Tree node ids");
                    _boundDefinition.Json = BTDefinitionIO.SerializeDocument(doc);
                    EditorUtility.SetDirty(_boundDefinition);
                }
            }
            catch (BTDefinitionException ex)
            {
                EditorUtility.DisplayDialog("加载失败", ex.Message, "确定");
                _graphView.ClearGraph();
            }
        }

        private void NewGraph()
        {
            if (!EditorUtility.DisplayDialog("新建", "清空当前图中的所有节点？", "确定", "取消"))
                return;
            _graphView.ClearGraph();
        }

        private void LoadJsonFromFile()
        {
            var start = EditorPrefs.GetString(PrefKeyLastPath, Application.dataPath);
            var path = EditorUtility.OpenFilePanel("打开行为树 JSON", Path.GetDirectoryName(start) ?? "", "json");
            if (string.IsNullOrEmpty(path))
                return;
            EditorPrefs.SetString(PrefKeyLastPath, path);

            try
            {
                var doc = BTDefinitionIO.LoadDocumentFromJsonFile(path);
                ClearBinding();
                BehaviourTreeGraphSerializer.DocumentToGraph(doc, _graphView);
            }
            catch (BTDefinitionException ex)
            {
                EditorUtility.DisplayDialog("打开失败", ex.Message, "确定");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("打开失败", ex.Message, "确定");
            }
        }

        /// <summary>有绑定则写回资产，否则弹出保存 JSON 对话框。</summary>
        private void SavePrimary()
        {
            if (_boundDefinition != null)
            {
                SaveToBoundAsset();
                return;
            }

            var doc = BehaviourTreeGraphSerializer.GraphToDocument(_graphView, out var error);
            if (doc == null)
            {
                EditorUtility.DisplayDialog("无法保存", error ?? "未知错误", "确定");
                return;
            }

            var start = EditorPrefs.GetString(PrefKeyLastPath, Application.dataPath);
            var path = EditorUtility.SaveFilePanel("保存行为树 JSON", Path.GetDirectoryName(start) ?? "", "BehaviourTree", "json");
            if (string.IsNullOrEmpty(path))
                return;
            EditorPrefs.SetString(PrefKeyLastPath, path);

            try
            {
                BTDefinitionIO.SaveJson(path, doc);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("已保存", path, "确定");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("保存失败", ex.Message, "确定");
            }
        }

        private void SaveToBoundAsset()
        {
            var doc = BehaviourTreeGraphSerializer.GraphToDocument(_graphView, out var error);
            if (doc == null)
            {
                EditorUtility.DisplayDialog("无法保存", error ?? "未知错误", "确定");
                return;
            }

            Undo.RecordObject(_boundDefinition, "Save Behaviour Tree");
            _boundDefinition.Json = BTDefinitionIO.SerializeDocument(doc);
            EditorUtility.SetDirty(_boundDefinition);
            AssetDatabase.SaveAssets();
        }

        private void SpawnAtViewCenter()
        {
            var gv = _graphView;
            var r = gv.contentViewContainer.layout;
            var pos = r.width > 16f && r.height > 16f
                ? new Vector2(r.width * 0.5f - BTGraphNode.DefaultWidth * 0.5f, r.height * 0.5f)
                : new Vector2(400f, 200f);
            gv.CreateNodeAt(pos);
        }
    }
}
