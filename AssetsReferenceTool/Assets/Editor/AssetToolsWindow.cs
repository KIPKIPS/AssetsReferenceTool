using UnityEngine;
using UnityEditor;

namespace EditorAssetTools {
    //########copyright-->2019/3/5 LuoYao

    /// <summary>
    /// 编辑器多工具集成窗口
    /// </summary>
    public class EditorAssetToolsWindow : EditorWindow
    {
        [MenuItem("Tools/编辑器资源工具")]
        static void OpenInProjectView()
        {
            EditorAssetToolsWindow.OpenToolsWindow();
        }

        int m_select_tool_index = -1;
        string[] m_tool_display_array;
        BaseAssetTool[] m_tool_array = new BaseAssetTool[] {
            new ModifyNameAssetTool(),
            new ReferenceSearchAssetTool(),
            new TextureCheckTool(),
            new TransparentGraphicPrefabCheckTool(),
        };
        static void OpenToolsWindow()
        {
            if(Application.isPlaying) {
                Debug.LogError("不允许在运行状态下打开资源工具窗口");
                return;
            }
            var window = EditorWindow.GetWindow<EditorAssetToolsWindow>(false, "资源工具", false);
            EditorWindow.FocusWindowIfItsOpen<EditorAssetToolsWindow>();
            window.Show();
        }

        void OnEnable()
        {
            m_tool_display_array = new string[m_tool_array.Length];
            for(int idx = 0; idx < m_tool_array.Length; ++idx) {
                BaseAssetTool tool = m_tool_array[idx];
                tool.toolsWindow = this;
                tool.DoInit();
                m_tool_display_array[idx] = tool.Name;
            }
            if(m_select_tool_index < 0) SelectTool(0);
            Selection.selectionChanged += OnSelectChange;
        }
        void OnDisable()
        {
            foreach (var tool in m_tool_array) tool.DoDestroy();
            Selection.selectionChanged -= OnSelectChange;
        }

        void SelectTool(int index) {
            if(m_select_tool_index == index) return;
            m_select_tool_index = index;
            m_tool_array[m_select_tool_index].DoShow();
        }
        void OnGUI()
        {
            if (GUILayout.Button("-->说明文档<--", EditorStyles.toolbarButton)) {
                Application.OpenURL("http://note.youdao.com/s/cArtSc6b");
            }
            int select_index = GUILayout.Toolbar(m_select_tool_index, m_tool_display_array, EditorStyles.toolbarButton);
            if(select_index != m_select_tool_index) {
                SelectTool(select_index);
            }
            EditorGUILayout.Space();

            m_tool_array[m_select_tool_index].OnGUI();
        }
        void Update()
        {
            if(Application.isPlaying) {
                base.Close();
                return;
            }
            foreach (var tool in m_tool_array) tool.Update();
        }

        void OnSelectChange() {
            m_tool_array[m_select_tool_index].OnSelectChange();
            base.Repaint();
        }
    }
}
