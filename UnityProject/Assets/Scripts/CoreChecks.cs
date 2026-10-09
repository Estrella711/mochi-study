using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace MochiDay
{
    public static class CoreChecks
    {
        public static int Failures { get; private set; }
        static readonly List<string> results = new List<string>();
        static void Check(string name, Action test)
        { try { test(); results.Add("PASS " + name); } catch(Exception e) { Failures++; results.Add("FAIL " + name + ": " + e); } }
        static void Assert(bool value, string reason) { if (!value) throw new Exception(reason); }
        public static void Run(string report)
        {
            results.Clear(); Failures=0;
            string dir=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report)),"test-data-"+Guid.NewGuid().ToString("N"));
            Check("CRUD and all four quadrants",()=> {
                var b=new TaskBook(new SaveData(),new DateTime(2026,10,9));
                for(int q=0;q<4;q++) Assert(b.Add("任务 "+q,q<2,q%2==0).Quadrant==q,"quadrant mismatch");
                var t=b.Today.tasks[0]; b.Edit(t,"已修改中文任务",false,false); Assert(t.title=="已修改中文任务" && t.Quadrant==3,"edit failed");
                b.Delete(t); Assert(b.Today.tasks.Count==3,"delete failed");
                bool rejected=false; try { b.Add("  ",true,true); } catch(ArgumentException) { rejected=true; } Assert(rejected,"empty title accepted");
            });
            Check("Completion, undo, caps and delete recalculate",()=> {
                var b=new TaskBook(new SaveData(),DateTime.Today); var t=b.Add("喝水",true,false);
                b.Toggle(t); Assert(b.Today.Energy==73 && b.Today.Mood==70,"reward mismatch"); b.Toggle(t); Assert(b.Today.Energy==70 && b.Today.Mood==65,"undo mismatch");
                for(int i=0;i<20;i++) b.Toggle(b.Add("小任务"+i,false,false)); Assert(b.Today.Energy==100 && b.Today.Mood==100,"cap failed");
                b.Today.tasks.RemoveAll(x=>x!=t); Assert(b.Today.Energy==70,"delete reward retained");
            });
            Check("Midnight, history, clock rollback and restart",()=> {
                var b=new TaskBook(new SaveData(),new DateTime(2026,10,9,23,59,59)); b.Toggle(b.Add("今天",true,true));
                Assert(b.SelectDate(new DateTime(2026,10,10)),"no rollover"); Assert(b.Today.tasks.Count==0 && b.Today.Energy==70,"new day not reset");
                Assert(!b.SelectDate(new DateTime(2026,10,10,12,0,0)),"duplicate day");
                b.SelectDate(new DateTime(2026,10,9)); Assert(b.Today.Completed==1,"history lost");
                var s=new LocalStore(dir); Assert(s.Save(b.Data),"save failed"); var fresh=new TaskBook(s.Load(),new DateTime(2026,10,9));
                Assert(fresh.Today.tasks[0].title=="今天" && fresh.Today.Completed==1 && fresh.Data.days.Count==2,"restart lost data");
            });
            Check("Atomic backup, corruption recovery, invalid schema protection",()=> {
                string path=Path.Combine(dir,"recovery"); var s=new LocalStore(path); var b=new TaskBook(new SaveData(),DateTime.Today);
                b.Add("备份任务",true,true); Assert(s.Save(b.Data),"first save failed"); b.Add("新任务",false,false); Assert(s.Save(b.Data),"second save failed");
                File.WriteAllText(s.FilePath,"broken"); var r=new LocalStore(path); Assert(r.Load().days[0].tasks.Count==1 && r.Writable && r.Warning!=null,"backup recovery failed");
                Assert(Directory.GetFiles(path,"*.corrupt-*").Length==1,"corrupt evidence not retained");
                string unsafePath=Path.Combine(dir,"invalid"); Directory.CreateDirectory(unsafePath); var broken=new LocalStore(unsafePath);
                File.WriteAllText(broken.FilePath,"{\"version\":99,\"days\":[]}");
                broken.Load(); Assert(!broken.Writable && !broken.Save(new SaveData()),"invalid data overwritten");
            });
            StudyChecks.Run(Check,dir);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report)));
            results.Add("TOTAL " + (results.Count-Failures) + " passed, " + Failures + " failed");
            File.WriteAllLines(report,results.ToArray()); foreach(var line in results) Debug.Log(line);
        }
    }
}
