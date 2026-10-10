using System;
using UnityEngine;

namespace AAR.PCBMusic
{
    [Serializable]
    public struct PCBInstrument
    {
        public MusicRole role;
        public AudioClip clip;
        public int variant;
    }
    [CreateAssetMenu(menuName="AAR/Music/PCB Instrument Bank")]
    public sealed class PCBInstrumentBank : ScriptableObject
    {
        public PCBInstrument[] instruments=Array.Empty<PCBInstrument>();
        public AudioClip Find(MusicRole role,int variant)
        {
            AudioClip fallback=null;
            for(int i=0;i<instruments.Length;i++)if(instruments[i].role==role)
            {if(instruments[i].variant==variant)return instruments[i].clip;if(!fallback)fallback=instruments[i].clip;}
            return fallback;
        }
    }

    // Offline timbre authoring only; no waveform computation on the audio thread.
    public static class PCBInstrumentSynth
    {
        public const int SampleRate=48000;
        public static float[] Render(MusicRole role,int variant)
        {
            double length=role==MusicRole.Capacitor?4.5:role==MusicRole.Passive?.35:role==MusicRole.RAM?1.5:2.2;
            int root=role==MusicRole.Capacitor?33:role==MusicRole.RAM?45:role==MusicRole.Passive?64:54;
            double f=440*Math.Pow(2,(root-69)/12.0), phase=0;
            float[] data=new float[(int)(length*SampleRate)];uint noise=(uint)(1984+variant*31+(int)role*997);double low=0;
            double peak=0;
            for(int i=0;i<data.Length;i++)
            {
                double t=(double)i/SampleRate;phase+=2*Math.PI*f/SampleRate;
                noise=PCBMusicComposer.Hash(noise);double white=(noise&0xffff)/32768.0-1;
                low+=.06*(white-low);
                double envelope=(1-Math.Exp(-t/(role==MusicRole.Capacitor?.09:.012)))*Math.Exp(-t/(role==MusicRole.Capacitor?1.15:role==MusicRole.Passive?.055:.48));
                double tone;
                if(role==MusicRole.Capacitor)tone=Math.Sin(phase)+.24*Math.Sin(phase*1.006+variant*.4)+.13*Math.Sin(phase*2.001);
                else if(role==MusicRole.RAM)tone=Math.Sin(phase)+.28*Math.Sin(phase*2+.7*Math.Sin(t*4))+.1*Math.Sin(phase*3);
                else if(role==MusicRole.Passive)tone=low*2+Math.Sin(phase*(1+variant*.2))*Math.Exp(-t*35)*.4;
                else tone=Math.Sin(phase+(.7+variant*.25)*Math.Sin(phase*(2.003+variant*.17))*Math.Exp(-t*3))+.16*Math.Sin(phase*3.01);
                double fade=Math.Min(1,(length-t)/.05);
                double sample=tone*envelope*fade;data[i]=(float)sample;peak=Math.Max(peak,Math.Abs(sample));
            }
            float scale=peak>0?(float)(.72/peak):1;for(int i=0;i<data.Length;i++)data[i]*=scale;
            return data;
        }
    }
}
