using System;
using UnityEngine;

namespace AAR.PCBMusic
{
    public struct PCBMusicNote
    {
        public int partId, variant, semitones;
        public MusicRole role;
        public float gain, pan;
        public double time;
    }

    // Pure musical decisions, also used by offline auditions and deterministic replays.
    public sealed class PCBMusicComposer
    {
        public MusicState State {get;private set;}=MusicState.Veille;
        public double StateRemaining => Math.Max(0,nextState-now);
        public int Step {get;private set;}
        public bool IsForced {get;private set;}
        public readonly PCBMusicNote[] notes=new PCBMusicNote[4];
        readonly PCBMusicProfile profile;
        uint random;
        double now,nextState;
        public PCBMusicComposer(PCBMusicProfile profile,int boardSeed)
        {this.profile=profile;random=Hash((uint)(profile.musicSeed^boardSeed));nextState=Duration(MusicState.Veille);}
        public static uint Hash(uint x){unchecked{x^=x>>16;x*=0x7feb352d;x^=x>>15;x*=0x846ca68b;x^=x>>16;return x==0?1:x;}}
        float Draw(){random=Hash(random);return (random&0xffffff)/16777216f;}
        double Duration(MusicState state)
        {
            Vector2 span=state==MusicState.Silence?profile.silenceDuration:state==MusicState.Veille?profile.veilleDuration:state==MusicState.Pression?profile.pressureDuration:state==MusicState.Suspension?profile.suspensionDuration:profile.releaseDuration;
            return Mathf.Lerp(Mathf.Max(.5f,span.x),Mathf.Max(.5f,Mathf.Max(span.x,span.y)),Draw());
        }
        public void Force(MusicState state){State=state;IsForced=true;}
        public void Automatic(){IsForced=false;nextState=now+Duration(State);}
        public void Advance(double elapsed)
        {
            now=elapsed;if(IsForced||now<nextState)return;
            float chance=Draw();
            switch(State)
            {
                case MusicState.Silence: State=MusicState.Veille;break;
                case MusicState.Veille: State=chance<profile.silenceChance?MusicState.Silence:MusicState.Pression;break;
                case MusicState.Pression: State=chance<.7f?MusicState.Suspension:MusicState.Retombee;break;
                case MusicState.Suspension: State=chance<.45f?MusicState.Silence:MusicState.Retombee;break;
                default: State=chance<profile.silenceChance?MusicState.Silence:MusicState.Veille;break;
            }
            nextState=now+Duration(State);
        }
        public int Compose(MusicAttention[] attention,int count,double time)
        {
            int step=Step++;if(State==MusicState.Silence||State==MusicState.Suspension)return 0;
            float density=profile.activity*(State==MusicState.Pression?1.5f:State==MusicState.Retombee?.5f:.8f);
            int emitted=0;
            for(int i=0;i<Math.Min(count,profile.maxContributors);i++)
            {
                var a=attention[i];bool eligible;
                if(a.role==MusicRole.Chip)eligible=step%8==0||step%8==3||step%8==5;
                else if(a.role==MusicRole.RAM)eligible=a.connectedResponse?step%8==2||step%8==6:step%2==0;
                else if(a.role==MusicRole.Capacitor)eligible=step%8==0;
                else eligible=step%4==2;
                if(!eligible||Draw()>Mathf.Min(.95f,density*(a.role==MusicRole.Capacitor?1.5f:1)))continue;
                uint identity=Hash((uint)(profile.musicSeed^a.id*397^a.group*31));
                int[] palette=profile.intervals;int n=palette==null?0:palette.Length;
                int interval=n==0?0:palette[(int)((identity+(uint)(step/8+step%8))%(uint)n)];
                if(a.role==MusicRole.Capacitor)interval=0;
                else if(a.role==MusicRole.Passive)interval=0;
                else if(State==MusicState.Pression&&step%8==5&&Draw()<profile.tension*.5f)interval=(identity&1)==0?1:6;
                float strength=Mathf.Lerp(.42f,1,Mathf.Clamp01(a.weight));
                float roleGain=a.role==MusicRole.Capacitor?.42f:a.role==MusicRole.RAM?.32f:a.role==MusicRole.Passive?.2f:.5f;
                notes[emitted++]=new PCBMusicNote {partId=a.id,role=a.role,variant=(int)(identity%3),semitones=interval,
                    gain=strength*roleGain*(State==MusicState.Retombee?.65f:1),pan=a.pan,time=time};
            }
            return emitted;
        }
    }
}
