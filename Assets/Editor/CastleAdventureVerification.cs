using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

[InitializeOnLoad]
public static class CastleAdventureVerification
{
    static CastleAdventureVerification()
    {
        string state=SessionState.GetString("CastleAdventureVerification.State", "Idle");
        if(state=="EnteringPlayMode" || state=="Armed")
        {
            EditorApplication.playModeStateChanged-=Changed;
            EditorApplication.playModeStateChanged+=Changed;
            if(EditorApplication.isPlaying) Changed(PlayModeStateChange.EnteredPlayMode);
        }
    }
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static Component quest,player,puzzle,level,machine;
    static Rigidbody body;
    static Component[] guardians;
    static GameObject reward;
    static UnityEngine.InputSystem.Keyboard keyboard;
    static int phase,frames,guardIndex,pressCount;
    static double start,phaseStart,lastPress;
    static bool released,machineMoved;
    static Vector3 firstMachinePosition;
    static readonly List<string> logs=new List<string>();
    static readonly List<string> errors=new List<string>();
    public static void Arm()
    {
        phase=0;frames=0;guardIndex=0;pressCount=0;released=true;machineMoved=false;
        logs.Clear();errors.Clear();start=EditorApplication.timeSinceStartup;
        SessionState.EraseString("CastleAdventureVerification.Result");
        SessionState.SetString("CastleAdventureVerification.State","Armed");
        EditorApplication.playModeStateChanged+=Changed;
        EditorApplication.delayCall+=()=>{SessionState.SetString("CastleAdventureVerification.State","EnteringPlayMode");EditorApplication.isPlaying=true;};
    }
    static void Changed(PlayModeStateChange state)
    {
        SessionState.SetString("CastleAdventureVerification.State",state.ToString());
        if(state==PlayModeStateChange.EnteredPlayMode){start=EditorApplication.timeSinceStartup;Application.logMessageReceived+=Capture;EditorApplication.update+=Tick;}
        if(state==PlayModeStateChange.EnteredEditMode){EditorApplication.playModeStateChanged-=Changed;EditorApplication.update-=Tick;Application.logMessageReceived-=Capture;}
    }
    static void Capture(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception)errors.Add(message);}
    static object Read(Component c,string name)=>c.GetType().GetField(name,Flags).GetValue(c);
    static object Prop(Component c,string name)=>c.GetType().GetProperty(name,Flags).GetValue(c);
    static void Call(Component c,string name,params object[] args)=>c.GetType().GetMethod(name,Flags).Invoke(c,args);
    static void Check(bool value,string message){logs.Add(message+": "+value);Debug.Log("[Test] "+message+": "+value);if(!value)throw new Exception(message);}
    static Component Locate(string name)
    {
        foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Component>(true))if(c!=null&&c.GetType().Name==name&&c.gameObject.activeInHierarchy)return c;
        return null;
    }
    static void Teleport(Vector3 position){body.position=position;player.transform.position=position;body.linearVelocity=Vector3.zero;Physics.SyncTransforms();}
    static void Key(UnityEngine.InputSystem.Key key,bool down)
    {
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,down?new UnityEngine.InputSystem.LowLevel.KeyboardState(key):new UnityEngine.InputSystem.LowLevel.KeyboardState());
    }
    static void ChangePhase(int value){phase=value;phaseStart=EditorApplication.timeSinceStartup;frames=0;}
    static void Click(UnityEngine.UI.Button button)
    {
        Canvas.ForceUpdateCanvases();
        var centre=RectTransformUtility.WorldToScreenPoint(null,button.GetComponent<RectTransform>().TransformPoint(button.GetComponent<RectTransform>().rect.center));
        var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=centre,button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        var hits=new List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(data,hits);
        Check(hits.Count>0,"Puzzle button raycasts at "+centre);
        var receiver=UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject);
        Check(receiver==button.gameObject,"Rune button is unobstructed");
        UnityEngine.EventSystems.ExecuteEvents.Execute(receiver,data,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
    }
    static void VerifyRoute(bool reverse)
    {
        var points=quest.transform.parent.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Maze solution waypoints").Cast<Transform>().OrderBy(t=>t.name).ToArray();
        if(reverse)Array.Reverse(points);
        Check(points.Length>=15,"Winding maze has substantial route ("+points.Length+" cells)");
        int blocked=0;
        for(int i=1;i<points.Length;i++)
        {
            Vector3 a=points[i-1].position+Vector3.up*0.7f,b=points[i].position+Vector3.up*0.7f;
            foreach(var hit in Physics.SphereCastAll(a,0.32f,(b-a).normalized,(b-a).magnitude,~0,QueryTriggerInteraction.Ignore))
                if(hit.collider.name.StartsWith("Maze wall")||hit.collider.name.StartsWith("Keep ")){logs.Add("Blocked corridor="+hit.collider.name);blocked++;}
        }
        Check(blocked==0,reverse?"Reverse maze corridor clearance":"Forward maze corridor clearance");
    }
    static void Tick()
    {
        frames++;
        try
        {
            double now=EditorApplication.timeSinceStartup;
            if(now-start>30)throw new Exception("Timed out in phase "+phase+"; quest="+(quest!=null?Prop(quest,"Stage").ToString():"none"));
            if(phase==0)
            {
                if(frames<5)return;
                quest=Locate("CastleAdventure");player=Locate("MedievalInteractor");puzzle=Locate("CastleRunePuzzle");level=Locate("MedievalLevel");
                Check(quest!=null&&player!=null&&puzzle!=null&&level!=null,"Castle quest components present");
                machine=(Component)Read(quest,"machine");body=player.GetComponent<Rigidbody>();reward=(GameObject)Read(quest,"rewardChest");
                guardians=((Array)Read(quest,"guardians")).Cast<Component>().ToArray();
                Check(Prop(quest,"Stage").ToString()=="Arrival","Opening crash sequence running");
                Check(!((Behaviour)Read(quest,"player")).enabled&&!((Behaviour)Read(quest,"interactor")).enabled,"Player controls locked during arrival");
                Check(!reward.activeSelf&&guardians.All(g=>!g.gameObject.activeSelf),"Reward and guardians initially locked");
                firstMachinePosition=machine.transform.position;ChangePhase(1);return;
            }
            if(phase==1)
            {
                if(Vector3.Distance(machine.transform.position,firstMachinePosition)>0.1f)machineMoved=true;
                if(Prop(quest,"Stage").ToString()!="Village")return;
                Check(machineMoved,"Time machine travelled along crash trajectory");
                Check(Vector3.Distance(machine.transform.position,((Transform)Read(quest,"crashEnd")).position)<0.1f,"Machine landed at village centre");
                Check(((Behaviour)Read(quest,"player")).enabled&&((Behaviour)Read(quest,"interactor")).enabled,"Controls restored after player exits");
                Check(((SpriteRenderer[])Read(quest,"playerSprites")).All(s=>s.enabled),"Player visible after exit");
                Check(((GameObject)Read(quest,"crashEffect")).activeSelf,"Landing debris visible");
                keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
                Teleport(puzzle.transform.position+new Vector3(0,0.1f,1));Key(UnityEngine.InputSystem.Key.E,true);ChangePhase(2);return;
            }
            if(phase==2)
            {
                if(frames<4)return;
                Key(UnityEngine.InputSystem.Key.E,false);
                Check((bool)Prop(puzzle,"IsShowing"),"E opens rune puzzle UI near entrance");
                Check(!((Behaviour)Read(quest,"player")).enabled,"Puzzle locks movement");
                var buttons=((Array)Read(puzzle,"runeButtons")).Cast<UnityEngine.UI.Button>().ToArray();
                Click(buttons[2]);Check((int)Read(puzzle,"progress")==0&&!(bool)Prop(puzzle,"Solved"),"Wrong rune resets puzzle without opening gate");
                Click(buttons[0]);Click(buttons[1]);Click(buttons[2]);
                Check((bool)Prop(puzzle,"Solved")&&!(bool)Prop(puzzle,"IsShowing"),"Correct SUN MOON CROWN sequence solves and closes puzzle");
                Check(((Behaviour)Read(quest,"player")).enabled,"Movement restored after puzzle");
                Key(UnityEngine.InputSystem.Key.D,true);ChangePhase(3);return;
            }
            if(phase==3)
            {
                if(now-phaseStart<1.8)return;
                Key(UnityEngine.InputSystem.Key.D,false);
                var gate=(Component)Read(puzzle,"gate");
                Check((bool)Prop(gate,"IsOpen")&&gate.transform.localPosition.y < -3.5f,"Rune gate slides clear during normal movement");
                VerifyRoute(false);
                Teleport(new Vector3(0,0.1f,29));ChangePhase(4);return;
            }
            if(phase==4)
            {
                if(frames<5)return;
                Check(Prop(quest,"Stage").ToString()=="Guardians"&&guardians.All(g=>g.gameObject.activeSelf),"Entering chamber awakens exactly three guardians");
                Check(!reward.activeSelf,"Chest locked before battle completion");
                lastPress=now;released=true;ChangePhase(5);return;
            }
            if(phase==5)
            {
                if(!released){Key(UnityEngine.InputSystem.Key.E,false);released=true;return;}
                var guardian=guardians[guardIndex];
                if(guardian==null || (bool)Prop(guardian,"IsDead"))
                {
                    logs.Add("Guardian "+(guardIndex+1)+" defeated after "+pressCount+" strikes");
                    guardIndex++;pressCount=0;
                    if(guardIndex<3){Check(!reward.activeSelf,"Chest still locked after "+guardIndex+" guardian kills");lastPress=now;return;}
                    ChangePhase(6);return;
                }
                Teleport(guardian.transform.position+new Vector3(0,0.1f,-1.4f));
                if(now-lastPress<0.4)return;
                Key(UnityEngine.InputSystem.Key.E,true);released=false;lastPress=now;pressCount++;
                if(pressCount>4)throw new Exception("E attacks did not defeat guardian "+guardIndex);
                return;
            }
            if(phase==6)
            {
                if(frames<3)return;
                Check(reward.activeSelf&&Prop(quest,"Stage").ToString()=="CrystalReward","All three defeats spawn crystal chest");
                Teleport(reward.transform.position+new Vector3(1.5f,0.1f,0));ChangePhase(7);return;
            }
            if(phase==7)
            {
                if(frames<4)return;
                Key(UnityEngine.InputSystem.Key.E,true);ChangePhase(8);return;
            }
            if(phase==8)
            {
                if(frames<4)return;Key(UnityEngine.InputSystem.Key.E,false);
                Check((bool)Prop(level,"HasCrystal")&&Prop(quest,"Stage").ToString()=="Returning","Opening chest awards crystal and return objective");
                VerifyRoute(true);
                Teleport(machine.transform.position+new Vector3(2,0.1f,0));ChangePhase(9);return;
            }
            if(phase==9)
            {
                if(frames<5)return;Key(UnityEngine.InputSystem.Key.E,true);ChangePhase(10);return;
            }
            if(phase==10)
            {
                if(frames==3){Key(UnityEngine.InputSystem.Key.E,false);Check(Prop(machine,"Current").ToString()=="Repaired","E repairs crashed machine with Time Crystal");}
                if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="SampleScene")return;
                Check(true,"Repaired machine travels to existing next build scene SampleScene");
                Check(errors.Count==0,"No runtime errors recorded ("+errors.Count+")");Finish(true,null);
            }
        }
        catch(Exception e){Finish(false,e.ToString());}
    }
    static void Finish(bool success,string error)
    {
        EditorApplication.update-=Tick;
        Application.logMessageReceived-=Capture;
        if(keyboard!=null){UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);keyboard=null;}
        SessionState.SetString("CastleAdventureVerification.Result",JsonUtility.ToJson(new Output{success=success,error=error,logs=logs.ToArray(),runtimeErrors=errors.ToArray()}));
        EditorApplication.isPlaying=false;
    }
    [Serializable] class Output{public bool success;public string error;public string[] logs;public string[] runtimeErrors;}
}
