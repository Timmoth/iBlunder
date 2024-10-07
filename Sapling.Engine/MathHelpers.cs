﻿using System.Runtime.InteropServices;

namespace Sapling.Engine
{
    public static unsafe class MemoryHelpers
    {
        public static T* Allocate<T>(int count) where T : unmanaged
        {
            const nuint alignment = 64;
            var size = (sizeof(T) * count);
            var block = NativeMemory.AlignedAlloc((nuint)size, alignment);
            NativeMemory.Clear(block, (nuint)size);

            return (T*)block;
        }
    }
    public static class MathHelpers
    {
        public static float[] LogLookup = new float[230];
        static MathHelpers()
        {
            for (var i = 0; i < LogLookup.Length; i++)
            {
                LogLookup[i] = (float)Math.Log(i);
            }
        }
    }
}
