using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AAR.PCB;

namespace AAR.PCBMusic.Editor
{
    public static class PCBMusicSetup
    {
        const string ProfilePath="Assets/Resources/PCBMusic/Default Music.asset";
        const string BankPath="Assets/Resources/PCBMusic/Instrument Bank.asset";
        [MenuItem("Tools/AAR/Install PCB Music Prototype")]
        public static void Install()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Install outside Play mode.");
            Folder("Assets/Resources");Folder("Assets/Resources/PCBMusic");Folder("Assets/PCBMusic/Audio");
            var profile=AssetDatabase.LoadAssetAtPath<PCBMusicProfile>(ProfilePath);
            if(!profile){profile=ScriptableObject.CreateInstance<PCBMusicProfile>();AssetDatabase.CreateAsset(profile,ProfilePath);}
            var bank=AssetDatabase.LoadAssetAtPath<PCBInstrumentBank>(BankPath);
            if(!bank)
            {
                bank=ScriptableObject.CreateInstance<PCBInstrumentBank>();var entries=new List<PCBInstrument>();
                for(int role=0;role<4;role++)for(int variant=0;variant<3;variant++)
                {
                    string path="Assets/PCBMusic/Audio/"+(MusicRole)role+"-"+variant+".wav";
                    WriteWav(path,PCBInstrumentSynth.Render((MusicRole)role,variant),1);
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                    var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;
                    settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
                    settings.preloadAudioData=true;importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.SaveAndReimport();
                    entries.Add(new PCBInstrument{role=(MusicRole)role,variant=variant,clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path)});
                }
                bank.instruments=entries.ToArray();AssetDatabase.CreateAsset(bank,BankPath);
            }
            var floor=UnityEngine.Object.FindAnyObjectByType<PCBFloorGenerator>();if(!floor)throw new InvalidOperationException("Open a generated PCB scene first.");
            EnsureSnapshot(floor);
            var controller=floor.GetComponentInChildren<PCBMusicController>();
            if(!controller)
            {
                var root=new GameObject("PCB Music");root.transform.SetParent(floor.transform,false);Undo.RegisterCreatedObjectUndo(root,"Install PCB music");
                controller=root.AddComponent<PCBMusicController>();root.AddComponent<PCBMusicDebugPanel>();
            }
            Undo.RecordObject(controller,"Configure PCB music");controller.profile=profile;controller.instruments=bank;controller.playerCamera=Camera.main;
            if(!UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>())
            {
                var go=new GameObject("EventSystem",typeof(UnityEngine.EventSystems.EventSystem),typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(go,"Create music UI events");go.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
            }
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(floor.gameObject.scene);
        }
        public static PCBMusicSnapshot EnsureSnapshot(PCBFloorGenerator floor)
        {
            var existing=floor.GetComponentInChildren<PCBMusicSnapshot>();if(existing)return existing;
            if(floor.routingStyle!=PCBFloorGenerator.RoutingStyle.GlobalBoard)throw new InvalidOperationException("The music prototype requires Global Board mode.");
            var root=floor.transform.Find("Generated PCB Tiles");var components=root?root.Find("Global Components"):null;
            if(!components)throw new InvalidOperationException("Generate the PCB before installing music.");
            var plan=floor.LastGlobalPlan??PCBGlobalBoard.Create(floor);
            // Migration adds metadata only. Validate against existing logical roots, including merged SMD parents.
            var roots=new List<Transform>();foreach(Transform child in components)if(child.name.StartsWith("Group ",StringComparison.Ordinal))roots.Add(child);
            if(roots.Count!=plan.parts.Count)throw new InvalidOperationException("Current PCB settings do not match the saved geometry. Regenerate explicitly before installing music.");
            for(int i=0;i<roots.Count;i++)
            {
                var part=plan.parts[i];var p=roots[i].localPosition;
                if(Mathf.Abs(p.x-part.position.x)>.01f||Mathf.Abs(p.z-part.position.y)>.01f)
                    throw new InvalidOperationException("PCB metadata migration cannot match component "+i+". Regenerate explicitly first.");
            }
            var snapshot=PCBMusicSnapshot.Capture(root,plan,floor);EditorUtility.SetDirty(snapshot);return snapshot;
        }
        [MenuItem("Tools/AAR/Open PCB Music Profile")]
        public static void OpenProfile()
        {
            var controller=UnityEngine.Object.FindAnyObjectByType<PCBMusicController>();
            Selection.activeObject=Application.isPlaying&&controller&&controller.Settings?controller.Settings:AssetDatabase.LoadAssetAtPath<PCBMusicProfile>(ProfilePath);
        }
        [MenuItem("Tools/AAR/Export PCB Music Auditions")]
        public static void ExportAuditions()
        {
            var source=AssetDatabase.LoadAssetAtPath<PCBMusicProfile>(ProfilePath);if(!source)throw new InvalidOperationException("Install PCB music first.");
            string output=Path.Combine(Application.dataPath,"../Temp/PCBMusicAuditions");Directory.CreateDirectory(output);
            for(int audition=0;audition<3;audition++)
            {
                var profile=UnityEngine.Object.Instantiate(source);profile.musicSeed=1984+audition*37;profile.activity=.85f;profile.volume=.45f;
                var composer=new PCBMusicComposer(profile,1984);
                var attention=new[]{new MusicAttention{id=1,group=0,role=MusicRole.Chip,weight=.8f,pan=-.2f},new MusicAttention{id=3,group=1,role=MusicRole.RAM,weight=.55f,pan=.2f},new MusicAttention{id=6,group=0,role=MusicRole.Capacitor,weight=.65f,pan=0}};
                var samples=new float[4,3][];for(int role=0;role<4;role++)for(int v=0;v<3;v++)samples[role,v]=PCBInstrumentSynth.Render((MusicRole)role,v);
                const int seconds=32;var mix=new float[seconds*PCBInstrumentSynth.SampleRate*2];
                int steps=(int)(seconds/(profile.BeatSeconds*.5));
                for(int step=0;step<steps;step++)
                {
                    double t=step*profile.BeatSeconds*.5;
                    var state=t>=14&&t<21?MusicState.Silence:t<8?MusicState.Veille:t<14?MusicState.Pression:MusicState.Retombee;
                    composer.Force(state);int count=composer.Compose(attention,attention.Length,t);
                    for(int k=0;k<count;k++)
                    {
                        var note=composer.notes[k];if(audition==0&&note.role==MusicRole.RAM)continue;if(audition==1&&note.role==MusicRole.Capacitor)continue;
                        var data=samples[(int)note.role,note.variant];double pitch=Math.Pow(2,note.semitones/12.0);
                        int begin=(int)(t*PCBInstrumentSynth.SampleRate),frames=(int)(data.Length/pitch);
                        for(int frame=0;frame<frames&&begin+frame<seconds*PCBInstrumentSynth.SampleRate;frame++)
                        {
                            double time=t+(double)frame/PCBInstrumentSynth.SampleRate;
                            // Auditions share the live director's explicit silence, including bounded tail retirement.
                            float gate=time<14?1:time<14.15?(float)((14.15-time)/.15):time<21?0:1;
                            double at=frame*pitch;int index=(int)at;if(index+1>=data.Length)break;
                            float value=Mathf.Lerp(data[index],data[index+1],(float)(at-index))*note.gain*profile.volume*gate;
                            int dst=(begin+frame)*2;mix[dst]+=value*Mathf.Sqrt((1-note.pan)*.5f);mix[dst+1]+=value*Mathf.Sqrt((1+note.pan)*.5f);
                        }
                    }
                }
                for(int i=0;i<mix.Length;i++)mix[i]=(float)Math.Tanh(mix[i]);
                WriteWav(Path.Combine(output,audition==0?"01-Resonance.wav":audition==1?"02-Pulses.wav":"03-Hybrid.wav"),mix,2);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
        static void Folder(string path)
        {if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
        [MenuItem("Tools/AAR/Record PCB Music/Start 32 seconds")]
        public static void StartRecording()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Record in Play mode.");
            var listener=UnityEngine.Object.FindAnyObjectByType<AudioListener>();if(!listener)throw new InvalidOperationException("No AudioListener.");
            var recorder=listener.GetComponent<PCBMusicRecording>();if(!recorder)recorder=listener.gameObject.AddComponent<PCBMusicRecording>();recorder.Begin(32);
        }
        [MenuItem("Tools/AAR/Record PCB Music/Save recording")]
        public static void SaveRecording()
        {
            var recorder=UnityEngine.Object.FindAnyObjectByType<PCBMusicRecording>();if(!recorder)throw new InvalidOperationException("Start a recording first.");
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/PCBMusicAuditions/Live-PCB.wav"));
            var data=recorder.Finish();if(data.Length==0||recorder.Channels==0)throw new InvalidOperationException("No audio captured yet.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));WriteWav(path,data,recorder.Channels,recorder.SampleRate);UnityEngine.Object.Destroy(recorder);
            Debug.Log("PCB listening take saved to "+path);
        }
        static void WriteWav(string path,float[] samples,int channels,int sampleRate=PCBInstrumentSynth.SampleRate)
        {
            using(var stream=new BinaryWriter(File.Create(path)))
            {
                int bytes=samples.Length*2;stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));stream.Write(36+bytes);stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));stream.Write(16);stream.Write((short)1);stream.Write((short)channels);
                stream.Write(sampleRate);stream.Write(sampleRate*channels*2);stream.Write((short)(channels*2));stream.Write((short)16);stream.Write(System.Text.Encoding.ASCII.GetBytes("data"));stream.Write(bytes);
                foreach(float sample in samples)stream.Write((short)(Mathf.Clamp(sample,-1,1)*32767));
            }
        }
    }
}
