using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;

public static class MedievalVillageBuilder
{
    const string MaterialFolder = "Assets/Materials/Medieval/Village";
    const string MeshFolder = "Assets/Models/MedievalVillage";
    static Material stone, wood, iron, gold, plaster, roofRed, roofBlue, water, paving, grass;
    static Mesh roofMesh;
    static Transform environment;
    static readonly Vector2Int[] directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
    public static int RouteLength { get; private set; }

    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Medieval.unity") throw new System.InvalidOperationException("Open Medieval scene first.");
        var roots = scene.GetRootGameObjects();
        if (roots.Any(r => r.name == "Medieval Castle Village")) throw new System.InvalidOperationException("Castle village already exists; do not overwrite it.");
        var player = roots.SelectMany(r => r.GetComponentsInChildren<PlayerMovement>(true)).First();
        var camera = roots.SelectMany(r => r.GetComponentsInChildren<CameraFollow>(true)).First();
        var level = roots.SelectMany(r => r.GetComponentsInChildren<MedievalLevel>(true)).First();
        var clock = roots.SelectMany(r => r.GetComponentsInChildren<Clock>(true)).First();
        var interactor = player.GetComponent<MedievalInteractor>();
        var jump = player.GetComponent<MedievalJump>();
        var body = player.GetComponent<Rigidbody>();
        foreach (var border in roots.Where(r => r != null).SelectMany(r => r.GetComponentsInChildren<LevelBorder>(true)).ToArray())
        {
            Undo.RecordObject(border, "Use castle boundary"); border.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(border);
        }
        foreach (var root in roots.Where(r => r != null && r.name == "Level"))
        {
            Undo.RecordObject(root, "Preserve old Medieval layout"); root.name = "Previous Medieval Layout (preserved)"; root.SetActive(false);
        }
        Folder(MaterialFolder); Folder(MeshFolder);
        stone = LoadMaterial("MedievalStone"); wood = LoadMaterial("MedievalWood"); iron = LoadMaterial("MedievalIron"); gold = LoadMaterial("MedievalGold");
        plaster = NewMaterial("Warm plaster", new Color(0.78f, 0.65f, 0.43f));
        roofRed = NewMaterial("Terracotta roof", new Color(0.36f, 0.12f, 0.08f));
        roofBlue = NewMaterial("Slate roof", new Color(0.13f, 0.22f, 0.28f));
        water = NewMaterial("Moat water", new Color(0.055f, 0.30f, 0.37f), 0.8f);
        paving = NewMaterial("Courtyard cobble", new Color(0.40f, 0.37f, 0.30f));
        grass = NewMaterial("Moss green", new Color(0.21f, 0.28f, 0.14f));
        roofMesh = CreateRoofMesh();
        var rootVillage = Root("Medieval Castle Village", null); environment = rootVillage.transform;
        var scenery = Root("Castle and Moat", environment).transform;
        Box("Castle island foundation", scenery, new Vector3(0,-0.35f,12), new Vector3(68,0.7f,80), stone, true);
        Box("Village cobbled courtyard", scenery, new Vector3(0,0.015f,-13), new Vector3(66,0.03f,28), paving, false);
        Box("Keep floor", scenery, new Vector3(0,0.015f,23), new Vector3(43,0.03f,43), paving, false);
        Box("West moat", scenery, new Vector3(-42,-0.65f,12), new Vector3(16,0.08f,108), water, false);
        Box("East moat", scenery, new Vector3(42,-0.65f,12), new Vector3(16,0.08f,108), water, false);
        Box("South moat", scenery, new Vector3(0,-0.65f,-36), new Vector3(68,0.08f,16), water, false);
        Box("North moat", scenery, new Vector3(0,-0.65f,60), new Vector3(68,0.08f,16), water, false);
        Box("West bank", scenery, new Vector3(-51,-0.9f,12), new Vector3(3,0.4f,108), grass, false);
        Box("East bank", scenery, new Vector3(51,-0.9f,12), new Vector3(3,0.4f,108), grass, false);
        Curtain("West curtain", scenery, new Vector3(-33,0,12), 80, true);
        Curtain("East curtain", scenery, new Vector3(33,0,12), 80, true);
        Curtain("North curtain", scenery, new Vector3(0,0,51), 66, false);
        Curtain("South curtain left", scenery, new Vector3(-18,0,-27), 30, false);
        Curtain("South curtain right", scenery, new Vector3(18,0,-27), 30, false);
        Box("Main gate arch", scenery, new Vector3(0,5.2f,-27), new Vector3(6,1.1f,1.3f), stone, true);
        for (int x=-2;x<=2;x++) Box("Outer portcullis", scenery, new Vector3(x,2.4f,-27), new Vector3(0.16f,4.8f,0.2f), iron, true);
        Box("Drawbridge", scenery, new Vector3(0,-0.02f,-35), new Vector3(5.5f,0.25f,15), wood, true);
        for(int x=-1;x<=1;x+=2) Box("Bridge rail",scenery,new Vector3(x*2.6f,0.65f,-35),new Vector3(0.15f,0.15f,15),wood,false);
        foreach (float x in new[]{-32f,32f}) foreach(float z in new[]{-26f,50f}) Tower(scenery,new Vector3(x,0,z));
        Tower(scenery,new Vector3(-6,0,-27)); Tower(scenery,new Vector3(6,0,-27));
        var village = Root("Decorated Village",environment).transform;
        for(int side=-1;side<=1;side+=2) for(int row=0;row<3;row++) for(int column=0;column<2;column++)
            House(village,new Vector3(side*(17+column*9),0,-21+row*8),side<0?90:-90,(row+column)%2==0?roofRed:roofBlue);
        Well(village,new Vector3(0,0,-7));
        for(int side=-1;side<=1;side+=2)
        {
            Stall(village,new Vector3(side*8,0,-20),side<0?roofRed:roofBlue);
            Stall(village,new Vector3(side*8,0,-5),side<0?roofBlue:roofRed);
            for(int i=0;i<4;i++)
            {
                Prefab("Barrel",village,new Vector3(side*(11+i%2),0,-23+i*5));
                Prefab("Crate",village,new Vector3(side*12,0,-20+i*5));
            }
            for(int z=-24;z<=-2;z+=6) Torch(village,new Vector3(side*4.8f,0,z));
            Bench(village,new Vector3(side*5,0,-9));
        }
        var keep = Root("Maze Keep",environment).transform;
        Box("Keep west wall",keep,new Vector3(-21,2.1f,23),new Vector3(0.9f,4.2f,43),stone,true);
        Box("Keep east wall",keep,new Vector3(21,2.1f,23),new Vector3(0.9f,4.2f,43),stone,true);
        Box("Keep north wall",keep,new Vector3(0,2.1f,44),new Vector3(43,4.2f,0.9f),stone,true);
        Box("Keep entrance left",keep,new Vector3(-12,2.1f,2),new Vector3(18,4.2f,0.9f),stone,true);
        Box("Keep entrance right",keep,new Vector3(12,2.1f,2),new Vector3(18,4.2f,0.9f),stone,true);
        Box("Keep arch",keep,new Vector3(0,4.3f,2),new Vector3(6,0.6f,1),stone,false);
        for(int x=-1;x<=1;x+=2) Torch(keep,new Vector3(x*4f,0,0));
        var gateObject=Root("Rune portcullis",keep);
        gateObject.transform.position=new Vector3(0,0,2);
        Box("Gate iron bars",gateObject.transform,new Vector3(0,1.6f,2),new Vector3(5.7f,3.2f,0.3f),iron,true);
        for(int x=-2;x<=2;x++)Box("Gate gold strip",gateObject.transform,new Vector3(x,1.6f,1.81f),new Vector3(0.07f,3.2f,0.08f),gold,false);
        var gate=gateObject.AddComponent<Gate>();gate.openOffset=new Vector3(0,-4f,0);gate.openTime=0.8f;
        var puzzleObject=Root("Rune lock pedestal",keep);puzzleObject.transform.position=new Vector3(-3.8f,0,-0.7f);
        Box("Rune stone",puzzleObject.transform,puzzleObject.transform.position+Vector3.up*0.8f,new Vector3(0.8f,1.6f,0.8f),stone,true);
        var puzzle=puzzleObject.AddComponent<CastleRunePuzzle>();
        var adventureObject=Root("Castle Adventure",environment);var adventure=adventureObject.AddComponent<CastleAdventure>();
        Set(puzzle,"gate",gate);Set(puzzle,"movement",player);Set(puzzle,"jump",jump);Set(puzzle,"playerInteractor",interactor);Set(puzzle,"playerBody",body);
        BuildPuzzleUI(puzzle);
        BuildMaze(keep);
        var machineObject=Prefab("TimeMachine",village,new Vector3(0,0,-16));var machine=machineObject.GetComponent<TimeMachine>();
        Undo.RecordObject(machine,"Enable departure");machine.loadNextEra=true;
        machine.inspectPrompt="Inspect crashed time machine";
        UnityEditor.Events.UnityEventTools.AddPersistentListener(machine.onRepaired,adventure.DepartAfterRepair);
        var crashStart=Marker("Crash approach",environment,new Vector3(-17,18,-26));
        var crashEnd=Marker("Crash landing centre",environment,new Vector3(0,0,-16));
        var exit=Marker("Player exit",environment,new Vector3(3,0.1f,-13));
        var cinematic=Marker("Arrival camera shot",environment,new Vector3(0,38,-70));cinematic.LookAt(new Vector3(0,2,-12));
        var debris=Root("Crash debris",village);debris.transform.position=new Vector3(0,0,-16);
        for(int i=0;i<10;i++){float angle=i*Mathf.PI*0.2f;var shard=Box("Scattered stone",debris.transform,new Vector3(Mathf.Sin(angle)*(2.1f+i*0.08f),0.1f,-16+Mathf.Cos(angle)*(2.1f+i*0.08f)),new Vector3(0.35f,0.18f,0.4f),stone,false);shard.transform.rotation=Quaternion.Euler(0,i*37,15);}
        debris.SetActive(false);
        var guideObject=Root("Castle Warden",village);guideObject.transform.position=new Vector3(5,0,-12);
        var guideSpriteObject=Root("Sprite",guideObject.transform);guideSpriteObject.transform.localPosition=Vector3.up*0.6f;
        CompleteQuestWiring();
    }

    public static void CompleteQuestWiring()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var roots=scene.GetRootGameObjects();
        var rootVillage=roots.First(r=>r.name=="Medieval Castle Village");
        var transforms=rootVillage.GetComponentsInChildren<Transform>(true);
        var village=transforms.First(t=>t.name=="Decorated Village");
        var keep=transforms.First(t=>t.name=="Maze Keep");
        var guideObject=transforms.First(t=>t.name=="Castle Warden").gameObject;
        var guideSpriteObject=guideObject.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Sprite").gameObject;
        var adventure=rootVillage.GetComponentInChildren<CastleAdventure>(true);
        var puzzle=rootVillage.GetComponentInChildren<CastleRunePuzzle>(true);
        var machine=rootVillage.GetComponentInChildren<TimeMachine>(true);
        var crashStart=transforms.First(t=>t.name=="Crash approach");var crashEnd=transforms.First(t=>t.name=="Crash landing centre");
        var exit=transforms.First(t=>t.name=="Player exit");var cinematic=transforms.First(t=>t.name=="Arrival camera shot");
        var debris=transforms.First(t=>t.name=="Crash debris").gameObject;
        var player=roots.SelectMany(r=>r.GetComponentsInChildren<PlayerMovement>(true)).First();
        var camera=roots.SelectMany(r=>r.GetComponentsInChildren<CameraFollow>(true)).First();
        var level=roots.SelectMany(r=>r.GetComponentsInChildren<MedievalLevel>(true)).First();
        var clock=roots.SelectMany(r=>r.GetComponentsInChildren<Clock>(true)).First();
        var interactor=player.GetComponent<MedievalInteractor>();var jump=player.GetComponent<MedievalJump>();var body=player.GetComponent<Rigidbody>();
        var sr=guideSpriteObject.GetComponent<SpriteRenderer>();if(sr==null)sr=guideSpriteObject.AddComponent<SpriteRenderer>();sr.sprite=player.sprite.sprite;sr.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SpriteLit.mat");
        guideSpriteObject.AddComponent<Billboard>();
        var guideTrigger=guideObject.AddComponent<SphereCollider>();guideTrigger.isTrigger=true;guideTrigger.radius=2.5f;
        var guide=guideObject.AddComponent<CastleGuide>();Set(guide,"adventure",adventure);
        var arena=Root("Crystal Guardian Arena",keep);arena.transform.position=new Vector3(0,0,23);
        var arenaTrigger=arena.AddComponent<BoxCollider>();arenaTrigger.isTrigger=true;arenaTrigger.center=Vector3.up;arenaTrigger.size=new Vector3(16,3,16);
        Set(arena.AddComponent<CastleArenaTrigger>(),"adventure",adventure);
        var guardians=new SkeletonEnemy[3];
        var locations=new[]{new Vector3(-4,0,23),new Vector3(4,0,23),new Vector3(0,0,29)};
        for(int i=0;i<3;i++)
        {
            var guardianObject=Prefab("Skeleton",arena.transform,locations[i]);guardianObject.name="Crystal Guardian "+(i+1);
            var skeleton=guardianObject.GetComponent<SkeletonEnemy>();skeleton.patrolA=locations[i]+Vector3.left;skeleton.patrolB=locations[i]+Vector3.right;skeleton.leashRadius=7;skeleton.aggroRadius=6;skeleton.chaseSpeed=2.5f;
            Set(guardianObject.AddComponent<CastleGuardCombat>(),"playerInteractor",interactor);guardians[i]=skeleton;guardianObject.SetActive(false);
        }
        var chestObject=Prefab("TreasureChest",arena.transform,new Vector3(0,0,25));chestObject.name="Time Crystal Reward Chest";
        var chest=chestObject.GetComponent<TreasureChest>();chest.timeBonus=45;chest.openedLines=new[]{"The Time Crystal is yours. Return through the maze to the crashed machine!"};
        UnityEditor.Events.UnityEventTools.AddPersistentListener(chest.onOpened,adventure.CrystalClaimed);
        var crystal=Primitive("Time Crystal",chestObject.transform,new Vector3(0,1.15f,25),new Vector3(0.4f,0.65f,0.4f),PrimitiveType.Cube,LoadMaterial("MedievalCrystal"),false);crystal.transform.rotation=Quaternion.Euler(0,45,45);
        chestObject.SetActive(false);
        Set(adventure,"level",level);Set(adventure,"player",player);Set(adventure,"jump",jump);Set(adventure,"interactor",interactor);Set(adventure,"playerBody",body);Set(adventure,"followCamera",camera);Set(adventure,"machine",machine);
        Set(adventure,"crashStart",crashStart);Set(adventure,"crashEnd",crashEnd);Set(adventure,"exitPoint",exit);Set(adventure,"cinematicCamera",cinematic);Set(adventure,"crashEffect",debris);Set(adventure,"rewardChest",chestObject);Set(adventure,"entrancePuzzle",puzzle);
        SetArray(adventure,"guardians",guardians.Cast<Object>().ToArray());SetArray(adventure,"playerSprites",player.GetComponentsInChildren<SpriteRenderer>(true).Cast<Object>().ToArray());
        Undo.RecordObject(level,"Castle quest introduction");var levelData=new SerializedObject(level);levelData.FindProperty("externalIntro").boolValue=true;levelData.ApplyModifiedProperties();
        level.repairedLines=new[]{"The Time Crystal powers the engine. Your time machine is repaired!","Leaving the castle... the next journey begins."};
        Undo.RecordObject(player.transform,"Castle arrival point");player.transform.position=exit.position;
        Undo.RecordObject(camera,"Castle camera framing");camera.pitch=45;camera.distance=48;camera.smoothTime=0.18f;
        Undo.RecordObject(camera.transform,"Castle camera placement");camera.transform.rotation=Quaternion.Euler(camera.pitch,0,0);camera.transform.position=exit.position-camera.transform.forward*camera.distance;
        Undo.RecordObject(clock,"Castle level time allowance");clock.startSeconds=240;
        PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(camera);PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(clock);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject=rootVillage;
    }

    public static void BuildMaze(Transform parent)
    {
        stone=LoadMaterial("MedievalStone");wood=LoadMaterial("MedievalWood");
        bool[,,] walls=new bool[7,7,4];bool[,] visited=new bool[7,7];
        for(int x=0;x<7;x++)for(int y=0;y<7;y++)for(int d=0;d<4;d++)walls[x,y,d]=true;
        // Generate the winding outer maze separately from the central battle chamber.
        for(int x=2;x<=4;x++)for(int y=2;y<=4;y++)visited[x,y]=true;
        var random=new System.Random(47);var stack=new Stack<Vector2Int>();var start=new Vector2Int(3,0);stack.Push(start);visited[start.x,start.y]=true;
        while(stack.Count>0)
        {
            var c=stack.Peek();var options=new List<int>();for(int d=0;d<4;d++){var n=c+directions[d];if(n.x>=0&&n.x<7&&n.y>=0&&n.y<7&&!visited[n.x,n.y])options.Add(d);}
            if(options.Count==0){stack.Pop();continue;}int chosen=options[random.Next(options.Count)];var next=c+directions[chosen];walls[c.x,c.y,chosen]=false;walls[next.x,next.y,(chosen+2)%4]=false;visited[next.x,next.y]=true;stack.Push(next);
        }
        for(int x=2;x<=4;x++)for(int y=2;y<=4;y++)for(int d=0;d<4;d++){var n=new Vector2Int(x,y)+directions[d];if(n.x>=2&&n.x<=4&&n.y>=2&&n.y<=4)walls[x,y,d]=false;}
        walls[3,4,1]=false;walls[3,5,3]=false;
        var maze=Root("Maze walls",parent).transform;
        for(int x=0;x<7;x++)for(int y=0;y<7;y++)
        {
            var pos=Cell(new Vector2Int(x,y));
            if(x<6&&walls[x,y,0])Box("Maze wall E "+x+","+y,maze,pos+new Vector3(3,1.6f,0),new Vector3(0.55f,3.2f,6.1f),stone,true);
            if(y<6&&walls[x,y,1])Box("Maze wall N "+x+","+y,maze,pos+new Vector3(0,1.6f,3),new Vector3(6.1f,3.2f,0.55f),stone,true);
        }
        var queue=new Queue<Vector2Int>();var previous=new Dictionary<Vector2Int,Vector2Int>();queue.Enqueue(start);previous[start]=start;var end=new Vector2Int(3,3);
        while(queue.Count>0){var c=queue.Dequeue();if(c==end)break;for(int d=0;d<4;d++){var n=c+directions[d];if(n.x<0||n.x>=7||n.y<0||n.y>=7||walls[c.x,c.y,d]||previous.ContainsKey(n))continue;previous[n]=c;queue.Enqueue(n);}}
        var route=new List<Vector2Int>();var at=end;while(at!=start){route.Add(at);at=previous[at];}route.Add(start);route.Reverse();RouteLength=route.Count;
        var routeRoot=Root("Maze solution waypoints",parent).transform;
        for(int i=0;i<route.Count;i++)Marker("Route "+i.ToString("D2"),routeRoot,Cell(route[i]));
        var traps=Root("Spikes and swinging guillotines",parent).transform;
        for(int i=1;i<route.Count-1;i++)
        {
            var cell=route[i];if(cell.x>=2&&cell.x<=4&&cell.y>=2&&cell.y<=4)continue;
            Vector3 pos=Cell(cell);bool alongX=route[i+1].x!=cell.x;
            if(i%2==1){var trap=Prefab("SpikeTrap",traps,pos);trap.name="Route spike trap "+i;trap.transform.localScale=alongX?new Vector3(1.4f,1,2.7f):new Vector3(2.7f,1,1.4f);trap.GetComponent<SpikeTrap>().phaseOffset=(i%3)/3f;}
            else Guillotine(traps,pos,alongX,i);
        }
        for(int x=0;x<7;x++)for(int y=0;y<7;y++)if((x+y)%5==0 && !(x>=2&&x<=4&&y>=2&&y<=4) && !route.Contains(new Vector2Int(x,y)))
            Prefab("SpikeTrap",traps,Cell(new Vector2Int(x,y))).GetComponent<SpikeTrap>().phaseOffset=0.5f;
        for(int x=0;x<7;x+=2)for(int y=0;y<7;y+=2)Torch(parent,Cell(new Vector2Int(x,y))+new Vector3(2.2f,0,2.2f));
    }

    static void Guillotine(Transform parent,Vector3 pos,bool alongX,int index)
    {
        var go=Prefab("SwingingAxe",parent,pos);go.name="Swinging guillotine "+index;
        var swing=go.GetComponentInChildren<SwingingAxe>();swing.axis=alongX?Vector3.right:Vector3.forward;swing.phaseOffset=(index%4)*0.2f;
        foreach(var t in go.GetComponentsInChildren<Transform>())if(t.name=="Beam")t.localScale=alongX?new Vector3(0.3f,0.3f,5.7f):new Vector3(5.7f,0.3f,0.3f);
        for(int side=-1;side<=1;side+=2)Box("Guillotine frame",go.transform,pos+(alongX?new Vector3(0,2.1f,side*2.6f):new Vector3(side*2.6f,2.1f,0)),new Vector3(0.25f,4.2f,0.25f),wood,false);
    }
    static Vector3 Cell(Vector2Int c) => new Vector3(-18+c.x*6,0,5+c.y*6);
    static void Curtain(string name,Transform parent,Vector3 pos,float length,bool northSouth)
    {
        Box(name,parent,pos+Vector3.up*2.5f,northSouth?new Vector3(1.2f,5,length):new Vector3(length,5,1.2f),stone,true);
        for(float n=-length/2+1;n<length/2;n+=2.2f)Box("Crenellation",parent,pos+(northSouth?new Vector3(0,5.5f,n):new Vector3(n,5.5f,0)),new Vector3(1.2f,1,1.2f),stone,false);
    }
    static void Tower(Transform parent,Vector3 pos)
    {
        var t=Root("Castle tower",parent).transform;
        Primitive("Tower stone",t,pos+Vector3.up*3.4f,new Vector3(4.8f,3.4f,4.8f),PrimitiveType.Cylinder,stone,true);
        Primitive("Tower rim",t,pos+Vector3.up*6.8f,new Vector3(5.3f,0.2f,5.3f),PrimitiveType.Cylinder,stone,false);
        for(int i=0;i<10;i++){float a=i*Mathf.PI*0.2f;Box("Tower merlon",t,pos+new Vector3(Mathf.Sin(a)*2.2f,7.4f,Mathf.Cos(a)*2.2f),new Vector3(0.7f,1,0.7f),stone,false);}
        Box("Tower banner",t,pos+new Vector3(0,4,-2.45f),new Vector3(1,2,0.05f),roofRed,false);
    }
    static void House(Transform parent,Vector3 pos,float yaw,Material roofMaterial)
    {
        var h=Root("Timber medieval house",parent).transform;h.position=pos;
        Box("Stone footing",h,pos+Vector3.up*0.25f,new Vector3(5.3f,0.5f,5.3f),stone,true);
        Box("Plaster house",h,pos+Vector3.up*2,new Vector3(5,3.5f,5),plaster,true);
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Box("Oak corner",h,pos+new Vector3(x*2.45f,2,z*2.45f),new Vector3(0.2f,3.8f,0.2f),wood,false);
        for(float y=0.7f;y<3.8f;y+=1.5f)Box("Timber band",h,pos+Vector3.up*y,new Vector3(5.1f,0.18f,5.1f),wood,false);
        var roof=Root("Gabled roof",h);roof.transform.position=pos+Vector3.up*3.75f;roof.transform.localScale=new Vector3(5.8f,2,5.8f);roof.AddComponent<MeshFilter>().sharedMesh=roofMesh;roof.AddComponent<MeshRenderer>().sharedMaterial=roofMaterial;
        Box("Chimney",h,pos+new Vector3(1.4f,5,0.8f),new Vector3(0.6f,2.4f,0.7f),stone,false);
        Box("Oak doorway",h,pos+new Vector3(0,1.2f,-2.53f),new Vector3(1.2f,2.2f,0.08f),wood,false);
        for(int side=-1;side<=1;side+=2){Box("Window frame",h,pos+new Vector3(side*1.5f,2.4f,-2.54f),new Vector3(0.9f,1.1f,0.1f),wood,false);Box("Window glow",h,pos+new Vector3(side*1.5f,2.4f,-2.6f),new Vector3(0.65f,0.85f,0.02f),gold,false);}
        h.rotation=Quaternion.Euler(0,yaw,0);
    }
    static void Well(Transform parent,Vector3 pos)
    {
        var well=Root("Village Centre Well",parent).transform;
        Primitive("Well water",well,pos+Vector3.up*0.2f,new Vector3(2.5f,0.02f,2.5f),PrimitiveType.Cylinder,water,false);
        for(int row=0;row<2;row++)for(int i=0;i<16;i++){float a=(i+row*0.5f)*Mathf.PI/8;var b=Box("Well stone",well,pos+new Vector3(Mathf.Sin(a)*1.5f,0.25f+row*0.4f,Mathf.Cos(a)*1.5f),new Vector3(0.62f,0.38f,0.42f),stone,true);b.transform.rotation=Quaternion.Euler(0,a*Mathf.Rad2Deg,0);}
        for(int side=-1;side<=1;side+=2)Box("Well oak upright",well,pos+new Vector3(side*1.8f,1.6f,0),new Vector3(0.22f,3.2f,0.22f),wood,false);
        Box("Well crossbeam",well,pos+Vector3.up*2.9f,new Vector3(4,0.25f,0.25f),wood,false);
        Primitive("Well rope",well,pos+Vector3.up*1.85f,new Vector3(0.035f,0.9f,0.035f),PrimitiveType.Cylinder,wood,false);
        Primitive("Bucket",well,pos+Vector3.up*0.95f,new Vector3(0.45f,0.25f,0.45f),PrimitiveType.Cylinder,iron,false);
    }
    static void Stall(Transform parent,Vector3 pos,Material canopy)
    {
        var t=Root("Market stall",parent).transform;
        Box("Market counter",t,pos+Vector3.up*0.8f,new Vector3(3.5f,0.15f,2),wood,true);
        for(int side=-1;side<=1;side+=2)Box("Stall post",t,pos+new Vector3(side*1.5f,1.2f,0.7f),new Vector3(0.15f,2.4f,0.15f),wood,false);
        var shade=Box("Fabric canopy",t,pos+new Vector3(0,2.4f,0),new Vector3(3.8f,0.1f,2.4f),canopy,false);shade.transform.rotation=Quaternion.Euler(12,0,0);
        for(int i=0;i<4;i++)Primitive("Produce sack",t,pos+new Vector3(-1.2f+i*0.7f,1.15f,0),new Vector3(0.5f,0.5f,0.5f),PrimitiveType.Sphere,grass,false);
    }
    static void Bench(Transform parent,Vector3 pos)
    {
        Box("Village bench",parent,pos+Vector3.up*0.55f,new Vector3(2.3f,0.18f,0.55f),wood,false);
        for(int side=-1;side<=1;side+=2)Box("Bench foot",parent,pos+new Vector3(side*0.9f,0.25f,0),new Vector3(0.15f,0.5f,0.5f),wood,false);
    }
    static void Torch(Transform parent,Vector3 pos) { Prefab("Torch",parent,pos); }
    static GameObject Prefab(string name,Transform parent,Vector3 pos)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/Medieval/"+name+".prefab");
        if(asset==null)throw new System.InvalidOperationException("Missing prefab "+name);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);Undo.RegisterCreatedObjectUndo(go,"Place "+name);go.transform.position=pos;return go;
    }
    static GameObject Root(string name,Transform parent)
    {
        var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Create "+name);if(parent!=null)go.transform.SetParent(parent,false);return go;
    }
    static GameObject Box(string name,Transform parent,Vector3 pos,Vector3 size,Material material,bool solid) => Primitive(name,parent,pos,size,PrimitiveType.Cube,material,solid);
    static GameObject Primitive(string name,Transform parent,Vector3 pos,Vector3 size,PrimitiveType type,Material material,bool solid)
    {
        var go=GameObject.CreatePrimitive(type);Undo.RegisterCreatedObjectUndo(go,"Create "+name);go.name=name;go.transform.SetParent(parent,false);go.transform.position=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
        if(!solid)Undo.DestroyObjectImmediate(go.GetComponent<Collider>());return go;
    }
    static Transform Marker(string name,Transform parent,Vector3 pos){var go=Root(name,parent);go.transform.position=pos;return go.transform;}
    static Material LoadMaterial(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Medieval/"+name+".mat");
    static Material NewMaterial(string name,Color color,float smooth=0.05f)
    {
        string path=MaterialFolder+"/"+name+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing!=null)return existing;
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);AssetDatabase.CreateAsset(m,path);Undo.RegisterCreatedObjectUndo(m,"Create village material");return m;
    }
    static Mesh CreateRoofMesh()
    {
        string path=MeshFolder+"/GabledRoof.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
        var mesh=new Mesh{name="Gabled roof"};mesh.vertices=new[]{new Vector3(-0.5f,0,-0.5f),new Vector3(0.5f,0,-0.5f),new Vector3(0,1,-0.5f),new Vector3(-0.5f,0,0.5f),new Vector3(0.5f,0,0.5f),new Vector3(0,1,0.5f)};
        mesh.triangles=new[]{0,2,1,3,4,5,0,3,5,0,5,2,1,2,5,1,5,4,0,1,4,0,4,3};mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);Undo.RegisterCreatedObjectUndo(mesh,"Create roof mesh");return mesh;
    }
    static void Folder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');string parent=path.Substring(0,slash);Folder(parent);AssetDatabase.CreateFolder(parent,path.Substring(slash+1));
    }
    static void Set(Object target,string name,Object value)
    {
        var data=new SerializedObject(target);data.FindProperty(name).objectReferenceValue=value;data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(target);
    }
    static void SetArray(Object target,string name,Object[] values)
    {
        var data=new SerializedObject(target);var array=data.FindProperty(name);array.arraySize=values.Length;for(int i=0;i<values.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=values[i];data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(target);
    }
    static RectTransform Ui(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));Undo.RegisterCreatedObjectUndo(go,"Create puzzle UI");var rt=(RectTransform)go.transform;rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0.5f,0.5f);rt.anchoredPosition=position;rt.sizeDelta=size;return rt;
    }
    static TMPro.TextMeshProUGUI Label(string name,Transform parent,string text,Vector2 pos,Vector2 size,int fontSize)
    {
        var rt=Ui(name,parent,pos,size);var label=rt.gameObject.AddComponent<TMPro.TextMeshProUGUI>();label.font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");label.text=text;label.fontSize=fontSize;label.color=new Color(0.95f,0.87f,0.66f);label.alignment=TMPro.TextAlignmentOptions.Center;label.raycastTarget=false;return label;
    }
    static UnityEngine.UI.Button Button(Transform parent,string text,Vector2 pos,Vector2 size)
    {
        var rt=Ui(text+" button",parent,pos,size);var image=rt.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(0.25f,0.20f,0.12f);var button=rt.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;Label("Label",rt,text,Vector2.zero,size,28);return button;
    }
    static void BuildPuzzleUI(CastleRunePuzzle puzzle)
    {
        var canvasObject=Root("Castle Rune Puzzle UI",null);var canvas=canvasObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=150;
        var scaler=canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=0.5f;canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var overlay=Ui("Puzzle overlay",canvasObject.transform,Vector2.zero,Vector2.zero);overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.sizeDelta=Vector2.zero;
        var backdrop=overlay.gameObject.AddComponent<UnityEngine.UI.Image>();backdrop.color=new Color(0.025f,0.02f,0.015f,0.88f);
        var panel=Ui("Rune inscription panel",overlay,Vector2.zero,new Vector2(900,520));panel.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(0.13f,0.10f,0.07f,1);
        Label("Title",panel,"THE KEEP'S THREE SEALS",new Vector2(0,195),new Vector2(820,65),42);
        Label("Inscription",panel,"First, the light that wakes the kingdom.\nThen, the watcher of the night.\nLast, the ruler who wears the crown.",new Vector2(0,75),new Vector2(800,155),28);
        var status=Label("Puzzle status",panel,"Choose three seals in the order of the inscription.",new Vector2(0,-30),new Vector2(800,60),25);
        var buttons=new[]{Button(panel,"SUN",new Vector2(-245,-115),new Vector2(205,75)),Button(panel,"MOON",new Vector2(0,-115),new Vector2(205,75)),Button(panel,"CROWN",new Vector2(245,-115),new Vector2(205,75))};
        var cancel=Button(panel,"Close / Escape",new Vector2(0,-215),new Vector2(290,55));
        Set(puzzle,"panel",overlay.gameObject);Set(puzzle,"status",status);Set(puzzle,"closeButton",cancel);SetArray(puzzle,"runeButtons",buttons.Cast<Object>().ToArray());overlay.gameObject.SetActive(false);
        var eventSystems=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)).ToArray();
        if(eventSystems.Length==0){var events=Root("EventSystem",null);events.AddComponent<UnityEngine.EventSystems.EventSystem>();events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();}
    }
}
