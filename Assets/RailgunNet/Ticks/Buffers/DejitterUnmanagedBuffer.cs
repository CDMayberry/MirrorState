
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

using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mirror;

namespace RailgunNet.Ticks.Buffers
{
    [Serializable]
    public struct TickWrapper<T> where T : unmanaged
    {
        public uint Tick;
        public T Data;
    }

    public interface IReadOnlyDejitterUnmanagedBuffer<T> where T : unmanaged
    {
        TickWrapper<T> Get(uint tick);
        void GetFirstAfter(uint currentTick, out TickWrapper<T> current, out TickWrapper<T> next);
        TickWrapper<T> GetLatestAt(uint tick);
        IList<TickWrapper<T>> GetRange(uint start);
        IList<TickWrapper<T>> GetRangeAndNext(uint start, uint end, out TickWrapper<T> next);
        bool Contains(uint tick);
        bool TryGet(uint tick, out TickWrapper<T> value);
    }

    public class DejitterUnmanagedBuffer<T> : IReadOnlyDejitterUnmanagedBuffer<T> where T : unmanaged
    {
        private static readonly TickWrapper<T> Default = new TickWrapper<T>();
        private static readonly Comparer<uint> Comparer = Comparer<uint>.Default;
        private static int Compare(TickWrapper<T> x, TickWrapper<T> y)
        {
            return Comparer.Compare(x.Tick, y.Tick);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsDefault(TickWrapper<T> x)
        {
            return Compare(x, Default) == 0;
        }

        // Used for converting a key to an index. For example, the server may only
        // send a snapshot every two ticks, so we would divide the tick number
        // key by 2 so as to avoid wasting space in the frame buffer
        private readonly uint _divisor;

        /// <summary>
        /// The most recent value stored in this buffer.
        /// </summary>
        public TickWrapper<T> Latest
        {
            get
            {
                if (this._latestIdx < 0)
                {
                    return default;
                }

                return this._data[this._latestIdx];
            }
        }

        /// <summary>
        /// The oldest value stored in this buffer. Not thoroughly tested.
        /// </summary>
        public TickWrapper<T> Last
        {
            get
            {
                if (this._lastIdx < 0)
                {
                    return default;
                }

                return this._data[this._lastIdx];
            }
        }

        private readonly TickWrapper<T>[] _data;
        private int _latestIdx;
        private int _lastIdx;
        private readonly List<TickWrapper<T>> _returnList; // A reusable list for returning results

        public IEnumerable<TickWrapper<T>> Values
        {
            get
            {
                foreach (TickWrapper<T> value in this._data)
                {
                    if (!IsDefault(value))
                    {
                        yield return value;
                    }
                }
            }
        }

        public DejitterUnmanagedBuffer(int capacity, uint divisor = 1)
        {
            this._returnList = new List<TickWrapper<T>>();
            this._divisor = divisor;
            this._data = new TickWrapper<T>[capacity / divisor];
            this._latestIdx = -1;
            this._lastIdx = -1;
        }

        /// <summary>
        /// Clears the buffer, freeing all contents.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < this._data.Length; i++)
            {
                _data[i] = default;
            }

            this._latestIdx = -1;
            this._lastIdx = -1;
        }

        /// <summary>
        /// Stores a value. Will not replace a stored value with an older one.
        /// </summary>
        public bool Store(TickWrapper<T> value)
        {
            int index = this.TickToIndex(value.Tick);
            bool store = false;

            if (this._latestIdx < 0)
            {
                store = true;
            }
            else
            {
                TickWrapper<T> latest = this._data[this._latestIdx];
                if (IsDefault(value) || value.Tick >= latest.Tick)
                {
                    store = true;
                }
            }
            if (store)
            {
                _data[index] = value;
                _latestIdx = index;
                _lastIdx = index + 1;
                if (_lastIdx >= _data.Length)
                {
                    _lastIdx = 0;
                }
            }
            return store;
        }

        /// <summary>
        /// Stores a value. Will not replace a stored value with an older one.
        /// </summary>
        public bool Replace(TickWrapper<T> value)
        {
            int index = this.TickToIndex(value.Tick);
            bool replace = false;

            TickWrapper<T> element = this._data[index];
            if (IsDefault(value) || value.Tick == element.Tick)
            {
                replace = true;
            }

            if (replace)
            {
                _data[index] = value;
            }

            return replace;
        }

        public TickWrapper<T> Get(uint tick)
        {
            // This is a good case for a custom Tick object, even if it's just a wrapper around a uint
            if (tick == TickConstants.BadTick)
            {
                return Default;
            }

            TickWrapper<T> result = this._data[this.TickToIndex(tick)];
            if (!IsDefault(result) && result.Tick == tick)
                return result;

            return Default;
        }

        /// <summary>
        /// Given a tick, returns the the following values:
        /// - The value at or immediately before the tick (current)
        /// - The value immediately after that (next)
        /// 
        /// Runs in O(n).
        /// </summary>
        public void GetFirstAfter(uint currentTick, out TickWrapper<T> current, out TickWrapper<T> next)
        {
            current = default;
            next = default;

            if (currentTick == TickConstants.BadTick)
            {
                return;
            }

            for (int i = 0; i < this._data.Length; i++)
            {
                TickWrapper<T> value = this._data[i];
                if (!IsDefault(value))
                {
                    if (value.Tick > currentTick)
                    {
                        if (IsDefault(next) || value.Tick < next.Tick)
                        {
                            next = value;
                        }
                    }
                    else if (IsDefault(current) || value.Tick > current.Tick)
                    {
                        current = value;
                    }
                }
            }
        }

        /// <summary>
        /// Finds the latest value at or before a given tick. O(n)
        /// </summary>
        public TickWrapper<T> GetLatestAt(uint tick)
        {
            if (tick == TickConstants.BadTick)
            {
                return default;
            }

            TickWrapper<T> result = this.Get(tick);
            if (!IsDefault(result))
            {
                return result;
            }

            for (int i = 0; i < this._data.Length; i++)
            {
                TickWrapper<T> value = this._data[i];
                if (!IsDefault(value))
                {
                    if (value.Tick == tick)
                    {
                        return value;
                    }

                    if (value.Tick < tick && (IsDefault(result) || result.Tick < value.Tick))
                    {
                        result = value;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Finds all items at or later than the given tick, in order.
        /// </summary>
        public IList<TickWrapper<T>> GetRange(uint start)
        {
            this._returnList.Clear();
            if (start == TickConstants.BadTick)
                return this._returnList;

            for (int i = 0; i < this._data.Length; i++)
            {
                TickWrapper<T> val = this._data[i];
                if (!IsDefault(val) && val.Tick >= start)
                {
                    this._returnList.Add(val);
                }
            }

            this._returnList.Sort(Compare);
            return this._returnList;
        }

        /// <summary>
        /// Finds all items with ticks in the inclusive range [start, end]
        /// and also returns the value immediately following (if one exists)
        /// </summary>
        public IList<TickWrapper<T>> GetRangeAndNext(uint start, uint end, out TickWrapper<T> next)
        {
            next = default;
            this._returnList.Clear();
            if (start == TickConstants.BadTick)
                return this._returnList;

            uint lowest = uint.MaxValue;
            for (int i = 0; i < this._data.Length; i++)
            {
                TickWrapper<T> val = this._data[i];
                if (!IsDefault(val))
                {
                    if (val.Tick >= start && val.Tick <= end)
                    {
                        this._returnList.Add(val);
                    }

                    if (val.Tick > end && (lowest == uint.MaxValue || val.Tick < lowest))
                    {
                        next = val;
                        lowest = val.Tick;
                    }
                }
            }

            this._returnList.Sort(Compare);
            return this._returnList;
        }

        public bool Contains(uint tick)
        {
            if (tick == TickConstants.BadTick)
                return false;

            TickWrapper<T> result = this._data[this.TickToIndex(tick)];
            if (!IsDefault(result) && result.Tick == tick)
            {
                return true;
            }

            return false;
        }

        public bool TryGet(uint tick, out TickWrapper<T> value)
        {
            if (tick == TickConstants.BadTick)
            {
                value = default;
                return false;
            }

            value = this.Get(tick);
            return !IsDefault(value);
        }

        private int TickToIndex(uint tick)
        {
            return (int)(tick / _divisor) % _data.Length;
        }
    }
}