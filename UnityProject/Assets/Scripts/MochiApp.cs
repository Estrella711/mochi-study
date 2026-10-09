using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace MochiDay
{
    public partial class MochiApp : MonoBehaviour
    {
        TaskBook book; StudyService study; LocalStore store; Mutex instance; bool ownsMutex;
        PetRenderer pets;
        Texture2D rounded, pill, circle, white;
        GUIStyle label, small, title, button, field, fill, clockStyle;
        Vector2[] scroll = new Vector2[4]; Vector2 historyScroll, shopScroll, recordScroll, taskScroll;
        string draft = "", error = "", toast = "", modal = "", petNameDraft = "", selectedTaskId = "";
        bool important = true, urgent, initialized, focusDraft, compact, eyePrompt, eyeWasRunning;
        Todo editing, deleted; string deletedDate;
        FocusSettings settingsDraft;
        float celebrateUntil, toastUntil, nextDayCheck, lastResize;
        double eyeRestSeconds, saveElapsed; long lastTick;
        int historyIndex, tabIndex, lastWidth, lastHeight, titleSecond = -1;
        readonly string[] names = { "立即行动", "认真计划", "轻快处理", "留点空白" };
        readonly string[] hints = { "重要 · 紧急", "重要 · 不紧急", "不重要 · 紧急", "不重要 · 不紧急" };
        readonly string[] tabs = { "今日任务", "专注空间", "宠物小铺", "学习足迹" };
        readonly Color ink = Hex("43575B"), muted = Hex("819193"), cream = Hex("FAF8F1"), mint = Hex("64998A");
        readonly Color[] accents = { Hex("BA7D73"), Hex("6F9786"), Hex("9A8DAE"), Hex("A7956A") };
        readonly Color[] cards = { Hex("F8EAE5"), Hex("E8F1E9"), Hex("EEEAF5"), Hex("F5EFDE") };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (FindObjectOfType<MochiApp>() == null) new GameObject("Mochi Study App").AddComponent<MochiApp>(); }
        public static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs(); for (int i = 0; i < args.Length-1; i++) if (args[i] == key) return args[i+1]; return null;
        }
        void Start()
        {
            Application.targetFrameRate = 30; Application.runInBackground = true;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--mochi-selftest") >= 0)
            { CoreChecks.Run(Argument("--report") ?? Path.Combine(Application.persistentDataPath, "test-report.txt")); Application.Quit(CoreChecks.Failures == 0 ? 0 : 1); return; }
            string path = Argument("--data-dir") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MochiDay");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            string token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToLowerInvariant())).Replace('\\', '_').Replace('/', '_');
            instance = new Mutex(false, "Local\\MochiDay_" + token);
            try { ownsMutex = instance.WaitOne(0); } catch (AbandonedMutexException) { ownsMutex = true; }
            if (!ownsMutex) { Debug.LogWarning("Mochi app already running for this data directory."); Application.Quit(); return; }
#endif
            store = new LocalStore(path); var data = store.Load(); string warning = store.Warning;
            book = new TaskBook(data, DateTime.Now); study = new StudyService(data); study.RecoverAfterRestart();
            if(book.Today.tasks.Exists(t=>t.id==data.timer.taskId&&!t.done)) selectedTaskId=data.timer.taskId;
            pets = new PetRenderer();
            if (!store.Writable) Notice(warning, 20);
            else { Persist(); if (!string.IsNullOrEmpty(warning)) Notice(warning, 15); }
            Screen.SetResolution(data.width, data.height, FullScreenMode.Windowed);
            lastWidth = data.width; lastHeight = data.height; lastTick = System.Diagnostics.Stopwatch.GetTimestamp();
            Invoke("InitWindow", 1f); initialized = true;
        }
        void InitWindow()
        {
            WindowsWindow.Corner();
            if (book.Data.topmost && !WindowsWindow.SetTopmost(true)) { book.Data.topmost = false; Persist(); }
        }
        void Update()
        {
            if (!initialized) return;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            double delta = (now-lastTick)/(double)System.Diagnostics.Stopwatch.Frequency; lastTick = now;
            if (store.Writable)
            {
                study.Tick(delta, DateTime.Now);
                if (study.FocusFinishedThisTick)
                { celebrateUntil = Time.unscaledTime+4; Notice("这轮完成啦！获得 "+study.LastReward+" 枚金币。",8); eyePrompt=false; Persist(); WindowsWindow.Notify(book.Data.settings.soundEnabled); }
                if (study.BreakFinishedThisTick)
                { Notice("休息结束，准备好后再开始下一轮。",8); Persist(); WindowsWindow.Notify(book.Data.settings.soundEnabled); }
                if (study.EyeReminderThisTick && !study.FocusFinishedThisTick)
                { eyePrompt=true; Persist(); WindowsWindow.Notify(book.Data.settings.soundEnabled); }
                if (book.Data.timer.running) { saveElapsed += delta > 0 && delta <= 5 ? delta : 0; if (saveElapsed>=5) { saveElapsed=0; Persist(); } }
                if (modal=="eyeRest" && delta>0 && delta<=5) eyeRestSeconds = Math.Max(0,eyeRestSeconds-delta);
            }
            if (Time.unscaledTime >= nextDayCheck)
            {
                nextDayCheck = Time.unscaledTime+1;
                if (book.SelectDate(DateTime.Now)) { if (modal=="edit") modal=""; editing=null; deleted=null; selectedTaskId=""; scroll=new Vector2[4]; if(Persist()) Notice("新的一天，慢慢开始吧。昨日任务已留在历史中。"); }
            }
            int second = (int)Math.Ceiling(book.Data.timer.remainingSeconds);
            if (second != titleSecond)
            { titleSecond=second; WindowsWindow.Title(TimeText()+" · "+PhaseText()+" · "+book.Data.pet.Name+"的学习时间"); }
            if (Screen.width!=lastWidth || Screen.height!=lastHeight)
            { lastWidth=Screen.width; lastHeight=Screen.height; lastResize=Time.unscaledTime; }
            if (lastResize>0 && Time.unscaledTime-lastResize>1)
            {
                lastResize=0;
                int minW=compact ? 360 : 680, minH=compact ? 460 : 480;
                if (Screen.width<minW || Screen.height<minH) Screen.SetResolution(Math.Max(minW,Screen.width),Math.Max(minH,Screen.height),FullScreenMode.Windowed);
                if (!compact) { book.Data.width=Mathf.Clamp(Screen.width,680,1600); book.Data.height=Mathf.Clamp(Screen.height,480,1100); Persist(); }
            }
        }
        void OnApplicationFocus(bool focused) { if (focused && initialized) nextDayCheck=0; }
        void OnApplicationQuit()
        {
            if (initialized) { study.Pause(); Persist(); }
            if (ownsMutex && instance!=null) instance.ReleaseMutex(); if(instance!=null) instance.Dispose();
        }
        void OnDestroy() { if(pets!=null) pets.Dispose(); foreach(var t in new[]{rounded,pill,circle}) if(t!=null) Destroy(t); }
        bool Persist()
        { if(store==null) return false; if(store.Save(book.Data)) return true; Notice(store.Warning ?? "当前处于只读模式",12); return false; }
        void Notice(string text,float seconds=5) { toast=text; toastUntil=Time.unscaledTime+seconds; }
        bool Apply(bool okay,string message=null)
        { if(!okay) { Notice(study.LastError ?? "暂时无法执行。"); return false; } titleSecond=-1; if(!Persist()) return false; if(message!=null) Notice(message); return true; }
        static Color Hex(string hex) { Color c; ColorUtility.TryParseHtmlString("#"+hex,out c); return c; }
        static Texture2D Shape(bool ellipse,int radius)
        {
            var t=new Texture2D(64,64,TextureFormat.RGBA32,false); t.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<64;y++) for(int x=0;x<64;x++)
            {
                float dx=Mathf.Max(Mathf.Abs(x-31.5f)-(31.5f-radius),0),dy=Mathf.Max(Mathf.Abs(y-31.5f)-(31.5f-radius),0);
                float d=ellipse ? new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f).magnitude : Mathf.Sqrt(dx*dx+dy*dy)/radius;
                t.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01((1-d)*24)));
            }
            t.Apply(); return t;
        }
        void Styles()
        {
            if(rounded!=null) return;
            rounded=Shape(false,10); pill=Shape(false,30); circle=Shape(true,32); white=Texture2D.whiteTexture;
            fill=new GUIStyle{border=new RectOffset(16,16,16,16)}; fill.normal.background=rounded;
            var font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},20);
            label=new GUIStyle(GUI.skin.label){font=font,fontSize=19,wordWrap=true,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(0,0,0,0)}; label.normal.textColor=ink;
            small=new GUIStyle(label){fontSize=15}; small.normal.textColor=muted;
            title=new GUIStyle(label){fontSize=28,fontStyle=FontStyle.Bold};
            clockStyle=new GUIStyle(title){fontSize=72,alignment=TextAnchor.MiddleCenter};
            button=new GUIStyle(label){alignment=TextAnchor.MiddleCenter,fontSize=17,border=new RectOffset(12,12,12,12),padding=new RectOffset(2,2,0,0)};
            button.normal.background=rounded; button.hover.background=rounded; button.active.background=rounded;
            button.normal.textColor=ink; button.hover.textColor=ink; button.active.textColor=ink;
            field=new GUIStyle(GUI.skin.textField){font=font,fontSize=20,border=new RectOffset(12,12,12,12),padding=new RectOffset(12,12,12,8)};
            field.normal.background=rounded; field.focused.background=rounded; field.normal.textColor=ink; field.focused.textColor=ink;
        }
        void Box(Rect r,Color c,Texture2D t=null) { var old=GUI.color; GUI.color=c; if(t==null) GUI.Box(r,GUIContent.none,fill); else GUI.DrawTexture(r,t); GUI.color=old; }
        void Text(Rect r,string text,GUIStyle style=null,Color? color=null)
        { var s=style??label; var old=s.normal.textColor; if(color.HasValue) s.normal.textColor=color.Value; GUI.Label(r,text,s); s.normal.textColor=old; }
        void Center(Rect r,string text,GUIStyle style=null,Color? color=null)
        { var s=style??label; var old=s.alignment; s.alignment=TextAnchor.MiddleCenter; Text(r,text,s,color); s.alignment=old; }
        bool Button(Rect r,string text,Color? color=null)
        { var old=GUI.backgroundColor; GUI.backgroundColor=color??Color.white; bool clicked=GUI.Button(r,text,button); GUI.backgroundColor=old; return clicked; }
        void SetCompact(bool value)
        {
            if(value==compact) return;
            if(value) { book.Data.width=Mathf.Clamp(Screen.width,680,1600); book.Data.height=Mathf.Clamp(Screen.height,480,1100); Persist(); }
            compact=value; Screen.SetResolution(value?440:book.Data.width,value?540:book.Data.height,FullScreenMode.Windowed);
            lastResize=0; Invoke("InitWindow",.3f);
        }
        void ToggleTopmost()
        {
            bool value=!book.Data.topmost;
            if(WindowsWindow.SetTopmost(value)) { book.Data.topmost=value; Persist(); } else Notice("置顶仅在 Windows 可执行版中可用。");
        }
        void OpenEditor(Todo task,int quadrant=1)
        {
            if(!store.Writable) { Notice(store.Warning,15); return; }
            editing=task; draft=task==null?"":task.title; important=task==null?quadrant<2:task.important; urgent=task==null?quadrant%2==0:task.urgent;
            error=""; modal="edit"; focusDraft=true;
        }
        void Commit()
        {
            try { if(editing==null) book.Add(draft,important,urgent); else book.Edit(editing,draft,important,urgent); modal=""; if(Persist()) Notice("已保存，按自己的节奏来。"); }
            catch(ArgumentException e) { error=e.Message; }
        }
        void OnGUI()
        {
            if(!initialized) return; Styles();
            float s=Mathf.Min(Screen.width/(compact?440f:1100f),Screen.height/(compact?540f:720f));
            float w=Screen.width/s,h=Screen.height/s;
            GUI.matrix=Matrix4x4.Scale(new Vector3(s,s,1)); Box(new Rect(0,0,w,h),cream,white);
            var evt=Event.current;
            if(evt.type==EventType.KeyDown)
            {
                if(evt.keyCode==KeyCode.Escape) { modal=""; evt.Use(); }
                else if(evt.control && evt.keyCode==KeyCode.N) { SetCompact(false); OpenEditor(null); evt.Use(); }
                else if(modal=="edit" && evt.control && (evt.keyCode==KeyCode.Return || evt.keyCode==KeyCode.KeypadEnter)) { Commit(); evt.Use(); }
            }
            bool enabled=GUI.enabled; GUI.enabled=modal=="";
            if(compact) DrawCompact(w,h);
            else
            {
                Text(new Rect(24,17,320,38),"糯米学习",title);
                Text(new Rect(25,57,480,25),DateTime.Now.ToString("MM 月 dd 日 · dddd")+"   /   和搭子一起，慢慢变好",small);
                Box(new Rect(w-415,25,102,35),Hex("F5E9BF")); Center(new Rect(w-415,25,102,35),"金币 "+book.Data.coins,small,Hex("94784C"));
                if(Button(new Rect(w-302,25,74,35),"设置")) { settingsDraft=book.Data.settings.Copy(); modal="settings"; }
                if(Button(new Rect(w-217,25,90,35),"迷你钟")) SetCompact(true);
                if(Button(new Rect(w-116,25,92,35),book.Data.topmost?"置顶：开":"置顶：关",book.Data.topmost?Hex("DCEEE3"):Color.white)) ToggleTopmost();
                for(int i=0;i<4;i++) if(Button(new Rect(24+i*149,96,137,34),tabs[i],tabIndex==i?Hex("DCEEE3"):Hex("EFEEE7"))) tabIndex=i;
                Rect area=new Rect(24,145,w-350,h-204);
                if(tabIndex==0) DrawTasks(area); else if(tabIndex==1) DrawFocus(area); else if(tabIndex==2) DrawShop(area); else DrawStats(area);
                DrawCompanion(new Rect(w-302,145,278,h-171));
                Text(new Rect(25,h-44,w-352,34),store.Writable?"今日 "+book.Today.Completed+"/"+book.Today.tasks.Count+" 件任务   ·   专注 "+MinutesToday+" 分钟   ·   一次只做一件事":"只读："+store.Warning,small,store.Writable?muted:accents[0]);
            }
            if(eyePrompt && modal=="") DrawEyeBanner(w,compact);
            GUI.enabled=enabled;
            if(Time.unscaledTime<toastUntil && modal=="")
            {
                Box(new Rect(20,h-63,w-40,43),ink); Text(new Rect(32,h-60,w-144,37),toast,small,Color.white);
                if(deleted!=null && deletedDate==book.Today.date && modal=="" && Button(new Rect(w-102,h-56,70,29),"撤销"))
                { book.Today.tasks.Add(deleted); deleted=null; if(Persist()) Notice("任务已恢复。"); }
            }
            if(modal!="")
            {
                Box(new Rect(0,0,w,h),new Color(.2f,.3f,.3f,.23f),white);
                if(modal=="edit") DrawEditor(new Rect((w-540)/2,(h-350)/2,540,350));
                else if(modal=="history") DrawHistory(new Rect((w-650)/2,(h-470)/2,650,470));
                else if(modal=="pets") DrawPetPicker(new Rect((w-680)/2,(h-485)/2,680,485));
                else if(modal=="settings") DrawSettings(new Rect((w-650)/2,(h-545)/2,650,545));
                else if(modal=="eyeRest") { float mw=Math.Min(540,w-40); DrawEyeRest(new Rect((w-mw)/2,(h-310)/2,mw,310)); }
                if(Time.unscaledTime<toastUntil)
                { Box(new Rect(20,h-63,w-40,43),ink); Text(new Rect(32,h-60,w-64,37),toast,small,Color.white); }
            }
            GUI.matrix=Matrix4x4.identity;
        }
        void DrawTasks(Rect r)
        {
            Text(new Rect(r.x,r.y,r.width-260,30),"把今天安排得轻一点",label);
            if(Button(new Rect(r.xMax-214,r.y,93,31),"任务历史")) { modal="history"; historyIndex=book.Data.days.Count-1; historyScroll=Vector2.zero; }
            if(Button(new Rect(r.xMax-111,r.y,111,31),"＋ 新任务",Hex("DCEEE3"))) OpenEditor(null);
            float gap=14,cw=(r.width-gap)/2,ch=(r.height-47-gap)/2;
            bool old=GUI.enabled; GUI.enabled=old&&store.Writable;
            for(int q=0;q<4;q++) DrawQuadrant(q,new Rect(r.x+(q%2)*(cw+gap),r.y+47+(q/2)*(ch+gap),cw,ch));
            GUI.enabled=old;
        }
        void DrawQuadrant(int q,Rect r)
        {
            Box(r,cards[q]); Text(new Rect(r.x+16,r.y+10,r.width-70,27),names[q],label,accents[q]);
            Text(new Rect(r.x+16,r.y+39,r.width-40,20),hints[q],small,accents[q]);
            if(Button(new Rect(r.xMax-42,r.y+16,28,28),"＋")) OpenEditor(null,q);
            var tasks=book.Today.tasks.FindAll(t=>t.Quadrant==q);
            Rect view=new Rect(r.x+10,r.y+67,r.width-20,r.height-79);
            if(tasks.Count==0) { Text(new Rect(view.x+7,view.y+12,view.width-14,49),q==3?"散步、发呆、小小的快乐…":"点 ＋ 放进一件小事",small); return; }
            scroll[q]=GUI.BeginScrollView(view,scroll[q],new Rect(0,0,view.width-17,Mathf.Max(view.height,tasks.Count*62)),false,false);
            for(int i=0;i<tasks.Count;i++)
            {
                Todo task=tasks[i]; float y=i*62,width=view.width-19;
                Box(new Rect(0,y,width,55),new Color(1,1,1,task.done?.5f:.84f));
                if(Button(new Rect(7,y+15,25,25),task.done?"✓":"",task.done?Hex("B7D9C6"):Hex("E5EAE4")))
                { book.Toggle(task); if(task.done) { if(selectedTaskId==task.id) selectedTaskId=""; celebrateUntil=Time.unscaledTime+3; Notice(book.Data.pet.Name+"为你开心，完成了一件小事！"); } else Notice("已取消完成，状态已更新。"); Persist(); }
                if(GUI.Button(new Rect(42,y+5,width-94,45),task.title,new GUIStyle(task.done?small:label){fontSize=17,normal={textColor=task.done?muted:ink}})) OpenEditor(task);
                if(Button(new Rect(width-40,y+16,33,24),"×")) { deleted=task; deletedDate=book.Today.date; if(selectedTaskId==task.id) selectedTaskId=""; book.Delete(task); if(Persist()) Notice("任务已删除，可撤销。",8); }
            }
            GUI.EndScrollView();
        }
        int SessionsToday { get { return book.Data.focusRecords.Count(f=>f.date==book.Today.date); } }
        int MinutesToday { get { return book.Data.focusRecords.Where(f=>f.date==book.Today.date).Sum(f=>f.minutes); } }
        void DrawCompanion(Rect r)
        {
            Box(r,Hex("F0F2E7")); Text(new Rect(r.x+18,r.y+13,r.width-126,25),"我的日程搭子",small,mint);
            bool old=GUI.enabled; GUI.enabled=old&&store.Writable;
            if(Button(new Rect(r.xMax-97,r.y+11,81,29),"换搭子")) { petNameDraft=book.Data.pet.Name; error=""; modal="pets"; }
            GUI.enabled=old;
            var p=book.Data.pet; bool happy=Time.unscaledTime<celebrateUntil;
            pets.Draw(new Rect(r.x+14,r.y+41,r.width-28,215),p.kind,p.hat,p.outfit,p.accessory,happy,Time.unscaledTime,ink);
            Center(new Rect(r.x+14,r.y+253,r.width-28,28),p.Name+" · "+(p.kind=="cat"?"银渐层小猫":"软糯小兔"),small,ink);
            Center(new Rect(r.x+14,r.y+285,r.width-28,32),happy?"你又向前走了一小步。":book.Data.timer.running?"我陪你，一起认真一会儿。":"安心陪着你，按自己的节奏来。",small);
            Meter(new Rect(r.x+19,r.y+331,r.width-38,36),"精力",Math.Min(100,book.Today.Energy+SessionsToday*2),Hex("83AF93"));
            Meter(new Rect(r.x+19,r.y+373,r.width-38,36),"心情",Math.Min(100,book.Today.Mood+SessionsToday*3),Hex("D7A090"));
            Rect timer=new Rect(r.x+14,r.yMax-131,r.width-28,117); Box(timer,Hex("E4EBDE"));
            Text(new Rect(timer.x+13,timer.y+9,112,22),PhaseText(),small,mint);
            Text(new Rect(timer.xMax-105,timer.y+4,91,34),TimeText(),new GUIStyle(title){fontSize=29,alignment=TextAnchor.MiddleRight});
            Text(new Rect(timer.x+13,timer.y+38,timer.width-26,22),"今日 "+MinutesToday+" 分钟 / "+SessionsToday+" 轮",small);
            GUI.enabled=old&&store.Writable; if(Button(new Rect(timer.x+12,timer.y+72,timer.width-24,32),PrimaryLabel(),Color.white)) PrimaryAction(); GUI.enabled=old;
        }
        void Meter(Rect r,string name,int value,Color color)
        { Text(new Rect(r.x,r.y,90,21),name,small); Text(new Rect(r.xMax-42,r.y,42,21),value+"",small); Box(new Rect(r.x,r.y+28,r.width,7),Hex("DFE5D8"),pill); Box(new Rect(r.x,r.y+28,r.width*value/100f,7),color,pill); }
        void DrawEditor(Rect r)
        {
            Box(r,cream); Text(new Rect(r.x+26,r.y+20,380,35),editing==null?"种下一件小事":"调整这件小事",title);
            if(Button(new Rect(r.xMax-58,r.y+20,32,30),"×")) modal="";
            Text(new Rect(r.x+27,r.y+69,420,25),"任务名称   ·   1–80 字",small);
            GUI.SetNextControlName("TaskTitle"); draft=GUI.TextField(new Rect(r.x+26,r.y+101,r.width-52,52),draft,80,field);
            if(focusDraft && Event.current.type==EventType.Repaint) { GUI.FocusControl("TaskTitle"); focusDraft=false; }
            if(Button(new Rect(r.x+26,r.y+177,232,38),(important?"✓ ":"○ ")+"重要",important?Hex("DCEEE3"):Color.white)) important=!important;
            if(Button(new Rect(r.x+274,r.y+177,240,38),(urgent?"✓ ":"○ ")+"紧急",urgent?Hex("F5DFD7"):Color.white)) urgent=!urgent;
            Text(new Rect(r.x+27,r.y+224,r.width-54,36),string.IsNullOrEmpty(error)?"保存：Ctrl + Enter    /    取消：Esc":error,small,string.IsNullOrEmpty(error)?muted:accents[0]);
            if(Button(new Rect(r.x+26,r.yMax-58,120,34),"取消")) modal="";
            if(Button(new Rect(r.xMax-176,r.yMax-58,150,34),"保存任务",Hex("CDE5D5"))) Commit();
        }
        void DrawHistory(Rect r)
        {
            Box(r,cream); Text(new Rect(r.x+25,r.y+18,400,36),"走过的小日子",title);
            if(Button(new Rect(r.xMax-56,r.y+22,30,30),"×")) modal="";
            GUI.enabled=historyIndex>0; if(Button(new Rect(r.x+25,r.y+72,60,30),"←")) { historyIndex--; historyScroll=Vector2.zero; }
            GUI.enabled=historyIndex<book.Data.days.Count-1; if(Button(new Rect(r.xMax-85,r.y+72,60,30),"→")) { historyIndex++; historyScroll=Vector2.zero; } GUI.enabled=true;
            var day=book.Data.days[historyIndex]; Text(new Rect(r.x+104,r.y+72,420,30),day.date+"   ·   完成 "+day.Completed+" / "+day.tasks.Count,label);
            Rect view=new Rect(r.x+25,r.y+121,r.width-50,r.height-185);
            historyScroll=GUI.BeginScrollView(view,historyScroll,new Rect(0,0,view.width-20,Mathf.Max(view.height,day.tasks.Count*48)));
            if(day.tasks.Count==0) Text(new Rect(10,20,460,40),"这一天还没有记录。",small);
            for(int i=0;i<day.tasks.Count;i++) { var task=day.tasks[i]; Text(new Rect(8,i*48,view.width-32,44),(task.done?"✓  ":"○  ")+task.title+"\n     "+hints[task.Quadrant],small); }
            GUI.EndScrollView(); Text(new Rect(r.x+25,r.yMax-52,r.width-50,35),"历史只读。每天自动切换，昨日任务不会自动移入今天。",small);
        }
    }
}
