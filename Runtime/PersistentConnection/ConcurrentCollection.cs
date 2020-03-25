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
    public struct VolatileBool
    {
        public volatile bool Value;
    }
    public struct VolatileLong
    {
        public long _Value;
        public long Value
        {
            get { return Volatile.Read(ref _Value); }
            set { Volatile.Write(ref _Value, value); }
        }
    }

    /// <summary>
    /// 一个环形数组实现的队列。如果已满则入队会失败。
    /// 优点是避免了GC分配，适用于各种缓存池。
    /// </summary>
    public class ConcurrentQueueFixedSize<T> : IProducerConsumerCollection<T>
    {
        public const int DEFAULT_CAPACITY = 32;
        public ConcurrentQueueFixedSize(int capacity)
        {
            _InnerList = new T[capacity];
            _InnerListReadyMark = new VolatileBool[capacity];
        }
        public ConcurrentQueueFixedSize() : this(DEFAULT_CAPACITY)
        { }

        private volatile T[] _InnerList;
        private volatile VolatileBool[] _InnerListReadyMark; 
        private VolatileLong _Low;
        private VolatileLong _High;

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
                long headLow, tailHigh;
                GetHeadTailPositions(out headLow, out tailHigh);
                return (int)(tailHigh - headLow);
            }
        }
        private void GetHeadTailPositions(out long headLow, out long tailHigh)
        {
            var low = _Low.Value;
            var high = _High.Value;
            SpinWait spin = new SpinWait();

            //we loop until the observed values are stable and sensible.  
            //This ensures that any update order by other methods can be tolerated.
            while (
                //if low and high pointers, retry
                low != _Low.Value || high != _High.Value
                )
            {
                spin.SpinOnce();
                low = _Low.Value;
                high = _High.Value;
            }

            headLow = low;
            tailHigh = high;
        }

        public bool IsSynchronized { get { return false; } }
        public object SyncRoot { get { throw new NotSupportedException(); } }

        /// <remarks>Maybe slow if changing while enumerating.</remarks>
        private List<T> ToList()
        {
            //store head and tail positions in buffer, 
            long headLow, tailHigh;
            GetHeadTailPositions(out headLow, out tailHigh);
            List<T> list = new List<T>();

            SpinWait spin = new SpinWait();
            for (var i = tailHigh - 1; i >= headLow; --i)
            {
                var index = i % _InnerList.Length;
                var ready = _InnerListReadyMark[index].Value;
                var val = _InnerList[index];
                var newlow = _Low.Value;

                spin.Reset();
                while (
                    newlow != _Low.Value
                    || i >= newlow && !ready
                    )
                {
                    spin.SpinOnce();
                    ready = _InnerListReadyMark[index].Value;
                    val = _InnerList[index];
                    newlow = _Low.Value;
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
        private IEnumerator<T> GetEnumerator(long headLow, long tailHigh)
        {
            SpinWait spin = new SpinWait();

            for (var i = headLow; i < tailHigh; i++)
            {
                // If the position is reserved by an Enqueue operation, but the value is not written into,
                // spin until the value is available.
                var index = i % _InnerList.Length;
                var ready = _InnerListReadyMark[index].Value;
                var val = _InnerList[index];
                var newlow = _Low.Value;

                spin.Reset();
                while (
                    newlow != _Low.Value
                    || i >= newlow && !ready
                    )
                {
                    spin.SpinOnce();
                    ready = _InnerListReadyMark[index].Value;
                    val = _InnerList[index];
                    newlow = _Low.Value;
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
            long headLow, tailHigh;
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
            long headLow, tailHigh;
            GetHeadTailPositions(out headLow, out tailHigh);
            while (tailHigh - headLow < Capacity && Interlocked.CompareExchange(ref _High._Value, tailHigh + 1, tailHigh) != tailHigh)
            {
                //spin.SpinOnce();
                GetHeadTailPositions(out headLow, out tailHigh);
            }
            if (tailHigh - headLow >= Capacity)
            {
                return false;
            }
            var index = tailHigh % _InnerList.Length;

            //spin.Reset();
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
            long headLow, tailHigh;
            GetHeadTailPositions(out headLow, out tailHigh);
            while (tailHigh - headLow > 0 && Interlocked.CompareExchange(ref _Low._Value, headLow + 1, headLow) != headLow)
            {
                //spin.SpinOnce();
                GetHeadTailPositions(out headLow, out tailHigh);
            }
            if (tailHigh - headLow <= 0)
            {
                item = default(T);
                return false;
            }
            var index = headLow % _InnerList.Length;

            //spin.Reset();
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
            var newlow = _Low.Value;
            var newhigh = _High.Value;
            var index = newlow % _InnerList.Length;
            var ready = _InnerListReadyMark[index].Value;
            var val = _InnerList[index];

            spin.Reset();
            while (
                newhigh - newlow > 0 &&
                (
                    newlow != _Low.Value
                    || newhigh != _High.Value
                    || !ready
                )
            )
            {
                spin.SpinOnce();
                newlow = _Low.Value;
                newhigh = _High.Value;
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

    /// <summary>
    /// 在现有ConcurrentQueue的基础上，加入了Segment回收重用的机制。
    /// 可以有效地减少GC分配。
    /// </summary>
    public class ConcurrentQueueGrowOnly<T> : IProducerConsumerCollection<T>
    {
        //fields of ConcurrentQueue
        private volatile Segment _head;
        private volatile Segment _tail;
        private volatile Segment _freetail;

        private const int SEGMENT_SIZE = 32;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentQueue{T}"/> class.
        /// </summary>
        public ConcurrentQueueGrowOnly()
        {
            _head = _tail = _freetail = new Segment(1, this);
            _head._is_free = false;
        }

        /// <summary>
        /// Initializes the contents of the queue from an existing collection.
        /// </summary>
        /// <param name="collection">A collection from which to copy elements.</param>
        private void InitializeFromCollection(IEnumerable<T> collection)
        {
            Segment localTail = new Segment(1, this);//use this local variable to avoid the extra volatile read/write. this is safe because it is only called from ctor
            _head = localTail;
            _head._is_free = false;

            int index = 0;
            foreach (T element in collection)
            {
                System.Diagnostics.Debug.Assert(index >= 0 && index < SEGMENT_SIZE);
                localTail.UnsafeAdd(element);
                index++;
                ++_Count;

                if (index >= SEGMENT_SIZE)
                {
                    localTail = localTail.UnsafeGrow();
                    index = 0;
                }
            }

            _tail = _freetail = localTail;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentQueue{T}"/>
        /// class that contains elements copied from the specified collection
        /// </summary>
        /// <param name="collection">The collection whose elements are copied to the new <see
        /// cref="ConcurrentQueue{T}"/>.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="collection"/> argument is
        /// null.</exception>
        public ConcurrentQueueGrowOnly(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException("collection");
            }

            InitializeFromCollection(collection);
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.ICollection"/> to an <see
        /// cref="T:System.Array"/>, starting at a particular
        /// <see cref="T:System.Array"/> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array">Array</see> that is the
        /// destination of the elements copied from the
        /// <see cref="T:System.Collections.Concurrent.ConcurrentBag"/>. The <see
        /// cref="T:System.Array">Array</see> must have zero-based indexing.</param>
        /// <param name="index">The zero-based index in <paramref name="array"/> at which copying
        /// begins.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is a null reference (Nothing in
        /// Visual Basic).</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than
        /// zero.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="array"/> is multidimensional. -or-
        /// <paramref name="array"/> does not have zero-based indexing. -or-
        /// <paramref name="index"/> is equal to or greater than the length of the <paramref name="array"/>
        /// -or- The number of elements in the source <see cref="T:System.Collections.ICollection"/> is
        /// greater than the available space from <paramref name="index"/> to the end of the destination
        /// <paramref name="array"/>. -or- The type of the source <see
        /// cref="T:System.Collections.ICollection"/> cannot be cast automatically to the type of the
        /// destination <paramref name="array"/>.
        /// </exception>
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

        /// <summary>
        /// Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection"/> is
        /// synchronized with the SyncRoot.
        /// </summary>
        /// <value>true if access to the <see cref="T:System.Collections.ICollection"/> is synchronized
        /// with the SyncRoot; otherwise, false. For <see cref="ConcurrentQueue{T}"/>, this property always
        /// returns false.</value>
        bool System.Collections.ICollection.IsSynchronized
        {
            // Gets a value indicating whether access to this collection is synchronized. Always returns
            // false. The reason is subtle. While access is in face thread safe, it's not the case that
            // locking on the SyncRoot would have prevented concurrent pushes and pops, as this property
            // would typically indicate; that's because we internally use CAS operations vs. true locks.
            get { return false; }
        }


        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see
        /// cref="T:System.Collections.ICollection"/>. This property is not supported.
        /// </summary>
        /// <exception cref="T:System.NotSupportedException">The SyncRoot property is not supported.</exception>
        object System.Collections.ICollection.SyncRoot
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="T:System.Collections.IEnumerator"/> that can be used to iterate through the collection.</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<T>)this).GetEnumerator();
        }

        /// <summary>
        /// Attempts to add an object to the <see
        /// cref="T:System.Collections.Concurrent.IProducerConsumerCollection{T}"/>.
        /// </summary>
        /// <param name="item">The object to add to the <see
        /// cref="T:System.Collections.Concurrent.IProducerConsumerCollection{T}"/>. The value can be a null
        /// reference (Nothing in Visual Basic) for reference types.
        /// </param>
        /// <returns>true if the object was added successfully; otherwise, false.</returns>
        /// <remarks>For <see cref="ConcurrentQueue{T}"/>, this operation will always add the object to the
        /// end of the <see cref="ConcurrentQueue{T}"/>
        /// and return true.</remarks>
        bool IProducerConsumerCollection<T>.TryAdd(T item)
        {
            Enqueue(item);
            return true;
        }

        /// <summary>
        /// Attempts to remove and return an object from the <see
        /// cref="T:System.Collections.Concurrent.IProducerConsumerCollection{T}"/>.
        /// </summary>
        /// <param name="item">
        /// When this method returns, if the operation was successful, <paramref name="item"/> contains the
        /// object removed. If no object was available to be removed, the value is unspecified.
        /// </param>
        /// <returns>true if an element was removed and returned successfully; otherwise, false.</returns>
        /// <remarks>For <see cref="ConcurrentQueue{T}"/>, this operation will attempt to remove the object
        /// from the beginning of the <see cref="ConcurrentQueue{T}"/>.
        /// </remarks>
        bool IProducerConsumerCollection<T>.TryTake(out T item)
        {
            return TryDequeue(out item);
        }

        /// <summary>
        /// Gets a value that indicates whether the <see cref="ConcurrentQueue{T}"/> is empty.
        /// </summary>
        /// <value>true if the <see cref="ConcurrentQueue{T}"/> is empty; otherwise, false.</value>
        /// <remarks>
        /// For determining whether the collection contains any items, use of this property is recommended
        /// rather than retrieving the number of items from the <see cref="Count"/> property and comparing it
        /// to 0.  However, as this collection is intended to be accessed concurrently, it may be the case
        /// that another thread will modify the collection after <see cref="IsEmpty"/> returns, thus invalidating
        /// the result.
        /// </remarks>
        public bool IsEmpty
        {
            get
            {
                Segment head = _head;
                if (!head.IsEmpty)
                    //fast route 1:
                    //if current head is not empty, then queue is not empty
                    return false;
                else if (head.Next == null)
                    //fast route 2:
                    //if current head is empty and it's the last segment
                    //then queue is empty
                    return true;
                else
                //slow route:
                //current head is empty and it is NOT the last segment,
                //it means another thread is growing new segment 
                {
                    return _Count <= 0;
                }
            }
        }

        /// <summary>
        /// Copies the elements stored in the <see cref="ConcurrentQueue{T}"/> to a new array.
        /// </summary>
        /// <returns>A new array containing a snapshot of elements copied from the <see
        /// cref="ConcurrentQueue{T}"/>.</returns>
        public T[] ToArray()
        {
            return ToList().ToArray();
        }
#pragma warning disable 0420 // No warning for Interlocked.xxx if compiled with new managed compiler (Roslyn)
        /// <summary>
        /// Copies the <see cref="ConcurrentQueue{T}"/> elements to a new <see
        /// cref="T:System.Collections.Generic.List{T}"/>.
        /// </summary>
        /// <returns>A new <see cref="T:System.Collections.Generic.List{T}"/> containing a snapshot of
        /// elements copied from the <see cref="ConcurrentQueue{T}"/>.</returns>
        private List<T> ToList()
        {
            List<T> list = new List<T>();
            //store head and tail positions in buffer,
            long headindex, tailindex;
            Segment head, tail;
            int headLow, tailHigh;
            GetHeadTailPositions(out head, out tail, out headLow, out tailHigh, out headindex, out tailindex);

            Segment curr = head;
            long curindex = headindex;
            List<T> part = new List<T>();
            bool[] states = new bool[SEGMENT_SIZE];
            T[] values = new T[SEGMENT_SIZE];
            while (true)
            {
                part.Clear();
                for (int i = 0; i < SEGMENT_SIZE; ++i)
                {
                    states[i] = curr._state[i].Value;
                    values[i] = curr._array[i];
                }

                GetHeadTailPositions(out head, out tail, out headLow, out tailHigh, out headindex, out tailindex);
                if (headindex > curindex)
                {
                    curr = head;
                    curindex = headindex;
                    list.Clear();
                }
                else
                {
                    var next = curr.Next;
                    long nextindex = 0;
                    if (next != null && !next._is_free && (nextindex = next._index) != curindex + 1)
                    {
                        curr = head;
                        curindex = headindex;
                        list.Clear();
                    }
                    else
                    {
                        int low = 0, high = SEGMENT_SIZE - 1;
                        if (curr == head)
                        {
                            low = headLow;
                        }
                        if (curr == tail)
                        {
                            high = tailHigh;
                        }
                        bool ready = true;
                        for (int i = low; i <= high; ++i)
                        {
                            if (!states[i])
                            {
                                ready = false;
                                break;
                            }
                            part.Add(values[i]);
                        }
                        if (!ready)
                        {
                            continue;
                        }
                        list.AddRange(part);

                        if (next == null || next._is_free || curr == tail)
                        {
                            break;
                        }
                        else
                        {
                            curr = next;
                            curindex = nextindex;
                        }
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Store the position of the current head and tail positions.
        /// </summary>
        /// <param name="head">return the head segment</param>
        /// <param name="tail">return the tail segment</param>
        /// <param name="headLow">return the head offset, value range [0, SEGMENT_SIZE]</param>
        /// <param name="tailHigh">return the tail offset, value range [-1, SEGMENT_SIZE-1]</param>
        private void GetHeadTailPositions(out Segment head, out Segment tail,
            out int headLow, out int tailHigh)
        {
            long hindex, tindex;
            GetHeadTailPositions(out head, out tail, out headLow, out tailHigh, out hindex, out tindex);
        }
        private void GetHeadTailPositions(out Segment head, out Segment tail,
            out int headLow, out int tailHigh, out long headIndex, out long tailIndex)
        {
            head = _head;
            headIndex = head._index;
            tail = _tail;
            tailIndex = tail._index;
            headLow = head.Low;
            tailHigh = tail.High;
            SpinWait spin = new SpinWait();

            //we loop until the observed values are stable and sensible.  
            //This ensures that any update order by other methods can be tolerated.
            while (
                //if head and tail changed, retry
                head != _head || tail != _tail
                //if low and high pointers, retry
                || headLow != head.Low || tailHigh != tail.High
                //if head jumps ahead of tail because of concurrent grow and dequeue, retry
                || head._index > tail._index)
            {
                spin.SpinOnce();
                head = _head;
                headIndex = head._index;
                tail = _tail;
                tailIndex = tail._index;
                headLow = head.Low;
                tailHigh = tail.High;
            }
        }


        /// <summary>
        /// Gets the number of elements contained in the <see cref="ConcurrentQueue{T}"/>.
        /// </summary>
        /// <value>The number of elements contained in the <see cref="ConcurrentQueue{T}"/>.</value>
        /// <remarks>
        /// For determining whether the collection contains any items, use of the <see cref="IsEmpty"/>
        /// property is recommended rather than retrieving the number of items from the <see cref="Count"/>
        /// property and comparing it to 0.
        /// </remarks>
        public int Count
        {
            //get
            //{
            //    //store head and tail positions in buffer, 
            //    Segment head, tail;
            //    int headLow, tailHigh;
            //    long headindex, tailindex;
            //    GetHeadTailPositions(out head, out tail, out headLow, out tailHigh, out headindex, out tailindex);

            //    if (head == tail)
            //    {
            //        return tailHigh - headLow + 1;
            //    }

            //    //head segment
            //    int count = SEGMENT_SIZE - headLow;

            //    //middle segment(s), if any, are full.
            //    //We don't deal with overflow to be consistent with the behavior of generic types in CLR.
            //    count += SEGMENT_SIZE * ((int)(tailindex - headindex - 1));

            //    //tail segment
            //    count += tailHigh + 1;

            //    return count;
            //}
            get { return _Count; }
        }
        protected volatile int _Count;


        /// <summary>
        /// Copies the <see cref="ConcurrentQueue{T}"/> elements to an existing one-dimensional <see
        /// cref="T:System.Array">Array</see>, starting at the specified array index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array">Array</see> that is the
        /// destination of the elements copied from the
        /// <see cref="ConcurrentQueue{T}"/>. The <see cref="T:System.Array">Array</see> must have zero-based
        /// indexing.</param>
        /// <param name="index">The zero-based index in <paramref name="array"/> at which copying
        /// begins.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is a null reference (Nothing in
        /// Visual Basic).</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than
        /// zero.</exception>
        /// <exception cref="ArgumentException"><paramref name="index"/> is equal to or greater than the
        /// length of the <paramref name="array"/>
        /// -or- The number of elements in the source <see cref="ConcurrentQueue{T}"/> is greater than the
        /// available space from <paramref name="index"/> to the end of the destination <paramref
        /// name="array"/>.
        /// </exception>
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


        /// <summary>
        /// Returns an enumerator that iterates through the <see
        /// cref="ConcurrentQueue{T}"/>.
        /// </summary>
        /// <returns>An enumerator for the contents of the <see
        /// cref="ConcurrentQueue{T}"/>.</returns>
        /// <remarks>
        /// The enumeration represents a moment-in-time snapshot of the contents
        /// of the queue.  It does not reflect any updates to the collection after 
        /// <see cref="GetEnumerator"/> was called.  The enumerator is safe to use
        /// concurrently with reads from and writes to the queue.
        /// </remarks>
        public IEnumerator<T> GetEnumerator()
        {
            return ToList().GetEnumerator();
        }

        /// <summary>
        /// Adds an object to the end of the <see cref="ConcurrentQueue{T}"/>.
        /// </summary>
        /// <param name="item">The object to add to the end of the <see
        /// cref="ConcurrentQueue{T}"/>. The value can be a null reference
        /// (Nothing in Visual Basic) for reference types.
        /// </param>
        public void Enqueue(T item)
        {
            SpinWait spin = new SpinWait();
            while (true)
            {
                Segment tail = _tail;
                if (tail.TryAppend(item))
                {
                    Interlocked.Increment(ref _Count);
                    return;
                }
                spin.SpinOnce();
            }
        }


        /// <summary>
        /// Attempts to remove and return the object at the beginning of the <see
        /// cref="ConcurrentQueue{T}"/>.
        /// </summary>
        /// <param name="result">
        /// When this method returns, if the operation was successful, <paramref name="result"/> contains the
        /// object removed. If no object was available to be removed, the value is unspecified.
        /// </param>
        /// <returns>true if an element was removed and returned from the beginning of the <see
        /// cref="ConcurrentQueue{T}"/>
        /// successfully; otherwise, false.</returns>
        public bool TryDequeue(out T result)
        {
            while (!IsEmpty)
            {
                Segment head = _head;
                if (head.TryRemove(out result))
                {
                    Interlocked.Decrement(ref _Count);
                    return true;
                }
                //since method IsEmpty spins, we don't need to spin in the while loop
            }
            result = default(T);
            return false;
        }

        /// <summary>
        /// Attempts to return an object from the beginning of the <see cref="ConcurrentQueue{T}"/>
        /// without removing it.
        /// </summary>
        /// <param name="result">When this method returns, <paramref name="result"/> contains an object from
        /// the beginning of the <see cref="T:System.Collections.Concurrent.ConcurrentQueue{T}"/> or an
        /// unspecified value if the operation failed.</param>
        /// <returns>true if and object was returned successfully; otherwise, false.</returns>
        public bool TryPeek(out T result)
        {
            while (!IsEmpty)
            {
                Segment head = _head;
                if (head.TryPeek(out result))
                {
                    if (head == _head)
                    {
                        return true;
                    }
                }
                //since method IsEmpty spins, we don't need to spin in the while loop
            }
            result = default(T);
            return false;
        }


        /// <summary>
        /// private class for ConcurrentQueue. 
        /// a queue is a linked list of small arrays, each node is called a segment.
        /// A segment contains an array, a pointer to the next segment, and _low, _high indices recording
        /// the first and last valid elements of the array.
        /// </summary>
        private class Segment
        {
            //we define two volatile arrays: _array and _state. Note that the accesses to the array items 
            //do not get volatile treatment. But we don't need to worry about loading adjacent elements or 
            //store/load on adjacent elements would suffer reordering. 
            // - Two stores:  these are at risk, but CLRv2 memory model guarantees store-release hence we are safe.
            // - Two loads: because one item from two volatile arrays are accessed, the loads of the array references
            //          are sufficient to prevent reordering of the loads of the elements.
            internal volatile T[] _array;

            // For each entry in _array, the corresponding entry in _state indicates whether this position contains 
            // a valid value. _state is initially all false. 
            internal volatile VolatileBool[] _state;

            //pointer to the next segment. null if the current segment is the last segment
            private volatile Segment _next;

            //We use this zero based index to track how many segments have been created for the queue, and
            //to compute how many active segments are there currently. 
            // * The number of currently active segments is : _tail._index - _head._index + 1;
            // * _index is incremented with every Segment.Grow operation. We use Int64 type, and we can safely 
            //   assume that it never overflows. To overflow, we need to do 2^63 increments, even at a rate of 4 
            //   billion (2^32) increments per second, it takes 2^31 seconds, which is about 64 years.
            internal long _index;

            //indices of where the first and last valid values
            // - _low points to the position of the next element to pop from this segment, range [0, infinity)
            //      _low >= SEGMENT_SIZE implies the segment is disposable
            // - _high points to the position of the latest pushed element, range [-1, infinity)
            //      _high == -1 implies the segment is new and empty
            //      _high >= SEGMENT_SIZE-1 means this segment is ready to grow. 
            //        and the thread who sets _high to SEGMENT_SIZE-1 is responsible to grow the segment
            // - Math.Min(_low, SEGMENT_SIZE) > Math.Min(_high, SEGMENT_SIZE-1) implies segment is empty
            // - initially _low =0 and _high=-1;
            private volatile int _low;
            private volatile int _high;

            //internal int _ref_cnt;
            internal volatile bool _is_free;

            private volatile ConcurrentQueueGrowOnly<T> _source;

            /// <summary>
            /// Create and initialize a segment with the specified index.
            /// </summary>
            internal Segment(long index, ConcurrentQueueGrowOnly<T> source)
            {
                //_ref_cnt = 1;
                _is_free = true;
                _array = new T[SEGMENT_SIZE];
                _state = new VolatileBool[SEGMENT_SIZE]; //all initialized to false
                _high = -1;
                System.Diagnostics.Debug.Assert(index >= 0);
                _index = index;
                _source = source;
            }

            /// <summary>
            /// return the next segment
            /// </summary>
            internal Segment Next
            {
                get { return _next; }
            }

            /// <summary>
            /// return true if the current segment is empty (doesn't have any element available to dequeue, 
            /// false otherwise
            /// </summary>
            internal bool IsEmpty
            {
                get { return (Low > High); }
            }

            /// <summary>
            /// Add an element to the tail of the current segment
            /// exclusively called by ConcurrentQueue.InitializedFromCollection
            /// InitializeFromCollection is responsible to guarantee that there is no index overflow,
            /// and there is no contention
            /// </summary>
            /// <param name="value"></param>
            internal void UnsafeAdd(T value)
            {
                System.Diagnostics.Debug.Assert(_high < SEGMENT_SIZE - 1);
                _high++;
                _array[_high] = value;
                _state[_high].Value = true;
            }

            /// <summary>
            /// Create a new segment and append to the current one
            /// Does not update the _tail pointer
            /// exclusively called by ConcurrentQueue.InitializedFromCollection
            /// InitializeFromCollection is responsible to guarantee that there is no index overflow,
            /// and there is no contention
            /// </summary>
            /// <returns>the reference to the new Segment</returns>
            internal Segment UnsafeGrow()
            {
                System.Diagnostics.Debug.Assert(_high >= SEGMENT_SIZE - 1);
                Segment newSegment = new Segment(_index + 1, _source); //_index is Int64, we don't need to worry about overflow
                newSegment._is_free = false;
                _next = newSegment;
                return newSegment;
            }

            /// <summary>
            /// Create a new segment and append to the current one
            /// Update the _tail pointer
            /// This method is called when there is no contention
            /// </summary>
            internal void Grow()
            {
                Segment next;
                if ((next = _next) == null)
                {
                    Segment newSegment = new Segment(0, _source);
                    SpinWait spin = new SpinWait();
                    var freetail = _source._freetail;
                    while (Interlocked.CompareExchange(ref _source._freetail, newSegment, freetail) != freetail)
                    {
                        spin.SpinOnce();
                        freetail = _source._freetail;
                    }
                    long previndex = 0;
                    while ((previndex = Volatile.Read(ref freetail._index)) == 0)
                    {
                        spin.SpinOnce();
                    }
                    Volatile.Write(ref newSegment._index, previndex + 1); //_index is Int64, we don't need to worry about overflow
                    freetail._next = newSegment;

                    spin.Reset();
                    while ((next = _next) == null)
                    {
                        spin.SpinOnce();
                    }
                }

                System.Diagnostics.Debug.Assert(_source._tail == this);
                next._is_free = false;
                _source._tail = next;
            }


            /// <summary>
            /// Try to append an element at the end of this segment.
            /// </summary>
            /// <param name="value">the element to append</param>
            /// <param name="tail">The tail.</param>
            /// <returns>true if the element is appended, false if the current segment is full</returns>
            /// <remarks>if appending the specified element succeeds, and after which the segment is full, 
            /// then grow the segment</remarks>
            internal bool TryAppend(T value)
            {
                //quickly check if _high is already over the boundary, if so, bail out
                if (_high >= SEGMENT_SIZE - 1)
                {
                    return false;
                }
                if (_is_free)
                {
                    return false;
                }

                //Now we will use a CAS to increment _high, and store the result in newhigh.
                //Depending on how many free spots left in this segment and how many threads are doing this Increment
                //at this time, the returning "newhigh" can be 
                // 1) < SEGMENT_SIZE - 1 : we took a spot in this segment, and not the last one, just insert the value
                // 2) == SEGMENT_SIZE - 1 : we took the last spot, insert the value AND grow the segment
                // 3) > SEGMENT_SIZE - 1 : we failed to reserve a spot in this segment, we return false to 
                //    Queue.Enqueue method, telling it to try again in the next segment.

                int newhigh = SEGMENT_SIZE; //initial value set to be over the boundary

                //We need do Interlocked.Increment and value/state update in a finally block to ensure that they run
                //without interuption. This is to prevent anything from happening between them, and another dequeue
                //thread maybe spinning forever to wait for _state[] to be true;
                try
                { }
                finally
                {
                    newhigh = Interlocked.Increment(ref _high);
                    if (newhigh <= SEGMENT_SIZE - 1)
                    {
                        _array[newhigh] = value;
                        _state[newhigh].Value = true;
                    }

                    //if this thread takes up the last slot in the segment, then this thread is responsible
                    //to grow a new segment. Calling Grow must be in the finally block too for reliability reason:
                    //if thread abort during Grow, other threads will be left busy spinning forever.
                    if (newhigh == SEGMENT_SIZE - 1)
                    {
                        Grow();
                    }
                }

                //if newhigh <= SEGMENT_SIZE-1, it means the current thread successfully takes up a spot
                return newhigh <= SEGMENT_SIZE - 1;
            }


            /// <summary>
            /// try to remove an element from the head of current segment
            /// </summary>
            /// <param name="result">The result.</param>
            /// <param name="head">The head.</param>
            /// <returns>return false only if the current segment is empty</returns>
            internal bool TryRemove(out T result)
            {
                SpinWait spin = new SpinWait();
                int lowLocal = Low, highLocal = High;
                while (lowLocal <= highLocal)
                {
                    //try to update _low
                    if (Interlocked.CompareExchange(ref _low, lowLocal + 1, lowLocal) == lowLocal)
                    {
                        //if the specified value is not available (this spot is taken by a push operation,
                        // but the value is not written into yet), then spin
                        SpinWait spinLocal = new SpinWait();
                        while (!_state[lowLocal].Value)
                        {
                            spinLocal.SpinOnce();
                        }
                        result = _array[lowLocal];

                        // If there is no other thread taking snapshot (GetEnumerator(), ToList(), etc), reset the deleted entry to null.
                        // It is ok if after this conditional check _numSnapshotTakers becomes > 0, because new snapshots won't include 
                        // the deleted entry at _array[lowLocal]. 
                        //if (_source._numSnapshotTakers <= 0)
                        {
                            _array[lowLocal] = default(T); //release the reference to the object. 
                        }

                        //if the current thread sets _low to SEGMENT_SIZE, which means the current segment becomes
                        //disposable, then this thread is responsible to dispose this segment, and reset _head 
                        if (lowLocal + 1 == SEGMENT_SIZE)
                        {
                            //  Invariant: we only dispose the current _head, not any other segment
                            //  In usual situation, disposing a segment is simply setting _head to _head._next
                            //  But there is one special case, where _head and _tail points to the same and ONLY
                            //segment of the queue: Another thread A is doing Enqueue and finds that it needs to grow,
                            //while the *current* thread is doing *this* Dequeue operation, and finds that it needs to 
                            //dispose the current (and ONLY) segment. Then we need to wait till thread A finishes its 
                            //Grow operation, this is the reason of having the following while loop
                            spinLocal = new SpinWait();
                            Segment next;
                            while ((next = _next) == null)
                            {
                                spinLocal.SpinOnce();
                            }
                            System.Diagnostics.Debug.Assert(_source._head == this);
                            _source._head = next;

                            Recycle();
                        }
                        return true;
                    }
                    else
                    {
                        //CAS failed due to contention: spin briefly and retry
                        spin.SpinOnce();
                        lowLocal = Low; highLocal = High;
                    }
                }//end of while
                result = default(T);
                return false;
            }
            internal void Recycle()
            {
                // cleanup this.
                _is_free = true;
                Volatile.Write(ref _index, 0);
                for (int i = 0; i < _array.Length; ++i)
                {
                    _array[i] = default(T);
                    _state[i].Value = false;
                }
                _next = null;
                _low = 0;
                _high = -1;

                // recycle this.
                SpinWait spin = new SpinWait();
                var freetail = _source._freetail;
                while (Interlocked.CompareExchange(ref _source._freetail, this, freetail) != freetail)
                {
                    spin.SpinOnce();
                    freetail = _source._freetail;
                }
                long previndex = 0;
                while ((previndex = Volatile.Read(ref freetail._index)) == 0)
                {
                    spin.SpinOnce();
                }
                Volatile.Write(ref _index, previndex + 1);
                freetail._next = this;
            }
#pragma warning restore 0420
            /// <summary>
            /// try to peek the current segment
            /// </summary>
            /// <param name="result">holds the return value of the element at the head position, 
            /// value set to default(T) if there is no such an element</param>
            /// <returns>true if there are elements in the current segment, false otherwise</returns>
            internal bool TryPeek(out T result)
            {
                result = default(T);
                while (true)
                {
                    int lowLocal = Low;
                    if (lowLocal > High)
                        return false;
                    SpinWait spin = new SpinWait();
                    while (!_state[lowLocal].Value)
                    {
                        spin.SpinOnce();
                    }
                    var val = _array[lowLocal];
                    if (lowLocal == Low)
                    {
                        result = val;
                        break;
                    }
                }
                return true;
            }

            ///// <summary>
            ///// Adds part or all of the current segment into a List.
            ///// </summary>
            ///// <param name="list">the list to which to add</param>
            ///// <param name="start">the start position</param>
            ///// <param name="end">the end position</param>
            //internal void AddToList(List<T> list, int start, int end)
            //{
            //    for (int i = start; i <= end; i++)
            //    {
            //        SpinWait spin = new SpinWait();
            //        while (!_state[i]._value)
            //        {
            //            spin.SpinOnce();
            //        }
            //        list.Add(_array[i]);
            //    }
            //}

            /// <summary>
            /// return the position of the head of the current segment
            /// Value range [0, SEGMENT_SIZE], if it's SEGMENT_SIZE, it means this segment is exhausted and thus empty
            /// </summary>
            internal int Low
            {
                get
                {
                    return Math.Min(_low, SEGMENT_SIZE);
                }
            }

            /// <summary>
            /// return the logical position of the tail of the current segment      
            /// Value range [-1, SEGMENT_SIZE-1]. When it's -1, it means this is a new segment and has no elemnet yet
            /// </summary>
            internal int High
            {
                get
                {
                    //if _high > SEGMENT_SIZE, it means it's out of range, we should return
                    //SEGMENT_SIZE-1 as the logical position
                    return Math.Min(_high, SEGMENT_SIZE - 1);
                }
            }
        }
    }

#if UNITY_INCLUDE_TESTS
    public static class ConcurrentCollectionTest
    {
        public static void TestConcurrentQueue<T>() where T : IProducerConsumerCollection<int>, new()
        {
            ConcurrentDictionary<int, bool> dict = new ConcurrentDictionary<int, bool>();
            const int cntprethread = 1000;
            const int threadcnt = 8;
            const int totalcnt = cntprethread * threadcnt;
            var queue = new T();
            int index = 0;
            Action<TaskProgress> produce = prog =>
            {
                int val;
                for (int i = 0; i < cntprethread; ++i)
                {
                    val = Interlocked.Increment(ref index);
                    dict[val] = true;
                    if (queue.TryAdd(val))
                    {
                        PlatDependant.LogInfo("Enqueue " + val);
                    }
                    else
                    {
                        dict.TryRemove(val, out bool b);
                        PlatDependant.LogWarning("Enqueue Failed " + val);
                    }
                }
            };
            Action<TaskProgress> consume = prog =>
            {
                int val;
                int tick = Environment.TickCount;
                while (Environment.TickCount - tick <= 2000)
                {
                    if (index < totalcnt)
                    {
                        tick = Environment.TickCount;
                    }
                    if (queue.TryTake(out val))
                    {
                        PlatDependant.LogInfo("Dequeue " + val);
                        dict.TryRemove(val, out bool b);
                    }
                }
                PlatDependant.LogInfo(dict.Count);
                foreach (var kvp in dict)
                {
                    PlatDependant.LogError(kvp.Key);
                }
            };

            for (int i = 0; i < threadcnt; ++i)
            {
                PlatDependant.RunBackground(produce);
                PlatDependant.RunBackground(consume);
            }
        }

        public static void TestConcurrentQueueLite<T>() where T : IProducerConsumerCollection<int>, new()
        {
            ConcurrentDictionary<int, bool> dict = new ConcurrentDictionary<int, bool>();
            var queue = new T();
            bool done = false;
            PlatDependant.RunBackground(prog =>
            {
                for (int i = 0; i < 1000; ++i)
                {
                    var val = i;
                    dict[val] = true;
                    if (queue.TryAdd(val))
                    {
                        PlatDependant.LogInfo("Enqueue " + val);
                    }
                    else
                    {
                        dict.TryRemove(val, out bool b);
                        PlatDependant.LogWarning("Enqueue Failed " + val);
                    }
                    Thread.Sleep(1000 / 30);
                }
                Volatile.Write(ref done, true);
            });
            PlatDependant.RunBackground(prog =>
            {
                int tick = Environment.TickCount;
                while (Environment.TickCount - tick <= 2000)
                {
                    if (!Volatile.Read(ref done))
                    {
                        tick = Environment.TickCount;
                    }
                    int val;
                    if (queue.TryTake(out val))
                    {
                        PlatDependant.LogInfo("Dequeue " + val);
                        dict.TryRemove(val, out bool b);
                    }
                }
                PlatDependant.LogInfo(dict.Count);
                foreach (var kvp in dict)
                {
                    PlatDependant.LogError(kvp.Key);
                }
            });
        }
    }
#endif

    public class WriteReadInTwoThread<T> where T : class, new()
    {
        protected class DataWithVersion
        {
            public T Data;
            public int Version;
        }

        protected DataWithVersion _Read = new DataWithVersion() { Data = new T() };
        protected DataWithVersion _Write = new DataWithVersion() { Data = new T() };
        protected DataWithVersion _Inter = new DataWithVersion() { Data = new T() };

        public T Read
        {
            get
            {
                var ver = _Read.Version;
                _Read = Interlocked.Exchange(ref _Inter, _Read);
                if (ver > _Read.Version)
                {
                    _Read = Interlocked.Exchange(ref _Inter, _Read);
                }
                return _Read.Data;
            }
        }
        public T Write
        {
            get
            {
                return _Write.Data;
            }
        }
        public void FinishWrite()
        {
            var ver = ++_Write.Version;
            _Write = Interlocked.Exchange(ref _Inter, _Write);
            _Write.Version = ver;
        }
    }
}