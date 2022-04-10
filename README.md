# AssetsReferenceTool
资源引用工具
### 一 功能简述
包含两个主要功能
* 查找某预制体资源引用了那些贴图资源
* 查找某贴图资源被那些预制体引用,并定位预制体到检视面板

### 二 设计思路
* 集成工具面板,包含了多个工具,所以需要一个基础的工具面板类BaseAssetTool,集成工具面板包含一个基础工具类列表,定义集成面板包含的工具列表BaseAssetTool[] toolArray,各个工具类按照需求继承基础工具类,再分别扩展自己需要的功能
* 资源工具面板
* 资源工具基类,实现基础方法,需要子类重写的虚方法等

### 三 实现代码
AssetToolsWindow 资源工具面板
```csharp
using UnityEngine;
using UnityEditor;
namespace EditorAssetTools {
    /// <summary>
    /// 编辑器多工具集成窗口
    /// </summary>
    public class EditorAssetToolsWindow : EditorWindow {
        [MenuItem("Tools/编辑器资源工具")]
        static void OpenInProjectView() {
            EditorAssetToolsWindow.OpenToolsWindow(); //编辑器资源工具入口
        }
        int selectToolIndex = -1; //选中的工具索引
        string[] toolDisplayArray; //工具的显示名称
        BaseAssetTool[] toolArray = new BaseAssetTool[] { //工具列表,这里只分析引用查找工具
            //new ModifyNameAssetTool(),
            new ReferenceSearchAssetTool(),
            //new TextureCheckTool(),
            //new TransparentGraphicPrefabCheckTool(),
        };
        static void OpenToolsWindow() { //打开集成工具窗口,包含工具列表的工具
            if(Application.isPlaying) {
                Debug.LogError("不允许在运行状态下打开资源工具窗口");
                return;
            }
            //打开编辑器窗口,1false代表非浮动窗口,2名称,3false代表聚焦,第一次打开默认聚焦
            var window = EditorWindow.GetWindow<EditorAssetToolsWindow>(false, "资源工具", false);
            EditorWindow.FocusWindowIfItsOpen<EditorAssetToolsWindow>();//第一次打开聚焦
            window.Show();//显示面板
        }
        //OnEnable时调用
        void OnEnable() {
            toolDisplayArray = new string[toolArray.Length];//创建工具名称数组
            for(int idx = 0; idx < toolArray.Length; ++idx) { //遍历工具列表
                BaseAssetTool tool = toolArray[idx];//工具
                tool.toolsWindow = this;
                tool.DoInit(); //初始化工具,主要是初始化资源路径列表
                toolDisplayArray[idx] = tool.Name;//保存工具名称
            }
            if(selectToolIndex < 0) SelectTool(0);//默认选第一个工具
            Selection.selectionChanged += OnSelectChange;//当前所选项发生更改时触发的委托回调 OnSelectChange
        }
        void OnDisable() {
            foreach (var tool in toolArray) tool.DoDestroy(); //销毁各个工具实例
            Selection.selectionChanged -= OnSelectChange;//取消注册委托侦听
        }
        void SelectTool(int index) { //选择工具
            if(selectToolIndex == index) return;//相同不进行处理
            selectToolIndex = index;//更新index
            toolArray[selectToolIndex].DoShow();//选中的工具调用自身的DoShow方法
        }
        void OnGUI() { //绘制界面
            if (GUILayout.Button("-->说明文档<--", EditorStyles.toolbarButton)) { //添加工具栏的单击按钮
                Application.OpenURL("url");
            }
            //创建一个工具栏,1所选按钮索引,2按钮的名称 列表,3按钮样式,return 选中的索引
            int selectIndex = GUILayout.Toolbar(selectToolIndex, toolDisplayArray, EditorStyles.toolbarButton);
            if(selectIndex != selectToolIndex) { //工具栏选中索引和当前的不一致
                SelectTool(selectIndex);//选中当前索引的工具
            }
            EditorGUILayout.Space();//在上一个控件和下一个控件之间留出一个小空间
            toolArray[selectToolIndex].OnGUI();//调用选中工具的OnGUI方法
        }
        void Update() {
            if(Application.isPlaying) { //运行则不处理
                base.Close();
                return;
            }
            foreach (var tool in toolArray) tool.Update();//各个工具调用更新方法
        }
        void OnSelectChange() { //选中的内容发生变动
            toolArray[selectToolIndex].OnSelectChange();//调用当前选中的工具的OnSelectChange
            base.Repaint();//重绘GUI
        }
    }
}
```
### BaseAssetTool 工具基础类
包含工具基础的功能和各个工具类需要重写的虚方法
```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using UnityEditor;
namespace EditorAssetTools {
    public abstract class BaseAssetTool {
        EditorAssetToolsWindow tools_window;
        protected List<string> allAssetPath = new List<string>();//所有资源的路径
        protected string selectAssetPath = null;//选中资源的路径
        GameObject sceneRootGo;//预制体对象在场景的挂载节点
        GameObject sceneUIRootGo;//UI预制体对象在场景的挂载节点
        Dictionary<GameObject, Transform> prefabInstanceRecordDict = new Dictionary<GameObject, Transform>();//实例化的预制体字典
        readonly string[] defaultTargetDirectoryArray = new string[] { //默认的文件夹列表
            "选择预设目录",
            "Assets/Prefabs/UI",
        };
        protected string targetDirectory { get; private set; }
        public EditorAssetToolsWindow toolsWindow {
            get {return tools_window; }
            set {tools_window = value; }
        }
        public virtual void DoInit() {
            allAssetPath.AddRange(AssetDatabase.GetAllAssetPaths()); //获取项目中所有的资源路径并把路径添加到allAssetPath列表尾部
            targetDirectory = "Assets";
        }
        public virtual void DoDestroy() { //销毁方法
            if(sceneRootGo != null) {
                GameObject.DestroyImmediate(sceneRootGo);
                sceneRootGo = null;
            }
            if(sceneUIRootGo != null) {
                GameObject.DestroyImmediate(sceneUIRootGo);
                sceneUIRootGo = null;
            }
            prefabInstanceRecordDict.Clear();
            allAssetPath.Clear();
        }
        //定义需要子类重写的虚方法或实现的抽象方法
        public virtual void DoShow() {} //界面OnShow
        public virtual void OnSelectChange() {} //选中项发生变化,委托
        public abstract string Name { get; } //抽象属性,面板名称
        public abstract void OnGUI();//需要实现的OnGUI方法
        public virtual void Update () {
            selectAssetPath = AssetDatabase.GetAssetPath(Selection.activeObject);//更新选中项的资源路径
        }
        protected void DrawTargetDirectoryOnGUI(string label) {
            using (new EditorGUILayout.HorizontalScope()) { //创建一个新的 HorizontalScope 并开始相应的水平组,滚动视图
                targetDirectory = EditorGUILayout.TextField(label, targetDirectory);//目标文件夹
                int index = EditorGUILayout.Popup(0, defaultTargetDirectoryArray, GUILayout.Width(80));//
                if(index >= 1) {
                    targetDirectory = defaultTargetDirectoryArray[index];
                }
                if (!AssetDatabase.IsValidFolder(targetDirectory)) { //文件夹不是有效的文件夹
                    Color lastColumn = GUI.contentColor;
                    GUI.contentColor = Color.red;
                    EditorGUILayout.LabelField("目录无效！！！", GUILayout.Width(100));
                    GUI.contentColor = lastColumn;
                }
            }
        }
        //获取预制体对象在场景中的挂载节点,bool是否UI类型的
        protected GameObject GetSceneRootGo(bool isUI) {
            if (!isUI) { //不是UI
                if(sceneRootGo == null) { //sceneRootGo场景挂载节点
                    sceneRootGo = new GameObject(this.Name);
                }
                return sceneRootGo;
            }
            if(sceneUIRootGo == null) {
                sceneUIRootGo = new GameObject(this.Name + "_UI"); //创建一个新的挂载节点
                Canvas sceneCanvas = GameObject.FindObjectOfType<Canvas>();//查找Canvas组件
                //sceneCanvas不为空则通过检查每个父项并返回最后一个画布来返回最接近根的Canvas,若未找到其他画布，则画布返回自身。
                sceneCanvas = sceneCanvas != null ? sceneCanvas.rootCanvas : null;
                if(sceneCanvas != null) {
                    GameObjectUtility.SetParentAndAlign(sceneUIRootGo, sceneCanvas.gameObject);//设置父项 并为子项提供相同的层和位置
                    RectTransform rectComp = sceneUIRootGo.AddComponent<RectTransform>();//为canvas添加rectTransform
                    //布局设置为全屏拉伸的
                    rectComp.anchorMax = Vector2.one;
                    rectComp.anchorMin = Vector2.zero;
                    rectComp.offsetMin = Vector2.zero;
                    rectComp.offsetMax = Vector2.zero;
                }
                else { 
                    sceneCanvas = sceneUIRootGo.AddComponent<Canvas>();// 添加Canvas组件
                    sceneCanvas.renderMode = RenderMode.ScreenSpaceOverlay;//设置渲染模式
                    sceneUIRootGo.AddComponent<CanvasScaler>();//添加组件
                }
            }
            return sceneUIRootGo;
        }
        protected Transform GetPrefabInstanceGo(string prefabPath) { //按照预制体路径获取预制体在场景中的实例
            GameObject prefabAssetGo = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);//实例化gameObject
            Transform prefabInsTrans = null;//预制体对象挂载节点
            prefabInstanceRecordDict.TryGetValue(prefabAssetGo, out prefabInsTrans);//先去字典当前场景实例化的记录查询
            if(prefabInsTrans == null) { //当前场景记录字典不存在prefabInsTrans
                //获取recttransform组件,inactive(不活跃)组件也获取
                RectTransform rectTransComp = prefabAssetGo.GetComponentInChildren<RectTransform>(true);
                GameObject parentRootGo = GetSceneRootGo(rectTransComp != null);//获取挂载的根节点,传入bool值为是否为UI
                //返回值由 GetFileName()返回的字符串组成 去掉了扩展名分隔符和扩展名
                string prefabName = Path.GetFileNameWithoutExtension(prefabPath);//返回预制体的名称
                GameObject insPrefabGo = PrefabUtility.InstantiatePrefab(prefabAssetGo) as GameObject;//实例化
                insPrefabGo.name = prefabName;//重命名
                GameObjectUtility.SetParentAndAlign(insPrefabGo, parentRootGo);//设置挂载节点 并为子项提供相同的层和位置
                prefabInsTrans = insPrefabGo.transform;//记录prefabInsTrans到字典中
                prefabInstanceRecordDict[prefabAssetGo] = prefabInsTrans;
            }
            return prefabInsTrans;//返回实例化预制体对象挂载的节点
        }
        //定位资源在预制体组件的位置
        protected GameObject TryLocationPrefabComponentByAsset(string assetPath, string prefabPath) {
            UnityEngine.Object refAssetObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);//加载资源
            if(refAssetObj == null || assetPath.EndsWith(".prefab")) { //检测定位的对象,不能为空,不能为预制体,需要资源类型
                return null;
            }
            if(!prefabPath.EndsWith(".prefab")) { //传入的预制体路径正确
                return null;
            }
            Transform prefabInsTrans = GetPrefabInstanceGo(prefabPath); //获取预制体加载到场景的Transform组件
            HashSet<GameObject> selectGoHash = new HashSet<GameObject>();//需要选中的物体
            foreach(var comp in prefabInsTrans.GetComponentsInChildren<Component>(true)) { //遍历该预制体所有的组件
                using (SerializedObject serObjComp = new SerializedObject(comp)) { //提供通用的方式编辑对象属性
                    SerializedProperty serPro = serObjComp.GetIterator(); //获取迭代器
                    /// **** ///这里有一个情况是,序列化之后列表都是数组类型,数组类型的引用,要去遍历整个数组,对每个元素做下面的处理
                    bool isTargetComp = false;//找到目标组件
                    while(serPro.NextVisible(true)) { //遍历所有属性字段 
                        if(serPro.propertyType != SerializedPropertyType.ObjectReference) continue; //不是引用类型不处理
                        if(serPro.objectReferenceValue == refAssetObj) { //对象引用属性的值和资源一致
                            isTargetComp = true;//标记为目标组件
                        } else { // 间接引用,例如(若是引入了材质球之类的,会有shader -> 贴图之类的)
                            string objRefPath = AssetDatabase.GetAssetPath(serPro.objectReferenceValue);//获取资源路径
                            if(!string.IsNullOrEmpty(objRefPath)) { //判空
                                foreach(string depPath in AssetDatabase.GetDependencies(objRefPath, true)) { //遍历依赖的资源列表
                                    if(depPath == assetPath) { //和目标资源路径相同
                                        isTargetComp = true; //标记为目标组件
                                        break;
                                    }
                                }
                            }
                        }
                        if(isTargetComp) {
                            selectGoHash.Add(comp.gameObject); //把目标组件添加到选中列表
                            break;
                        }
                    }
                }
            }
            if(selectGoHash.Count > 0) {     //存在可选中的对象
                GameObject[] goArray = new GameObject[selectGoHash.Count];
                selectGoHash.CopyTo(goArray);//将hashtable的元素复制到数组
                Selection.objects = goArray;//选中这些预制体的节点对象
            }
            return prefabInsTrans.gameObject;
        }
        //定位
        protected GameObject TryLocationPrefabInstanceChildByComponent(GameObject prefabAsset, Component prefabAssetComp) {
            string childPath = GetChildPathByComponent(prefabAsset.transform, prefabAssetComp);//查找组件在预制体中的路径
            Transform prefabInstance = GetPrefabInstanceGo(AssetDatabase.GetAssetPath(prefabAsset));
            GameObject locationGo = prefabInstance.gameObject;
            if (!string.IsNullOrEmpty(childPath)) { //可以找到组件
                Transform targetChild = prefabInstance.Find(childPath);//根据路径查组件
                locationGo = targetChild != null ? targetChild.gameObject : null;//需要定位的组件
            }
            Selection.activeGameObject = locationGo;//选中定位的组件
            return prefabInstance.gameObject;
        }
        //查找组件在预制体中的路径 类PetMainPanel/Bg/Title/Name
        protected string GetChildPathByComponent(Transform rootTrans, Component targetComp) {
            Transform parentTrans = targetComp.transform;//组件的transform节点
            string path = string.Empty;
            while(parentTrans.transform != rootTrans){ //父节点不为根节点
                string curGoName = parentTrans.gameObject.name;//节点名称
                if(path == string.Empty) path = curGoName;
                else path = curGoName + "/" + path;//这里拼接路径,每一个父节点都会拼接
                parentTrans = parentTrans.parent;//更新父节点
                if(parentTrans == null) { //父节点空报错
                    Debug.LogError("Can't Reach Root Transform-->" + rootTrans + "##" + targetComp);
                    break;
                }
            }
            return path;
        }
        //获取文件夹内的所有的资源路径列表,递归查找子文件夹内的文件
        public static void GetAssetPathsFromDirectory(string directoryPath, ref List<string> retPathList) {
            if (Directory.Exists(directoryPath)){ //目标文件夹存在
                foreach (var p in Directory.GetFileSystemEntries(directoryPath)){ //获取文件夹下的所有文件
                    if (Directory.Exists(p)){ //文件夹
                        GetAssetPathsFromDirectory(p, ref retPathList);
                    } else if (Path.GetExtension(p) != ".meta"){ //剔除掉meta文件的资源
                        retPathList.Add(p.Replace("\\", "/"));//反斜杠转斜杠
                    }
                }
            } else if (File.Exists(directoryPath) && Path.GetExtension(directoryPath) != ".meta"){ //选择了文件且不是meta文件
                retPathList.Add(directoryPath.Replace("\\", "/"));
            }
        }
    }
}
```

### ReferenceSearchAssetTool 资源引用查找
```chasrp
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace EditorAssetTools {
    /// 资源引用查找定位
    public class ReferenceSearchAssetTool : BaseAssetTool {
        class SearchObjInfo {
            public string objPath;
            public bool isSelect = false;
            public List<string> refPathList = new List<string>();
        }
        enum SortType {
            path, 
            type,
            refCoutDown, 
            refCountUp, 
            texSizeDown, 
            texSizeUp 
        }
        List<SearchObjInfo> searchObjList = new List<SearchObjInfo>();
        Dictionary<string, string[]> assetsDependenciesDict;
        float scrollItemIndex = 0;
        public override string Name { //重命名工具面板
            get{ return "资源引用查找"; }
        }
        //初始化,基类初始化 -> 获取项目中所有的资源路径并把路径添加到allAssetPath列表尾部
        public override void DoInit() {
            base.DoInit();
        }
        //销毁方法
        public override void DoDestroy() {
            searchObjList.Clear();
            assetsDependenciesDict = null;
            base.DoDestroy();
        }
        public override void OnGUI() { //绘制工具面板
            EditorGUILayout.BeginVertical(); //垂直控件组
            if(!string.IsNullOrEmpty(selectAssetPath)) { //选中的资源路径为为空
                EditorGUILayout.LabelField("当前选择的路径或资源：" + selectAssetPath);//显示选择路劲
            } else { //为空则提示语
                EditorGUILayout.HelpBox("请先从Project窗口里的右侧选择需要查找的资源或文件夹", MessageType.Warning);
            }
            GUILayout.Space(10);//插入空白元素 10 pexels
            
            base.DrawTargetDirectoryOnGUI("锁定搜索目录：");//在工具面板上显示文件路径
            EditorGUILayout.BeginHorizontal();//绘制水平组
            if (GUILayout.Button("查找被哪些资源引用")){ //按钮 查asset被那些prefabs引用
                try { 
                    DoReferenceSearch();//引用查找
                    DoSortList(SortType.path); 
                } finally { 
                    EditorUtility.ClearProgressBar();//清除进度条
                }
            }
            if (GUILayout.Button("查找引用了哪些资源")){ //按钮 查prefab被那些assets引用
                try { 
                    DoDependenciesSearch(); 
                    DoSortList(SortType.path); 
                } finally { 
                    EditorUtility.ClearProgressBar(); 
                }
            }
            EditorGUILayout.EndHorizontal();
            if (assetsDependenciesDict != null && GUILayout.Button("清除缓存，重置引用信息")){
                assetsDependenciesDict = null;
            }
            EditorGUILayout.EndVertical();
            if (searchObjList.Count == 0 ) return;
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("选中所有")) ClickSelectAllBtn(true);
            if (GUILayout.Button("取消所有选中")) ClickSelectAllBtn(false);
            if (GUILayout.Button("选中无引用的资源")) ClickSelectNoRefBtn();
            if (GUILayout.Button("删除选中资源")) ClickSearchDelAssetBtn();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("排序规则：", GUILayout.Width(60));
            if (GUILayout.Button("路径")) DoSortList(SortType.path);
            if (GUILayout.Button("类型")) DoSortList(SortType.type);
            if (GUILayout.Button("引用数量递增")) DoSortList(SortType.refCountUp );
            if (GUILayout.Button("引用数量递减")) DoSortList(SortType.refCoutDown);
            if (GUILayout.Button("贴图尺寸递增")) DoSortList(SortType.texSizeUp);
            if (GUILayout.Button("贴图尺寸递减")) DoSortList(SortType.texSizeDown);
            EditorGUILayout.EndHorizontal();
            //show asset list
            GUILayout.Space(10);
            EditorGUILayout.LabelField(string.Format("找到目标资源总数--> {0}", searchObjList.Count));
            GUILayout.Space(10);
            Event curEvt = Event.current;
            if (curEvt != null && curEvt.isScrollWheel){
                scrollItemIndex += curEvt.delta.y;
                curEvt.Use();
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            const int kViewItemCount = 25;
            int curItemIndex = 0;
            for (int idx = 0; idx < searchObjList.Count; ++idx){
                var objInfo = searchObjList[idx];
                if (curItemIndex >= scrollItemIndex && curItemIndex <= scrollItemIndex + kViewItemCount){
                    using (var horScope = new EditorGUILayout.HorizontalScope()){
                        Color guiColor = GUI.contentColor;
                        GUI.contentColor = objInfo.isSelect ? Color.blue : Color.green;
                        objInfo.isSelect = EditorGUILayout.Toggle(objInfo.isSelect, GUILayout.Width(20));
                        EditorGUILayout.LabelField(string.Format("目标资源{0}--> {1}", idx, objInfo.objPath));
                        if (GUILayout.Button(string.Format("定位资源({0}个引用)", objInfo.refPathList.Count), GUILayout.Width(300))){
                            Selection.activeObject = AssetDatabase.LoadAssetAtPath(objInfo.objPath, typeof(UnityEngine.Object));//选中查找的资源
                        }
                        if (curEvt != null && curEvt.rawType == EventType.MouseDown && curEvt.button == 0 && horScope.rect.Contains(curEvt.mousePosition)) {
                            objInfo.isSelect = !objInfo.isSelect;
                            if (curEvt.shift){
                                for (int j = idx - 1; j >= 0; --j){
                                    var lastObjInfo = searchObjList[j];
                                    if (lastObjInfo.isSelect != objInfo.isSelect) lastObjInfo.isSelect = objInfo.isSelect;
                                    else break;
                                }
                            }
                            curEvt.Use();
                        }
                        GUI.contentColor = guiColor;
                    }
                }
                ++curItemIndex;
                for (int k = 0; k < objInfo.refPathList.Count; ++k){
                    if (curItemIndex >= this.scrollItemIndex && curItemIndex <= this.scrollItemIndex + kViewItemCount){
                        EditorGUILayout.BeginHorizontal();
                        var refPath = objInfo.refPathList[k];
                        EditorGUILayout.LabelField(string.Format("\t{0}.{1}", k, refPath));
                        TryDrawLocationComponent(objInfo.objPath, refPath);
                        if (GUILayout.Button("定位资源", GUILayout.Width(100))){
                            var obj = AssetDatabase.LoadAssetAtPath(refPath, typeof(UnityEngine.Object));
                            if(obj != null) Selection.activeObject = obj;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    ++curItemIndex;
                }
            }
            EditorGUILayout.EndVertical();
            float scrollMaxValue = curItemIndex - kViewItemCount;
            scrollItemIndex = GUILayout.VerticalScrollbar(scrollItemIndex, 0, 0f, scrollMaxValue + 1, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndHorizontal();
        }
        //
        void TryDrawLocationComponent(string pathA, string pathB) {
            string assetPath = null;
            string prefabPath = null;
            if (pathA.EndsWith(".prefab") && GUILayout.Button("定位组件", GUILayout.Width(100))) {
                assetPath = pathB;
                prefabPath = pathA;
            } else if (pathB.EndsWith(".prefab") && GUILayout.Button("定位组件", GUILayout.Width(100))) {
                assetPath = pathA;
                prefabPath = pathB;
            }
            if(assetPath != null && prefabPath != null) {
                TryLocationPrefabComponentByAsset(assetPath, prefabPath);
            }
        }
        //引用搜索
        void DoReferenceSearch() {
            searchObjList.Clear();//清理搜索结果列表
            if(string.IsNullOrEmpty(selectAssetPath)) { //选中的资源路径不为空
                return;
            }
            //find all file asset path
            List<string> allObjPathList = new List<string>();//拿取目录下的所有资源路径
            GetAssetPathsFromDirectory(selectAssetPath, ref allObjPathList);
            if (allObjPathList.Count == 0) {
                return;
            }
            //这个字典存储选中资源名称 -> 搜索到的对象
            Dictionary<string, SearchObjInfo> assetSearchInfoDic = new Dictionary<string, SearchObjInfo>();
            for (int i = 0; i < allObjPathList.Count; ++i){
                var info = new SearchObjInfo();
                info.objPath = allObjPathList[i];//存储路径字符串
                assetSearchInfoDic[info.objPath] = info;
                searchObjList.Add(info);
            }
            float processIndex = 0;
            int totalProcessCount;//
            processIndex = 0;
            if(assetsDependenciesDict == null) { //不存在资源依赖记录字典,为所有资源建立一个依赖字典,后续查找耗时会大大减少
                //以下foreach循环完成后,该字典会存储所有目标文件夹下资源路径对应依赖资源路径列表的字典
                assetsDependenciesDict = new Dictionary<string, string[]>();
                foreach(var curAssetPath in base.allAssetPath) { //所有资源路径
                    //Debug.Log(curAssetPath);
                    ++processIndex;
                    //目标文件夹是无效文件夹且当前资源所处路径不是以目标文件夹为前驱目录
                    if (AssetDatabase.IsValidFolder(base.targetDirectory) && !curAssetPath.StartsWith(base.targetDirectory)) {
                        continue;
                    }
                    //是否要取消查找操作
                    if(EditorUtility.DisplayCancelableProgressBar("Search Assets", curAssetPath, processIndex / base.allAssetPath.Count)) {
                        assetsDependenciesDict = null;
                        return;
                    }
                    //返回一个数组,其中包含作为指定pathName处资源的依赖关系的所有资源,true为间接依赖也去搜索
                    string[] depPathArray = AssetDatabase.GetDependencies(curAssetPath, true);
                    // for (var i = 0; i < depPathArray.Length; i++) {
                    //     Debug.Log(depPathArray[i]);
                    // }
                    assetsDependenciesDict[curAssetPath] = depPathArray;//记录下该资源的依赖资源路径列表
                    foreach (var depPath in depPathArray){ //遍历依赖路劲列表
                        SearchObjInfo objInfo;
                        //当前依赖文件路径不是原资源路径
                        if (curAssetPath != depPath && assetSearchInfoDic.TryGetValue(depPath, out objInfo)){
                            objInfo.refPathList.Add(curAssetPath);//反推,被依赖资源路径 添加 某对象,记录下来
                        }
                    }
                }
                // foreach (KeyValuePair<string, string[]> kvp in assetsDependenciesDict) {
                //     Debug.Log("k: " + kvp.Key);
                //     for (var i = 0; i < kvp.Value.Length; i++) {
                //         Debug.Log("v: " + kvp.Value[i]);
                //     }
                //     Debug.Log("-----------------------------");
                // }
            } else {
                totalProcessCount = assetsDependenciesDict.Count;
                foreach(var kv in assetsDependenciesDict) { //遍历(资源路径 -> 依赖资源路径列表)记录字典
                    string curAssetPath = kv.Key;//资源路径
                    string[] assetDependenciesArray = kv.Value;//依赖资源路径列表
                    ++processIndex;
                    EditorUtility.DisplayProgressBar("Search Cache", curAssetPath, processIndex / totalProcessCount);//进度条
                    foreach (var depPath in assetDependenciesArray){ //遍历依赖资源路径列表
                        SearchObjInfo objInfo;
                        if (curAssetPath != depPath && assetSearchInfoDic.TryGetValue(depPath, out objInfo)){
                            objInfo.refPathList.Add(curAssetPath);//为依赖资源的被依赖对象路径列表添加资源路径
                        }
                    }
                }
            }
        }
        //依赖查找
        void DoDependenciesSearch() {
            searchObjList.Clear();//清理搜索结果列表
            if(string.IsNullOrEmpty(selectAssetPath)) { //选择的资源路径不为空
                return;
            }
            List<string> allObjPathList = new List<string>();
            GetAssetPathsFromDirectory(selectAssetPath, ref allObjPathList);
            float processIndex = 0;
            foreach(var objPath in allObjPathList) {
                ++processIndex;
                EditorUtility.DisplayProgressBar("Search Dependencies", objPath, processIndex / allObjPathList.Count);
                SearchObjInfo objInfo = new SearchObjInfo();
                searchObjList.Add(objInfo);
                objInfo.objPath = objPath;
                foreach (var path in AssetDatabase.GetDependencies(objPath, true)){ //获取资源路径对应资源的依赖项
                    if (path != objPath) objInfo.refPathList.Add(path);
                }
            }
        }
        #region
        void ClickSearchDelAssetBtn() {
            string hintStr = "";
            foreach (var objInfo in searchObjList) {
                if (objInfo.isSelect) { hintStr += "\n" + objInfo.objPath; }
            }
            if (string.IsNullOrEmpty(hintStr)) return;
            if (EditorUtility.DisplayDialog("删除资源", hintStr, "确定（资源删除后不能恢复）", "取消")) {
                for (int i = searchObjList.Count - 1; i >= 0; --i) {
                    var objInfo = searchObjList[i];
                    if (objInfo.isSelect) {
                        AssetDatabase.DeleteAsset(objInfo.objPath);
                        searchObjList.RemoveAt(i);
                    }
                }
                assetsDependenciesDict = null;
                AssetDatabase.RemoveUnusedAssetBundleNames();
                AssetDatabase.Refresh();
            }
        }
        void DoSortList(SortType sortType) {
            if (searchObjList == null) return;
            System.Comparison<string> CompareFunc = (pathA, pathB)=> {
                int compareV = 0;
                if (sortType == SortType.type) {
                    string exA = Path.GetExtension(pathA);
                    string exB = Path.GetExtension(pathB);
                    compareV = exA.CompareTo(exB);
                } else if (sortType == SortType.texSizeUp || sortType == SortType.texSizeDown) {
                    Texture texAssetA = AssetDatabase.LoadAssetAtPath<Texture>(pathA);
                    Texture texAssetB = AssetDatabase.LoadAssetAtPath<Texture>(pathB);
                    if (texAssetA != null && texAssetB != null) {
                        int sizeA = texAssetA.width * texAssetA.height;
                        int sizeB = texAssetB.width * texAssetB.height;
                        compareV = sortType == SortType.texSizeUp ? sizeA.CompareTo(sizeB) : sizeB.CompareTo(sizeA);
                    } else if (texAssetA != null){
                        compareV = -1;
                    } else if (texAssetB != null) {
                        compareV = 1;
                    }
                }
                if (compareV == 0) {
                    string directoryA = Path.GetDirectoryName(pathA) + "/" + Path.GetFileNameWithoutExtension(pathA);
                    string directoryB = Path.GetDirectoryName(pathB) + "/" + Path.GetFileNameWithoutExtension(pathB);
                    compareV = directoryA.CompareTo(directoryB);
                }
                return compareV;
            };
            searchObjList.Sort((infoA, infoB) => {
                int compareV = 0;
                if(sortType == SortType.refCountUp) {
                    compareV = infoA.refPathList.Count.CompareTo(infoB.refPathList.Count);
                } else if(sortType == SortType.refCoutDown) {
                    compareV = infoB.refPathList.Count.CompareTo(infoA.refPathList.Count);
                }
                if(compareV != 0) {
                    return compareV;
                }
                compareV = CompareFunc(infoA.objPath, infoB.objPath);
                return compareV;
            });
            foreach(var info in searchObjList) {
                info.refPathList.Sort(CompareFunc);
            }
        }
        void ClickSelectNoRefBtn() {
            if (searchObjList == null) return;
            foreach(var objInfo in searchObjList) {
                bool isNoRef = objInfo.refPathList.Count == 0;
                objInfo.isSelect = isNoRef == true;
            }
        }
        void ClickSelectAllBtn(bool isSelectAll) {
            if (searchObjList == null) return;
            foreach(var objInfo in searchObjList) {
                objInfo.isSelect = isSelectAll == true;
            }
        }
        #endregion
    }
}
```
