using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
#if !NET_4_6 && !NET_STANDARD_2_0
using Unity.Collections.Concurrent;
#else
using System.Collections.Concurrent;
#endif
#else
using System.Collections.Concurrent;
#endif

namespace Capstones.UnityEngineEx
{
    /// <summary>
    /// 一个环形数组实现的队列。如果已满则入队会失败。
    /// 优点是避免了GC分配，适用于各种缓存池。
    /// </summary>
    public class ConcurrentQueueFixedSize<T> : IProducerConsumerCollection<T>
        , IEnumerable<T>, IEnumerable, ICollection, IReadOnlyCollection<T>
    {
        public const int DEFAULT_CAPACITY = 16;
        public ConcurrentQueueFixedSize(int capacity)
        {
            _InnerList = new T[capacity];
            _InnerListReadyMark = new VolatileBool[capacity];
        }
        public ConcurrentQueueFixedSize() : this(DEFAULT_CAPACITY)
        { }

        private struct VolatileBool
        {
            public volatile bool Value;
        }

        private volatile T[] _InnerList;
        private volatile VolatileBool[] _InnerListReadyMark; 
        private volatile int _Low;
        private volatile int _High;

        public int Capacity
        {
            get
            {
                return _InnerList.Length;
            }
        }
        public int Count
        {
            get
            {
                int headLow, tailHigh;
                GetHeadTailPositions(out headLow, out tailHigh);
                return tailHigh - headLow;
            }
        }
        private void GetHeadTailPositions(out int headLow, out int tailHigh)
        {
            headLow = _Low;
            tailHigh = _High;
            SpinWait spin = new SpinWait();

            //we loop until the observed values are stable and sensible.  
            //This ensures that any update order by other methods can be tolerated.
            while (
                //if low and high pointers, retry
                headLow != _Low || tailHigh != _High
                )
            {
                spin.SpinOnce();
                headLow = _Low;
                tailHigh = _High;
            }
        }

        public bool IsSynchronized { get { return false; } }
        public object SyncRoot { get { throw new NotSupportedException(); } }

        /// <remarks>Maybe slow if changing while enumerating.</remarks>
        private List<T> ToList()
        {
            //store head and tail positions in buffer, 
            int headLow, tailHigh;
            GetHeadTailPositions(out headLow, out tailHigh);
            List<T> list = new List<T>();

            SpinWait spin = new SpinWait();
            for (int i = tailHigh - 1; i >= headLow; --i)
            {
                var index = i % _InnerList.Length;
                var ready = _InnerListReadyMark[index].Value;
                var val = _InnerList[index];
                var newlow = _Low;

                spin.Reset();
                while (
                    newlow != _Low
                    || i >= newlow && !ready
                    )
                {
                    spin.SpinOnce();
                    ready = _InnerListReadyMark[index].Value;
                    val = _InnerList[index];
                    newlow = _Low;
                }
                if (i < newlow)
                {
                    break;
                }
                list.Add(val);
            }
            list.Reverse();

            return list;
        }
        public void CopyTo(T[] array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            // We must be careful not to corrupt the array, so we will first accumulate an
            // internal list of elements that we will then copy to the array. This requires
            // some extra allocation, but is necessary since we don't know up front whether
            // the array is sufficiently large to hold the stack's contents.
            ToList().CopyTo(array, index);
        }
        void System.Collections.ICollection.CopyTo(Array array, int index)
        {
            // Validate arguments.
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            // We must be careful not to corrupt the array, so we will first accumulate an
            // internal list of elements that we will then copy to the array. This requires
            // some extra allocation, but is necessary since we don't know up front whether
            // the array is sufficiently large to hold the stack's contents.
            ((System.Collections.ICollection)ToList()).CopyTo(array, index);
        }

        /// <remarks>If we enumerate the collection when erasing an element, the list may not have erased item in the returned list.</remarks>
        private IEnumerator<T> GetEnumerator(int headLow, int tailHigh)
        {
            SpinWait spin = new SpinWait();

            for (int i = headLow; i < tailHigh; i++)
            {
                // If the position is reserved by an Enqueue operation, but the value is not written into,
                // spin until the value is available.
                var index = i % _InnerList.Length;
                var ready = _InnerListReadyMark[index].Value;
                var val = _InnerList[index];
                var newlow = _Low;

                spin.Reset();
                while (
                    newlow != _Low
                    || i >= newlow && !ready
                    )
                {
                    spin.SpinOnce();
                    ready = _InnerListReadyMark[index].Value;
                    val = _InnerList[index];
                    newlow = _Low;
                }
                if (i < newlow)
                {
                    yield break;
                }
                yield return val;
            }
        }
        public IEnumerator<T> GetEnumerator()
        {
            int headLow, tailHigh;
            GetHeadTailPositions(out headLow, out tailHigh);
            return GetEnumerator(headLow, tailHigh);
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<T>)this).GetEnumerator();
        }

        public T[] ToArray()
        {
            return ToList().ToArray();
        }

        public bool TryAdd(T item)
        {
            SpinWait spin = new SpinWait();
            int headLow, tailHigh;
            GetHeadTailPositions(out headLow, out tailHigh);
            while (tailHigh - headLow < Capacity && Interlocked.CompareExchange(ref _High, tailHigh + 1, tailHigh) != tailHigh)
            {
                spin.SpinOnce();
                GetHeadTailPositions(out headLow, out tailHigh);
            }
            if (tailHigh - headLow >= Capacity)
            {
                return false;
            }
            var index = tailHigh % _InnerList.Length;

            spin.Reset();
            while (_InnerListReadyMark[index].Value)
            {
                spin.SpinOnce();
            }
            _InnerList[index] = item;
            _InnerListReadyMark[index].Value = true;

            return true;
        }

        public bool TryTake(out T item)
        {
            SpinWait spin = new SpinWait();
            int headLow, tailHigh;
            GetHeadTailPositions(out headLow, out tailHigh);
            while (tailHigh - headLow > 0 && Interlocked.CompareExchange(ref _Low, headLow + 1, headLow) != headLow)
            {
                spin.SpinOnce();
                GetHeadTailPositions(out headLow, out tailHigh);
            }
            if (tailHigh - headLow <= 0)
            {
                item = default(T);
                return false;
            }
            var index = headLow % _InnerList.Length;

            spin.Reset();
            while (!_InnerListReadyMark[index].Value)
            {
                spin.SpinOnce();
            }
            item = _InnerList[index];
            _InnerList[index] = default(T);
            _InnerListReadyMark[index].Value = false;

            return true;
        }

        public bool Enqueue(T item)
        {
            return TryAdd(item);
        }
        public bool TryDequeue(out T result)
        {
            return TryTake(out result);
        }
        public bool TryPeek(out T result)
        {
            SpinWait spin = new SpinWait();
            var newlow = _Low;
            var newhigh = _High;
            var index = newlow % _InnerList.Length;
            var ready = _InnerListReadyMark[index].Value;
            var val = _InnerList[index];

            spin.Reset();
            while (
                newhigh - newlow > 0 &&
                (
                    newlow != _Low
                    || newhigh != _High
                    || !ready
                )
            )
            {
                spin.SpinOnce();
                newlow = _Low;
                newhigh = _High;
                index = newlow % _InnerList.Length;
                ready = _InnerListReadyMark[index].Value;
                val = _InnerList[index];
            }
            if (newhigh - newlow <= 0)
            {
                result = default(T);
                return false;
            }

            result = val;
            return true;
        }
    }

    public class ConcurrentQueueGrowOnly<T> : IProducerConsumerCollection<T>
        , IEnumerable<T>, IEnumerable, ICollection, IReadOnlyCollection<T>
    {
        public int Count => throw new NotImplementedException();

        public bool IsSynchronized { get { return false; } }
        public object SyncRoot { get { throw new NotSupportedException(); } }

        public void CopyTo(T[] array, int index)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public T[] ToArray()
        {
            throw new NotImplementedException();
        }

        public bool TryAdd(T item)
        {
            throw new NotImplementedException();
        }

        public bool TryTake(out T item)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}