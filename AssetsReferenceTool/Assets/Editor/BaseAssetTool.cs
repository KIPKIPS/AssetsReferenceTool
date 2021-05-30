using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using UnityEditor;

namespace EditorAssetTools {
    public abstract class BaseAssetTool {
        EditorAssetToolsWindow tools_window;

        protected List<string> all_asset_path = new List<string>();
        protected string select_asset_path = null;

        GameObject m_SceneRootGo;
        GameObject m_SceneUIRootGo;
        Dictionary<GameObject, Transform> m_prefab_instance_record_dict = new Dictionary<GameObject, Transform>();

        readonly string[] kDefaultTargetDirectoryArray = new string[] {
            "选择预设目录",
            "Assets",
            "Assets/Things/Prefabs/UI",
            "Assets/Things/Effect/Prefab",
            "Assets/Things/Unit"
        };
        protected string targetDirectory { get; private set; }

        public EditorAssetToolsWindow toolsWindow {
            get { return tools_window; }
            set { tools_window = value; }
        }

        public virtual void DoInit() {
            all_asset_path.AddRange(AssetDatabase.GetAllAssetPaths());
            targetDirectory = "Assets";
        }

        public virtual void DoDestroy() {
            if (m_SceneRootGo != null) {
                GameObject.DestroyImmediate(m_SceneRootGo);
                m_SceneRootGo = null;
            }
            if (m_SceneUIRootGo != null) {
                GameObject.DestroyImmediate(m_SceneUIRootGo);
                m_SceneUIRootGo = null;
            }
            m_prefab_instance_record_dict.Clear();
            all_asset_path.Clear();
        }

        public virtual void DoShow() { }

        public virtual void OnSelectChange() { }

        public abstract string Name { get; }
        public abstract void OnGUI();

        public virtual void Update() {
            select_asset_path = AssetDatabase.GetAssetPath(Selection.activeObject);
        }

        protected void DrawTargetDirectoryOnGUI(string label) {
            using (new EditorGUILayout.HorizontalScope()) {
                targetDirectory = EditorGUILayout.TextField(label, targetDirectory);
                int index = EditorGUILayout.Popup(0, kDefaultTargetDirectoryArray, GUILayout.Width(80));
                if (index >= 1) {
                    targetDirectory = kDefaultTargetDirectoryArray[index];
                }
                if (!AssetDatabase.IsValidFolder(targetDirectory)) {
                    Color last_col = GUI.contentColor;
                    GUI.contentColor = Color.red;
                    EditorGUILayout.LabelField("目录无效！！！", GUILayout.Width(100));
                    GUI.contentColor = last_col;
                }
            }
        }
        protected GameObject GetSceneRootGo(bool is_ui) {
            if (!is_ui) {
                if (m_SceneRootGo == null) {
                    m_SceneRootGo = new GameObject(this.Name);
                }
                return m_SceneRootGo;
            }
            if (m_SceneUIRootGo == null) {
                m_SceneUIRootGo = new GameObject(this.Name + "_UI");
                Canvas scene_canvas = GameObject.FindObjectOfType<Canvas>();
                scene_canvas = scene_canvas != null ? scene_canvas.rootCanvas : null;
                if (scene_canvas != null) {
                    GameObjectUtility.SetParentAndAlign(m_SceneUIRootGo, scene_canvas.gameObject);
                    RectTransform rect_comp = m_SceneUIRootGo.AddComponent<RectTransform>();
                    rect_comp.anchorMax = Vector2.one;
                    rect_comp.anchorMin = Vector2.zero;
                    rect_comp.offsetMin = Vector2.zero;
                    rect_comp.offsetMax = Vector2.zero;
                } else {
                    scene_canvas = m_SceneUIRootGo.AddComponent<Canvas>();
                    scene_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    m_SceneUIRootGo.AddComponent<CanvasScaler>();
                }
            }
            return m_SceneUIRootGo;
        }
        protected Transform GetPrefabInstanceGo(string prefab_path) {
            GameObject prefab_asset_go = AssetDatabase.LoadAssetAtPath<GameObject>(prefab_path);
            Transform prefab_ins_trans = null;
            m_prefab_instance_record_dict.TryGetValue(prefab_asset_go, out prefab_ins_trans);
            if (prefab_ins_trans == null) {
                RectTransform rect_trans_comp = prefab_asset_go.GetComponentInChildren<RectTransform>(true);
                GameObject parent_root_go = GetSceneRootGo(rect_trans_comp != null);
                string prefab_name = Path.GetFileNameWithoutExtension(prefab_path);
                GameObject ins_prefab_go = PrefabUtility.InstantiatePrefab(prefab_asset_go) as GameObject;
                ins_prefab_go.name = prefab_name;
                GameObjectUtility.SetParentAndAlign(ins_prefab_go, parent_root_go);
                prefab_ins_trans = ins_prefab_go.transform;
                m_prefab_instance_record_dict[prefab_asset_go] = prefab_ins_trans;
            }
            return prefab_ins_trans;
        }

        protected GameObject TryLocationPrefabComponentByAsset(string asset_path, string prefab_path) {
            UnityEngine.Object ref_asset_obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(asset_path);
            if (ref_asset_obj == null || asset_path.EndsWith(".prefab")) {
                return null;
            }
            if (!prefab_path.EndsWith(".prefab")) {
                return null;
            }
            Transform prefab_ins_trans = GetPrefabInstanceGo(prefab_path);
            HashSet<GameObject> select_go_hash = new HashSet<GameObject>();
            foreach (var comp in prefab_ins_trans.GetComponentsInChildren<Component>(true)) {
                using (SerializedObject ser_obj_comp = new SerializedObject(comp)) {
                    SerializedProperty ser_pro = ser_obj_comp.GetIterator();
                    bool is_target_comp = false;
                    while (ser_pro.NextVisible(true)) {
                        if (ser_pro.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (ser_pro.objectReferenceValue == ref_asset_obj) {
                            is_target_comp = true;
                        } else {
                            string obj_ref_path = AssetDatabase.GetAssetPath(ser_pro.objectReferenceValue);
                            if (!string.IsNullOrEmpty(obj_ref_path)) {
                                foreach (string dep_path in AssetDatabase.GetDependencies(obj_ref_path, true)) {
                                    if (dep_path == asset_path) {
                                        is_target_comp = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (is_target_comp) {
                            select_go_hash.Add(comp.gameObject);
                            break;
                        }
                    }
                }
            }
            if (select_go_hash.Count > 0) {
                GameObject[] go_array = new GameObject[select_go_hash.Count];
                select_go_hash.CopyTo(go_array);
                Selection.objects = go_array;
            }
            return prefab_ins_trans.gameObject;
        }

        protected GameObject TryLocationPrefabInstanceChildByComponent(GameObject prefab_asset, Component prefab_asset_comp) {
            string child_path = GetChildPathByComponent(prefab_asset.transform, prefab_asset_comp);
            Transform prefab_instance = GetPrefabInstanceGo(AssetDatabase.GetAssetPath(prefab_asset));
            GameObject location_go = prefab_instance.gameObject;
            if (!string.IsNullOrEmpty(child_path)) {
                Transform target_child = prefab_instance.Find(child_path);
                location_go = target_child != null ? target_child.gameObject : null;
            }
            Selection.activeGameObject = location_go;
            return prefab_instance.gameObject;
        }

        protected string GetChildPathByComponent(Transform root_trans, Component target_comp) {
            Transform parent_trans = target_comp.transform;
            string path = string.Empty;
            while (parent_trans.transform != root_trans) {
                string cur_go_name = parent_trans.gameObject.name;
                if (path == string.Empty) path = cur_go_name;
                else path = cur_go_name + "/" + path;
                parent_trans = parent_trans.parent;
                if (parent_trans == null) {
                    Debug.LogError("Can't Reach Root Transform-->" + root_trans + "##" + target_comp);
                    break;
                }
            }
            return path;
        }

        public static void GetAssetPathsFromDirectory(string directory_path, ref List<string> ret_path_list) {
            if (Directory.Exists(directory_path)) {
                foreach (var p in Directory.GetFileSystemEntries(directory_path)) {
                    if (Directory.Exists(p)) {
                        GetAssetPathsFromDirectory(p, ref ret_path_list);
                    } else if (Path.GetExtension(p) != ".meta") {
                        ret_path_list.Add(p.Replace("\\", "/"));
                    }
                }
            } else if (File.Exists(directory_path) && Path.GetExtension(directory_path) != ".meta") {
                ret_path_list.Add(directory_path.Replace("\\", "/"));
            }
        }
    }
}