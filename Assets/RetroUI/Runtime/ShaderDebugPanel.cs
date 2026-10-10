using TMPro;
using UnityEngine;
namespace AAR.RetroUI
{
    public sealed class ShaderDebugPanel : MonoBehaviour
    {
        public ShaderWaveController controller;
        public UnityEngine.UI.Button[] buttons;
        public TMP_Text status;
        [Min(360)] public int scaleReferenceHeight = 720;
        UnityEngine.UI.CanvasScaler scaler;
        void OnEnable()
        {
            scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
            if (!controller) return;
            for(int i=0;i<buttons.Length;i++) { int index=i; buttons[i].onClick.AddListener(()=>controller.Select(index)); }
            controller.Changed += Refresh;
            Refresh();
        }
        void OnDisable()
        {
            if (controller) controller.Changed -= Refresh;
            // This panel owns these runtime listeners; persistent inspector events remain.
            foreach(var button in buttons) if(button) button.onClick.RemoveAllListeners();
        }
        void Update()
        {
            if(scaler) scaler.scaleFactor = Mathf.Max(1, Mathf.Min(Screen.height / scaleReferenceHeight, Screen.width / 400));
            if(!controller) return;
            if(controller.IsTransitioning) status.text = "Vers : " + controller.looks[controller.Target].label;
        }
        void Refresh()
        {
            for(int i=0;i<buttons.Length;i++) buttons[i].GetComponent<Win98Bevel>().Refresh(i==controller.Target);
            if(status) status.text = "Actif : " + controller.looks[controller.Current].label;
        }
    }
}
