using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MirrorState.Scripts.Scriptables
{
    // NOTE: All scriptables objects of a type must be under a Resources folder of their namesake, IE Resources/TeamScriptable for the type TeamScriptable.
    public abstract class StateScriptableObject<T> : StateScriptableBase where T : StateScriptableObject<T>
    {
        // Issue: what if this object exists before LoadAll is called? _index would be wrong on the initial object.
        private byte _index;
        private bool _loaded;
        public static bool _cached;
        // Not ideal having a essentially reversed dictionary, but this is experimental code for now.
        // ReSharper disable once StaticMemberInGenericType
        public static Dictionary<byte, T> ScriptableLookup;
        public static Dictionary<T, byte> IndexLookup;

        public static T GetScriptable(byte index)
        {
            if (!_cached)
            {
                LoadAll();
            }

            return ScriptableLookup[index];
        }

        public byte ToIndex()
        {
            if (!_cached)
            {
                LoadAll();
            }

            // Test code to see if we can rely on LoadAll to update scriptables that were accessed before it was called.
            if(!_loaded) {
                throw new Exception(typeof(T).Name + " was not updated from LoadAll.");
            }

            return this._index;
        }

        private static void LoadAll()
        {
            var resources = Resources.LoadAll<T>(typeof(T).Name);

            //Resources.Load results will return cached objects, but does that return a reference to scriptables that are assigned to a object in the scene?
            for (int i = 0; i < resources.Length; i++) {
                T resource = resources[i];
                resource._index = (byte)i;
                resource._loaded = true;
            }

            ScriptableLookup = resources.ToDictionary(x => x._index);
            IndexLookup = resources.ToDictionary(x => x, x => x._index);
            _cached = true;
        }
    }

    public abstract class StateScriptableBase : ScriptableObject
    {
        // Marker class for the generic StateScriptableObject
    }
}