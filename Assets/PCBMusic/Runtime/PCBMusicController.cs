using System;
using UnityEngine;

namespace AAR.PCBMusic
{
    [DisallowMultipleComponent]
    public sealed class PCBMusicController : MonoBehaviour
    {
        public PCBMusicProfile profile;
        public PCBInstrumentBank instruments;
        public Camera playerCamera;
        public bool musicEnabled=true;
        public bool freezeObservation;
        public PCBViewAnalyzer View {get;private set;}
        public PCBMusicComposer Composer {get;private set;}
        public PCBMusicProfile Settings {get;private set;}
        public bool Paused {get;private set;}
        public int NotesPlayed {get;private set;}
        public int ActiveVoices => scheduler==null?0:scheduler.Active;
        public int DroppedNotes => scheduler==null?0:scheduler.Dropped;
        public int LateNotes => scheduler==null?0:scheduler.Late;
        public float AnalysisMilliseconds {get;private set;}
        public int SoloRole {get;private set;}=-1;
        DSPMusicScheduler scheduler;
        double origin,nextStep,pauseStart,nextAnalysis;
        MusicState previousState;
        bool needsBind=true;
        readonly PCBMusicNote[] history=new PCBMusicNote[128];
        public int HistoryCount {get;private set;}
        public PCBMusicNote History(int chronologicalIndex)=>history[(HistoryCount-Math.Min(HistoryCount,128)+chronologicalIndex)%128];
        void OnEnable()
        {
            if(!Application.isPlaying)return;
            if(!profile)profile=Resources.Load<PCBMusicProfile>("PCBMusic/Default Music");
            if(!instruments)instruments=Resources.Load<PCBInstrumentBank>("PCBMusic/Instrument Bank");
            if(!profile||!instruments){Debug.LogWarning("PCB music needs a profile and instrument bank.",this);return;}
            Settings=Instantiate(profile);Settings.hideFlags=HideFlags.HideAndDontSave;
            Settings.volume=PlayerPrefs.GetFloat("AAR.Music.Volume",Settings.volume);
            musicEnabled=PlayerPrefs.GetInt("AAR.Music.Enabled",musicEnabled?1:0)!=0;
            View=new PCBViewAnalyzer();scheduler=new DSPMusicScheduler(transform,Settings.voicePool);
            PCBMusicSnapshot.Changed+=RequestBind;needsBind=true;
        }
        void RequestBind(){needsBind=true;}
        void Bind()
        {
            needsBind=false;var snapshot=GetComponentInParent<AAR.PCB.PCBFloorGenerator>()?.GetComponentInChildren<PCBMusicSnapshot>();
            if(!snapshot)snapshot=FindAnyObjectByType<PCBMusicSnapshot>();
            View.Bind(snapshot);scheduler.Retire();Composer=new PCBMusicComposer(Settings,snapshot?snapshot.boardSeed:0);
            origin=AudioSettings.dspTime;nextStep=origin+Settings.scheduleAhead;previousState=Composer.State;
        }
        void Update()
        {
            if(!Settings||scheduler==null)return;
            if(needsBind)Bind();if(!playerCamera)playerCamera=Camera.main;
            double now=AudioSettings.dspTime;
            bool pause=SettingsMenu.IsOpen||Time.timeScale<=0;
            if(pause!=Paused)
            {
                Paused=pause;
                if(pause){pauseStart=now;scheduler.Pause();}
                else{double elapsed=now-pauseStart;origin+=elapsed;nextStep+=elapsed;scheduler.Resume(elapsed);nextStep=Math.Max(nextStep,now+Settings.scheduleAhead);}
            }
            if(Paused)return;
            if(!freezeObservation&&now>=nextAnalysis)
            {
                var timer=System.Diagnostics.Stopwatch.GetTimestamp();
                float interval=1/Mathf.Clamp(Settings.analysisHz,2,20);View.Analyze(playerCamera,Settings,interval);nextAnalysis=now+interval;
                AnalysisMilliseconds=(float)((System.Diagnostics.Stopwatch.GetTimestamp()-timer)*1000.0/System.Diagnostics.Stopwatch.Frequency);
            }
            Composer.Advance(now-origin);
            if(previousState!=Composer.State)
            {if(Composer.State==MusicState.Silence||Composer.State==MusicState.Suspension)scheduler.Retire();previousState=Composer.State;}
            scheduler.Tick(musicEnabled?Mathf.Clamp01(Settings.volume):0,Time.unscaledDeltaTime,Settings.fadeTime);
            if(!musicEnabled||!View.Snapshot){scheduler.Retire();nextStep=now+Settings.scheduleAhead;return;}
            if(nextStep<now+.025)nextStep=now+Settings.scheduleAhead;
            if(nextStep<=now+Settings.scheduleAhead)
            {
                int n=Composer.Compose(View.selected,View.SelectedCount,nextStep);
                for(int i=0;i<n;i++)
                {
                    var note=Composer.notes[i];if(SoloRole>=0&&(int)note.role!=SoloRole)continue;
                    if(scheduler.Schedule(note,instruments)){history[HistoryCount++%128]=note;NotesPlayed++;}
                }
                nextStep+=Settings.BeatSeconds*.5;
            }
        }
        public void Force(MusicState state){Composer?.Force(state);if(state==MusicState.Silence||state==MusicState.Suspension)scheduler?.Retire();}
        public void Automatic(){Composer?.Automatic();}
        public void Solo(int role){SoloRole=SoloRole==role?-1:role;scheduler?.Retire();}
        public void SetVolume(float value){if(Settings)Settings.volume=Mathf.Clamp01(value);}
        public void SetEnabled(bool value){musicEnabled=value;if(!value)scheduler?.Retire();}
        public void SavePreferences()
        {
            if(Settings)PlayerPrefs.SetFloat("AAR.Music.Volume",Settings.volume);
            PlayerPrefs.SetInt("AAR.Music.Enabled",musicEnabled?1:0);PlayerPrefs.Save();
        }
        void OnDisable()
        {
            if(Application.isPlaying&&Settings)SavePreferences();
            PCBMusicSnapshot.Changed-=RequestBind;scheduler?.Dispose();scheduler=null;View=null;Composer=null;
            if(Settings){Destroy(Settings);Settings=null;}Paused=false;
        }
        void OnApplicationQuit(){SavePreferences();}
    }
}
