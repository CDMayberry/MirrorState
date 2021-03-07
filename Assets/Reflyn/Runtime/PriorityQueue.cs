using System;
using System.Collections.Generic;

namespace Mayberry.Reflyn
{
    // https://gist.github.com/paralleltree/31045ab26f69b956052c
    public class PriorityQueue<T> where T : IComparable
    {
        private List<T> list;
        public int Count { get { return list.Count; } }
        public readonly bool IsDescending;

        public PriorityQueue()
        {
            list = new List<T>();
        }

        public PriorityQueue(bool isdesc)
            : this()
        {
            IsDescending = isdesc;
        }

        public PriorityQueue(int capacity)
            : this(capacity, false)
        { }

        public PriorityQueue(IEnumerable<T> collection)
            : this(collection, false)
        { }

        public PriorityQueue(int capacity, bool isdesc)
        {
            list = new List<T>(capacity);
            IsDescending = isdesc;
        }

        public PriorityQueue(IEnumerable<T> collection, bool isdesc)
            : this()
        {
            IsDescending = isdesc;
            foreach (var item in collection)
                Enqueue(item);
        }


        public void Enqueue(T x)
        {
            list.Add(x);
            int i = Count - 1;

            while (i > 0)
            {
                int p = (i - 1) / 2;
                if ((IsDescending ? -1 : 1) * list[p].CompareTo(x) <= 0) break;

                list[i] = list[p];
                i = p;
            }

            if (Count > 0) list[i] = x;
        }

        public T Dequeue()
        {
            T target = Peek();
            T root = list[Count - 1];
            list.RemoveAt(Count - 1);

            int i = 0;
            while (i * 2 + 1 < Count)
            {
                int a = i * 2 + 1;
                int b = i * 2 + 2;
                int c = b < Count && (IsDescending ? -1 : 1) * list[b].CompareTo(list[a]) < 0 ? b : a;

                if ((IsDescending ? -1 : 1) * list[c].CompareTo(root) >= 0) break;
                list[i] = list[c];
                i = c;
            }

            if (Count > 0) list[i] = root;
            return target;
        }

        public bool Any()
        {
            return Count != 0;
        }

        public T Peek()
        {
            if (Count == 0) throw new InvalidOperationException("Queue is empty.");
            return list[0];
        }

        public void Clear()
        {
            list.Clear();
        }
    }

    // https://gist.github.com/paralleltree/31045ab26f69b956052c
    public class PriorityQueue<TKey, TValue> where TKey : IComparable
    {
        private List<KeyValuePair<TKey, TValue>> list;
        public int Count { get { return list.Count; } }
        public readonly bool IsDescending;

        public PriorityQueue()
        {
            list = new List<KeyValuePair<TKey, TValue>>();
        }

        public PriorityQueue(bool isdesc)
            : this()
        {
            IsDescending = isdesc;
        }

        public PriorityQueue(int capacity)
            : this(capacity, false)
        { }

        public PriorityQueue(IEnumerable<KeyValuePair<TKey, TValue>> collection)
            : this(collection, false)
        { }

        public PriorityQueue(int capacity, bool isdesc)
        {
            list = new List<KeyValuePair<TKey, TValue>>(capacity);
            IsDescending = isdesc;
        }

        public PriorityQueue(IEnumerable<KeyValuePair<TKey, TValue>> collection, bool isdesc)
            : this()
        {
            IsDescending = isdesc;
            foreach (var item in collection)
                Enqueue(item);
        }

        public void Enqueue(TKey key, TValue val)
        {
            Enqueue(new KeyValuePair<TKey, TValue>(key, val));
        }


        public void Enqueue(KeyValuePair<TKey, TValue> val)
        {
            list.Add(val);
            int i = Count - 1;

            while (i > 0)
            {
                int p = (i - 1) / 2;
                if ((IsDescending ? -1 : 1) * list[p].Key.CompareTo(val.Key) <= 0) break;

                list[i] = list[p];
                i = p;
            }

            if (Count > 0) list[i] = val;
        }

        public KeyValuePair<TKey, TValue> Dequeue()
        {
            KeyValuePair<TKey, TValue> target = Peek();
            KeyValuePair<TKey, TValue> root = list[Count - 1];
            list.RemoveAt(Count - 1);

            int i = 0;
            while (i * 2 + 1 < Count)
            {
                int a = i * 2 + 1;
                int b = i * 2 + 2;
                int c = b < Count && (IsDescending ? -1 : 1) * list[b].Key.CompareTo(list[a].Key) < 0 ? b : a;

                if ((IsDescending ? -1 : 1) * list[c].Key.CompareTo(root.Key) >= 0) break;
                list[i] = list[c];
                i = c;
            }

            if (this.Any())
            {
                list[i] = root;
            }
            return target;
        }

        public bool Any()
        {
            return Count > 0;
        }

        public KeyValuePair<TKey, TValue> Peek()
        {
            if (Count == 0) throw new InvalidOperationException("Queue is empty.");
            return list[0];
        }

        public void Clear()
        {
            list.Clear();
        }
    }
}