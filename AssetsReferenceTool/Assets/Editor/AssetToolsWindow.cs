using UnityEngine;
using UnityEditor;

namespace EditorAssetTools {
    /// <summary>
    /// 编辑器多工具集成窗口
    /// </summary>
    public class EditorAssetToolsWindow : EditorWindow {
        [MenuItem("Tools/编辑器资源工具")]
        static void OpenInProjectView() { //主方法
            EditorAssetToolsWindow.OpenToolsWindow();//打开窗口
        }

        int toolIndex = -1;
        string[] toolDisplayArray;
        BaseAssetTool[] toolArray = new BaseAssetTool[] {
            //new ModifyNameAssetTool(),
            new ReferenceSearchAssetTool(),
            //new TextureCheckTool(),
            //new TransparentGraphicPrefabCheckTool(),
        };
        static void OpenToolsWindow() { //打开窗口
            if (Application.isPlaying) {
                Debug.LogError("不允许在运行状态下打开资源工具窗口");
                return;
            }
            var window = EditorWindow.GetWindow<EditorAssetToolsWindow>(false, "资源工具", false);
            EditorWindow.FocusWindowIfItsOpen<EditorAssetToolsWindow>();
            window.Show();
        }

        void OnEnable() { //多工具脚本OnEnable时调用
            toolDisplayArray = new string[toolArray.Length];
            for (int idx = 0; idx < toolArray.Length; ++idx) {
                BaseAssetTool tool = toolArray[idx];
                tool.toolsWindow = this;
                tool.DoInit();
                toolDisplayArray[idx] = tool.Name;
            }
            if (toolIndex < 0) SelectTool(0);
            Selection.selectionChanged += OnSelectChange;
        }
        void OnDisable() {
            foreach (var tool in toolArray) tool.DoDestroy();
            Selection.selectionChanged -= OnSelectChange;
        }

        void SelectTool(int index) {
            if (toolIndex == index) return;
            toolIndex = index;
            toolArray[toolIndex].DoShow();
        }
        void OnGUI() {
            if (GUILayout.Button("-->说明文档<--", EditorStyles.toolbarButton)) {
                Application.OpenURL("http://note.youdao.com/s/cArtSc6b");
            }
            int select_index = GUILayout.Toolbar(toolIndex, toolDisplayArray, EditorStyles.toolbarButton);
            if (select_index != toolIndex) {
                SelectTool(select_index);
            }
            EditorGUILayout.Space();

            toolArray[toolIndex].OnGUI();
        }
        void Update() {
            if (Application.isPlaying) {
                base.Close();
                return;
            }
            foreach (var tool in toolArray) tool.Update();
        }

        void OnSelectChange() {
            toolArray[toolIndex].OnSelectChange();
            base.Repaint();
        }
    }
}
