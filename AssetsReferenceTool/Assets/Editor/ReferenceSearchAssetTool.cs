using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace EditorAssetTools {
    /// <summary>
    /// 资源引用查找定位
    /// </summary>
    public class ReferenceSearchAssetTool : BaseAssetTool {
        private class SearchObjInfo {
            public string objPath;
            public bool isSelect;
            public readonly List<string> refPathList = new();
        }

        private enum SortType { Path, Type, RefCountDown, RefCountUp, TexSizeDown, TexSizeUp }

        private readonly List<SearchObjInfo> _searchObjList = new();
        private Dictionary<string, string[]> _assetsDependenciesDict;
        private float _scrollItemIndex;

        public override string Name => "资源引用查找";
        public override void DoDestroy() {
            _searchObjList.Clear();
            _assetsDependenciesDict = null;
            base.DoDestroy();
        }

        public override void OnGUI() {
            EditorGUILayout.BeginVertical();
            if (!string.IsNullOrEmpty(select_asset_path)) {
                EditorGUILayout.LabelField("当前选择的路径或资源：" + select_asset_path);
            } else {
                EditorGUILayout.HelpBox("请先从Project窗口里的右侧选择需要查找的资源或文件夹", MessageType.Warning);
            }
            GUILayout.Space(10);

            DrawTargetDirectoryOnGUI("锁定搜索目录：");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("查找被哪些资源引用")) {
                try { DoReferenceSearch(); DoSortList(SortType.Path); } finally { EditorUtility.ClearProgressBar(); }
            }
            if (GUILayout.Button("查找引用了哪些资源")) {
                try { DoDependenciesSearch(); DoSortList(SortType.Path); } finally { EditorUtility.ClearProgressBar(); }
            }
            EditorGUILayout.EndHorizontal();
            if (_assetsDependenciesDict != null && GUILayout.Button("清除缓存，重置引用信息")) {
                _assetsDependenciesDict = null;
            }
            EditorGUILayout.EndVertical();
            if (_searchObjList.Count == 0) return;

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("选中所有")) ClickSelectAllBtn(true);
            if (GUILayout.Button("取消所有选中")) ClickSelectAllBtn(false);
            if (GUILayout.Button("选中无引用的资源")) ClickSelectNoRefBtn();
            if (GUILayout.Button("删除选中资源")) ClickSearchDelAssetBtn();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("排序规则：", GUILayout.Width(60));
            if (GUILayout.Button("路径")) DoSortList(SortType.Path);
            if (GUILayout.Button("类型")) DoSortList(SortType.Type);
            if (GUILayout.Button("引用数量递增")) DoSortList(SortType.RefCountUp);
            if (GUILayout.Button("引用数量递减")) DoSortList(SortType.RefCountDown);
            if (GUILayout.Button("贴图尺寸递增")) DoSortList(SortType.TexSizeUp);
            if (GUILayout.Button("贴图尺寸递减")) DoSortList(SortType.TexSizeDown);
            EditorGUILayout.EndHorizontal();

            //show asset list
            GUILayout.Space(10);
            EditorGUILayout.LabelField($"找到目标资源总数--> {_searchObjList.Count}");
            GUILayout.Space(10);
            Event curEvt = Event.current;
            if (curEvt != null && curEvt.isScrollWheel) {
                _scrollItemIndex += curEvt.delta.y;
                curEvt.Use();
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            const int kViewItemCount = 25;
            int curItemIndex = 0;
            for (int idx = 0; idx < _searchObjList.Count; ++idx) {
                var objInfo = _searchObjList[idx];
                if (curItemIndex >= _scrollItemIndex && curItemIndex <= _scrollItemIndex + kViewItemCount) {
                    using (var horScope = new EditorGUILayout.HorizontalScope()) {
                        Color guiColor = GUI.contentColor;
                        GUI.contentColor = objInfo.isSelect ? Color.blue : Color.green;
                        objInfo.isSelect = EditorGUILayout.Toggle(objInfo.isSelect, GUILayout.Width(20));
                        EditorGUILayout.LabelField($"目标资源{idx}--> {objInfo.objPath}");
                        if (GUILayout.Button($"定位资源({objInfo.refPathList.Count}个引用)", GUILayout.Width(300))) {
                            Selection.activeObject = AssetDatabase.LoadAssetAtPath(objInfo.objPath, typeof(UnityEngine.Object));
                        }
                        if (curEvt != null && curEvt.rawType == EventType.MouseDown && curEvt.button == 0 &&
                            horScope.rect.Contains(curEvt.mousePosition)) {
                            objInfo.isSelect = !objInfo.isSelect;
                            if (curEvt.shift) {
                                for (int j = idx - 1; j >= 0; --j) {
                                    var lastObjInfo = _searchObjList[j];
                                    if (lastObjInfo.isSelect != objInfo.isSelect) lastObjInfo.isSelect = objInfo.isSelect;
                                    else break;
                                }
                            }
                            curEvt.Use();
                        }
                        GUI.contentColor = guiColor;
                    }
                }
                ++curItemIndex;
                for (int k = 0; k < objInfo.refPathList.Count; ++k) {
                    if (curItemIndex >= _scrollItemIndex && curItemIndex <= _scrollItemIndex + kViewItemCount) {
                        EditorGUILayout.BeginHorizontal();
                        var refPath = objInfo.refPathList[k];
                        EditorGUILayout.LabelField(string.Format("\t{0}.{1}", k, refPath));
                        TryDrawLocationComponent(objInfo.objPath, refPath);
                        if (GUILayout.Button("定位资源", GUILayout.Width(100))) {
                            var obj = AssetDatabase.LoadAssetAtPath(refPath, typeof(UnityEngine.Object));
                            if (obj != null) Selection.activeObject = obj;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    ++curItemIndex;
                }
            }
            EditorGUILayout.EndVertical();
            float scrollMAXValue = curItemIndex - kViewItemCount;
            _scrollItemIndex = GUILayout.VerticalScrollbar(_scrollItemIndex, 0, 0f, scrollMAXValue + 1, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndHorizontal();
        }

        void TryDrawLocationComponent(string path_a, string path_b) {
            string assetPath = null;
            string prefabPath = null;
            if (path_a.EndsWith(".prefab") && GUILayout.Button("定位组件", GUILayout.Width(100))) {
                assetPath = path_b;
                prefabPath = path_a;
            } else if (path_b.EndsWith(".prefab") && GUILayout.Button("定位组件", GUILayout.Width(100))) {
                assetPath = path_a;
                prefabPath = path_b;
            }
            if (assetPath != null) {
                TryLocationPrefabComponentByAsset(assetPath, prefabPath);
            }
        }

        void DoReferenceSearch() {
            _searchObjList.Clear();
            if (string.IsNullOrEmpty(select_asset_path)) {
                return;
            }
            //find all file asset path
            List<string> allObjPathList = new List<string>();
            GetAssetPathsFromDirectory(select_asset_path, ref allObjPathList);
            if (allObjPathList.Count == 0) {
                return;
            }
            Dictionary<string, SearchObjInfo> assetSearchInfoDic = new Dictionary<string, SearchObjInfo>();
            foreach (var t in allObjPathList) {
                var info = new SearchObjInfo();
                info.objPath = t;
                //string lua_obj_path = Path.GetDirectoryName(u_info.objPath) + "/" + Path.GetFileNameWithoutExtension(u_info.objPath);
                //lua_obj_path = lua_obj_path.Replace(kAssetForwardPath, "");
                //u_info.lua_path = lua_obj_path;
                assetSearchInfoDic[info.objPath] = info;
                _searchObjList.Add(info);
            }
            //find ref from Lua Data
            //var all_lua_data_dict = LuaScriptReader.GetAllLua();
            //foreach (var kv in all_lua_data_dict){
            //    string lua_data_path = kv.Key;
            //    string lua_content_str = kv.Value;
            //    ++process_index;
            //    if(EditorUtility.DisplayCancelableProgressBar("Search ExcelData", lua_data_path, process_index / total_process_count)) {
            //        return;
            //    }
            //    foreach(var obj_info in m_search_obj_list) {
            //        if(IsLuaDataContainsPath(lua_content_str, obj_info.lua_path)) {
            //            obj_info.refPathList.Add(lua_data_path);
            //        }
            //    }
            //}
            //find ref from unity asset
            float processIndex = 0;
            if (_assetsDependenciesDict == null) {
                _assetsDependenciesDict = new Dictionary<string, string[]>();
                foreach (var curAssetPath in base.all_asset_path) {
                    ++processIndex;
                    if (AssetDatabase.IsValidFolder(base.targetDirectory) && !curAssetPath.StartsWith(base.targetDirectory)) {
                        continue;
                    }
                    if (EditorUtility.DisplayCancelableProgressBar("Search Assets", curAssetPath, processIndex / base.all_asset_path.Count)) {
                        _assetsDependenciesDict = null;
                        return;
                    }
                    string[] depPathArray = AssetDatabase.GetDependencies(curAssetPath, true);
                    _assetsDependenciesDict[curAssetPath] = depPathArray;
                    foreach (var depPath in depPathArray) {
                        if (curAssetPath != depPath && assetSearchInfoDic.TryGetValue(depPath, out var objInfo)) {
                            objInfo.refPathList.Add(curAssetPath);
                        }
                    }
                }
            } else {
                var totalProcessCount = _assetsDependenciesDict.Count;// = all_lua_data_dict.Count;
                foreach (var kv in _assetsDependenciesDict) {
                    string curAssetPath = kv.Key;
                    string[] assetDependenciesArray = kv.Value;
                    ++processIndex;
                    EditorUtility.DisplayProgressBar("Search Cache", curAssetPath, processIndex / totalProcessCount);
                    foreach (var depPath in assetDependenciesArray) {
                        if (curAssetPath != depPath && assetSearchInfoDic.TryGetValue(depPath, out var objInfo)) {
                            objInfo.refPathList.Add(curAssetPath);
                        }
                    }
                }
            }
        }

        void DoDependenciesSearch() {
            _searchObjList.Clear();
            if (string.IsNullOrEmpty(select_asset_path)) {
                return;
            }
            List<string> allObjPathList = new List<string>();
            GetAssetPathsFromDirectory(select_asset_path, ref allObjPathList);
            float processIndex = 0;
            foreach (var objPath in allObjPathList) {
                ++processIndex;
                EditorUtility.DisplayProgressBar("Search Dependencies", objPath, processIndex / allObjPathList.Count);
                SearchObjInfo objInfo = new SearchObjInfo();
                _searchObjList.Add(objInfo);
                objInfo.objPath = objPath;
                foreach (var path in AssetDatabase.GetDependencies(objPath, true)) {
                    if (path != objPath) objInfo.refPathList.Add(path);
                }
            }
        }

        #region///////////////////////Func Btns
        void ClickSearchDelAssetBtn() {
            string hintStr = "";
            foreach (var objInfo in _searchObjList) {
                if (objInfo.isSelect) { hintStr += "\n" + objInfo.objPath; }
            }
            if (string.IsNullOrEmpty(hintStr)) return;
            if (EditorUtility.DisplayDialog("删除资源", hintStr, "确定（资源删除后不能恢复）", "取消")) {
                for (int i = _searchObjList.Count - 1; i >= 0; --i) {
                    var objInfo = _searchObjList[i];
                    if (objInfo.isSelect) {
                        AssetDatabase.DeleteAsset(objInfo.objPath);
                        _searchObjList.RemoveAt(i);
                    }
                }
                _assetsDependenciesDict = null;
                AssetDatabase.RemoveUnusedAssetBundleNames();
                AssetDatabase.Refresh();
            }
        }
        void DoSortList(SortType sort_type) {
            if (_searchObjList == null) return;
            System.Comparison<string> compareFunc = (path_a, path_b) => {
                int compareV = 0;
                if (sort_type == SortType.Type) {
                    string a = Path.GetExtension(path_a);
                    string b = Path.GetExtension(path_b);
                    compareV = a.CompareTo(b);
                } else if (sort_type == SortType.TexSizeUp || sort_type == SortType.TexSizeDown) {
                    Texture texAssetA = AssetDatabase.LoadAssetAtPath<Texture>(path_a);
                    Texture texAssetB = AssetDatabase.LoadAssetAtPath<Texture>(path_b);
                    if (texAssetA != null && texAssetB != null) {
                        int sizeA = texAssetA.width * texAssetA.height;
                        int sizeB = texAssetB.width * texAssetB.height;
                        compareV = sort_type == SortType.TexSizeUp ? sizeA.CompareTo(sizeB) : sizeB.CompareTo(sizeA);
                    } else if (texAssetA != null) compareV = -1;
                    else if (texAssetB != null) compareV = 1;
                }
                if (compareV == 0) {
                    string directoryA = Path.GetDirectoryName(path_a) + "/" + Path.GetFileNameWithoutExtension(path_a);
                    string directoryB = Path.GetDirectoryName(path_b) + "/" + Path.GetFileNameWithoutExtension(path_b);
                    compareV = directoryA.CompareTo(directoryB);
                }
                return compareV;
            };
            _searchObjList.Sort((info_a, info_b) => {
                int compareV = 0;
                if (sort_type == SortType.RefCountUp) {
                    compareV = info_a.refPathList.Count.CompareTo(info_b.refPathList.Count);
                } else if (sort_type == SortType.RefCountDown) {
                    compareV = info_b.refPathList.Count.CompareTo(info_a.refPathList.Count);
                }
                if (compareV != 0) {
                    return compareV;
                }
                compareV = compareFunc(info_a.objPath, info_b.objPath);
                return compareV;
            });
            foreach (var info in _searchObjList) {
                info.refPathList.Sort(compareFunc);
            }
        }

        void ClickSelectNoRefBtn() {
            if (_searchObjList == null) return;
            foreach (var objInfo in _searchObjList) {
                bool isNoRef = objInfo.refPathList.Count == 0;
                objInfo.isSelect = isNoRef == true;
            }
        }

        void ClickSelectAllBtn(bool is_select_all) {
            if (_searchObjList == null) return;
            foreach (var objInfo in _searchObjList) {
                objInfo.isSelect = is_select_all == true;
            }
        }
        #endregion
    }
}
