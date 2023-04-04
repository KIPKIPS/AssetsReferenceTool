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

        protected override void IsTargetPrefab(GameObject prefabGo, List<Component> compList) {
            foreach (var graphic in prefabGo.GetComponentsInChildren<Graphic>(true)) {
                if (IsTargetGraphic(graphic)) compList.Add(graphic);
            }
        }

        private static bool IsTargetGraphic(Graphic uiGraphic) {
            if (!StTargetGraphicDict.ContainsKey(uiGraphic.GetType())) {
                return false;
            }
            var uiShader = uiGraphic.material != null ? uiGraphic.material.shader : null;
            if (uiGraphic.color.a < 0.001f && (uiShader == null || uiShader.name != "XianXia/UIStencilMask")) {
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