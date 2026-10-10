using System;
using System.Threading;
using UnityEngine;

namespace AAR.PCBMusic
{
    // Optional listener tap for auditions. Buffer allocation happens before the audio callback.
    public sealed class PCBMusicRecording : MonoBehaviour
    {
        float[] buffer;
        int written,channels;
        volatile bool recording;
        public bool Recording => recording;
        public int SampleCount => Volatile.Read(ref written);
        public int Channels => Volatile.Read(ref channels);
        public int SampleRate {get;private set;}
        public void Begin(float seconds)
        {
            if(recording)throw new InvalidOperationException("Finish the current recording first.");
            SampleRate=AudioSettings.outputSampleRate;var mode=AudioSettings.GetConfiguration().speakerMode;
            int count=mode==AudioSpeakerMode.Mono?1:mode==AudioSpeakerMode.Quad?4:mode==AudioSpeakerMode.Surround?5:mode==AudioSpeakerMode.Mode5point1?6:mode==AudioSpeakerMode.Mode7point1?8:2;
            buffer=new float[(int)(Mathf.Clamp(seconds,1,60)*SampleRate*count)];written=channels=0;recording=true;
        }
        void OnAudioFilterRead(float[] data,int count)
        {
            if(!recording||buffer==null)return;
            Volatile.Write(ref channels,count);
            int cursor=written,length=Math.Min(data.Length,buffer.Length-cursor);
            Array.Copy(data,0,buffer,cursor,length);Volatile.Write(ref written,cursor+length);
            if(cursor+length>=buffer.Length)recording=false;
        }
        public float[] Finish()
        {
            recording=false;int length=SampleCount;var result=new float[length];
            if(length>0)Array.Copy(buffer,result,length);return result;
        }
    }
}
