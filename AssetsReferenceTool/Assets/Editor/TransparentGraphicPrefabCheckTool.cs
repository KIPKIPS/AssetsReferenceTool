using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

//*************************************************
//****copyRight LuoYao 2020/05
//*************************************************

namespace EditorAssetTools
{
    public class TransparentGraphicPrefabCheckTool : BasePrefabAssetTool
    {
        readonly static Dictionary<System.Type, bool> st_target_graphic_dict = new Dictionary<System.Type, bool>() {
            {typeof(Image), true},
            {typeof(RawImage), true},
            {typeof(Text), true},
        };

        public override string Name{
            get{ return "透明的UI Graphic"; }
        }

        public override void DoInit()
        {
            base.DoInit();
        }
        public override void DoDestroy()
        {
            base.DoDestroy();
        }

        protected override void IsTargetPrefab(GameObject prefab_go, List<Component> comp_list)
        {
            foreach(var graphic in prefab_go.GetComponentsInChildren<Graphic>(true)) {
                if(IsTargetGraphic(graphic)) comp_list.Add(graphic);
            }
        }

        public static bool IsTargetGraphic(Graphic ui_graphic)
        {
            if (!st_target_graphic_dict.ContainsKey(ui_graphic.GetType())) {
                return false;
            }
            Shader ui_shader = ui_graphic.material != null ? ui_graphic.material.shader : null;
            if (ui_graphic.color.a < 0.001f && (ui_shader == null || ui_shader.name != "XianXia/UIStencilMask")) {
                return true;
            }
            return false;
        }

        public override void OnGUI()
        {
            base.DrawDefaultHeader();
            base.DrawSearchButton();
            base.DrawTargetPrefabAssetList(null);
        }
    }
}
