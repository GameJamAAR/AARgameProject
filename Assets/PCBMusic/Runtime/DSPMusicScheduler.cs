using UnityEngine;

namespace AAR.PCBMusic
{
    public sealed class DSPMusicScheduler
    {
        sealed class Voice {public AudioSource source;public double start,end;public float gain;public bool retiring,paused;}
        readonly Voice[] voices;
        public int Dropped {get;private set;}
        public int Late {get;private set;}
        public int Active {get;private set;}
        public DSPMusicScheduler(Transform parent,int count)
        {
            voices=new Voice[Mathf.Clamp(count,8,32)];
            for(int i=0;i<voices.Length;i++)
            {
                var go=new GameObject("Music Voice "+i);go.transform.SetParent(parent,false);
                var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.loop=false;source.priority=80;
                voices[i]=new Voice{source=source};
            }
        }
        public bool Schedule(PCBMusicNote note,PCBInstrumentBank bank)
        {
            var clip=bank.Find(note.role,note.variant);if(!clip)return false;
            double now=AudioSettings.dspTime;if(note.time<now+.015){Late++;return false;}
            for(int i=0;i<voices.Length;i++)
            {
                var v=voices[i];if(v.end>now||v.paused)continue;
                v.source.Stop();v.source.clip=clip;v.source.pitch=Mathf.Pow(2,note.semitones/12f);
                v.source.panStereo=Mathf.Clamp(note.pan,-.35f,.35f);v.gain=note.gain;v.retiring=false;
                v.source.volume=0;v.start=note.time;v.end=note.time+clip.length/v.source.pitch;
                v.source.PlayScheduled(note.time);return true;
            }
            Dropped++;return false;
        }
        public void Tick(float volume,float dt,float fade)
        {
            Active=0;double now=AudioSettings.dspTime;float gainSum=0;
            foreach(var v in voices)if(v.end>now&&!v.retiring)gainSum+=v.gain*volume;
            float budget=Mathf.Min(1,.8f/Mathf.Max(.001f,gainSum));
            foreach(var v in voices)
            {
                if(v.end<=now&&!v.paused)continue;Active++;
                float target=v.retiring?0:v.gain*volume*budget;
                v.source.volume=Mathf.MoveTowards(v.source.volume,target,dt/Mathf.Max(.02f,fade));
                if(v.retiring&&v.source.volume<=.0001f){v.source.Stop();v.end=0;v.paused=false;}
            }
            float actual=0;foreach(var v in voices)if(v.end>now)actual+=v.source.volume;
            if(actual>.8f)foreach(var v in voices)if(v.end>now)v.source.volume*=.8f/actual;
        }
        public void Retire()
        {
            double now=AudioSettings.dspTime;
            foreach(var v in voices)
            {
                if(v.start>now){v.source.Stop();v.end=0;v.paused=false;}
                else v.retiring=true;
            }
        }
        public void Pause()
        {
            double now=AudioSettings.dspTime;
            foreach(var v in voices)if(v.end>now)
            {
                if(v.start>now){v.source.Stop();v.end=0;}
                else{v.source.Pause();v.paused=true;}
            }
        }
        public void Resume(double duration)
        {foreach(var v in voices)if(v.paused){v.start+=duration;v.end+=duration;v.source.UnPause();v.paused=false;}}
        public void Dispose()
        {foreach(var v in voices)if(v.source){v.source.Stop();Object.Destroy(v.source.gameObject);}}
    }
}
