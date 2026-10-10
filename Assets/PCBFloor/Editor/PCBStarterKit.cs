using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AAR.PCB.Editor
{
    public static class PCBStarterKit
    {
        public const string Root = "Assets/PCBFloor/StarterKit";
        public const string LibraryPath = Root + "/PCB Starter Library.asset";

        [MenuItem("Tools/AAR/PCB/Create or Rebuild Starter Kit")]
        public static void BuildDefault() { Build(); }

        public static PCBTileLibrary Build()
        {
            Directory.CreateDirectory(Root + "/Meshes");
            Directory.CreateDirectory(Root + "/Tiles");
            Directory.CreateDirectory(Root + "/Materials");
            AssetDatabase.Refresh();
            var library = AssetDatabase.LoadAssetAtPath<PCBTileLibrary>(LibraryPath);
            if (!library)
            { library = ScriptableObject.CreateInstance<PCBTileLibrary>(); AssetDatabase.CreateAsset(library, LibraryPath); }
            library.surfaceMaterial = library.surfaceMaterial ? library.surfaceMaterial : MakeMaterial("Board", true, .0f, .12f);
            library.copperMaterial = library.copperMaterial ? library.copperMaterial : MakeMaterial("Traces", false, -.32f, .2f);
            library.bodyMaterial = library.bodyMaterial ? library.bodyMaterial : MakeMaterial("Chip Bodies", false, -.5f, .18f);
            library.contactMaterial = library.contactMaterial ? library.contactMaterial : MakeMaterial("Contacts", false, -.08f, .65f);
            if (!library.legendMaterial)
            {
                library.legendMaterial = MakeMaterial("Silkscreen", true, 0, 0);
                library.legendMaterial.SetColor("_GroundColor", new Color(.38f,.40f,.39f));
                EditorUtility.SetDirty(library.legendMaterial);
            }
            int[] masks = { 0, 1, 5, 3, 7, 15 };
            string[] names = { "Blank", "End", "Straight", "Corner", "Tee", "Cross" };
            var entries = new List<PCBTileLibrary.Entry>();
            for (int shape = 0; shape < masks.Length; shape++)
                for (int variant = 0; variant < (library.controllerModules ? Mathf.Clamp(library.starterVariantCount,6,16) : 3); variant++)
                {
                    string name = names[shape] + " " + (char)('A' + variant);
                    var tile = new GameObject(name);
                    try
                    {
                        var surface = new Geometry();
                        surface.Box(new Vector3(0, -.025f, 0), new Vector3(1, .05f, 1));
                        AddGroup(tile, "Surface", surface, library.surfaceMaterial, name);
                        var traces = new Geometry();
                        var pads = new Geometry();
                        var bodies = new Geometry();
                        var pins = new Geometry();
                        var legend = new Geometry();
                        BuildCircuit(traces, pads, bodies, pins, legend, masks[shape], variant, library);
                        if(library.controllerModules){var composition=new GameObject("Central "+CentralRecipe(variant,library));composition.transform.SetParent(tile.transform,false);}
                        float side=variant==1 ? 1 : -1;
                        int pinCount=variant==2 ? 5 : 4;
                        float middlePin=(pinCount-1)/2;
                        float footprint=Mathf.Clamp(library.chipFootprint,.12f,.28f);
                        float chipZ=variant==0 ? -.25f : variant==1 ? -.27f : -.30f;
                        var port=new GameObject("Network Port").transform;
                        port.SetParent(tile.transform,false);
                        port.localPosition=library.controllerModules
                            ? new Vector3(ControllerCenter(variant,library).x,0,ControllerCenter(variant,library).y-ControllerSize(variant,library).y*.5f-.033f)
                            : new Vector3(side*(.29f-footprint*.34f-.077f),0,
                                chipZ+(middlePin-(pinCount-1)*.5f)*(footprint/(pinCount+1)+.008f));
                        AddGroup(tile, "Traces", traces, library.copperMaterial, name);
                        AddGroup(tile, "Pads", pads, library.contactMaterial, name);
                        AddGroup(tile, "Components", bodies, library.bodyMaterial, name);
                        AddGroup(tile, "Pins", pins, library.contactMaterial, name);
                        AddGroup(tile, "Markings", legend, library.legendMaterial, name);
                        var prefab = PrefabUtility.SaveAsPrefabAsset(tile, Root + "/Tiles/" + name + ".prefab");
                        entries.Add(new PCBTileLibrary.Entry { prefab = prefab, sockets = masks[shape], weight = variant == 0 ? 1 : .8f });
                    }
                    finally { Object.DestroyImmediate(tile); }
                }
            library.tiles = entries.ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        static void BuildCircuit(Geometry traces, Geometry pads, Geometry bodies, Geometry pins, Geometry legend,
            int mask, int variant, PCBTileLibrary kit)
        {
            if(kit.controllerModules)
            {
                BuildController(traces,pads,bodies,pins,legend,mask,variant,kit);
                return;
            }
            float width = Mathf.Clamp(kit.traceWidth, .012f, .035f);
            float spacing = Mathf.Clamp(kit.busSpacing, .045f, .085f);
            float via = Mathf.Clamp(kit.padRadius, .02f, .04f);
            const float height = .012f;
            // Vary the interior routing, while reserving the last .07 at each edge
            // for the invariant socket. This removes long tile-center grid lines.
            Vector2 junction = variant == 0 ? new Vector2(.045f,.025f)
                : variant == 1 ? new Vector2(-.05f,.06f) : new Vector2(.06f,-.035f);
            float corner = variant == 0 ? .10f : variant == 1 ? .21f : .29f;
            float jog = variant == 0 ? .075f : variant == 1 ? -.08f : .04f;
            // Three identical rails at every socket. The center rail joins the graph;
            // companion rails are independent signals with local via terminations.
            for (int lane = -1; lane <= 1; lane++)
            {
                float offset = lane * spacing;
                if (mask == 3)
                    traces.Route(new[] {new Vector2(offset,.5f),new Vector2(offset,corner+offset),
                        new Vector2(corner+offset,offset),new Vector2(.5f,offset)},width,height);
                else if (mask == 5)
                    traces.Route(new[] {new Vector2(offset,-.5f),new Vector2(offset,-.33f),
                        new Vector2(offset+jog,-.33f+Mathf.Abs(jog)),new Vector2(offset+jog,.20f-Mathf.Abs(jog)),
                        new Vector2(offset,.20f),new Vector2(offset,.5f)},width,height);
                else
                    for (int d = 0; d < 4; d++) if ((mask & (1 << d)) != 0)
                    {
                        var direction=PCBLayout.Directions[d];
                        var forward=new Vector2(direction.x,direction.y);
                        // Symmetric port bundle survives every quarter turn.
                        var lateral=new Vector2(-forward.y,forward.x)*offset;
                        var end=junction+lateral+forward*(lane==0 ? 0 : .13f);
                        var start=forward*.5f+lateral;
                        // Orthogonal lead-in and a 45-degree chamfer reach an offset junction.
                        float transverse=Vector2.Dot(junction,new Vector2(-forward.y,forward.x));
                        var bend=forward*.30f+lateral;
                        var shifted=bend+new Vector2(-forward.y,forward.x)*transverse-forward*Mathf.Abs(transverse);
                        traces.Route(new[] {start,bend,shifted,end},width,height);
                        if(lane!=0) pads.Ring(end,via,via*.43f,0,.016f);
                    }
            }
            if(mask==1 || mask==7 || mask==15) pads.Ring(junction,via,via*.43f,0,.016f);

            // Packages sit inside reserved quadrants; fanouts join lands to actual pins.
            float side=variant==1 ? 1 : -1;
            var chip=new Vector2(side*.29f,variant==0 ? -.25f : variant==1 ? -.27f : -.30f);
            float footprint=Mathf.Clamp(kit.chipFootprint,.12f,.28f);
            float sx=footprint*.68f,sz=footprint;
            bodies.Box(new Vector3(chip.x,.050f,chip.y),new Vector3(sx,.07f,sz));
            bodies.Box(new Vector3(chip.x,.087f,chip.y),new Vector3(sx*.85f,.007f,sz*.87f));
            // Pin-1 orientation marker and package legend are physical shallow geometry.
            legend.Ring(new Vector2(chip.x-sx*.27f,chip.y+sz*.30f),.013f,0,.091f,.093f);
            int pinCount=variant==2 ? 5 : 4;
            float pitch=sz/(pinCount+1);
            for(int bank=-1;bank<=1;bank+=2)
                for(int pin=0;pin<pinCount;pin++)
                {
                    float z=chip.y+(pin-(pinCount-1)*.5f)*pitch;
                    float landX=chip.x+bank*(sx*.5f+.022f);
                    pads.Box(new Vector3(landX,.007f,z),new Vector3(.052f,.014f,pitch*.55f));
                    pins.Box(new Vector3(landX-bank*.009f,.01575f,z),new Vector3(.050f,.0315f,pitch*.38f));
                    float viaX=chip.x+bank*(sx*.5f+.077f);
                    float viaZ=z+(pin-(pinCount-1)*.5f)*.008f;
                    traces.Route(new[] {new Vector2(landX,z),new Vector2(viaX-.018f*bank,z),new Vector2(viaX,viaZ)},width*.65f,height);
                    pads.Ring(new Vector2(viaX,viaZ),.015f,.006f,0,.016f);
                }
            // Attach one fanout to the main signal rather than scattering orphan squiggles.
            if(mask!=0)
            {
                float middlePin=(pinCount-1)/2;
                float startZ=chip.y+(middlePin-(pinCount-1)*.5f)*(pitch+.008f);
                var start=new Vector2(chip.x-side*(sx*.5f+.077f),startZ);
                var destination=mask==3 ? new Vector2(0,.38f) : mask==5 ? new Vector2(jog,0) : junction;
                traces.Route(new[] {start,new Vector2(start.x,-.14f),new Vector2(0,-.14f+Mathf.Abs(start.x)),destination},width,height);
            }
            Outline(legend,chip,new Vector2(sx+.11f,sz+.055f));
            legend.Label(new Vector2(chip.x-sx*.5f,chip.y-sz*.5f-.045f),variant+1);

            // Paired SMD passives with copper lands and a pin-to-via route.
            for(int r=0;r<3;r++)
            {
                var c=new Vector2(-side*(.24f+r*.046f),variant==0 ? .24f : variant==1 ? .30f : .27f);
                bodies.Box(new Vector3(c.x,.028f,c.y),new Vector3(.029f,.038f,.078f));
                for(int sign=-1;sign<=1;sign+=2)
                {
                    float z=c.y+sign*.045f;
                    pads.Box(new Vector3(c.x,.006f,z),new Vector3(.040f,.012f,.027f));
                    pins.Box(new Vector3(c.x,.0165f,c.y+sign*.033f),new Vector3(.033f,.033f,.019f));
                    var end=new Vector2(c.x,sign>0 ? .41f : .15f);
                    traces.Route(new[] {new Vector2(c.x,z),new Vector2(c.x,sign>0 ? .37f : .18f),end},width*.65f,height);
                    pads.Ring(end,.017f,.007f,0,.014f);
                }
            }
            // Plated through-hole header: square lands, drill openings and upright contacts.
            for(int i=0;i<(variant==1 ? 2 : 3);i++)
            {
                var p=variant==2 ? new Vector2(side*.34f,.20f+i*.067f)
                    : new Vector2(side*(.20f+i*.067f),variant==0 ? .29f : .38f);
                pads.Ring(p,.026f,.010f,0,.020f);
                bodies.Box(new Vector3(p.x,.025f,p.y),new Vector3(.051f,.04f,.056f));
                pins.Box(new Vector3(p.x,.065f,p.y),new Vector3(.016f,.13f,.016f));
                var end=new Vector2(p.x,.19f);
                traces.Route(new[] {p,end},width*.7f,height);
                pads.Ring(end,.016f,.006f,0,.015f);
            }
            // Sparse ground-stitching vias form a designed row, not random clutter.
            for(int i=0;i<(variant==1 ? 2 : 3);i++) pads.Ring(new Vector2(-.35f+i*.075f,variant==2 ? .42f : .44f),.017f,.007f,0,.014f);
        }

        static Vector2 ControllerSize(int variant,PCBTileLibrary kit)
        {
            Vector2[] profiles={new Vector2(.85f,.85f),new Vector2(.52f,1.02f),new Vector2(.66f,.66f),
                new Vector2(1.03f,.52f),new Vector2(.60f,.78f),new Vector2(.68f,1.05f),
                new Vector2(.48f,.78f),new Vector2(.96f,.64f),new Vector2(.76f,.50f),
                new Vector2(.55f,.55f),new Vector2(.84f,1.02f),new Vector2(1.04f,.60f),
                new Vector2(.50f,.92f),new Vector2(.74f,.74f),new Vector2(.92f,.48f),new Vector2(.60f,.95f)};
            return Vector2.Lerp(Vector2.one,profiles[variant%profiles.Length],Mathf.Clamp01(kit.packageSizeVariation))*Mathf.Clamp(kit.controllerFootprint,.30f,.44f);
        }
        static Vector2 ControllerCenter(int variant,PCBTileLibrary kit)
        {
            Vector2[] positions={new Vector2(.11f,-.08f),new Vector2(-.12f,.09f),new Vector2(.10f,.13f),
                new Vector2(-.13f,-.08f),new Vector2(.06f,-.13f),new Vector2(-.08f,.12f),
                new Vector2(.14f,.025f),new Vector2(-.015f,-.14f),new Vector2(-.14f,.025f),
                new Vector2(.12f,-.12f),new Vector2(-.09f,-.12f),new Vector2(.025f,.14f),
                new Vector2(-.13f,.13f),new Vector2(.13f,.08f),new Vector2(-.12f,-.04f),new Vector2(.08f,-.14f)};
            var random=new System.Random(kit.moduleVariationSeed^variant*7919);
            var center=positions[variant%positions.Length]*(Mathf.Clamp(kit.chipPositionVariation,0,.16f)/.13f);
            center+=new Vector2((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*kit.chipPositionVariation*.18f;
            var limit=Vector2.one*.31f-ControllerSize(variant,kit)*.5f;
            return new Vector2(Mathf.Clamp(center.x,-limit.x,limit.x),Mathf.Clamp(center.y,-limit.y,limit.y));
        }
        static bool PlaceComponent(PCBTraceRouter router,Vector2 preferred,Vector2 footprint,int variant,int index,out Vector2 location)
        {
            // Search the perimeter in a different order for each recipe, keeping solder lands clear.
            var angle=(variant%4)*Mathf.PI*.5f;
            var rotated=new Vector2(preferred.x*Mathf.Cos(angle)-preferred.y*Mathf.Sin(angle),preferred.x*Mathf.Sin(angle)+preferred.y*Mathf.Cos(angle));
            if(router.CanPlace(rotated,footprint)){location=rotated;router.Keepout(location,footprint);return true;}
            for(int n=0;n<28;n++)
            {
                int slot=(n+variant*5+index*3)%28;float t=-.38f+(slot%7)*.125f;
                var p=slot/7==0 ? new Vector2(t,.38f) : slot/7==1 ? new Vector2(.38f,t) : slot/7==2 ? new Vector2(t,-.38f) : new Vector2(-.38f,t);
                if(!router.CanPlace(p,footprint))continue;
                location=p;router.Keepout(p,footprint);return true;
            }
            location=default;return false;
        }

        enum CentralComposition { Controller, Capacitors, RAM, Empty }
        static CentralComposition CentralRecipe(int variant,PCBTileLibrary kit)
        {
            var weights=kit.centralCompositionWeights;
            float total=Mathf.Max(0,weights.x)+Mathf.Max(0,weights.y)+Mathf.Max(0,weights.z)+Mathf.Max(0,weights.w);
            if(total<=0)return CentralComposition.Empty;
            int variantCount=Mathf.Clamp(kit.starterVariantCount,6,16);
            // Coprime permutation preserves distribution for every supported variant count.
            int stride=variantCount%5!=0 ? 5 : 7;
            float sample=((variant*stride)%variantCount+.5f)/variantCount*total;
            for(int i=0;i<4;i++){sample-=Mathf.Max(0,weights[i]);if(sample<0)return (CentralComposition)i;}
            return CentralComposition.Empty;
        }

        static void BuildCentral(Geometry traces,Geometry pads,Geometry bodies,Geometry pins,Vector2 center,
            Vector2 package,CentralComposition composition,int variant,PCBTileLibrary kit,float width,float height)
        {
            if(composition==CentralComposition.Controller)
            {
                bodies.Box(new Vector3(center.x,.065f,center.y),new Vector3(package.x,.080f,package.y));
                bodies.Box(new Vector3(center.x,.108f,center.y),new Vector3(package.x*.90f,.006f,package.y*.90f));
                return;
            }
            // Compact chamfered power bus joins the central bank's solder lands.
            // It also leaves an empty recipe free of a compulsory package.
            var half=package*.5f+Vector2.one*.067f;float bevel=.030f;
            var bus=new[]{new Vector2(-half.x+bevel,-half.y),new Vector2(half.x-bevel,-half.y),
                new Vector2(half.x,-half.y+bevel),new Vector2(half.x,half.y-bevel),
                new Vector2(half.x-bevel,half.y),new Vector2(-half.x+bevel,half.y),
                new Vector2(-half.x,half.y-bevel),new Vector2(-half.x,-half.y+bevel),new Vector2(-half.x+bevel,-half.y)};
            for(int i=0;i<bus.Length;i++)bus[i]+=center;
            traces.Route(bus,width,height);
            if(composition==CentralComposition.Capacitors)
            {
                int count=4+variant%3;
                for(int i=0;i<count;i++)
                {
                    Vector2 local;float radius,capHeight;
                    float unit=Mathf.Min(package.x,package.y)*kit.centralCapacitorScale;
                    if(i==0){local=new Vector2(-.26f,.17f);radius=unit*.15f;capHeight=.24f;}
                    else if(i<3){local=new Vector2(.23f,i==1 ? .25f : -.04f);radius=unit*.095f;capHeight=.15f;}
                    else {local=new Vector2(-.28f+(i-3)*.24f,-.32f);radius=unit*.060f;capHeight=.085f;}
                    var p=center+Vector2.Scale(local,package);capHeight*=kit.centralComponentHeight;
                    if(!Imported(bodies,pins,kit.importedCapacitors,p,Vector2.one*radius*2,0,capHeight,variant,50+i,kit,out _))
                    {bodies.Ring(p,radius,0,.010f,capHeight);pins.Ring(p,radius*.9f,0,capHeight,capHeight+.004f);}
                    for(int sign=-1;sign<=1;sign+=2)
                    {
                        var land=p+new Vector2(sign*radius,0);
                        pins.Box(new Vector3(land.x,.010f,land.y),new Vector3(.025f,.020f,.008f));
                        pads.Ring(land,.008f,.003f,0,.012f);
                        traces.Route(new[]{land,new Vector2(center.x+sign*half.x,land.y)},width*.65f,height);
                    }
                }
            }
            else if(composition==CentralComposition.RAM)
            {
                int count=Mathf.Clamp(kit.centralRamCount,1,4);
                for(int i=0;i<count;i++)
                {
                    var p=center+new Vector2((i-(count-1)*.5f)*package.x/(count+1),0);
                    var footprint=new Vector2(package.x/(count+1)*.48f,package.y*.78f);
                    if(!Imported(bodies,pins,kit.importedModules,p,footprint,.014f,.18f*kit.centralComponentHeight,variant,70+i,kit,out _))
                        bodies.Box(new Vector3(p.x,.080f*kit.centralComponentHeight+.014f,p.y),new Vector3(.012f,.16f*kit.centralComponentHeight,footprint.y));
                    bodies.Box(new Vector3(p.x,.007f,p.y),new Vector3(footprint.x,.014f,footprint.y+.012f));
                    for(int sign=-1;sign<=1;sign+=2)
                    {
                        var land=p+new Vector2(0,sign*(footprint.y*.5f+.008f));
                        pins.Box(new Vector3(land.x,.007f,land.y),new Vector3(footprint.x*.8f,.014f,.024f));
                        pads.Box(new Vector3(land.x,.006f,land.y),new Vector3(footprint.x,.012f,.020f));
                        traces.Route(new[]{land,new Vector2(land.x,center.y+sign*half.y)},width*.65f,height);
                    }
                }
            }
        }

        static void BuildController(Geometry traces,Geometry pads,Geometry bodies,Geometry pins,Geometry legend,
            int mask,int variant,PCBTileLibrary kit)
        {
            var center=ControllerCenter(variant,kit);
            var package=ControllerSize(variant,kit);
            var central=CentralRecipe(variant,kit);
            float width=Mathf.Clamp(kit.traceWidth,.004f,.018f),spacing=Mathf.Clamp(kit.busSpacing,.045f,.085f);
            const float height=.008f;
            var router=new PCBTraceRouter(width*.5f+.003f);
            router.Keepout(center,package);
            var placement=new PCBTraceRouter(width*.5f+.003f);placement.Keepout(center,package+Vector2.one*.16f);
            var signalLands=new List<Vector2>();var escapes=new List<Vector2>();
            var mainRoutes=new List<Vector2[]>();var terminals=new List<Vector2>();
            var chipTerminals=new List<Vector2>();
            BuildCentral(traces,pads,bodies,pins,center,package,central,variant,kit,width,height);
            for(int d=0;d<4;d++)
            {
                var dir=PCBLayout.Directions[d];var forward=new Vector2(dir.x,dir.y);var tangent=new Vector2(-forward.y,forward.x);
                float radial=dir.x==0 ? package.y*.5f : package.x*.5f;
                float pitch=Mathf.Min(.045f,(dir.x==0 ? package.x : package.y)/7.8f);
                int bankSize=central==CentralComposition.Controller ? 3 : 1;
                for(int p=-bankSize;p<=bankSize;p++)
                {
                    var land=center+forward*(radial+.033f)+tangent*(p*pitch);
                    pads.Box(new Vector3(land.x,.006f,land.y),new Vector3(pitch*.64f,.012f,.075f),dir.x==0 ? 0 : 90);
                    if(central==CentralComposition.Controller)
                        pins.Box(new Vector3(land.x-forward.x*.012f,.030f,land.y-forward.y*.012f),new Vector3(pitch*.49f,.060f,.056f),dir.x==0 ? 0 : 90);
                    else pads.Ring(land,pitch*.23f,pitch*.09f,0,.012f);
                    var escape=center+forward*(radial+.090f)+tangent*(p*pitch);
                    if(p>=-1&&p<=1&&(mask&(1<<d))!=0)
                    {
                        var edge=forward*.5f+tangent*(p*spacing);var end=land+forward*.035f;
                        float change=Vector2.Dot(end-edge,tangent);var lead=edge-forward*.02f;
                        var shifted=lead+tangent*change-forward*Mathf.Abs(change);
                        mainRoutes.Add(new[]{edge,lead,shifted,end});
                    }
                    else {signalLands.Add(land);escapes.Add(escape);}
                }
            }
            Outline(legend,center,package+Vector2.one*.12f);
            legend.Label(new Vector2(center.x-.035f,center.y-package.y*.5f-.10f),variant+1);
            if(central==CentralComposition.Controller)
            {
                pins.Ring(center+new Vector2(-package.x*.34f,package.y*.34f),.012f,0,.112f,.115f);
                for(int stripe=0;stripe<3;stripe++)pins.Box(new Vector3(center.x,.114f,center.y+stripe*.027f-.027f),new Vector3(package.x*.34f,.003f,.008f));
            }
            foreach(var route in mainRoutes){traces.Route(route,width,height);router.Reserve(route,width);placement.Reserve(route,width);}
            for(int i=0;i<escapes.Count;i++)
                router.Reserve(new[]{signalLands[i],escapes[i]-(escapes[i]-signalLands[i]).normalized*.020f},width);
            int recipe=variant%6;
            int capacitors=Mathf.Clamp(kit.capacitorCount+(recipe==4 ? 2 : recipe==1 ? -2 : 0),0,5);
            for(int c=0;c<capacitors;c++)
            {
                var preferred=c<2 ? new Vector2(-.37f,-.35f+c*.12f) : new Vector2(.22f+(c-2)*.10f,.35f);
                float radius=variant==4 ? .048f : .035f+(c%2)*.005f;
                float capHeight=variant==4 ? .24f : variant==2 ? .10f : .14f+(c%2)*.035f;
                if(!PlaceComponent(placement,preferred,new Vector2(radius*2+.050f,radius*2+.012f),variant,c,out var location))continue;
                router.Keepout(location,Vector2.one*radius*2);
                if(!Imported(bodies,pins,kit.importedCapacitors,location,Vector2.one*radius*2,0,capHeight,variant,c+1,kit,out _))
                {
                    bodies.Ring(location,radius,0,.019f,capHeight);pins.Ring(location,radius*.89f,0,capHeight,capHeight+.004f);
                    bodies.Box(new Vector3(location.x,capHeight+.006f,location.y),new Vector3(radius*1.1f,.003f,.004f));
                    bodies.Box(new Vector3(location.x,capHeight+.006f,location.y),new Vector3(.004f,.003f,radius*1.1f));
                }
                for(int sign=-1;sign<=1;sign+=2)
                {
                    var land=location+new Vector2(sign*(radius+.018f),0);
                    pads.Ring(land,.011f,.004f,0,.012f);
                    pins.Box(new Vector3(location.x+sign*radius,.012f,location.y),new Vector3(.032f,.024f,.008f));
                    terminals.Add(land);
                }
            }
            int resistors=Mathf.Clamp(kit.resistorCount+(recipe==2 ? 2 : recipe==4 ? -2 : 0),1,8);
            for(int r=0;r<resistors;r++)
            {
                var preferred=r<4 ? new Vector2(-.38f+r*.08f,.34f) : new Vector2(.10f+(r-4)*.082f,-.35f);
                bool selectedImport=PickImported(kit.importedPassives,variant,r+8,kit);
                float chipScale=selectedImport ? Mathf.Clamp(kit.importedPassiveScale,1,4) : 1;
                var footprint=new Vector2(.038f,.080f)*chipScale;
                if(!PlaceComponent(placement,preferred,footprint+(selectedImport ? Vector2.one*.100f : new Vector2(.010f,.050f)),variant,r+5,out var location))continue;
                bool importedPassive=Imported(bodies,pins,kit.importedPassives,location,footprint,0,.045f*chipScale,variant,r+8,kit,out var passiveSize);
                router.Keepout(location,importedPassive ? new Vector2(passiveSize.x,passiveSize.z) : footprint);
                if(importedPassive)
                {
                    var importedPrefab=PickImported(kit.importedPassives,variant,r+8,kit);
                    var contacts=PCBImportedPinLayout.Contacts(importedPrefab,location,passiveSize);
                    if(contacts.Count==0)throw new System.InvalidOperationException("Imported chip has no mounting contacts: "+importedPrefab.name);
                    for(int contactIndex=0;contactIndex<contacts.Count;contactIndex++)
                    {
                        var contact=contacts[contactIndex];var direction=contact.direction;
                        float halfExtent=direction.x!=0 ? passiveSize.x*.5f : passiveSize.z*.5f;
                        // One long branch per bank gets first access to the main router.
                        bool branch=contactIndex==0||contacts[contactIndex-1].direction!=direction;
                        var tangent=new Vector2(-direction.y,direction.x);
                        float transverse=Vector2.Dot(contact.point-location,tangent);
                        var escape=location+direction*(halfExtent+(branch ? .048f : .020f))+tangent*transverse;
                        float pinWidth=Mathf.Clamp(contact.width*.60f,.0012f,.004f);
                        pads.Box(new Vector3(contact.point.x,.006f,contact.point.y),new Vector3(pinWidth*1.6f,.012f,.013f),direction.x!=0 ? 90 : 0);
                        traces.Route(new[]{contact.point,escape},pinWidth,height);
                        pads.Ring(escape,pinWidth*.9f,pinWidth*.32f,0,.012f);
                        // Keep the branch's routing endpoint clear of its own fanout.
                        router.Reserve(new[]{contact.point,branch ? escape-direction*.022f : escape},pinWidth);
                        if(branch)chipTerminals.Add(escape);
                    }
                    continue;
                }
                if(!importedPassive)
                {
                    bodies.Box(new Vector3(location.x,.035f,location.y),new Vector3(.038f,.045f,.080f));
                    for(int band=0;band<3;band++)pins.Box(new Vector3(location.x,.060f,location.y+(band-1)*.018f),new Vector3(.040f,.004f,.007f));
                }
                for(int sign=-1;sign<=1;sign+=2)
                {
                    float halfLength=importedPassive ? passiveSize.z*.5f : .040f;
                    float contactWidth=importedPassive ? Mathf.Min(passiveSize.x,.037f) : .037f;
                    var land=location+new Vector2(0,sign*(halfLength+.010f));
                    pads.Box(new Vector3(land.x,.006f,land.y),new Vector3(contactWidth+.008f,.012f,.030f));
                    float contactHeight=importedPassive ? .004f : .0365f;
                    pins.Box(new Vector3(land.x,contactHeight*.5f,land.y-sign*.005f),new Vector3(contactWidth,contactHeight,.030f));
                    terminals.Add(land);
                }
            }
            // A vertical RAM module sits in a low socket, with contacts at the socket ends.
            // Its thin board keeps the imported proportions and does not receive QFP legs.
            if(variant%3==1 && PlaceComponent(placement,new Vector2(.30f,-.27f),new Vector2(.090f,.29f),variant,24,out var ram))
            {
                if(Imported(bodies,pins,kit.importedModules,ram,new Vector2(.055f,.24f),.014f,.10f,variant,24,kit,out _))
                {
                    router.Keepout(ram,new Vector2(.075f,.27f));
                    bodies.Box(new Vector3(ram.x,.007f,ram.y),new Vector3(.075f,.014f,.27f));
                    for(int sign=-1;sign<=1;sign+=2)
                    {
                        var land=ram+new Vector2(0,sign*.145f);
                        pads.Box(new Vector3(land.x,.006f,land.y),new Vector3(.060f,.012f,.035f));
                        pins.Box(new Vector3(land.x,.007f,land.y-sign*.014f),new Vector3(.050f,.014f,.045f));
                        terminals.Add(land);
                    }
                }
            }
            int headerPins=recipe==5 ? 7 : recipe==3 ? 2 : 4;
            for(int i=0;i<headerPins;i++)
            {
                var preferred=recipe==5 ? new Vector2(-.32f+i*.060f,-.44f) : new Vector2(.44f,-.28f+i*.063f);
                if(!PlaceComponent(placement,preferred,Vector2.one*.038f,variant,i+15,out var location))continue;
                pads.Ring(location,.016f,.007f,0,.014f);
                pins.Box(new Vector3(location.x,.0725f,location.y),new Vector3(.014f,.145f,.014f));
                terminals.Add(location);
            }
            // Prefer nearby pins, then try other free pins when the direct corridor is occupied.
            // A rejected route is omitted; it never becomes a crossing or a floating ornament.
            int budget=Mathf.Clamp(kit.localTraceDensity,1,3)*7;
            int connected=0;
            chipTerminals.AddRange(terminals);
            foreach(var terminal in chipTerminals)
            {
                if(connected>=budget)break;
                var candidates=new List<int>();for(int i=0;i<escapes.Count;i++)candidates.Add(i);
                candidates.Sort((a,b)=>(escapes[a]-terminal).sqrMagnitude.CompareTo((escapes[b]-terminal).sqrMagnitude));
                foreach(int i in candidates)
                {
                    if(!router.TryRoute(escapes[i],terminal,out var route))continue;
                    traces.Route(new[]{signalLands[i],escapes[i]},width,height);
                    traces.Route(route,width,height);router.Reserve(route,width);router.Reserve(new[]{signalLands[i],escapes[i]},width);
                    signalLands.RemoveAt(i);escapes.RemoveAt(i);connected++;break;
                }
            }
        }

        static GameObject PickImported(GameObject[] variants,int variant,int index,PCBTileLibrary kit)
        {
            if(variants==null||variants.Length==0)return null;
            var random=new System.Random(kit.moduleVariationSeed ^ variant*7919 ^ index*104729);
            if(random.NextDouble()>=kit.importedComponentVariation)return null;
            return variants[random.Next(variants.Length)];
        }

        static bool Imported(Geometry bodies,Geometry pins,GameObject[] variants,Vector2 center,Vector2 footprint,
            float bottom,float height,int variant,int index,PCBTileLibrary kit,out Vector3 mountedSize,bool allowed=true)
        {
            mountedSize=Vector3.zero;
            if(!allowed)return false;
            var prefab=PickImported(variants,variant,index,kit);if(!prefab)return false;
            var filters=prefab.GetComponentsInChildren<MeshFilter>();
            var bounds=new Bounds();bool first=true;
            foreach(var f in filters)
            {
                var matrix=prefab.transform.worldToLocalMatrix*f.transform.localToWorldMatrix;
                foreach(var v in f.sharedMesh.vertices){var p=matrix.MultiplyPoint3x4(v);if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
            }
            if(first||bounds.size.y<.0001f)return false;
            float horizontal=Mathf.Min(footprint.x/Mathf.Max(bounds.size.x,.0001f),footprint.y/Mathf.Max(bounds.size.z,.0001f));
            var scale=new Vector3(horizontal,Mathf.Min(horizontal,height/bounds.size.y)*kit.importedHeightScale,horizontal);
            mountedSize=Vector3.Scale(bounds.size,scale);
            var offset=new Vector3(center.x,bottom,center.y)-Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),scale);
            foreach(var f in filters)
            {
                var matrix=Matrix4x4.TRS(offset,Quaternion.identity,scale)*prefab.transform.worldToLocalMatrix*f.transform.localToWorldMatrix;
                var renderer=f.GetComponent<MeshRenderer>();
                for(int sub=0;sub<f.sharedMesh.subMeshCount;sub++)
                {
                    bool contact=renderer && renderer.sharedMaterials.Length>sub && renderer.sharedMaterials[sub]==kit.contactMaterial;
                    (contact?pins:bodies).Append(f.sharedMesh,matrix,sub);
                }
            }
            return true;
        }

        static void Outline(Geometry legend,Vector2 center,Vector2 size)
        {
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {
                var corner=center+Vector2.Scale(new Vector2(x,z),size*.5f);
                legend.Route(new[] {corner-new Vector2(x*.03f,0),corner,corner-new Vector2(0,z*.03f)},.004f,.004f);
            }
        }

        static Material MakeMaterial(string name, bool whiteFloor, float brightness, float specular)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Material>(whiteFloor ? "Assets/ShaderTests/Ground.mat" : "Assets/ShaderTests/ShaderTest.mat");
            var material = source ? new Material(source) : new Material(Shader.Find("AAR/Retro Dither"));
            material.name = "PCB " + name;
            material.enableInstancing = true;
            material.SetFloat("_ShadowOnly", whiteFloor ? 1 : 0);
            material.SetColor("_GroundColor", Color.white);
            material.SetFloat("_Brightness", brightness);
            material.SetFloat("_Specular", specular);
            material.SetFloat("_RimStrength", .0f);
            material.SetFloat("_PixelSize", 2);
            material.SetFloat("_PatternScaleMode", 0);
            material.SetFloat("_PreserveDither", 1);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void AddGroup(GameObject root, string groupName, Geometry geometry, Material material, string tile)
        {
            var mesh = geometry.Mesh(tile + " " + groupName);
            string path = Root + "/Meshes/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                // Use mesh setters so Unity also invalidates the renderer's native buffers.
                existing.Clear();
                existing.indexFormat = mesh.indexFormat;
                existing.vertices = mesh.vertices;
                existing.uv = mesh.uv;
                existing.triangles = mesh.triangles;
                existing.normals = mesh.normals;
                existing.bounds = mesh.bounds;
                Object.DestroyImmediate(mesh);
                mesh = existing;
                EditorUtility.SetDirty(mesh);
            }
            else AssetDatabase.CreateAsset(mesh, path);
            var child = new GameObject(groupName);
            child.transform.SetParent(root.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = (groupName == "Surface" || groupName == "Markings") ? ShadowCastingMode.Off : ShadowCastingMode.On;
        }

        sealed class Geometry
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<int> triangles = new List<int>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly Dictionary<int,Vector3> importedNormals = new Dictionary<int,Vector3>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                // Each face has two surface dimensions. A top-down X/Z projection
                // collapses vertical faces to a line and cannot carry a surface dither.
                var normal=Vector3.Cross(b-a,c-a);
                if(normal.sqrMagnitude<1e-12f)normal=Vector3.Cross(c-a,d-a);
                var axis=new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z));
                Vector2 SurfaceUV(Vector3 p) => axis.y>=axis.x&&axis.y>=axis.z
                    ? new Vector2(p.x+.5f,p.z+.5f) : axis.x>=axis.z
                    ? new Vector2(p.z+.5f,p.y+.5f) : new Vector2(p.x+.5f,p.y+.5f);
                uv.AddRange(new[]{SurfaceUV(a),SurfaceUV(b),SurfaceUV(c),SurfaceUV(d)});
                triangles.AddRange(new[] { start, start+1, start+2, start, start+2, start+3 });
            }
            public void Box(Vector3 center, Vector3 size, float angle = 0)
            {
                var p = new Vector3[8];
                var rotation = Quaternion.Euler(0, angle, 0);
                for (int i=0;i<8;i++) p[i] = center + rotation * Vector3.Scale(new Vector3((i&1)==0 ? -.5f:.5f, (i&2)==0 ? -.5f:.5f, (i&4)==0 ? -.5f:.5f), size);
                Quad(p[2],p[6],p[7],p[3]); Quad(p[0],p[1],p[5],p[4]);
                Quad(p[0],p[2],p[3],p[1]); Quad(p[4],p[5],p[7],p[6]);
                Quad(p[0],p[4],p[6],p[2]); Quad(p[1],p[3],p[7],p[5]);
            }
            public void Route(Vector2[] points, float width, float height)
            {
                for (int i=1;i<points.Length;i++)
                {
                    Vector2 delta = points[i]-points[i-1], mid=(points[i]+points[i-1])*.5f;
                    Box(new Vector3(mid.x,height*.5f,mid.y),new Vector3(width,height,delta.magnitude),Mathf.Atan2(delta.x,delta.y)*Mathf.Rad2Deg);
                    // Fill the interior of chamfer joints, never extend beyond edge endpoints.
                    if(i<points.Length-1) Ring(points[i],width*.5f,0,0,height);
                }
            }
            public void Ring(Vector2 center,float outer,float inner,float bottom,float top)
            {
                const int segments=16;
                for(int i=0;i<segments;i++)
                {
                    float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                    Vector3 O(float angle,float radius,float y) => new Vector3(center.x+Mathf.Cos(angle)*radius,y,center.y+Mathf.Sin(angle)*radius);
                    Quad(O(a,inner,top),O(b,inner,top),O(b,outer,top),O(a,outer,top));
                    Quad(O(a,outer,bottom),O(b,outer,bottom),O(b,inner,bottom),O(a,inner,bottom));
                    int outerStart=uv.Count;
                    Quad(O(a,outer,top),O(b,outer,top),O(b,outer,bottom),O(a,outer,bottom));
                    // Unwrap can walls around their circumference, keeping continuity
                    // across all 16 facets and avoiding projection changes on the curve.
                    uv[outerStart]=new Vector2(a*outer,top);
                    uv[outerStart+1]=new Vector2(b*outer,top);
                    uv[outerStart+2]=new Vector2(b*outer,bottom);
                    uv[outerStart+3]=new Vector2(a*outer,bottom);
                    if(inner>0)
                    {
                        int innerStart=uv.Count;
                        Quad(O(a,inner,bottom),O(b,inner,bottom),O(b,inner,top),O(a,inner,top));
                        uv[innerStart]=new Vector2(a*inner,bottom);
                        uv[innerStart+1]=new Vector2(b*inner,bottom);
                        uv[innerStart+2]=new Vector2(b*inner,top);
                        uv[innerStart+3]=new Vector2(a*inner,top);
                    }
                }
            }
            public void Label(Vector2 origin,int digit)
            {
                const float h=.028f,w=.016f,t=.003f;
                Route(new[]{origin+new Vector2(0,h),origin,origin+new Vector2(w,0),origin+new Vector2(w,h)},t,.004f);
                Vector2 p=origin+new Vector2(.026f,0);
                int bits=digit==1 ? 6 : digit==2 ? 91 : 79;
                Vector2[] a={new Vector2(0,h),new Vector2(w,h),new Vector2(w,h*.5f),Vector2.zero,Vector2.zero,new Vector2(0,h*.5f),new Vector2(0,h*.5f)};
                Vector2[] b={new Vector2(w,h),new Vector2(w,h*.5f),new Vector2(w,0),new Vector2(w,0),new Vector2(0,h*.5f),new Vector2(0,h),new Vector2(w,h*.5f)};
                for(int i=0;i<7;i++)if((bits&(1<<i))!=0)Route(new[]{p+a[i],p+b[i]},t,.004f);
            }
            public void Append(Mesh mesh,Matrix4x4 matrix,int submesh)
            {
                var source=mesh.vertices;var sourceUV=mesh.uv;var normals=mesh.normals;var normalMatrix=matrix.inverse.transpose;int start=vertices.Count;
                for(int i=0;i<source.Length;i++){var p=matrix.MultiplyPoint3x4(source[i]);vertices.Add(p);uv.Add(sourceUV.Length==source.Length?sourceUV[i]:new Vector2(p.x+.5f,p.z+.5f));}
                if(normals.Length==source.Length)for(int i=0;i<normals.Length;i++)importedNormals[start+i]=normalMatrix.MultiplyVector(normals[i]).normalized;
                foreach(var index in mesh.GetTriangles(submesh))triangles.Add(start+index);
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh {name=name, indexFormat=UnityEngine.Rendering.IndexFormat.UInt32}; mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();if(importedNormals.Count>0){var normals=mesh.normals;foreach(var pair in importedNormals)normals[pair.Key]=pair.Value;mesh.normals=normals;}mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
