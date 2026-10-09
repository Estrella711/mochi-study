using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MochiDay
{
    public partial class MochiApp
    {
        string TimeText()
        { int sec=(int)Math.Ceiling(Math.Max(0,book.Data.timer.remainingSeconds)); return (sec/60).ToString("00")+":"+(sec%60).ToString("00"); }
        string PhaseText()
        {
            var t=book.Data.timer;
            if(t.phase=="focusDone") return "专注已完成";
            if(t.phase=="breakDone") return "休息已结束";
            if(t.phase=="break") return t.running?"放松休息":"休息已暂停";
            return t.running?"正在专注":string.IsNullOrEmpty(t.sessionId)?"准备专注":"专注已暂停";
        }
        string PrimaryLabel()
        {
            var t=book.Data.timer;
            if(t.phase=="focusDone") return "进入休息";
            if(t.phase=="breakDone") return "开始下一轮";
            if(t.running) return "暂停计时";
            if(t.phase=="break") return "继续休息";
            return string.IsNullOrEmpty(t.sessionId)?"开始专注":"继续专注";
        }
        void StartSelectedFocus()
        {
            var selected=book.Today.tasks.Find(t=>t.id==selectedTaskId);
            Apply(study.BeginFocus(selected==null?"":selected.id,selected==null?"自由专注":selected.title));
        }
        void PrimaryAction()
        {
            var t=book.Data.timer;
            if(t.phase=="focusDone") Apply(study.StartBreak(),"该休息一下啦。");
            else if(t.phase=="breakDone") { study.SkipBreak(); StartSelectedFocus(); }
            else if(t.running) { study.Pause(); Persist(); titleSecond=-1; }
            else if(t.phase=="focus" && string.IsNullOrEmpty(t.sessionId)) StartSelectedFocus();
            else Apply(study.Resume());
        }
        void DrawFocus(Rect r)
        {
            Text(new Rect(r.x,r.y,r.width,31),"留一段时间，只做眼前这一件事",label);
            Rect timer=new Rect(r.x,r.y+47,r.width,310); Box(timer,Hex("E8F1E9"));
            Center(new Rect(timer.x+20,timer.y+19,timer.width-40,26),PhaseText(),label,mint);
            Center(new Rect(timer.x+20,timer.y+55,timer.width-40,96),TimeText(),clockStyle);
            var t=book.Data.timer;
            float progress=(float)(1-t.remainingSeconds/Math.Max(1,t.phaseDurationSeconds));
            Box(new Rect(timer.x+55,timer.y+166,timer.width-110,8),Hex("D7E5D9"),pill);
            if(progress>0) Box(new Rect(timer.x+55,timer.y+166,(timer.width-110)*Mathf.Clamp01(progress),8),mint,pill);
            string linked=string.IsNullOrEmpty(t.sessionId)?"选择下方任务，或自由专注":string.IsNullOrEmpty(t.taskTitle)?"自由专注":t.taskTitle;
            Center(new Rect(timer.x+26,timer.y+188,timer.width-52,29),linked,small,ink);
            bool old=GUI.enabled; GUI.enabled=old&&store.Writable;
            float bw=Math.Min(235,(timer.width-90)/2);
            if(Button(new Rect(timer.center.x-bw-8,timer.y+233,bw,43),PrimaryLabel(),Hex("CDE5D5"))) PrimaryAction();
            if(Button(new Rect(timer.center.x+8,timer.y+233,bw,43),t.phase=="focus"?"重新计时":"跳过休息",Color.white))
            { Apply(t.phase=="focus"?study.Reset():study.SkipBreak(),"已准备新一轮，取消的计时不发金币。"); eyePrompt=false; }
            GUI.enabled=old;
            string duration=t.phase=="break"?"本次休息 "+(t.phaseDurationSeconds/60).ToString("0")+" 分钟":"本轮专注 "+t.sessionFocusMinutes+" 分钟";
            Center(new Rect(timer.x+15,timer.y+281,timer.width-30,22),duration+" · 已完成 "+t.cycle+" 轮 · 完成整轮才发金币",small);
            Text(new Rect(r.x,r.y+374,r.width,25),"关联今天的任务",label);
            Rect view=new Rect(r.x,r.y+407,r.width,Math.Max(50,r.height-407));
            var tasks=book.Today.tasks.Where(x=>!x.done).ToList();
            taskScroll=GUI.BeginScrollView(view,taskScroll,new Rect(0,0,view.width-20,Math.Max(view.height,(tasks.Count+1)*43)));
            GUI.enabled=old&&store.Writable&&t.phase=="focus"&&!t.running&&string.IsNullOrEmpty(t.sessionId);
            if(Button(new Rect(0,0,view.width-23,35),(selectedTaskId==""?"✓  ":"")+"自由专注",selectedTaskId==""?Hex("DCEEE3"):Color.white)) selectedTaskId="";
            for(int i=0;i<tasks.Count;i++)
            {
                var task=tasks[i];
                if(Button(new Rect(0,(i+1)*43,view.width-23,35),(selectedTaskId==task.id?"✓  ":"")+task.title,selectedTaskId==task.id?Hex("DCEEE3"):Color.white)) selectedTaskId=task.id;
            }
            GUI.enabled=old; GUI.EndScrollView();
        }
        void DrawShop(Rect r)
        {
            Text(new Rect(r.x,r.y,r.width,30),"小小奖励，让搭子更可爱",label);
            Text(new Rect(r.x,r.y+35,r.width,23),"专注 1 分钟 = 1 金币，整轮完成后到账。购买一次，随时换装。",small);
            Rect view=new Rect(r.x,r.y+74,r.width,r.height-74);
            float cw=(view.width-32)/2,ch=265;
            shopScroll=GUI.BeginScrollView(view,shopScroll,new Rect(0,0,view.width-20,3*(ch+14)));
            bool old=GUI.enabled;
            for(int i=0;i<PetCatalog.Items.Length;i++)
            {
                var item=PetCatalog.Items[i]; var p=book.Data.pet;
                Rect card=new Rect((i%2)*(cw+12),(i/2)*(ch+14),cw,ch); Box(card,i%2==0?Hex("F0F2E7"):Hex("EEEAF5"));
                string hat=item.slot=="hat"?item.id:p.hat,outfit=item.slot=="outfit"?item.id:p.outfit,acc=item.slot=="accessory"?item.id:p.accessory;
                pets.Draw(new Rect(card.x+12,card.y+1,card.width-24,137),p.kind,hat,outfit,acc,false,Time.unscaledTime,ink);
                Text(new Rect(card.x+15,card.y+133,card.width-30,28),item.name,label);
                Text(new Rect(card.x+15,card.y+164,card.width-30,36),item.description,small);
                bool owned=book.Data.ownedItems.Contains(item.id);
                bool equipped=(p.hat==item.id||p.outfit==item.id||p.accessory==item.id);
                GUI.enabled=old&&store.Writable&&(owned||book.Data.coins>=item.price);
                string caption=equipped?"已穿戴 · 脱下":owned?"给搭子穿上":book.Data.coins<item.price?item.price+" 金币 · 还差 "+(item.price-book.Data.coins):item.price+" 金币 · 购买";
                if(Button(new Rect(card.x+14,card.y+215,card.width-28,34),caption,equipped?Hex("CDE5D5"):Color.white))
                {
                    if(equipped) Apply(study.Unequip(item.slot),"已经收进衣橱。");
                    else if(owned) Apply(study.Equip(item.id),"新造型，真可爱。");
                    else if(Apply(study.Buy(item.id))) Apply(study.Equip(item.id),"已购买并穿戴 "+item.name+"。");
                }
                GUI.enabled=old;
            }
            GUI.EndScrollView();
        }
        void DrawStats(Rect r)
        {
            Text(new Rect(r.x,r.y,r.width,31),"每天一点点，进步看得见",label);
            int totalMinutes=book.Data.focusRecords.Sum(f=>f.minutes), earned=book.Data.focusRecords.Sum(f=>f.coins);
            float cw=(r.width-24)/3;
            string[] tops={"今日专注","累计专注","累计金币"}, values={MinutesToday+" 分钟",totalMinutes+" 分钟",earned+" 枚"};
            for(int i=0;i<3;i++)
            {
                var card=new Rect(r.x+i*(cw+12),r.y+47,cw,77); Box(card,cards[i+1]);
                Text(new Rect(card.x+15,card.y+8,card.width-30,23),tops[i],small);
                Text(new Rect(card.x+15,card.y+34,card.width-30,30),values[i],new GUIStyle(title){fontSize=23});
            }
            Rect graph=new Rect(r.x,r.y+138,r.width,207); Box(graph,Color.white);
            Text(new Rect(graph.x+17,graph.y+11,graph.width-34,24),"近七天 · 每日目标 "+book.Data.settings.dailyGoalMinutes+" 分钟",small);
            int[] mins=new int[7]; int max=book.Data.settings.dailyGoalMinutes;
            for(int i=0;i<7;i++) { string date=DateTime.Today.AddDays(i-6).ToString("yyyy-MM-dd"); mins[i]=book.Data.focusRecords.Where(f=>f.date==date).Sum(f=>f.minutes); max=Math.Max(max,mins[i]); }
            float space=(graph.width-34)/7,barW=Math.Min(42,space*.5f),baseY=graph.y+165;
            float goalY=baseY-106f*book.Data.settings.dailyGoalMinutes/max;
            Box(new Rect(graph.x+17,goalY,graph.width-34,1),Hex("DCE6DD"),white);
            for(int i=0;i<7;i++)
            {
                float x=graph.x+17+i*space,bh=106f*mins[i]/max;
                if(bh>0) Box(new Rect(x+(space-barW)/2,baseY-bh,barW,bh),i==6?mint:Hex("B6D1BC"));
                Center(new Rect(x,baseY-bh-22,space,21),mins[i]+"",small);
                Center(new Rect(x,baseY+7,space,24),DateTime.Today.AddDays(i-6).ToString("MM/dd"),small);
            }
            Text(new Rect(r.x,r.y+359,r.width,26),"最近专注",label);
            Rect view=new Rect(r.x,r.y+398,r.width,Math.Max(50,r.height-398));
            var records=book.Data.focusRecords.AsEnumerable().Reverse().Take(100).ToList();
            recordScroll=GUI.BeginScrollView(view,recordScroll,new Rect(0,0,view.width-20,Math.Max(view.height,records.Count*55)));
            if(records.Count==0) Text(new Rect(10,9,view.width-30,55),"还没有记录，先和搭子完成一轮专注吧。",small);
            for(int i=0;i<records.Count;i++)
            {
                var f=records[i]; float y=i*55; Box(new Rect(0,y,view.width-23,49),Hex("F0F2E7"));
                Text(new Rect(12,y+3,view.width-163,25),string.IsNullOrEmpty(f.taskTitle)?"自由专注":f.taskTitle,small,ink);
                Text(new Rect(12,y+27,view.width-160,19),f.date,small);
                Text(new Rect(view.width-146,y+8,116,30),f.minutes+" 分钟  +"+f.coins,small,mint);
            }
            GUI.EndScrollView();
        }
        void DrawPetPicker(Rect r)
        {
            Box(r,cream); Text(new Rect(r.x+26,r.y+18,440,37),"选择你的学习搭子",title);
            if(Button(new Rect(r.xMax-56,r.y+22,30,30),"×")) modal="";
            for(int i=0;i<2;i++)
            {
                string kind=i==0?"cat":"bunny"; bool chosen=book.Data.pet.kind==kind;
                Rect card=new Rect(r.x+26+i*321,r.y+72,307,231); Box(card,chosen?Hex("DCEEE3"):Hex("F0F2E7"));
                var p=book.Data.pet;
                pets.Draw(new Rect(card.x+10,card.y+2,card.width-20,179),kind,p.hat,p.outfit,p.accessory,false,Time.unscaledTime,ink);
                if(Button(new Rect(card.x+20,card.y+181,card.width-40,34),(chosen?"✓ ":"")+(i==0?"银渐层小猫":"糯米小兔"),Color.white))
                { if(Apply(study.SelectPet(kind))) { petNameDraft=book.Data.pet.Name; error=""; } }
            }
            Text(new Rect(r.x+26,r.y+321,180,26),"给搭子起个名字",label);
            petNameDraft=GUI.TextField(new Rect(r.x+26,r.y+357,r.width-206,44),petNameDraft,12,field);
            if(Button(new Rect(r.xMax-165,r.y+357,139,44),"保存名字",Hex("CDE5D5")))
            { if(Apply(study.RenamePet(petNameDraft),"搭子现在叫 "+petNameDraft+"。")) modal=""; else error=study.LastError??store.Warning; }
            Text(new Rect(r.x+26,r.y+419,r.width-52,41),string.IsNullOrEmpty(error)?"1–12 个字符 · 小猫和小兔会各自记住名字，衣橱共享。":error,small,string.IsNullOrEmpty(error)?muted:accents[0]);
        }
        int SettingRow(Rect r,float y,string name,int value,int step,int min,int max,string unit)
        {
            Text(new Rect(r.x+26,r.y+y,270,31),name,label);
            if(Button(new Rect(r.xMax-262,r.y+y,35,31),"−")) value=Math.Max(min,value-step);
            Center(new Rect(r.xMax-221,r.y+y,147,31),value+" "+unit,small,ink);
            if(Button(new Rect(r.xMax-68,r.y+y,35,31),"＋")) value=Math.Min(max,value+step);
            return value;
        }
        void DrawSettings(Rect r)
        {
            Box(r,cream); Text(new Rect(r.x+26,r.y+18,420,37),"找到适合你的节奏",title);
            if(Button(new Rect(r.xMax-56,r.y+22,30,30),"×")) modal="";
            var s=settingsDraft;
            s.focusMinutes=SettingRow(r,74,"每轮专注",s.focusMinutes,5,5,90,"分钟");
            s.shortBreakMinutes=SettingRow(r,118,"短休息",s.shortBreakMinutes,1,1,30,"分钟");
            s.longBreakMinutes=SettingRow(r,162,"长休息",s.longBreakMinutes,5,1,30,"分钟");
            s.cyclesBeforeLongBreak=SettingRow(r,206,"每几轮安排长休息",s.cyclesBeforeLongBreak,1,2,8,"轮");
            s.eyeReminderMinutes=SettingRow(r,250,"专注用眼提醒间隔",s.eyeReminderMinutes,5,10,60,"分钟");
            s.dailyGoalMinutes=SettingRow(r,294,"每日专注目标",s.dailyGoalMinutes,30,30,480,"分钟");
            if(Button(new Rect(r.x+26,r.y+343,284,33),(s.eyeReminderEnabled?"✓ ":"○ ")+"用眼提醒",s.eyeReminderEnabled?Hex("DCEEE3"):Color.white)) s.eyeReminderEnabled=!s.eyeReminderEnabled;
            if(Button(new Rect(r.x+324,r.y+343,300,33),(s.soundEnabled?"✓ ":"○ ")+"完成时提示音",s.soundEnabled?Hex("DCEEE3"):Color.white)) s.soundEnabled=!s.soundEnabled;
            Text(new Rect(r.x+26,r.y+392,r.width-52,52),"新时长从下一轮生效；当前这轮保持原时长与奖励。\n每天目标用于进度展示，不会催促或扣金币。",small);
            if(Button(new Rect(r.x+26,r.yMax-67,190,37),"导出本地备份")) Backup();
            bool old=GUI.enabled; GUI.enabled=old&&store.Writable;
            if(Button(new Rect(r.xMax-214,r.yMax-67,188,37),"保存设置",Hex("CDE5D5"))) if(Apply(study.SetSettings(s),"学习节奏已保存。")) modal="";
            GUI.enabled=old;
        }
        void Backup()
        {
            try
            {
                if(!store.Writable) { Notice("当前存档处于只读模式，请先保留原文件。"); return; }
                if(!Persist()) return; string dir=Path.Combine(Path.GetDirectoryName(store.FilePath),"Backups"); Directory.CreateDirectory(dir);
                string path=Path.Combine(dir,"study-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json");
                File.Copy(store.FilePath,path,false); modal=""; Notice("已备份到存档目录的 Backups 文件夹。",8);
            }
            catch(Exception e) { Notice("备份失败："+e.Message,10); }
        }
        void DrawCompact(float w,float h)
        {
            Text(new Rect(20,13,w-145,34),"糯米学习",new GUIStyle(title){fontSize=23});
            if(Button(new Rect(w-117,15,97,31),"完整界面")) SetCompact(false);
            var p=book.Data.pet;
            Center(new Rect(20,56,w-40,25),p.Name+"陪你，一次只做一件事",small,mint);
            pets.Draw(new Rect((w-240)/2,87,240,197),p.kind,p.hat,p.outfit,p.accessory,Time.unscaledTime<celebrateUntil,Time.unscaledTime,ink);
            Center(new Rect(20,291,w-40,30),PhaseText(),label,mint);
            Center(new Rect(20,323,w-40,69),TimeText(),new GUIStyle(clockStyle){fontSize=66});
            Center(new Rect(20,398,w-40,23),"今日 "+MinutesToday+" 分钟  ·  金币 "+book.Data.coins,small);
            bool old=GUI.enabled; GUI.enabled=old&&store.Writable;
            if(Button(new Rect(20,443,w-161,43),PrimaryLabel(),Hex("CDE5D5"))) PrimaryAction();
            if(Button(new Rect(w-125,443,105,43),"重新计时")) { Apply(study.Reset(),"已准备新一轮，取消的计时不发金币。"); eyePrompt=false; }
            GUI.enabled=old;
            if(Button(new Rect(20,h-41,125,29),book.Data.topmost?"置顶：开":"置顶：关")) ToggleTopmost();
            Text(new Rect(160,h-40,w-176,29),store.Writable?"关闭后暂停，重开可继续":"存档只读，请在完整界面查看原因",new GUIStyle(small){fontSize=12});
        }
        void DrawEyeBanner(float w,bool mini)
        {
            float y=mini?82:97; Box(new Rect(20,y,w-40,43),Hex("F5E9BF"));
            Text(new Rect(32,y+4,w-(mini?182:420),35),mini?"看远处，歇歇眼睛":"专注一会儿啦，看看约 6 米外的远处，让眼睛歇一歇。",small,ink);
            if(Button(new Rect(w-(mini?147:271),y+6,mini?115:145,30),"远眺 20 秒",Color.white))
            { eyeWasRunning=book.Data.timer.running; study.Pause(); titleSecond=-1; study.ResetEyeCounter(); Persist(); eyePrompt=false; eyeRestSeconds=20; modal="eyeRest"; }
            if(!mini && Button(new Rect(w-115,y+6,83,30),"稍后"))
            { eyePrompt=false; book.Data.timer.eyeElapsedSeconds=Math.Max(0,(book.Data.settings.eyeReminderMinutes-5)*60); Persist(); }
        }
        void DrawEyeRest(Rect r)
        {
            Box(r,cream); Center(new Rect(r.x+20,r.y+19,r.width-40,38),"让眼睛放个小假",title);
            Center(new Rect(r.x+25,r.y+65,r.width-50,57),"看向约 6 米外的远处\n专注计时已暂停",small);
            Center(new Rect(r.x+20,r.y+128,r.width-40,64),Math.Ceiling(eyeRestSeconds)+" 秒",new GUIStyle(clockStyle){fontSize=43},mint);
            bool old=GUI.enabled; GUI.enabled=eyeRestSeconds<=0&&old;
            if(Button(new Rect(r.x+26,r.yMax-75,r.width-52,38),eyeRestSeconds>0?"慢慢眨眨眼，放松一下":eyeWasRunning?"休息好了，继续专注":"休息好了",Hex("CDE5D5")))
            { modal=""; study.ResetEyeCounter(); if(eyeWasRunning) study.Resume(); titleSecond=-1; Persist(); }
            GUI.enabled=old;
            Center(new Rect(r.x+20,r.yMax-32,r.width-40,22),"Esc 可关闭休息卡片，计时保持暂停。",new GUIStyle(small){fontSize=12});
        }
    }
}
