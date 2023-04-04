using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace EditorAssetTools {
    public abstract class BasePrefabAssetTool : BaseAssetTool {
        private float _scrollItemIndex;

        protected class ItemData {
            public GameObject prefab;
            public string prefabPath;
            public List<Component> compList;
        }

        private readonly List<ItemData> _itemDataList = new();

        protected void DrawDefaultHeader() {
            if (!string.IsNullOrEmpty(selectAssetPath)) {
                EditorGUILayout.LabelField("当前选择的查找路径：" + selectAssetPath);
            } else {
                EditorGUILayout.HelpBox("请先从Project窗口右侧选择需要查找的Prefab或文件夹", MessageType.Info);
            }
            GUILayout.Space(10);
        }
        protected void DrawSearchButton() {
            if (string.IsNullOrEmpty(selectAssetPath) || !GUILayout.Button("开始查找")) return;
            try {
                DoSearch();
            } finally {
                EditorUtility.ClearProgressBar();
            }
        }

        protected void DrawTargetPrefabAssetList(Action<Component, ItemData> drawCompAction) {
            GUILayout.Space(10);
            EditorGUILayout.LabelField($"找到目标Prefab总数--> {_itemDataList.Count}");
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
            for (var idx = 0; idx < _itemDataList.Count; ++idx) {
                var itemData = _itemDataList[idx];
                var guiColor = GUI.contentColor;
                GUI.contentColor = Color.green;
                if (curItemIndex >= _scrollItemIndex && curItemIndex <= _scrollItemIndex + kViewItemCount) {
                    using var horScope = new EditorGUILayout.HorizontalScope();
                    EditorGUILayout.LabelField($"目标Prefab#{idx}--> {itemData.prefabPath}");
                    if (GUILayout.Button("定位资源", GUILayout.Width(200))) {
                        Selection.activeObject = itemData.prefab;
                    }
                }
                GUI.contentColor = guiColor;
                ++curItemIndex;
                for (var k = 0; k < itemData.compList.Count; ++k) {
                    if (curItemIndex >= _scrollItemIndex && curItemIndex <= _scrollItemIndex + kViewItemCount) {
                        var comp = itemData.compList[k];
                        if (drawCompAction != null) {
                            drawCompAction(comp, itemData);
                        } else {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"\t{k}.{comp}");
                            if (GUILayout.Button("定位", GUILayout.Width(100))) {
                                TryLocationPrefabInstanceChildByComponent(itemData.prefab, comp);
                            }
                            EditorGUILayout.EndHorizontal();
                        }
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
            if (string.IsNullOrEmpty(selectAssetPath)) {
                return;
            }
            var allObjPathList = new List<string>();
            GetAssetPathsFromDirectory(selectAssetPath, ref allObjPathList);
            if (allObjPathList.Count == 0) {
                return;
            }
            _itemDataList.Clear();
            DoPrefabCheckStart();
            var objPathCount = allObjPathList.Count;
            for (var idx = 0; idx < objPathCount; ++idx) {
                var assetPath = allObjPathList[idx];
                EditorUtility.DisplayProgressBar("Check Asset", assetPath, (float) idx / allObjPathList.Count);
                if (!assetPath.EndsWith(".prefab")) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (go == null) continue;
                var targetCompList = new List<Component>();
                IsTargetPrefab(go, targetCompList);
                if (targetCompList.Count <= 0) continue;
                var item = new ItemData() {
                    prefab = go,
                    prefabPath = assetPath,
                    compList = targetCompList
                };
                _itemDataList.Add(item);
            }
            _itemDataList.Sort((itemA, itemB) => string.CompareOrdinal(itemA.prefabPath, itemB.prefabPath));
        }

        private static void DoPrefabCheckStart() {
        }
        protected virtual void IsTargetPrefab(GameObject prefabGo, List<Component> compList) {
        }
    }
}