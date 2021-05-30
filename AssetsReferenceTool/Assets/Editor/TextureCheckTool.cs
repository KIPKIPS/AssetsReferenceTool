using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace EditorAssetTools {
    /// <summary>
    /// 贴图检查
    /// </summary>
    public class TextureCheckTool : BaseAssetTool {
        class SearchObjInfo {
            public string tex_display_path;
            public string tex_path;
            public Texture tex_asset;
            public List<string> ref_path_list = new List<string>();
        }

        List<SearchObjInfo> m_search_obj_list = new List<SearchObjInfo>();
        int m_check_width = 512;
        int m_check_height = 512;
        float scroll_item_index = 0;

        public override string Name {
            get { return "贴图尺寸检查"; }
        }
        public override void DoInit() {
            base.DoInit();
        }
        public override void DoDestroy() {
            m_search_obj_list.Clear();
            base.DoDestroy();
        }

        public override void OnGUI() {
            EditorGUILayout.BeginVertical();
            if (!string.IsNullOrEmpty(select_asset_path)) {
                EditorGUILayout.LabelField("当前选择的查找路径：" + select_asset_path);
            } else {
                EditorGUILayout.LabelField("看这儿！！！！-->请先从Project窗口右侧选择需要查找的资源或文件夹");
            }
            GUILayout.Space(10);
            m_check_width = EditorGUILayout.IntField("最小的宽", m_check_width);
            m_check_height = EditorGUILayout.IntField("最小的高", m_check_height);
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("开始查找")) {
                try { DoSearch(); } finally { EditorUtility.ClearProgressBar(); }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            if (m_search_obj_list.Count == 0) return;

            //show asset list
            GUILayout.Space(10);
            EditorGUILayout.LabelField(string.Format("找到目标资源总数--> {0}", m_search_obj_list.Count));
            GUILayout.Space(10);
            Event cur_evt = Event.current;
            if (cur_evt != null && cur_evt.isScrollWheel) {
                scroll_item_index += cur_evt.delta.y;
                cur_evt.Use();
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            const int k_view_item_count = 25;
            int cur_item_index = 0;
            for (int idx = 0; idx < m_search_obj_list.Count; ++idx) {
                var u_obj_info = m_search_obj_list[idx];
                Color gui_color = GUI.contentColor;
                GUI.contentColor = Color.green;
                if (cur_item_index >= scroll_item_index && cur_item_index <= scroll_item_index + k_view_item_count) {
                    using (var hor_scope = new EditorGUILayout.HorizontalScope()) {
                        EditorGUILayout.LabelField(string.Format("目标资源{0}--> {1}", idx, u_obj_info.tex_display_path));
                        if (GUILayout.Button(string.Format("定位资源({0}个引用)", u_obj_info.ref_path_list.Count), GUILayout.Width(300))) {
                            Selection.activeObject = AssetDatabase.LoadAssetAtPath(u_obj_info.tex_display_path, typeof(UnityEngine.Object));
                        }
                    }
                }
                GUI.contentColor = gui_color;
                ++cur_item_index;
                for (int k = 0; k < u_obj_info.ref_path_list.Count; ++k) {
                    if (cur_item_index >= this.scroll_item_index && cur_item_index <= this.scroll_item_index + k_view_item_count) {
                        EditorGUILayout.BeginHorizontal();
                        string path = u_obj_info.ref_path_list[k];
                        EditorGUILayout.LabelField(string.Format("\t{0}.{1}", k, path));
                        if (path.EndsWith(".prefab") && GUILayout.Button("定位组件", GUILayout.Width(100))) {
                            TryLocationPrefabComponentByAsset(u_obj_info.tex_path, path);
                        }
                        if (GUILayout.Button("定位资源", GUILayout.Width(100))) {
                            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                            if (asset != null) Selection.activeObject = asset;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    ++cur_item_index;
                }
            }
            EditorGUILayout.EndVertical();
            float scroll_max_value = cur_item_index - k_view_item_count;
            scroll_item_index = GUILayout.VerticalScrollbar(scroll_item_index, 0, 0f, scroll_max_value + 1, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndHorizontal();
        }

        void DoSearch() {
            m_search_obj_list.Clear();
            if (string.IsNullOrEmpty(select_asset_path)) {
                return;
            }
            //find all file asset path
            List<string> all_obj_path_list = new List<string>();
            GetAssetPathsFromDirectory(select_asset_path, ref all_obj_path_list);
            if (all_obj_path_list.Count == 0) {
                return;
            }
            Dictionary<Texture, SearchObjInfo> target_dict = new Dictionary<Texture, SearchObjInfo>();
            for (int i = 0; i < all_obj_path_list.Count; ++i) {
                string asset_path = all_obj_path_list[i];
                EditorUtility.DisplayProgressBar("Check Asset", asset_path, (float)i / all_obj_path_list.Count);
                foreach (var ref_path in AssetDatabase.GetDependencies(asset_path, true)) {
                    Texture tex = AssetDatabase.LoadAssetAtPath<Texture>(ref_path);
                    if (tex != null && (tex.width >= m_check_width || tex.height >= m_check_height)) {
                        SearchObjInfo u_info;
                        if (!target_dict.TryGetValue(tex, out u_info)) {
                            u_info = new SearchObjInfo();
                            string display_path = ref_path + string.Format(" {0}X{1}", tex.width, tex.height);
                            u_info.tex_display_path = display_path;
                            u_info.tex_path = ref_path;
                            u_info.tex_asset = tex;
                            target_dict[tex] = u_info;
                        }
                        u_info.ref_path_list.Add(asset_path);
                    }
                }
            }
            m_search_obj_list.AddRange(target_dict.Values);
            m_search_obj_list.Sort((info_a, info_b) => {
                int size_a = info_a.tex_asset.width * info_a.tex_asset.height;
                int size_b = info_b.tex_asset.width * info_b.tex_asset.height;
                return size_b.CompareTo(size_a);
            });
        }
    }
}
