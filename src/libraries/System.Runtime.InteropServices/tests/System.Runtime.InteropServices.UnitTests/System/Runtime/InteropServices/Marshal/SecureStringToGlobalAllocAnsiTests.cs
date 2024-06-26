// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Security;
using Xunit;

namespace System.Runtime.InteropServices.Tests
{
    public class SecureStringToGlobalAllocAnsiTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("pizza")]
        [InlineData("pepperoni")]
        [InlineData("password")]
        [InlineData("P4ssw0rdAa1")]
        [InlineData("\u1234")]
        [InlineData("\uD800")]
        [InlineData("\uD800\uDC00")]
        [InlineData("\0")]
        [InlineData("abc\0def")]
        public void SecureStringToGlobalAllocAnsi_InvokePtrToStringAnsi_Roundtrips(string s)
        {
            string expectedFullString = new string(s.ToCharArray().Select(c => c > 0xFF ? '?' : c).ToArray());
            int nullIndex = expectedFullString.IndexOf('\0');
            string expectedParameterlessString = nullIndex == -1 ? expectedFullString : expectedFullString.Substring(0, nullIndex);

            using (SecureString secureString = ToSecureString(s))
            {
                IntPtr ptr = Marshal.SecureStringToGlobalAllocAnsi(secureString);
                try
                {
                    Assert.NotEqual(IntPtr.Zero, ptr);

                    // The check is incorrect for UTF8 encoding of non-Ansi chars. Detect UTF8 encoding via SystemMaxDBCSCharSize.
                    bool containsNonAnsiChars = s.Any(c => c > 0xFF);
                    if (!containsNonAnsiChars || Marshal.SystemMaxDBCSCharSize < 3)
                    {
                        // Make sure the native memory is correctly laid out.
                        for (int i = 0; i < s.Length; i++)
                        {
                            Assert.Equal(expectedFullString[i], (char)Marshal.ReadByte(IntPtr.Add(ptr, i)));
                        }

                        // Make sure the native memory roundtrips.
                        Assert.Equal(expectedParameterlessString, Marshal.PtrToStringAnsi(ptr));
                        Assert.Equal(expectedFullString, Marshal.PtrToStringAnsi(ptr, s.Length));
                    }
                }
                finally
                {
                    Marshal.ZeroFreeGlobalAllocAnsi(ptr);
                }
            }
        }

        [Fact]
        public void SecureStringToGlobalAllocAnsi_NullString_ThrowsArgumentNullException()
        {
            AssertExtensions.Throws<ArgumentNullException>("s", () => Marshal.SecureStringToGlobalAllocAnsi(null));
        }

        [Fact]
        public void SecureStringToGlobalAllocAnsi_DisposedString_ThrowsObjectDisposedException()
        {
            var secureString = new SecureString();
            secureString.Dispose();

            Assert.Throws<ObjectDisposedException>(() => Marshal.SecureStringToGlobalAllocAnsi(secureString));
        }

        private static SecureString ToSecureString(string data)
        {
            var str = new SecureString();
            foreach (char c in data)
            {
                str.AppendChar(c);
            }
            str.MakeReadOnly();
            return str;
        }
    }
}
