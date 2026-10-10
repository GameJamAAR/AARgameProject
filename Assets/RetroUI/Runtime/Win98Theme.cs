using TMPro;
using UnityEngine;

namespace AAR.RetroUI
{
    [CreateAssetMenu(menuName="AAR/UI/Windows 98 Theme")]
    public sealed class Win98Theme : ScriptableObject
    {
        public TMP_FontAsset font;
        public Color face=new Color32(192,192,192,255);
        public Color title=new Color32(0,0,128,255);
    }

    public static class Win98Style
    {
        static Win98Theme theme;
        public static Win98Theme Theme => theme ? theme : theme=Resources.Load<Win98Theme>("RetroUI/Windows 98 Theme");
        public static TMP_FontAsset Font => Theme&&Theme.font?Theme.font:TMP_Settings.defaultFontAsset;
        public static Color Face => Theme?Theme.face:(Color)new Color32(192,192,192,255);
        public static Color Title => Theme?Theme.title:(Color)new Color32(0,0,128,255);
        public static Win98Bevel Bevel(RectTransform rect,bool inset=false,Color? face=null,bool interactive=false)
        {
            var bevel=rect.gameObject.AddComponent<Win98Bevel>();bevel.face=face??Face;bevel.inset=inset;bevel.raycastTarget=interactive;return bevel;
        }
    }
}
