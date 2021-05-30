using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace EditorAssetTools
{
    //########copyright-->2019/3/5 LuoYao

    /// <summary>
    /// 资源引用查找定位
    /// </summary>
    public class ReferenceSearchAssetTool : BaseAssetTool
    {
        class SearchObjInfo
        {
            public string obj_path;
            public bool is_select = false;
            public List<string> ref_path_list = new List<string>();
        }
        enum SortType {path, type, ref_cout_down, ref_count_up, tex_size_down, tex_size_up }

        List<SearchObjInfo> m_search_obj_list = new List<SearchObjInfo>();
        Dictionary<string, string[]> m_assets_dependencies_dict;
        float scroll_item_index = 0;

        public override string Name{
            get{ return "资源引用查找"; }
        }
        public override void DoInit()
        {
            base.DoInit();
        }
        public override void DoDestroy()
        {
            m_search_obj_list.Clear();
            m_assets_dependencies_dict = null;
            base.DoDestroy();
        }

        public override void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            if(!string.IsNullOrEmpty(select_asset_path)) {
                EditorGUILayout.LabelField("当前选择的路径或资源：" + select_asset_path);
            }
            else {
                EditorGUILayout.HelpBox("请先从Project窗口里的右侧选择需要查找的资源或文件夹", MessageType.Warning);
            }
            GUILayout.Space(10);
            
            base.DrawTargetDirectoryOnGUI("锁定搜索目录：");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("查找被哪些资源引用")){
                try { DoReferenceSearch(); DoSortList(SortType.path); }
                finally { EditorUtility.ClearProgressBar(); }
            }
            if (GUILayout.Button("查找引用了哪些资源")){
                try { DoDependenciesSearch(); DoSortList(SortType.path); }
                finally { EditorUtility.ClearProgressBar(); }
            }
            EditorGUILayout.EndHorizontal();
            if (m_assets_dependencies_dict != null && GUILayout.Button("清除缓存，重置引用信息")){
                m_assets_dependencies_dict = null;
            }
            EditorGUILayout.EndVertical();
            if (m_search_obj_list.Count == 0 ) return;

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("选中所有")) ClickSelectAllBtn(true);
            if (GUILayout.Button("取消所有选中")) ClickSelectAllBtn(false);
            if (GUILayout.Button("选中无引用的资源")) ClickSelectNoRefBtn();
            if (GUILayout.Button("删除选中资源")) ClickSearchDelAssetBtn();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("排序规则：", GUILayout.Width(60));
            if (GUILayout.Button("路径")) DoSortList(SortType.path);
            if (GUILayout.Button("类型")) DoSortList(SortType.type);
            if (GUILayout.Button("引用数量递增")) DoSortList(SortType.ref_count_up );
            if (GUILayout.Button("引用数量递减")) DoSortList(SortType.ref_cout_down);
            if (GUILayout.Button("贴图尺寸递增")) DoSortList(SortType.tex_size_up);
            if (GUILayout.Button("贴图尺寸递减")) DoSortList(SortType.tex_size_down);
            EditorGUILayout.EndHorizontal();

            //show asset list
            GUILayout.Space(10);
            EditorGUILayout.LabelField(string.Format("找到目标资源总数--> {0}", m_search_obj_list.Count));
            GUILayout.Space(10);
            Event cur_evt = Event.current;
            if (cur_evt != null && cur_evt.isScrollWheel){
                scroll_item_index += cur_evt.delta.y;
                cur_evt.Use();
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            const int k_view_item_count = 25;
            int cur_item_index = 0;
            for (int idx = 0; idx < m_search_obj_list.Count; ++idx){
                var u_obj_info = m_search_obj_list[idx];
                if (cur_item_index >= scroll_item_index && cur_item_index <= scroll_item_index + k_view_item_count){
                    using (var hor_scope = new EditorGUILayout.HorizontalScope()){
                        Color gui_color = GUI.contentColor;
                        GUI.contentColor = u_obj_info.is_select ? Color.blue : Color.green;
                        u_obj_info.is_select = EditorGUILayout.Toggle(u_obj_info.is_select, GUILayout.Width(20));
                        EditorGUILayout.LabelField(string.Format("目标资源{0}--> {1}", idx, u_obj_info.obj_path));
                        if (GUILayout.Button(string.Format("定位资源({0}个引用)", u_obj_info.ref_path_list.Count), GUILayout.Width(300))){
                            Selection.activeObject = AssetDatabase.LoadAssetAtPath(u_obj_info.obj_path, typeof(UnityEngine.Object));
                        }
                        if (cur_evt != null && cur_evt.rawType == EventType.MouseDown && cur_evt.button == 0 &&
                            hor_scope.rect.Contains(cur_evt.mousePosition))
                        {
                            u_obj_info.is_select = !u_obj_info.is_select;
                            if (cur_evt.shift){
                                for (int j = idx - 1; j >= 0; --j){
                                    var last_obj_info = m_search_obj_list[j];
                                    if (last_obj_info.is_select != u_obj_info.is_select) last_obj_info.is_select = u_obj_info.is_select;
                                    else break;
                                }
                            }
                            cur_evt.Use();
                        }
                        GUI.contentColor = gui_color;
                    }
                }
                ++cur_item_index;
                for (int k = 0; k < u_obj_info.ref_path_list.Count; ++k){
                    if (cur_item_index >= this.scroll_item_index && cur_item_index <= this.scroll_item_index + k_view_item_count){
                        EditorGUILayout.BeginHorizontal();
                        var ref_path = u_obj_info.ref_path_list[k];
                        EditorGUILayout.LabelField(string.Format("\t{0}.{1}", k, ref_path));
                        TryDrawLocationComponent(u_obj_info.obj_path, ref_path);
                        if (GUILayout.Button("定位资源", GUILayout.Width(100))){
                            var obj = AssetDatabase.LoadAssetAtPath(ref_path, typeof(UnityEngine.Object));
                            if(obj != null) Selection.activeObject = obj;
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

        void TryDrawLocationComponent(string path_a, string path_b)
        {
            string asset_path = null;
            string prefab_path = null;
            if (path_a.EndsWith(".prefab") && GUILayout.Button("定位组件", GUILayout.Width(100))) {
                asset_path = path_b;
                prefab_path = path_a;
            }
            else if (path_b.EndsWith(".prefab") && GUILayout.Button("定位组件", GUILayout.Width(100))) {
                asset_path = path_a;
                prefab_path = path_b;
            }
            if(asset_path != null && prefab_path != null) {
                TryLocationPrefabComponentByAsset(asset_path, prefab_path);
            }
        }

        void DoReferenceSearch()
        {
            m_search_obj_list.Clear();
            if(string.IsNullOrEmpty(select_asset_path)) {
                return;
            }
            //find all file asset path
            List<string> all_obj_path_list = new List<string>();
            GetAssetPathsFromDirectory(select_asset_path, ref all_obj_path_list);
            if (all_obj_path_list.Count == 0) {
                return;
            }
            Dictionary<string, SearchObjInfo> asset_search_info_dic = new Dictionary<string, SearchObjInfo>();
            for (int i = 0; i < all_obj_path_list.Count; ++i){
                var u_info = new SearchObjInfo();
                u_info.obj_path = all_obj_path_list[i];
                //string lua_obj_path = Path.GetDirectoryName(u_info.obj_path) + "/" + Path.GetFileNameWithoutExtension(u_info.obj_path);
                //lua_obj_path = lua_obj_path.Replace(kAssetForwardPath, "");
                //u_info.lua_path = lua_obj_path;
                asset_search_info_dic[u_info.obj_path] = u_info;
                m_search_obj_list.Add(u_info);
            }
            //find ref from Lua Data
            //var all_lua_data_dict = LuaScriptReader.GetAllLua();
            float process_index = 0;
            int total_process_count;// = all_lua_data_dict.Count;
            //foreach (var kv in all_lua_data_dict){
            //    string lua_data_path = kv.Key;
            //    string lua_content_str = kv.Value;
            //    ++process_index;
            //    if(EditorUtility.DisplayCancelableProgressBar("Search ExcelData", lua_data_path, process_index / total_process_count)) {
            //        return;
            //    }
            //    foreach(var obj_info in m_search_obj_list) {
            //        if(IsLuaDataContainsPath(lua_content_str, obj_info.lua_path)) {
            //            obj_info.ref_path_list.Add(lua_data_path);
            //        }
            //    }
            //}
            //find ref from unity asset
            process_index = 0;
            if(m_assets_dependencies_dict == null) {
                m_assets_dependencies_dict = new Dictionary<string, string[]>();
                foreach(var cur_asset_path in base.all_asset_path) {
                    ++process_index;
                    if (AssetDatabase.IsValidFolder(base.targetDirectory) && !cur_asset_path.StartsWith(base.targetDirectory)) {
                        continue;
                    }
                    if(EditorUtility.DisplayCancelableProgressBar("Search Assets", cur_asset_path, process_index / base.all_asset_path.Count)) {
                        m_assets_dependencies_dict = null;
                        return;
                    }
                    string[] dep_path_array = AssetDatabase.GetDependencies(cur_asset_path, true);
                    m_assets_dependencies_dict[cur_asset_path] = dep_path_array;
                    foreach (var dep_path in dep_path_array){
                        SearchObjInfo obj_info;
                        if (cur_asset_path != dep_path && asset_search_info_dic.TryGetValue(dep_path, out obj_info)){
                            obj_info.ref_path_list.Add(cur_asset_path);
                        }
                    }
                }
            }
            else {
                total_process_count = m_assets_dependencies_dict.Count;
                foreach(var kv in m_assets_dependencies_dict) {
                    string cur_asset_path = kv.Key;
                    string[] asset_dependencies_array = kv.Value;
                    ++process_index;
                    EditorUtility.DisplayProgressBar("Search Cache", cur_asset_path, process_index / total_process_count);
                    foreach (var dep_path in asset_dependencies_array){
                        SearchObjInfo obj_info;
                        if (cur_asset_path != dep_path && asset_search_info_dic.TryGetValue(dep_path, out obj_info)){
                            obj_info.ref_path_list.Add(cur_asset_path);
                        }
                    }
                }
            }
        }

        void DoDependenciesSearch()
        {
            m_search_obj_list.Clear();
            if(string.IsNullOrEmpty(select_asset_path)) {
                return;
            }
            List<string> all_obj_path_list = new List<string>();
            GetAssetPathsFromDirectory(select_asset_path, ref all_obj_path_list);
            float process_index = 0;
            foreach(var obj_path in all_obj_path_list){
                ++process_index;
                EditorUtility.DisplayProgressBar("Search Dependencies", obj_path, process_index / all_obj_path_list.Count);
                SearchObjInfo obj_info = new SearchObjInfo();
                m_search_obj_list.Add(obj_info);
                obj_info.obj_path = obj_path;
                foreach (var path in AssetDatabase.GetDependencies(obj_path, true)){
                    if (path != obj_path) obj_info.ref_path_list.Add(path);
                }
            }
        }

        #region///////////////////////Func Btns
        void ClickSearchDelAssetBtn()
        {
            string hint_str = "";
            foreach (var obj_info in m_search_obj_list)
            {
                if (obj_info.is_select) { hint_str += "\n" + obj_info.obj_path; }
            }
            if (string.IsNullOrEmpty(hint_str)) return;
            if (EditorUtility.DisplayDialog("删除资源", hint_str, "确定（资源删除后不能恢复）", "取消"))
            {
                for (int i = m_search_obj_list.Count - 1; i >= 0; --i)
                {
                    var obj_info = m_search_obj_list[i];
                    if (obj_info.is_select)
                    {
                        AssetDatabase.DeleteAsset(obj_info.obj_path);
                        m_search_obj_list.RemoveAt(i);
                    }
                }
                m_assets_dependencies_dict = null;
                AssetDatabase.RemoveUnusedAssetBundleNames();
                AssetDatabase.Refresh();
            }
        }
        void DoSortList(SortType sort_type)
        {
            if (m_search_obj_list == null) return;
            System.Comparison<string> ComparFunc = (path_a, path_b)=> {
                int compare_v = 0;
                if (sort_type == SortType.type) {
                    string ex_a = Path.GetExtension(path_a);
                    string ex_b = Path.GetExtension(path_b);
                    compare_v = ex_a.CompareTo(ex_b);
                }
                else if (sort_type == SortType.tex_size_up || sort_type == SortType.tex_size_down) {
                    Texture tex_asset_a = AssetDatabase.LoadAssetAtPath<Texture>(path_a);
                    Texture tex_asset_b = AssetDatabase.LoadAssetAtPath<Texture>(path_b);
                    if (tex_asset_a != null && tex_asset_b != null) {
                        int size_a = tex_asset_a.width * tex_asset_a.height;
                        int size_b = tex_asset_b.width * tex_asset_b.height;
                        compare_v = sort_type == SortType.tex_size_up ? size_a.CompareTo(size_b) : size_b.CompareTo(size_a);
                    }
                    else if (tex_asset_a != null) compare_v = -1;
                    else if (tex_asset_b != null) compare_v = 1;
                }
                if (compare_v == 0) {
                    string directory_a = Path.GetDirectoryName(path_a) + "/" + Path.GetFileNameWithoutExtension(path_a);
                    string directory_b = Path.GetDirectoryName(path_b) + "/" + Path.GetFileNameWithoutExtension(path_b);
                    compare_v = directory_a.CompareTo(directory_b);
                }
                return compare_v;
            };
            m_search_obj_list.Sort((info_a, info_b) =>
            {
                int compare_v = 0;
                if(sort_type == SortType.ref_count_up) {
                    compare_v = info_a.ref_path_list.Count.CompareTo(info_b.ref_path_list.Count);
                }
                else if(sort_type == SortType.ref_cout_down) {
                    compare_v = info_b.ref_path_list.Count.CompareTo(info_a.ref_path_list.Count);
                }
                if(compare_v != 0) {
                    return compare_v;
                }
                compare_v = ComparFunc(info_a.obj_path, info_b.obj_path);
                return compare_v;
            });
            foreach(var info in m_search_obj_list) {
                info.ref_path_list.Sort(ComparFunc);
            }
        }

        void ClickSelectNoRefBtn() {
            if (m_search_obj_list == null) return;
            foreach(var obj_info in m_search_obj_list) {
                bool is_no_ref = obj_info.ref_path_list.Count == 0;
                obj_info.is_select = is_no_ref == true;
            }
        }

        void ClickSelectAllBtn(bool is_select_all) {
            if (m_search_obj_list == null) return;
            foreach(var obj_info in m_search_obj_list) {
                obj_info.is_select = is_select_all == true;
            }
        }
        #endregion
    }
}
