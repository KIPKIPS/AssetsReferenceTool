using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace EditorAssetTools
{
    //########copyright-->2019/3/5 LuoYao

    /// <summary>
    /// 资源或文件夹批量改名
    /// </summary>
    public class ModifyNameAssetTool : BaseAssetTool
    {
        class NameInfo
        {
            public UnityEngine.Object obj;
            public string origin_name;
            public string new_name;
            public NameInfo(UnityEngine.Object obj, string origin_name){
                this.obj = obj; this.origin_name = origin_name; new_name = string.Copy(origin_name);
            }
        }
        class CustomConvertInfo
        {
            public string convertChar = string.Empty;
            public int initValue = 0;
            public int operationChar = 0;
            public int rate = 1;
        }

        const int kNameWidth = 150;
        readonly string[] kOperationStringList = new string[4] { "+", "-", "*", "%" };
        NameInfo[] m_NameInfoArray = null;
        string m_TargetName = null;
        CustomConvertInfo m_CustomConvertInfo = new CustomConvertInfo();

        public override string Name{
            get{ return "批量命名"; }
        }

        public override void DoInit()
        {
            base.DoInit();
            UpdateSelectObjects();
            m_TargetName = "Test@";
            m_CustomConvertInfo.convertChar = "@";
            UpdateObjectsNewName();
        }
        public override void DoDestroy()
        {
            base.DoDestroy();
        }
        public override void OnSelectChange()
        {
            base.OnSelectChange();
            UpdateSelectObjects();
            UpdateObjectsNewName();
        }
        void UpdateSelectObjects()
        {
            UnityEngine.Object[] select_objects = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Unfiltered);
            if (select_objects.Length > 0){
                m_NameInfoArray = new NameInfo[select_objects.Length];
                List<NameInfo> name_list = new List<NameInfo>(select_objects.Length);
                for (int i = 0; i < select_objects.Length; ++i){
                    UnityEngine.Object obj = select_objects[i];
                    name_list.Add(new NameInfo(obj, obj.name));
                }
                name_list.Sort((info_a, info_b) =>
                {
                    GameObject obj_a = info_a.obj as GameObject;
                    GameObject obj_b = info_b.obj as GameObject;
                    if (obj_a != null && obj_b != null){
                        int a_index = obj_a.transform.GetSiblingIndex();
                        int b_index = obj_b.transform.GetSiblingIndex();
                        return a_index.CompareTo(b_index);
                    }
                    string a_name = info_a.origin_name;
                    string b_name = info_b.origin_name;
                    if (!string.IsNullOrEmpty(a_name) && !string.IsNullOrEmpty(b_name)){
                        return a_name.CompareTo(b_name);
                    }
                    return -1;
                });
                name_list.CopyTo(m_NameInfoArray);
            }
            else m_NameInfoArray = null;
        }
        public override void OnGUI()
        {
            if (m_NameInfoArray == null || m_NameInfoArray.Length == 0){
                EditorGUILayout.HelpBox("请先选中要改名的资源，文件夹或场景里的物体", MessageType.Warning);
                return;
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("确定改名")) {
                for (int k = 0; k < m_NameInfoArray.Length; ++k) {
                    NameInfo cur_name_info = m_NameInfoArray[k];
                    if (string.IsNullOrEmpty(cur_name_info.new_name)) continue;
                    ObjectNames.SetNameSmart(cur_name_info.obj, cur_name_info.new_name); //同步改变：对象名，meta文件名，资源文件名
                }
                AssetDatabase.SaveAssets();
                UpdateSelectObjects();
            }
            if (GUILayout.Button("重置")){
                m_TargetName = null;
                m_CustomConvertInfo = new CustomConvertInfo();
                for (int k = 0; k < m_NameInfoArray.Length; ++k){
                    NameInfo info = m_NameInfoArray[k];
                    info.new_name = info.origin_name;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            UpdateCustomNamesOnGUI();
            if (EditorGUI.EndChangeCheck()) {
                UpdateObjectsNewName();
            }

            EditorGUILayout.LabelField("----------------------------结果预览----------------------------");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("原名字", GUILayout.Width(kNameWidth));
            EditorGUILayout.LabelField(string.Empty, GUILayout.Width(kNameWidth));
            EditorGUILayout.LabelField("改后名字", GUILayout.Width(kNameWidth));
            EditorGUILayout.EndHorizontal();
            for (int i = 0; i < m_NameInfoArray.Length; ++i){
                NameInfo name_info = m_NameInfoArray[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(name_info.origin_name, GUILayout.Width(kNameWidth));
                EditorGUILayout.LabelField("-->", GUILayout.Width(kNameWidth));
                EditorGUILayout.LabelField(name_info.new_name, GUILayout.Width(kNameWidth));
                EditorGUILayout.EndHorizontal();
            }
        }
        void UpdateCustomNamesOnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("名字格式：", GUILayout.Width(70));
            m_TargetName = EditorGUILayout.TextField(m_TargetName, GUILayout.Width(150));
            EditorGUILayout.LabelField("转换字符：", GUILayout.Width(70));
            m_CustomConvertInfo.convertChar = EditorGUILayout.TextField(m_CustomConvertInfo.convertChar, GUILayout.Width(100));
            EditorGUILayout.LabelField("初始值：", GUILayout.Width(50));
            m_CustomConvertInfo.initValue = EditorGUILayout.IntField(m_CustomConvertInfo.initValue, GUILayout.Width(100));
            m_CustomConvertInfo.operationChar = EditorGUILayout.Popup(m_CustomConvertInfo.operationChar, kOperationStringList, GUILayout.Width(50));
            EditorGUILayout.LabelField("运算值：", GUILayout.Width(50));
            m_CustomConvertInfo.rate = EditorGUILayout.IntField(m_CustomConvertInfo.rate, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
        }
        void UpdateObjectsNewName()
        {
            if(m_NameInfoArray == null || m_NameInfoArray.Length == 0) {
                return;
            }
            if (string.IsNullOrEmpty(m_TargetName)) {
                return;
            }
            if (string.IsNullOrEmpty(m_CustomConvertInfo.convertChar)){
                foreach(var nameInfo in m_NameInfoArray) {
                    nameInfo.new_name = string.Copy(m_TargetName);
                }
                return;
            }
            string operationChar = kOperationStringList[m_CustomConvertInfo.operationChar];
            int value = m_CustomConvertInfo.initValue;
            int objectNameCount = m_NameInfoArray.Length;
            int[] numValueArray = new int[objectNameCount];
            int maxUnsignedValueLength = 1;
            for (int i = 0; i < objectNameCount; ++i) {
                switch (operationChar) {
                    case "+":
                        {
                            value += m_CustomConvertInfo.rate;
                            break;
                        }
                    case "-":
                        {
                            value -= m_CustomConvertInfo.rate;
                            break;
                        }
                    case "*":
                        {
                            value *= m_CustomConvertInfo.rate;
                            break;
                        }
                    case "%":
                        {
                            value = m_CustomConvertInfo.rate == 0 ? value : (value / m_CustomConvertInfo.rate);
                            break;
                        }
                }
                numValueArray[i] = value;
                string unsignedValueStr = Mathf.Abs(value).ToString();
                maxUnsignedValueLength = Mathf.Max(maxUnsignedValueLength, unsignedValueStr.Length);
            }
            for(int i = 0; i < objectNameCount; ++i) {
                NameInfo nameInfo = m_NameInfoArray[i];
                int numValue = numValueArray[i];
                string unsignedValueStr = Mathf.Abs(numValue).ToString();
                while (unsignedValueStr.Length < maxUnsignedValueLength) {
                    unsignedValueStr = "0" + unsignedValueStr;
                }
                if (numValue < 0) {
                    unsignedValueStr = "-" + unsignedValueStr;
                }
                nameInfo.new_name = m_TargetName.Replace(m_CustomConvertInfo.convertChar, unsignedValueStr);
            }
        }
    }
}
