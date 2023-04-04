using UnityEngine;
using UnityEditor;

namespace EditorAssetTools {
    /// <summary>
    /// 编辑器多工具集成窗口
    /// </summary>
    public class EditorAssetToolsWindow : EditorWindow {
        [MenuItem("Tools/编辑器资源工具")]
        private static void OpenInProjectView() { //主方法
            OpenToolsWindow(); //打开窗口
        }

        private int _toolIndex = -1;
        private string[] _toolDisplayArray;
        private readonly BaseAssetTool[] _toolArray = {
            new ModifyNameAssetTool(),
            new ReferenceSearchAssetTool(),
            new TextureCheckTool(),
            new TransparentGraphicPrefabCheckTool(),
        };
        private static void OpenToolsWindow() { //打开窗口
            if (Application.isPlaying) {
                Debug.LogError("不允许在运行状态下打开资源工具窗口");
                return;
            }
            var window = GetWindow<EditorAssetToolsWindow>(false, "资源工具", false);
            FocusWindowIfItsOpen<EditorAssetToolsWindow>();
            window.Show();
        }

        private void OnEnable() { //多工具脚本OnEnable时调用
            _toolDisplayArray = new string[_toolArray.Length];
            for (var idx = 0; idx < _toolArray.Length; ++idx) {
                var tool = _toolArray[idx];
                tool.DoInit();
                _toolDisplayArray[idx] = tool.Name;
            }
            if (_toolIndex < 0) SelectTool(0);
            Selection.selectionChanged += OnSelectChange;
        }
        private void OnDisable() {
            foreach (var tool in _toolArray) tool.DoDestroy();
            Selection.selectionChanged -= OnSelectChange;
        }

        private void SelectTool(int index) {
            if (_toolIndex == index) return;
            _toolIndex = index;
            _toolArray[_toolIndex].DoShow();
        }
        private void OnGUI() {
            if (GUILayout.Button("-->说明文档<--", EditorStyles.toolbarButton)) {
                Application.OpenURL("https://note.youdao.com/s/cArtSc6b");
            }
            var selectIndex = GUILayout.Toolbar(_toolIndex, _toolDisplayArray, EditorStyles.toolbarButton);
            if (selectIndex != _toolIndex) {
                SelectTool(selectIndex);
            }
            EditorGUILayout.Space();
            _toolArray[_toolIndex].OnGUI();
        }
        private void Update() {
            if (Application.isPlaying) {
                Close();
                return;
            }
            foreach (var tool in _toolArray) tool.Update();
        }

        private void OnSelectChange() {
            _toolArray[_toolIndex].OnSelectChange();
            Repaint();
        }
    }
}