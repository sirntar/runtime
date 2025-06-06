// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO;

namespace System.Reflection.Internal
{
    /// <summary>
    /// Represents raw memory owned by an external object.
    /// </summary>
    internal sealed unsafe class ExternalMemoryBlockProvider : MemoryBlockProvider
    {
        private byte* _memory;
        private int _size;

        public ExternalMemoryBlockProvider(byte* memory, int size)
        {
            _memory = memory;
            _size = size;
        }

        public override int Size
        {
            get
            {
                return _size;
            }
        }

        protected override AbstractMemoryBlock GetMemoryBlockImpl(int start, int size)
        {
            return new ExternalMemoryBlock(this, _memory + start, size);
        }

        protected override void Dispose(bool disposing)
        {
            Debug.Assert(disposing);

            // we don't own the memory, just null out the pointer.
            _memory = null;
            _size = 0;
        }

        public byte* Pointer
        {
            get
            {
                return _memory;
            }
        }
    }
}
