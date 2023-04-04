using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace EditorAssetTools {
    /// <summary>
    /// 资源或文件夹批量改名
    /// </summary>
    public class ModifyNameAssetTool : BaseAssetTool {
        private class NameInfo {
            public readonly Object obj;
            public readonly string originName;
            public string newName;
            public NameInfo(Object obj, string originName) {
                this.obj = obj; this.originName = originName; newName = string.Copy(originName);
            }
        }

        private class CustomConvertInfo {
            public string convertChar = string.Empty;
            public int initValue;
            public int operationChar;
            public int rate = 1;
        }

        private const int KNameWidth = 150;
        private readonly string[] _operationStringList = { "+", "-", "*", "%" };
        private NameInfo[] _nameInfoArray;
        private string _targetName;
        private CustomConvertInfo _customConvertInfo = new();

        public override string Name => "批量命名";

        public override void DoInit() {
            base.DoInit();
            UpdateSelectObjects();
            _targetName = "Test@";
            _customConvertInfo.convertChar = "@";
            UpdateObjectsNewName();
        }

        public override void OnSelectChange() {
            base.OnSelectChange();
            UpdateSelectObjects();
            UpdateObjectsNewName();
        }
        private void UpdateSelectObjects() {
            var selectObjects = Selection.GetFiltered<Object>(SelectionMode.Unfiltered);
            if (selectObjects.Length > 0) {
                _nameInfoArray = new NameInfo[selectObjects.Length];
                var nameList = new List<NameInfo>(selectObjects.Length);
                foreach (var obj in selectObjects) {
                    nameList.Add(new NameInfo(obj, obj.name));
                }
                nameList.Sort((info_a, info_b) => {
                    var objA = info_a.obj as GameObject;
                    var objB = info_b.obj as GameObject;
                    if (objA != null && objB != null) {
                        var indexA = objA.transform.GetSiblingIndex();
                        var indexB = objB.transform.GetSiblingIndex();
                        return indexA.CompareTo(indexB);
                    }
                    var nameA = info_a.originName;
                    var nameB = info_b.originName;
                    if (!string.IsNullOrEmpty(nameA) && !string.IsNullOrEmpty(nameB)) {
                        return string.Compare(nameA, nameB, StringComparison.Ordinal);
                    }
                    return -1;
                });
                nameList.CopyTo(_nameInfoArray);
            } else _nameInfoArray = null;
        }
        public override void OnGUI() {
            if (_nameInfoArray == null || _nameInfoArray.Length == 0) {
                EditorGUILayout.HelpBox("请先选中要改名的资源，文件夹或场景里的物体", MessageType.Warning);
                return;
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("确定改名")) {
                foreach (var curNameInfo in _nameInfoArray) {
                    if (string.IsNullOrEmpty(curNameInfo.newName)) continue;
                    ObjectNames.SetNameSmart(curNameInfo.obj, curNameInfo.newName); //同步改变：对象名，meta文件名，资源文件名
                }
                AssetDatabase.SaveAssets();
                UpdateSelectObjects();
            }
            if (GUILayout.Button("重置")) {
                _targetName = null;
                _customConvertInfo = new CustomConvertInfo();
                foreach (var info in _nameInfoArray) {
                    info.newName = info.originName;
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
            EditorGUILayout.LabelField("原名字", GUILayout.Width(KNameWidth));
            EditorGUILayout.LabelField(string.Empty, GUILayout.Width(KNameWidth));
            EditorGUILayout.LabelField("改后名字", GUILayout.Width(KNameWidth));
            EditorGUILayout.EndHorizontal();
            foreach (var nameInfo in _nameInfoArray) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(nameInfo.originName, GUILayout.Width(KNameWidth));
                EditorGUILayout.LabelField("-->", GUILayout.Width(KNameWidth));
                EditorGUILayout.LabelField(nameInfo.newName, GUILayout.Width(KNameWidth));
                EditorGUILayout.EndHorizontal();
            }
        }
        private void UpdateCustomNamesOnGUI() {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("名字格式：", GUILayout.Width(70));
            _targetName = EditorGUILayout.TextField(_targetName, GUILayout.Width(150));
            EditorGUILayout.LabelField("转换字符：", GUILayout.Width(70));
            _customConvertInfo.convertChar = EditorGUILayout.TextField(_customConvertInfo.convertChar, GUILayout.Width(100));
            EditorGUILayout.LabelField("初始值：", GUILayout.Width(50));
            _customConvertInfo.initValue = EditorGUILayout.IntField(_customConvertInfo.initValue, GUILayout.Width(100));
            _customConvertInfo.operationChar = EditorGUILayout.Popup(_customConvertInfo.operationChar, _operationStringList, GUILayout.Width(50));
            EditorGUILayout.LabelField("运算值：", GUILayout.Width(50));
            _customConvertInfo.rate = EditorGUILayout.IntField(_customConvertInfo.rate, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
        }
        private void UpdateObjectsNewName() {
            if (_nameInfoArray == null || _nameInfoArray.Length == 0) {
                return;
            }
            if (string.IsNullOrEmpty(_targetName)) {
                return;
            }
            if (string.IsNullOrEmpty(_customConvertInfo.convertChar)) {
                foreach (var nameInfo in _nameInfoArray) {
                    nameInfo.newName = string.Copy(_targetName);
                }
                return;
            }
            var operationChar = _operationStringList[_customConvertInfo.operationChar];
            var value = _customConvertInfo.initValue;
            var objectNameCount = _nameInfoArray.Length;
            var numValueArray = new int[objectNameCount];
            var maxUnsignedValueLength = 1;
            for (var i = 0; i < objectNameCount; ++i) {
                switch (operationChar) {
                    case "+": {
                            value += _customConvertInfo.rate;
                            break;
                        }
                    case "-": {
                            value -= _customConvertInfo.rate;
                            break;
                        }
                    case "*": {
                            value *= _customConvertInfo.rate;
                            break;
                        }
                    case "%": {
                            value = _customConvertInfo.rate == 0 ? value : (value / _customConvertInfo.rate);
                            break;
                        }
                }
                numValueArray[i] = value;
                var unsignedValueStr = Mathf.Abs(value).ToString();
                maxUnsignedValueLength = Mathf.Max(maxUnsignedValueLength, unsignedValueStr.Length);
            }
            for (var i = 0; i < objectNameCount; ++i) {
                var nameInfo = _nameInfoArray[i];
                var numValue = numValueArray[i];
                var unsignedValueStr = Mathf.Abs(numValue).ToString();
                while (unsignedValueStr.Length < maxUnsignedValueLength) {
                    unsignedValueStr = "0" + unsignedValueStr;
                }
                if (numValue < 0) {
                    unsignedValueStr = "-" + unsignedValueStr;
                }
                nameInfo.newName = _targetName.Replace(_customConvertInfo.convertChar, unsignedValueStr);
            }
        }
    }
}
