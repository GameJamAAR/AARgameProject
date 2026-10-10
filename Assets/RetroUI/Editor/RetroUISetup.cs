using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.InputSystem.UI;
namespace AAR.RetroUI.Editor
{
    public static class RetroUISetup
    {
        const string FontPath = "Assets/RetroUI/Fonts/Tiny5 Bitmap.asset";
        [MenuItem("Tools/AAR/Install Shader Debug UI")]
        public static void Install()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Install outside Play mode.");
            if (GameObject.Find("Shader Debug UI")) return;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (!font)
            {
                if (!Shader.Find("TextMeshPro/Bitmap")) TMP_PackageResourceImporter.ImportResources(true,false,false);
                font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/RetroUI/Fonts/Tiny5-Regular.ttf"),
                    16, 1, GlyphRenderMode.RASTER, 512, 512, AtlasPopulationMode.Dynamic);
                font.name = "Tiny5 Bitmap";
                AssetDatabase.CreateAsset(font,FontPath);
                font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 :.%/-+éèêàùçÉ[]");
                font.atlasPopulationMode = AtlasPopulationMode.Static;
                font.atlasTexture.filterMode = FilterMode.Point;
                AssetDatabase.AddObjectToAsset(font.material,font);
                AssetDatabase.AddObjectToAsset(font.atlasTexture,font);
                EditorUtility.SetDirty(font); AssetDatabase.SaveAssets();
            }
            var root = new GameObject("Shader Debug UI",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(root,"Install shader debug UI");
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.pixelPerfect = true; canvas.sortingOrder=100;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
            // Whole-number scaling preserves the font's bitmap texels.
            scaler.scaleFactor = 1;
            var controller = root.AddComponent<ShaderWaveController>();
            var character = GameObject.Find("Character"); if(character) controller.character=character.transform;
            var panel = Rect("Window",root.transform,new Vector2(360,154),new Vector2(24,24));
            panel.anchorMin=panel.anchorMax=panel.pivot=Vector2.zero;
            var frame = panel.gameObject.AddComponent<Win98Bevel>(); frame.raycastTarget=false;
            var title=Rect("TitleBar",panel,new Vector2(348,26),new Vector2(6,-6));
            Solid(title,new Color32(0,0,128,255));
            Label("Title",title,"Shader Lab",font,Color.white,new Vector2(326,26),new Vector2(8,0));
            Label("Instruction",panel,"Palette  /  choisir les couleurs",font,Color.black,new Vector2(332,24),new Vector2(14,-38));
            var row=Rect("Looks",panel,new Vector2(332,38),new Vector2(14,-68));
            var layout=row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();layout.spacing=8;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=true;
            var buttons = new UnityEngine.UI.Button[controller.looks.Length];
            for(int i=0;i<buttons.Length;i++)
            {
                var rect=Rect(controller.looks[i].label,row,new Vector2(77,38),Vector2.zero);
                var bevel=rect.gameObject.AddComponent<Win98Bevel>();bevel.raycastTarget=true;
                var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=bevel;button.transition=UnityEngine.UI.Selectable.Transition.None;
                var label=Label("Label",rect,controller.looks[i].label,font,Color.black,new Vector2(77,38),Vector2.zero);
                label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=Vector2.zero;label.rectTransform.offsetMax=Vector2.zero;label.alignment=TextAlignmentOptions.Center;
                buttons[i]=button;
            }
            var statusRect=Rect("Status",panel,new Vector2(332,26),new Vector2(14,-116));
            var statusFrame=statusRect.gameObject.AddComponent<Win98Bevel>();statusFrame.inset=true;statusFrame.raycastTarget=false;
            var status=Label("Text",statusRect,"Actif : Original",font,Color.black,new Vector2(318,26),new Vector2(8,0));
            var debug=root.AddComponent<ShaderDebugPanel>();debug.controller=controller;debug.buttons=buttons;debug.status=status;
            if (!UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>())
            {
                var events=new GameObject("EventSystem",typeof(UnityEngine.EventSystems.EventSystem),typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(events,"Create UI events"); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            if(!AssetDatabase.IsValidFolder("Assets/RetroUI/Prefabs")) AssetDatabase.CreateFolder("Assets/RetroUI","Prefabs");
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/RetroUI/Prefabs/Shader Debug UI.prefab");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.scene);
            AssetDatabase.SaveAssets();
        }
        static RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 pos)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.sizeDelta=size;r.anchoredPosition=pos;return r;
        }
        static UnityEngine.UI.Image Solid(RectTransform r,Color c)
        { var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=c;image.raycastTarget=false;return image; }
        static TMP_Text Label(string name,Transform parent,string value,TMP_FontAsset font,Color color,Vector2 size,Vector2 pos)
        {
            var r=Rect(name,parent,size,pos);var text=r.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=16;
            text.text=value;text.color=color;text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.NoWrap;return text;
        }
    }
}
