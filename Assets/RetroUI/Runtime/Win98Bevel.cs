using UnityEngine;
using UnityEngine.EventSystems;

namespace AAR.RetroUI
{
    // Resolution-independent geometry, with two crisp nested bevels and no textures.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class Win98Bevel : UnityEngine.UI.MaskableGraphic, IPointerDownHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public Color face = new Color32(192,192,192,255);
        public bool selected;
        public bool inset;
        bool pressed, hover, focus;
        public void Refresh(bool active) { selected = active; SetVerticesDirty(); }
        public void OnPointerDown(PointerEventData e) { pressed = true; SetVerticesDirty(); }
        public void OnPointerUp(PointerEventData e) { pressed = false; SetVerticesDirty(); }
        public void OnPointerEnter(PointerEventData e) { hover = true; SetVerticesDirty(); }
        public void OnPointerExit(PointerEventData e) { hover = pressed = false; SetVerticesDirty(); }
        public void OnSelect(BaseEventData e) { focus = true; SetVerticesDirty(); }
        public void OnDeselect(BaseEventData e) { focus = false; SetVerticesDirty(); }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear(); var r = GetPixelAdjustedRect();
            bool down = inset || selected || pressed;
            Fill(vh, r, hover && !down ? Color.Lerp(face, Color.white, .10f) : face);
            Border(vh, r, down ? new Color32(64,64,64,255) : Color.white,
                down ? Color.white : new Color32(32,32,32,255));
            r = Shrink(r, 1);
            Border(vh, r, down ? new Color32(128,128,128,255) : new Color32(223,223,223,255),
                down ? new Color32(223,223,223,255) : new Color32(128,128,128,255));
            if (focus && raycastTarget)
            {
                r = Shrink(r, 4);
                for (float x = r.xMin; x < r.xMax; x += 3) { Fill(vh,new Rect(x,r.yMin,1,1),Color.black); Fill(vh,new Rect(x,r.yMax-1,1,1),Color.black); }
                for (float y = r.yMin; y < r.yMax; y += 3) { Fill(vh,new Rect(r.xMin,y,1,1),Color.black); Fill(vh,new Rect(r.xMax-1,y,1,1),Color.black); }
            }
        }
        static Rect Shrink(Rect r, float v) => new Rect(r.x+v,r.y+v,r.width-2*v,r.height-2*v);
        static void Border(UnityEngine.UI.VertexHelper v, Rect r, Color top, Color bottom)
        {
            Fill(v,new Rect(r.xMin,r.yMax-1,r.width,1),top);
            Fill(v,new Rect(r.xMin,r.yMin,1,r.height),top);
            Fill(v,new Rect(r.xMin+1,r.yMin,r.width-1,1),bottom);
            Fill(v,new Rect(r.xMax-1,r.yMin,1,r.height-1),bottom);
        }
        static void Fill(UnityEngine.UI.VertexHelper vh, Rect r, Color c)
        {
            int i=vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin,r.yMin),c,Vector2.zero);
            vh.AddVert(new Vector3(r.xMin,r.yMax),c,Vector2.zero);
            vh.AddVert(new Vector3(r.xMax,r.yMax),c,Vector2.zero);
            vh.AddVert(new Vector3(r.xMax,r.yMin),c,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2); vh.AddTriangle(i,i+2,i+3);
        }
    }
}
