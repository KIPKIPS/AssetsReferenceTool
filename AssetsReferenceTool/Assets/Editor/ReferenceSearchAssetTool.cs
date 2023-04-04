using System;
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

        private enum SortType {
            Path,
            Type,
            RefCountDown,
            RefCountUp,
            TexSizeDown,
            TexSizeUp
        }

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
            if (!string.IsNullOrEmpty(selectAssetPath)) {
                EditorGUILayout.LabelField("当前选择的路径或资源：" + selectAssetPath);
            } else {
                EditorGUILayout.HelpBox("请先从Project窗口里的右侧选择需要查找的资源或文件夹", MessageType.Warning);
            }
            GUILayout.Space(10);
            DrawTargetDirectoryOnGUI("锁定搜索目录：");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("查找被哪些资源引用")) {
                try {
                    DoReferenceSearch();
                    DoSortList(SortType.Path);
                } finally {
                    EditorUtility.ClearProgressBar();
                }
            }
            if (GUILayout.Button("查找引用了哪些资源")) {
                try {
                    DoDependenciesSearch();
                    DoSortList(SortType.Path);
                } finally {
                    EditorUtility.ClearProgressBar();
                }
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
            var curEvt = Event.current;
            if (curEvt is {
                isScrollWheel: true
            }) {
                _scrollItemIndex += curEvt.delta.y;
                curEvt.Use();
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            const int kViewItemCount = 25;
            var curItemIndex = 0;
            for (var idx = 0; idx < _searchObjList.Count; ++idx) {
                var objInfo = _searchObjList[idx];
                if (curItemIndex >= _scrollItemIndex && curItemIndex <= _scrollItemIndex + kViewItemCount) {
                    using var horScope = new EditorGUILayout.HorizontalScope();
                    var guiColor = GUI.contentColor;
                    GUI.contentColor = objInfo.isSelect ? Color.blue : Color.green;
                    objInfo.isSelect = EditorGUILayout.Toggle(objInfo.isSelect, GUILayout.Width(20));
                    EditorGUILayout.LabelField($"目标资源{idx}--> {objInfo.objPath}");
                    if (GUILayout.Button($"定位资源({objInfo.refPathList.Count}个引用)", GUILayout.Width(300))) {
                        Selection.activeObject = AssetDatabase.LoadAssetAtPath(objInfo.objPath, typeof(UnityEngine.Object));
                    }
                    if (curEvt is {
                        rawType: EventType.MouseDown, button: 0
                    } && horScope.rect.Contains(curEvt.mousePosition)) {
                        objInfo.isSelect = !objInfo.isSelect;
                        if (curEvt.shift) {
                            for (var j = idx - 1; j >= 0; --j) {
                                var lastObjInfo = _searchObjList[j];
                                if (lastObjInfo.isSelect != objInfo.isSelect)
                                    lastObjInfo.isSelect = objInfo.isSelect;
                                else
                                    break;
                            }
                        }
                        curEvt.Use();
                    }
                    GUI.contentColor = guiColor;
                }
                ++curItemIndex;
                for (var k = 0; k < objInfo.refPathList.Count; ++k) {
                    if (curItemIndex >= _scrollItemIndex && curItemIndex <= _scrollItemIndex + kViewItemCount) {
                        EditorGUILayout.BeginHorizontal();
                        var refPath = objInfo.refPathList[k];
                        EditorGUILayout.LabelField($"\t{k}.{refPath}");
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

        private void TryDrawLocationComponent(string pathA, string pathB) {
            string assetPath = null;
            string prefabPath = null;
            if (pathA.EndsWith(".prefab") && GUILayout.Button("定位组件", GUILayout.Width(100))) {
                assetPath = pathB;
                prefabPath = pathA;
            } else if (pathB.EndsWith(".prefab") && GUILayout.Button("定位组件", GUILayout.Width(100))) {
                assetPath = pathA;
                prefabPath = pathB;
            }
            if (assetPath != null) {
                TryLocationPrefabComponentByAsset(assetPath, prefabPath);
            }
        }

        private void DoReferenceSearch() {
            _searchObjList.Clear();
            if (string.IsNullOrEmpty(selectAssetPath)) {
                return;
            }
            //find all file asset path
            var allObjPathList = new List<string>();
            GetAssetPathsFromDirectory(selectAssetPath, ref allObjPathList);
            if (allObjPathList.Count == 0) {
                return;
            }
            var assetSearchInfoDic = new Dictionary<string, SearchObjInfo>();
            foreach (var t in allObjPathList) {
                var info = new SearchObjInfo {
                    objPath = t
                };
                assetSearchInfoDic[info.objPath] = info;
                _searchObjList.Add(info);
            }
            float processIndex = 0;
            if (_assetsDependenciesDict == null) {
                _assetsDependenciesDict = new Dictionary<string, string[]>();
                foreach (var curAssetPath in allAssetPath) {
                    ++processIndex;
                    if (AssetDatabase.IsValidFolder(TargetDirectory) && !curAssetPath.StartsWith(TargetDirectory)) {
                        continue;
                    }
                    if (EditorUtility.DisplayCancelableProgressBar("Search Assets", curAssetPath, processIndex / allAssetPath.Count)) {
                        _assetsDependenciesDict = null;
                        return;
                    }
                    var depPathArray = AssetDatabase.GetDependencies(curAssetPath, true);
                    _assetsDependenciesDict[curAssetPath] = depPathArray;
                    foreach (var depPath in depPathArray) {
                        if (curAssetPath != depPath && assetSearchInfoDic.TryGetValue(depPath, out var objInfo)) {
                            objInfo.refPathList.Add(curAssetPath);
                        }
                    }
                }
            } else {
                var totalProcessCount = _assetsDependenciesDict.Count;
                foreach (var kv in _assetsDependenciesDict) {
                    var curAssetPath = kv.Key;
                    var assetDependenciesArray = kv.Value;
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

        private void DoDependenciesSearch() {
            _searchObjList.Clear();
            if (string.IsNullOrEmpty(selectAssetPath)) {
                return;
            }
            var allObjPathList = new List<string>();
            GetAssetPathsFromDirectory(selectAssetPath, ref allObjPathList);
            float processIndex = 0;
            foreach (var objPath in allObjPathList) {
                ++processIndex;
                EditorUtility.DisplayProgressBar("Search Dependencies", objPath, processIndex / allObjPathList.Count);
                var objInfo = new SearchObjInfo();
                _searchObjList.Add(objInfo);
                objInfo.objPath = objPath;
                foreach (var path in AssetDatabase.GetDependencies(objPath, true)) {
                    if (path != objPath) objInfo.refPathList.Add(path);
                }
            }
        }

        #region Func Btns
        private void ClickSearchDelAssetBtn() {
            var hintStr = "";
            foreach (var objInfo in _searchObjList) {
                if (objInfo.isSelect) {
                    hintStr += "\n" + objInfo.objPath;
                }
            }
            if (string.IsNullOrEmpty(hintStr)) return;
            if (!EditorUtility.DisplayDialog("删除资源", hintStr, "确定（资源删除后不能恢复）", "取消")) return;
            for (var i = _searchObjList.Count - 1; i >= 0; --i) {
                var objInfo = _searchObjList[i];
                if (!objInfo.isSelect) continue;
                AssetDatabase.DeleteAsset(objInfo.objPath);
                _searchObjList.RemoveAt(i);
            }
            _assetsDependenciesDict = null;
            AssetDatabase.RemoveUnusedAssetBundleNames();
            AssetDatabase.Refresh();
        }
        private void DoSortList(SortType sortType) {
            if (_searchObjList == null) return;
            int CompareFunc(string pathA, string pathB) {
                var compareV = 0;
                switch (sortType) {
                    case SortType.Type: {
                        var a = Path.GetExtension(pathA);
                        var b = Path.GetExtension(pathB);
                        compareV = string.Compare(a, b, StringComparison.Ordinal);
                        break;
                    }
                    case SortType.TexSizeUp:
                    case SortType.TexSizeDown: {
                        var texAssetA = AssetDatabase.LoadAssetAtPath<Texture>(pathA);
                        var texAssetB = AssetDatabase.LoadAssetAtPath<Texture>(pathB);
                        if (texAssetA != null && texAssetB != null) {
                            var sizeA = texAssetA.width * texAssetA.height;
                            var sizeB = texAssetB.width * texAssetB.height;
                            compareV = sortType == SortType.TexSizeUp ? sizeA.CompareTo(sizeB) : sizeB.CompareTo(sizeA);
                        } else if (texAssetA != null)
                            compareV = -1;
                        else if (texAssetB != null) compareV = 1;
                        break;
                    }
                    case SortType.Path:
                        break;
                    case SortType.RefCountDown:
                        break;
                    case SortType.RefCountUp:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(sortType), sortType, null);
                }
                if (compareV != 0) return compareV;
                var directoryA = Path.GetDirectoryName(pathA) + "/" + Path.GetFileNameWithoutExtension(pathA);
                var directoryB = Path.GetDirectoryName(pathB) + "/" + Path.GetFileNameWithoutExtension(pathB);
                compareV = String.Compare(directoryA, directoryB, StringComparison.Ordinal);
                return compareV;
            }
            _searchObjList.Sort((infoA, infoB) => {
                var compareV = 0;
                switch (sortType) {
                    case SortType.RefCountUp:
                        compareV = infoA.refPathList.Count.CompareTo(infoB.refPathList.Count);
                        break;
                    case SortType.RefCountDown:
                        compareV = infoB.refPathList.Count.CompareTo(infoA.refPathList.Count);
                        break;
                    case SortType.Path:
                        break;
                    case SortType.Type:
                        break;
                    case SortType.TexSizeDown:
                        break;
                    case SortType.TexSizeUp:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(sortType), sortType, null);
                }
                if (compareV != 0) {
                    return compareV;
                }
                compareV = CompareFunc(infoA.objPath, infoB.objPath);
                return compareV;
            });
            foreach (var info in _searchObjList) {
                info.refPathList.Sort(CompareFunc);
            }
        }

        private void ClickSelectNoRefBtn() {
            if (_searchObjList == null) return;
            foreach (var objInfo in _searchObjList) {
                var isNoRef = objInfo.refPathList.Count == 0;
                objInfo.isSelect = isNoRef;
            }
        }

        private void ClickSelectAllBtn(bool isSelectAll) {
            if (_searchObjList == null) return;
            foreach (var objInfo in _searchObjList) {
                objInfo.isSelect = isSelectAll;
            }
        }
        #endregion
    }
}