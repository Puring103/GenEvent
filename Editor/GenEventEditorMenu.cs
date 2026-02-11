using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using GenEvent.Editor;

[InitializeOnLoad]
public static class GenEventEditorMenu
{
    private const string DefaultOutputPath = "Assets/GenEvent/Runtime/gen";

    static GenEventEditorMenu()
    {
        // 注册编译回调
        // CompilationPipeline.compilationStarted += OnCompilationStarted;
        // CompilationPipeline.compilationFinished += OnCompilationFinished;
    }

    [MenuItem("Tools/GenEvent/Generate Code")]
    public static void GenerateCode()
    {
        var generator = new CodeGenerator();
        var success = generator.GenerateCode(DefaultOutputPath);

        if (success)
        {
            Debug.Log("代码生成成功！");
        }
        else
        {
            Debug.LogError("代码生成失败，请查看上面的错误信息。");
        }
    }

    private static void OnCompilationStarted(object obj)
    {
        Debug.Log("[GenEvent] 编译开始，删除生成代码文件...");

        try
        {
            DeleteGeneratedFiles(DefaultOutputPath);
            Debug.Log("[GenEvent] 编译前删除生成文件成功");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GenEvent] 编译前删除生成文件异常: {e.Message}");
        }
    }

    private static void OnCompilationFinished(object obj)
    {
        Debug.Log("[GenEvent] 编译完成，执行编译后代码生成...");

        try
        {
            var generator = new CodeGenerator();
            var success = generator.GenerateCode(DefaultOutputPath);

            if (success)
            {
                Debug.Log("[GenEvent] 编译后代码生成成功");
            }
            else
            {
                Debug.LogWarning("[GenEvent] 编译后代码生成失败");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[GenEvent] 编译后代码生成异常: {e.Message}");
        }
    }

    private static void DeleteGeneratedFiles(string outputPath)
    {
        // 如果输出路径是Assets相对路径，转换为绝对路径
        string fullPath;
        if (outputPath.StartsWith("Assets"))
        {
            fullPath = Path.Combine(Application.dataPath, outputPath.Substring("Assets".Length).TrimStart('/', '\\'));
        }
        else
        {
            fullPath = outputPath;
        }

        if (!Directory.Exists(fullPath))
        {
            return;
        }

        // 删除所有 .g.cs 文件
        var files = Directory.GetFiles(fullPath, "*.g.cs", SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
                Debug.Log($"[GenEvent] 删除文件: {file}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GenEvent] 删除文件失败 {file}: {e.Message}");
            }
        }

        // 刷新资源数据库
        AssetDatabase.Refresh();
    }
}

