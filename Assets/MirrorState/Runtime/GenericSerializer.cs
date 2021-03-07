using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Mirror;

namespace Assets.MirrorState.Scripts
{
    /*public static class UnsafeSerializer<T> where T : unmanaged
    {
        public static readonly int Size = Marshal.SizeOf(typeof(T));
        // Not sure if we should be clearing this buffer, but since it's always going to be the size of the structure all data will be overwritten anyway?
        // ReSharper disable once StaticMemberInGenericType
        public static readonly byte[] Buffer = new byte[Size];


        public static unsafe byte[] Serialize(T obj)
        {
            fixed (byte* b = Buffer)
                Unsafe.Write(b, obj);

            return Buffer;
        }

        public static unsafe T Deserialize(byte[] bytes)
        {
            fixed (byte* b = bytes)
            {
                return Unsafe.Read<T>(b);
            }
        }
    }*/
}