using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EditorAssetTools {
    public class TransparentGraphicPrefabCheckTool : BasePrefabAssetTool {
        private static readonly Dictionary<System.Type, bool> StTargetGraphicDict = new() {
            {typeof(Image), true},
            {typeof(RawImage), true},
            {typeof(Text), true},
        };

        public override string Name => "透明的UI Graphic";

        protected override void IsTargetPrefab(GameObject prefab_go, List<Component> comp_list) {
            foreach (var graphic in prefab_go.GetComponentsInChildren<Graphic>(true)) {
                if (IsTargetGraphic(graphic)) comp_list.Add(graphic);
            }
        }

        private static bool IsTargetGraphic(Graphic ui_graphic) {
            if (!StTargetGraphicDict.ContainsKey(ui_graphic.GetType())) {
                return false;
            }
            var uiShader = ui_graphic.material != null ? ui_graphic.material.shader : null;
            if (ui_graphic.color.a < 0.001f && (uiShader == null || uiShader.name != "XianXia/UIStencilMask")) {
                return true;
            }
            return false;
        }

        public override void OnGUI() {
            DrawDefaultHeader();
            DrawSearchButton();
            DrawTargetPrefabAssetList(null);
        }
    }
}