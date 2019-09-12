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

    public class NativeBufferStream : Stream, IList<byte>
    {
        protected const int _HeadSpace = 128;

        protected NativeBufferStruct _Buffer;
        protected int _Offset;
        protected int _Count;
        protected int _Pos;

        protected bool _InsertMode = false;
        public bool InsertMode { get { return _InsertMode; } set { _InsertMode = value; } }

        public NativeBufferStream(int size)
        {
            if (size < 0)
            {
                size = 0;
            }
            _Buffer = new NativeBufferStruct(size + _HeadSpace);
            _Offset = _HeadSpace;
            _Count = 0;
            _Pos = 0;
        }
        public NativeBufferStream() : this(0)
        {
        }
        internal void EnsureSpace()
        {
            if (_Count + _Offset >= _Buffer.Count)
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
                _Buffer.Resize(newsize + _Offset);
            }
        }
        internal void AppendTo(int pos)
        {
            if (pos >= _Count)
            {
                int cnt = pos + 1;
                if (cnt + _Offset > _Buffer.Count)
                {
                    int newsize = cnt;
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
                    _Buffer.Resize(newsize + _Offset);
                }
                _Count = cnt;
            }
            else if (pos < 0)
            {
                if (_Offset + pos < 0)
                {
                    int newsize = _Buffer.Count - _Offset - pos + _HeadSpace;
                    _Buffer.Resize(newsize);
                    int copyoffset = _HeadSpace - pos;
                    Capstones.Net.KCPLib.kcp_memmove((IntPtr)((long)_Buffer.Address + copyoffset), (IntPtr)((long)_Buffer.Address + _Offset), _Count);
                    _Offset = _HeadSpace;
                    _Count -= pos;
                    _Pos -= pos;
                }
                else
                {
                    _Offset += pos;
                    _Count -= pos;
                    _Pos -= pos;
                }
            }
        }

        public int ReadList(IList<byte> buffer, int offset, int count)
        {
            if (_Pos < 0 || _Pos >= _Count)
            {
                return 0;
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            var canreadcnt = _Count - _Pos;
            int rcnt = Math.Min(canreadcnt, count);
            if (rcnt <= 0)
            {
                return 0;
            }
            for (int i = 0; i < rcnt; ++i)
            {
                buffer[offset + i] = _Buffer[_Offset + _Pos + i];
            }
            _Pos += rcnt;
            return rcnt;
        }
        public void InsertList(IList<byte> buffer, int offset, int count)
        {
            if (count <= 0)
            {
                return;
            }
            if (_Pos < 0)
            {
                AppendTo(_Pos);
            }
            else if (_Pos > _Count)
            {
                AppendTo(_Pos - 1);
            }
            if (_Pos < 0 || _Pos == 0 && _Count > 0)
            {
                // insert to head.
                AppendTo(-count);
                _Pos -= count;
            }
            else
            {
                // move towards tail.
                AppendTo(_Count + count - 1);
                Capstones.Net.KCPLib.kcp_memmove((IntPtr)((long)_Buffer.Address + _Offset + _Pos + count), (IntPtr)((long)_Buffer.Address + _Offset + _Pos), _Count - _Pos - count);
            }
            for (int i = 0; i < count; ++i)
            {
                _Buffer[_Offset + _Pos + i] = buffer[offset + i];
            }
            _Pos += count;
        }
        public void OverwriteList(IList<byte> buffer, int offset, int count)
        {
            if (count <= 0)
            {
                return;
            }
            if (_Pos < 0 || _Pos >= _Count)
            {
                AppendTo(_Pos);
            }
            AppendTo(_Pos + count - 1);
            for (int i = 0; i < count; ++i)
            {
                _Buffer[_Offset + _Pos + i] = buffer[offset + i];
            }
            _Pos += count;
        }
        public void WriteList(IList<byte> buffer, int offset, int count)
        {
            if (_InsertMode)
            {
                InsertList(buffer, offset, count);
            }
            else
            {
                OverwriteList(buffer, offset, count);
            }
        }

        #region Dispose
        private bool disposedValue = false; // 要检测冗余调用
        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                //if (disposing)
                //{
                //    GC.SuppressFinalize(this);
                //}
                _Buffer.Dispose();
                _Offset = 0;
                _Count = 0;
                _Pos = 0;
                disposedValue = true;
            }
            base.Dispose(disposing);
        }
        ~NativeBufferStream()
        {
            Dispose(false);
        }
        #endregion

        #region Stream
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { return _Count; } }
        public override long Position
        { // Notice: _Pos can be moved beyond head or after tail. At these point, if we write something, it means append the stream.
            get
            {
                return _Pos;
            }
            set
            {
                _Pos = (int)value;
            }
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin)
        {
            var pos = _Pos;
            if (origin == SeekOrigin.Begin)
            {
                pos = 0;
            }
            else if (origin == SeekOrigin.End)
            {
                pos = _Count;
            }
            pos += (int)offset;
            Position = pos;
            return pos;
        }
        public override void SetLength(long value)
        {
            if (value < 0 || value > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException();
            }
            var len = (int)value;
            if (len < _Count)
            {
                _Count = len;
            }
            else if (len > _Count)
            {
                AppendTo(len - 1);
            }
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_Pos < 0 || _Pos >= _Count)
            {
                return 0;
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            var canreadcnt = _Count - _Pos;
            int rcnt = Math.Min(canreadcnt, count);
            if (rcnt <= 0)
            {
                return 0;
            }
            System.Runtime.InteropServices.Marshal.Copy((IntPtr)((long)_Buffer.Address + _Offset + _Pos), buffer, offset, rcnt);
            _Pos += rcnt;
            return rcnt;
        }
        public void Insert(byte[] buffer, int offset, int count)
        {
            if (count <= 0)
            {
                return;
            }
            if (_Pos < 0)
            {
                AppendTo(_Pos);
            }
            else if (_Pos > _Count)
            {
                AppendTo(_Pos - 1);
            }
            if (_Pos < 0 || _Pos == 0 && _Count > 0)
            {
                // insert to head.
                AppendTo(-count);
                _Pos -= count;
            }
            else
            {
                // move towards tail.
                AppendTo(_Count + count - 1);
                Capstones.Net.KCPLib.kcp_memmove((IntPtr)((long)_Buffer.Address + _Offset + _Pos + count), (IntPtr)((long)_Buffer.Address + _Offset + _Pos), _Count - _Pos - count);
            }
            System.Runtime.InteropServices.Marshal.Copy(buffer, offset, (IntPtr)((long)_Buffer.Address + _Offset + _Pos), count);
            _Pos += count;
        }
        public void Overwrite(byte[] buffer, int offset, int count)
        {
            if (count <= 0)
            {
                return;
            }
            if (_Pos < 0 || _Pos >= _Count)
            {
                AppendTo(_Pos);
            }
            AppendTo(_Pos + count - 1);
            System.Runtime.InteropServices.Marshal.Copy(buffer, offset, (IntPtr)((long)_Buffer.Address + _Offset + _Pos), count);
            _Pos += count;
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_InsertMode)
            {
                Insert(buffer, offset, count);
            }
            else
            {
                Overwrite(buffer, offset, count);
            }
        }
        #endregion

        #region List
        public byte this[int index]
        {
            get { return _Buffer[_Offset + index]; }
            set { _Buffer[_Offset + index] = value; }
        }
        public int Count { get { return _Count; } }
        public bool IsReadOnly { get { return false; } }
        public void Add(byte item)
        {
            AppendTo(_Count);
            _Buffer[_Offset + _Count - 1] = item;
        }
        public void Clear()
        {
            _Offset = _HeadSpace;
            _Count = 0;
            _Pos = 0;
        }
        public bool Contains(byte item)
        {
            for (int i = 0; i < _Count; ++i)
            {
                if (_Buffer[_Offset + i] == item)
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
                System.Runtime.InteropServices.Marshal.Copy((IntPtr)((long)_Buffer.Address + _Offset), array, arrayIndex, Math.Min(_Count, array.Length - arrayIndex));
            }
        }
        public IEnumerator<byte> GetEnumerator()
        {
            for (int i = 0; i < _Count; ++i)
            {
                yield return _Buffer[_Offset + i];
            }
        }
        public int IndexOf(byte item)
        {
            for (int i = 0; i < _Count; ++i)
            {
                if (_Buffer[_Offset + i] == item)
                {
                    return i;
                }
            }
            return -1;
        }
        public void Insert(int index, byte item)
        {
            if (index <= 0)
            {
                AppendTo(index - 1);
                _Buffer[_Offset] = item;
            }
            else if (index >= _Count)
            {
                AppendTo(index);
                _Buffer[_Offset + index] = item;
            }
            else
            {
                AppendTo(_Count);
                for (int i = _Count - 1; i > index; --i)
                {
                    _Buffer[_Offset + i] = _Buffer[_Offset + i - 1];
                }
                _Buffer[_Offset + index] = item;
                if (_Pos > index)
                {
                    ++_Pos;
                }
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
                    _Buffer[_Offset + i] = _Buffer[_Offset + i + 1];
                }
                if (_Pos > index)
                {
                    --_Pos;
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
        #endregion

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
