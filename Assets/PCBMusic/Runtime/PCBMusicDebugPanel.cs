using UnityEngine;
using TMPro;
using AAR.RetroUI;

namespace AAR.PCBMusic
{
    public sealed class PCBMusicDebugPanel : MonoBehaviour
    {
        public PCBMusicController controller;
        RectTransform window;
        TMP_Text status,contributors;
        UnityEngine.UI.CanvasScaler scaler;
        double nextRefresh;
        bool collapsed;
        bool interacting,previousInput;
        PlayerController player;
        UnityEngine.UI.Slider volumeSlider;
        static readonly string[] RoleNames={"Puce","Condensateur","RAM","SMD"};
        static readonly string[] StateNames={"Silence","Veille","Pression","Suspension","Retombée"};
        void Start()
        {
            if(!controller)controller=GetComponent<PCBMusicController>();
            var root=Rect("Music Debug Canvas",transform);
            var canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=110;canvas.pixelPerfect=true;
            scaler=root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
            root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            window=Rect("Music Lab",root);window.anchorMin=window.anchorMax=window.pivot=Vector2.one;window.anchoredPosition=new Vector2(-24,-24);window.sizeDelta=new Vector2(378,296);Win98Style.Bevel(window);
            var stack=window.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();stack.padding=new RectOffset(6,6,6,6);stack.spacing=6;stack.childControlWidth=stack.childControlHeight=true;stack.childForceExpandWidth=true;stack.childForceExpandHeight=false;
            var title=Row("Title",window,26);var face=title.gameObject.AddComponent<UnityEngine.UI.Image>();face.color=Win98Style.Title;face.raycastTarget=false;
            Label(title,"Music Lab / F8",Color.white);
            Button(title,"_",()=>{collapsed=!collapsed;for(int i=1;i<window.childCount;i++)window.GetChild(i).gameObject.SetActive(!collapsed);window.sizeDelta=new Vector2(378,collapsed?38:296);},24);
            status=Label(Row("Status",window,40),"Initialisation…",Color.black);status.fontSize=15;
            contributors=Label(Row("Contributors",window,54),"",Color.black);contributors.fontSize=15;
            var modes=Row("Form",window,28);Button(modes,"Auto",()=>controller.Automatic());Button(modes,"Veille",()=>controller.Force(MusicState.Veille));Button(modes,"Pression",()=>controller.Force(MusicState.Pression));
            var silence=Row("Silence",window,28);Button(silence,"Silence",()=>controller.Force(MusicState.Silence));Button(silence,"Suspension",()=>controller.Force(MusicState.Suspension));Button(silence,"Figer vue",()=>controller.freezeObservation=!controller.freezeObservation);
            var solo=Row("Solo",window,28);for(int i=0;i<3;i++){int role=i;Button(solo,RoleNames[i],()=>controller.Solo(role));}
            var volume=Row("Volume",window,26);Label(volume,"Volume musique",Color.black);
            var rail=Rect("Volume Slider",volume);var railSize=rail.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();railSize.preferredWidth=128;railSize.flexibleWidth=1;var track=Win98Style.Bevel(rail,true);track.raycastTarget=true;
            var slider=rail.gameObject.AddComponent<UnityEngine.UI.Slider>();volumeSlider=slider;slider.minValue=0;slider.maxValue=1;
            var handle=Rect("Handle",rail);handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=handle.offsetMax=Vector2.zero;handle.sizeDelta=new Vector2(12,0);var bevel=Win98Style.Bevel(handle,false,null,true);
            slider.handleRect=handle;slider.targetGraphic=bevel;slider.transition=UnityEngine.UI.Selectable.Transition.None;slider.SetValueWithoutNotify(controller.Settings?controller.Settings.volume:.32f);slider.onValueChanged.AddListener(controller.SetVolume);
        }
        void Update()
        {
            if(SettingsMenu.IsOpen)interacting=false;
            var keyboard=UnityEngine.InputSystem.Keyboard.current;
            if(keyboard!=null&&keyboard.f8Key.wasPressedThisFrame&&!SettingsMenu.IsOpen)
            {
                SetInteraction(!interacting);
            }
            if(scaler)scaler.scaleFactor=Mathf.Max(1,Mathf.Min(Screen.height/720,Screen.width/1000));
            if(!controller||controller.Composer==null||Time.unscaledTimeAsDouble<nextRefresh||!status)return;nextRefresh=Time.unscaledTimeAsDouble+.25;
            var c=controller;string state=c.Paused?"Pause":StateNames[(int)c.Composer.State];
            if(volumeSlider&&c.Settings)volumeSlider.SetValueWithoutNotify(c.Settings.volume);
            status.text=state+" / "+(c.Composer.IsForced?"Manuel":"Auto")+" / voix "+c.ActiveVoices+"\nAnalyse "+c.AnalysisMilliseconds.ToString("0.00")+" ms / notes "+c.NotesPlayed;
            string names="";for(int i=0;i<c.View.SelectedCount;i++){var a=c.View.selected[i];if(i>0)names+="  /  ";names+=RoleNames[(int)a.role]+" G"+a.group;}
            contributors.text=c.View.Snapshot?names+"\nCandidats "+c.View.Candidates+" / masqués "+c.View.Occluded:"Pas de données PCB : installer le registre";
        }
        public void SetInteraction(bool value)
        {
            if(interacting==value||SettingsMenu.IsOpen)return;
            if(!player)player=FindAnyObjectByType<PlayerController>();interacting=value;
            if(player){if(value){previousInput=player.InputEnabled;player.SetInputEnabled(false);}else player.SetInputEnabled(previousInput);}
        }
        void OnDisable(){if(interacting&&player&&!SettingsMenu.IsOpen)player.SetInputEnabled(previousInput);interacting=false;}
        static RectTransform Rect(string name,Transform parent)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r;}
        static RectTransform Row(string name,Transform parent,float height)
        {
            var r=Rect(name,parent);var e=r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();e.preferredHeight=e.minHeight=height;e.flexibleHeight=0;
            var layout=r.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();layout.spacing=5;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=false;layout.childForceExpandHeight=true;return r;
        }
        static TMP_Text Label(Transform parent,string value,Color color)
        {
            var r=Rect("Label",parent);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=Win98Style.Font;t.fontSize=16;t.color=color;t.text=value;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;t.alignment=TextAlignmentOptions.MidlineLeft;
            var e=r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();e.flexibleWidth=1;e.minWidth=0;return t;
        }
        static void Button(Transform parent,string name,UnityEngine.Events.UnityAction action,float width=-1)
        {
            var r=Rect(name,parent);var bevel=Win98Style.Bevel(r,false,null,true);var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=bevel;b.transition=UnityEngine.UI.Selectable.Transition.None;b.onClick.AddListener(action);
            var e=r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();e.flexibleWidth=width<0?1:0;if(width>0)e.minWidth=e.preferredWidth=width;
            var text=Label(r,name,Color.black);text.alignment=TextAlignmentOptions.Center;text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;
        }
    }
}
