using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Mirror;
using Mirror.Websocket;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mayberry.Scripts
{
    public static class MayberryUtils
    {
        // http://answers.unity.com/answers/1216386/view.html
        public static List<T> FindAssetsByType<T>() where T : Object
        {
            List<T> assets = new List<T>();
            string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(T)));
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }
            return assets;
        }


        /// <summary>
        ///	This makes it easy to create, name and place unique new ScriptableObject asset files.
        /// </summary>
        public static T CreateAsset<T>(string name, string path) where T : ScriptableObject
        {
            Directory.CreateDirectory(path);
            T asset = ScriptableObject.CreateInstance<T>();
            string fullPath = path + "/" + name + ".asset";
            Directory.CreateDirectory(path);
            AssetDatabase.CreateAsset(asset, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(fullPath);

            return asset;

            //EditorUtility.FocusProjectWindow();

            //Selection.activeObject = asset;

            /*T asset = ScriptableObject.CreateInstance<T>();

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (path == "")
            {
                path = "Assets";
            }
            else if (Path.GetExtension(path) != "")
            {
                path = path.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");
            }

            string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "/New " + typeof(T).ToString() + ".asset");

            AssetDatabase.CreateAsset(asset, assetPathAndName);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;*/
        }

        public static string PathToNamespace(string path)
        {
            // TODO: convert / to .
            throw new Exception();
        }

        public static string GetFolder(string fullPath)
        {
            var lastIdx = fullPath.LastIndexOf('/') ;
            if (lastIdx == -1)
            {
                lastIdx = fullPath.LastIndexOf('\\');
            }

            return fullPath.Substring(0, lastIdx);
        }
    }
}
