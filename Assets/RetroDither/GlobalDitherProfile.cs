using UnityEngine;

namespace AAR.RetroDither
{
    [CreateAssetMenu(menuName="AAR/Shared Dither Settings")]
    public sealed class GlobalDitherProfile : ScriptableObject
    {
        [Header("Screen pattern")]
        [Range(1,12)] public int pixelSize=1;
        [Range(0,1)] public float strength=.8f;
        public enum Matrix { Bayer2=2, Bayer4=4, Bayer8=8 }
        public Matrix matrix=Matrix.Bayer4;
        public enum Sizing { OutputPixels, ReferenceHeight }
        public Sizing sizing=Sizing.OutputPixels;
        [Range(180,2160)] public int referenceHeight=720;
        [Header("Filtering")]
        [Range(0,2)] public float detailStability=.75f;
        [Range(0,1)] public float cellLighting=.6f;
        public bool preserveDither=true;
        [Range(0,2)] public float specularAA=1;
    }
}
