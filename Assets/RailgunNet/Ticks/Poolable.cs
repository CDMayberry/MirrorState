
/*
 *  RailgunNet - A Client/Server Network State-Synchronization Layer for Games
 *  Copyright (c) 2016 - Alexander Shoulson - http://ashoulson.com
 *
 *  This software is provided 'as-is', without any express or implied
 *  warranty. In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 *  
 *  1. The origin of this software must not be misrepresented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
*/

using System.Collections.Generic;

namespace RailgunNet.Ticks
{
    public interface ITickPoolable<T> where T : ITickPoolable<T>
    {
        void Reset();
    }

    public static class TickPool<T> where T : ITickPoolable<T>, new()
    {
        private static readonly Stack<T> FreeList = new Stack<T>();

        private static T Create()
        {
            return new T();
        }

        public static T Allocate()
        {
            T obj;
            if (FreeList.Count > 0)
                obj = FreeList.Pop();
            else
                obj = Create();

            obj.Reset();
            return obj;
        }

        public static void Deallocate(T obj)
        {
            obj.Reset();
            FreeList.Push(obj);
        }

        public static void SafeReplace(ref T destination, T obj)
        {
            if (destination != null)
                TickPool<T>.Deallocate(destination);
            destination = obj;
        }

        // TODO: This should be called on destroying the player object.
        public static void DrainQueue(Queue<T> queue)
        {
            while (queue.Count > 0)
            {
                TickPool<T>.Deallocate(queue.Dequeue());
            }
        }
    }
}