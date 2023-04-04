using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace EditorAssetTools {
    /// <summary>
    /// 贴图检查
    /// </summary>
    public class TextureCheckTool : BaseAssetTool {
        private class SearchObjInfo {
            public string texDisplayPath;
            public string texPath;
            public Texture texAsset;
            public readonly List<string> refPathList = new();
        }

        private readonly List<SearchObjInfo> _searchObjList = new();
        private int _checkWidth = 512;
        private int _checkHeight = 512;
        private float _scrollItemIndex;

        public override string Name => "贴图尺寸检查";

        public override void DoDestroy() {
            _searchObjList.Clear();
            base.DoDestroy();
        }

        public override void OnGUI() {
            EditorGUILayout.BeginVertical();
            if (!string.IsNullOrEmpty(selectAssetPath)) {
                EditorGUILayout.LabelField("当前选择的查找路径：" + selectAssetPath);
            } else {
                EditorGUILayout.LabelField("看这儿！！！！-->请先从Project窗口右侧选择需要查找的资源或文件夹");
            }
            GUILayout.Space(10);
            _checkWidth = EditorGUILayout.IntField("最小的宽", _checkWidth);
            _checkHeight = EditorGUILayout.IntField("最小的高", _checkHeight);
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("开始查找")) {
                try {
                    DoSearch();
                } finally {
                    EditorUtility.ClearProgressBar();
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            if (_searchObjList.Count == 0) return;

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
                var uObjInfo = _searchObjList[idx];
                var guiColor = GUI.contentColor;
                GUI.contentColor = Color.green;
                if (curItemIndex >= _scrollItemIndex && curItemIndex <= _scrollItemIndex + kViewItemCount) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        EditorGUILayout.LabelField($"目标资源{idx}--> {uObjInfo.texDisplayPath}");
                        if (GUILayout.Button($"定位资源({uObjInfo.refPathList.Count}个引用)", GUILayout.Width(300))) {
                            Selection.activeObject = AssetDatabase.LoadAssetAtPath(uObjInfo.texDisplayPath, typeof(UnityEngine.Object));
                        }
                    }
                }
                GUI.contentColor = guiColor;
                ++curItemIndex;
                for (var k = 0; k < uObjInfo.refPathList.Count; ++k) {
                    if (curItemIndex >= this._scrollItemIndex && curItemIndex <= this._scrollItemIndex + kViewItemCount) {
                        EditorGUILayout.BeginHorizontal();
                        var path = uObjInfo.refPathList[k];
                        EditorGUILayout.LabelField($"\t{k}.{path}");
                        if (path.EndsWith(".prefab") && GUILayout.Button("定位组件", GUILayout.Width(100))) {
                            TryLocationPrefabComponentByAsset(uObjInfo.texPath, path);
                        }
                        if (GUILayout.Button("定位资源", GUILayout.Width(100))) {
                            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                            if (asset != null) Selection.activeObject = asset;
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

        private void DoSearch() {
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
            var targetDict = new Dictionary<Texture, SearchObjInfo>();
            for (var i = 0; i < allObjPathList.Count; ++i) {
                var assetPath = allObjPathList[i];
                EditorUtility.DisplayProgressBar("Check Asset", assetPath, (float) i / allObjPathList.Count);
                foreach (var refPath in AssetDatabase.GetDependencies(assetPath, true)) {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture>(refPath);
                    if (tex == null || (tex.width < _checkWidth && tex.height < _checkHeight)) continue;
                    if (!targetDict.TryGetValue(tex, out var uInfo)) {
                        uInfo = new SearchObjInfo();
                        var displayPath = refPath + $" {tex.width}X{tex.height}";
                        uInfo.texDisplayPath = displayPath;
                        uInfo.texPath = refPath;
                        uInfo.texAsset = tex;
                        targetDict[tex] = uInfo;
                    }
                    uInfo.refPathList.Add(assetPath);
                }
            }
            _searchObjList.AddRange(targetDict.Values);
            _searchObjList.Sort((infoA, infoB) => {
                var sizeA = infoA.texAsset.width * infoA.texAsset.height;
                var sizeB = infoB.texAsset.width * infoB.texAsset.height;
                return sizeB.CompareTo(sizeA);
            });
        }
    }
}