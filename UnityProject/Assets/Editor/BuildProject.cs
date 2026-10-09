using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MochiDay.EditorTools
{
    public static class BuildProject
    {
        [MenuItem("Mochi Day/准备场景与窗口设置")]
        public static void Configure()
        {
            PlayerSettings.companyName = "MochiDay";
            PlayerSettings.productName = "糯米学习 · Mochi Study";
            PlayerSettings.bundleVersion = "2.0.0";
            PlayerSettings.defaultScreenWidth = 1100; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard_2_0);
            CreateIcon();
            if (!File.Exists("Assets/Scenes/Main.unity"))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.orthographic = true; camera.backgroundColor = new Color(0.98f, 0.97f, 0.94f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.transform.position = new Vector3(0, 0, -10);
                EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
            AssetDatabase.SaveAssets();
        }
        static void CreateIcon()
        {
            const string path = "Assets/MochiIcon.asset";
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (icon == null)
            {
                icon = new Texture2D(256,256,TextureFormat.RGBA32,false);
                for(int y=0;y<256;y++) for(int x=0;x<256;x++)
                {
                    Color color = new Color(.84f,.92f,.85f,1);
                    Func<float,float,float,float,bool> oval = (cx,cy,rx,ry) => Mathf.Pow((x-cx)/rx,2)+Mathf.Pow((y-cy)/ry,2)<1;
                    if(oval(94,188,20,56)||oval(162,188,20,56)||oval(128,111,79,65)) color=new Color(1,.99f,.95f);
                    if(oval(94,197,10,33)||oval(162,197,10,33)||oval(83,96,14,7)||oval(173,96,14,7)) color=new Color(.93f,.74f,.69f);
                    if(oval(105,116,5,7)||oval(151,116,5,7)||oval(128,92,6,3)) color=new Color(.27f,.35f,.36f);
                    icon.SetPixel(x,y,color);
                }
                icon.Apply(); AssetDatabase.CreateAsset(icon,path);
            }
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone,new[] { icon });
        }
        [MenuItem("Mochi Day/构建 Windows x64")]
        public static void BuildWindows()
        {
            Configure();
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Windows/MochiStudy.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Scenes/Main.unity" }, locationPathName = path, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            if (result.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed: " + result.summary.result);
            Debug.Log("MOCHI_BUILD_OK " + path);
        }
    }
}
