using Shibafu.BehaviourTree;
using UnityEditor;
using UnityEngine;

namespace Shibafu.BehaviourTree.Editor
{
    [CustomEditor(typeof(BehaviourTreeRunner))]
    public sealed class BehaviourTreeRunnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var r = (BehaviourTreeRunner)target;
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(r.Definition == null))
            {
                if (GUILayout.Button("在行为树编辑器中打开"))
                    BehaviourTreeGraphWindow.OpenAndBind(r.Definition, r);
            }

            if (r.Definition == null)
                EditorGUILayout.HelpBox("指定 Behaviour Tree Definition 资产后，可在此打开编辑器，或在窗口已打开时选中本物体自动切换。", MessageType.Info);
            else
                EditorGUILayout.HelpBox("「Auto Tick」勾选时进入 Play 后会在 Update 里每帧 Tick；关闭时可自行调用 TickOnce() 或从别的系统驱动。Play 时若行为树编辑器绑定同一资产，节点左侧色条会显示上一帧各节点状态（绿 Success / 红 Failure / 蓝 Running）。", MessageType.None);
        }
    }
}
