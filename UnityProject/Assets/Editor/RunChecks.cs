using System.IO;
using UnityEditor;
using UnityEngine;
namespace MochiDay.EditorTools
{
    public static class RunChecks
    {
        [MenuItem("Mochi Day/运行存档与日期测试")]
        public static void Run()
        {
            string report=MochiApp.Argument("--report") ?? Path.GetFullPath(Path.Combine(Application.dataPath,"../../TestReport.txt"));
            CoreChecks.Run(report); if(CoreChecks.Failures>0) throw new System.Exception("Mochi Day tests failed");
        }
    }
}
