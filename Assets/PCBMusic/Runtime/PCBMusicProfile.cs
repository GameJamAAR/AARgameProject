using UnityEngine;

namespace AAR.PCBMusic
{
    public enum MusicState { Silence, Veille, Pression, Suspension, Retombee }
    public enum MusicRole { Chip, Capacitor, RAM, Passive }

    [CreateAssetMenu(menuName = "AAR/Music/PCB Music Profile")]
    public sealed class PCBMusicProfile : ScriptableObject
    {
        [Header("Composition")]
        public int musicSeed = 1984;
        [Range(40,100)] public float tempo = 68;
        [Range(0,1)] public float tension = .65f;
        [Range(0,1)] public float activity = .48f;
        [Range(0,1)] public float silenceChance = .32f;
        [Range(0,1)] public float volume = .32f;
        [Range(1,4)] public int maxContributors = 3;
        public bool includePassives;
        [Tooltip("Semitone intervals above each instrument's root; 1 and 6 are reserved for tension.")]
        public int[] intervals = { 0, 2, 3, 7, 10 };
        [Header("Form (seconds)")]
        public Vector2 silenceDuration = new Vector2(3,10);
        public Vector2 veilleDuration = new Vector2(8,18);
        public Vector2 pressureDuration = new Vector2(6,14);
        public Vector2 suspensionDuration = new Vector2(3,7);
        public Vector2 releaseDuration = new Vector2(5,10);
        [Header("Attention")]
        [Range(2,20)] public float analysisHz = 10;
        [Min(5)] public float maxDistance = 42;
        [Range(.1f,2)] public float attentionAttack = .5f;
        [Range(.2f,5)] public float attentionRelease = 1.6f;
        [Range(0,2)] public float centerBias = .8f;
        [Range(4,64)] public int candidateLimit = 24;
        public bool occlusion = true;
        [Header("Audio")]
        [Range(.1f,.5f)] public float scheduleAhead = .2f;
        [Range(.02f,.5f)] public float fadeTime = .12f;
        [Range(8,32)] public int voicePool = 20;
        public float BeatSeconds => 60f / Mathf.Clamp(tempo,40,100);
    }
}
