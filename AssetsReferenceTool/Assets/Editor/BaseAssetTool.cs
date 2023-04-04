using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using UnityEditor;

namespace EditorAssetTools {
    public abstract class BaseAssetTool {
        protected readonly List<string> allAssetPath = new();
        protected string selectAssetPath;

        private GameObject _sceneRootGo;
        private GameObject _sceneUIRootGo;
        private readonly Dictionary<GameObject, Transform> _prefabInstanceRecordDict = new();

        private readonly string[] _defaultTargetDirectoryArray = {
            "选择预设目录",
            "Assets",
            "Assets/ResourcesAssets/Prefabs",
        };
        protected string TargetDirectory { get; private set; }

        public virtual void DoInit() {
            allAssetPath.AddRange(AssetDatabase.GetAllAssetPaths());
            TargetDirectory = "Assets";
        }

        public virtual void DoDestroy() {
            if (_sceneRootGo != null) {
                Object.DestroyImmediate(_sceneRootGo);
                _sceneRootGo = null;
            }
            if (_sceneUIRootGo != null) {
                Object.DestroyImmediate(_sceneUIRootGo);
                _sceneUIRootGo = null;
            }
            _prefabInstanceRecordDict.Clear();
            allAssetPath.Clear();
        }

        public void DoShow() {
        }

        public virtual void OnSelectChange() {
        }

        public abstract string Name { get; }
        public abstract void OnGUI();

        public void Update() {
            selectAssetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        }

        protected void DrawTargetDirectoryOnGUI(string label) {
            using (new EditorGUILayout.HorizontalScope()) {
                TargetDirectory = EditorGUILayout.TextField(label, TargetDirectory);
                var index = EditorGUILayout.Popup(0, _defaultTargetDirectoryArray, GUILayout.Width(80));
                if (index >= 1) {
                    TargetDirectory = _defaultTargetDirectoryArray[index];
                }
                if (AssetDatabase.IsValidFolder(TargetDirectory)) return;
                var lastCol = GUI.contentColor;
                GUI.contentColor = Color.red;
                EditorGUILayout.LabelField("目录无效！！！", GUILayout.Width(100));
                GUI.contentColor = lastCol;
            }
        }
        private GameObject GetSceneRootGo(bool isUI) {
            if (!isUI) {
                if (_sceneRootGo == null) {
                    _sceneRootGo = new GameObject(Name);
                }
                return _sceneRootGo;
            }
            if (_sceneUIRootGo != null) return _sceneUIRootGo;
            _sceneUIRootGo = new GameObject(Name + "_UI");
            var sceneCanvas = Object.FindObjectOfType<Canvas>();
            sceneCanvas = sceneCanvas != null ? sceneCanvas.rootCanvas : null;
            if (sceneCanvas != null) {
                GameObjectUtility.SetParentAndAlign(_sceneUIRootGo, sceneCanvas.gameObject);
                var rectComp = _sceneUIRootGo.AddComponent<RectTransform>();
                rectComp.anchorMax = Vector2.one;
                rectComp.anchorMin = Vector2.zero;
                rectComp.offsetMin = Vector2.zero;
                rectComp.offsetMax = Vector2.zero;
            } else {
                sceneCanvas = _sceneUIRootGo.AddComponent<Canvas>();
                sceneCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _sceneUIRootGo.AddComponent<CanvasScaler>();
            }
            return _sceneUIRootGo;
        }
        private Transform GetPrefabInstanceGo(string prefabPath) {
            var prefabAssetGO = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            _prefabInstanceRecordDict.TryGetValue(prefabAssetGO, out var prefabInsTrans);
            if (prefabInsTrans != null) return prefabInsTrans;
            var rectTransComp = prefabAssetGO.GetComponentInChildren<RectTransform>(true);
            var parentRootGO = GetSceneRootGo(rectTransComp != null);
            var prefabName = Path.GetFileNameWithoutExtension(prefabPath);
            var insPrefabGO = PrefabUtility.InstantiatePrefab(prefabAssetGO) as GameObject;
            insPrefabGO.name = prefabName;
            GameObjectUtility.SetParentAndAlign(insPrefabGO, parentRootGO);
            prefabInsTrans = insPrefabGO.transform;
            _prefabInstanceRecordDict[prefabAssetGO] = prefabInsTrans;
            return prefabInsTrans;
        }

        protected void TryLocationPrefabComponentByAsset(string assetPath, string prefabPath) {
            var refAssetObj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (refAssetObj == null || assetPath.EndsWith(".prefab")) {
                return;
            }
            if (!prefabPath.EndsWith(".prefab")) {
                return;
            }
            var prefabInsTrans = GetPrefabInstanceGo(prefabPath);
            var selectGOHash = new HashSet<Object>();
            foreach (var comp in prefabInsTrans.GetComponentsInChildren<Component>(true)) {
                using var serObjComp = new SerializedObject(comp);
                var serPro = serObjComp.GetIterator();
                var isTargetComp = false;
                while (serPro.NextVisible(true)) {
                    if (serPro.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (serPro.objectReferenceValue == refAssetObj) {
                        isTargetComp = true;
                    } else {
                        var objRefPath = AssetDatabase.GetAssetPath(serPro.objectReferenceValue);
                        if (!string.IsNullOrEmpty(objRefPath)) {
                            foreach (var depPath in AssetDatabase.GetDependencies(objRefPath, true)) {
                                if (depPath != assetPath) continue;
                                isTargetComp = true;
                                break;
                            }
                        }
                    }
                    if (!isTargetComp) continue;
                    selectGOHash.Add(comp.gameObject);
                    break;
                }
            }
            if (selectGOHash.Count <= 0) return;
            var goArray = new Object[selectGOHash.Count];
            selectGOHash.CopyTo(goArray);
            Selection.objects = goArray;
        }

        protected void TryLocationPrefabInstanceChildByComponent(GameObject prefabAsset, Component prefabAssetComp) {
            var childPath = GetChildPathByComponent(prefabAsset.transform, prefabAssetComp);
            var prefabInstance = GetPrefabInstanceGo(AssetDatabase.GetAssetPath(prefabAsset));
            var locationGO = prefabInstance.gameObject;
            if (!string.IsNullOrEmpty(childPath)) {
                var targetChild = prefabInstance.Find(childPath);
                locationGO = targetChild != null ? targetChild.gameObject : null;
            }
            Selection.activeGameObject = locationGO;
        }

        private static string GetChildPathByComponent(Object rootTrans, Component targetComp) {
            var parentTrans = targetComp.transform;
            var path = string.Empty;
            while (parentTrans.transform != rootTrans) {
                var curGOName = parentTrans.gameObject.name;
                if (path == string.Empty)
                    path = curGOName;
                else
                    path = curGOName + "/" + path;
                parentTrans = parentTrans.parent;
                if (parentTrans != null) continue;
                Debug.LogError("Can't Reach Root Transform-->" + rootTrans + "##" + targetComp);
                break;
            }
            return path;
        }

        protected static void GetAssetPathsFromDirectory(string directoryPath, ref List<string> retPathList) {
            if (Directory.Exists(directoryPath)) {
                foreach (var p in Directory.GetFileSystemEntries(directoryPath)) {
                    if (Directory.Exists(p)) {
                        GetAssetPathsFromDirectory(p, ref retPathList);
                    } else if (Path.GetExtension(p) != ".meta") {
                        retPathList.Add(p.Replace("\\", "/"));
                    }
                }
            } else if (File.Exists(directoryPath) && Path.GetExtension(directoryPath) != ".meta") {
                retPathList.Add(directoryPath.Replace("\\", "/"));
            }
        }
    }
}