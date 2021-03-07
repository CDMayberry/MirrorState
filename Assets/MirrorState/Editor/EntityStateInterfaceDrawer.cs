using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirrorState.Scripts.Editor
{
    //  TODO: Make this an attribute instead and convert the EntityStateInterface list to a string list. (Hm, how does the Attribute work with a List though?)
    [CustomPropertyDrawer(typeof(EntityStateInterface))]
    public class EntityStateInterfaceDrawer : PropertyDrawer
    {
        SerializedProperty Name;
        private string _name;
        private int _index = 0;
        bool _cache = false;

        private static GUIContent _label = new GUIContent("Interface");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!_cache)
            {
                //get the name before it's gone
                //_name = property.displayName;

                //get the X and Y values
                property.Next(true);
                Name = property.Copy();
            }


            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            // Draw label
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), _label);

            /*// Don't make child fields be indented
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;*/
            var nameRect = new Rect(position.x + 90, position.y, position.width - 90, position.height);
            int index = EditorGUI.Popup(nameRect, !string.IsNullOrEmpty(Name.stringValue) ? EntityStateInterface.InterfaceIndex[Name.stringValue] : 0, EntityStateInterface.InterfaceNames);
            if (index > 0)
            {
                Type interfce = EntityStateInterface.Interfaces[index];

                Name.stringValue = interfce.Name;
            }

            EditorGUI.EndProperty();
        }
    }
}