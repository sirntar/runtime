// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Generated by Fuzzlyn v3.2 on 2025-07-07 15:44:07
// Run on X64 Windows
// Seed: 13630635548352241444-vectort,vector128,vector256,x86aes,x86avx,x86avx2,x86avx512bw,x86avx512bwvl,x86avx512cd,x86avx512cdvl,x86avx512dq,x86avx512dqvl,x86avx512f,x86avx512fvl,x86avx512fx64,x86bmi1,x86bmi1x64,x86bmi2,x86bmi2x64,x86fma,x86lzcnt,x86lzcntx64,x86pclmulqdq,x86popcnt,x86popcntx64,x86sse,x86ssex64,x86sse2,x86sse2x64,x86sse3,x86sse41,x86sse41x64,x86sse42,x86sse42x64,x86ssse3,x86x86base
// Reduced from 23.1 KiB to 0.7 KiB in 00:01:26
// Hits JIT assert for Release:
// Assertion failed 'unreached' in 'S1:M4():byte:this' during 'Morph - Global' (IL size 58; hash 0x43f4d8e9; FullOpts)
//
//     File: D:\a\_work\1\s\src\coreclr\jit\simd.h Line: 1142
//

using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Xunit;

public class Runtime_117377
{
    static Vector256<uint> s_1;

    [Fact]
    public static void TestEntryPoint()
    {
        (new S1()).M4();
    }

    struct S1
    {
        public byte F4;

        public byte M4()
        {
            if (Avx512F.VL.IsSupported)
            {
                var vr3 = Vector256.CreateScalar(0U);
                var vr5 = Vector256.Create<uint>(0);
                var vr6 = Vector256.CreateScalar(85122339U);
                var vr4 = Avx512F.VL.RotateLeftVariable(vr5, vr6);
                var vr1 = Avx512F.VL.CompareGreaterThanOrEqual(vr3, vr4);
                s_1 = Avx2.AndNot(vr1, s_1);
            }
            return F4;
        }
    }
}
