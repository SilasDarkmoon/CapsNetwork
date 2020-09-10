using Capstones.Net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Capstones.UnityEngineEx
{
    public struct NativeBufferStruct : IList<byte>, IDisposable
    {
        private IntPtr _Address;
        private int _Size;

        public NativeBufferStruct(int cnt)
        {
            if (cnt <= 0)
            {
                _Size = 0;
                _Address = IntPtr.Zero;
            }
            else
            {
                _Address = System.Runtime.InteropServices.Marshal.AllocHGlobal(cnt);
                _Size = cnt;
            }
        }
        internal IntPtr Address { get { return _Address; } }
        public void Resize(int cnt)
        {
            if (cnt < 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            if (cnt == _Size)
            {
                return;
            }
            if (cnt == 0)
            {
                Dispose();
            }
            else
            {
                if (_Address == IntPtr.Zero)
                {
                    _Address = System.Runtime.InteropServices.Marshal.AllocHGlobal(cnt);
                    _Size = cnt;
                }
                else
                {
                    _Address = System.Runtime.InteropServices.Marshal.ReAllocHGlobal(_Address, (IntPtr)cnt);
                    _Size = cnt;
                }
            }
        }

        public byte this[int index]
        {
            get
            {
                if (index >= 0 && index < _Size)
                {
                    return System.Runtime.InteropServices.Marshal.ReadByte(_Address, index);
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }
            set
            {
                if (index >= 0 && index < _Size)
                {
                    System.Runtime.InteropServices.Marshal.WriteByte(_Address, index, value);
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }
        }
        public int Count { get { return _Size; } }
        public bool IsReadOnly { get { return false; } }
        public void Add(byte item)
        {
            throw new NotSupportedException();
        }
        public void Clear()
        {
            throw new NotSupportedException();
        }
        public bool Contains(byte item)
        {
            for (int i = 0; i < _Size; ++i)
            {
                if (this[i] == item)
                {
                    return true;
                }
            }
            return false;
        }
        public void CopyTo(byte[] array, int arrayIndex)
        {
            if (arrayIndex >= 0 && _Size > 0)
            {
                System.Runtime.InteropServices.Marshal.Copy(_Address, array, arrayIndex, Math.Min(_Size, array.Length - arrayIndex));
            }
        }
        public IEnumerator<byte> GetEnumerator()
        {
            for (int i = 0; i < _Size; ++i)
            {
                yield return this[i];
            }
        }
        public int IndexOf(byte item)
        {
            for (int i = 0; i < _Size; ++i)
            {
                if (this[i] == item)
                {
                    return i;
                }
            }
            return -1;
        }
        public void Insert(int index, byte item)
        {
            throw new NotSupportedException();
        }
        public bool Remove(byte item)
        {
            throw new NotSupportedException();
        }
        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose()
        {
            if (_Address != IntPtr.Zero)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(_Address);
                _Address = IntPtr.Zero;
                _Size = 0;
            }
        }
    }
    public class NativeBuffer : IList<byte>, IDisposable
    {
        protected NativeBufferStruct _Buffer;

        public NativeBuffer(int cnt)
        {
            _Buffer = new NativeBufferStruct(cnt);
        }

        internal IntPtr Address { get { return _Buffer.Address; } }
        public void Resize(int cnt)
        {
            _Buffer.Resize(cnt);
        }

        public byte this[int index]
        {
            get { return _Buffer[index]; }
            set { _Buffer[index] = value; }
        }
        public int Count { get { return _Buffer.Count; } }
        public bool IsReadOnly { get { return _Buffer.IsReadOnly; } }
        public void Add(byte item)
        {
            _Buffer.Add(item);
        }
        public void Clear()
        {
            _Buffer.Clear();
        }
        public bool Contains(byte item)
        {
            return _Buffer.Contains(item);
        }
        public void CopyTo(byte[] array, int arrayIndex)
        {
            _Buffer.CopyTo(array, arrayIndex);
        }
        public IEnumerator<byte> GetEnumerator()
        {
            return _Buffer.GetEnumerator();
        }
        public int IndexOf(byte item)
        {
            return _Buffer.IndexOf(item);
        }
        public void Insert(int index, byte item)
        {
            _Buffer.Insert(index, item);
        }
        public bool Remove(byte item)
        {
            return _Buffer.Remove(item);
        }
        public void RemoveAt(int index)
        {
            _Buffer.RemoveAt(index);
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _Buffer.GetEnumerator();
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                //if (disposing)
                //{
                //    // 释放托管状态(托管对象)。
                //}

                // 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // 将大型字段设置为 null。
                _Buffer.Dispose();

                disposedValue = true;
            }
        }

        ~NativeBuffer()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(false);
        }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    public struct NativeByteListStruct : IList<byte>, IDisposable
    {
        private NativeBufferStruct _Buffer;
        private int _Count;

        public NativeByteListStruct(int size)
        {
            _Buffer = new NativeBufferStruct(size);
            _Count = 0;
        }
        internal void EnsureSpace(int size)
        {
            if (size > _Buffer.Count)
            {
                _Buffer.Resize(size);
            }
        }
        internal void EnsureSpace()
        {
            if (_Count >= _Buffer.Count)
            {
                int newsize = _Count;
                if (newsize > 100)
                {
                    newsize = newsize + 100;
                }
                else if (newsize == 0)
                {
                    newsize = 4;
                }
                else
                {
                    newsize = newsize * 2;
                }
                EnsureSpace(newsize);
            }
        }

        public byte this[int index]
        {
            get
            {
                if (index >= 0 && index < _Count)
                {
                    return _Buffer[index];
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }
            set
            {
                if (index >= 0 && index < _Count)
                {
                    _Buffer[index] = value;
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }
        }
        public int Count { get { return _Count; } }
        public bool IsReadOnly { get { return false; } }
        public void Add(byte item)
        {
            EnsureSpace();
            _Buffer[_Count++] = item;
        }
        public void Clear()
        {
            _Count = 0;
        }
        public bool Contains(byte item)
        {
            for (int i = 0; i < _Count; ++i)
            {
                if (_Buffer[i] == item)
                {
                    return true;
                }
            }
            return false;
        }
        public void CopyTo(byte[] array, int arrayIndex)
        {
            if (arrayIndex >= 0 && _Count > 0)
            {
                System.Runtime.InteropServices.Marshal.Copy(_Buffer.Address, array, arrayIndex, Math.Min(_Count, array.Length - arrayIndex));
            }
        }
        public IEnumerator<byte> GetEnumerator()
        {
            for (int i = 0; i < _Count; ++i)
            {
                yield return _Buffer[i];
            }
        }
        public int IndexOf(byte item)
        {
            for (int i = 0; i < _Count; ++i)
            {
                if (_Buffer[i] == item)
                {
                    return i;
                }
            }
            return -1;
        }
        public void Insert(int index, byte item)
        {
            if (index >= 0 && index <= _Count)
            {
                EnsureSpace();
                ++_Count;
                for (int i = _Count - 1; i > index; --i)
                {
                    _Buffer[i] = _Buffer[i - 1];
                }
                _Buffer[index] = item;
            }
        }
        public bool Remove(byte item)
        {
            var index = IndexOf(item);
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }
            return false;
        }
        public void RemoveAt(int index)
        {
            if (index >= 0 && index < _Count)
            {
                --_Count;
                for (int i = index; i < _Count; ++i)
                {
                    _Buffer[i] = _Buffer[i + 1];
                }
            }
            else
            {
                throw new IndexOutOfRangeException();
            }
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose()
        {
            _Buffer.Dispose();
            _Count = 0;
        }
    }
    public class NativeByteList : IList<byte>, IDisposable
    {
        protected NativeByteListStruct _List;

        public NativeByteList(int size)
        {
            _List = new NativeByteListStruct(size);
        }
        public NativeByteList() : this(0)
        {
        }
        internal void EnsureSpace()
        {
            _List.EnsureSpace();
        }

        public byte this[int index]
        {
            get { return _List[index]; }
            set { _List[index] = value; }
        }
        public int Count { get { return _List.Count; } }
        public bool IsReadOnly { get { return _List.IsReadOnly; } }
        public void Add(byte item)
        {
            _List.Add(item);
        }
        public void Clear()
        {
            _List.Clear();
        }
        public bool Contains(byte item)
        {
            return _List.Contains(item);
        }
        public void CopyTo(byte[] array, int arrayIndex)
        {
            _List.CopyTo(array, arrayIndex);
        }
        public IEnumerator<byte> GetEnumerator()
        {
            return _List.GetEnumerator();
        }
        public int IndexOf(byte item)
        {
            return _List.IndexOf(item);
        }
        public void Insert(int index, byte item)
        {
            _List.Insert(index, item);
        }
        public bool Remove(byte item)
        {
            return _List.Remove(item);
        }
        public void RemoveAt(int index)
        {
            _List.RemoveAt(index);
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _List.GetEnumerator();
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                //if (disposing)
                //{
                //    // 释放托管状态(托管对象)。
                //}

                // 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // 将大型字段设置为 null。
                _List.Dispose();

                disposedValue = true;
            }
        }

        ~NativeByteList()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(false);
        }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class NativeBufferStream : InsertableStream
    {
        protected NativeBuffer _RealBuffer;
        protected override IList<byte> _Buffer { get { return _RealBuffer; } }

        public NativeBufferStream(int size)
        {
            if (size < 0)
            {
                size = 0;
            }
            _RealBuffer = new NativeBuffer(size + _HeadSpace);
            _Offset = _HeadSpace;
            _Count = 0;
            _Pos = 0;
        }
        public NativeBufferStream() : this(0)
        {
        }

        protected override void Resize(int cnt)
        {
            _RealBuffer.Resize(cnt);
        }
        protected override void Move(int dstOffset, int srcOffset, int cnt)
        {
            Capstones.Net.KCPLib.kcp_memmove((IntPtr)((long)_RealBuffer.Address + dstOffset), (IntPtr)((long)_RealBuffer.Address + srcOffset), cnt);
        }
        protected override void CopyTo(int srcOffset, byte[] dest, int dstOffset, int cnt)
        {
            System.Runtime.InteropServices.Marshal.Copy((IntPtr)((long)_RealBuffer.Address + srcOffset), dest, dstOffset, cnt);
        }
        protected override void CopyFrom(byte[] src, int srcOffset, int dstOffset, int cnt)
        {
            System.Runtime.InteropServices.Marshal.Copy(src, srcOffset, (IntPtr)((long)_RealBuffer.Address + dstOffset), cnt);
        }

        #region Test
        public static class NativeBufferStreamTest
        {
            public static bool TestOverwriteModeOfList()
            {
                using (NativeBufferStream testStream = new NativeBufferStream())
                {
                    List<byte> data0 = new List<byte>();
                    for (int i = 0; i < 100; ++i)
                    {
                        data0.Add((byte)i);
                    }
                    testStream.WriteList(data0, 0, data0.Count);
                    testStream.Seek(10, SeekOrigin.End);
                    testStream.WriteList(data0, 0, data0.Count);
                    testStream.Seek(-10, SeekOrigin.Begin);
                    testStream.WriteList(data0, 0, data0.Count);
                    testStream.Seek(-200, SeekOrigin.Begin);
                    testStream.WriteList(data0, 0, data0.Count);

                    if (testStream.Count != 420)
                    {
                        return false;
                    }
                    byte[] result = new byte[420];
                    testStream.Seek(0, SeekOrigin.Begin);
                    testStream.ReadList(result, 0, result.Length);
                    for (int i = 0; i < 100; ++i)
                    {
                        if (result[i] != i)
                        {
                            return false;
                        }
                    }
                    for (int i = 0; i < 100; ++i)
                    {
                        if (result[i + 200] != i)
                        {
                            return false;
                        }
                    }
                    for (int i = 100; i < 110; ++i)
                    {
                        if (result[i + 200] != i - 10)
                        {
                            return false;
                        }
                    }
                    for (int i = 0; i < 100; ++i)
                    {
                        if (result[i + 320] != i)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            public static bool TestInsertModeOfList()
            {
                using (NativeBufferStream testStream = new NativeBufferStream())
                {
                    testStream.InsertMode = true;

                    List<byte> data0 = new List<byte>();
                    for (int i = 0; i < 100; ++i)
                    {
                        data0.Add((byte)i);
                    }
                    testStream.WriteList(data0, 0, data0.Count);
                    testStream.Seek(50, SeekOrigin.Begin);
                    testStream.WriteList(data0, 0, data0.Count);
                    testStream.Seek(10, SeekOrigin.End);
                    testStream.WriteList(data0, 0, data0.Count);
                    testStream.Seek(-10, SeekOrigin.Begin);
                    testStream.WriteList(data0, 0, data0.Count);
                    testStream.Seek(0, SeekOrigin.Begin);
                    testStream.WriteList(data0, 0, data0.Count);
                    testStream.Seek(-200, SeekOrigin.Begin);
                    testStream.WriteList(data0, 0, data0.Count);

                    if (testStream.Count != 820)
                    {
                        return false;
                    }
                    byte[] result = new byte[820];
                    testStream.Seek(0, SeekOrigin.Begin);
                    testStream.ReadList(result, 0, result.Length);
                    for (int i = 0; i < 100; ++i)
                    {
                        if (result[i] != i)
                        {
                            return false;
                        }
                    }
                    for (int i = 0; i < 100; ++i)
                    {
                        if (result[i + 300] != i)
                        {
                            return false;
                        }
                    }
                    for (int i = 0; i < 100; ++i)
                    {
                        if (result[i + 400] != i)
                        {
                            return false;
                        }
                    }
                    for (int i = 0; i < 50; ++i)
                    {
                        if (result[i + 510] != i)
                        {
                            return false;
                        }
                    }
                    for (int i = 0; i < 100; ++i)
                    {
                        if (result[i + 560] != i)
                        {
                            return false;
                        }
                    }
                    for (int i = 0; i < 50; ++i)
                    {
                        if (result[i + 660] != i + 50)
                        {
                            return false;
                        }
                    }
                    for (int i = 0; i < 100; ++i)
                    {
                        if (result[i + 720] != i)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            public static bool TestOverwriteModeOfArray()
            {
                using (NativeBufferStream testStream = new NativeBufferStream())
                {
                    byte[] data0 = new byte[100];
                    for (int i = 0; i < 100; ++i)
                    {
                        data0[i] = (byte)i;
                    }
                    testStream.Write(data0, 0, data0.Length);
                    testStream.Seek(10, SeekOrigin.End);
                    testStream.Write(data0, 0, data0.Length);
                    testStream.Seek(-10, SeekOrigin.Begin);
                    testStream.Write(data0, 0, data0.Length);
                    testStream.Seek(-200, SeekOrigin.Begin);
                    testStream.Write(data0, 0, data0.Length);

                    if (testStream.Count != 420)
                    {
                        return false;
                    }
                    byte[] result = new byte[420];
                    testStream.Seek(0, SeekOrigin.Begin);
                    testStream.Read(result, 0, result.Length);
                    for (int i = 0; i < 100; ++i)
                    {
                        if (result[i] != i)
                        {
                            return false;
                        }
                    }
                    for (int i = 0; i < 100; ++i)
                    {
                        if (result[i + 200] != i)
                        {
                            return false;
                        }
                    }
                    for (int i = 100; i < 110; ++i)
                    {
                        if (result[i + 200] != i - 10)
                        {
                            return false;
                        }
                    }
                    for (int i = 0; i < 100; ++i)
                    {
                        if (result[i + 320] != i)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            public static bool TestInsertModeOfArray()
            {
                using (NativeBufferStream testStream = new NativeBufferStream())
                {
                    testStream.InsertMode = true;

                    byte[] data0 = new byte[100];
                    for (int i = 0; i < 100; ++i)
                    {
                        data0[i] = (byte)i;
                    }
                    testStream.Write(data0, 0, data0.Length);
                    testStream.Seek(50, SeekOrigin.Begin);
                    testStream.Write(data0, 0, data0.Length);
                    testStream.Seek(10, SeekOrigin.End);
                    testStream.Write(data0, 0, data0.Length);
                    testStream.Seek(-10, SeekOrigin.Begin);
                    testStream.Write(data0, 0, data0.Length);
                    testStream.Seek(0, SeekOrigin.Begin);
                    testStream.Write(data0, 0, data0.Length);
                    testStream.Seek(-200, SeekOrigin.Begin);
                    testStream.Write(data0, 0, data0.Length);

                    if (testStream.Count != 820)
                    {
                        return false;
                    }
                    byte[] result = new byte[820];
                    testStream.Seek(0, SeekOrigin.Begin);
                    testStream.Read(result, 0, result.Length);
                    for (int i = 0; i < 100; ++i)
                    {
                        if (result[i] != i)
                        {
                            return false;
                        }
                    }
                    for (int i = 0; i < 100; ++i)
                    {
                        if (result[i + 300] != i)
                        {
                            return false;
                        }
                    }
                    for (int i = 0; i < 100; ++i)
                    {
                        if (result[i + 400] != i)
                        {
                            return false;
                        }
                    }
                    for (int i = 0; i < 50; ++i)
                    {
                        if (result[i + 510] != i)
                        {
                            return false;
                        }
                    }
                    for (int i = 0; i < 100; ++i)
                    {
                        if (result[i + 560] != i)
                        {
                            return false;
                        }
                    }
                    for (int i = 0; i < 50; ++i)
                    {
                        if (result[i + 660] != i + 50)
                        {
                            return false;
                        }
                    }
                    for (int i = 0; i < 100; ++i)
                    {
                        if (result[i + 720] != i)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }
        #endregion
    }
}
