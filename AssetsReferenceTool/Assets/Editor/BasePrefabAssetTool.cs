using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
namespace EditorAssetTools {
    public abstract class BasePrefabAssetTool : BaseAssetTool {
        float scroll_item_index = 0;

        protected class ItemData {
            public GameObject prefab;
            public string prefab_path;
            public List<Component> comp_list;
        }

        List<ItemData> m_item_data_list = new List<ItemData>();

        public override void DoInit() {
            base.DoInit();
        }
        public override void DoDestroy() {
            base.DoDestroy();
        }

        protected void DrawDefaultHeader() {
            if (!string.IsNullOrEmpty(select_asset_path)) {
                EditorGUILayout.LabelField("当前选择的查找路径：" + select_asset_path);
            } else {
                EditorGUILayout.HelpBox("请先从Project窗口右侧选择需要查找的Prefab或文件夹", MessageType.Info);
            }
            GUILayout.Space(10);
        }
        protected void DrawSearchButton() {
            if (!string.IsNullOrEmpty(select_asset_path) && GUILayout.Button("开始查找")) {
                try { DoSearch(); } finally { EditorUtility.ClearProgressBar(); }
            }
        }

        protected void DrawTargetPrefabAssetList(Action<Component, ItemData> draw_comp_action) {
            GUILayout.Space(10);
            EditorGUILayout.LabelField(string.Format("找到目标Prefab总数--> {0}", m_item_data_list.Count));
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
            for (int idx = 0; idx < m_item_data_list.Count; ++idx) {
                var item_data = m_item_data_list[idx];
                Color gui_color = GUI.contentColor;
                GUI.contentColor = Color.green;
                if (cur_item_index >= scroll_item_index && cur_item_index <= scroll_item_index + k_view_item_count) {
                    using (var hor_scope = new EditorGUILayout.HorizontalScope()) {
                        EditorGUILayout.LabelField(string.Format("目标Prefab#{0}--> {1}", idx, item_data.prefab_path));
                        if (GUILayout.Button("定位资源", GUILayout.Width(200))) {
                            Selection.activeObject = item_data.prefab;
                        }
                    }
                }
                GUI.contentColor = gui_color;
                ++cur_item_index;
                for (int k = 0; k < item_data.comp_list.Count; ++k) {
                    if (cur_item_index >= this.scroll_item_index && cur_item_index <= this.scroll_item_index + k_view_item_count) {
                        UnityEngine.Component comp = item_data.comp_list[k];
                        if (draw_comp_action != null) {
                            draw_comp_action(comp, item_data);
                        } else {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(string.Format("\t{0}.{1}", k, comp.ToString()));
                            if (GUILayout.Button("定位", GUILayout.Width(100))) {
                                base.TryLocationPrefabInstanceChildByComponent(item_data.prefab, comp);
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                    ++cur_item_index;
                }
            }
            EditorGUILayout.EndVertical();
            float scroll_max_value = cur_item_index - k_view_item_count;
            scroll_item_index = GUILayout.VerticalScrollbar(scroll_item_index, 0, 0f, scroll_max_value + 1, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndHorizontal();
        }

        protected void DoSearch() {
            if (string.IsNullOrEmpty(select_asset_path)) {
                return;
            }
            List<string> all_obj_path_list = new List<string>();
            GetAssetPathsFromDirectory(select_asset_path, ref all_obj_path_list);
            if (all_obj_path_list.Count == 0) {
                return;
            }
            m_item_data_list.Clear();
            DoPrefabCheckStart();
            int obj_path_count = all_obj_path_list.Count;
            for (int idx = 0; idx < obj_path_count; ++idx) {
                string asset_path = all_obj_path_list[idx];
                EditorUtility.DisplayProgressBar("Check Asset", asset_path, (float)idx / all_obj_path_list.Count);
                if (asset_path.EndsWith(".prefab")) {
                    GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(asset_path);
                    if (go == null) continue;
                    List<Component> target_comp_list = new List<Component>();
                    IsTargetPrefab(go, target_comp_list);
                    if (target_comp_list.Count > 0) {
                        ItemData item = new ItemData() {
                            prefab = go,
                            prefab_path = asset_path,
                            comp_list = target_comp_list
                        };
                        m_item_data_list.Add(item);
                    }
                }
            }
            m_item_data_list.Sort((item_a, item_b) => string.Compare(item_a.prefab_path, item_b.prefab_path));
        }

        virtual protected void DoPrefabCheckStart() {

        }
        virtual protected void IsTargetPrefab(GameObject prefab_go, List<Component> comp_list) {

        }
    }
}
