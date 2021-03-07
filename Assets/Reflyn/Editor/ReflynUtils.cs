using System;
using UnityEngine;
using System.Collections;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

public static class ReflynUtils
{
    public static string GetFolder(string fullPath)
    {
        var lastIdx = fullPath.LastIndexOf('/');
        if (lastIdx == -1)
        {
            lastIdx = fullPath.LastIndexOf('\\');
        }

        return fullPath.Substring(0, lastIdx);
    }

    public static bool CompileSyntax(CompilationUnitSyntax syntax, params Type[] types)
    {
        var output = syntax.NormalizeWhitespace();
        string assemblyName = Path.GetRandomFileName();

        var references = new List<MetadataReference>
        {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Vector3).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Rigidbody).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(GetFolder(typeof(object).Assembly.Location) + "\\Facades\\", "netstandard.dll")),
                MetadataReference.CreateFromFile(typeof(Queue<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Animator).Assembly.Location),
                /*MetadataReference.CreateFromFile(typeof(PriorityQueue<>).Assembly.Location),
#if MIRROR
                MetadataReference.CreateFromFile(typeof(NetworkBehaviour).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ClientRpcAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(NetworkIdentityExtensions).Assembly.Location),
#endif*/
        };
        references.AddRange(
            types.Select(x => MetadataReference.CreateFromFile(x.Assembly.Location))
        );

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: new[] { output.SyntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default));

        using (var ms = new MemoryStream())
        {
            EmitResult result = compilation.Emit(ms);

            if (!result.Success)
            {
                IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

                foreach (Diagnostic diagnostic in failures)
                {
                    Debug.LogError(diagnostic.ToString());
                }

                string path = "Assets/MirrorState/Scripts/Generated";

                Directory.CreateDirectory(path);

                string fullPath = $"{path}/error-output.txt";

                var writer = new StreamWriter(fullPath, false);
                writer.WriteLine(output.ToFullString());
                writer.Close();

                AssetDatabase.ImportAsset(fullPath);
                Object obj = AssetDatabase.LoadAssetAtPath(fullPath, typeof(Object));

                // Select the object in the project folder
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
                return false;
            }

            Debug.Log("Compiled successfully.");
            return true;
        }
    }


}
