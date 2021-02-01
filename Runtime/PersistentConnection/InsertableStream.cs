using Capstones.Net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Capstones.UnityEngineEx
{
    public abstract class InsertableStream : Stream, IList<byte>
    {
        protected const int _HeadSpace = 128;

        protected abstract IList<byte> _Buffer { get; }
        protected int _Offset;
        protected int _Count;
        protected int _Pos;

        public int Offset { get { return _Offset; } }

        protected bool _InsertMode = false;
        public bool InsertMode { get { return _InsertMode; } set { _InsertMode = value; } }

        protected abstract void Resize(int cnt);
        protected abstract void Move(int dstOffset, int srcOffset, int cnt);
        protected abstract void CopyTo(int srcOffset, byte[] dest, int dstOffset, int cnt);
        protected abstract void CopyFrom(byte[] src, int srcOffset, int dstOffset, int cnt);

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
                Resize(newsize + _Offset);
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
                    Resize(newsize + _Offset);
                }
                _Count = cnt;
            }
            else if (pos < 0)
            {
                if (_Offset + pos < 0)
                {
                    int newsize = _Buffer.Count - _Offset - pos + _HeadSpace;
                    Resize(newsize);
                    int copyoffset = _HeadSpace - pos;
                    Move(copyoffset, _Offset, _Count);
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
                Move(_Offset + _Pos + count, _Offset + _Pos, _Count - _Pos - count);
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
        public void Consume()
        {
            _Offset += _Pos;
            _Count -= _Pos;
            _Pos = 0;
        }

        #region Dispose
        protected int _DisposedCnt;
        protected override void Dispose(bool disposing)
        {
            if (System.Threading.Interlocked.Increment(ref _DisposedCnt) == 1)
            {
                //if (disposing)
                //{
                //    GC.SuppressFinalize(this);
                //}
                var disposableBuffer = _Buffer as IDisposable;
                if (disposableBuffer != null)
                {
                    disposableBuffer.Dispose();
                }
                _Offset = 0;
                _Count = 0;
                _Pos = 0;
                base.Dispose(disposing);
            }
        }
        ~InsertableStream()
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
            CopyTo(_Offset + _Pos, buffer, offset, rcnt);
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
                Move(_Offset + _Pos + count, _Offset + _Pos, _Count - _Pos - count);
            }
            CopyFrom(buffer, offset, _Offset + _Pos, count);
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
            CopyFrom(buffer, offset, _Offset + _Pos, count);
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
                CopyTo(_Offset, array, arrayIndex, Math.Min(_Count, array.Length - arrayIndex));
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
    }

    public abstract class ManagedBufferStream : InsertableStream
    {
        protected byte[] _RealBuffer;
        protected override IList<byte> _Buffer { get { return _RealBuffer; } }
        public byte[] RealBuffer { get { return _RealBuffer; } }

        protected override void Resize(int cnt)
        {
            var newBuffer = new byte[cnt];
            Buffer.BlockCopy(_RealBuffer, 0, newBuffer, 0, Math.Min(cnt, _RealBuffer.Length));
            _RealBuffer = newBuffer;
        }
        protected override void Move(int dstOffset, int srcOffset, int cnt)
        {
            Buffer.BlockCopy(_RealBuffer, srcOffset, _RealBuffer, dstOffset, cnt);
        }
        protected override void CopyTo(int srcOffset, byte[] dest, int dstOffset, int cnt)
        {
            Buffer.BlockCopy(_RealBuffer, srcOffset, dest, dstOffset, cnt);
        }
        protected override void CopyFrom(byte[] src, int srcOffset, int dstOffset, int cnt)
        {
            Buffer.BlockCopy(src, srcOffset, _RealBuffer, dstOffset, cnt);
        }
    }

    public class ArrayBufferStream : ManagedBufferStream
    {
        public ArrayBufferStream(int size)
        {
            if (size < 0)
            {
                size = 0;
            }
            _RealBuffer = new byte[size + _HeadSpace];
            _Offset = _HeadSpace;
            _Count = 0;
            _Pos = 0;
        }
        public ArrayBufferStream(byte[] buffer, int offset, int cnt)
        {
            _RealBuffer = buffer;
            _Offset = offset;
            _Count = cnt;
            _Pos = 0;
        }
        public ArrayBufferStream() : this(0)
        {
        }
    }
}
