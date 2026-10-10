using UnityEngine;

namespace AAR.RetroDither
{
    // Shader uniforms affect existing, instantiated and newly baked materials alike.
    // Only the common pattern controls are shared; no material assets are rewritten.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GlobalDitherController : MonoBehaviour
    {
        public GlobalDitherProfile profile;
        public static GlobalDitherController Instance { get; private set; }
        GlobalDitherProfile runtime;
        public GlobalDitherProfile Settings => Application.isPlaying ? runtime : profile;
        static readonly int Grid=Shader.PropertyToID("_RetroDitherSharedGrid");
        static readonly int Sizing=Shader.PropertyToID("_RetroDitherSharedSizing");
        static readonly int Filtering=Shader.PropertyToID("_RetroDitherSharedFilter");

        void OnEnable()
        {
            if(Instance&&Instance!=this){Debug.LogWarning("Only one shared dither controller can be active.",this);enabled=false;return;}
            Instance=this;
            if(!profile)profile=Resources.Load<GlobalDitherProfile>("RetroDither/Global Settings");
            if(Application.isPlaying&&profile)
            {
                runtime=Instantiate(profile);runtime.hideFlags=HideFlags.HideAndDontSave;
                LoadPreferences();
            }
            Apply();
        }
        void LateUpdate(){if(Instance==this)Apply();}
        void OnDisable()
        {
            if(Instance==this){Shader.SetGlobalVector(Grid,Vector4.zero);Instance=null;}
            if(runtime){if(Application.isPlaying)Destroy(runtime);else DestroyImmediate(runtime);runtime=null;}
        }
        public static GlobalDitherController Ensure()
        {
            if(Instance)return Instance;
            var existing=FindAnyObjectByType<GlobalDitherController>();
            if(existing)return existing;
            return new GameObject("Shared Dither Settings").AddComponent<GlobalDitherController>();
        }
        public void Apply()
        {
            var p=Settings;if(!p){Shader.SetGlobalVector(Grid,Vector4.zero);return;}
            Shader.SetGlobalVector(Grid,new Vector4(Mathf.Clamp(p.pixelSize,1,12),Mathf.Clamp01(p.strength),(int)p.matrix,1));
            Shader.SetGlobalVector(Sizing,new Vector4((int)p.sizing,Mathf.Max(180,p.referenceHeight),0,0));
            Shader.SetGlobalVector(Filtering,new Vector4(Mathf.Clamp(p.detailStability,0,2),Mathf.Clamp01(p.cellLighting),p.preserveDither?1:0,Mathf.Clamp(p.specularAA,0,2)));
        }
        public void SavePreferences()
        {
            if(!Application.isPlaying||!runtime)return;
            PlayerPrefs.SetInt("AAR.Dither.PixelSize",runtime.pixelSize);
            PlayerPrefs.SetFloat("AAR.Dither.Strength",runtime.strength);
            PlayerPrefs.SetInt("AAR.Dither.Matrix",(int)runtime.matrix);
            PlayerPrefs.SetInt("AAR.Dither.Sizing",(int)runtime.sizing);
            PlayerPrefs.SetInt("AAR.Dither.Reference",runtime.referenceHeight);
            PlayerPrefs.SetFloat("AAR.Dither.Specular",runtime.specularAA);
            PlayerPrefs.SetFloat("AAR.Dither.Detail",runtime.detailStability);
            PlayerPrefs.SetFloat("AAR.Dither.CellLighting",runtime.cellLighting);
            PlayerPrefs.SetInt("AAR.Dither.Crisp",runtime.preserveDither?1:0);
            PlayerPrefs.Save();
        }
        void LoadPreferences()
        {
            runtime.pixelSize=Mathf.Clamp(PlayerPrefs.GetInt("AAR.Dither.PixelSize",runtime.pixelSize),1,12);
            runtime.strength=Mathf.Clamp01(PlayerPrefs.GetFloat("AAR.Dither.Strength",runtime.strength));
            int matrix=PlayerPrefs.GetInt("AAR.Dither.Matrix",(int)runtime.matrix);
            runtime.matrix=matrix==2?GlobalDitherProfile.Matrix.Bayer2:matrix==8?GlobalDitherProfile.Matrix.Bayer8:GlobalDitherProfile.Matrix.Bayer4;
            runtime.sizing=(GlobalDitherProfile.Sizing)Mathf.Clamp(PlayerPrefs.GetInt("AAR.Dither.Sizing",(int)runtime.sizing),0,1);
            runtime.referenceHeight=Mathf.Clamp(PlayerPrefs.GetInt("AAR.Dither.Reference",runtime.referenceHeight),180,2160);
            runtime.specularAA=Mathf.Clamp(PlayerPrefs.GetFloat("AAR.Dither.Specular",runtime.specularAA),0,2);
            runtime.detailStability=Mathf.Clamp(PlayerPrefs.GetFloat("AAR.Dither.Detail",runtime.detailStability),0,2);
            runtime.cellLighting=Mathf.Clamp01(PlayerPrefs.GetFloat("AAR.Dither.CellLighting",runtime.cellLighting));
            runtime.preserveDither=PlayerPrefs.GetInt("AAR.Dither.Crisp",runtime.preserveDither?1:0)!=0;
        }
        public void ResetPreferences()
        {
            if(!runtime||!profile)return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(profile),runtime);
            Apply();SavePreferences();
        }
    }
}
