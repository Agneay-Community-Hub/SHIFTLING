using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Command-line builds, e.g.
///   unity build --target WebGL --execute-method BuildScript.BuildWebGL --output-path Builds/WebGL
///   unity build --target StandaloneWindows64 --execute-method BuildScript.BuildWindows --output-path Builds/Windows/Shiftling.exe
/// </summary>
public static class BuildScript
{
    static readonly string[] Scenes = { "Assets/Scenes/SampleScene.unity" };

    static string OutputPath(string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-buildOutput" || args[i] == "-customBuildPath") return args[i + 1];
        return fallback;
    }

    static void CommonSettings()
    {
        PlayerSettings.companyName = "Shiftling Team";
        PlayerSettings.productName = "Shiftling";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.runInBackground = false;   // pause (and go silent) when the window/tab loses focus
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.SplashScreen.showUnityLogo = false;
    }

    public static void BuildWebGL()
    {
        CommonSettings();
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        Build(BuildTarget.WebGL, OutputPath("Builds/WebGL"));
    }

    public static void BuildWindows()
    {
        CommonSettings();
        Build(BuildTarget.StandaloneWindows64, OutputPath("Builds/Windows/Shiftling.exe"));
    }

    static void Build(BuildTarget target, string path)
    {
        Directory.CreateDirectory(target == BuildTarget.WebGL ? path : Path.GetDirectoryName(Path.GetFullPath(path)));
        var options = new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = path,
            target = target,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        var s = report.summary;
        Debug.Log($"[BuildScript] {target} -> {path}: {s.result}, {s.totalSize / (1024f * 1024f):0.0} MB, {s.totalErrors} errors");
        if (Application.isBatchMode)
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }
}
