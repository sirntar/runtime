// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SerializationTypes;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Xunit;
using System.Runtime.Serialization.Tests;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

public static partial class DataContractSerializerTests
{
#if ReflectionOnly
    private static readonly string SerializationOptionSetterName = "set_Option";

    static DataContractSerializerTests()
    {
        MethodInfo method = typeof(DataContractSerializer).GetMethod(SerializationOptionSetterName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.True(method != null, $"No method named {SerializationOptionSetterName}");
        method.Invoke(null, new object[] { 1 });
    }
#endif
    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/60462", TestPlatforms.iOS | TestPlatforms.tvOS)]
    public static void DCS_DateTimeOffsetAsRoot()
    {
        // Assume that UTC offset doesn't change more often than once in the day 2013-01-02
        // DO NOT USE TimeZoneInfo.Local.BaseUtcOffset !
        var offsetMinutes = (int)TimeZoneInfo.Local.GetUtcOffset(new DateTime(2013, 1, 2)).TotalMinutes;
        var objs = new DateTimeOffset[]
        {
            // Adding offsetMinutes so the DateTime component in serialized strings are time-zone independent
            new DateTimeOffset(new DateTime(2013, 1, 2, 3, 4, 5, 6).AddMinutes(offsetMinutes)),
            new DateTimeOffset(new DateTime(2013, 1, 2, 3, 4, 5, 6, DateTimeKind.Local).AddMinutes(offsetMinutes)),
            new DateTimeOffset(new DateTime(2013, 1, 2, 3, 4, 5, 6, DateTimeKind.Unspecified).AddMinutes(offsetMinutes)),

            new DateTimeOffset(new DateTime(2013, 1, 2, 3, 4, 5, 6, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc))
        };
        var serializedStrings = new string[]
        {
            string.Format(@"<DateTimeOffset xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://schemas.datacontract.org/2004/07/System""><DateTime>2013-01-02T03:04:05.006Z</DateTime><OffsetMinutes>{0}</OffsetMinutes></DateTimeOffset>", offsetMinutes),
            string.Format(@"<DateTimeOffset xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://schemas.datacontract.org/2004/07/System""><DateTime>2013-01-02T03:04:05.006Z</DateTime><OffsetMinutes>{0}</OffsetMinutes></DateTimeOffset>", offsetMinutes),
            string.Format(@"<DateTimeOffset xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://schemas.datacontract.org/2004/07/System""><DateTime>2013-01-02T03:04:05.006Z</DateTime><OffsetMinutes>{0}</OffsetMinutes></DateTimeOffset>", offsetMinutes),
            @"<DateTimeOffset xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://schemas.datacontract.org/2004/07/System""><DateTime>2013-01-02T03:04:05.006Z</DateTime><OffsetMinutes>0</OffsetMinutes></DateTimeOffset>",
            @"<DateTimeOffset xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://schemas.datacontract.org/2004/07/System""><DateTime>0001-01-01T00:00:00Z</DateTime><OffsetMinutes>0</OffsetMinutes></DateTimeOffset>",
            @"<DateTimeOffset xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://schemas.datacontract.org/2004/07/System""><DateTime>9999-12-31T23:59:59.9999999Z</DateTime><OffsetMinutes>0</OffsetMinutes></DateTimeOffset>"
        };
        for (int i = 0; i < objs.Length; ++i)
        {
            Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<DateTimeOffset>(objs[i], serializedStrings[i]), objs[i]);
        }
    }

    [Fact]
    public static void DCS_BoolAsRoot()
    {
        Assert.True(DataContractSerializerHelper.SerializeAndDeserialize<bool>(true, @"<boolean xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">true</boolean>"));
        Assert.False(DataContractSerializerHelper.SerializeAndDeserialize<bool>(false, @"<boolean xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">false</boolean>"));
    }

    [Fact]
    public static void DCS_ByteArrayAsRoot()
    {
        Assert.Null(DataContractSerializerHelper.SerializeAndDeserialize<byte[]>(null, @"<base64Binary i:nil=""true"" xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""/>"));
        byte[] x = new byte[] { 1, 2 };
        byte[] y = DataContractSerializerHelper.SerializeAndDeserialize<byte[]>(x, @"<base64Binary xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">AQI=</base64Binary>");
        Assert.Equal<byte>(x, y);
    }

    [Fact]
    public static void DCS_CharAsRoot()
    {
        Assert.StrictEqual(char.MinValue, DataContractSerializerHelper.SerializeAndDeserialize<char>(char.MinValue, @"<char xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">0</char>"));
        Assert.StrictEqual(char.MaxValue, DataContractSerializerHelper.SerializeAndDeserialize<char>(char.MaxValue, @"<char xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">65535</char>"));
        Assert.StrictEqual('a', DataContractSerializerHelper.SerializeAndDeserialize<char>('a', @"<char xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">97</char>"));
        Assert.StrictEqual('\u00F1', DataContractSerializerHelper.SerializeAndDeserialize<char>('\u00F1', @"<char xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">241</char>"));
        Assert.StrictEqual('\u6F22', DataContractSerializerHelper.SerializeAndDeserialize<char>('\u6F22', @"<char xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">28450</char>"));
    }

    [Fact]
    public static void DCS_ByteAsRoot()
    {
        Assert.StrictEqual(10, DataContractSerializerHelper.SerializeAndDeserialize<byte>(10, @"<unsignedByte xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">10</unsignedByte>"));
        Assert.StrictEqual(byte.MinValue, DataContractSerializerHelper.SerializeAndDeserialize<byte>(byte.MinValue, @"<unsignedByte xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">0</unsignedByte>"));
        Assert.StrictEqual(byte.MaxValue, DataContractSerializerHelper.SerializeAndDeserialize<byte>(byte.MaxValue, @"<unsignedByte xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">255</unsignedByte>"));
    }

    [Fact]
    public static void DCS_DateTimeAsRoot()
    {
        var offsetMinutes = (int)TimeZoneInfo.Local.GetUtcOffset(new DateTime(2013, 1, 2)).TotalMinutes;
        Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<DateTime>(new DateTime(2013, 1, 2), @"<dateTime xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">2013-01-02T00:00:00</dateTime>"), new DateTime(2013, 1, 2));
        Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<DateTime>(new DateTime(2013, 1, 2, 3, 4, 5, 6, DateTimeKind.Local), string.Format(@"<dateTime xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">2013-01-02T03:04:05.006{0:+;-}{1}</dateTime>", offsetMinutes, new TimeSpan(0, offsetMinutes, 0).ToString(@"hh\:mm"))), new DateTime(2013, 1, 2, 3, 4, 5, 6, DateTimeKind.Local));
        Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<DateTime>(new DateTime(2013, 1, 2, 3, 4, 5, 6, DateTimeKind.Unspecified), @"<dateTime xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">2013-01-02T03:04:05.006</dateTime>"), new DateTime(2013, 1, 2, 3, 4, 5, 6, DateTimeKind.Unspecified));
        Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<DateTime>(new DateTime(2013, 1, 2, 3, 4, 5, 6, DateTimeKind.Utc), @"<dateTime xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">2013-01-02T03:04:05.006Z</dateTime>"), new DateTime(2013, 1, 2, 3, 4, 5, 6, DateTimeKind.Utc));
        Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<DateTime>(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), @"<dateTime xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">0001-01-01T00:00:00Z</dateTime>"), DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc));
        Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<DateTime>(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), @"<dateTime xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">9999-12-31T23:59:59.9999999Z</dateTime>"), DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc));
    }

    [Fact]
    public static void DCS_BinarySerializationOfDateTime()
    {
        DateTime dateTime = DateTime.Parse("2021-01-01");
        MemoryStream ms = new();
        DataContractSerializer dcs = new(dateTime.GetType());
        using (XmlDictionaryWriter writer = XmlDictionaryWriter.CreateBinaryWriter(ms, null, null, ownsStream: true))
            dcs.WriteObject(writer, dateTime);
        var serializedBytes = ms.ToArray();
        Assert.Equal(72, serializedBytes.Length);
    }

    [Fact]
    public static void DCS_DecimalAsRoot()
    {
        foreach (decimal value in new decimal[] { (decimal)-1.2, (decimal)0, (decimal)2.3, decimal.MinValue, decimal.MaxValue })
        {
            Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<decimal>(value, string.Format(@"<decimal xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">{0}</decimal>", value.ToString(CultureInfo.InvariantCulture))), value);
        }
    }

    [Fact]
    public static void DCS_DoubleAsRoot()
    {
        Assert.StrictEqual(-1.2, DataContractSerializerHelper.SerializeAndDeserialize<double>(-1.2, @"<double xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">-1.2</double>"));
        Assert.StrictEqual(0, DataContractSerializerHelper.SerializeAndDeserialize<double>(0, @"<double xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">0</double>"));
        Assert.StrictEqual(2.3, DataContractSerializerHelper.SerializeAndDeserialize<double>(2.3, @"<double xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">2.3</double>"));
        Assert.StrictEqual(double.MinValue, DataContractSerializerHelper.SerializeAndDeserialize<double>(double.MinValue, @"<double xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">-1.7976931348623157E+308</double>"));
        Assert.StrictEqual(double.MaxValue, DataContractSerializerHelper.SerializeAndDeserialize<double>(double.MaxValue, @"<double xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">1.7976931348623157E+308</double>"));
    }

    [Fact]
    public static void DCS_FloatAsRoot()
    {
        Assert.StrictEqual((float)-1.2, DataContractSerializerHelper.SerializeAndDeserialize<float>((float)-1.2, @"<float xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">-1.2</float>"));
        Assert.StrictEqual((float)0, DataContractSerializerHelper.SerializeAndDeserialize<float>((float)0, @"<float xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">0</float>"));
        Assert.StrictEqual((float)2.3, DataContractSerializerHelper.SerializeAndDeserialize<float>((float)2.3, @"<float xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">2.3</float>"));
    }

    [Fact]
    public static void DCS_FloatAsRoot_NotNetFramework()
    {
        Assert.StrictEqual(float.MinValue, DataContractSerializerHelper.SerializeAndDeserialize<float>(float.MinValue, @"<float xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">-3.4028235E+38</float>"));
        Assert.StrictEqual(float.MaxValue, DataContractSerializerHelper.SerializeAndDeserialize<float>(float.MaxValue, @"<float xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">3.4028235E+38</float>"));
    }

    [Fact]
    public static void DCS_GuidAsRoot()
    {
        foreach (Guid value in new Guid[] { Guid.NewGuid(), Guid.Empty })
        {
            Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<Guid>(value, string.Format(@"<guid xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">{0}</guid>", value.ToString())), value);
        }
    }

    [Fact]
    public static void DCS_IntAsRoot()
    {
        foreach (int value in new int[] { -1, 0, 2, int.MinValue, int.MaxValue })
        {
            Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<int>(value, string.Format(@"<int xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">{0}</int>", value)), value);
        }
    }

    [Fact]
    public static void DCS_LongAsRoot()
    {
        foreach (long value in new long[] { (long)-1, (long)0, (long)2, long.MinValue, long.MaxValue })
        {
            Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<long>(value, string.Format(@"<long xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">{0}</long>", value)), value);
        }
    }

    [Fact]
    public static void DCS_ObjectAsRoot()
    {
        Assert.StrictEqual(1, DataContractSerializerHelper.SerializeAndDeserialize<object>(1, @"<z:anyType i:type=""a:int"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://www.w3.org/2001/XMLSchema"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"">1</z:anyType>"));
        Assert.StrictEqual(true, DataContractSerializerHelper.SerializeAndDeserialize<object>(true, @"<z:anyType i:type=""a:boolean"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://www.w3.org/2001/XMLSchema"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"">true</z:anyType>"));
        Assert.StrictEqual("abc", DataContractSerializerHelper.SerializeAndDeserialize<object>("abc", @"<z:anyType i:type=""a:string"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://www.w3.org/2001/XMLSchema"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"">abc</z:anyType>"));
        Assert.Null(DataContractSerializerHelper.SerializeAndDeserialize<object>(null, @"<z:anyType i:nil=""true"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""/>"));
    }

    [Fact]
    public static void DCS_XmlQualifiedNameAsRoot()
    {
        Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<XmlQualifiedName>(new XmlQualifiedName("abc", "def"), @"<z:QName xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""def"">a:abc</z:QName>"), new XmlQualifiedName("abc", "def"));
        Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<XmlQualifiedName>(XmlQualifiedName.Empty, @"<z:QName xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/>"), XmlQualifiedName.Empty);
    }

    [Fact]
    public static void DCS_ShortAsRoot()
    {
        foreach (short value in new short[] { (short)-1.2, (short)0, (short)2.3, short.MinValue, short.MaxValue })
        {
            Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<short>(value, string.Format(@"<short xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">{0}</short>", value)), value);
        }
    }

    [Fact]
    public static void DCS_SbyteAsRoot()
    {
        foreach (sbyte value in new sbyte[] { (sbyte)3, (sbyte)0, sbyte.MinValue, sbyte.MaxValue })
        {
            Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<sbyte>(value, string.Format(@"<byte xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">{0}</byte>", value)), value);
        }
    }

    [Fact]
    public static void DCS_StringAsRoot()
    {
        Assert.Equal("abc", DataContractSerializerHelper.SerializeAndDeserialize<string>("abc", @"<string xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">abc</string>"));
        Assert.Equal("  a b  ", DataContractSerializerHelper.SerializeAndDeserialize<string>("  a b  ", @"<string xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">  a b  </string>"));
        Assert.Null(DataContractSerializerHelper.SerializeAndDeserialize<string>(null, @"<string i:nil=""true"" xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""/>"));
        Assert.Equal("", DataContractSerializerHelper.SerializeAndDeserialize<string>("", @"<string xmlns=""http://schemas.microsoft.com/2003/10/Serialization/""/>"));
        Assert.Equal(" ", DataContractSerializerHelper.SerializeAndDeserialize<string>(" ", @"<string xmlns=""http://schemas.microsoft.com/2003/10/Serialization/""> </string>"));
        Assert.Equal("Hello World! \u6F22 \u00F1", DataContractSerializerHelper.SerializeAndDeserialize<string>("Hello World! \u6F22 \u00F1", "<string xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/\">Hello World! \u6F22 \u00F1</string>"));
    }

    [Fact]
    public static void DCS_TimeSpanAsRoot()
    {
        Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<TimeSpan>(new TimeSpan(1, 2, 3), @"<duration xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">PT1H2M3S</duration>"), new TimeSpan(1, 2, 3));
        Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<TimeSpan>(TimeSpan.Zero, @"<duration xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">PT0S</duration>"), TimeSpan.Zero);
        Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<TimeSpan>(TimeSpan.MinValue, @"<duration xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">-P10675199DT2H48M5.4775808S</duration>"), TimeSpan.MinValue);
        Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<TimeSpan>(TimeSpan.MaxValue, @"<duration xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">P10675199DT2H48M5.4775807S</duration>"), TimeSpan.MaxValue);
    }

    [Fact]
    public static void DCS_UintAsRoot()
    {
        foreach (uint value in new uint[] { (uint)3, (uint)0, uint.MinValue, uint.MaxValue })
        {
            Assert.StrictEqual<uint>(DataContractSerializerHelper.SerializeAndDeserialize<uint>(value, string.Format(@"<unsignedInt xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">{0}</unsignedInt>", value)), value);
        }
    }

    [Fact]
    public static void DCS_UlongAsRoot()
    {
        foreach (ulong value in new ulong[] { (ulong)3, (ulong)0, ulong.MinValue, ulong.MaxValue })
        {
            Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<ulong>(value, string.Format(@"<unsignedLong xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">{0}</unsignedLong>", value)), value);
        }
    }

    [Fact]
    public static void DCS_UshortAsRoot()
    {
        foreach (ushort value in new ushort[] { (ushort)3, (ushort)0, ushort.MinValue, ushort.MaxValue })
        {
            Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<ushort>(value, string.Format(@"<unsignedShort xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">{0}</unsignedShort>", value)), value);
        }
    }

    [Fact]
    public static void DCS_UriAsRoot()
    {
        Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<Uri>(new Uri("http://abc/"), @"<anyURI xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">http://abc/</anyURI>"), new Uri("http://abc/"));
        Assert.StrictEqual(DataContractSerializerHelper.SerializeAndDeserialize<Uri>(new Uri("http://abc/def/x.aspx?p1=12&p2=34"), @"<anyURI xmlns=""http://schemas.microsoft.com/2003/10/Serialization/"">http://abc/def/x.aspx?p1=12&amp;p2=34</anyURI>"), new Uri("http://abc/def/x.aspx?p1=12&p2=34"));
    }

    [Fact]
    public static void DCS_ArrayAsRoot()
    {
        SimpleType[] x = new SimpleType[] { new SimpleType { P1 = "abc", P2 = 11 }, new SimpleType { P1 = "def", P2 = 12 } };
        SimpleType[] y = DataContractSerializerHelper.SerializeAndDeserialize<SimpleType[]>(x, @"<ArrayOfSimpleType xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><SimpleType><P1>abc</P1><P2>11</P2></SimpleType><SimpleType><P1>def</P1><P2>12</P2></SimpleType></ArrayOfSimpleType>");

        Utils.Equal<SimpleType>(x, y, (a, b) => { return SimpleType.AreEqual(a, b); });
    }

    [Fact]
    public static void DCS_ArrayAsGetSet()
    {
        TypeWithGetSetArrayMembers x = new TypeWithGetSetArrayMembers
        {
            F1 = new SimpleType[] { new SimpleType { P1 = "ab", P2 = 1 }, new SimpleType { P1 = "cd", P2 = 2 } },
            F2 = new int[] { -1, 3 },
            P1 = new SimpleType[] { new SimpleType { P1 = "ef", P2 = 5 }, new SimpleType { P1 = "gh", P2 = 7 } },
            P2 = new int[] { 11, 12 }
        };
        TypeWithGetSetArrayMembers y = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithGetSetArrayMembers>(x, @"<TypeWithGetSetArrayMembers xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><F1><SimpleType><P1>ab</P1><P2>1</P2></SimpleType><SimpleType><P1>cd</P1><P2>2</P2></SimpleType></F1><F2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:int>-1</a:int><a:int>3</a:int></F2><P1><SimpleType><P1>ef</P1><P2>5</P2></SimpleType><SimpleType><P1>gh</P1><P2>7</P2></SimpleType></P1><P2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:int>11</a:int><a:int>12</a:int></P2></TypeWithGetSetArrayMembers>");

        Assert.NotNull(y);
        Utils.Equal<SimpleType>(x.F1, y.F1, (a, b) => { return SimpleType.AreEqual(a, b); });
        Assert.Equal<int>(x.F2, y.F2);
        Utils.Equal<SimpleType>(x.P1, y.P1, (a, b) => { return SimpleType.AreEqual(a, b); });
        Assert.Equal<int>(x.P2, y.P2);
    }

    [Fact]
    public static void DCS_ArrayAsGetOnly()
    {
        TypeWithGetOnlyArrayProperties x = new TypeWithGetOnlyArrayProperties();
        x.P1[0] = new SimpleType { P1 = "ab", P2 = 1 };
        x.P1[1] = new SimpleType { P1 = "cd", P2 = 2 };
        x.P2[0] = -1;
        x.P2[1] = 3;

        TypeWithGetOnlyArrayProperties y = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithGetOnlyArrayProperties>(x, @"<TypeWithGetOnlyArrayProperties xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><P1><SimpleType><P1>ab</P1><P2>1</P2></SimpleType><SimpleType><P1>cd</P1><P2>2</P2></SimpleType></P1><P2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:int>-1</a:int><a:int>3</a:int></P2></TypeWithGetOnlyArrayProperties>");

        Assert.NotNull(y);
        Utils.Equal<SimpleType>(x.P1, y.P1, (a, b) => { return SimpleType.AreEqual(a, b); });
        Assert.Equal<int>(x.P2, y.P2);
    }

    [Fact]
    public static void DCS_DictionaryGenericRoot()
    {
        Dictionary<string, int> x = new Dictionary<string, int>();
        x.Add("one", 1);
        x.Add("two", 2);

        Dictionary<string, int> y = DataContractSerializerHelper.SerializeAndDeserialize<Dictionary<string, int>>(x, @"<ArrayOfKeyValueOfstringint xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><KeyValueOfstringint><Key>one</Key><Value>1</Value></KeyValueOfstringint><KeyValueOfstringint><Key>two</Key><Value>2</Value></KeyValueOfstringint></ArrayOfKeyValueOfstringint>");

        Assert.NotNull(y);
        Assert.True(y.Count == 2);
        Assert.True(y["one"] == 1);
        Assert.True(y["two"] == 2);
    }

    [Fact]
    public static void DCS_DictionaryGenericMembers()
    {
        TypeWithDictionaryGenericMembers x = new TypeWithDictionaryGenericMembers
        {
            F1 = new Dictionary<string, int>(),
            F2 = new Dictionary<string, int>(),
            P1 = new Dictionary<string, int>(),
            P2 = new Dictionary<string, int>()
        };
        x.F1.Add("ab", 12);
        x.F1.Add("cd", 15);
        x.F2.Add("ef", 17);
        x.F2.Add("gh", 19);
        x.P1.Add("12", 120);
        x.P1.Add("13", 130);
        x.P2.Add("14", 140);
        x.P2.Add("15", 150);

        x.RO1.Add(true, 't');
        x.RO1.Add(false, 'f');

        x.RO2.Add(true, 'a');
        x.RO2.Add(false, 'b');

        TypeWithDictionaryGenericMembers y = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithDictionaryGenericMembers>(x, @"<TypeWithDictionaryGenericMembers xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><F1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfstringint><a:Key>ab</a:Key><a:Value>12</a:Value></a:KeyValueOfstringint><a:KeyValueOfstringint><a:Key>cd</a:Key><a:Value>15</a:Value></a:KeyValueOfstringint></F1><F2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfstringint><a:Key>ef</a:Key><a:Value>17</a:Value></a:KeyValueOfstringint><a:KeyValueOfstringint><a:Key>gh</a:Key><a:Value>19</a:Value></a:KeyValueOfstringint></F2><P1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfstringint><a:Key>12</a:Key><a:Value>120</a:Value></a:KeyValueOfstringint><a:KeyValueOfstringint><a:Key>13</a:Key><a:Value>130</a:Value></a:KeyValueOfstringint></P1><P2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfstringint><a:Key>14</a:Key><a:Value>140</a:Value></a:KeyValueOfstringint><a:KeyValueOfstringint><a:Key>15</a:Key><a:Value>150</a:Value></a:KeyValueOfstringint></P2><RO1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfbooleanchar><a:Key>true</a:Key><a:Value>116</a:Value></a:KeyValueOfbooleanchar><a:KeyValueOfbooleanchar><a:Key>false</a:Key><a:Value>102</a:Value></a:KeyValueOfbooleanchar></RO1><RO2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfbooleanchar><a:Key>true</a:Key><a:Value>97</a:Value></a:KeyValueOfbooleanchar><a:KeyValueOfbooleanchar><a:Key>false</a:Key><a:Value>98</a:Value></a:KeyValueOfbooleanchar></RO2></TypeWithDictionaryGenericMembers>");
        Assert.NotNull(y);

        Assert.NotNull(y.F1);
        Assert.True(y.F1.Count == 2);
        Assert.True(y.F1["ab"] == 12);
        Assert.True(y.F1["cd"] == 15);

        Assert.NotNull(y.F2);
        Assert.True(y.F2.Count == 2);
        Assert.True(y.F2["ef"] == 17);
        Assert.True(y.F2["gh"] == 19);

        Assert.NotNull(y.P1);
        Assert.True(y.P1.Count == 2);
        Assert.True(y.P1["12"] == 120);
        Assert.True(y.P1["13"] == 130);

        Assert.NotNull(y.P2);
        Assert.True(y.P2.Count == 2);
        Assert.True(y.P2["14"] == 140);
        Assert.True(y.P2["15"] == 150);

        Assert.NotNull(y.RO1);
        Assert.True(y.RO1.Count == 2);
        Assert.True(y.RO1[true] == 't');
        Assert.True(y.RO1[false] == 'f');

        Assert.NotNull(y.RO2);
        Assert.True(y.RO2.Count == 2);
        Assert.True(y.RO2[true] == 'a');
        Assert.True(y.RO2[false] == 'b');
    }

    [Fact]
    public static void DCS_DictionaryRoot()
    {
        MyDictionary x = new MyDictionary();
        x.Add(1, "one");
        x.Add(2, "two");

        MyDictionary y = DataContractSerializerHelper.SerializeAndDeserialize<MyDictionary>(x, @"<ArrayOfKeyValueOfanyTypeanyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><KeyValueOfanyTypeanyType><Key i:type=""a:int"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">1</Key><Value i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">one</Value></KeyValueOfanyTypeanyType><KeyValueOfanyTypeanyType><Key i:type=""a:int"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">2</Key><Value i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">two</Value></KeyValueOfanyTypeanyType></ArrayOfKeyValueOfanyTypeanyType>");

        Assert.NotNull(y);
        Assert.True(y.Count == 2);
        Assert.True((string)y[1] == "one");
        Assert.True((string)y[2] == "two");
    }

    [Fact]
    public static void DCS_DictionaryMembers()
    {
        TypeWithDictionaryMembers x = new TypeWithDictionaryMembers();

        x.F1 = new MyDictionary();
        x.F1.Add("ab", 12);
        x.F1.Add("cd", 15);

        x.F2 = new MyDictionary();
        x.F2.Add("ef", 17);
        x.F2.Add("gh", 19);

        x.P1 = new MyDictionary();
        x.P1.Add("12", 120);
        x.P1.Add("13", 130);

        x.P2 = new MyDictionary();
        x.P2.Add("14", 140);
        x.P2.Add("15", 150);

        x.RO1.Add(true, 't');
        x.RO1.Add(false, 'f');

        x.RO2.Add(true, 'a');
        x.RO2.Add(false, 'b');

        TypeWithDictionaryMembers y = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithDictionaryMembers>(x, @"<TypeWithDictionaryMembers xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><F1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfanyTypeanyType><a:Key i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">ab</a:Key><a:Value i:type=""b:int"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">12</a:Value></a:KeyValueOfanyTypeanyType><a:KeyValueOfanyTypeanyType><a:Key i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">cd</a:Key><a:Value i:type=""b:int"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">15</a:Value></a:KeyValueOfanyTypeanyType></F1><F2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfanyTypeanyType><a:Key i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">ef</a:Key><a:Value i:type=""b:int"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">17</a:Value></a:KeyValueOfanyTypeanyType><a:KeyValueOfanyTypeanyType><a:Key i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">gh</a:Key><a:Value i:type=""b:int"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">19</a:Value></a:KeyValueOfanyTypeanyType></F2><P1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfanyTypeanyType><a:Key i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">12</a:Key><a:Value i:type=""b:int"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">120</a:Value></a:KeyValueOfanyTypeanyType><a:KeyValueOfanyTypeanyType><a:Key i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">13</a:Key><a:Value i:type=""b:int"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">130</a:Value></a:KeyValueOfanyTypeanyType></P1><P2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfanyTypeanyType><a:Key i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">14</a:Key><a:Value i:type=""b:int"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">140</a:Value></a:KeyValueOfanyTypeanyType><a:KeyValueOfanyTypeanyType><a:Key i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">15</a:Key><a:Value i:type=""b:int"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">150</a:Value></a:KeyValueOfanyTypeanyType></P2><RO1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfanyTypeanyType><a:Key i:type=""b:boolean"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">true</a:Key><a:Value i:type=""b:char"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">116</a:Value></a:KeyValueOfanyTypeanyType><a:KeyValueOfanyTypeanyType><a:Key i:type=""b:boolean"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">false</a:Key><a:Value i:type=""b:char"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">102</a:Value></a:KeyValueOfanyTypeanyType></RO1><RO2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfanyTypeanyType><a:Key i:type=""b:boolean"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">true</a:Key><a:Value i:type=""b:char"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">97</a:Value></a:KeyValueOfanyTypeanyType><a:KeyValueOfanyTypeanyType><a:Key i:type=""b:boolean"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">false</a:Key><a:Value i:type=""b:char"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">98</a:Value></a:KeyValueOfanyTypeanyType></RO2></TypeWithDictionaryMembers>");
        Assert.NotNull(y);

        Assert.NotNull(y.F1);
        Assert.True(y.F1.Count == 2);
        Assert.True((int)y.F1["ab"] == 12);
        Assert.True((int)y.F1["cd"] == 15);

        Assert.NotNull(y.F2);
        Assert.True(y.F2.Count == 2);
        Assert.True((int)y.F2["ef"] == 17);
        Assert.True((int)y.F2["gh"] == 19);

        Assert.NotNull(y.P1);
        Assert.True(y.P1.Count == 2);
        Assert.True((int)y.P1["12"] == 120);
        Assert.True((int)y.P1["13"] == 130);

        Assert.NotNull(y.P2);
        Assert.True(y.P2.Count == 2);
        Assert.True((int)y.P2["14"] == 140);
        Assert.True((int)y.P2["15"] == 150);

        Assert.NotNull(y.RO1);
        Assert.True(y.RO1.Count == 2);
        Assert.True((char)y.RO1[true] == 't');
        Assert.True((char)y.RO1[false] == 'f');

        Assert.NotNull(y.RO2);
        Assert.True(y.RO2.Count == 2);
        Assert.True((char)y.RO2[true] == 'a');
        Assert.True((char)y.RO2[false] == 'b');
    }

    [Fact]
    public static void DCS_TypeWithIDictionaryPropertyInitWithConcreteType()
    {
        // Test for Bug 876869 : [Serialization] Concrete type not inferred for DCS
        var dict = new TypeWithIDictionaryPropertyInitWithConcreteType();
        dict.DictionaryProperty.Add("key1", "value1");
        dict.DictionaryProperty.Add("key2", "value2");

        var dict2 = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithIDictionaryPropertyInitWithConcreteType>(dict, @"<TypeWithIDictionaryPropertyInitWithConcreteType xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><DictionaryProperty xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfstringstring><a:Key>key1</a:Key><a:Value>value1</a:Value></a:KeyValueOfstringstring><a:KeyValueOfstringstring><a:Key>key2</a:Key><a:Value>value2</a:Value></a:KeyValueOfstringstring></DictionaryProperty></TypeWithIDictionaryPropertyInitWithConcreteType>");

        Assert.True(dict2 != null && dict2.DictionaryProperty != null);
        Assert.True(dict.DictionaryProperty.Count == dict2.DictionaryProperty.Count);
        foreach (var entry in dict.DictionaryProperty)
        {
            Assert.True(dict2.DictionaryProperty.ContainsKey(entry.Key) && dict2.DictionaryProperty[entry.Key].Equals(dict.DictionaryProperty[entry.Key]));
        }
    }

    [Fact]
    public static void DCS_ListGenericRoot()
    {
        List<string> x = new List<string>();
        x.Add("zero");
        x.Add("one");

        List<string> y = DataContractSerializerHelper.SerializeAndDeserialize<List<string>>(x, @"<ArrayOfstring xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><string>zero</string><string>one</string></ArrayOfstring>");

        Assert.NotNull(y);
        Assert.True(y.Count == 2);
        Assert.True(y[0] == "zero");
        Assert.True(y[1] == "one");
    }

    [Fact]
    public static void DCS_ListGenericMembers()
    {
        TypeWithListGenericMembers x = new TypeWithListGenericMembers();

        x.F1 = new List<string>();
        x.F1.Add("zero");
        x.F1.Add("one");

        x.F2 = new List<string>();
        x.F2.Add("abc");
        x.F2.Add("def");

        x.P1 = new List<int>();
        x.P1.Add(10);
        x.P1.Add(20);

        x.P2 = new List<int>();
        x.P2.Add(12);
        x.P2.Add(34);

        x.RO1.Add('a');
        x.RO1.Add('b');

        x.RO2.Add('c');
        x.RO2.Add('d');

        TypeWithListGenericMembers y = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithListGenericMembers>(x, @"<TypeWithListGenericMembers xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><F1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>zero</a:string><a:string>one</a:string></F1><F2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>abc</a:string><a:string>def</a:string></F2><P1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:int>10</a:int><a:int>20</a:int></P1><P2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:int>12</a:int><a:int>34</a:int></P2><RO1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:char>97</a:char><a:char>98</a:char></RO1><RO2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:char>99</a:char><a:char>100</a:char></RO2></TypeWithListGenericMembers>");
        Assert.NotNull(y);

        Assert.NotNull(y.F1);
        Assert.True(y.F1.Count == 2);
        Assert.True(y.F1[0] == "zero");
        Assert.True(y.F1[1] == "one");

        Assert.NotNull(y.F2);
        Assert.True(y.F2.Count == 2);
        Assert.True(y.F2[0] == "abc");
        Assert.True(y.F2[1] == "def");

        Assert.NotNull(y.P1);
        Assert.True(y.P1.Count == 2);
        Assert.True(y.P1[0] == 10);
        Assert.True(y.P1[1] == 20);

        Assert.NotNull(y.P2);
        Assert.True(y.P2.Count == 2);
        Assert.True(y.P2[0] == 12);
        Assert.True(y.P2[1] == 34);

        Assert.NotNull(y.RO1);
        Assert.True(y.RO1.Count == 2);
        Assert.True(y.RO1[0] == 'a');
        Assert.True(y.RO1[1] == 'b');

        Assert.NotNull(y.RO2);
        Assert.True(y.RO2.Count == 2);
        Assert.True(y.RO2[0] == 'c');
        Assert.True(y.RO2[1] == 'd');
    }

    [Fact]
    public static void DCS_CollectionGenericRoot()
    {
        MyCollection<string> x = new MyCollection<string>("a1", "a2");
        MyCollection<string> y = DataContractSerializerHelper.SerializeAndDeserialize<MyCollection<string>>(x, @"<ArrayOfstring xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><string>a1</string><string>a2</string></ArrayOfstring>");

        Assert.NotNull(y);
        Assert.True(y.Count == 2);
        foreach (var item in x)
        {
            Assert.Contains(item, y);
        }
    }

    [Fact]
    public static void DCS_CollectionGenericMembers()
    {
        TypeWithCollectionGenericMembers x = new TypeWithCollectionGenericMembers
        {
            F1 = new MyCollection<string>("a1", "a2"),
            F2 = new MyCollection<string>("b1", "b2"),
            P1 = new MyCollection<string>("c1", "c2"),
            P2 = new MyCollection<string>("d1", "d2"),
        };
        x.RO1.Add("abc");
        x.RO2.Add("xyz");

        TypeWithCollectionGenericMembers y = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithCollectionGenericMembers>(x, @"<TypeWithCollectionGenericMembers xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><F1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>a1</a:string><a:string>a2</a:string></F1><F2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>b1</a:string><a:string>b2</a:string></F2><P1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>c1</a:string><a:string>c2</a:string></P1><P2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>d1</a:string><a:string>d2</a:string></P2><RO1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>abc</a:string></RO1><RO2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>xyz</a:string></RO2></TypeWithCollectionGenericMembers>");
        Assert.NotNull(y);
        Assert.True(y.F1.Count == 2, getCheckFailureMsg("F1"));
        Assert.True(y.F2.Count == 2, getCheckFailureMsg("F2"));
        Assert.True(y.P1.Count == 2, getCheckFailureMsg("P1"));
        Assert.True(y.P2.Count == 2, getCheckFailureMsg("P2"));
        Assert.True(y.RO1.Count == 1, getCheckFailureMsg("RO1"));
        Assert.True(y.RO2.Count == 1, getCheckFailureMsg("RO2"));




        foreach (var item in x.F1)
        {
            Assert.True(y.F1.Contains(item), getCheckFailureMsg("F1"));
        }
        foreach (var item in x.F2)
        {
            Assert.True(y.F2.Contains(item), getCheckFailureMsg("F2"));
        }
        foreach (var item in x.P1)
        {
            Assert.True(y.P1.Contains(item), getCheckFailureMsg("P1"));
        }
        foreach (var item in x.P2)
        {
            Assert.True(y.P2.Contains(item), getCheckFailureMsg("P2"));
        }
        foreach (var item in x.RO1)
        {
            Assert.True(y.RO1.Contains(item), getCheckFailureMsg("RO1"));
        }
        foreach (var item in x.RO2)
        {
            Assert.True(y.RO2.Contains(item), getCheckFailureMsg("RO2"));
        }
    }

    [Fact]
    public static void DCS_ListRoot()
    {
        MyList x = new MyList("a1", "a2");
        MyList y = DataContractSerializerHelper.SerializeAndDeserialize<MyList>(x, @"<ArrayOfanyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><anyType i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">a1</anyType><anyType i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">a2</anyType></ArrayOfanyType>");

        Assert.NotNull(y);
        Assert.True(y.Count == 2);

        foreach (var item in x)
        {
            Assert.True(y.Contains(item));
        }
    }

    [Fact]
    public static void DCS_ListMembers()
    {
        TypeWithListMembers x = new TypeWithListMembers
        {
            F1 = new MyList("a1", "a2"),
            F2 = new MyList("b1", "b2"),
            P1 = new MyList("c1", "c2"),
            P2 = new MyList("d1", "d2"),
        };
        x.RO1.Add("abc");
        x.RO2.Add("xyz");

        TypeWithListMembers y = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithListMembers>(x, @"<TypeWithListMembers xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><F1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">a1</a:anyType><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">a2</a:anyType></F1><F2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">b1</a:anyType><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">b2</a:anyType></F2><P1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">c1</a:anyType><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">c2</a:anyType></P1><P2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">d1</a:anyType><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">d2</a:anyType></P2><RO1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">abc</a:anyType></RO1><RO2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">xyz</a:anyType></RO2></TypeWithListMembers>");
        Assert.NotNull(y);
        Assert.True(y.F1.Count == 2, getCheckFailureMsg("F1"));
        Assert.True(y.F2.Count == 2, getCheckFailureMsg("F2"));
        Assert.True(y.P1.Count == 2, getCheckFailureMsg("P1"));
        Assert.True(y.P2.Count == 2, getCheckFailureMsg("P2"));
        Assert.True(y.RO1.Count == 1, getCheckFailureMsg("RO1"));
        Assert.True(y.RO2.Count == 1, getCheckFailureMsg("RO2"));

        Assert.True((string)x.F1[0] == (string)y.F1[0], getCheckFailureMsg("F1"));
        Assert.True((string)x.F1[1] == (string)y.F1[1], getCheckFailureMsg("F1"));
        Assert.True((string)x.F2[0] == (string)y.F2[0], getCheckFailureMsg("F2"));
        Assert.True((string)x.F2[1] == (string)y.F2[1], getCheckFailureMsg("F2"));
        Assert.True((string)x.P1[0] == (string)y.P1[0], getCheckFailureMsg("P1"));
        Assert.True((string)x.P1[1] == (string)y.P1[1], getCheckFailureMsg("P1"));
        Assert.True((string)x.P2[0] == (string)y.P2[0], getCheckFailureMsg("P2"));
        Assert.True((string)x.P2[1] == (string)y.P2[1], getCheckFailureMsg("P2"));
        Assert.True((string)x.RO1[0] == (string)y.RO1[0], getCheckFailureMsg("RO1"));
        Assert.True((string)x.RO2[0] == (string)y.RO2[0], getCheckFailureMsg("RO2"));
    }

    [Fact]
    public static void DCS_EnumerableGenericRoot()
    {
        MyEnumerable<string> x = new MyEnumerable<string>("a1", "a2");
        MyEnumerable<string> y = DataContractSerializerHelper.SerializeAndDeserialize<MyEnumerable<string>>(x, @"<ArrayOfstring xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><string>a1</string><string>a2</string></ArrayOfstring>");

        Assert.NotNull(y);
        Assert.True(y.Count == 2);

        string actual = string.Join("", y);
        Assert.Equal("a1a2", actual);
    }

    [Fact]
    public static void DCS_EnumerableGenericMembers()
    {
        TypeWithEnumerableGenericMembers x = new TypeWithEnumerableGenericMembers
        {
            F1 = new MyEnumerable<string>("a1", "a2"),
            F2 = new MyEnumerable<string>("b1", "b2"),
            P1 = new MyEnumerable<string>("c1", "c2"),
            P2 = new MyEnumerable<string>("d1", "d2")
        };
        x.RO1.Add("abc");

        TypeWithEnumerableGenericMembers y = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithEnumerableGenericMembers>(x, @"<TypeWithEnumerableGenericMembers xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><F1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>a1</a:string><a:string>a2</a:string></F1><F2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>b1</a:string><a:string>b2</a:string></F2><P1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>c1</a:string><a:string>c2</a:string></P1><P2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>d1</a:string><a:string>d2</a:string></P2><RO1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>abc</a:string></RO1></TypeWithEnumerableGenericMembers>");

        Assert.NotNull(y);
        Assert.True(y.F1.Count == 2);
        Assert.True(((string[])y.F2).Length == 2);
        Assert.True(y.P1.Count == 2);
        Assert.True(((string[])y.P2).Length == 2);
        Assert.True(y.RO1.Count == 1);
    }

    [Fact]
    public static void DCS_CollectionRoot()
    {
        MyCollection x = new MyCollection('a', 45);
        MyCollection y = DataContractSerializerHelper.SerializeAndDeserialize<MyCollection>(x, @"<ArrayOfanyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><anyType i:type=""a:char"" xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/"">97</anyType><anyType i:type=""a:int"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">45</anyType></ArrayOfanyType>");

        Assert.NotNull(y);
        Assert.True(y.Count == 2);
        Assert.True((char)y[0] == 'a');
        Assert.True((int)y[1] == 45);
    }

    [Fact]
    public static void DCS_CollectionMembers()
    {
        TypeWithCollectionMembers x = new TypeWithCollectionMembers
        {
            F1 = new MyCollection('a', 45),
            F2 = new MyCollection("ab", true),
            P1 = new MyCollection("x", "y"),
            P2 = new MyCollection(false, true)
        };
        x.RO1.Add("abc");

        TypeWithCollectionMembers y = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithCollectionMembers>(x, @"<TypeWithCollectionMembers xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><F1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:char"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">97</a:anyType><a:anyType i:type=""b:int"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">45</a:anyType></F1><F2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">ab</a:anyType><a:anyType i:type=""b:boolean"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">true</a:anyType></F2><P1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">x</a:anyType><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">y</a:anyType></P1><P2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:boolean"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">false</a:anyType><a:anyType i:type=""b:boolean"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">true</a:anyType></P2><RO1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">abc</a:anyType></RO1></TypeWithCollectionMembers>");
        Assert.NotNull(y);

        Assert.NotNull(y.F1);
        Assert.True(y.F1.Count == 2);
        Assert.True((char)y.F1[0] == 'a');
        Assert.True((int)y.F1[1] == 45);

        Assert.NotNull(y.F2);
        Assert.True(((object[])y.F2).Length == 2);
        Assert.True((string)((object[])y.F2)[0] == "ab");
        Assert.True((bool)((object[])y.F2)[1] == true);

        Assert.True(y.P1.Count == 2);
        Assert.True((string)y.P1[0] == "x");
        Assert.True((string)y.P1[1] == "y");

        Assert.True(((object[])y.P2).Length == 2);
        Assert.True((bool)((object[])y.P2)[0] == false);
        Assert.True((bool)((object[])y.P2)[1] == true);

        Assert.True(y.RO1.Count == 1);
        Assert.True((string)y.RO1[0] == "abc");
    }

    [Fact]
    public static void DCS_EnumerableRoot()
    {
        MyEnumerable x = new MyEnumerable("abc", 3);
        MyEnumerable y = DataContractSerializerHelper.SerializeAndDeserialize<MyEnumerable>(x, @"<ArrayOfanyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><anyType i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">abc</anyType><anyType i:type=""a:int"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">3</anyType></ArrayOfanyType>");

        Assert.NotNull(y);
        Assert.True(y.Count == 2);
        Assert.True((string)y[0] == "abc");
        Assert.True((int)y[1] == 3);
    }

    [Fact]
    public static void DCS_EnumerableMembers()
    {
        TypeWithEnumerableMembers x = new TypeWithEnumerableMembers
        {
            F1 = new MyEnumerable('a', 45),
            F2 = new MyEnumerable("ab", true),
            P1 = new MyEnumerable("x", "y"),
            P2 = new MyEnumerable(false, true)
        };
        x.RO1.Add('x');

        TypeWithEnumerableMembers y = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithEnumerableMembers>(x, @"<TypeWithEnumerableMembers xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><F1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:char"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">97</a:anyType><a:anyType i:type=""b:int"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">45</a:anyType></F1><F2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">ab</a:anyType><a:anyType i:type=""b:boolean"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">true</a:anyType></F2><P1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">x</a:anyType><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">y</a:anyType></P1><P2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:boolean"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">false</a:anyType><a:anyType i:type=""b:boolean"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">true</a:anyType></P2><RO1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:char"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">120</a:anyType></RO1></TypeWithEnumerableMembers>");
        Assert.NotNull(y);

        Assert.True(y.F1.Count == 2);
        Assert.True((char)y.F1[0] == 'a');
        Assert.True((int)y.F1[1] == 45);

        Assert.True(((object[])y.F2).Length == 2);
        Assert.True((string)((object[])y.F2)[0] == "ab");
        Assert.True((bool)((object[])y.F2)[1] == true);

        Assert.True(y.P1.Count == 2);
        Assert.True((string)y.P1[0] == "x");
        Assert.True((string)y.P1[1] == "y");

        Assert.True(((object[])y.P2).Length == 2);
        Assert.True((bool)((object[])y.P2)[0] == false);
        Assert.True((bool)((object[])y.P2)[1] == true);

        Assert.True(y.RO1.Count == 1);
        Assert.True((char)y.RO1[0] == 'x');
    }

    [Fact]
    public static void DCS_EnumerableMemberConcreteTypeWithoutDefaultConstructor()
    {
        TypeWithEnumerableMembers x = new TypeWithEnumerableMembers
        {
            F1 = new MyEnumerable('a', 45),
            F2 = new List<string> { "a", "b", "c" }.OrderBy(x => x),
            P1 = new MyEnumerable("x", "y"),
            P2 = Enumerable.Empty<int>()
        };

        var dcs = new DataContractSerializer(typeof(TypeWithEnumerableMembers));

        string baseline = @"<TypeWithEnumerableMembers xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><F1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:char"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">97</a:anyType><a:anyType i:type=""b:int"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">45</a:anyType></F1><F2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">a</a:anyType><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">b</a:anyType><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">c</a:anyType></F2><P1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">x</a:anyType><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">y</a:anyType></P1><P2 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""/><RO1 xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""/></TypeWithEnumerableMembers>";
        using (MemoryStream ms = new MemoryStream())
        {
            dcs.WriteObject(ms, x);
            ms.Position = 0;

            string actualOutput = new StreamReader(ms).ReadToEnd();

            Utils.CompareResult result = Utils.Compare(baseline, actualOutput);
            Assert.True(result.Equal, string.Format("{1}{0}Test failed for input: {2}{0}Expected: {3}{0}Actual: {4}",
                Environment.NewLine, result.ErrorMessage, x, baseline, actualOutput));
        }
    }

    [Fact]
    public static void DCS_CustomType()
    {
        MyTypeA x = new MyTypeA
        {
            PropX = new MyTypeC { PropC = 'a', PropB = true },
            PropY = 45,
        };

        MyTypeA y = DataContractSerializerHelper.SerializeAndDeserialize<MyTypeA>(x, @"<MyTypeA xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><P_Col_Array i:nil=""true""/><PropX i:type=""MyTypeC""><PropA i:nil=""true""/><PropC>97</PropC><PropB>true</PropB></PropX><PropY>45</PropY></MyTypeA>");

        Assert.NotNull(y);
        Assert.NotNull(y.PropX);
        Assert.StrictEqual(x.PropX.PropC, y.PropX.PropC);
        Assert.StrictEqual(((MyTypeC)x.PropX).PropB, ((MyTypeC)y.PropX).PropB);
        Assert.StrictEqual(x.PropY, y.PropY);
    }

    [Fact]
    public static void DCS_TypeWithPrivateFieldAndPrivateGetPublicSetProperty()
    {
        TypeWithPrivateFieldAndPrivateGetPublicSetProperty x = new TypeWithPrivateFieldAndPrivateGetPublicSetProperty
        {
            Name = "foo",
        };

        TypeWithPrivateFieldAndPrivateGetPublicSetProperty y = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithPrivateFieldAndPrivateGetPublicSetProperty>(x, @"<TypeWithPrivateFieldAndPrivateGetPublicSetProperty xmlns=""http://schemas.datacontract.org/2004/07/"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""></TypeWithPrivateFieldAndPrivateGetPublicSetProperty>");
        Assert.Null(y.GetName());
    }

    [Fact]
    public static void DCS_DataContractAttribute()
    {
        DataContractSerializerHelper.SerializeAndDeserialize<DCA_1>(new DCA_1 { P1 = "xyz" }, @"<DCA_1 xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""/>");
        DataContractSerializerHelper.SerializeAndDeserialize<DCA_2>(new DCA_2 { P1 = "xyz" }, @"<abc xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""/>");
        DataContractSerializerHelper.SerializeAndDeserialize<DCA_3>(new DCA_3 { P1 = "xyz" }, @"<DCA_3 xmlns=""def"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""/>");
        DataContractSerializerHelper.SerializeAndDeserialize<DCA_4>(new DCA_4 { P1 = "xyz" }, @"<DCA_4 z:Id=""i1"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/>");
        DataContractSerializerHelper.SerializeAndDeserialize<DCA_5>(new DCA_5 { P1 = "xyz" }, @"<abc xmlns=""def"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""/>");
    }

    [Fact]
    public static void DCS_DataMemberAttribute()
    {
        DataContractSerializerHelper.SerializeAndDeserialize<DMA_1>(new DMA_1 { P1 = "abc", P2 = 12, P3 = true, P4 = 'a', P5 = 10, MyDataMemberInAnotherNamespace = new MyDataContractClass04_1() { MyDataMember = "Test" }, Order100 = true, OrderMaxValue = false }, @"<DMA_1 xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><MyDataMemberInAnotherNamespace xmlns:a=""http://MyDataContractClass04_1.com/""><a:MyDataMember>Test</a:MyDataMember></MyDataMemberInAnotherNamespace><P1>abc</P1><P4>97</P4><P5>10</P5><xyz>12</xyz><P3>true</P3><Order100>true</Order100><OrderMaxValue>false</OrderMaxValue></DMA_1>");
    }

    [Fact]
    public static void DCS_IgnoreDataMemberAttribute()
    {
        IDMA_1 x = new IDMA_1 { MyDataMember = "MyDataMember", MyIgnoreDataMember = "MyIgnoreDataMember", MyUnsetDataMember = "MyUnsetDataMember" };
        IDMA_1 y = DataContractSerializerHelper.SerializeAndDeserialize<IDMA_1>(x, @"<IDMA_1 xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><MyDataMember>MyDataMember</MyDataMember></IDMA_1>");
        Assert.NotNull(y);
        Assert.Equal(x.MyDataMember, y.MyDataMember);
        Assert.Null(y.MyIgnoreDataMember);
        Assert.Null(y.MyUnsetDataMember);
    }

    [Fact]
    public static void DCS_EnumAsRoot()
    {
        //The approved types for an enum are byte, sbyte, short, ushort, int, uint, long, or ulong.
        Assert.StrictEqual(MyEnum.Two, DataContractSerializerHelper.SerializeAndDeserialize<MyEnum>(MyEnum.Two, @"<MyEnum xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"">Two</MyEnum>"));
        Assert.StrictEqual(ByteEnum.Option1, DataContractSerializerHelper.SerializeAndDeserialize<ByteEnum>(ByteEnum.Option1, @"<ByteEnum xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"">Option1</ByteEnum>"));
        Assert.StrictEqual(SByteEnum.Option1, DataContractSerializerHelper.SerializeAndDeserialize<SByteEnum>(SByteEnum.Option1, @"<SByteEnum xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"">Option1</SByteEnum>"));
        Assert.StrictEqual(ShortEnum.Option1, DataContractSerializerHelper.SerializeAndDeserialize<ShortEnum>(ShortEnum.Option1, @"<ShortEnum xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"">Option1</ShortEnum>"));
        Assert.StrictEqual(IntEnum.Option1, DataContractSerializerHelper.SerializeAndDeserialize(IntEnum.Option1, @"<IntEnum xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"">Option1</IntEnum>"));
        Assert.StrictEqual(UIntEnum.Option1, DataContractSerializerHelper.SerializeAndDeserialize<UIntEnum>(UIntEnum.Option1, @"<UIntEnum xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"">Option1</UIntEnum>"));
        Assert.StrictEqual(LongEnum.Option1, DataContractSerializerHelper.SerializeAndDeserialize(LongEnum.Option1, @"<LongEnum xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"">Option1</LongEnum>"));
        Assert.StrictEqual(ULongEnum.Option1, DataContractSerializerHelper.SerializeAndDeserialize<ULongEnum>(ULongEnum.Option1, @"<ULongEnum xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"">Option1</ULongEnum>"));
    }

    [Fact]
    public static void DCS_EnumAsMember()
    {
        TypeWithEnumMembers x = new TypeWithEnumMembers { F1 = MyEnum.Three, P1 = MyEnum.Two };
        TypeWithEnumMembers y = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithEnumMembers>(x, @"<TypeWithEnumMembers xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><F1>Three</F1><P1>Two</P1></TypeWithEnumMembers>");

        Assert.NotNull(y);
        Assert.StrictEqual(x.F1, y.F1);
        Assert.StrictEqual(x.P1, y.P1);
    }

    [Fact]
    public static void DCS_DCClassWithEnumAndStruct()
    {
        var x = new DCClassWithEnumAndStruct(true);
        var y = DataContractSerializerHelper.SerializeAndDeserialize<DCClassWithEnumAndStruct>(x, @"<DCClassWithEnumAndStruct xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><MyEnum1>One</MyEnum1><MyStruct><Data>Data</Data></MyStruct></DCClassWithEnumAndStruct>");

        Assert.StrictEqual(x.MyStruct, y.MyStruct);
        Assert.StrictEqual(x.MyEnum1, y.MyEnum1);
    }

    [Fact]
    public static void DCS_SuspensionManager()
    {
        var x = new Dictionary<string, object>();
        var subDictionary = new Dictionary<string, object>();
        subDictionary.Add("subkey1", "subkey1value");
        x.Add("Key1", subDictionary);

        Dictionary<string, object> y = DataContractSerializerHelper.SerializeAndDeserialize<Dictionary<string, object>>(x, @"<ArrayOfKeyValueOfstringanyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><KeyValueOfstringanyType><Key>Key1</Key><Value i:type=""ArrayOfKeyValueOfstringanyType""><KeyValueOfstringanyType><Key>subkey1</Key><Value i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">subkey1value</Value></KeyValueOfstringanyType></Value></KeyValueOfstringanyType></ArrayOfKeyValueOfstringanyType>");
        Assert.NotNull(y);
        Assert.StrictEqual(1, y.Count);
        Assert.True(y["Key1"] is Dictionary<string, object>);
        Assert.Equal("subkey1value", ((y["Key1"] as Dictionary<string, object>)["subkey1"]) as string);
    }

    [Fact]
    public static void DCS_BuiltInTypes()
    {
        BuiltInTypes x = new BuiltInTypes
        {
            ByteArray = new byte[] { 1, 2 }
        };
        BuiltInTypes y = DataContractSerializerHelper.SerializeAndDeserialize<BuiltInTypes>(x, @"<BuiltInTypes xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><ByteArray>AQI=</ByteArray></BuiltInTypes>");

        Assert.NotNull(y);
        Assert.Equal<byte>(x.ByteArray, y.ByteArray);
    }

    [Fact]
    public static void DCS_CircularLink()
    {
        CircularLinkDerived circularLinkDerived = new CircularLinkDerived(true);
        DataContractSerializerHelper.SerializeAndDeserialize<CircularLinkDerived>(circularLinkDerived, @"<CircularLinkDerived z:Id=""i1"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Link z:Id=""i2""><Link z:Id=""i3""><Link z:Ref=""i1""/><RandomHangingLink i:nil=""true""/></Link><RandomHangingLink i:nil=""true""/></Link><RandomHangingLink z:Id=""i4""><Link z:Id=""i5""><Link z:Id=""i6"" i:type=""CircularLinkDerived""><Link z:Ref=""i4""/><RandomHangingLink i:nil=""true""/></Link><RandomHangingLink i:nil=""true""/></Link><RandomHangingLink i:nil=""true""/></RandomHangingLink></CircularLinkDerived>");
    }

    [Fact]
    public static void DCS_DataMemberNames()
    {
        var obj = new AppEnvironment()
        {
            ScreenDpi = 440,
            ScreenOrientation = "horizontal"
        };
        var actual = DataContractSerializerHelper.SerializeAndDeserialize(obj, "<AppEnvironment xmlns=\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><screen_dpi_x0028_x_x003A_y_x0029_>440</screen_dpi_x0028_x_x003A_y_x0029_><screen_x003A_orientation>horizontal</screen_x003A_orientation></AppEnvironment>");
        Assert.StrictEqual(obj.ScreenDpi, actual.ScreenDpi);
        Assert.Equal(obj.ScreenOrientation, actual.ScreenOrientation);
    }

    [Fact]
    public static void DCS_GenericBase()
    {
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<GenericBase2<SimpleBaseDerived, SimpleBaseDerived2>>(new GenericBase2<SimpleBaseDerived, SimpleBaseDerived2>(true), @"<GenericBase2OfSimpleBaseDerivedSimpleBaseDerived2zbP0weY4 z:Id=""i1"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><genericData1 z:Id=""i2""><BaseData/><DerivedData/></genericData1><genericData2 z:Id=""i3""><BaseData/><DerivedData/></genericData2></GenericBase2OfSimpleBaseDerivedSimpleBaseDerived2zbP0weY4>");

        Assert.True(actual.genericData1 is SimpleBaseDerived);
        Assert.True(actual.genericData2 is SimpleBaseDerived2);
    }

    [Fact]
    public static void DCS_GenericContainer()
    {
        DataContractSerializerHelper.SerializeAndDeserialize<GenericContainer>(new GenericContainer(true), @"<GenericContainer z:Id=""i1"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><GenericData z:Id=""i2"" i:type=""GenericBaseOfSimpleBaseContainervjX03eZJ""><genericData z:Id=""i3"" i:type=""SimpleBaseContainer""><Base1 i:nil=""true""/><Base2 i:nil=""true""/></genericData></GenericData></GenericContainer>");
    }

    [Fact]
    public static void DCS_DictionaryWithVariousKeyValueTypes()
    {
        var x = new DictionaryWithVariousKeyValueTypes(true);

        var y = DataContractSerializerHelper.SerializeAndDeserialize<DictionaryWithVariousKeyValueTypes>(x, @"<DictionaryWithVariousKeyValueTypes xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><WithEnums xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfMyEnumMyEnumzbP0weY4><a:Key>Two</a:Key><a:Value>Three</a:Value></a:KeyValueOfMyEnumMyEnumzbP0weY4><a:KeyValueOfMyEnumMyEnumzbP0weY4><a:Key>One</a:Key><a:Value>One</a:Value></a:KeyValueOfMyEnumMyEnumzbP0weY4></WithEnums><WithNullables xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfNullableOfshortNullableOfboolean_ShTDFhl_P><a:Key>-32768</a:Key><a:Value>true</a:Value></a:KeyValueOfNullableOfshortNullableOfboolean_ShTDFhl_P><a:KeyValueOfNullableOfshortNullableOfboolean_ShTDFhl_P><a:Key>0</a:Key><a:Value>false</a:Value></a:KeyValueOfNullableOfshortNullableOfboolean_ShTDFhl_P><a:KeyValueOfNullableOfshortNullableOfboolean_ShTDFhl_P><a:Key>32767</a:Key><a:Value i:nil=""true""/></a:KeyValueOfNullableOfshortNullableOfboolean_ShTDFhl_P></WithNullables><WithStructs xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfStructNotSerializableStructNotSerializablezbP0weY4><a:Key><value>10</value></a:Key><a:Value><value>12</value></a:Value></a:KeyValueOfStructNotSerializableStructNotSerializablezbP0weY4><a:KeyValueOfStructNotSerializableStructNotSerializablezbP0weY4><a:Key><value>2147483647</value></a:Key><a:Value><value>-2147483648</value></a:Value></a:KeyValueOfStructNotSerializableStructNotSerializablezbP0weY4></WithStructs></DictionaryWithVariousKeyValueTypes>");

        Assert.StrictEqual(MyEnum.Three, y.WithEnums[MyEnum.Two]);
        Assert.StrictEqual(MyEnum.One, y.WithEnums[MyEnum.One]);
        Assert.StrictEqual(new StructNotSerializable() { value = 12 }, y.WithStructs[new StructNotSerializable() { value = 10 }]);
        Assert.StrictEqual(new StructNotSerializable() { value = int.MinValue }, y.WithStructs[new StructNotSerializable() { value = int.MaxValue }]);
        Assert.StrictEqual(true, y.WithNullables[short.MinValue]);
        Assert.StrictEqual(false, y.WithNullables[0]);
        Assert.Null(y.WithNullables[short.MaxValue]);
    }

    [Fact]
    public static void DCS_TypesWithArrayOfOtherTypes()
    {
        var x = new TypeHasArrayOfASerializedAsB(true);
        var y = DataContractSerializerHelper.SerializeAndDeserialize<TypeHasArrayOfASerializedAsB>(x, @"<TypeHasArrayOfASerializedAsB xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Items><TypeA><Name>typeAValue</Name></TypeA><TypeA><Name>typeBValue</Name></TypeA></Items></TypeHasArrayOfASerializedAsB>");

        Assert.Equal(x.Items[0].Name, y.Items[0].Name);
        Assert.Equal(x.Items[1].Name, y.Items[1].Name);
    }

    [Fact]
    public static void DCS_WithDuplicateNames()
    {
        var x = new WithDuplicateNames(true);
        var y = DataContractSerializerHelper.SerializeAndDeserialize<WithDuplicateNames>(x, "<WithDuplicateNames xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><ClassA1 xmlns:a=\"http://schemas.datacontract.org/2004/07/DuplicateTypeNamesTest.ns1\"><a:Name>Hello World! \u6F22 \u00F1</a:Name></ClassA1><ClassA2 xmlns:a=\"http://schemas.datacontract.org/2004/07/DuplicateTypeNamesTest.ns2\"><a:Nombre/></ClassA2><EnumA1>two</EnumA1><EnumA2>dos</EnumA2><StructA1 xmlns:a=\"http://schemas.datacontract.org/2004/07/DuplicateTypeNamesTest.ns1\"><a:Text/></StructA1><StructA2 xmlns:a=\"http://schemas.datacontract.org/2004/07/DuplicateTypeNamesTest.ns2\"><a:Texto/></StructA2></WithDuplicateNames>");

        Assert.Equal(x.ClassA1.Name, y.ClassA1.Name);
        Assert.StrictEqual(x.StructA1, y.StructA1);
        Assert.StrictEqual(x.EnumA1, y.EnumA1);
        Assert.StrictEqual(x.EnumA2, y.EnumA2);
        Assert.StrictEqual(x.StructA2, y.StructA2);
    }

    [Fact]
    public static void DCS_XElementAsRoot()
    {
        var original = new XElement("ElementName1");
        original.SetAttributeValue(XName.Get("Attribute1"), "AttributeValue1");
        original.SetValue("Value1");
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<XElement>(original, @"<ElementName1 Attribute1=""AttributeValue1"">Value1</ElementName1>");

        VerifyXElementObject(original, actual);
    }

    [Fact]
    public static void DCS_WithXElement()
    {
        var original = new WithXElement(true);
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<WithXElement>(original, @"<WithXElement xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><e><ElementName1 Attribute1=""AttributeValue1"" xmlns="""">Value1</ElementName1></e></WithXElement>");

        VerifyXElementObject(original.e, actual.e);
    }

    private static void VerifyXElementObject(XElement x1, XElement x2, bool checkFirstAttribute = true)
    {
        Assert.Equal(x1.Value, x2.Value);
        Assert.StrictEqual(x1.Name, x2.Name);
        if (checkFirstAttribute)
        {
            Assert.StrictEqual(x1.FirstAttribute.Name, x2.FirstAttribute.Name);
            Assert.Equal(x1.FirstAttribute.Value, x2.FirstAttribute.Value);
        }
    }

    [Fact]
    public static void DCS_WithXElementWithNestedXElement()
    {
        var original = new WithXElementWithNestedXElement(true);
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<WithXElementWithNestedXElement>(original, @"<WithXElementWithNestedXElement xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><e1><ElementName1 Attribute1=""AttributeValue1"" xmlns=""""><ElementName2 Attribute2=""AttributeValue2"">Value2</ElementName2></ElementName1></e1></WithXElementWithNestedXElement>");

        VerifyXElementObject(original.e1, actual.e1);
        VerifyXElementObject((XElement)original.e1.FirstNode, (XElement)actual.e1.FirstNode);
    }

    [Fact]
    public static void DCS_WithArrayOfXElement()
    {
        var original = new WithArrayOfXElement(true);
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<WithArrayOfXElement>(original, @"<WithArrayOfXElement xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><a xmlns:a=""http://schemas.datacontract.org/2004/07/System.Xml.Linq""><a:XElement><item xmlns=""http://p.com/"">item0</item></a:XElement><a:XElement><item xmlns=""http://p.com/"">item1</item></a:XElement><a:XElement><item xmlns=""http://p.com/"">item2</item></a:XElement></a></WithArrayOfXElement>");

        Assert.StrictEqual(original.a.Length, actual.a.Length);
        VerifyXElementObject(original.a[0], actual.a[0], checkFirstAttribute: false);
        VerifyXElementObject(original.a[1], actual.a[1], checkFirstAttribute: false);
        VerifyXElementObject(original.a[2], actual.a[2], checkFirstAttribute: false);
    }

    [Fact]
    public static void DCS_WithListOfXElement()
    {
        var original = new WithListOfXElement(true);
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<WithListOfXElement>(original, @"<WithListOfXElement xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><list xmlns:a=""http://schemas.datacontract.org/2004/07/System.Xml.Linq""><a:XElement><item xmlns=""http://p.com/"">item0</item></a:XElement><a:XElement><item xmlns=""http://p.com/"">item1</item></a:XElement><a:XElement><item xmlns=""http://p.com/"">item2</item></a:XElement></list></WithListOfXElement>");

        Assert.StrictEqual(original.list.Count, actual.list.Count);
        VerifyXElementObject(original.list[0], actual.list[0], checkFirstAttribute: false);
        VerifyXElementObject(original.list[1], actual.list[1], checkFirstAttribute: false);
        VerifyXElementObject(original.list[2], actual.list[2], checkFirstAttribute: false);
    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/60462", TestPlatforms.iOS | TestPlatforms.tvOS)]
    public static void DCS_DerivedTypeWithDifferentOverrides()
    {
        var x = new DerivedTypeWithDifferentOverrides() { Name1 = "Name1", Name2 = "Name2", Name3 = "Name3", Name4 = "Name4", Name5 = "Name5" };
        var y = DataContractSerializerHelper.SerializeAndDeserialize<DerivedTypeWithDifferentOverrides>(x, @"<DerivedTypeWithDifferentOverrides xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Name1>Name1</Name1><Name2 i:nil=""true""/><Name3 i:nil=""true""/><Name4 i:nil=""true""/><Name5 i:nil=""true""/><Name6 i:nil=""true""/><Name7 i:nil=""true""/><Name2>Name2</Name2><Name3>Name3</Name3><Name5>Name5</Name5></DerivedTypeWithDifferentOverrides>");

        Assert.Equal(x.Name1, y.Name1);
        Assert.Equal(x.Name2, y.Name2);
        Assert.Equal(x.Name3, y.Name3);
        Assert.Null(y.Name4);
        Assert.Equal(x.Name5, y.Name5);
    }

    [Fact]
    public static void DCS_TypeNamesWithSpecialCharacters()
    {
        var x = new __TypeNameWithSpecialCharacters\u6F22\u00F1() { PropertyNameWithSpecialCharacters\u6F22\u00F1 = "Test" };
        var y = DataContractSerializerHelper.SerializeAndDeserialize<__TypeNameWithSpecialCharacters\u6F22\u00F1>(x, "<__TypeNameWithSpecialCharacters\u6F22\u00F1 xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><PropertyNameWithSpecialCharacters\u6F22\u00F1>Test</PropertyNameWithSpecialCharacters\u6F22\u00F1></__TypeNameWithSpecialCharacters\u6F22\u00F1>");

        Assert.Equal(x.PropertyNameWithSpecialCharacters\u6F22\u00F1, y.PropertyNameWithSpecialCharacters\u6F22\u00F1);
    }

    [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.HasAssemblyFiles))]
#if XMLSERIALIZERGENERATORTESTS
    // Lack of AssemblyDependencyResolver results in assemblies that are not loaded by path to get
    // loaded in the default ALC, which causes problems for this test.
    [SkipOnPlatform(TestPlatforms.Browser, "AssemblyDependencyResolver not supported in wasm")]
#endif
    [ActiveIssue("34072", TestRuntimes.Mono)]
    public static void DCS_TypeInCollectibleALC()
    {
        ExecuteAndUnload("SerializableAssembly.dll", "SerializationTypes.SimpleType", makeCollection: false, out var weakRef);

        for (int i = 0; weakRef.IsAlive && i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.True(!weakRef.IsAlive);
    }

    [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.HasAssemblyFiles))]
#if XMLSERIALIZERGENERATORTESTS
    // Lack of AssemblyDependencyResolver results in assemblies that are not loaded by path to get
    // loaded in the default ALC, which causes problems for this test.
    [SkipOnPlatform(TestPlatforms.Browser, "AssemblyDependencyResolver not supported in wasm")]
#endif
    [ActiveIssue("34072", TestRuntimes.Mono)]
    public static void DCS_CollectionTypeInCollectibleALC()
    {
        ExecuteAndUnload("SerializableAssembly.dll", "SerializationTypes.SimpleType", makeCollection: true, out var weakRef);

        for (int i = 0; weakRef.IsAlive && i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.True(!weakRef.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ExecuteAndUnload(string assemblyfile, string typename, bool makeCollection, out WeakReference wref)
    {
        var fullPath = Path.GetFullPath(assemblyfile);
        var alc = new TestAssemblyLoadContext("DataContractSerializerTests", true, fullPath);
        object obj;
        wref = new WeakReference(alc);

        // Load assembly by path. By name, and it gets loaded in the default ALC.
        var asm = alc.LoadFromAssemblyPath(fullPath);

        // Ensure the type loaded in the intended non-Default ALC
        var type = asm.GetType(typename);
        Assert.Equal(AssemblyLoadContext.GetLoadContext(type.Assembly), alc);
        Assert.NotEqual(alc, AssemblyLoadContext.Default);

        if (makeCollection)
        {
            int arrayLength = 3;
            var array = Array.CreateInstance(type, arrayLength);
            for (int i = 0; i < arrayLength; i++)
            {
                array.SetValue(Activator.CreateInstance(type), i);
            }
            type = array.GetType();
            obj = array;
        }
        else
        {
            obj = Activator.CreateInstance(type);
        }

        // Round-Trip the instance
        var dcs = new DataContractSerializer(type);
        var rtobj = DataContractSerializerHelper.SerializeAndDeserialize<object>(obj, null, null, () => dcs, true, false);
        Assert.NotNull(rtobj);
        if (makeCollection)
            Assert.Equal(obj, rtobj);
        else
            Assert.True(rtobj.Equals(obj));

        alc.Unload();
    }

    [Fact]
    public static void DCS_JaggedArrayAsRoot()
    {
        int[][] jaggedIntegerArray = new int[][] { new int[] { 1, 3, 5, 7, 9 }, new int[] { 0, 2, 4, 6 }, new int[] { 11, 22 } };
        var actualJaggedIntegerArray = DataContractSerializerHelper.SerializeAndDeserialize<int[][]>(jaggedIntegerArray, @"<ArrayOfArrayOfint xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><ArrayOfint><int>1</int><int>3</int><int>5</int><int>7</int><int>9</int></ArrayOfint><ArrayOfint><int>0</int><int>2</int><int>4</int><int>6</int></ArrayOfint><ArrayOfint><int>11</int><int>22</int></ArrayOfint></ArrayOfArrayOfint>");

        Assert.Equal<int>(jaggedIntegerArray[0], actualJaggedIntegerArray[0]);
        Assert.Equal<int>(jaggedIntegerArray[1], actualJaggedIntegerArray[1]);
        Assert.Equal<int>(jaggedIntegerArray[2], actualJaggedIntegerArray[2]);

        string[][] jaggedStringArray = new string[][] { new string[] { "1", "3", "5", "7", "9" }, new string[] { "0", "2", "4", "6" }, new string[] { "11", "22" } };
        var actualJaggedStringArray = DataContractSerializerHelper.SerializeAndDeserialize<string[][]>(jaggedStringArray, @"<ArrayOfArrayOfstring xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><ArrayOfstring><string>1</string><string>3</string><string>5</string><string>7</string><string>9</string></ArrayOfstring><ArrayOfstring><string>0</string><string>2</string><string>4</string><string>6</string></ArrayOfstring><ArrayOfstring><string>11</string><string>22</string></ArrayOfstring></ArrayOfArrayOfstring>");

        Assert.Equal(jaggedStringArray[0], actualJaggedStringArray[0]);
        Assert.Equal(jaggedStringArray[1], actualJaggedStringArray[1]);
        Assert.Equal(jaggedStringArray[2], actualJaggedStringArray[2]);

        object[] objectArray = new object[] { 1, 1.0F, 1.0, "string", Guid.Parse("2054fd3e-e118-476a-9962-1a882be51860"), new DateTime(2013, 1, 2) };
        var actualObjectArray = DataContractSerializerHelper.SerializeAndDeserialize<object[]>(objectArray, @"<ArrayOfanyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><anyType i:type=""a:int"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">1</anyType><anyType i:type=""a:float"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">1</anyType><anyType i:type=""a:double"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">1</anyType><anyType i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">string</anyType><anyType i:type=""a:guid"" xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/"">2054fd3e-e118-476a-9962-1a882be51860</anyType><anyType i:type=""a:dateTime"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">2013-01-02T00:00:00</anyType></ArrayOfanyType>");

        Assert.True(1 == (int)actualObjectArray[0]);
        Assert.True(1.0F == (float)actualObjectArray[1]);
        Assert.True(1.0 == (double)actualObjectArray[2]);
        Assert.True("string" == (string)actualObjectArray[3]);
        Assert.True(Guid.Parse("2054fd3e-e118-476a-9962-1a882be51860") == (Guid)actualObjectArray[4]);
        Assert.True(new DateTime(2013, 1, 2) == (DateTime)actualObjectArray[5]);

        int[][][] jaggedIntegerArray2 = new int[][][] { new int[][] { new int[] { 1 }, new int[] { 3 } }, new int[][] { new int[] { 0 } }, new int[][] { new int[] { } } };
        var actualJaggedIntegerArray2 = DataContractSerializerHelper.SerializeAndDeserialize<int[][][]>(jaggedIntegerArray2, @"<ArrayOfArrayOfArrayOfint xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><ArrayOfArrayOfint><ArrayOfint><int>1</int></ArrayOfint><ArrayOfint><int>3</int></ArrayOfint></ArrayOfArrayOfint><ArrayOfArrayOfint><ArrayOfint><int>0</int></ArrayOfint></ArrayOfArrayOfint><ArrayOfArrayOfint><ArrayOfint/></ArrayOfArrayOfint></ArrayOfArrayOfArrayOfint>");

        Assert.True(actualJaggedIntegerArray2.Length == 3);
        Assert.True(actualJaggedIntegerArray2[0][0][0] == 1);
        Assert.True(actualJaggedIntegerArray2[0][1][0] == 3);
        Assert.True(actualJaggedIntegerArray2[1][0][0] == 0);
        Assert.True(actualJaggedIntegerArray2[2][0].Length == 0);
    }

    [Fact]
    public static void DCS_MyDataContractResolver()
    {
        var myresolver = new MyResolver();
        var settings = new DataContractSerializerSettings() { DataContractResolver = myresolver, KnownTypes = new Type[] { typeof(MyOtherType) } };
        var input = new MyType() { Value = new MyOtherType() { Str = "Hello World" } };
        var output = DataContractSerializerHelper.SerializeAndDeserialize<MyType>(input, @"<MyType xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Value i:type=""MyOtherType""><Str>Hello World</Str></Value></MyType>", settings);

        Assert.True(myresolver.ResolveNameInvoked, "myresolver.ResolveNameInvoked is false");
        Assert.True(myresolver.TryResolveTypeInvoked, "myresolver.TryResolveTypeInvoked is false");
        Assert.True(myresolver.DeclaredTypeIsNotNull, "myresolver.DeclaredTypeIsNotNull is false");
        Assert.True(input.OnSerializingMethodInvoked, "input.OnSerializingMethodInvoked is false");
        Assert.True(input.OnSerializedMethodInvoked, "input.OnSerializedMethodInvoked is false");
        Assert.True(output.OnDeserializingMethodInvoked, "output.OnDeserializingMethodInvoked is false");
        Assert.True(output.OnDeserializedMethodInvoked, "output.OnDeserializedMethodInvoked is false");
    }

    [Fact]
    public static void DCS_WriteObject_Use_DataContractResolver()
    {
        var settings = new DataContractSerializerSettings() { DataContractResolver = null, KnownTypes = new Type[] { typeof(MyOtherType) } };
        var dcs = new DataContractSerializer(typeof(MyType), settings);

        var value = new MyType() { Value = new MyOtherType() { Str = "Hello World" } };
        using (var ms = new MemoryStream())
        {
            var myresolver = new MyResolver();
            var xmlWriter = XmlDictionaryWriter.CreateTextWriter(ms);
            dcs.WriteObject(xmlWriter, value, myresolver);

            xmlWriter.Flush();
            ms.Position = 0;

            Assert.True(myresolver.ResolveNameInvoked, "myresolver.ResolveNameInvoked was false");
            Assert.True(myresolver.TryResolveTypeInvoked, "myresolver.TryResolveTypeInvoked was false");

            ms.Position = 0;
            myresolver = new MyResolver();
            var xmlReader = XmlDictionaryReader.CreateTextReader(ms, XmlDictionaryReaderQuotas.Max);
            MyType deserialized = (MyType)dcs.ReadObject(xmlReader, false, myresolver);

            Assert.NotNull(deserialized);
            Assert.True(deserialized.Value is MyOtherType, "deserialized.Value was not of MyOtherType.");
            Assert.Equal(((MyOtherType)value.Value).Str, ((MyOtherType)deserialized.Value).Str);

            Assert.True(myresolver.ResolveNameInvoked, "myresolver.ResolveNameInvoked was false");
        }
    }

    [Fact]
    public static void DCS_DataContractResolver_Property()
    {
        var myresolver = new MyResolver();
        var settings = new DataContractSerializerSettings() { DataContractResolver = myresolver };
        var dcs = new DataContractSerializer(typeof(MyType), settings);
        Assert.Equal(myresolver, dcs.DataContractResolver);
    }

    [Fact]
    public static void DCS_EnumerableStruct()
    {
        var original = new EnumerableStruct();
        original.Add("a");
        original.Add("b");

        var actual = DataContractSerializerHelper.SerializeAndDeserialize<EnumerableStruct>(original, @"<ArrayOfstring xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><string i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">a</string><string i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">b</string></ArrayOfstring>");

        Assert.Equal((IEnumerable<string>)actual, (IEnumerable<string>)original);
    }

    [Fact]
    public static void DCS_EnumerableCollection()
    {
        var original = new EnumerableCollection();
        original.Add(new DateTime(100, DateTimeKind.Utc));
        original.Add(new DateTime(200, DateTimeKind.Utc));
        original.Add(new DateTime(300, DateTimeKind.Utc));

        var actual = DataContractSerializerHelper.SerializeAndDeserialize<EnumerableCollection>(original, @"<ArrayOfdateTime xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><dateTime i:type=""a:dateTime"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00.00001Z</dateTime><dateTime i:type=""a:dateTime"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00.00002Z</dateTime><dateTime i:type=""a:dateTime"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00.00003Z</dateTime></ArrayOfdateTime>");

        Assert.Equal((IEnumerable<DateTime>)actual, (IEnumerable<DateTime>)original);
    }

    [Fact]
    public static void DCS_BaseClassAndDerivedClassWithSameProperty()
    {
        var value = new DerivedClassWithSameProperty() { DateTimeProperty = new DateTime(100), IntProperty = 5, StringProperty = "TestString", ListProperty = new List<string>() };
        value.ListProperty.AddRange(new string[] { "one", "two", "three" });
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<DerivedClassWithSameProperty>(value, @"<DerivedClassWithSameProperty xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><DateTimeProperty>0001-01-01T00:00:00</DateTimeProperty><IntProperty>0</IntProperty><ListProperty i:nil=""true"" xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""/><StringProperty i:nil=""true""/><DateTimeProperty>0001-01-01T00:00:00.00001</DateTimeProperty><IntProperty>5</IntProperty><ListProperty xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>one</a:string><a:string>two</a:string><a:string>three</a:string></ListProperty><StringProperty>TestString</StringProperty></DerivedClassWithSameProperty>");

        Assert.StrictEqual(value.DateTimeProperty, actual.DateTimeProperty);
        Assert.StrictEqual(value.IntProperty, actual.IntProperty);
        Assert.Equal(value.StringProperty, actual.StringProperty);
        Assert.NotNull(actual.ListProperty);
        Assert.True(value.ListProperty.Count == actual.ListProperty.Count);
        Assert.Equal("one", actual.ListProperty[0]);
        Assert.Equal("two", actual.ListProperty[1]);
        Assert.Equal("three", actual.ListProperty[2]);
    }

    [Fact]
    public static void DCS_ContainsLinkedList()
    {
        var value = new ContainsLinkedList(true);

        DataContractSerializerHelper.SerializeAndDeserialize<ContainsLinkedList>(value, @"<ContainsLinkedList xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Data><SimpleDCWithRef><Data><Data>23:59:59</Data></Data><RefData><Data>23:59:59</Data></RefData></SimpleDCWithRef><SimpleDCWithRef><Data><Data>23:59:59</Data></Data><RefData><Data>23:59:59</Data></RefData></SimpleDCWithRef><SimpleDCWithRef><Data><Data>23:59:59</Data></Data><RefData><Data>23:59:59</Data></RefData></SimpleDCWithRef><SimpleDCWithRef><Data><Data>23:59:59</Data></Data><RefData><Data>23:59:59</Data></RefData></SimpleDCWithRef><SimpleDCWithRef><Data><Data>23:59:59</Data></Data><RefData><Data>23:59:59</Data></RefData></SimpleDCWithRef><SimpleDCWithRef><Data><Data>23:59:59</Data></Data><RefData><Data>23:59:59</Data></RefData></SimpleDCWithRef><SimpleDCWithRef><Data><Data>23:59:59</Data></Data><RefData><Data>23:59:59</Data></RefData></SimpleDCWithRef></Data></ContainsLinkedList>");
    }

    [Fact]
    public static void DCS_SimpleCollectionDataContract()
    {
        var value = new SimpleCDC(true);
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<SimpleCDC>(value, @"<SimpleCDC xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Item>One</Item><Item>Two</Item><Item>Three</Item></SimpleCDC>");

        Assert.True(actual.Count == 3);
        Assert.Contains("One", actual);
        Assert.Contains("Two", actual);
        Assert.Contains("Three", actual);
    }

    [Fact]
    public static void DCS_MyDerivedCollectionContainer()
    {
        var value = new MyDerivedCollectionContainer();
        value.Items.AddLast("One");
        value.Items.AddLast("Two");
        value.Items.AddLast("Three");
        DataContractSerializerHelper.SerializeAndDeserialize<MyDerivedCollectionContainer>(value, @"<MyDerivedCollectionContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Items><string>One</string><string>Two</string><string>Three</string></Items></MyDerivedCollectionContainer>");
    }

    [Fact]
    public static void DCS_EnumFlags()
    {
        EnumFlags value1 = EnumFlags.One | EnumFlags.Four;
        var value2 = DataContractSerializerHelper.SerializeAndDeserialize<EnumFlags>(value1, @"<EnumFlags xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"">One Four</EnumFlags>");
        Assert.StrictEqual(value1, value2);
    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/60462", TestPlatforms.iOS | TestPlatforms.tvOS)]
    public static void DCS_SerializeClassThatImplementsInterface()
    {
        ClassImplementsInterface value = new ClassImplementsInterface() { ClassID = "ClassID", DisplayName = "DisplayName", Id = "Id", IsLoaded = true };
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<ClassImplementsInterface>(value, @"<ClassImplementsInterface xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><DisplayName>DisplayName</DisplayName><Id>Id</Id></ClassImplementsInterface>");


        Assert.Equal(value.DisplayName, actual.DisplayName);
        Assert.Equal(value.Id, actual.Id);
    }

    [Fact]
    public static void DCS_Nullables()
    {
        // Arrange
        var baseline = @"<WithNullables xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Optional>Option1</Optional><OptionalInt>42</OptionalInt><Optionull i:nil=""true""/><OptionullInt i:nil=""true""/><Struct1><A>1</A><B>2</B></Struct1><Struct2 i:nil=""true""/></WithNullables>";

        var item = new WithNullables()
        {
            Optional = IntEnum.Option1,
            OptionalInt = 42,
            Struct1 = new SomeStruct { A = 1, B = 2 }
        };

        // Act
        var actual = DataContractSerializerHelper.SerializeAndDeserialize(item, baseline);

        // Assert
        Assert.StrictEqual(item.OptionalInt, actual.OptionalInt);
        Assert.StrictEqual(item.Optional, actual.Optional);
        Assert.StrictEqual(item.Optionull, actual.Optionull);
        Assert.StrictEqual(item.OptionullInt, actual.OptionullInt);
        Assert.Null(actual.Struct2);
        Assert.StrictEqual(item.Struct1.Value.A, actual.Struct1.Value.A);
        Assert.StrictEqual(item.Struct1.Value.B, actual.Struct1.Value.B);
    }

    [Fact]
    public static void DCS_SimpleStructWithProperties()
    {
        SimpleStructWithProperties x = new SimpleStructWithProperties() { Num = 1, Text = "Foo" };
        var y = DataContractSerializerHelper.SerializeAndDeserialize(x, "<SimpleStructWithProperties xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><Num>1</Num><Text>Foo</Text></SimpleStructWithProperties>");

        Assert.True(x.Num == y.Num, "x.Num != y.Num");
        Assert.True(x.Text == y.Text, "x.Text != y.Text");
    }

    [Fact]
    public static void DCS_InternalTypeSerialization()
    {
        var value = new InternalType() { InternalProperty = 12 };
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<InternalType>(value, @"<InternalType xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><InternalProperty>12</InternalProperty><PrivateProperty>100</PrivateProperty></InternalType>");
        Assert.StrictEqual(deserializedValue.InternalProperty, value.InternalProperty);
        Assert.StrictEqual(deserializedValue.GetPrivatePropertyValue(), value.GetPrivatePropertyValue());
    }

    [Fact]
    public static void DCS_PrivateTypeSerialization()
    {
        var value = new PrivateType();
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<PrivateType>(value, @"<DataContractSerializerTests.PrivateType xmlns=""http://schemas.datacontract.org/2004/07/"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><InternalProperty>1</InternalProperty><PrivateProperty>2</PrivateProperty></DataContractSerializerTests.PrivateType>");
        Assert.StrictEqual(deserializedValue.GetInternalPropertyValue(), value.GetInternalPropertyValue());
        Assert.StrictEqual(deserializedValue.GetPrivatePropertyValue(), value.GetPrivatePropertyValue());
    }

#region private type has to be in with in the class
    [DataContract]
    private class PrivateType
    {
        public PrivateType()
        {
            InternalProperty = 1;
            PrivateProperty = 2;
        }

        [DataMember]
        internal int InternalProperty { get; set; }

        [DataMember]
        private int PrivateProperty { get; set; }

        public int GetInternalPropertyValue()
        {
            return InternalProperty;
        }

        public int GetPrivatePropertyValue()
        {
            return PrivateProperty;
        }
    }
#endregion

    [Fact]
    public static void DCS_RootNameAndNamespaceThroughConstructorAsString()
    {
        //Constructor# 3
        var obj = new MyOtherType() { Str = "Hello" };
        Func<DataContractSerializer> serializerFactory = () => new DataContractSerializer(typeof(MyOtherType), "ChangedRoot", "http://changedNamespace");
        string baselineXml = @"<ChangedRoot xmlns=""http://changedNamespace"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><a:Str>Hello</a:Str></ChangedRoot>";
        var result = DataContractSerializerHelper.SerializeAndDeserialize(obj, baselineXml, serializerFactory: serializerFactory);
        Assert.Equal("Hello", result.Str);
    }

    [Fact]
    public static void DCS_RootNameAndNamespaceThroughConstructorAsXmlDictionary()
    {
        //Constructor# 4
        var xmlDictionary = new XmlDictionary();
        var obj = new MyOtherType() { Str = "Hello" };
        Func<DataContractSerializer> serializerFactory = () => new DataContractSerializer(typeof(MyOtherType), xmlDictionary.Add("ChangedRoot"), xmlDictionary.Add("http://changedNamespace"));
        string baselineXml = @"<ChangedRoot xmlns=""http://changedNamespace"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><a:Str>Hello</a:Str></ChangedRoot>";
        var result = DataContractSerializerHelper.SerializeAndDeserialize(obj, baselineXml, serializerFactory: serializerFactory);
        Assert.Equal("Hello", result.Str);
    }

    [Fact]
    public static void DCS_RootNameAndNamespaceThroughSettings()
    {
        var xmlDictionary = new XmlDictionary();
        var obj = new MyOtherType() { Str = "Hello" };
        var settings = new DataContractSerializerSettings() { RootName = xmlDictionary.Add("ChangedRoot"), RootNamespace = xmlDictionary.Add("http://changedNamespace") };
        Func<DataContractSerializer> serializerFactory = () => new DataContractSerializer(typeof(MyOtherType), settings);
        string baselineXml = @"<ChangedRoot xmlns=""http://changedNamespace"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><a:Str>Hello</a:Str></ChangedRoot>";
        var result = DataContractSerializerHelper.SerializeAndDeserialize(obj, baselineXml, serializerFactory: serializerFactory);
        Assert.Equal("Hello", result.Str);
    }

    [Fact]
    public static void DCS_RootNameWithoutNamespaceThroughSettings()
    {
        var xmlDictionary = new XmlDictionary();
        var obj = new MyOtherType() { Str = "Hello" };
        var settings = new DataContractSerializerSettings() { RootName = xmlDictionary.Add("ChangedRoot") };
        Func<DataContractSerializer> serializerFactory = () => new DataContractSerializer(typeof(MyOtherType), settings);
        string baselineXml = @"<ChangedRoot xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><a:Str>Hello</a:Str></ChangedRoot>";
        var result = DataContractSerializerHelper.SerializeAndDeserialize(obj, baselineXml, serializerFactory: serializerFactory);
        Assert.Equal("Hello", result.Str);
    }

    [Fact]
    public static void DCS_KnownTypesThroughConstructor()
    {
        //Constructor# 5
        var value = new KnownTypesThroughConstructor() { EnumValue = MyEnum.One, SimpleTypeValue = new SimpleKnownTypeValue() { StrProperty = "PropertyValue" } };
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<KnownTypesThroughConstructor>(value,
            @"<KnownTypesThroughConstructor xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><EnumValue i:type=""MyEnum"">One</EnumValue><SimpleTypeValue i:type=""SimpleKnownTypeValue""><StrProperty>PropertyValue</StrProperty></SimpleTypeValue></KnownTypesThroughConstructor>",
            null, () => { return new DataContractSerializer(typeof(KnownTypesThroughConstructor), new Type[] { typeof(MyEnum), typeof(SimpleKnownTypeValue) }); });

        Assert.StrictEqual((MyEnum)value.EnumValue, (MyEnum)actual.EnumValue);
        Assert.True(actual.SimpleTypeValue is SimpleKnownTypeValue);
        Assert.Equal("PropertyValue", ((SimpleKnownTypeValue)actual.SimpleTypeValue).StrProperty);
    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/60462", TestPlatforms.iOS | TestPlatforms.tvOS)]
    public static void DCS_DuplicatedKnownTypesWithAdapterThroughConstructor()
    {
        //Constructor# 5
        DateTimeOffset dto = new DateTimeOffset(new DateTime(2015, 11, 11), new TimeSpan(0, 0, 0));
        var value = new KnownTypesThroughConstructor() { EnumValue = dto, SimpleTypeValue = dto };
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<KnownTypesThroughConstructor>(value,
            @"<KnownTypesThroughConstructor xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><EnumValue i:type=""a:DateTimeOffset"" xmlns:a=""http://schemas.datacontract.org/2004/07/System""><a:DateTime>2015-11-11T00:00:00Z</a:DateTime><a:OffsetMinutes>0</a:OffsetMinutes></EnumValue><SimpleTypeValue i:type=""a:DateTimeOffset"" xmlns:a=""http://schemas.datacontract.org/2004/07/System""><a:DateTime>2015-11-11T00:00:00Z</a:DateTime><a:OffsetMinutes>0</a:OffsetMinutes></SimpleTypeValue></KnownTypesThroughConstructor>",
            null, () => { return new DataContractSerializer(typeof(KnownTypesThroughConstructor), new Type[] { typeof(DateTimeOffset), typeof(DateTimeOffset) }); });

        Assert.StrictEqual((DateTimeOffset)value.EnumValue, (DateTimeOffset)actual.EnumValue);
        Assert.True(actual.SimpleTypeValue is DateTimeOffset);
        Assert.StrictEqual((DateTimeOffset)actual.SimpleTypeValue, (DateTimeOffset)actual.SimpleTypeValue);
    }

    [Fact]
    public static void DCS_KnownTypesThroughSettings()
    {
        //Constructor# 2.1
        var value = new KnownTypesThroughConstructor() { EnumValue = MyEnum.One, SimpleTypeValue = new SimpleKnownTypeValue() { StrProperty = "PropertyValue" } };
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<KnownTypesThroughConstructor>(value,
            @"<KnownTypesThroughConstructor xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><EnumValue i:type=""MyEnum"">One</EnumValue><SimpleTypeValue i:type=""SimpleKnownTypeValue""><StrProperty>PropertyValue</StrProperty></SimpleTypeValue></KnownTypesThroughConstructor>",
            new DataContractSerializerSettings() { KnownTypes = new Type[] { typeof(MyEnum), typeof(SimpleKnownTypeValue) } });

        Assert.StrictEqual((MyEnum)value.EnumValue, (MyEnum)actual.EnumValue);
        Assert.True(actual.SimpleTypeValue is SimpleKnownTypeValue);
        Assert.Equal("PropertyValue", ((SimpleKnownTypeValue)actual.SimpleTypeValue).StrProperty);
    }

    [Fact]
    public static void DCS_RootNameNamespaceAndKnownTypesThroughConstructorAsStrings()
    {
        //Constructor# 6
        var value = new KnownTypesThroughConstructor() { EnumValue = MyEnum.One, SimpleTypeValue = new SimpleKnownTypeValue() { StrProperty = "PropertyValue" } };
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<KnownTypesThroughConstructor>(value,
            @"<ChangedRoot xmlns=""http://changedNamespace"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><a:EnumValue i:type=""a:MyEnum"">One</a:EnumValue><a:SimpleTypeValue i:type=""a:SimpleKnownTypeValue""><a:StrProperty>PropertyValue</a:StrProperty></a:SimpleTypeValue></ChangedRoot>",
            null, () => { return new DataContractSerializer(typeof(KnownTypesThroughConstructor), "ChangedRoot", "http://changedNamespace", new Type[] { typeof(MyEnum), typeof(SimpleKnownTypeValue) }); });

        Assert.StrictEqual((MyEnum)value.EnumValue, (MyEnum)actual.EnumValue);
        Assert.True(actual.SimpleTypeValue is SimpleKnownTypeValue);
        Assert.Equal("PropertyValue", ((SimpleKnownTypeValue)actual.SimpleTypeValue).StrProperty);
    }

    [Fact]
    public static void DCS_RootNameNamespaceAndKnownTypesThroughConstructorAsXmlDictionary()
    {
        //Constructor# 7
        var xmlDictionary = new XmlDictionary();
        var value = new KnownTypesThroughConstructor() { EnumValue = MyEnum.One, SimpleTypeValue = new SimpleKnownTypeValue() { StrProperty = "PropertyValue" } };
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<KnownTypesThroughConstructor>(value,
            @"<ChangedRoot xmlns=""http://changedNamespace"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><a:EnumValue i:type=""a:MyEnum"">One</a:EnumValue><a:SimpleTypeValue i:type=""a:SimpleKnownTypeValue""><a:StrProperty>PropertyValue</a:StrProperty></a:SimpleTypeValue></ChangedRoot>",
            null, () => { return new DataContractSerializer(typeof(KnownTypesThroughConstructor), xmlDictionary.Add("ChangedRoot"), xmlDictionary.Add("http://changedNamespace"), new Type[] { typeof(MyEnum), typeof(SimpleKnownTypeValue) }); });

        Assert.StrictEqual((MyEnum)value.EnumValue, (MyEnum)actual.EnumValue);
        Assert.True(actual.SimpleTypeValue is SimpleKnownTypeValue);
        Assert.Equal("PropertyValue", ((SimpleKnownTypeValue)actual.SimpleTypeValue).StrProperty);
    }

    [Fact]
    public static void DCS_ExceptionObject()
    {
        var value = new Exception("Test Exception");
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<Exception>(value, @"<Exception xmlns=""http://schemas.datacontract.org/2004/07/System"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:x=""http://www.w3.org/2001/XMLSchema""><ClassName i:type=""x:string"" xmlns="""">System.Exception</ClassName><Message i:type=""x:string"" xmlns="""">Test Exception</Message><Data i:nil=""true"" xmlns=""""/><InnerException i:nil=""true"" xmlns=""""/><HelpURL i:nil=""true"" xmlns=""""/><StackTraceString i:nil=""true"" xmlns=""""/><RemoteStackTraceString i:nil=""true"" xmlns=""""/><RemoteStackIndex i:type=""x:int"" xmlns="""">0</RemoteStackIndex><ExceptionMethod i:nil=""true"" xmlns=""""/><HResult i:type=""x:int"" xmlns="""">-2146233088</HResult><Source i:nil=""true"" xmlns=""""/><WatsonBuckets i:nil=""true"" xmlns=""""/></Exception>");

        Assert.Equal(value.Message, actual.Message);
        Assert.Equal(value.Source, actual.Source);
        Assert.Equal(value.StackTrace, actual.StackTrace);
        Assert.StrictEqual(value.HResult, actual.HResult);
        Assert.Equal(value.HelpLink, actual.HelpLink);
    }

    [Fact]
    public static void DCS_MyArgumentExceptionObject()
    {
        var value = new MyArgumentException("Test Exception", "paramName");
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<MyArgumentException>(value, @"<MyArgumentException xmlns=""http://schemas.datacontract.org/2004/07/"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:x=""http://www.w3.org/2001/XMLSchema""><ClassName i:type=""x:string"" xmlns="""">MyArgumentException</ClassName><Message i:type=""x:string"" xmlns="""">Test Exception</Message><Data i:nil=""true"" xmlns=""""/><InnerException i:nil=""true"" xmlns=""""/><HelpURL i:nil=""true"" xmlns=""""/><StackTraceString i:nil=""true"" xmlns=""""/><RemoteStackTraceString i:nil=""true"" xmlns=""""/><RemoteStackIndex i:type=""x:int"" xmlns="""">0</RemoteStackIndex><ExceptionMethod i:nil=""true"" xmlns=""""/><HResult i:type=""x:int"" xmlns="""">-2146233088</HResult><Source i:nil=""true"" xmlns=""""/><WatsonBuckets i:nil=""true"" xmlns=""""/><ParamName i:type=""x:string"" xmlns="""">paramName</ParamName></MyArgumentException>");

        Assert.Equal(value.Message, actual.Message);
        Assert.Equal(value.ParamName, actual.ParamName);
        Assert.Equal(value.Source, actual.Source);
        Assert.Equal(value.StackTrace, actual.StackTrace);
        Assert.StrictEqual(value.HResult, actual.HResult);
        Assert.Equal(value.HelpLink, actual.HelpLink);
    }

    [Fact]
    public static void DCS_ExceptionMessageWithSpecialChars()
    {
        var value = new Exception("Test Exception<>&'\"");
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<Exception>(value, @"<Exception xmlns=""http://schemas.datacontract.org/2004/07/System"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:x=""http://www.w3.org/2001/XMLSchema""><ClassName i:type=""x:string"" xmlns="""">System.Exception</ClassName><Message i:type=""x:string"" xmlns="""">Test Exception&lt;&gt;&amp;'""</Message><Data i:nil=""true"" xmlns=""""/><InnerException i:nil=""true"" xmlns=""""/><HelpURL i:nil=""true"" xmlns=""""/><StackTraceString i:nil=""true"" xmlns=""""/><RemoteStackTraceString i:nil=""true"" xmlns=""""/><RemoteStackIndex i:type=""x:int"" xmlns="""">0</RemoteStackIndex><ExceptionMethod i:nil=""true"" xmlns=""""/><HResult i:type=""x:int"" xmlns="""">-2146233088</HResult><Source i:nil=""true"" xmlns=""""/><WatsonBuckets i:nil=""true"" xmlns=""""/></Exception>");

        Assert.Equal(value.Message, actual.Message);
        Assert.Equal(value.Source, actual.Source);
        Assert.Equal(value.StackTrace, actual.StackTrace);
        Assert.StrictEqual(value.HResult, actual.HResult);
        Assert.Equal(value.HelpLink, actual.HelpLink);
    }

    [Fact]
    public static void DCS_InnerExceptionMessageWithSpecialChars()
    {
        var value = new Exception("", new Exception("Test Exception<>&'\""));
        var actual = DataContractSerializerHelper.SerializeAndDeserialize<Exception>(value, @"<Exception xmlns=""http://schemas.datacontract.org/2004/07/System"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:x=""http://www.w3.org/2001/XMLSchema""><ClassName i:type=""x:string"" xmlns="""">System.Exception</ClassName><Message i:type=""x:string"" xmlns=""""/><Data i:nil=""true"" xmlns=""""/><InnerException i:type=""a:Exception"" xmlns="""" xmlns:a=""http://schemas.datacontract.org/2004/07/System""><ClassName i:type=""x:string"">System.Exception</ClassName><Message i:type=""x:string"">Test Exception&lt;&gt;&amp;'""</Message><Data i:nil=""true""/><InnerException i:nil=""true""/><HelpURL i:nil=""true""/><StackTraceString i:nil=""true""/><RemoteStackTraceString i:nil=""true""/><RemoteStackIndex i:type=""x:int"">0</RemoteStackIndex><ExceptionMethod i:nil=""true""/><HResult i:type=""x:int"">-2146233088</HResult><Source i:nil=""true""/><WatsonBuckets i:nil=""true""/></InnerException><HelpURL i:nil=""true"" xmlns=""""/><StackTraceString i:nil=""true"" xmlns=""""/><RemoteStackTraceString i:nil=""true"" xmlns=""""/><RemoteStackIndex i:type=""x:int"" xmlns="""">0</RemoteStackIndex><ExceptionMethod i:nil=""true"" xmlns=""""/><HResult i:type=""x:int"" xmlns="""">-2146233088</HResult><Source i:nil=""true"" xmlns=""""/><WatsonBuckets i:nil=""true"" xmlns=""""/></Exception>");

        Assert.Equal(value.Message, actual.Message);
        Assert.Equal(value.Source, actual.Source);
        Assert.Equal(value.StackTrace, actual.StackTrace);
        Assert.StrictEqual(value.HResult, actual.HResult);
        Assert.Equal(value.HelpLink, actual.HelpLink);

        Assert.Equal(value.InnerException.Message, actual.InnerException.Message);
        Assert.Equal(value.InnerException.Source, actual.InnerException.Source);
        Assert.Equal(value.InnerException.StackTrace, actual.InnerException.StackTrace);
        Assert.StrictEqual(value.InnerException.HResult, actual.InnerException.HResult);
        Assert.Equal(value.InnerException.HelpLink, actual.InnerException.HelpLink);
    }

    [Fact]
    public static void DCS_TypeWithUriTypeProperty()
    {
        var value = new TypeWithUriTypeProperty() { ConfigUri = new Uri("http://www.bing.com") };

        var actual = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<TypeWithUriTypeProperty xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><ConfigUri>http://www.bing.com/</ConfigUri></TypeWithUriTypeProperty>");

        Assert.StrictEqual(value.ConfigUri, actual.ConfigUri);
    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/60462", TestPlatforms.iOS | TestPlatforms.tvOS)]
    public static void DCS_TypeWithDatetimeOffsetTypeProperty()
    {
        var value = new TypeWithDateTimeOffsetTypeProperty() { ModifiedTime = new DateTimeOffset(new DateTime(2013, 1, 2, 3, 4, 5, 6, DateTimeKind.Utc)) };
        var actual = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<TypeWithDateTimeOffsetTypeProperty xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><ModifiedTime xmlns:a=""http://schemas.datacontract.org/2004/07/System""><a:DateTime>2013-01-02T03:04:05.006Z</a:DateTime><a:OffsetMinutes>0</a:OffsetMinutes></ModifiedTime></TypeWithDateTimeOffsetTypeProperty>");
        Assert.StrictEqual(value.ModifiedTime, actual.ModifiedTime);

        // Assume that UTC offset doesn't change more often than once in the day 2013-01-02
        // DO NOT USE TimeZoneInfo.Local.BaseUtcOffset !
        var offsetMinutes = (int)TimeZoneInfo.Local.GetUtcOffset(new DateTime(2013, 1, 2)).TotalMinutes;
        // Adding offsetMinutes to ModifiedTime property so the DateTime component in serialized strings are time-zone independent
        value = new TypeWithDateTimeOffsetTypeProperty() { ModifiedTime = new DateTimeOffset(new DateTime(2013, 1, 2, 3, 4, 5, 6).AddMinutes(offsetMinutes)) };
        actual = DataContractSerializerHelper.SerializeAndDeserialize(value, string.Format(@"<TypeWithDateTimeOffsetTypeProperty xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><ModifiedTime xmlns:a=""http://schemas.datacontract.org/2004/07/System""><a:DateTime>2013-01-02T03:04:05.006Z</a:DateTime><a:OffsetMinutes>{0}</a:OffsetMinutes></ModifiedTime></TypeWithDateTimeOffsetTypeProperty>", offsetMinutes));
        Assert.StrictEqual(value.ModifiedTime, actual.ModifiedTime);

        value = new TypeWithDateTimeOffsetTypeProperty() { ModifiedTime = new DateTimeOffset(new DateTime(2013, 1, 2, 3, 4, 5, 6, DateTimeKind.Local).AddMinutes(offsetMinutes)) };
        actual = DataContractSerializerHelper.SerializeAndDeserialize(value, string.Format(@"<TypeWithDateTimeOffsetTypeProperty xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><ModifiedTime xmlns:a=""http://schemas.datacontract.org/2004/07/System""><a:DateTime>2013-01-02T03:04:05.006Z</a:DateTime><a:OffsetMinutes>{0}</a:OffsetMinutes></ModifiedTime></TypeWithDateTimeOffsetTypeProperty>", offsetMinutes));
        Assert.StrictEqual(value.ModifiedTime, actual.ModifiedTime);
    }

    [Fact]
    public static void DCS_Tuple()
    {
        DCS_Tuple1();
        DCS_Tuple2();
        DCS_Tuple3();
        DCS_Tuple4();
        DCS_Tuple5();
        DCS_Tuple6();
        DCS_Tuple7();
        DCS_Tuple8();
    }

    private static void DCS_Tuple1()
    {
        Tuple<int> value = new Tuple<int>(1);
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<Tuple<int>>(value, @"<TupleOfint xmlns=""http://schemas.datacontract.org/2004/07/System"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><m_Item1>1</m_Item1></TupleOfint>");
        Assert.StrictEqual<Tuple<int>>(value, deserializedValue);
    }

    private static void DCS_Tuple2()
    {
        Tuple<int, int> value = new Tuple<int, int>(1, 2);
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<Tuple<int, int>>(value, @"<TupleOfintint xmlns=""http://schemas.datacontract.org/2004/07/System"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><m_Item1>1</m_Item1><m_Item2>2</m_Item2></TupleOfintint>");
        Assert.StrictEqual<Tuple<int, int>>(value, deserializedValue);
    }

    private static void DCS_Tuple3()
    {
        Tuple<int, int, int> value = new Tuple<int, int, int>(1, 2, 3);
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<Tuple<int, int, int>>(value, @"<TupleOfintintint xmlns=""http://schemas.datacontract.org/2004/07/System"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><m_Item1>1</m_Item1><m_Item2>2</m_Item2><m_Item3>3</m_Item3></TupleOfintintint>");
        Assert.StrictEqual<Tuple<int, int, int>>(value, deserializedValue);
    }

    private static void DCS_Tuple4()
    {
        Tuple<int, int, int, int> value = new Tuple<int, int, int, int>(1, 2, 3, 4);
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<Tuple<int, int, int, int>>(value, @"<TupleOfintintintint xmlns=""http://schemas.datacontract.org/2004/07/System"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><m_Item1>1</m_Item1><m_Item2>2</m_Item2><m_Item3>3</m_Item3><m_Item4>4</m_Item4></TupleOfintintintint>");
        Assert.StrictEqual<Tuple<int, int, int, int>>(value, deserializedValue);
    }

    private static void DCS_Tuple5()
    {
        Tuple<int, int, int, int, int> value = new Tuple<int, int, int, int, int>(1, 2, 3, 4, 5);
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<Tuple<int, int, int, int, int>>(value, @"<TupleOfintintintintint xmlns=""http://schemas.datacontract.org/2004/07/System"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><m_Item1>1</m_Item1><m_Item2>2</m_Item2><m_Item3>3</m_Item3><m_Item4>4</m_Item4><m_Item5>5</m_Item5></TupleOfintintintintint>");
        Assert.StrictEqual<Tuple<int, int, int, int, int>>(value, deserializedValue);
    }

    private static void DCS_Tuple6()
    {
        Tuple<int, int, int, int, int, int> value = new Tuple<int, int, int, int, int, int>(1, 2, 3, 4, 5, 6);
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<Tuple<int, int, int, int, int, int>>(value, @"<TupleOfintintintintintint xmlns=""http://schemas.datacontract.org/2004/07/System"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><m_Item1>1</m_Item1><m_Item2>2</m_Item2><m_Item3>3</m_Item3><m_Item4>4</m_Item4><m_Item5>5</m_Item5><m_Item6>6</m_Item6></TupleOfintintintintintint>");
        Assert.StrictEqual<Tuple<int, int, int, int, int, int>>(value, deserializedValue);
    }

    private static void DCS_Tuple7()
    {
        Tuple<int, int, int, int, int, int, int> value = new Tuple<int, int, int, int, int, int, int>(1, 2, 3, 4, 5, 6, 7);
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<Tuple<int, int, int, int, int, int, int>>(value, @"<TupleOfintintintintintintint xmlns=""http://schemas.datacontract.org/2004/07/System"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><m_Item1>1</m_Item1><m_Item2>2</m_Item2><m_Item3>3</m_Item3><m_Item4>4</m_Item4><m_Item5>5</m_Item5><m_Item6>6</m_Item6><m_Item7>7</m_Item7></TupleOfintintintintintintint>");
        Assert.StrictEqual<Tuple<int, int, int, int, int, int, int>>(value, deserializedValue);
    }

    private static void DCS_Tuple8()
    {
        Tuple<int, int, int, int, int, int, int, Tuple<int>> value = new Tuple<int, int, int, int, int, int, int, Tuple<int>>(1, 2, 3, 4, 5, 6, 7, new Tuple<int>(8));
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<Tuple<int, int, int, int, int, int, int, Tuple<int>>>(value, @"<TupleOfintintintintintintintTupleOfintcd6ORBnm xmlns=""http://schemas.datacontract.org/2004/07/System"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><m_Item1>1</m_Item1><m_Item2>2</m_Item2><m_Item3>3</m_Item3><m_Item4>4</m_Item4><m_Item5>5</m_Item5><m_Item6>6</m_Item6><m_Item7>7</m_Item7><m_Rest><m_Item1>8</m_Item1></m_Rest></TupleOfintintintintintintintTupleOfintcd6ORBnm>");
        Assert.StrictEqual<Tuple<int, int, int, int, int, int, int, Tuple<int>>>(value, deserializedValue);
    }

    [Fact]
    public static void DCS_GenericQueue()
    {
        Queue<int> value = new Queue<int>();
        value.Enqueue(1);
        object syncRoot = ((ICollection)value).SyncRoot;
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<Queue<int>>(value, @"<QueueOfint xmlns=""http://schemas.datacontract.org/2004/07/System.Collections.Generic"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_array xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:int>1</a:int><a:int>0</a:int><a:int>0</a:int><a:int>0</a:int></_array><_head>0</_head><_size>1</_size><_tail>1</_tail><_version>2</_version></QueueOfint>");
        var a1 = value.ToArray();
        var a2 = deserializedValue.ToArray();
        Assert.StrictEqual(a1.Length, a2.Length);
        Assert.StrictEqual(a1[0], a2[0]);
    }

    [Fact]
    public static void DCS_GenericStack()
    {
        var value = new Stack<int>();
        value.Push(123);
        value.Push(456);
        object syncRoot = ((ICollection)value).SyncRoot;
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<Stack<int>>(value, @"<StackOfint xmlns=""http://schemas.datacontract.org/2004/07/System.Collections.Generic"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_array xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:int>123</a:int><a:int>456</a:int><a:int>0</a:int><a:int>0</a:int></_array><_size>2</_size><_version>2</_version></StackOfint>");
        var a1 = value.ToArray();
        var a2 = deserializedValue.ToArray();
        Assert.StrictEqual(a1.Length, a2.Length);
        Assert.StrictEqual(a1[0], a2[0]);
        Assert.StrictEqual(a1[1], a2[1]);
    }

    [Fact]
    public static void DCS_Queue()
    {
        var value = new Queue();
        value.Enqueue(123);
        value.Enqueue("Foo");
        object syncRoot = ((ICollection)value).SyncRoot;
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<Queue>(value, @"<Queue xmlns=""http://schemas.datacontract.org/2004/07/System.Collections"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_array xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:int"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">123</a:anyType><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">Foo</a:anyType><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/></_array><_growFactor>200</_growFactor><_head>0</_head><_size>2</_size><_tail>2</_tail><_version>2</_version></Queue>");
        var a1 = value.ToArray();
        var a2 = deserializedValue.ToArray();
        Assert.StrictEqual(a1.Length, a2.Length);
        Assert.StrictEqual(a1[0], a2[0]);
    }

    [Fact]
    public static void DCS_Stack()
    {
        var value = new Stack();
        value.Push(123);
        value.Push("Foo");
        object syncRoot = ((ICollection)value).SyncRoot;
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<Stack>(value, @"<Stack xmlns=""http://schemas.datacontract.org/2004/07/System.Collections"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_array xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""b:int"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">123</a:anyType><a:anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">Foo</a:anyType><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/><a:anyType i:nil=""true""/></_array><_size>2</_size><_version>2</_version></Stack>");
        var a1 = value.ToArray();
        var a2 = deserializedValue.ToArray();
        Assert.StrictEqual(a1.Length, a2.Length);
        Assert.StrictEqual(a1[0], a2[0]);
        Assert.StrictEqual(a1[1], a2[1]);
    }

    [Fact]
    public static void DCS_SortedList()
    {
        var value = new SortedList();
        value.Add(456, "Foo");
        value.Add(123, "Bar");
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<SortedList>(value, @"<ArrayOfKeyValueOfanyTypeanyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><KeyValueOfanyTypeanyType><Key i:type=""a:int"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">123</Key><Value i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">Bar</Value></KeyValueOfanyTypeanyType><KeyValueOfanyTypeanyType><Key i:type=""a:int"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">456</Key><Value i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">Foo</Value></KeyValueOfanyTypeanyType></ArrayOfKeyValueOfanyTypeanyType>");
        Assert.StrictEqual(value.Count, deserializedValue.Count);
        Assert.StrictEqual(value[0], deserializedValue[0]);
        Assert.StrictEqual(value[1], deserializedValue[1]);
    }

    [Fact]
    public static void DCS_SystemVersion()
    {
        Version value = new Version(1, 2, 3, 4);
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<Version>(value,
            @"<Version xmlns=""http://schemas.datacontract.org/2004/07/System"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_Build>3</_Build><_Major>1</_Major><_Minor>2</_Minor><_Revision>4</_Revision></Version>");
        Assert.StrictEqual(value.Major, deserializedValue.Major);
        Assert.StrictEqual(value.Minor, deserializedValue.Minor);
        Assert.StrictEqual(value.Build, deserializedValue.Build);
        Assert.StrictEqual(value.Revision, deserializedValue.Revision);
    }

    [Fact]
    public static void DCS_TypeWithCommonTypeProperties()
    {
        TypeWithCommonTypeProperties value = new TypeWithCommonTypeProperties { Ts = new TimeSpan(1, 1, 1), Id = new Guid("ad948f1e-9ba9-44c8-8e2e-b6ba969ec987") };
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithCommonTypeProperties>(value, @"<TypeWithCommonTypeProperties xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Id>ad948f1e-9ba9-44c8-8e2e-b6ba969ec987</Id><Ts>PT1H1M1S</Ts></TypeWithCommonTypeProperties>");
        Assert.StrictEqual<TypeWithCommonTypeProperties>(value, deserializedValue);
    }

    [Fact]
    public static void DCS_TypeWithTypeProperty()
    {
        TypeWithTypeProperty value = new TypeWithTypeProperty { Id = 123, Name = "Jon Doe" };
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithTypeProperty>(value, @"<TypeWithTypeProperty xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Id>123</Id><Name>Jon Doe</Name><Type i:nil=""true"" xmlns:a=""http://schemas.datacontract.org/2004/07/System""/></TypeWithTypeProperty>");
        Assert.StrictEqual(value.Id, deserializedValue.Id);
        Assert.Equal(value.Name, deserializedValue.Name);
        Assert.StrictEqual(value.Type, deserializedValue.Type);
    }

    [Fact]
    public static void DCS_TypeWithExplicitIEnumerableImplementation()
    {
        TypeWithExplicitIEnumerableImplementation value = new TypeWithExplicitIEnumerableImplementation { };
        value.Add("Foo");
        value.Add("Bar");
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithExplicitIEnumerableImplementation>(value, @"<ArrayOfanyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><anyType i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">Foo</anyType><anyType i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">Bar</anyType></ArrayOfanyType>");
        Assert.StrictEqual(2, deserializedValue.Count);
        IEnumerator enumerator = ((IEnumerable)deserializedValue).GetEnumerator();
        enumerator.MoveNext();
        Assert.Equal("Foo", (string)enumerator.Current);
        enumerator.MoveNext();
        Assert.Equal("Bar", (string)enumerator.Current);
    }

    [Fact]
    public static void DCS_TypeWithGenericDictionaryAsKnownType()
    {
        TypeWithGenericDictionaryAsKnownType value = new TypeWithGenericDictionaryAsKnownType { };
        value.Foo.Add(10, new Level() { Name = "Foo", LevelNo = 1 });
        value.Foo.Add(20, new Level() { Name = "Bar", LevelNo = 2 });
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithGenericDictionaryAsKnownType>(value, @"<TypeWithGenericDictionaryAsKnownType xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Foo xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfintLevelQk4Xq8_SP><a:Key>10</a:Key><a:Value><LevelNo>1</LevelNo><Name>Foo</Name></a:Value></a:KeyValueOfintLevelQk4Xq8_SP><a:KeyValueOfintLevelQk4Xq8_SP><a:Key>20</a:Key><a:Value><LevelNo>2</LevelNo><Name>Bar</Name></a:Value></a:KeyValueOfintLevelQk4Xq8_SP></Foo></TypeWithGenericDictionaryAsKnownType>");

        Assert.StrictEqual(2, deserializedValue.Foo.Count);
        Assert.Equal("Foo", deserializedValue.Foo[10].Name);
        Assert.StrictEqual(1, deserializedValue.Foo[10].LevelNo);
        Assert.Equal("Bar", deserializedValue.Foo[20].Name);
        Assert.StrictEqual(2, deserializedValue.Foo[20].LevelNo);
    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/60462", TestPlatforms.iOS | TestPlatforms.tvOS)]
    public static void DCS_TypeWithKnownTypeAttributeAndInterfaceMember()
    {
        TypeWithKnownTypeAttributeAndInterfaceMember value = new TypeWithKnownTypeAttributeAndInterfaceMember();
        value.HeadLine = new NewsArticle() { Title = "Foo News" };
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithKnownTypeAttributeAndInterfaceMember>(value, @"<TypeWithKnownTypeAttributeAndInterfaceMember xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><HeadLine i:type=""NewsArticle""><Category>News</Category><Title>Foo News</Title></HeadLine></TypeWithKnownTypeAttributeAndInterfaceMember>");

        Assert.Equal("News", deserializedValue.HeadLine.Category);
        Assert.Equal("Foo News", deserializedValue.HeadLine.Title);
    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/60462", TestPlatforms.iOS | TestPlatforms.tvOS)]
    public static void DCS_TypeWithKnownTypeAttributeAndListOfInterfaceMember()
    {
        TypeWithKnownTypeAttributeAndListOfInterfaceMember value = new TypeWithKnownTypeAttributeAndListOfInterfaceMember();
        value.Articles = new List<IArticle>() { new SummaryArticle() { Title = "Bar Summary" } };
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithKnownTypeAttributeAndListOfInterfaceMember>(value, @"<TypeWithKnownTypeAttributeAndListOfInterfaceMember xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Articles xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType i:type=""SummaryArticle""><Category>Summary</Category><Title>Bar Summary</Title></a:anyType></Articles></TypeWithKnownTypeAttributeAndListOfInterfaceMember>");

        Assert.StrictEqual(1, deserializedValue.Articles.Count);
        Assert.Equal("Summary", deserializedValue.Articles[0].Category);
        Assert.Equal("Bar Summary", deserializedValue.Articles[0].Title);
    }

    /*
     * Begin tests of the InvalidDataContract generated for illegal types
     */

    [Fact]
    public static void DCS_InvalidDataContract_Write_And_Read_Empty_Collection_Of_Invalid_Type_Succeeds()
    {
        // Collections of invalid types can be serialized and deserialized if they are empty.
        // This is consistent with .Net
        List<Invalid_Class_No_Parameterless_Ctor> list = new List<Invalid_Class_No_Parameterless_Ctor>();
        MemoryStream ms = new MemoryStream();
        DataContractSerializer dcs = new DataContractSerializer(list.GetType());
        dcs.WriteObject(ms, list);
        ms.Seek(0L, SeekOrigin.Begin);
        List<Invalid_Class_No_Parameterless_Ctor> list2 = (List<Invalid_Class_No_Parameterless_Ctor>)dcs.ReadObject(ms);
        Assert.True(list2.Count == 0, string.Format("Unexpected length {0}", list.Count));
    }

    [Fact]
    public static void DCS_InvalidDataContract_Write_NonEmpty_Collection_Of_Invalid_Type_Throws()
    {
        // Non-empty collections of invalid types throw
        // This is consistent with .Net
        Invalid_Class_No_Parameterless_Ctor c = new Invalid_Class_No_Parameterless_Ctor("test");
        List<Invalid_Class_No_Parameterless_Ctor> list = new List<Invalid_Class_No_Parameterless_Ctor>();
        list.Add(c);
        DataContractSerializer dcs = new DataContractSerializer(list.GetType());

        MemoryStream ms = new MemoryStream();
        Assert.Throws<InvalidDataContractException>(() =>
        {
            dcs.WriteObject(ms, c);
        });
    }

    /*
     * End tests of the InvalidDataContract generated for illegal types
     */

    [Fact]
    public static void DCS_DerivedTypeWithBaseTypeWithDataMember()
    {
        DerivedTypeWithDataMemberInBaseType value = new DerivedTypeWithDataMemberInBaseType() { EmbeddedDataMember = new TypeAsEmbeddedDataMember { Name = "Foo" } };
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<DerivedTypeWithDataMemberInBaseType>(value, @"<DerivedTypeWithDataMemberInBaseType xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><EmbeddedDataMember><Name>Foo</Name></EmbeddedDataMember></DerivedTypeWithDataMemberInBaseType>");

        Assert.Equal("Foo", deserializedValue.EmbeddedDataMember.Name);
    }

    [Fact]
    public static void DCS_PocoDerivedTypeWithBaseTypeWithDataMember()
    {
        PocoDerivedTypeWithDataMemberInBaseType value = new PocoDerivedTypeWithDataMemberInBaseType() { EmbeddedDataMember = new PocoTypeAsEmbeddedDataMember { Name = "Foo" } };
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<PocoDerivedTypeWithDataMemberInBaseType>(value, @"<PocoDerivedTypeWithDataMemberInBaseType xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><EmbeddedDataMember><Name>Foo</Name></EmbeddedDataMember></PocoDerivedTypeWithDataMemberInBaseType>");

        Assert.Equal("Foo", deserializedValue.EmbeddedDataMember.Name);
    }

    [Fact]
    public static void DCS_ClassImplementingIXmlSerializable()
    {
        ClassImplementingIXmlSerializable value = new ClassImplementingIXmlSerializable() { StringValue = "Foo" };
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<ClassImplementingIXmlSerializable>(value, @"<ClassImplementingIXmlSerializable StringValue=""Foo"" BoolValue=""True"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes""/>");
        Assert.Equal(value.StringValue, deserializedValue.StringValue);
    }

    [Fact]
    public static void DCS_TypeWithNestedGenericClassImplementingIXmlSerializable()
    {
        TypeWithNestedGenericClassImplementingIXmlSerializable.NestedGenericClassImplementingIXmlSerializable<bool> value = new TypeWithNestedGenericClassImplementingIXmlSerializable.NestedGenericClassImplementingIXmlSerializable<bool>() { StringValue = "Foo" };
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithNestedGenericClassImplementingIXmlSerializable.NestedGenericClassImplementingIXmlSerializable<bool>>(value, @"<TypeWithNestedGenericClassImplementingIXmlSerializable.NestedGenericClassImplementingIXmlSerializableOfbooleanRvdAXEcW StringValue=""Foo"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes""/>");
        Assert.Equal(value.StringValue, deserializedValue.StringValue);
    }

    [Fact]
    public static void DCS_GenericTypeWithNestedGenerics()
    {
        GenericTypeWithNestedGenerics<int>.InnerGeneric<double> value = new GenericTypeWithNestedGenerics<int>.InnerGeneric<double>()
        {
            data1 = 123,
            data2 = 4.56
        };
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<GenericTypeWithNestedGenerics<int>.InnerGeneric<double>>(value, @"<GenericTypeWithNestedGenerics.InnerGenericOfintdouble2LMUf4bh xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><data1>123</data1><data2>4.56</data2></GenericTypeWithNestedGenerics.InnerGenericOfintdouble2LMUf4bh>");
        Assert.StrictEqual(value.data1, deserializedValue.data1);
        Assert.StrictEqual(value.data2, deserializedValue.data2);
    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/60462", TestPlatforms.iOS | TestPlatforms.tvOS)]
    public static void DCS_DuplicatedKeyDateTimeOffset()
    {
        DateTimeOffset value = new DateTimeOffset(new DateTime(2013, 1, 2, 3, 4, 5, 6, DateTimeKind.Utc).AddMinutes(7));
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<DateTimeOffset>(value, @"<DateTimeOffset xmlns=""http://schemas.datacontract.org/2004/07/System"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><DateTime>2013-01-02T03:11:05.006Z</DateTime><OffsetMinutes>0</OffsetMinutes></DateTimeOffset>");

        DataContractJsonSerializer dcjs = new DataContractJsonSerializer(typeof(DateTimeOffset));
        MemoryStream stream = new MemoryStream();
        dcjs.WriteObject(stream, value);
    }

    [Fact]
    public static void DCS_DuplicatedKeyXmlQualifiedName()
    {
        XmlQualifiedName qname = new XmlQualifiedName("abc", "def");
        TypeWithXmlQualifiedName value = new TypeWithXmlQualifiedName() { Value = qname };
        TypeWithXmlQualifiedName deserialized = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithXmlQualifiedName>(value, @"<TypeWithXmlQualifiedName xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><q:Value xmlns:q=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:a=""def"">a:abc</q:Value></TypeWithXmlQualifiedName>");
        Assert.StrictEqual(value.Value, deserialized.Value);
    }

    [Fact]
    public static void DCS_DeserializeTypeWithInnerInvalidDataContract()
    {
        DataContractSerializer dcs = new DataContractSerializer(typeof(TypeWithPropertyWithoutDefaultCtor));
        string xmlString = @"<TypeWithPropertyWithoutDefaultCtor xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Name>Foo</Name></TypeWithPropertyWithoutDefaultCtor>";
        MemoryStream ms = new MemoryStream();
        StreamWriter sw = new StreamWriter(ms);
        sw.Write(xmlString);
        sw.Flush();
        ms.Seek(0, SeekOrigin.Begin);

        TypeWithPropertyWithoutDefaultCtor deserializedValue = (TypeWithPropertyWithoutDefaultCtor)dcs.ReadObject(ms);
        Assert.Equal("Foo", deserializedValue.Name);
        Assert.Null(deserializedValue.MemberWithInvalidDataContract);
    }

    [Fact]
    public static void DCS_ReadOnlyCollection()
    {
        List<string> list = new List<string>() { "Foo", "Bar" };
        ReadOnlyCollection<string> value = new ReadOnlyCollection<string>(list);
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<ReadOnlyCollection<string>>(value, @"<ReadOnlyCollectionOfstring xmlns=""http://schemas.datacontract.org/2004/07/System.Collections.ObjectModel"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><list xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>Foo</a:string><a:string>Bar</a:string></list></ReadOnlyCollectionOfstring>");
        Assert.StrictEqual(value.Count, deserializedValue.Count);
        Assert.Equal(value[0], deserializedValue[0]);
        Assert.Equal(value[1], deserializedValue[1]);
    }

    [Fact]
    public static void DCS_ReadOnlyDictionary()
    {
        var dict = new Dictionary<string, int>();
        dict["Foo"] = 1;
        dict["Bar"] = 2;
        ReadOnlyDictionary<string, int> value = new ReadOnlyDictionary<string, int>(dict);
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ReadOnlyDictionaryOfstringint xmlns=""http://schemas.datacontract.org/2004/07/System.Collections.ObjectModel"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><m_dictionary xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfstringint><a:Key>Foo</a:Key><a:Value>1</a:Value></a:KeyValueOfstringint><a:KeyValueOfstringint><a:Key>Bar</a:Key><a:Value>2</a:Value></a:KeyValueOfstringint></m_dictionary></ReadOnlyDictionaryOfstringint>");

        Assert.StrictEqual(value.Count, deserializedValue.Count);
        Assert.StrictEqual(value["Foo"], deserializedValue["Foo"]);
        Assert.StrictEqual(value["Bar"], deserializedValue["Bar"]);
    }

    [Fact]
    public static void DCS_KeyValuePair()
    {
        var value = new KeyValuePair<string, object>("FooKey", "FooValue");
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<KeyValuePair<string, object>>(value, @"<KeyValuePairOfstringanyType xmlns=""http://schemas.datacontract.org/2004/07/System.Collections.Generic"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><key>FooKey</key><value i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">FooValue</value></KeyValuePairOfstringanyType>");

        Assert.Equal(value.Key, deserializedValue.Key);
        Assert.StrictEqual(value.Value, deserializedValue.Value);
    }

    [Fact]
    public static void DCS_ConcurrentDictionary()
    {
        var value = new ConcurrentDictionary<string, int>();
        value["one"] = 1;
        value["two"] = 2;
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<ConcurrentDictionary<string, int>>(value, @"<ArrayOfKeyValueOfstringint xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><KeyValueOfstringint><Key>one</Key><Value>1</Value></KeyValueOfstringint><KeyValueOfstringint><Key>two</Key><Value>2</Value></KeyValueOfstringint></ArrayOfKeyValueOfstringint>", null, null, true);

        Assert.NotNull(deserializedValue);
        Assert.True(deserializedValue.Count == 2);
        Assert.True(deserializedValue["one"] == 1);
        Assert.True(deserializedValue["two"] == 2);
    }

    [Fact]
    public static void DCS_DataContractWithDotInName()
    {
        DataContractWithDotInName value = new DataContractWithDotInName() { Name = "Foo" };
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<DataContractWithDotInName>(value, @"<DCWith.InName xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Name>Foo</Name></DCWith.InName>");

        Assert.NotNull(deserializedValue);
        Assert.Equal(value.Name, deserializedValue.Name);
    }

    [Fact]
    public static void DCS_DataContractWithMinusSignInName()
    {
        DataContractWithMinusSignInName value = new DataContractWithMinusSignInName() { Name = "Foo" };
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<DataContractWithMinusSignInName>(value, @"<DCWith-InName xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Name>Foo</Name></DCWith-InName>");

        Assert.NotNull(deserializedValue);
        Assert.Equal(value.Name, deserializedValue.Name);
    }

    [Fact]
    public static void DCS_DataContractWithOperatorsInName()
    {
        DataContractWithOperatorsInName value = new DataContractWithOperatorsInName() { Name = "Foo" };
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<DataContractWithOperatorsInName>(value, @"<DCWith_x007B__x007D__x005B__x005D__x0028__x0029_._x002C__x003A__x003B__x002B_-_x002A__x002F__x0025__x0026__x007C__x005E__x0021__x007E__x003D__x003C__x003E__x003F__x002B__x002B_--_x0026__x0026__x007C__x007C__x003C__x003C__x003E__x003E__x003D__x003D__x0021__x003D__x003C__x003D__x003E__x003D__x002B__x003D_-_x003D__x002A__x003D__x002F__x003D__x0025__x003D__x0026__x003D__x007C__x003D__x005E__x003D__x003C__x003C__x003D__x003E__x003E__x003D_-_x003E_InName xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Name>Foo</Name></DCWith_x007B__x007D__x005B__x005D__x0028__x0029_._x002C__x003A__x003B__x002B_-_x002A__x002F__x0025__x0026__x007C__x005E__x0021__x007E__x003D__x003C__x003E__x003F__x002B__x002B_--_x0026__x0026__x007C__x007C__x003C__x003C__x003E__x003E__x003D__x003D__x0021__x003D__x003C__x003D__x003E__x003D__x002B__x003D_-_x003D__x002A__x003D__x002F__x003D__x0025__x003D__x0026__x003D__x007C__x003D__x005E__x003D__x003C__x003C__x003D__x003E__x003E__x003D_-_x003E_InName>");

        Assert.NotNull(deserializedValue);
        Assert.Equal(value.Name, deserializedValue.Name);
    }

    [Fact]
    public static void DCS_DataContractWithOtherSymbolsInName()
    {
        DataContractWithOtherSymbolsInName value = new DataContractWithOtherSymbolsInName() { Name = "Foo" };
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<DataContractWithOtherSymbolsInName>(value, @"<DCWith_x0060__x0040__x0023__x0024__x0027__x0022__x0020__x0009_InName xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Name>Foo</Name></DCWith_x0060__x0040__x0023__x0024__x0027__x0022__x0020__x0009_InName>");

        Assert.NotNull(deserializedValue);
        Assert.Equal(value.Name, deserializedValue.Name);
    }

    [Fact]
    public static void DCS_CollectionDataContractWithCustomKeyName()
    {
        CollectionDataContractWithCustomKeyName value = new CollectionDataContractWithCustomKeyName();
        value.Add(100, 123);
        value.Add(200, 456);
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<CollectionDataContractWithCustomKeyName>(value, @"<MyHeaders xmlns=""http://schemas.microsoft.com/netservices/2010/10/servicebus/connect"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><MyHeader><MyKey>100</MyKey><MyValue>123</MyValue></MyHeader><MyHeader><MyKey>200</MyKey><MyValue>456</MyValue></MyHeader></MyHeaders>");

        Assert.NotNull(deserializedValue);
        Assert.StrictEqual(value[100], deserializedValue[100]);
        Assert.StrictEqual(value[200], deserializedValue[200]);
    }

    [Fact]
    public static void DCS_CollectionDataContractWithCustomKeyNameDuplicate()
    {
        CollectionDataContractWithCustomKeyNameDuplicate value = new CollectionDataContractWithCustomKeyNameDuplicate();
        value.Add(100, 123);
        value.Add(200, 456);
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<CollectionDataContractWithCustomKeyNameDuplicate>(value, @"<MyHeaders2 xmlns=""http://schemas.microsoft.com/netservices/2010/10/servicebus/connect"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><MyHeader2><MyKey2>100</MyKey2><MyValue2>123</MyValue2></MyHeader2><MyHeader2><MyKey2>200</MyKey2><MyValue2>456</MyValue2></MyHeader2></MyHeaders2>");

        Assert.NotNull(deserializedValue);
        Assert.StrictEqual(value[100], deserializedValue[100]);
        Assert.StrictEqual(value[200], deserializedValue[200]);
    }

    [Fact]
    public static void DCS_TypeWithCollectionWithoutDefaultConstructor()
    {
        TypeWithCollectionWithoutDefaultConstructor value = new TypeWithCollectionWithoutDefaultConstructor();
        value.CollectionProperty.Add("Foo");
        value.CollectionProperty.Add("Bar");
        var deserializedValue = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithCollectionWithoutDefaultConstructor>(value, @"<TypeWithCollectionWithoutDefaultConstructor xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><CollectionProperty xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>Foo</a:string><a:string>Bar</a:string></CollectionProperty></TypeWithCollectionWithoutDefaultConstructor>");

        Assert.NotNull(deserializedValue);
        Assert.NotNull(deserializedValue.CollectionProperty);
        Assert.StrictEqual(value.CollectionProperty.Count, deserializedValue.CollectionProperty.Count);
        Assert.True(Enumerable.SequenceEqual(value.CollectionProperty, deserializedValue.CollectionProperty));
    }

    [Fact]
    public static void DCS_DeserializeEmptyString()
    {
        var serializer = new DataContractSerializer(typeof(object));
        bool exceptionThrown = false;
        try
        {
            serializer.ReadObject(new MemoryStream());
        }
        catch (Exception e)
        {
            Type expectedExceptionType = typeof(XmlException);
            Type actualExceptionType = e.GetType();
            if (!actualExceptionType.Equals(expectedExceptionType))
            {
                var messageBuilder = new StringBuilder();
                messageBuilder.AppendLine("The actual exception was not of the expected type.");
                messageBuilder.AppendLine($"Expected exception type: {expectedExceptionType.FullName}, {expectedExceptionType.GetTypeInfo().Assembly.FullName}");
                messageBuilder.AppendLine($"Actual exception type: {actualExceptionType.FullName}, {actualExceptionType.GetTypeInfo().Assembly.FullName}");
                messageBuilder.AppendLine($"The type of {nameof(expectedExceptionType)} was: {expectedExceptionType.GetType()}");
                messageBuilder.AppendLine($"The type of {nameof(actualExceptionType)} was: {actualExceptionType.GetType()}");
                Assert.Fail(messageBuilder.ToString());
            }

            exceptionThrown = true;
        }

        Assert.True(exceptionThrown, "An expected exception was not thrown.");
    }

    [Theory]
    [MemberData(nameof(XmlDictionaryReaderQuotasData))]
    public static void DCS_XmlDictionaryQuotas(XmlDictionaryReaderQuotas quotas, bool shouldSucceed)
    {
        var input = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><TypeWithTypeWithIntAndStringPropertyProperty><ObjectProperty><SampleInt>10</SampleInt><SampleString>Sample string</SampleString></ObjectProperty></TypeWithTypeWithIntAndStringPropertyProperty>";
        var content = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using (var reader = XmlDictionaryReader.CreateTextReader(content, Encoding.UTF8, quotas, onClose: null))
        {
            var serializer = new DataContractSerializer(typeof(TypeWithTypeWithIntAndStringPropertyProperty), new DataContractSerializerSettings());
            if (shouldSucceed)
            {
                var deserializedObject = (TypeWithTypeWithIntAndStringPropertyProperty)serializer.ReadObject(reader);
                Assert.StrictEqual(10, deserializedObject.ObjectProperty.SampleInt);
                Assert.Equal("Sample string", deserializedObject.ObjectProperty.SampleString);
            }
            else
            {
                Assert.Throws<SerializationException>(() => { serializer.ReadObject(reader); });
            }
        }
    }

    public static IEnumerable<object[]> XmlDictionaryReaderQuotasData
    {
        get
        {
            return new[]
            {
                new object[] { new XmlDictionaryReaderQuotas(), true },
                new object[] { new XmlDictionaryReaderQuotas() { MaxDepth = 1}, false },
                new object[] { new XmlDictionaryReaderQuotas() { MaxStringContentLength = 1}, false }
            };
        }
    }

    [Fact]
    public static void DCS_CollectionInterfaceGetOnlyCollection()
    {
        var obj = new TypeWithCollectionInterfaceGetOnlyCollection(new List<string>() { "item1", "item2", "item3" });
        var deserializedObj = DataContractSerializerHelper.SerializeAndDeserialize(obj, @"<TypeWithCollectionInterfaceGetOnlyCollection xmlns=""http://schemas.datacontract.org/2004/07/"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Items xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>item1</a:string><a:string>item2</a:string><a:string>item3</a:string></Items></TypeWithCollectionInterfaceGetOnlyCollection>");
        Assert.Equal(obj.Items, deserializedObj.Items);
    }

    [Fact]
    public static void DCS_EnumerableInterfaceGetOnlyCollection()
    {
        // Expect exception in deserialization process
        Assert.Throws<InvalidDataContractException>(() => {
            var obj = new TypeWithEnumerableInterfaceGetOnlyCollection(new List<string>() { "item1", "item2", "item3" });
            DataContractSerializerHelper.SerializeAndDeserialize(obj, @"<TypeWithEnumerableInterfaceGetOnlyCollection xmlns=""http://schemas.datacontract.org/2004/07/"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Items xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:string>item1</a:string><a:string>item2</a:string><a:string>item3</a:string></Items></TypeWithEnumerableInterfaceGetOnlyCollection>");
        });
    }

    [Fact]
    public static void DCS_RecursiveCollection()
    {
        Assert.Throws<InvalidDataContractException>(() =>
        {
            (new DataContractSerializer(typeof (RecursiveCollection))).WriteObject(new MemoryStream(), new RecursiveCollection());
        });
    }

    [Fact]
    public static void DCS_XmlElementAsRoot()
    {
        XmlDocument xDoc = new XmlDocument();
        xDoc.LoadXml(@"<html></html>");
        XmlElement expected = xDoc.CreateElement("Element");
        expected.InnerText = "Element innertext";
        var actual = DataContractSerializerHelper.SerializeAndDeserialize(expected,
@"<Element>Element innertext</Element>");
        Assert.NotNull(actual);
        Assert.Equal(expected.InnerText, actual.InnerText);
    }

    [Fact]
    public static void DCS_TypeWithXmlElementProperty()
    {
        XmlDocument xDoc = new XmlDocument();
        xDoc.LoadXml(@"<html></html>");
        XmlElement productElement = xDoc.CreateElement("Product");
        productElement.InnerText = "Product innertext";
        XmlElement categoryElement = xDoc.CreateElement("Category");
        categoryElement.InnerText = "Category innertext";
        var expected = new TypeWithXmlElementProperty() { Elements = new[] { productElement, categoryElement } };
        var actual = DataContractSerializerHelper.SerializeAndDeserialize(expected,
@"<TypeWithXmlElementProperty xmlns=""http://schemas.datacontract.org/2004/07/"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Elements xmlns:a=""http://schemas.datacontract.org/2004/07/System.Xml""><a:XmlElement><Product xmlns="""">Product innertext</Product></a:XmlElement><a:XmlElement><Category xmlns="""">Category innertext</Category></a:XmlElement></Elements></TypeWithXmlElementProperty>");
        Assert.StrictEqual(expected.Elements.Length, actual.Elements.Length);
        for (int i = 0; i < expected.Elements.Length; ++i)
        {
            Assert.Equal(expected.Elements[i].InnerText, actual.Elements[i].InnerText);
        }
    }

    [Fact]
    public static void DCS_ArrayOfSimpleType_PreserveObjectReferences_True()
    {
        var x = new SimpleType[3];
        var simpleObject1 = new SimpleType() { P1 = "simpleObject1", P2 = 1 };
        var simpleObject2 = new SimpleType() { P1 = "simpleObject2", P2 = 2 };
        x[0] = simpleObject1;
        x[1] = simpleObject1;
        x[2] = simpleObject2;

        var settings = new DataContractSerializerSettings
        {
            PreserveObjectReferences = true,
        };

        var y = DataContractSerializerHelper.SerializeAndDeserialize(x,
            baseline: "<ArrayOfSimpleType z:Id=\"1\" z:Size=\"3\" xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"><SimpleType z:Id=\"2\"><P1 z:Id=\"3\">simpleObject1</P1><P2>1</P2></SimpleType><SimpleType z:Ref=\"2\" i:nil=\"true\"/><SimpleType z:Id=\"4\"><P1 z:Id=\"5\">simpleObject2</P1><P2>2</P2></SimpleType></ArrayOfSimpleType>",
            settings: settings);

        Assert.True(x.Length == y.Length, "x.Length != y.Length");
        Assert.True(x[0].P1 == y[0].P1, "x[0].P1 != y[0].P1");
        Assert.True(x[0].P2 == y[0].P2, "x[0].P2 != y[0].P2");
        Assert.True(y[0] == y[1], "y[0] and y[1] should point to the same object, but they pointed to different objects.");

        Assert.True(x[2].P1 == y[2].P1, "x[2].P1 != y[2].P1");
        Assert.True(x[2].P2 == y[2].P2, "x[2].P2 != y[2].P2");
    }

    [Fact]
    public static void DCS_ArrayOfSimpleType_PreserveObjectReferences_False()
    {
        var x = new SimpleType[3];
        var simpleObject1 = new SimpleType() { P1 = "simpleObject1", P2 = 1 };
        var simpleObject2 = new SimpleType() { P1 = "simpleObject2", P2 = 2 };
        x[0] = simpleObject1;
        x[1] = simpleObject1;
        x[2] = simpleObject2;

        var settings = new DataContractSerializerSettings
        {
            PreserveObjectReferences = false,
        };

        var y = DataContractSerializerHelper.SerializeAndDeserialize(x,
            baseline: "<ArrayOfSimpleType xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><SimpleType><P1>simpleObject1</P1><P2>1</P2></SimpleType><SimpleType><P1>simpleObject1</P1><P2>1</P2></SimpleType><SimpleType><P1>simpleObject2</P1><P2>2</P2></SimpleType></ArrayOfSimpleType>",
            settings: settings);

        Assert.True(x.Length == y.Length, "x.Length != y.Length");
        Assert.True(x[0].P1 == y[0].P1, "x[0].P1 != y[0].P1");
        Assert.True(x[0].P2 == y[0].P2, "x[0].P2 != y[0].P2");
        Assert.True(x[1].P1 == y[1].P1, "x[1].P1 != y[1].P1");
        Assert.True(x[1].P2 == y[1].P2, "x[1].P2 != y[1].P2");
        Assert.True(y[0] != y[1], "y[0] and y[1] should point to different objects, but they pointed to the same object.");

        Assert.True(x[2].P1 == y[2].P1, "x[2].P1 != y[2].P1");
        Assert.True(x[2].P2 == y[2].P2, "x[2].P2 != y[2].P2");
    }

    [Fact]
    public static void DCS_CircularTypes_PreserveObjectReferences_True()
    {
        var root = new TypeWithListOfReferenceChildren();
        var typeOfReferenceChildA = new TypeOfReferenceChild { Root = root, Name = "A" };
        var typeOfReferenceChildB = new TypeOfReferenceChild { Root = root, Name = "B" };
        root.Children = new List<TypeOfReferenceChild> {
                typeOfReferenceChildA,
                typeOfReferenceChildB,
                typeOfReferenceChildA,
        };

        var settings = new DataContractSerializerSettings
        {
            PreserveObjectReferences = true,
        };

        var root2 = DataContractSerializerHelper.SerializeAndDeserialize(root,
            baseline: "<TypeWithListOfReferenceChildren z:Id=\"1\" xmlns=\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"><Children z:Id=\"2\" z:Size=\"3\"><TypeOfReferenceChild z:Id=\"3\"><Name z:Id=\"4\">A</Name><Root z:Ref=\"1\" i:nil=\"true\"/></TypeOfReferenceChild><TypeOfReferenceChild z:Id=\"5\"><Name z:Id=\"6\">B</Name><Root z:Ref=\"1\" i:nil=\"true\"/></TypeOfReferenceChild><TypeOfReferenceChild z:Ref=\"3\" i:nil=\"true\"/></Children></TypeWithListOfReferenceChildren>",
            settings: settings);

        Assert.True(3 == root2.Children.Count, $"root2.Children.Count was expected to be {2}, but the actual value was {root2.Children.Count}");
        Assert.True(root.Children[0].Name == root2.Children[0].Name, "root.Children[0].Name != root2.Children[0].Name");
        Assert.True(root.Children[1].Name == root2.Children[1].Name, "root.Children[1].Name != root2.Children[1].Name");
        Assert.True(root2 == root2.Children[0].Root, "root2 != root2.Children[0].Root");
        Assert.True(root2 == root2.Children[1].Root, "root2 != root2.Children[1].Root");

        Assert.True(root2.Children[0] == root2.Children[2], "root2.Children[0] != root2.Children[2]");
    }

    [Fact]
    public static void DCS_CircularTypes_PreserveObjectReferences_False()
    {
        var root = new TypeWithListOfReferenceChildren();
        var typeOfReferenceChildA = new TypeOfReferenceChild { Root = root, Name = "A" };
        var typeOfReferenceChildB = new TypeOfReferenceChild { Root = root, Name = "B" };
        root.Children = new List<TypeOfReferenceChild> {
                typeOfReferenceChildA,
                typeOfReferenceChildB,
                typeOfReferenceChildA,
        };

        var settings = new DataContractSerializerSettings
        {
            PreserveObjectReferences = false,
        };

        var root2 = DataContractSerializerHelper.SerializeAndDeserialize(root,
            baseline: "<TypeWithListOfReferenceChildren xmlns=\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><Children><TypeOfReferenceChild z:Id=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"><Name>A</Name><Root><Children><TypeOfReferenceChild z:Ref=\"i1\"/><TypeOfReferenceChild z:Id=\"i2\"><Name>B</Name><Root><Children><TypeOfReferenceChild z:Ref=\"i1\"/><TypeOfReferenceChild z:Ref=\"i2\"/><TypeOfReferenceChild z:Ref=\"i1\"/></Children></Root></TypeOfReferenceChild><TypeOfReferenceChild z:Ref=\"i1\"/></Children></Root></TypeOfReferenceChild><TypeOfReferenceChild z:Ref=\"i2\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/><TypeOfReferenceChild z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></Children></TypeWithListOfReferenceChildren>",
            settings: settings);

        Assert.True(3 == root2.Children.Count, $"root2.Children.Count was expected to be {2}, but the actual value was {root2.Children.Count}");
        Assert.True(root.Children[0].Name == root2.Children[0].Name, "root.Children[0].Name != root2.Children[0].Name");
        Assert.True(root.Children[1].Name == root2.Children[1].Name, "root.Children[1].Name != root2.Children[1].Name");
        Assert.True(root2 != root2.Children[0].Root, "root2 == root2.Children[0].Root");
        Assert.True(root2 != root2.Children[1].Root, "root2 == root2.Children[1].Root");
        Assert.True(root2.Children[0].Root != root2.Children[1].Root, "root2.Children[0].Root == root2.Children[1].Root");
        Assert.True(root2.Children[0] == root2.Children[2], "root2.Children[0] != root2.Children[2]");
    }

    [Fact]
    public static void DCS_TypeWithPrimitiveProperties()
    {
        TypeWithPrimitiveProperties x = new TypeWithPrimitiveProperties { P1 = "abc", P2 = 11 };
        TypeWithPrimitiveProperties y = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithPrimitiveProperties>(x, @"<TypeWithPrimitiveProperties xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><P1>abc</P1><P2>11</P2></TypeWithPrimitiveProperties>");
        Assert.Equal(x.P1, y.P1);
        Assert.StrictEqual(x.P2, y.P2);
    }

    [Fact]
    public static void DCS_TypeWithPrimitiveFields()
    {
        TypeWithPrimitiveFields x = new TypeWithPrimitiveFields { P1 = "abc", P2 = 11 };
        TypeWithPrimitiveFields y = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithPrimitiveFields>(x, @"<TypeWithPrimitiveFields xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><P1>abc</P1><P2>11</P2></TypeWithPrimitiveFields>");
        Assert.Equal(x.P1, y.P1);
        Assert.StrictEqual(x.P2, y.P2);
    }

    [Fact]
    public static void DCS_TypeWithAllPrimitiveProperties()
    {
        TypeWithAllPrimitiveProperties x = new TypeWithAllPrimitiveProperties
        {
            BooleanMember = true,
            //ByteArrayMember = new byte[] { 1, 2, 3, 4 },
            CharMember = 'C',
            DateTimeMember = new DateTime(2016, 7, 8, 9, 10, 11),
            DecimalMember = new decimal(123, 456, 789, true, 0),
            DoubleMember = 123.456,
            FloatMember = 456.789f,
            GuidMember = Guid.Parse("2054fd3e-e118-476a-9962-1a882be51860"),
            //public byte[] HexBinaryMember
            StringMember = "abc",
            IntMember = 123
        };
        TypeWithAllPrimitiveProperties y = DataContractSerializerHelper.SerializeAndDeserialize<TypeWithAllPrimitiveProperties>(x, @"<TypeWithAllPrimitiveProperties xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><BooleanMember>true</BooleanMember><CharMember>67</CharMember><DateTimeMember>2016-07-08T09:10:11</DateTimeMember><DecimalMember>-14554481076115341312123</DecimalMember><DoubleMember>123.456</DoubleMember><FloatMember>456.789</FloatMember><GuidMember>2054fd3e-e118-476a-9962-1a882be51860</GuidMember><IntMember>123</IntMember><StringMember>abc</StringMember></TypeWithAllPrimitiveProperties>");
        Assert.StrictEqual(x.BooleanMember, y.BooleanMember);
        //Assert.StrictEqual(x.ByteArrayMember, y.ByteArrayMember);
        Assert.StrictEqual(x.CharMember, y.CharMember);
        Assert.StrictEqual(x.DateTimeMember, y.DateTimeMember);
        Assert.StrictEqual(x.DecimalMember, y.DecimalMember);
        Assert.StrictEqual(x.DoubleMember, y.DoubleMember);
        Assert.StrictEqual(x.FloatMember, y.FloatMember);
        Assert.StrictEqual(x.GuidMember, y.GuidMember);
        Assert.Equal(x.StringMember, y.StringMember);
        Assert.StrictEqual(x.IntMember, y.IntMember);
    }

#region Array of primitive types

    [Fact]
    public static void DCS_ArrayOfBoolean()
    {
        var value = new bool[] { true, false, true };
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfboolean xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><boolean>true</boolean><boolean>false</boolean><boolean>true</boolean></ArrayOfboolean>");
        Assert.StrictEqual(value.Length, deserialized.Length);
        Assert.True(Enumerable.SequenceEqual(value, deserialized));
    }

    [Fact]
    public static void DCS_ArrayOfDateTime()
    {
        var value = new DateTime[] { new DateTime(2000, 1, 2, 3, 4, 5, DateTimeKind.Utc), new DateTime(2011, 2, 3, 4, 5, 6, DateTimeKind.Utc) };
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfdateTime xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><dateTime>2000-01-02T03:04:05Z</dateTime><dateTime>2011-02-03T04:05:06Z</dateTime></ArrayOfdateTime>");
        Assert.StrictEqual(value.Length, deserialized.Length);
        Assert.True(Enumerable.SequenceEqual(value, deserialized));
    }

    [Fact]
    public static void DCS_ArrayOfDecimal()
    {
        var value = new decimal[] { new decimal(1, 2, 3, false, 1), new decimal(4, 5, 6, true, 2) };
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfdecimal xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><decimal>5534023222971858944.1</decimal><decimal>-1106804644637321461.80</decimal></ArrayOfdecimal>");
        Assert.StrictEqual(value.Length, deserialized.Length);
        Assert.True(Enumerable.SequenceEqual(value, deserialized));
    }

    [Fact]
    public static void DCS_ArrayOfInt32()
    {
        var value = new int[] { 123, int.MaxValue, int.MinValue };
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfint xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><int>123</int><int>2147483647</int><int>-2147483648</int></ArrayOfint>");
        Assert.StrictEqual(value.Length, deserialized.Length);
        Assert.True(Enumerable.SequenceEqual(value, deserialized));
    }

    [Fact]
    public static void DCS_ArrayOfInt64()
    {
        var value = new long[] { 123, long.MaxValue, long.MinValue };
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOflong xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><long>123</long><long>9223372036854775807</long><long>-9223372036854775808</long></ArrayOflong>");
        Assert.StrictEqual(value.Length, deserialized.Length);
        Assert.True(Enumerable.SequenceEqual(value, deserialized));
    }

    [Fact]
    public static void DCS_ArrayOfSingle()
    {
        var value = new float[] { 1.23f, 4.56f, 7.89f };
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOffloat xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><float>1.23</float><float>4.56</float><float>7.89</float></ArrayOffloat>");
        Assert.StrictEqual(value.Length, deserialized.Length);
        Assert.True(Enumerable.SequenceEqual(value, deserialized));
    }

    [Fact]
    public static void DCS_ArrayOfDouble()
    {
        var value = new double[] { 1.23, 4.56, 7.89 };
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfdouble xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><double>1.23</double><double>4.56</double><double>7.89</double></ArrayOfdouble>");
        Assert.StrictEqual(value.Length, deserialized.Length);
        Assert.True(Enumerable.SequenceEqual(value, deserialized));
    }

    [Fact]
    public static void DCS_ArrayOfString()
    {
        var value = new string[] { "abc", "def", "xyz" };
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfstring xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><string>abc</string><string>def</string><string>xyz</string></ArrayOfstring>");
        Assert.StrictEqual(value.Length, deserialized.Length);
        Assert.True(Enumerable.SequenceEqual(value, deserialized));
    }

    [Fact]
    public static void DCS_ArrayOfTypeWithPrimitiveProperties()
    {
        var value = new TypeWithPrimitiveProperties[]
        {
            new TypeWithPrimitiveProperties() { P1 = "abc" , P2 = 123 },
            new TypeWithPrimitiveProperties() { P1 = "def" , P2 = 456 },
        };
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfTypeWithPrimitiveProperties xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><TypeWithPrimitiveProperties><P1>abc</P1><P2>123</P2></TypeWithPrimitiveProperties><TypeWithPrimitiveProperties><P1>def</P1><P2>456</P2></TypeWithPrimitiveProperties></ArrayOfTypeWithPrimitiveProperties>");
        Assert.StrictEqual(value.Length, deserialized.Length);
        Assert.True(Enumerable.SequenceEqual(value, deserialized));
    }

    [Fact]
    public static void DCS_ArrayOfSimpleType()
    {
        // Intentionally set count to 64 to test array resizing functionality during de-serialization.
        int count = 64;
        var value = new SimpleType[count];
        for (int i = 0; i < count; i++)
        {
            value[i] = new SimpleType() { P1 = i.ToString(), P2 = i };
        }

        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, baseline: null, skipStringCompare: true);
        Assert.StrictEqual(value.Length, deserialized.Length);
        Assert.StrictEqual(0, deserialized[0].P2);
        Assert.StrictEqual(1, deserialized[1].P2);
        Assert.StrictEqual(count-1, deserialized[count-1].P2);
    }

    [Fact]
    public static void DCS_TypeWithEmitDefaultValueFalse()
    {
        var value = new TypeWithEmitDefaultValueFalse();

        var actual = DataContractSerializerHelper.SerializeAndDeserialize(value, "<TypeWithEmitDefaultValueFalse xmlns=\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"/>");

        Assert.NotNull(actual);
        Assert.Equal(value.Name, actual.Name);
        Assert.Equal(value.ID, actual.ID);
    }

#endregion

#region Collection

    [Fact]
    public static void DCS_GenericICollectionOfBoolean()
    {
        var value = new TypeImplementsGenericICollection<bool>() { true, false, true };
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfboolean xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><boolean>true</boolean><boolean>false</boolean><boolean>true</boolean></ArrayOfboolean>");
        Assert.StrictEqual(value.Count, deserialized.Count);
        Assert.True(Enumerable.SequenceEqual(value, deserialized));
    }

    [Fact]
    public static void DCS_GenericICollectionOfDecimal()
    {
        var value = new TypeImplementsGenericICollection<decimal>() { new decimal(1, 2, 3, false, 1), new decimal(4, 5, 6, true, 2) };
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfdecimal xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><decimal>5534023222971858944.1</decimal><decimal>-1106804644637321461.80</decimal></ArrayOfdecimal>");
        Assert.StrictEqual(value.Count, deserialized.Count);
        Assert.True(Enumerable.SequenceEqual(value, deserialized));
    }

    [Fact]
    public static void DCS_GenericICollectionOfInt32()
    {
        TypeImplementsGenericICollection<int> x = new TypeImplementsGenericICollection<int>(123, int.MaxValue, int.MinValue);
        TypeImplementsGenericICollection<int> y = DataContractSerializerHelper.SerializeAndDeserialize(x, @"<ArrayOfint xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><int>123</int><int>2147483647</int><int>-2147483648</int></ArrayOfint>");

        Assert.NotNull(y);
        Assert.StrictEqual(x.Count, y.Count);
        Assert.True(x.SequenceEqual(y));
    }

    [Fact]
    public static void DCS_GenericICollectionOfInt64()
    {
        var value = new TypeImplementsGenericICollection<long>() { 123, long.MaxValue, long.MinValue };
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOflong xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><long>123</long><long>9223372036854775807</long><long>-9223372036854775808</long></ArrayOflong>");
        Assert.StrictEqual(value.Count, deserialized.Count);
        Assert.True(Enumerable.SequenceEqual(value, deserialized));
    }

    [Fact]
    public static void DCS_GenericICollectionOfSingle()
    {
        var value = new TypeImplementsGenericICollection<float>() { 1.23f, 4.56f, 7.89f };
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOffloat xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><float>1.23</float><float>4.56</float><float>7.89</float></ArrayOffloat>");
        Assert.StrictEqual(value.Count, deserialized.Count);
        Assert.True(Enumerable.SequenceEqual(value, deserialized));
    }

    [Fact]
    public static void DCS_GenericICollectionOfDouble()
    {
        var value = new TypeImplementsGenericICollection<double>() { 1.23, 4.56, 7.89 };
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfdouble xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><double>1.23</double><double>4.56</double><double>7.89</double></ArrayOfdouble>");
        Assert.StrictEqual(value.Count, deserialized.Count);
        Assert.True(Enumerable.SequenceEqual(value, deserialized));
    }

    [Fact]
    public static void DCS_GenericICollectionOfString()
    {
        TypeImplementsGenericICollection<string> value = new TypeImplementsGenericICollection<string>("a1", "a2");
        TypeImplementsGenericICollection<string> deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfstring xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><string>a1</string><string>a2</string></ArrayOfstring>");

        Assert.NotNull(deserialized);
        Assert.StrictEqual(value.Count, deserialized.Count);
        Assert.True(value.SequenceEqual(deserialized));
    }

    [Fact]
    public static void DCS_GenericICollectionOfTypeWithPrimitiveProperties()
    {
        var value = new TypeImplementsGenericICollection<TypeWithPrimitiveProperties>()
        {
            new TypeWithPrimitiveProperties() { P1 = "abc" , P2 = 123 },
            new TypeWithPrimitiveProperties() { P1 = "def" , P2 = 456 },
        };
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfTypeWithPrimitiveProperties xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><TypeWithPrimitiveProperties><P1>abc</P1><P2>123</P2></TypeWithPrimitiveProperties><TypeWithPrimitiveProperties><P1>def</P1><P2>456</P2></TypeWithPrimitiveProperties></ArrayOfTypeWithPrimitiveProperties>");
        Assert.StrictEqual(value.Count, deserialized.Count);
        Assert.True(Enumerable.SequenceEqual(value, deserialized));
    }

    [Fact]
    public static void DCS_CollectionOfTypeWithNonDefaultNamcespace()
    {
        var value = new CollectionOfTypeWithNonDefaultNamcespace();
        value.Add(new TypeWithNonDefaultNamcespace() { Name = "foo" });

        var actual = DataContractSerializerHelper.SerializeAndDeserialize(value, "<CollectionOfTypeWithNonDefaultNamcespace xmlns=\"CollectionNamespace\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:a=\"ItemTypeNamespace\"><TypeWithNonDefaultNamcespace><a:Name>foo</a:Name></TypeWithNonDefaultNamcespace></CollectionOfTypeWithNonDefaultNamcespace>");
        Assert.NotNull(actual);
        Assert.NotNull(actual[0]);
        Assert.Equal(value[0].Name, actual[0].Name);
    }

#endregion

#region Generic Dictionary

    [Fact]
    public static void DCS_GenericDictionaryOfInt32Boolean()
    {
        var value = new Dictionary<int, bool>();
        value.Add(123, true);
        value.Add(456, false);
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfKeyValueOfintboolean xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><KeyValueOfintboolean><Key>123</Key><Value>true</Value></KeyValueOfintboolean><KeyValueOfintboolean><Key>456</Key><Value>false</Value></KeyValueOfintboolean></ArrayOfKeyValueOfintboolean>");
        Assert.StrictEqual(value.Count, deserialized.Count);
        Assert.True(Enumerable.SequenceEqual(value.ToArray(), deserialized.ToArray()));
    }

    [Fact]
    public static void DCS_GenericDictionaryOfInt32String()
    {
        var value = new Dictionary<int, string>();
        value.Add(123, "abc");
        value.Add(456, "def");
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfKeyValueOfintstring xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><KeyValueOfintstring><Key>123</Key><Value>abc</Value></KeyValueOfintstring><KeyValueOfintstring><Key>456</Key><Value>def</Value></KeyValueOfintstring></ArrayOfKeyValueOfintstring>");
        Assert.StrictEqual(value.Count, deserialized.Count);
        Assert.True(Enumerable.SequenceEqual(value.ToArray(), deserialized.ToArray()));
    }

    [Fact]
    public static void DCS_GenericDictionaryOfStringInt32()
    {
        var value = new Dictionary<string, int>();
        value.Add("abc", 123);
        value.Add("def", 456);
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfKeyValueOfstringint xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><KeyValueOfstringint><Key>abc</Key><Value>123</Value></KeyValueOfstringint><KeyValueOfstringint><Key>def</Key><Value>456</Value></KeyValueOfstringint></ArrayOfKeyValueOfstringint>");
        Assert.StrictEqual(value.Count, deserialized.Count);
        Assert.True(Enumerable.SequenceEqual(value.ToArray(), deserialized.ToArray()));
    }

#endregion

#region Non-Generic Dictionary

    [Fact]
    public static void DCS_NonGenericDictionaryOfInt32Boolean()
    {
        var value = new MyNonGenericDictionary();
        value.Add(123, true);
        value.Add(456, false);
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfKeyValueOfanyTypeanyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><KeyValueOfanyTypeanyType><Key i:type=""a:int"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">123</Key><Value i:type=""a:boolean"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">true</Value></KeyValueOfanyTypeanyType><KeyValueOfanyTypeanyType><Key i:type=""a:int"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">456</Key><Value i:type=""a:boolean"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">false</Value></KeyValueOfanyTypeanyType></ArrayOfKeyValueOfanyTypeanyType>");
        Assert.StrictEqual(value.Count, deserialized.Count);
        Assert.True(Enumerable.SequenceEqual(value.Keys.Cast<int>().ToArray(), deserialized.Keys.Cast<int>().ToArray()));
        Assert.True(Enumerable.SequenceEqual(value.Values.Cast<bool>().ToArray(), deserialized.Values.Cast<bool>().ToArray()));
    }

    [Fact]
    public static void DCS_NonGenericDictionaryOfInt32String()
    {
        var value = new MyNonGenericDictionary();
        value.Add(123, "abc");
        value.Add(456, "def");
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfKeyValueOfanyTypeanyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><KeyValueOfanyTypeanyType><Key i:type=""a:int"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">123</Key><Value i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">abc</Value></KeyValueOfanyTypeanyType><KeyValueOfanyTypeanyType><Key i:type=""a:int"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">456</Key><Value i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">def</Value></KeyValueOfanyTypeanyType></ArrayOfKeyValueOfanyTypeanyType>");
        Assert.StrictEqual(value.Count, deserialized.Count);
        Assert.True(Enumerable.SequenceEqual(value.Keys.Cast<int>().ToArray(), deserialized.Keys.Cast<int>().ToArray()));
        Assert.True(Enumerable.SequenceEqual(value.Values.Cast<string>().ToArray(), deserialized.Values.Cast<string>().ToArray()));
    }

    [Fact]
    public static void DCS_NonGenericDictionaryOfStringInt32()
    {
        var value = new MyNonGenericDictionary();
        value.Add("abc", 123);
        value.Add("def", 456);
        var deserialized = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<ArrayOfKeyValueOfanyTypeanyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><KeyValueOfanyTypeanyType><Key i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">abc</Key><Value i:type=""a:int"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">123</Value></KeyValueOfanyTypeanyType><KeyValueOfanyTypeanyType><Key i:type=""a:string"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">def</Key><Value i:type=""a:int"" xmlns:a=""http://www.w3.org/2001/XMLSchema"">456</Value></KeyValueOfanyTypeanyType></ArrayOfKeyValueOfanyTypeanyType>");
        Assert.StrictEqual(value.Count, deserialized.Count);
        Assert.True(Enumerable.SequenceEqual(value.Keys.Cast<string>().ToArray(), deserialized.Keys.Cast<string>().ToArray()));
        Assert.True(Enumerable.SequenceEqual(value.Values.Cast<int>().ToArray(), deserialized.Values.Cast<int>().ToArray()));
    }

#endregion

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/60462", TestPlatforms.iOS | TestPlatforms.tvOS)]
    public static void DCS_BasicRoundTripResolveDTOTypes()
    {
        ObjectContainer instance = new ObjectContainer(new DTOContainer());
        Func<DataContractSerializer> serializerfunc = () =>
        {
            var settings = new DataContractSerializerSettings()
            {
                DataContractResolver = new DTOResolver()
            };

            var serializer = new DataContractSerializer(typeof(ObjectContainer), settings);
            return serializer;
        };

        string expectedxmlstring = "<ObjectContainer xmlns =\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:DTOContainer\" xmlns:a=\"http://www.default.com\"><nDTO i:type=\"a:DTO\"><DateTime xmlns=\"http://schemas.datacontract.org/2004/07/System\">9999-12-31T23:59:59.9999999Z</DateTime><OffsetMinutes xmlns=\"http://schemas.datacontract.org/2004/07/System\">0</OffsetMinutes></nDTO></_data></ObjectContainer>";
        ObjectContainer deserialized = DataContractSerializerHelper.SerializeAndDeserialize(instance, expectedxmlstring, null, serializerfunc, false);
        Assert.Equal(DateTimeOffset.MaxValue, ((DTOContainer)deserialized.Data).nDTO);
    }

    [Fact]
    public static void DCS_ExtensionDataObjectTest()
    {
        var p2 = new PersonV2();
        p2.Name = "Elizabeth";
        p2.ID = 2006;

        // Serialize the PersonV2 object
        var ser = new DataContractSerializer(typeof(PersonV2));
        var ms1 = new MemoryStream();
        ser.WriteObject(ms1, p2);

        // Verify the payload
        ms1.Position = 0;
        string actualOutput1 = new StreamReader(ms1).ReadToEnd();
        string baseline1 = "<Person xmlns=\"http://www.msn.com/employees\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><Name>Elizabeth</Name><ID>2006</ID></Person>";

        Utils.CompareResult result = Utils.Compare(baseline1, actualOutput1);
        Assert.True(result.Equal, $"{nameof(actualOutput1)} was not as expected: {Environment.NewLine}Expected: {baseline1}{Environment.NewLine}Actual: {actualOutput1}");

        // Deserialize the payload into a Person instance.
        ms1.Position = 0;
        var ser2 = new DataContractSerializer(typeof(Person));
        XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(ms1, new XmlDictionaryReaderQuotas());
        var p1 = (Person)ser2.ReadObject(reader, false);

        Assert.True(p1 != null, $"Variable {nameof(p1)} was null.");
        Assert.True(p1.ExtensionData != null, $"{nameof(p1.ExtensionData)} was null.");
        Assert.Equal(p2.Name, p1.Name);

        // Serialize the Person instance
        var ms2 = new MemoryStream();
        ser2.WriteObject(ms2, p1);

        // Verify the payload
        ms2.Position = 0;
        string actualOutput2 = new StreamReader(ms2).ReadToEnd();
        string baseline2 = "<Person xmlns=\"http://www.msn.com/employees\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><Name>Elizabeth</Name><ID>2006</ID></Person>";

        Utils.CompareResult result2 = Utils.Compare(baseline2, actualOutput2);
        Assert.True(result2.Equal, $"{nameof(actualOutput2)} was not as expected: {Environment.NewLine}Expected: {baseline2}{Environment.NewLine}Actual: {actualOutput2}");
    }

    [Fact]
    public static void DCS_XPathQueryGeneratorTest()
    {
        Type t = typeof(Order);
        MemberInfo[] mi = t.GetMember("Product");
        MemberInfo[] mi2 = t.GetMember("Value");
        MemberInfo[] mi3 = t.GetMember("Quantity");
        Assert.Equal("/xg0:Order/xg0:productName", GenerateaAndGetXPath(t, mi));
        Assert.Equal("/xg0:Order/xg0:cost", GenerateaAndGetXPath(t, mi2));
        Assert.Equal("/xg0:Order/xg0:quantity", GenerateaAndGetXPath(t, mi3));
        Type t2 = typeof(Line);
        MemberInfo[] mi4 = t2.GetMember("Items");
        Assert.Equal("/xg0:Line/xg0:Items", GenerateaAndGetXPath(t2, mi4));
    }
    static string GenerateaAndGetXPath(Type t, MemberInfo[] mi)
    {
        // Create a new name table and name space manager.
        NameTable nt = new NameTable();
        XmlNamespaceManager xname = new XmlNamespaceManager(nt);
        // Generate the query and print it.
        return XPathQueryGenerator.CreateFromDataContractSerializer(
            t, mi, out xname);
    }

    [Fact]
    public static void DCS_MyISerializableType()
    {
        var value = new MyISerializableType();
        value.StringValue = "test string";

        var actual = DataContractSerializerHelper.SerializeAndDeserialize(value, "<MyISerializableType xmlns=\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:x=\"http://www.w3.org/2001/XMLSchema\"><_stringValue i:type=\"x:string\" xmlns=\"\">test string</_stringValue></MyISerializableType>");

        Assert.NotNull(actual);
        Assert.Equal(value.StringValue, actual.StringValue);
    }

    [Fact]
    public static void DCS_TypeWithNonSerializedField()
    {
        var value = new TypeWithSerializableAttributeAndNonSerializedField();
        value.Member1 = 11;
        value.Member2 = "22";
        value.SetMember3(33);
        value.Member4 = "44";

        var actual = DataContractSerializerHelper.SerializeAndDeserialize(
            value,
            "<TypeWithSerializableAttributeAndNonSerializedField xmlns=\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><Member1>11</Member1><_member2>22</_member2><_member3>33</_member3></TypeWithSerializableAttributeAndNonSerializedField>",
            skipStringCompare: false);
        Assert.NotNull(actual);
        Assert.Equal(value.Member1, actual.Member1);
        Assert.Equal(value.Member2, actual.Member2);
        Assert.Equal(value.Member3, actual.Member3);
        Assert.Null(actual.Member4);
    }

    [Fact]
    public static void DCS_TypeWithOptionalField()
    {
        var value = new TypeWithOptionalField();
        value.Member1 = 11;
        value.Member2 = 22;

        var actual = DataContractSerializerHelper.SerializeAndDeserialize(value, "<TypeWithOptionalField xmlns=\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><Member1>11</Member1><Member2>22</Member2></TypeWithOptionalField>");
        Assert.NotNull(actual);
        Assert.Equal(value.Member1, actual.Member1);
        Assert.Equal(value.Member2, actual.Member2);

        int member1Value = 11;
        string payloadMissingOptionalField = $"<TypeWithOptionalField xmlns=\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><Member1>{member1Value}</Member1></TypeWithOptionalField>";
        var deserialized = DeserializeString<TypeWithOptionalField>(payloadMissingOptionalField);
        Assert.Equal(member1Value, deserialized.Member1);
        Assert.Equal(0, deserialized.Member2);
    }

    [Fact]
    public static void DCS_SerializableEnumWithNonSerializedValue()
    {
        var value1 = new TypeWithSerializableEnum();
        value1.EnumField = SerializableEnumWithNonSerializedValue.One;
        var actual1 = DataContractSerializerHelper.SerializeAndDeserialize(value1, "<TypeWithSerializableEnum xmlns=\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><EnumField>One</EnumField></TypeWithSerializableEnum>");
        Assert.NotNull(actual1);
        Assert.Equal(value1.EnumField, actual1.EnumField);

        var value2 = new TypeWithSerializableEnum();
        value2.EnumField = SerializableEnumWithNonSerializedValue.Two;
        Assert.Throws<SerializationException>(() => DataContractSerializerHelper.SerializeAndDeserialize(value2, ""));
    }

    [Fact]
    public static void DCS_SquareWithDeserializationCallback()
    {
        var value = new SquareWithDeserializationCallback(2);
        var actual = DataContractSerializerHelper.SerializeAndDeserialize(value, "<SquareWithDeserializationCallback xmlns=\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><Edge>2</Edge></SquareWithDeserializationCallback>");
        Assert.NotNull(actual);
        Assert.Equal(value.Area, actual.Area);
    }

    [Fact]
    public static void DCS_TypeWithDelegate()
    {
        var value = new TypeWithDelegate();
        value.IntProperty = 3;
        var actual = DataContractSerializerHelper.SerializeAndDeserialize(value, "<TypeWithDelegate xmlns=\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:x=\"http://www.w3.org/2001/XMLSchema\"><IntValue i:type=\"x:int\" xmlns=\"\">3</IntValue></TypeWithDelegate>");
        Assert.NotNull(actual);
        Assert.Null(actual.DelegateProperty);
        Assert.Equal(value.IntProperty, actual.IntProperty);
    }

#region DesktopTest

    [Fact]
    public static void DCS_ResolveNameReturnsEmptyNamespace()
    {
        SerializationTestTypes.EmptyNsContainer instance = new SerializationTestTypes.EmptyNsContainer(new SerializationTestTypes.EmptyNSAddress());
        var settings = new DataContractSerializerSettings() { MaxItemsInObjectGraph = int.MaxValue, IgnoreExtensionDataObject = false, PreserveObjectReferences = false };
        string baseline1 = @"<EmptyNsContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Name>P1</Name><address i:type=""EmptyNSAddress"" xmlns=""""><street>downing street</street></address></EmptyNsContainer>";
        var result = DataContractSerializerHelper.SerializeAndDeserialize(instance, baseline1, settings);
        Assert.True(result.address == null, "Address not null");

        settings = new DataContractSerializerSettings() { DataContractResolver = new SerializationTestTypes.EmptyNamespaceResolver(), MaxItemsInObjectGraph = int.MaxValue, IgnoreExtensionDataObject = false, PreserveObjectReferences = false };
        result = DataContractSerializerHelper.SerializeAndDeserialize(instance, baseline1, settings);
        Assert.True(result.address == null, "Address not null");

        instance = new SerializationTestTypes.EmptyNsContainer(new SerializationTestTypes.UknownEmptyNSAddress());
        string baseline2 = @"<EmptyNsContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Name>P1</Name><address i:type=""AddressFoo"" xmlns=""""><street>downing street</street></address></EmptyNsContainer>";
        result = DataContractSerializerHelper.SerializeAndDeserialize(instance, baseline2, settings);
        Assert.True(result.address == null, "Address not null");
    }

    [Fact]
    public static void DCS_ResolveDatacontractBaseType()
    {
        SerializationTestTypes.Customer customerInstance = new SerializationTestTypes.PreferredCustomerProxy();
        Type customerBaseType = customerInstance.GetType().BaseType;
        var settings = new DataContractSerializerSettings() { DataContractResolver = new SerializationTestTypes.ProxyDataContractResolver(), MaxItemsInObjectGraph = int.MaxValue, IgnoreExtensionDataObject = false, PreserveObjectReferences = true };
        string baseline1 = @"<Customer z:Id=""1"" i:type=""PreferredCustomer"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Name i:nil=""true""/><VipInfo i:nil=""true""/></Customer>";
        object result = DataContractSerializerHelper.SerializeAndDeserialize(customerInstance, baseline1, settings);
        Assert.Equal(customerBaseType, result.GetType());

        settings = new DataContractSerializerSettings() { DataContractResolver = new SerializationTestTypes.ProxyDataContractResolver(), MaxItemsInObjectGraph = int.MaxValue, IgnoreExtensionDataObject = false, PreserveObjectReferences = false };
        string baseline2 = @"<Customer z:Id=""i1"" i:type=""PreferredCustomer"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Name i:nil=""true""/><VipInfo i:nil=""true""/></Customer>";
        result = DataContractSerializerHelper.SerializeAndDeserialize(customerInstance, baseline2, settings);
        Assert.Equal(customerBaseType, result.GetType());
    }

    /// <summary>
    /// Roundtrips a Datacontract type  which contains Primitive types assigned to member of type object.
    /// Resolver is plugged in and resolves the primitive types. Verify resolver called during ser and deser
    /// </summary>
    private static void DCS_BasicRoundTripResolvePrimitiveTypes(string baseline)
    {
        var dataContractSerializerSettings = new DataContractSerializerSettings()
        {
            DataContractResolver = new SerializationTestTypes.PrimitiveTypeResolver(),
            IgnoreExtensionDataObject = false,
            KnownTypes = null,
            MaxItemsInObjectGraph = int.MaxValue,
            PreserveObjectReferences = false
        };

        var value = new SerializationTestTypes.ObjectContainer(new SerializationTestTypes.PrimitiveContainer());

        var actual = DataContractSerializerHelper.SerializeAndDeserialize(value, baseline, dataContractSerializerSettings);
        // Throw Exception when verification failed
        SerializationTestTypes.ComparisonHelper.CompareRecursively(value, actual);
    }

    /// <summary>
    /// Roundtrips a Datacontract type  which contains Primitive types assigned to member of type object.
    /// Resolver is plugged in and resolves the primitive types. Verify resolver called during ser and deser
    /// </summary>
    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/60462", TestPlatforms.iOS | TestPlatforms.tvOS)]
    public static void DCS_BasicRoundTripResolvePrimitiveTypes_NotNetFramework()
    {
        string baseline = @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:PrimitiveContainer_foo"" xmlns:a=""http://www.default.com""><a i:type=""a:Boolean_foo"">false</a><array1><anyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""/><anyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""/><anyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""/></array1><b i:type=""a:Byte_foo"">255</b><c i:type=""a:Byte_foo"">0</c><d i:type=""a:Char_foo"">65535</d><e i:type=""a:Decimal_foo"">79228162514264337593543950335</e><f i:type=""a:Decimal_foo"">-1</f><f5 i:type=""a:DateTime_foo"">9999-12-31T23:59:59.9999999</f5><g i:type=""a:Decimal_foo"">-79228162514264337593543950335</g><guidData i:type=""a:Guid_foo"">4bc848b1-a541-40bf-8aa9-dd6ccb6d0e56</guidData><h i:type=""a:Decimal_foo"">1</h><i i:type=""a:Decimal_foo"">0</i><j i:type=""a:Decimal_foo"">0</j><k i:type=""a:Double_foo"">0</k><l i:type=""a:Double_foo"">5E-324</l><lDTO xmlns:b=""http://schemas.datacontract.org/2004/07/System""/><m i:type=""a:Double_foo"">1.7976931348623157E+308</m><n i:type=""a:Double_foo"">-1.7976931348623157E+308</n><nDTO i:type=""a:DateTimeOffset_foo""><DateTime xmlns=""http://schemas.datacontract.org/2004/07/System"">9999-12-31T23:59:59.9999999Z</DateTime><OffsetMinutes xmlns=""http://schemas.datacontract.org/2004/07/System"">0</OffsetMinutes></nDTO><o i:type=""a:Double_foo"">NaN</o><obj/><p i:type=""a:Double_foo"">-INF</p><q i:type=""a:Double_foo"">INF</q><r i:type=""a:Single_foo"">0</r><s i:type=""a:Single_foo"">1E-45</s><strData i:nil=""true""/><t i:type=""a:Single_foo"">-3.4028235E+38</t><timeSpan i:type=""a:TimeSpan_foo"">P10675199DT2H48M5.4775807S</timeSpan><u i:type=""a:Single_foo"">3.4028235E+38</u><uri>http://www.microsoft.com/</uri><v i:type=""a:Single_foo"">NaN</v><w i:type=""a:Single_foo"">-INF</w><x i:type=""a:Single_foo"">INF</x><xmlQualifiedName i:type=""a:XmlQualifiedName_foo"" xmlns:b=""http://www.microsoft.com"">b:WCF</xmlQualifiedName><y i:type=""a:Int32_foo"">0</y><z i:type=""a:Int32_foo"">2147483647</z><z1 i:type=""a:Int32_foo"">-2147483648</z1><z2 i:type=""a:Int64_foo"">0</z2><z3 i:type=""a:Int64_foo"">9223372036854775807</z3><z4 i:type=""a:Int64_foo"">-9223372036854775808</z4><z5/><z6 i:type=""a:SByte_foo"">0</z6><z7 i:type=""a:SByte_foo"">127</z7><z8 i:type=""a:SByte_foo"">-128</z8><z9 i:type=""a:Int16_foo"">0</z9><z91 i:type=""a:Int16_foo"">32767</z91><z92 i:type=""a:Int16_foo"">-32768</z92><z93 i:type=""a:String_foo"">abc</z93><z94 i:type=""a:UInt16_foo"">0</z94><z95 i:type=""a:UInt16_foo"">65535</z95><z96 i:type=""a:UInt16_foo"">0</z96><z97 i:type=""a:UInt32_foo"">0</z97><z98 i:type=""a:UInt32_foo"">4294967295</z98><z99 i:type=""a:UInt32_foo"">0</z99><z990 i:type=""a:UInt64_foo"">0</z990><z991 i:type=""a:UInt64_foo"">18446744073709551615</z991><z992 i:type=""a:UInt64_foo"">0</z992><z993>AQIDBA==</z993></_data><_data2 i:type=""a:PrimitiveContainer_foo"" xmlns:a=""http://www.default.com""><a i:type=""a:Boolean_foo"">false</a><array1><anyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""/><anyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""/><anyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""/></array1><b i:type=""a:Byte_foo"">255</b><c i:type=""a:Byte_foo"">0</c><d i:type=""a:Char_foo"">65535</d><e i:type=""a:Decimal_foo"">79228162514264337593543950335</e><f i:type=""a:Decimal_foo"">-1</f><f5 i:type=""a:DateTime_foo"">9999-12-31T23:59:59.9999999</f5><g i:type=""a:Decimal_foo"">-79228162514264337593543950335</g><guidData i:type=""a:Guid_foo"">4bc848b1-a541-40bf-8aa9-dd6ccb6d0e56</guidData><h i:type=""a:Decimal_foo"">1</h><i i:type=""a:Decimal_foo"">0</i><j i:type=""a:Decimal_foo"">0</j><k i:type=""a:Double_foo"">0</k><l i:type=""a:Double_foo"">5E-324</l><lDTO xmlns:b=""http://schemas.datacontract.org/2004/07/System""/><m i:type=""a:Double_foo"">1.7976931348623157E+308</m><n i:type=""a:Double_foo"">-1.7976931348623157E+308</n><nDTO i:type=""a:DateTimeOffset_foo""><DateTime xmlns=""http://schemas.datacontract.org/2004/07/System"">9999-12-31T23:59:59.9999999Z</DateTime><OffsetMinutes xmlns=""http://schemas.datacontract.org/2004/07/System"">0</OffsetMinutes></nDTO><o i:type=""a:Double_foo"">NaN</o><obj/><p i:type=""a:Double_foo"">-INF</p><q i:type=""a:Double_foo"">INF</q><r i:type=""a:Single_foo"">0</r><s i:type=""a:Single_foo"">1E-45</s><strData i:nil=""true""/><t i:type=""a:Single_foo"">-3.4028235E+38</t><timeSpan i:type=""a:TimeSpan_foo"">P10675199DT2H48M5.4775807S</timeSpan><u i:type=""a:Single_foo"">3.4028235E+38</u><uri>http://www.microsoft.com/</uri><v i:type=""a:Single_foo"">NaN</v><w i:type=""a:Single_foo"">-INF</w><x i:type=""a:Single_foo"">INF</x><xmlQualifiedName i:type=""a:XmlQualifiedName_foo"" xmlns:b=""http://www.microsoft.com"">b:WCF</xmlQualifiedName><y i:type=""a:Int32_foo"">0</y><z i:type=""a:Int32_foo"">2147483647</z><z1 i:type=""a:Int32_foo"">-2147483648</z1><z2 i:type=""a:Int64_foo"">0</z2><z3 i:type=""a:Int64_foo"">9223372036854775807</z3><z4 i:type=""a:Int64_foo"">-9223372036854775808</z4><z5/><z6 i:type=""a:SByte_foo"">0</z6><z7 i:type=""a:SByte_foo"">127</z7><z8 i:type=""a:SByte_foo"">-128</z8><z9 i:type=""a:Int16_foo"">0</z9><z91 i:type=""a:Int16_foo"">32767</z91><z92 i:type=""a:Int16_foo"">-32768</z92><z93 i:type=""a:String_foo"">abc</z93><z94 i:type=""a:UInt16_foo"">0</z94><z95 i:type=""a:UInt16_foo"">65535</z95><z96 i:type=""a:UInt16_foo"">0</z96><z97 i:type=""a:UInt32_foo"">0</z97><z98 i:type=""a:UInt32_foo"">4294967295</z98><z99 i:type=""a:UInt32_foo"">0</z99><z990 i:type=""a:UInt64_foo"">0</z990><z991 i:type=""a:UInt64_foo"">18446744073709551615</z991><z992 i:type=""a:UInt64_foo"">0</z992><z993>AQIDBA==</z993></_data2></ObjectContainer>";
        DCS_BasicRoundTripResolvePrimitiveTypes(baseline);
    }

    /// <summary>
    /// Roundtrip Datacontract types  which contains members of type enum and struct.
    /// Some enums are resolved by Resolver and others by the KT attribute.
    /// Enum and struct members are of base enum type and ValueTyperespecitively
    /// </summary>
    [Fact]
    public static void DCS_BasicRoundTripResolveEnumStructTypes()
    {
        var dataContractSerializerSettings = new DataContractSerializerSettings()
        {
            DataContractResolver = new SerializationTestTypes.PrimitiveTypeResolver(),
            IgnoreExtensionDataObject = false,
            KnownTypes = null,
            MaxItemsInObjectGraph = int.MaxValue,
            PreserveObjectReferences = false
        };

        string baseline = @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:EnumStructContainer"" xmlns:a=""http://www.default.com""><enumArrayData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:anyType i:type=""a:1munemy"">red</b:anyType><b:anyType i:type=""a:1munemy"">black</b:anyType><b:anyType i:type=""a:1munemy"">blue</b:anyType><b:anyType i:type=""a:1"">Autumn</b:anyType><b:anyType i:type=""a:2"">Spring</b:anyType></enumArrayData><p1 i:type=""a:VT_foo""><b>10</b></p1><p2 i:type=""a:NotSer_foo""><a>0</a></p2><p3 i:type=""a:MyStruct_foo""><globName i:nil=""true""/><value>0</value></p3></_data><_data2 i:type=""a:EnumStructContainer"" xmlns:a=""http://www.default.com""><enumArrayData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:anyType i:type=""a:1munemy"">red</b:anyType><b:anyType i:type=""a:1munemy"">black</b:anyType><b:anyType i:type=""a:1munemy"">blue</b:anyType><b:anyType i:type=""a:1"">Autumn</b:anyType><b:anyType i:type=""a:2"">Spring</b:anyType></enumArrayData><p1 i:type=""a:VT_foo""><b>10</b></p1><p2 i:type=""a:NotSer_foo""><a>0</a></p2><p3 i:type=""a:MyStruct_foo""><globName i:nil=""true""/><value>0</value></p3></_data2></ObjectContainer>";
        var value = new SerializationTestTypes.ObjectContainer(new SerializationTestTypes.EnumStructContainer());

        var actual = DataContractSerializerHelper.SerializeAndDeserialize(value, baseline, dataContractSerializerSettings);
        SerializationTestTypes.ComparisonHelper.CompareRecursively(value, actual);
    }

    [Fact]
    public static void DCS_BasicRoundtripDCRVariation1()
    {
        SerializationTestTypes.DCRVariations dcrVariationsGoing = new SerializationTestTypes.DCRVariations();
        dcrVariationsGoing.unknownType1 = new SerializationTestTypes.Person();
        dcrVariationsGoing.unknownType2 = new SerializationTestTypes.SimpleDC();
        var setting1 = new DataContractSerializerSettings()
        {
            DataContractResolver = new SerializationTestTypes.SimpleResolver_Ser(),
            PreserveObjectReferences = true
        };
        var setting2 = new DataContractSerializerSettings()
        {
            DataContractResolver = new SerializationTestTypes.SimpleResolver_DeSer(),
            PreserveObjectReferences = true
        };
        var dcs1 = new DataContractSerializer(typeof(SerializationTestTypes.CustomClass), setting1);
        var dcs2 = new DataContractSerializer(typeof(SerializationTestTypes.CustomClass), setting2);
        string baseline = @"<CustomClass z:Id=""1"" i:type=""a:SerializationTestTypes.DCRVariations"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/""><unknownType1 z:Id=""2"" i:type=""a:SerializationTestTypes.Person""><Age>0</Age><Name i:nil=""true""/></unknownType1><unknownType2 z:Id=""3"" i:type=""a:SerializationTestTypes.SimpleDC""><Data i:nil=""true""/></unknownType2></CustomClass>";

        MemoryStream ms = new MemoryStream();
        dcs1.WriteObject(ms, dcrVariationsGoing);
        CompareBaseline(baseline, ms);
        ms.Position = 0;
        var dcrVariationsReturning = dcs2.ReadObject(ms);
        SerializationTestTypes.ComparisonHelper.CompareRecursively(dcrVariationsGoing, dcrVariationsReturning);
    }

    [Fact]
    public static void DCS_BasicRoundtripDCRVariation2()
    {
        SerializationTestTypes.DCRVariations dcrVariationsGoing = new SerializationTestTypes.DCRVariations();
        dcrVariationsGoing.unknownType1 = new SerializationTestTypes.Person();
        dcrVariationsGoing.unknownType2 = new SerializationTestTypes.SimpleDC();
        var dcr1 = new SerializationTestTypes.SimpleResolver_Ser();
        var dcr2 = new SerializationTestTypes.SimpleResolver_DeSer();
        var setting = new DataContractSerializerSettings()
        {
            PreserveObjectReferences = true
        };
        var dcs = new DataContractSerializer(typeof(SerializationTestTypes.DCRVariations), setting);
        string baseline = @"<DCRVariations z:Id=""1"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><unknownType1 z:Id=""2"" i:type=""a:SerializationTestTypes.Person"" xmlns:a=""http://schemas.datacontract.org/2004/07/""><Age>0</Age><Name i:nil=""true""/></unknownType1><unknownType2 z:Id=""3"" i:type=""a:SerializationTestTypes.SimpleDC"" xmlns:a=""http://schemas.datacontract.org/2004/07/""><Data i:nil=""true""/></unknownType2></DCRVariations>";

        MemoryStream ms = new MemoryStream();
        var xmlWriter = XmlDictionaryWriter.CreateTextWriter(ms);
        dcs.WriteObject(xmlWriter, dcrVariationsGoing, dcr1);
        xmlWriter.Flush();
        CompareBaseline(baseline, ms);
        ms.Position = 0;
        var xmlReader = XmlDictionaryReader.CreateTextReader(ms, XmlDictionaryReaderQuotas.Max);
        var dcrVariationsReturning = dcs.ReadObject(xmlReader, false, dcr2);
        SerializationTestTypes.ComparisonHelper.CompareRecursively(dcrVariationsGoing, dcrVariationsReturning);
    }

    [Fact]
    public static void DCS_BasicRoundtripDCRVariation3()
    {
        SerializationTestTypes.DCRVariations dcrVariationsGoing = new SerializationTestTypes.DCRVariations();
        dcrVariationsGoing.unknownType1 = new SerializationTestTypes.Person();
        dcrVariationsGoing.unknownType2 = new SerializationTestTypes.SimpleDC();
        var dcr1 = new SerializationTestTypes.SimpleResolver_Ser();
        var dcr2 = new SerializationTestTypes.SimpleResolver_DeSer();
        var setting = new DataContractSerializerSettings()
        {
            DataContractResolver = dcr2,
            PreserveObjectReferences = true
        };
        var dcs = new DataContractSerializer(typeof(SerializationTestTypes.DCRVariations), setting);
        string baseline = @"<DCRVariations z:Id=""1"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><unknownType1 z:Id=""2"" i:type=""a:SerializationTestTypes.Person"" xmlns:a=""http://schemas.datacontract.org/2004/07/""><Age>0</Age><Name i:nil=""true""/></unknownType1><unknownType2 z:Id=""3"" i:type=""a:SerializationTestTypes.SimpleDC"" xmlns:a=""http://schemas.datacontract.org/2004/07/""><Data i:nil=""true""/></unknownType2></DCRVariations>";

        MemoryStream ms = new MemoryStream();
        var xmlWriter = XmlDictionaryWriter.CreateTextWriter(ms);
        dcs.WriteObject(xmlWriter, dcrVariationsGoing, dcr1);
        xmlWriter.Flush();
        CompareBaseline(baseline, ms);
        ms.Position = 0;
        var xmlReader = XmlDictionaryReader.CreateTextReader(ms, XmlDictionaryReaderQuotas.Max);
        var dcrVariationsReturning = dcs.ReadObject(xmlReader, false);
        SerializationTestTypes.ComparisonHelper.CompareRecursively(dcrVariationsGoing, dcrVariationsReturning);
    }

    [Fact]
    public static void DCS_BasicRoundtripDCRVariation4()
    {
        SerializationTestTypes.DCRVariations dcrVariationsGoing = new SerializationTestTypes.DCRVariations();
        dcrVariationsGoing.unknownType1 = new SerializationTestTypes.Person();
        dcrVariationsGoing.unknownType2 = new SerializationTestTypes.SimpleDC();
        var dcr1 = new SerializationTestTypes.SimpleResolver_Ser();
        var dcr2 = new SerializationTestTypes.SimpleResolver_DeSer();
        var setting = new DataContractSerializerSettings()
        {
            DataContractResolver = dcr1,
            PreserveObjectReferences = true
        };
        var dcs = new DataContractSerializer(typeof(SerializationTestTypes.DCRVariations), setting);
        string baseline = @"<DCRVariations z:Id=""1"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><unknownType1 z:Id=""2"" i:type=""a:SerializationTestTypes.Person"" xmlns:a=""http://schemas.datacontract.org/2004/07/""><Age>0</Age><Name i:nil=""true""/></unknownType1><unknownType2 z:Id=""3"" i:type=""a:SerializationTestTypes.SimpleDC"" xmlns:a=""http://schemas.datacontract.org/2004/07/""><Data i:nil=""true""/></unknownType2></DCRVariations>";

        MemoryStream ms = new MemoryStream();
        var xmlWriter = XmlDictionaryWriter.CreateTextWriter(ms);
        dcs.WriteObject(xmlWriter, dcrVariationsGoing);
        xmlWriter.Flush();
        CompareBaseline(baseline, ms);
        ms.Position = 0;
        var xmlReader = XmlDictionaryReader.CreateTextReader(ms, XmlDictionaryReaderQuotas.Max);
        var dcrVariationsReturning = dcs.ReadObject(xmlReader, false, dcr2);
        SerializationTestTypes.ComparisonHelper.CompareRecursively(dcrVariationsGoing, dcrVariationsReturning);
    }

    private static void CompareBaseline(string baseline, MemoryStream ms)
    {
        ms.Position = 0;
        string actualOutput = new StreamReader(ms).ReadToEnd();
        var result = Utils.Compare(baseline, actualOutput);
        Assert.True(result.Equal, string.Format("{1}{0}Test failed.{0}Expected: {2}{0}Actual: {3}",
                Environment.NewLine, result.ErrorMessage, baseline, actualOutput));
    }

    [Fact]
    public static void DCS_BasicRoundTripPOCOWithIgnoreDM()
    {
        var dataContractSerializerSettings = new DataContractSerializerSettings()
        {
            DataContractResolver = new SerializationTestTypes.POCOTypeResolver(),
            IgnoreExtensionDataObject = false,
            KnownTypes = null,
            MaxItemsInObjectGraph = int.MaxValue,
            PreserveObjectReferences = false
        };

        string baseline = @"<POCOObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Data i:type=""a:EmptyDCType"" xmlns:a=""http://www.Default.com""/></POCOObjectContainer>";
        var value = new SerializationTestTypes.POCOObjectContainer();
        value.NonSerializedData = new SerializationTestTypes.Person();

        var actual = DataContractSerializerHelper.SerializeAndDeserialize(value, baseline, dataContractSerializerSettings);

        SerializationTestTypes.ComparisonHelper.CompareRecursively(value, actual);
    }

    [Fact]
    public static void DCS_BasicRoundtripDCRVerifyWireformatScenarios()
    {
        var dataContractSerializerSettings = new DataContractSerializerSettings()
        {
            DataContractResolver = new SerializationTestTypes.WireFormatVerificationResolver(),
            IgnoreExtensionDataObject = false,
            KnownTypes = null,
            MaxItemsInObjectGraph = int.MaxValue,
            PreserveObjectReferences = true
        };
        string typeName = typeof(SerializationTestTypes.Employee).FullName;
        string typeNamespace = typeof(SerializationTestTypes.Employee).Assembly.FullName;

        string baseline1 = $@"<Wireformat1 z:Id=""1"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><alpha z:Id=""2""><person z:Id=""3""><Age>0</Age><Name i:nil=""true""/></person></alpha><beta z:Id=""4""><unknown1 z:Id=""5"" i:type=""CharClass""><c>0</c><c1>65535</c1><c2>0</c2><c3>99</c3></unknown1></beta><charlie z:Id=""6""><unknown2 z:Id=""7"" i:type=""a:{typeName}***"" xmlns:a=""{typeNamespace}***""><dateHired xmlns=""NonExistNamespace"">0001-01-01T00:00:00</dateHired><individual i:nil=""true"" xmlns=""NonExistNamespace"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/><salary xmlns=""NonExistNamespace"">0</salary></unknown2></charlie></Wireformat1>";
        var value1 = new SerializationTestTypes.Wireformat1();
        var actual1 = DataContractSerializerHelper.SerializeAndDeserialize(value1, baseline1, dataContractSerializerSettings);
        SerializationTestTypes.ComparisonHelper.CompareRecursively(value1, actual1);

        string baseline2 = $@"<Wireformat2 z:Id=""1"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><beta1 z:Id=""2""><unknown1 z:Id=""3"" i:type=""CharClass""><c>0</c><c1>65535</c1><c2>0</c2><c3>99</c3></unknown1></beta1><beta2 z:Id=""4""><unknown1 z:Id=""5"" i:type=""CharClass""><c>0</c><c1>65535</c1><c2>0</c2><c3>99</c3></unknown1></beta2><charlie z:Id=""6""><unknown2 z:Id=""7"" i:type=""a:{typeName}***"" xmlns:a=""{typeNamespace}***""><dateHired xmlns=""NonExistNamespace"">0001-01-01T00:00:00</dateHired><individual i:nil=""true"" xmlns=""NonExistNamespace"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/><salary xmlns=""NonExistNamespace"">0</salary></unknown2></charlie></Wireformat2>";
        var value2 = new SerializationTestTypes.Wireformat2();
        var actual2 = DataContractSerializerHelper.SerializeAndDeserialize(value2, baseline2, dataContractSerializerSettings);
        SerializationTestTypes.ComparisonHelper.CompareRecursively(value2, actual2);

        string baseline3 = $@"<Wireformat3 z:Id=""1"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><beta z:Id=""2""><unknown1 z:Id=""3"" i:type=""CharClass""><c>0</c><c1>65535</c1><c2>0</c2><c3>99</c3></unknown1></beta><charlie1 z:Id=""4""><unknown2 z:Id=""5"" i:type=""a:{typeName}***"" xmlns:a=""{typeNamespace}***""><dateHired xmlns=""NonExistNamespace"">0001-01-01T00:00:00</dateHired><individual i:nil=""true"" xmlns=""NonExistNamespace"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/><salary xmlns=""NonExistNamespace"">0</salary></unknown2></charlie1><charlie2 z:Id=""6""><unknown2 z:Id=""7"" i:type=""a:{typeName}***"" xmlns:a=""{typeNamespace}***""><dateHired xmlns=""NonExistNamespace"">0001-01-01T00:00:00</dateHired><individual i:nil=""true"" xmlns=""NonExistNamespace"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/><salary xmlns=""NonExistNamespace"">0</salary></unknown2></charlie2></Wireformat3>";
        var value3 = new SerializationTestTypes.Wireformat3();
        var actual3 = DataContractSerializerHelper.SerializeAndDeserialize(value3, baseline3, dataContractSerializerSettings);
        SerializationTestTypes.ComparisonHelper.CompareRecursively(value3, actual3);
    }

    [Fact]
    public static void DCS_ResolveNameVariationTest()
    {
        SerializationTestTypes.ObjectContainer instance = new SerializationTestTypes.ObjectContainer(new SerializationTestTypes.UserTypeContainer());
        var setting = new DataContractSerializerSettings()
        {
            DataContractResolver = new SerializationTestTypes.UserTypeToPrimitiveTypeResolver()
        };
        string baseline = @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:UserType"" xmlns:a=""http://www.default.com""><unknownData i:type=""b:int"" xmlns:b=""http://www.w3.org/2001/XMLSchema""><id>10000</id></unknownData></_data><_data2 i:type=""a:UserType"" xmlns:a=""http://www.default.com""><unknownData i:type=""b:int"" xmlns:b=""http://www.w3.org/2001/XMLSchema""><id>10000</id></unknownData></_data2></ObjectContainer>";

        var result = DataContractSerializerHelper.SerializeAndDeserialize(instance, baseline, setting);
        SerializationTestTypes.ComparisonHelper.CompareRecursively(instance, result);
    }

    [Fact]
    public static void DCS_BasicRoundtripDCRDefaultCollections()
    {
        var defaultCollections = new SerializationTestTypes.DefaultCollections();
        var setting = new DataContractSerializerSettings()
        {
            DataContractResolver = new SerializationTestTypes.ResolverDefaultCollections(),
            PreserveObjectReferences = true
        };
        string baseline = @"<DefaultCollections z:Id=""1"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><_arrayList z:Id=""2"" z:Size=""1"" xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType z:Id=""3"" i:type=""b:SerializationTestTypes.Person"" xmlns:b=""http://www.default.com""><Age>0</Age><Name i:nil=""true""/></a:anyType></_arrayList><_dictionary z:Id=""4"" z:Size=""1"" xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfintanyType><a:Key>1</a:Key><a:Value z:Id=""5"" i:type=""b:SerializationTestTypes.CharClass"" xmlns:b=""http://www.default.com""><c>0</c><c1>65535</c1><c2>0</c2><c3>99</c3></a:Value></a:KeyValueOfintanyType></_dictionary><_hashtable z:Id=""6"" z:Size=""1"" xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:KeyValueOfanyTypeanyType><a:Key z:Id=""7"" i:type=""b:System.String"" xmlns:b=""http://www.default.com"">one</a:Key><a:Value z:Id=""8"" i:type=""b:SerializationTestTypes.Version1"" xmlns:b=""http://www.default.com""><make z:Id=""9"" i:type=""b:System.String"" xmlns=""TestingVersionTolerance"">Chevrolet</make></a:Value></a:KeyValueOfanyTypeanyType></_hashtable><_singleDimArray z:Id=""10"" z:Size=""1"" xmlns:a=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><a:anyType z:Id=""11"" i:type=""b:SerializationTestTypes.Employee"" xmlns:b=""http://www.default.com""><dateHired xmlns=""NonExistNamespace"">0001-01-01T00:00:00</dateHired><individual i:nil=""true"" xmlns=""NonExistNamespace"" xmlns:c=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/><salary xmlns=""NonExistNamespace"">0</salary></a:anyType></_singleDimArray></DefaultCollections>";

        var actual = DataContractSerializerHelper.SerializeAndDeserialize(defaultCollections, baseline, setting);
        SerializationTestTypes.ComparisonHelper.CompareRecursively(defaultCollections, actual);
    }

    [Fact]
    public static void DCS_BasicPerSerializerRoundTripAndCompare_IObjectRef()
    {

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCExplicitInterfaceIObjRef(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.DCExplicitInterfaceIObjRef***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCExplicitInterfaceIObjRef***""><data z:Id=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Data z:Ref=""i1""/></data></_data><_data2 i:type=""a:SerializationTestTypes.DCExplicitInterfaceIObjRef***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCExplicitInterfaceIObjRef***""><data z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCIObjRef(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.DCIObjRef***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCIObjRef***""><data i:nil=""true""/></_data><_data2 i:type=""a:SerializationTestTypes.DCIObjRef***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCIObjRef***""><data i:nil=""true""/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SerExplicitInterfaceIObjRefReturnsPrivate(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SerExplicitInterfaceIObjRefReturnsPrivate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SerExplicitInterfaceIObjRefReturnsPrivate***""/><_data2 i:type=""a:SerializationTestTypes.SerExplicitInterfaceIObjRefReturnsPrivate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SerExplicitInterfaceIObjRefReturnsPrivate***""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SerIObjRefReturnsPrivate(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SerIObjRefReturnsPrivate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SerIObjRefReturnsPrivate***""/><_data2 i:type=""a:SerializationTestTypes.SerIObjRefReturnsPrivate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SerIObjRefReturnsPrivate***""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCExplicitInterfaceIObjRefReturnsPrivate(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.DCExplicitInterfaceIObjRefReturnsPrivate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCExplicitInterfaceIObjRefReturnsPrivate***""><_data z:Id=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></_data></_data><_data2 i:type=""a:SerializationTestTypes.DCExplicitInterfaceIObjRefReturnsPrivate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCExplicitInterfaceIObjRefReturnsPrivate***""><_data z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCIObjRefReturnsPrivate(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.DCIObjRefReturnsPrivate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCIObjRefReturnsPrivate***""><_data z:Id=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></_data></_data><_data2 i:type=""a:SerializationTestTypes.DCIObjRefReturnsPrivate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCIObjRefReturnsPrivate***""><_data z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data2></ObjectContainer>");
    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/60462", TestPlatforms.iOS | TestPlatforms.tvOS)]
    public static void DCS_BasicPerSerializerRoundTripAndCompare_SampleTypes()
    {
        string assemblyName = typeof(DataContractSerializerTests).Assembly.FullName;

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.TypeNotFound(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.TypeNotFound***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.TypeNotFound***""/><_data2 i:type=""a:SerializationTestTypes.TypeNotFound***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.TypeNotFound***""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.EmptyDCType(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.EmptyDCType***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.EmptyDCType***""/><_data2 i:type=""a:SerializationTestTypes.EmptyDCType***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.EmptyDCType***""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.ObjectContainer(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.ObjectContainer***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.ObjectContainer***""><_data i:type=""b:boolean"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">true</_data><_data2 i:type=""b:boolean"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">true</_data2></_data><_data2 i:type=""a:SerializationTestTypes.ObjectContainer***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.ObjectContainer***""><_data i:type=""b:boolean"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">true</_data><_data2 i:type=""b:boolean"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">true</_data2></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.POCOObjectContainer(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.POCOObjectContainer***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.POCOObjectContainer***""><Data i:type=""b:boolean"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">true</Data></_data><_data2 i:type=""a:SerializationTestTypes.POCOObjectContainer***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.POCOObjectContainer***""><Data i:type=""b:boolean"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">true</Data></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CircularLink(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CircularLink***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CircularLink***""><Link z:Id=""i2""><Link z:Id=""i3""><Link z:Ref=""i1""/><RandomHangingLink i:nil=""true""/></Link><RandomHangingLink i:nil=""true""/></Link><RandomHangingLink z:Id=""i4""><Link z:Id=""i5""><Link z:Id=""i6"" i:type=""b:SerializationTestTypes.CircularLinkDerived***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CircularLinkDerived***""><Link z:Ref=""i4""/><RandomHangingLink i:nil=""true""/></Link><RandomHangingLink i:nil=""true""/></Link><RandomHangingLink i:nil=""true""/></RandomHangingLink></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CircularLinkDerived(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CircularLinkDerived***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CircularLinkDerived***""><Link i:nil=""true""/><RandomHangingLink i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.KT1Base(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.KT1Base***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.KT1Base***""><BData z:Id=""i2"" i:type=""b:SerializationTestTypes.KT1Derived***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.KT1Derived***""><BData i:nil=""true""/><DData>TestData</DData></BData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.KT1Derived(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.KT1Derived***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.KT1Derived***""><BData i:nil=""true""/><DData>TestData</DData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.KT2Base(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.KT2Base***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.KT2Base***""><BData z:Id=""i2"" i:type=""b:SerializationTestTypes.KT2Derived***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.KT2Derived***""><BData i:nil=""true""/><DData>TestData</DData></BData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.KT3BaseKTMReturnsPrivateType(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.KT3BaseKTMReturnsPrivateType***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.KT3BaseKTMReturnsPrivateType***""><BData z:Id=""i2"" i:type=""KT3DerivedPrivate""><BData i:nil=""true""/><DData>TestData</DData></BData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.KT2Derived(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.KT2Derived***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.KT2Derived***""><BData i:nil=""true""/><DData>TestData</DData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.ArrayListWithCDCFilledPublicTypes(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.ArrayListWithCDCFilledPublicTypes***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.ArrayListWithCDCFilledPublicTypes***""><List xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:anyType z:Id=""i2"" i:type=""c:SerializationTestTypes.PublicDC***"" xmlns:c=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDC***""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:anyType><b:anyType z:Id=""i3"" i:type=""c:SerializationTestTypes.PublicDCClassPublicDM***"" xmlns:c=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDCClassPublicDM***""><Data>No change</Data></b:anyType></List></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CustomGenericContainerPrivateType1(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CustomGenericContainerPrivateType1***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CustomGenericContainerPrivateType1***""><_data1 z:Id=""i2""><t z:Id=""i3""><Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></t></_data1></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CustomGenericContainerPrivateType2(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CustomGenericContainerPrivateType2***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CustomGenericContainerPrivateType2***""><_data1 z:Id=""i2""><k z:Id=""i3""><Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></k><t z:Id=""i4""><Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></t></_data1></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CustomGenericContainerPrivateType3(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CustomGenericContainerPrivateType3***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CustomGenericContainerPrivateType3***""><_data1 z:Id=""i2""><k z:Id=""i3""><Data i:nil=""true""/></k><t z:Id=""i4""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></t></_data1></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CustomGenericContainerPrivateType4(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CustomGenericContainerPrivateType4***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CustomGenericContainerPrivateType4***""><_data1 z:Id=""i2""><k z:Id=""i3""><_data/></k><t z:Id=""i4""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></t></_data1></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CustomGenericContainerPublicType1(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CustomGenericContainerPublicType1***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CustomGenericContainerPublicType1***""><data1 z:Id=""i2""><t z:Id=""i3""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></t></data1></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CustomGenericContainerPublicType2(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CustomGenericContainerPublicType2***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CustomGenericContainerPublicType2***""><data1 z:Id=""i2""><k z:Id=""i3""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></k><t z:Id=""i4""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></t></data1></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CustomGenericContainerPublicType3(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CustomGenericContainerPublicType3***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CustomGenericContainerPublicType3***""><data1 z:Id=""i2""><k z:Id=""i3""><Data i:nil=""true""/></k><t z:Id=""i4""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></t></data1></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CustomGenericContainerPublicType4(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CustomGenericContainerPublicType4***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CustomGenericContainerPublicType4***""><data1 z:Id=""i2""><k z:Id=""i3""><Data i:nil=""true""/></k><t z:Id=""i4""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></t></data1></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.GenericContainer(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.GenericContainer***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.GenericContainer***""><GenericData z:Id=""i2"" i:type=""GenericBaseOfSimpleBaseContainermrfXJLu8""><genericData z:Id=""i3"" i:type=""b:SerializationTestTypes.SimpleBaseContainer***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleBaseContainer***""><Base1 i:nil=""true""/><Base2 i:nil=""true""/></genericData></GenericData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.GenericBase<SerializationTestTypes.NonDCPerson>(), $@"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.GenericBase`1[[SerializationTestTypes.NonDCPerson, {assemblyName}]]***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.GenericBase`1[[SerializationTestTypes.NonDCPerson, {assemblyName}]]***""><genericData i:type=""b:SerializationTestTypes.NonDCPerson***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.NonDCPerson***""><Age>20</Age><Name>jeff</Name></genericData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SimpleBase(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SimpleBase***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleBase***""><BaseData/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SimpleBaseDerived(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SimpleBaseDerived***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleBaseDerived***""><BaseData/><DerivedData/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SimpleBaseDerived2(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SimpleBaseDerived2***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleBaseDerived2***""><BaseData/><DerivedData/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SimpleBaseContainer(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SimpleBaseContainer***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleBaseContainer***""><Base1 z:Id=""i2"" i:type=""b:SerializationTestTypes.SimpleBaseDerived***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleBaseDerived***""><BaseData/><DerivedData/></Base1><Base2 z:Id=""i3"" i:type=""b:SerializationTestTypes.SimpleBaseDerived2***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleBaseDerived2***""><BaseData/><DerivedData/></Base2></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCListPrivateTContainer2(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCListPrivateTContainer2***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCListPrivateTContainer2***""><ListData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:anyType z:Id=""i2"" i:type=""PrivateDC""><Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></b:anyType></ListData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCListPrivateTContainer(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCListPrivateTContainer***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCListPrivateTContainer***""><_listData><PrivateDC z:Id=""i2""><Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></PrivateDC></_listData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCListPublicTContainer(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCListPublicTContainer***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCListPublicTContainer***""><ListData><PublicDC z:Id=""i2""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></PublicDC></ListData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCListMixedTContainer(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCListMixedTContainer***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCListMixedTContainer***""><_listData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:anyType z:Id=""i2"" i:type=""c:SerializationTestTypes.PublicDC***"" xmlns:c=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDC***""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:anyType><b:anyType z:Id=""i3"" i:type=""PrivateDC""><Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></b:anyType></_listData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleListImplicitWithDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SampleListImplicitWithDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListImplicitWithDC***""><anyType i:type=""b:dateTime"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</anyType><anyType i:type=""b:duration"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">P10675199DT2H48M5.4775807S</anyType><anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</anyType><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</anyType><anyType i:type=""b:guid"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</anyType><anyType z:Id=""i1"" i:type=""b:SerializationTestTypes.SimpleDCWithRef***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleDCWithRef***""><Data z:Id=""i2""><Data>This is a string</Data></Data><RefData z:Ref=""i2""/></anyType></_data><_data2 i:type=""a:SerializationTestTypes.SampleListImplicitWithDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListImplicitWithDC***""><anyType i:type=""b:dateTime"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</anyType><anyType i:type=""b:duration"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">P10675199DT2H48M5.4775807S</anyType><anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</anyType><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</anyType><anyType i:type=""b:guid"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</anyType><anyType z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleListImplicitWithoutDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SampleListImplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListImplicitWithoutDC***""><anyType i:type=""b:dateTime"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</anyType><anyType i:type=""b:duration"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">P10675199DT2H48M5.4775807S</anyType><anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</anyType><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</anyType><anyType i:type=""b:guid"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">899288c9-8bee-41c1-a6d4-13c477ec1b29</anyType></_data><_data2 i:type=""a:SerializationTestTypes.SampleListImplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListImplicitWithoutDC***""><anyType i:type=""b:dateTime"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</anyType><anyType i:type=""b:duration"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">P10675199DT2H48M5.4775807S</anyType><anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</anyType><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</anyType><anyType i:type=""b:guid"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">899288c9-8bee-41c1-a6d4-13c477ec1b29</anyType></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleListImplicitWithCDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleListImplicitWithCDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListImplicitWithCDC***""><Item i:type=""b:dateTime"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</Item><Item i:type=""z:duration"" xmlns=""Test"">P10675199DT2H48M5.4775807S</Item><Item i:type=""b:string"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</Item><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</Item><Item i:type=""z:guid"" xmlns=""Test"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</Item></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleListExplicitWithDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleListExplicitWithDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListExplicitWithDC***""/><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleListTImplicitWithDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleListTImplicitWithDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListTImplicitWithDC***""/><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleListTImplicitWithoutDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SampleListTImplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListTImplicitWithoutDC***""><DC z:Id=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Data>TestData</Data><Next i:nil=""true""/></DC><DC z:Id=""i2"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Data>TestData</Data><Next i:nil=""true""/></DC><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data><_data2 i:type=""a:SerializationTestTypes.SampleListTImplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListTImplicitWithoutDC***""><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/><DC z:Ref=""i2"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleListTImplicitWithCDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleListTImplicitWithCDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListTImplicitWithCDC***""><Item z:Id=""i2"" xmlns=""Test""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">TestData</Data><Next i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></Item><Item z:Id=""i3"" xmlns=""Test""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">TestData</Data><Next i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></Item><Item z:Ref=""i2"" xmlns=""Test""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleListTExplicitWithDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleListTExplicitWithDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListTExplicitWithDC***""/><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleICollectionTImplicitWithDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleICollectionTImplicitWithDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionTImplicitWithDC***""/><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleICollectionTImplicitWithoutDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SampleICollectionTImplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionTImplicitWithoutDC***""><DC z:Id=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Data>TestData</Data><Next i:nil=""true""/></DC><DC z:Id=""i2"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Data>TestData</Data><Next i:nil=""true""/></DC><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data><_data2 i:type=""a:SerializationTestTypes.SampleICollectionTImplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionTImplicitWithoutDC***""><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/><DC z:Ref=""i2"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleICollectionTImplicitWithCDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleICollectionTImplicitWithCDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionTImplicitWithCDC***""><Item z:Id=""i2"" xmlns=""Test""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">TestData</Data><Next i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></Item><Item z:Id=""i3"" xmlns=""Test""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">TestData</Data><Next i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></Item><Item z:Ref=""i2"" xmlns=""Test""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleICollectionTExplicitWithDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleICollectionTExplicitWithDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionTExplicitWithDC***""/><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleIEnumerableTImplicitWithDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleIEnumerableTImplicitWithDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableTImplicitWithDC***""/><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleIEnumerableTImplicitWithoutDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SampleIEnumerableTImplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableTImplicitWithoutDC***""><DC z:Id=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Data>TestData</Data><Next i:nil=""true""/></DC><DC z:Id=""i2"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Data>TestData</Data><Next i:nil=""true""/></DC><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data><_data2 i:type=""a:SerializationTestTypes.SampleIEnumerableTImplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableTImplicitWithoutDC***""><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/><DC z:Ref=""i2"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleIEnumerableTImplicitWithCDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleIEnumerableTImplicitWithCDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableTImplicitWithCDC***""><Item z:Id=""i2"" xmlns=""Test""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">TestData</Data><Next i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></Item><Item z:Id=""i3"" xmlns=""Test""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">TestData</Data><Next i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></Item><Item z:Ref=""i2"" xmlns=""Test""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleIEnumerableTExplicitWithDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleIEnumerableTExplicitWithDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableTExplicitWithDC***""/><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleIEnumerableTExplicitWithoutDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SampleIEnumerableTExplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableTExplicitWithoutDC***""><DC z:Id=""i1"" i:type=""b:SerializationTestTypes.DC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC***""><Data>TestData</Data><Next i:nil=""true""/></DC><DC z:Id=""i2"" i:type=""b:SerializationTestTypes.DC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC***""><Data>TestData</Data><Next i:nil=""true""/></DC><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data><_data2 i:type=""a:SerializationTestTypes.SampleIEnumerableTExplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableTExplicitWithoutDC***""><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/><DC z:Ref=""i2"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleIEnumerableTExplicitWithCDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleIEnumerableTExplicitWithCDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableTExplicitWithCDC***""><Item z:Id=""i2"" i:type=""b:SerializationTestTypes.DC***"" xmlns=""Test"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC***""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">TestData</Data><Next i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></Item><Item z:Id=""i3"" i:type=""b:SerializationTestTypes.DC***"" xmlns=""Test"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC***""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">TestData</Data><Next i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></Item><Item z:Ref=""i2"" xmlns=""Test""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleIEnumerableTExplicitWithCDCContainsPrivateDC(true), $@"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleIEnumerableTExplicitWithCDCContainsPrivateDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableTExplicitWithCDCContainsPrivateDC***""><Item z:Id=""i2"" i:type=""b:SerializationTestTypes.PrivateDC"" xmlns=""Test"" xmlns:b=""{assemblyName}""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></Item><Item z:Id=""i3"" i:type=""b:SerializationTestTypes.PrivateDC"" xmlns=""Test"" xmlns:b=""{assemblyName}""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></Item><Item z:Ref=""i2"" xmlns=""Test""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleICollectionImplicitWithDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleICollectionImplicitWithDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionImplicitWithDC***""/><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleICollectionImplicitWithoutDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SampleICollectionImplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionImplicitWithoutDC***""><anyType i:type=""b:dateTime"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</anyType><anyType i:type=""b:duration"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">P10675199DT2H48M5.4775807S</anyType><anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</anyType><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</anyType><anyType i:type=""b:guid"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</anyType></_data><_data2 i:type=""a:SerializationTestTypes.SampleICollectionImplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionImplicitWithoutDC***""><anyType i:type=""b:dateTime"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</anyType><anyType i:type=""b:duration"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">P10675199DT2H48M5.4775807S</anyType><anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</anyType><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</anyType><anyType i:type=""b:guid"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</anyType></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleICollectionImplicitWithCDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleICollectionImplicitWithCDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionImplicitWithCDC***""><Item i:type=""b:dateTime"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</Item><Item i:type=""z:duration"" xmlns=""Test"">P10675199DT2H48M5.4775807S</Item><Item i:type=""b:string"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</Item><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</Item><Item i:type=""z:guid"" xmlns=""Test"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</Item></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleICollectionExplicitWithDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleICollectionExplicitWithDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionExplicitWithDC***""/><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleICollectionExplicitWithoutDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SampleICollectionExplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionExplicitWithoutDC***""><anyType i:type=""b:dateTime"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</anyType><anyType i:type=""b:duration"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">P10675199DT2H48M5.4775807S</anyType><anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</anyType><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</anyType><anyType i:type=""b:guid"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</anyType></_data><_data2 i:type=""a:SerializationTestTypes.SampleICollectionExplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionExplicitWithoutDC***""><anyType i:type=""b:dateTime"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</anyType><anyType i:type=""b:duration"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">P10675199DT2H48M5.4775807S</anyType><anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</anyType><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</anyType><anyType i:type=""b:guid"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</anyType></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleICollectionExplicitWithCDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleICollectionExplicitWithCDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionExplicitWithCDC***""><Item i:type=""b:dateTime"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</Item><Item i:type=""z:duration"" xmlns=""Test"">P10675199DT2H48M5.4775807S</Item><Item i:type=""b:string"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</Item><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</Item><Item i:type=""z:guid"" xmlns=""Test"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</Item></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleICollectionExplicitWithCDCContainsPrivateDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleICollectionExplicitWithCDCContainsPrivateDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionExplicitWithCDCContainsPrivateDC***""><Item i:type=""b:dateTime"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</Item><Item i:type=""z:duration"" xmlns=""Test"">P10675199DT2H48M5.4775807S</Item><Item i:type=""b:string"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</Item><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</Item><Item i:type=""z:guid"" xmlns=""Test"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</Item><Item z:Id=""i2"" i:type=""b:PrivateDC"" xmlns=""Test"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""><b:Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</b:Data></Item></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleIEnumerableImplicitWithDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleIEnumerableImplicitWithDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableImplicitWithDC***""/><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleIEnumerableImplicitWithoutDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SampleIEnumerableImplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableImplicitWithoutDC***""><anyType i:type=""b:dateTime"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</anyType><anyType i:type=""b:duration"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">P10675199DT2H48M5.4775807S</anyType><anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</anyType><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</anyType><anyType i:type=""b:guid"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</anyType></_data><_data2 i:type=""a:SerializationTestTypes.SampleIEnumerableImplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableImplicitWithoutDC***""><anyType i:type=""b:dateTime"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</anyType><anyType i:type=""b:duration"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">P10675199DT2H48M5.4775807S</anyType><anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</anyType><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</anyType><anyType i:type=""b:guid"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</anyType></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleIEnumerableImplicitWithCDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleIEnumerableImplicitWithCDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableImplicitWithCDC***""><Item i:type=""b:dateTime"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</Item><Item i:type=""z:duration"" xmlns=""Test"">P10675199DT2H48M5.4775807S</Item><Item i:type=""b:string"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</Item><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</Item><Item i:type=""z:guid"" xmlns=""Test"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</Item></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleIEnumerableExplicitWithDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleIEnumerableExplicitWithDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableExplicitWithDC***""/><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleIEnumerableExplicitWithoutDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SampleIEnumerableExplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableExplicitWithoutDC***""><anyType i:type=""b:dateTime"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</anyType><anyType i:type=""b:duration"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">P10675199DT2H48M5.4775807S</anyType><anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</anyType><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</anyType><anyType i:type=""b:guid"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</anyType></_data><_data2 i:type=""a:SerializationTestTypes.SampleIEnumerableExplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableExplicitWithoutDC***""><anyType i:type=""b:dateTime"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</anyType><anyType i:type=""b:duration"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">P10675199DT2H48M5.4775807S</anyType><anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</anyType><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</anyType><anyType i:type=""b:guid"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</anyType></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleIEnumerableExplicitWithCDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleIEnumerableExplicitWithCDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableExplicitWithCDC***""><Item i:type=""b:dateTime"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</Item><Item i:type=""z:duration"" xmlns=""Test"">P10675199DT2H48M5.4775807S</Item><Item i:type=""b:string"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</Item><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</Item><Item i:type=""z:guid"" xmlns=""Test"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</Item></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleIEnumerableExplicitWithCDCContainsPrivateDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleIEnumerableExplicitWithCDCContainsPrivateDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleIEnumerableExplicitWithCDCContainsPrivateDC***""><Item i:type=""b:dateTime"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</Item><Item i:type=""z:duration"" xmlns=""Test"">P10675199DT2H48M5.4775807S</Item><Item i:type=""b:string"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</Item><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</Item><Item i:type=""z:guid"" xmlns=""Test"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</Item><Item z:Id=""i2"" i:type=""b:PrivateDC"" xmlns=""Test"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""><b:Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</b:Data></Item></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.MyIDictionaryContainsPublicDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.MyIDictionaryContainsPublicDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.MyIDictionaryContainsPublicDC***""><DictItem xmlns=""MyDictNS1""><DictKey z:Id=""i2"" i:type=""b:SerializationTestTypes.PublicDC***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDC***""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></DictKey><DictValue z:Id=""i3"" i:type=""b:SerializationTestTypes.PublicDC***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDC***""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></DictValue></DictItem></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.MyIDictionaryContainsPublicDCExplicit(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.MyIDictionaryContainsPublicDCExplicit***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.MyIDictionaryContainsPublicDCExplicit***""><DictItem xmlns=""MyDictNS1""><DictKey z:Id=""i2"" i:type=""b:SerializationTestTypes.PublicDC***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDC***""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></DictKey><DictValue z:Id=""i3"" i:type=""b:SerializationTestTypes.PublicDC***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDC***""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></DictValue></DictItem></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.MyIDictionaryContainsPrivateDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.MyIDictionaryContainsPrivateDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.MyIDictionaryContainsPrivateDC***""><DictItem xmlns=""MyDictNS2""><DictKey z:Id=""i2"" i:type=""b:PrivateDC"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""><b:Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</b:Data></DictKey><DictValue z:Id=""i3"" i:type=""b:PrivateDC"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""><b:Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</b:Data></DictValue></DictItem><DictItem xmlns=""MyDictNS2""><DictKey z:Id=""i4"" i:type=""b:SerializationTestTypes.PublicDCClassPrivateDM***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDCClassPrivateDM***""><_data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></DictKey><DictValue z:Id=""i5"" i:type=""b:SerializationTestTypes.PublicDCClassPrivateDM***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDCClassPrivateDM***""><_data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></DictValue></DictItem></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCDictionaryPrivateKTContainer(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCDictionaryPrivateKTContainer***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCDictionaryPrivateKTContainer***""><_dictData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:KeyValueOfPrivateDCPrivateDCzETuxydO><b:Key z:Id=""i2""><Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></b:Key><b:Value z:Id=""i3""><Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></b:Value></b:KeyValueOfPrivateDCPrivateDCzETuxydO></_dictData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCDictionaryPublicKTContainer(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCDictionaryPublicKTContainer***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCDictionaryPublicKTContainer***""><DictData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:KeyValueOfPublicDCPublicDCzETuxydO><b:Key z:Id=""i2""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Key><b:Value z:Id=""i3""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Value></b:KeyValueOfPublicDCPublicDCzETuxydO></DictData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCDictionaryMixedKTContainer1(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCDictionaryMixedKTContainer1***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCDictionaryMixedKTContainer1***""><DictData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:KeyValueOfanyTypeanyType><b:Key z:Id=""i2"" i:type=""PrivateDC""><Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></b:Key><b:Value z:Id=""i3"" i:type=""c:SerializationTestTypes.PublicDC***"" xmlns:c=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDC***""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Value></b:KeyValueOfanyTypeanyType><b:KeyValueOfanyTypeanyType><b:Key z:Id=""i4"" i:type=""c:SerializationTestTypes.PublicDC***"" xmlns:c=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDC***""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Key><b:Value z:Id=""i5"" i:type=""PrivateDC""><Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></b:Value></b:KeyValueOfanyTypeanyType></DictData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCDictionaryMixedKTContainer2(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCDictionaryMixedKTContainer2***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCDictionaryMixedKTContainer2***""><_dictData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:KeyValueOfPublicDCPrivateDCzETuxydO><b:Key z:Id=""i2""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Key><b:Value z:Id=""i3""><Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></b:Value></b:KeyValueOfPublicDCPrivateDCzETuxydO></_dictData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCDictionaryMixedKTContainer3(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCDictionaryMixedKTContainer3***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCDictionaryMixedKTContainer3***""><_dictData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:KeyValueOfPrivateDCPublicDCzETuxydO><b:Key z:Id=""i2""><Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></b:Key><b:Value z:Id=""i3""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Value></b:KeyValueOfPrivateDCPublicDCzETuxydO></_dictData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCDictionaryMixedKTContainer4(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCDictionaryMixedKTContainer4***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCDictionaryMixedKTContainer4***""><DictData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:KeyValueOfPublicDCDerivedPublicPublicDCzETuxydO><b:Key z:Id=""i2""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Key><b:Value z:Id=""i3""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Value></b:KeyValueOfPublicDCDerivedPublicPublicDCzETuxydO></DictData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCDictionaryMixedKTContainer5(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCDictionaryMixedKTContainer5***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCDictionaryMixedKTContainer5***""><_dictData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:KeyValueOfPublicDCDerivedPrivatePublicDCzETuxydO><b:Key z:Id=""i2""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Key><b:Value z:Id=""i3""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Value></b:KeyValueOfPublicDCDerivedPrivatePublicDCzETuxydO></_dictData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCDictionaryMixedKTContainer6(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCDictionaryMixedKTContainer6***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCDictionaryMixedKTContainer6***""><_dictData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:KeyValueOfPublicDCDerivedPublicPrivateDCzETuxydO><b:Key z:Id=""i2""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Key><b:Value z:Id=""i3""><Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></b:Value></b:KeyValueOfPublicDCDerivedPublicPrivateDCzETuxydO></_dictData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCDictionaryMixedKTContainer7(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCDictionaryMixedKTContainer7***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCDictionaryMixedKTContainer7***""><_dictData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:KeyValueOfPublicDCDerivedPrivatePrivateDCzETuxydO><b:Key z:Id=""i2""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Key><b:Value z:Id=""i3""><Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></b:Value></b:KeyValueOfPublicDCDerivedPrivatePrivateDCzETuxydO></_dictData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCDictionaryMixedKTContainer8(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCDictionaryMixedKTContainer8***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCDictionaryMixedKTContainer8***""><DictData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:KeyValueOfPublicDCDerivedPublicPublicDCDerivedPubliczETuxydO><b:Key z:Id=""i2""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Key><b:Value z:Id=""i3""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Value></b:KeyValueOfPublicDCDerivedPublicPublicDCDerivedPubliczETuxydO></DictData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCDictionaryMixedKTContainer9(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCDictionaryMixedKTContainer9***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCDictionaryMixedKTContainer9***""><_dictData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:KeyValueOfPublicDCDerivedPrivatePublicDCDerivedPrivatezETuxydO><b:Key z:Id=""i2""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Key><b:Value z:Id=""i3""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Value></b:KeyValueOfPublicDCDerivedPrivatePublicDCDerivedPrivatezETuxydO></_dictData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCDictionaryMixedKTContainer10(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCDictionaryMixedKTContainer10***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCDictionaryMixedKTContainer10***""><_dictData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:KeyValueOfPublicDCDerivedPublicPublicDCDerivedPrivatezETuxydO><b:Key z:Id=""i2""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Key><b:Value z:Id=""i3""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Value></b:KeyValueOfPublicDCDerivedPublicPublicDCDerivedPrivatezETuxydO></_dictData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCDictionaryMixedKTContainer11(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCDictionaryMixedKTContainer11***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCDictionaryMixedKTContainer11***""><_dictData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:KeyValueOfPublicDCDerivedPrivatePublicDCDerivedPubliczETuxydO><b:Key z:Id=""i2""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Key><b:Value z:Id=""i3""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></b:Value></b:KeyValueOfPublicDCDerivedPrivatePublicDCDerivedPubliczETuxydO></_dictData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCDictionaryMixedKTContainer12(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCDictionaryMixedKTContainer12***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCDictionaryMixedKTContainer12***""><_dictData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:KeyValueOfPublicDCClassPrivateDMPublicDCClassPublicDM_DerivedDCClassPublicContainsPrivateDMzETuxydO><b:Key z:Id=""i2""><_data/></b:Key><b:Value z:Id=""i3""><Data i:nil=""true""/><DerivedData2 i:nil=""true""/><_derivedData1/></b:Value></b:KeyValueOfPublicDCClassPrivateDMPublicDCClassPublicDM_DerivedDCClassPublicContainsPrivateDMzETuxydO></_dictData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCDictionaryMixedKTContainer13(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCDictionaryMixedKTContainer13***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCDictionaryMixedKTContainer13***""><_dictData xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:KeyValueOfKT1BaseKT2BasezETuxydO><b:Key z:Id=""i2""><BData i:nil=""true""/></b:Key><b:Value z:Id=""i3""><BData i:nil=""true""/></b:Value></b:KeyValueOfKT1BaseKT2BasezETuxydO></_dictData></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCDictionaryMixedKTContainer14(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCDictionaryMixedKTContainer14***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCDictionaryMixedKTContainer14***""/><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.PublicDC(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.PublicDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDC***""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.PublicDCDerivedPublic(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.PublicDCDerivedPublic***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDCDerivedPublic***""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DC(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC***""><Data>TestData</Data><Next i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCWithReadOnlyField(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCWithReadOnlyField***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCWithReadOnlyField***""><Data i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.PublicDCClassPublicDM(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.PublicDCClassPublicDM***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDCClassPublicDM***""><Data>No change</Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.PublicDCClassPrivateDM(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.PublicDCClassPrivateDM***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDCClassPrivateDM***""><_data>No change</_data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.PublicDCClassInternalDM(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.PublicDCClassInternalDM***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDCClassInternalDM***""><Data>No change</Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.PublicDCClassMixedDM(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.PublicDCClassMixedDM***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDCClassMixedDM***""><Data1>No change</Data1><Data3/><_data2/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.PublicDCClassPublicDM_DerivedDCClassPublic(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.PublicDCClassPublicDM_DerivedDCClassPublic***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDCClassPublicDM_DerivedDCClassPublic***""><Data i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.PublicDCClassPrivateDM_DerivedDCClassPublic(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.PublicDCClassPrivateDM_DerivedDCClassPublic***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDCClassPrivateDM_DerivedDCClassPublic***""><_data/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.PublicDCClassPublicDM_DerivedDCClassPublicContainsPrivateDM(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.PublicDCClassPublicDM_DerivedDCClassPublicContainsPrivateDM***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDCClassPublicDM_DerivedDCClassPublicContainsPrivateDM***""><Data i:nil=""true""/><DerivedData2 i:nil=""true""/><_derivedData1/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Prop_PublicDCClassPublicDM_PublicDCClassPrivateDM(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Prop_PublicDCClassPublicDM_PublicDCClassPrivateDM***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Prop_PublicDCClassPublicDM_PublicDCClassPrivateDM***""><Data z:Id=""i2""><_data/></Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Prop_SetPrivate_PublicDCClassPublicDM_PublicDCClassPrivateDM(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Prop_SetPrivate_PublicDCClassPublicDM_PublicDCClassPrivateDM***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Prop_SetPrivate_PublicDCClassPublicDM_PublicDCClassPrivateDM***""><Data z:Id=""i2""><_data/></Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Prop_GetPrivate_PublicDCClassPublicDM_PublicDCClassPrivateDM(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Prop_GetPrivate_PublicDCClassPublicDM_PublicDCClassPrivateDM***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Prop_GetPrivate_PublicDCClassPublicDM_PublicDCClassPrivateDM***""><Data z:Id=""i2""><_data/></Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Prop_PublicDCClassPublicDM(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Prop_PublicDCClassPublicDM***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Prop_PublicDCClassPublicDM***""><Data>No change</Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Prop_PublicDCClassPrivateDM(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Prop_PublicDCClassPrivateDM***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Prop_PublicDCClassPrivateDM***""><Data>No change</Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Prop_PublicDCClassInternalDM(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Prop_PublicDCClassInternalDM***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Prop_PublicDCClassInternalDM***""><Data>No change</Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Prop_PublicDCClassMixedDM(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Prop_PublicDCClassMixedDM***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Prop_PublicDCClassMixedDM***""><Data1>No change</Data1><Data2 i:nil=""true""/><Data3 i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Prop_PublicDCClassPublicDM_DerivedDCClassPublic(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Prop_PublicDCClassPublicDM_DerivedDCClassPublic***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Prop_PublicDCClassPublicDM_DerivedDCClassPublic***""><Data i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Prop_PublicDCClassPrivateDM_DerivedDCClassPublic(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Prop_PublicDCClassPrivateDM_DerivedDCClassPublic***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Prop_PublicDCClassPrivateDM_DerivedDCClassPublic***""><Data i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Prop_PublicDCClassPublicDM_DerivedDCClassPublicContainsPrivateDM(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Prop_PublicDCClassPublicDM_DerivedDCClassPublicContainsPrivateDM***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Prop_PublicDCClassPublicDM_DerivedDCClassPublicContainsPrivateDM***""><Data i:nil=""true""/><DerivedData2 i:nil=""true""/><_derivedData1/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Prop_SetPrivate_PublicDCClassPublicDM(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Prop_SetPrivate_PublicDCClassPublicDM***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Prop_SetPrivate_PublicDCClassPublicDM***""><Data>No change</Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Prop_GetPrivate_PublicDCClassPublicDM(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Prop_GetPrivate_PublicDCClassPublicDM***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Prop_GetPrivate_PublicDCClassPublicDM***""><Data>No change</Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Derived_Override_Prop_All_Public(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Derived_Override_Prop_All_Public***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived_Override_Prop_All_Public***""><Data>No change</Data><Data>No change</Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Derived_Override_Prop_Private(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Derived_Override_Prop_Private***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived_Override_Prop_Private***""><Data>No change</Data><Data>No change</Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Derived_Override_Prop_GetPrivate_All_Public(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Derived_Override_Prop_GetPrivate_All_Public***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived_Override_Prop_GetPrivate_All_Public***""><Data>No change</Data><Data>No change</Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Derived_Override_Prop_GetPrivate_Private(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Derived_Override_Prop_GetPrivate_Private***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived_Override_Prop_GetPrivate_Private***""><Data>No change</Data><Data>No change</Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DC1_Version1(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.DC1_Version1***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC1_Version1***""/><_data2 i:type=""a:SerializationTestTypes.DC1_Version1***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC1_Version1***""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DC2_Version1(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.DC2_Version1***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC2_Version1***""><Data i:nil=""true""/></_data><_data2 i:type=""a:SerializationTestTypes.DC2_Version1***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC2_Version1***""><Data i:nil=""true""/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DC2_Version4(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.DC2_Version4***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC2_Version4***""><_data/></_data><_data2 i:type=""a:SerializationTestTypes.DC2_Version4***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC2_Version4***""><_data/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DC2_Version5(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.DC2_Version5***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC2_Version5***""><Data i:nil=""true""/></_data><_data2 i:type=""a:SerializationTestTypes.DC2_Version5***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC2_Version5***""><Data i:nil=""true""/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DC3_Version1(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.DC3_Version1***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC3_Version1***""><Data1 i:nil=""true""/></_data><_data2 i:type=""a:SerializationTestTypes.DC3_Version1***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC3_Version1***""><Data1 i:nil=""true""/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DC3_Version2(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.DC3_Version2***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC3_Version2***""/><_data2 i:type=""a:SerializationTestTypes.DC3_Version2***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC3_Version2***""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DC3_Version3(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.DC3_Version3***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC3_Version3***""/><_data2 i:type=""a:SerializationTestTypes.DC3_Version3***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC3_Version3***""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CallBackSample_OnSerializing_Public(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CallBackSample_OnSerializing_Public***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CallBackSample_OnSerializing_Public***""><Data i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CallBackSample_OnSerialized_Public(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CallBackSample_OnSerialized_Public***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CallBackSample_OnSerialized_Public***""><Data i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CallBackSample_OnDeserializing_Public(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CallBackSample_OnDeserializing_Public***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CallBackSample_OnDeserializing_Public***""><Data i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CallBackSample_OnDeserialized_Public(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CallBackSample_OnDeserialized_Public***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CallBackSample_OnDeserialized_Public***""><Data i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CallBackSample_OnSerializing(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CallBackSample_OnSerializing***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CallBackSample_OnSerializing***""><Data i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CallBackSample_OnSerialized(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CallBackSample_OnSerialized***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CallBackSample_OnSerialized***""><Data i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CallBackSample_OnDeserializing(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CallBackSample_OnDeserializing***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CallBackSample_OnDeserializing***""><Data i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CallBackSample_OnDeserialized(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CallBackSample_OnDeserialized***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CallBackSample_OnDeserialized***""><Data i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CallBackSample_IDeserializationCallback(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CallBackSample_IDeserializationCallback***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CallBackSample_IDeserializationCallback***""><Data i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CallBackSample_IDeserializationCallback_Explicit(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CallBackSample_IDeserializationCallback_Explicit***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CallBackSample_IDeserializationCallback_Explicit***""><Data i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CallBackSample_OnDeserialized_Private_Base(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CallBackSample_OnDeserialized_Private_Base***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CallBackSample_OnDeserialized_Private_Base***""><Data>string</Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CallBackSample_OnDeserialized_Public_Derived(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CallBackSample_OnDeserialized_Public_Derived***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CallBackSample_OnDeserialized_Public_Derived***""><Data>string</Data><Data2>string</Data2></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(SerializationTestTypes.CDC_Positive.CreateInstance(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CDC_Positive***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CDC_Positive***""><string>112</string></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(SerializationTestTypes.Base_Positive_VirtualAdd.CreateInstance(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.Base_Positive_VirtualAdd***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.Base_Positive_VirtualAdd***""><string>222323</string><string>222323</string></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(SerializationTestTypes.CDC_NewAddToPrivate.CreateInstance(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CDC_NewAddToPrivate***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CDC_NewAddToPrivate***""><string>223213</string><string>223213</string></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.NonDCPerson(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.NonDCPerson***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.NonDCPerson***""><Age>20</Age><Name>jeff</Name></_data><_data2 i:type=""a:SerializationTestTypes.NonDCPerson***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.NonDCPerson***""><Age>20</Age><Name>jeff</Name></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.PersonSurrogated(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.PersonSurrogated***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PersonSurrogated***""><Age>30</Age><Name>Jeffery</Name></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCSurrogate(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.DCSurrogate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCSurrogate***""/><_data2 i:type=""a:SerializationTestTypes.DCSurrogate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCSurrogate***""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SerSurrogate(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SerSurrogate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SerSurrogate***""/><_data2 i:type=""a:SerializationTestTypes.SerSurrogate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SerSurrogate***""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCSurrogateExplicit(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.DCSurrogateExplicit***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCSurrogateExplicit***""/><_data2 i:type=""a:SerializationTestTypes.DCSurrogateExplicit***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCSurrogateExplicit***""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SerSurrogateExplicit(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SerSurrogateExplicit***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SerSurrogateExplicit***""/><_data2 i:type=""a:SerializationTestTypes.SerSurrogateExplicit***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SerSurrogateExplicit***""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCSurrogateReturnPrivate(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.DCSurrogateReturnPrivate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCSurrogateReturnPrivate***""/><_data2 i:type=""a:SerializationTestTypes.DCSurrogateReturnPrivate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCSurrogateReturnPrivate***""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SerSurrogateReturnPrivate(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SerSurrogateReturnPrivate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SerSurrogateReturnPrivate***""/><_data2 i:type=""a:SerializationTestTypes.SerSurrogateReturnPrivate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SerSurrogateReturnPrivate***""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.NullableContainerContainsValue(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.NullableContainerContainsValue***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.NullableContainerContainsValue***""><Data><Data>Data</Data></Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.NullableContainerContainsNull(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.NullableContainerContainsNull***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.NullableContainerContainsNull***""><Data i:nil=""true""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.NullablePrivateContainerContainsValue(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.NullablePrivateContainerContainsValue***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.NullablePrivateContainerContainsValue***""><Data i:type=""PrivateDCStruct""><Data>2147483647</Data></Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.NullablePrivateContainerContainsNull(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.NullablePrivateContainerContainsNull***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.NullablePrivateContainerContainsNull***""><Data i:type=""PrivateDCStruct""><Data>2147483647</Data></Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.NullablePrivateDataInDMContainerContainsValue(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.NullablePrivateDataInDMContainerContainsValue***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.NullablePrivateDataInDMContainerContainsValue***""><Data><Data z:Id=""i2""><_data>No change</_data></Data></Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.NullablePrivateDataInDMContainerContainsNull(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.NullablePrivateDataInDMContainerContainsNull***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.NullablePrivateDataInDMContainerContainsNull***""><Data i:type=""PublicDCStructContainsPrivateDataInDM""><Data z:Id=""i2""><_data>No change</_data></Data></Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CustomGeneric2<SerializationTestTypes.NonDCPerson>(), $@"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CustomGeneric2`1[[SerializationTestTypes.NonDCPerson, {assemblyName}]]***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CustomGeneric2`1[[SerializationTestTypes.NonDCPerson, {assemblyName}]]***""><Data>data</Data></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DTOContainer(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DTOContainer***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DTOContainer***""><array1><anyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""/><anyType i:type=""b:DateTimeOffset"" xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays"" xmlns:b=""http://schemas.datacontract.org/2004/07/System""><b:DateTime>0001-01-01T00:00:00Z</b:DateTime><b:OffsetMinutes>0</b:OffsetMinutes></anyType><anyType xmlns=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""/></array1><arrayDTO xmlns:b=""http://schemas.datacontract.org/2004/07/System""><b:DateTimeOffset><b:DateTime>0001-01-01T00:00:00Z</b:DateTime><b:OffsetMinutes>0</b:OffsetMinutes></b:DateTimeOffset><b:DateTimeOffset><b:DateTime>9999-12-31T23:59:59.9999999Z</b:DateTime><b:OffsetMinutes>0</b:OffsetMinutes></b:DateTimeOffset></arrayDTO><dictDTO xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/Arrays""><b:KeyValueOfDateTimeOffsetDateTimeOffset_ShTDFhl_P><b:Key xmlns:c=""http://schemas.datacontract.org/2004/07/System""><c:DateTime>0001-01-01T00:00:00Z</c:DateTime><c:OffsetMinutes>0</c:OffsetMinutes></b:Key><b:Value xmlns:c=""http://schemas.datacontract.org/2004/07/System""><c:DateTime>9999-12-31T23:59:59.9999999Z</c:DateTime><c:OffsetMinutes>0</c:OffsetMinutes></b:Value></b:KeyValueOfDateTimeOffsetDateTimeOffset_ShTDFhl_P><b:KeyValueOfDateTimeOffsetDateTimeOffset_ShTDFhl_P><b:Key xmlns:c=""http://schemas.datacontract.org/2004/07/System""><c:DateTime>9999-12-31T23:59:59.9999999Z</c:DateTime><c:OffsetMinutes>0</c:OffsetMinutes></b:Key><b:Value xmlns:c=""http://schemas.datacontract.org/2004/07/System""><c:DateTime>0001-01-01T00:00:00Z</c:DateTime><c:OffsetMinutes>0</c:OffsetMinutes></b:Value></b:KeyValueOfDateTimeOffsetDateTimeOffset_ShTDFhl_P></dictDTO><enumBase1 i:type=""b:SerializationTestTypes.MyEnum1***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.MyEnum1***"">red</enumBase1><lDTO xmlns:b=""http://schemas.datacontract.org/2004/07/System""><b:DateTimeOffset><b:DateTime>9999-12-31T23:59:59.9999999Z</b:DateTime><b:OffsetMinutes>0</b:OffsetMinutes></b:DateTimeOffset><b:DateTimeOffset><b:DateTime>0001-01-01T00:00:00Z</b:DateTime><b:OffsetMinutes>0</b:OffsetMinutes></b:DateTimeOffset></lDTO><nDTO xmlns:b=""http://schemas.datacontract.org/2004/07/System""><b:DateTime>9999-12-31T23:59:59.9999999Z</b:DateTime><b:OffsetMinutes>0</b:OffsetMinutes></nDTO><valType i:type=""b:DateTimeOffset"" xmlns:b=""http://schemas.datacontract.org/2004/07/System""><b:DateTime>0001-01-01T00:00:00Z</b:DateTime><b:OffsetMinutes>0</b:OffsetMinutes></valType></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.MyGenericIDictionaryKVContainsPublicDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.MyGenericIDictionaryKVContainsPublicDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.MyGenericIDictionaryKVContainsPublicDC***""><KeyValueOfPublicDCPublicDCzETuxydO><Key z:Id=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></Key><Value z:Id=""i2"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></Value></KeyValueOfPublicDCPublicDCzETuxydO></_data><_data2 i:type=""a:SerializationTestTypes.MyGenericIDictionaryKVContainsPublicDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.MyGenericIDictionaryKVContainsPublicDC***""><KeyValueOfPublicDCPublicDCzETuxydO><Key z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/><Value z:Ref=""i2"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></KeyValueOfPublicDCPublicDCzETuxydO></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.MyGenericIDictionaryKVContainsPublicDCExplicit(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.MyGenericIDictionaryKVContainsPublicDCExplicit***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.MyGenericIDictionaryKVContainsPublicDCExplicit***""><KeyValueOfPublicDCPublicDCzETuxydO><Key z:Id=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></Key><Value z:Id=""i2"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></Value></KeyValueOfPublicDCPublicDCzETuxydO></_data><_data2 i:type=""a:SerializationTestTypes.MyGenericIDictionaryKVContainsPublicDCExplicit***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.MyGenericIDictionaryKVContainsPublicDCExplicit***""><KeyValueOfPublicDCPublicDCzETuxydO><Key z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/><Value z:Ref=""i2"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></KeyValueOfPublicDCPublicDCzETuxydO></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.MyGenericIDictionaryKVContainsPrivateDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.MyGenericIDictionaryKVContainsPrivateDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.MyGenericIDictionaryKVContainsPrivateDC***""><DictItem xmlns=""MyDictNS""><DictKey z:Id=""i2"" i:type=""b:PrivateDC"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""><b:Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</b:Data></DictKey><DictValue z:Id=""i3"" i:type=""b:PrivateDC"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""><b:Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</b:Data></DictValue></DictItem></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(SerializationTestTypes.CDC_PrivateAdd.CreateInstance(), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CDC_PrivateAdd***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CDC_PrivateAdd***""><string>222323</string><string>222323</string></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CDC_PrivateDefaultCtor(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CDC_PrivateDefaultCtor***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CDC_PrivateDefaultCtor***""/><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCHashtableContainerPublic(true), "", skipStringCompare: true);

        var customGeneric1 = new SerializationTestTypes.CustomGeneric1<SerializationTestTypes.KT1Base>();
        customGeneric1.t = new SerializationTestTypes.KT1Base(true);
        TestObjectInObjectContainerWithSimpleResolver(customGeneric1, $@"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CustomGeneric1`1[[SerializationTestTypes.KT1Base, {assemblyName}]]***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CustomGeneric1`1[[SerializationTestTypes.KT1Base, {assemblyName}]]***""><t z:Id=""i2""><BData z:Id=""i3"" i:type=""b:SerializationTestTypes.KT1Derived***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.KT1Derived***""><BData i:nil=""true""/><DData>TestData</DData></BData></t></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        var customGeneric2 = new SerializationTestTypes.CustomGeneric2<SerializationTestTypes.KT1Base, SerializationTestTypes.NonDCPerson>();
        customGeneric2.t = new SerializationTestTypes.KT1Base(true);
        TestObjectInObjectContainerWithSimpleResolver(customGeneric2, $@"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CustomGeneric2`2[[SerializationTestTypes.KT1Base, {assemblyName}],[SerializationTestTypes.NonDCPerson, {assemblyName}]]***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CustomGeneric2`2[[SerializationTestTypes.KT1Base, {assemblyName}],[SerializationTestTypes.NonDCPerson, {assemblyName}]]***""><k><Age>20</Age><Name>jeff</Name></k><t z:Id=""i2""><BData z:Id=""i3"" i:type=""b:SerializationTestTypes.KT1Derived***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.KT1Derived***""><BData i:nil=""true""/><DData>TestData</DData></BData></t></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        var genericBase2 = new SerializationTestTypes.GenericBase2<SerializationTestTypes.KT1Base, SerializationTestTypes.NonDCPerson>();
        genericBase2.genericData1 = new SerializationTestTypes.KT1Base(true);
        TestObjectInObjectContainerWithSimpleResolver(genericBase2, $@"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.GenericBase2`2[[SerializationTestTypes.KT1Base, {assemblyName}],[SerializationTestTypes.NonDCPerson, {assemblyName}]]***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.GenericBase2`2[[SerializationTestTypes.KT1Base, {assemblyName}],[SerializationTestTypes.NonDCPerson, {assemblyName}]]***""><genericData1 z:Id=""i2""><BData z:Id=""i3"" i:type=""b:SerializationTestTypes.KT1Derived***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.KT1Derived***""><BData i:nil=""true""/><DData>TestData</DData></BData></genericData1><genericData2><Age>20</Age><Name>jeff</Name></genericData2></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");
    }

    [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.DataSetXmlSerializationIsSupported))]
    public static void DCS_BasicPerSerializerRoundTripAndCompare_DataSet()
    {

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCPublicDatasetPublic(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCPublicDatasetPublic***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCPublicDatasetPublic***""><dataSet><xs:schema id=""MyData"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns="""" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><xs:element name=""MyData"" msdata:IsDataSet=""true"" msdata:UseCurrentLocale=""true""><xs:complexType><xs:choice minOccurs=""0"" maxOccurs=""unbounded""><xs:element name=""MyTable""><xs:complexType><xs:sequence><xs:element name=""Data"" type=""xs:string"" minOccurs=""0""/></xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType></xs:element></xs:schema><diffgr:diffgram xmlns:diffgr=""urn:schemas-microsoft-com:xml-diffgram-v1"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><MyData xmlns=""""><MyTable diffgr:id=""MyTable1"" msdata:rowOrder=""0"" diffgr:hasChanges=""inserted""><Data>10</Data></MyTable></MyData></diffgr:diffgram></dataSet><dataSet2><xs:schema id=""MyData"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns="""" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><xs:element name=""MyData"" msdata:IsDataSet=""true"" msdata:UseCurrentLocale=""true""><xs:complexType><xs:choice minOccurs=""0"" maxOccurs=""unbounded""><xs:element name=""MyTable""><xs:complexType><xs:sequence><xs:element name=""Data"" type=""xs:string"" minOccurs=""0""/></xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType></xs:element></xs:schema><diffgr:diffgram xmlns:diffgr=""urn:schemas-microsoft-com:xml-diffgram-v1"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><MyData xmlns=""""><MyTable diffgr:id=""MyTable1"" msdata:rowOrder=""0"" diffgr:hasChanges=""inserted""><Data>10</Data></MyTable></MyData></diffgr:diffgram></dataSet2><dataTable><xs:schema id=""MyData"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns="""" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><xs:element name=""MyData"" msdata:IsDataSet=""true"" msdata:MainDataTable=""MyTable"" msdata:UseCurrentLocale=""true""><xs:complexType><xs:choice minOccurs=""0"" maxOccurs=""unbounded""><xs:element name=""MyTable""><xs:complexType><xs:sequence><xs:element name=""Data"" type=""xs:string"" minOccurs=""0""/></xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType></xs:element></xs:schema><diffgr:diffgram xmlns:diffgr=""urn:schemas-microsoft-com:xml-diffgram-v1"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><MyData xmlns=""""><MyTable diffgr:id=""MyTable1"" msdata:rowOrder=""0"" diffgr:hasChanges=""inserted""><Data>10</Data></MyTable></MyData></diffgr:diffgram></dataTable><dataTable2><xs:schema id=""MyData"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns="""" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><xs:element name=""MyData"" msdata:IsDataSet=""true"" msdata:MainDataTable=""MyTable"" msdata:UseCurrentLocale=""true""><xs:complexType><xs:choice minOccurs=""0"" maxOccurs=""unbounded""><xs:element name=""MyTable""><xs:complexType><xs:sequence><xs:element name=""Data"" type=""xs:string"" minOccurs=""0""/></xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType></xs:element></xs:schema><diffgr:diffgram xmlns:diffgr=""urn:schemas-microsoft-com:xml-diffgram-v1"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><MyData xmlns=""""><MyTable diffgr:id=""MyTable1"" msdata:rowOrder=""0"" diffgr:hasChanges=""inserted""><Data>10</Data></MyTable></MyData></diffgr:diffgram></dataTable2></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCPublicDatasetPrivate(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.DCPublicDatasetPrivate***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCPublicDatasetPrivate***""><_dataSet><xs:schema id=""MyData"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns="""" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><xs:element name=""MyData"" msdata:IsDataSet=""true"" msdata:UseCurrentLocale=""true""><xs:complexType><xs:choice minOccurs=""0"" maxOccurs=""unbounded""><xs:element name=""MyTable""><xs:complexType><xs:sequence><xs:element name=""Data"" type=""xs:string"" minOccurs=""0""/></xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType></xs:element></xs:schema><diffgr:diffgram xmlns:diffgr=""urn:schemas-microsoft-com:xml-diffgram-v1"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><MyData xmlns=""""><MyTable diffgr:id=""MyTable1"" msdata:rowOrder=""0"" diffgr:hasChanges=""inserted""><Data>20</Data></MyTable></MyData></diffgr:diffgram></_dataSet><dataTable><xs:schema id=""MyData"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns="""" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><xs:element name=""MyData"" msdata:IsDataSet=""true"" msdata:MainDataTable=""MyTable"" msdata:UseCurrentLocale=""true""><xs:complexType><xs:choice minOccurs=""0"" maxOccurs=""unbounded""><xs:element name=""MyTable""><xs:complexType><xs:sequence><xs:element name=""Data"" type=""xs:string"" minOccurs=""0""/></xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType></xs:element></xs:schema><diffgr:diffgram xmlns:diffgr=""urn:schemas-microsoft-com:xml-diffgram-v1"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><MyData xmlns=""""><MyTable diffgr:id=""MyTable1"" msdata:rowOrder=""0"" diffgr:hasChanges=""inserted""><Data>20</Data></MyTable></MyData></diffgr:diffgram></dataTable></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        var setting = new DataContractSerializerSettings()
        {
            DataContractResolver = new SerializationTestTypes.SimpleResolver()
        };

        //SerPublicDatasetPublic
        string baselineSerPublicDatasetPublic = @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SerPublicDatasetPublic***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SerPublicDatasetPublic***""><dataSet><xs:schema id=""MyData"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns="""" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><xs:element name=""MyData"" msdata:IsDataSet=""true"" msdata:UseCurrentLocale=""true""><xs:complexType><xs:choice minOccurs=""0"" maxOccurs=""unbounded""><xs:element name=""MyTable""><xs:complexType><xs:sequence><xs:element name=""Data"" type=""xs:string"" minOccurs=""0""/></xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType></xs:element></xs:schema><diffgr:diffgram xmlns:diffgr=""urn:schemas-microsoft-com:xml-diffgram-v1"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><MyData xmlns=""""><MyTable diffgr:id=""MyTable1"" msdata:rowOrder=""0"" diffgr:hasChanges=""inserted""><Data>Testing</Data></MyTable></MyData></diffgr:diffgram></dataSet><dataTable><xs:schema id=""NewDataSet"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns="""" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><xs:element name=""NewDataSet"" msdata:IsDataSet=""true"" msdata:MainDataTable=""MyTable"" msdata:UseCurrentLocale=""true""><xs:complexType><xs:choice minOccurs=""0"" maxOccurs=""unbounded""><xs:element name=""MyTable""><xs:complexType><xs:sequence><xs:element name=""Data"" type=""xs:string"" minOccurs=""0""/></xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType></xs:element></xs:schema><diffgr:diffgram xmlns:diffgr=""urn:schemas-microsoft-com:xml-diffgram-v1"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><DocumentElement xmlns=""""><MyTable diffgr:id=""MyTable1"" msdata:rowOrder=""0"" diffgr:hasChanges=""inserted""><Data>Testing</Data></MyTable></DocumentElement></diffgr:diffgram></dataTable></_data><_data2 i:type=""a:SerializationTestTypes.SerPublicDatasetPublic***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SerPublicDatasetPublic***""><dataSet><xs:schema id=""MyData"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns="""" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><xs:element name=""MyData"" msdata:IsDataSet=""true"" msdata:UseCurrentLocale=""true""><xs:complexType><xs:choice minOccurs=""0"" maxOccurs=""unbounded""><xs:element name=""MyTable""><xs:complexType><xs:sequence><xs:element name=""Data"" type=""xs:string"" minOccurs=""0""/></xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType></xs:element></xs:schema><diffgr:diffgram xmlns:diffgr=""urn:schemas-microsoft-com:xml-diffgram-v1"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><MyData xmlns=""""><MyTable diffgr:id=""MyTable1"" msdata:rowOrder=""0"" diffgr:hasChanges=""inserted""><Data>Testing</Data></MyTable></MyData></diffgr:diffgram></dataSet><dataTable><xs:schema id=""NewDataSet"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns="""" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><xs:element name=""NewDataSet"" msdata:IsDataSet=""true"" msdata:MainDataTable=""MyTable"" msdata:UseCurrentLocale=""true""><xs:complexType><xs:choice minOccurs=""0"" maxOccurs=""unbounded""><xs:element name=""MyTable""><xs:complexType><xs:sequence><xs:element name=""Data"" type=""xs:string"" minOccurs=""0""/></xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType></xs:element></xs:schema><diffgr:diffgram xmlns:diffgr=""urn:schemas-microsoft-com:xml-diffgram-v1"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><DocumentElement xmlns=""""><MyTable diffgr:id=""MyTable1"" msdata:rowOrder=""0"" diffgr:hasChanges=""inserted""><Data>Testing</Data></MyTable></DocumentElement></diffgr:diffgram></dataTable></_data2></ObjectContainer>";
        var valueSerPublicDatasetPublic = new SerializationTestTypes.ObjectContainer(new SerializationTestTypes.SerPublicDatasetPublic(true));
        var resultSerPublicDatasetPublic = DataContractSerializerHelper.SerializeAndDeserialize(valueSerPublicDatasetPublic, baselineSerPublicDatasetPublic, setting);

        Assert.True(valueSerPublicDatasetPublic.Data.GetType().Equals(resultSerPublicDatasetPublic.Data.GetType()));
        var valueDataSetPublic = (SerializationTestTypes.SerPublicDatasetPublic)(valueSerPublicDatasetPublic.Data);
        var resultDataSetPublic = (SerializationTestTypes.SerPublicDatasetPublic)(resultSerPublicDatasetPublic.Data);
        Assert.Equal(valueDataSetPublic.dataSet.DataSetName, resultDataSetPublic.dataSet.DataSetName);
        Assert.Equal(valueDataSetPublic.dataSet.Tables[0].TableName, resultDataSetPublic.dataSet.Tables[0].TableName);
        Assert.Equal(valueDataSetPublic.dataSet.Tables[0].Columns[0].ColumnName, resultDataSetPublic.dataSet.Tables[0].Columns[0].ColumnName);
        Assert.Equal(valueDataSetPublic.dataSet.Tables[0].Rows[0][0], resultDataSetPublic.dataSet.Tables[0].Rows[0][0]);

        Assert.Equal(valueDataSetPublic.dataTable.TableName, resultDataSetPublic.dataTable.TableName);
        Assert.Equal(valueDataSetPublic.dataTable.Columns[0].ColumnName, resultDataSetPublic.dataTable.Columns[0].ColumnName);
        Assert.Equal(valueDataSetPublic.dataTable.Rows[0][0], resultDataSetPublic.dataTable.Rows[0][0]);

        //SerPublicDatasetPrivate
        string baselineSerPublicDatasetPrivate = @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SerPublicDatasetPrivate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SerPublicDatasetPrivate***""><_dataSet><xs:schema id=""MyData"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns="""" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><xs:element name=""MyData"" msdata:IsDataSet=""true"" msdata:UseCurrentLocale=""true""><xs:complexType><xs:choice minOccurs=""0"" maxOccurs=""unbounded""><xs:element name=""MyTable""><xs:complexType><xs:sequence><xs:element name=""Data"" type=""xs:string"" minOccurs=""0""/></xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType></xs:element></xs:schema><diffgr:diffgram xmlns:diffgr=""urn:schemas-microsoft-com:xml-diffgram-v1"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><MyData xmlns=""""><MyTable diffgr:id=""MyTable1"" msdata:rowOrder=""0"" diffgr:hasChanges=""inserted""><Data>Testing</Data></MyTable></MyData></diffgr:diffgram></_dataSet><dataTable><xs:schema id=""NewDataSet"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns="""" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><xs:element name=""NewDataSet"" msdata:IsDataSet=""true"" msdata:MainDataTable=""MyTable"" msdata:UseCurrentLocale=""true""><xs:complexType><xs:choice minOccurs=""0"" maxOccurs=""unbounded""><xs:element name=""MyTable""><xs:complexType><xs:sequence><xs:element name=""Data"" type=""xs:string"" minOccurs=""0""/></xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType></xs:element></xs:schema><diffgr:diffgram xmlns:diffgr=""urn:schemas-microsoft-com:xml-diffgram-v1"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><DocumentElement xmlns=""""><MyTable diffgr:id=""MyTable1"" msdata:rowOrder=""0"" diffgr:hasChanges=""inserted""><Data>Testing</Data></MyTable></DocumentElement></diffgr:diffgram></dataTable></_data><_data2 i:type=""a:SerializationTestTypes.SerPublicDatasetPrivate***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SerPublicDatasetPrivate***""><_dataSet><xs:schema id=""MyData"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns="""" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><xs:element name=""MyData"" msdata:IsDataSet=""true"" msdata:UseCurrentLocale=""true""><xs:complexType><xs:choice minOccurs=""0"" maxOccurs=""unbounded""><xs:element name=""MyTable""><xs:complexType><xs:sequence><xs:element name=""Data"" type=""xs:string"" minOccurs=""0""/></xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType></xs:element></xs:schema><diffgr:diffgram xmlns:diffgr=""urn:schemas-microsoft-com:xml-diffgram-v1"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><MyData xmlns=""""><MyTable diffgr:id=""MyTable1"" msdata:rowOrder=""0"" diffgr:hasChanges=""inserted""><Data>Testing</Data></MyTable></MyData></diffgr:diffgram></_dataSet><dataTable><xs:schema id=""NewDataSet"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns="""" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><xs:element name=""NewDataSet"" msdata:IsDataSet=""true"" msdata:MainDataTable=""MyTable"" msdata:UseCurrentLocale=""true""><xs:complexType><xs:choice minOccurs=""0"" maxOccurs=""unbounded""><xs:element name=""MyTable""><xs:complexType><xs:sequence><xs:element name=""Data"" type=""xs:string"" minOccurs=""0""/></xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType></xs:element></xs:schema><diffgr:diffgram xmlns:diffgr=""urn:schemas-microsoft-com:xml-diffgram-v1"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata""><DocumentElement xmlns=""""><MyTable diffgr:id=""MyTable1"" msdata:rowOrder=""0"" diffgr:hasChanges=""inserted""><Data>Testing</Data></MyTable></DocumentElement></diffgr:diffgram></dataTable></_data2></ObjectContainer>";
        var valueSerPublicDatasetPrivate = new SerializationTestTypes.ObjectContainer(new SerializationTestTypes.SerPublicDatasetPrivate(true));
        var resultSerPublicDatasetPrivate = DataContractSerializerHelper.SerializeAndDeserialize(valueSerPublicDatasetPrivate, baselineSerPublicDatasetPrivate, setting);

        Assert.True(valueSerPublicDatasetPrivate.Data.GetType().Equals(resultSerPublicDatasetPrivate.Data.GetType()));
        var valueDataSetPrivate = (SerializationTestTypes.SerPublicDatasetPrivate)(valueSerPublicDatasetPrivate.Data);
        var resultDataSetPrivate = (SerializationTestTypes.SerPublicDatasetPrivate)(resultSerPublicDatasetPrivate.Data);
        Assert.Equal(valueDataSetPrivate.dataTable.TableName, resultDataSetPrivate.dataTable.TableName);
        Assert.Equal(valueDataSetPrivate.dataTable.Columns[0].ColumnName, resultDataSetPrivate.dataTable.Columns[0].ColumnName);
        Assert.Equal(valueDataSetPrivate.dataTable.Rows[0][0], resultDataSetPrivate.dataTable.Rows[0][0]);
    }

    [Fact]
    public static void DCS_BasicPerSerializerRoundTripAndCompare_TypeInheritedFromIListT()
    {
        string assemblyName = typeof(DataContractSerializerTests).Assembly.FullName;

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleListTExplicitWithoutDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SampleListTExplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListTExplicitWithoutDC***""><DC z:Id=""i1"" i:type=""b:SerializationTestTypes.DC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC***""><Data>TestData</Data><Next i:nil=""true""/></DC><DC z:Id=""i2"" i:type=""b:SerializationTestTypes.DC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC***""><Data>TestData</Data><Next i:nil=""true""/></DC><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data><_data2 i:type=""a:SerializationTestTypes.SampleListTExplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListTExplicitWithoutDC***""><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/><DC z:Ref=""i2"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleListTExplicitWithCDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleListTExplicitWithCDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListTExplicitWithCDC***""><Item z:Id=""i2"" i:type=""b:SerializationTestTypes.DC***"" xmlns=""Test"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC***""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">TestData</Data><Next i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></Item><Item z:Id=""i3"" i:type=""b:SerializationTestTypes.DC***"" xmlns=""Test"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC***""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">TestData</Data><Next i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></Item><Item z:Ref=""i2"" xmlns=""Test""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleListTExplicitWithCDCContainsPublicDCClassPrivateDM(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleListTExplicitWithCDCContainsPublicDCClassPrivateDM***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListTExplicitWithCDCContainsPublicDCClassPrivateDM***""><Item z:Id=""i2"" i:type=""b:SerializationTestTypes.PublicDCClassPrivateDM***"" xmlns=""Test"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDCClassPrivateDM***""><_data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></Item><Item z:Id=""i3"" i:type=""b:SerializationTestTypes.PublicDCClassPrivateDM***"" xmlns=""Test"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDCClassPrivateDM***""><_data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></Item><Item z:Ref=""i2"" xmlns=""Test""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleListTExplicitWithCDCContainsPrivateDC(true), $@"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleListTExplicitWithCDCContainsPrivateDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListTExplicitWithCDCContainsPrivateDC***""><Item z:Id=""i2"" i:type=""b:SerializationTestTypes.PrivateDC"" xmlns=""Test"" xmlns:b=""{assemblyName}""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></Item><Item z:Id=""i3"" i:type=""b:SerializationTestTypes.PrivateDC"" xmlns=""Test"" xmlns:b=""{assemblyName}""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></Item><Item z:Ref=""i2"" xmlns=""Test""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");
    }

    [Fact]
    public static void DCS_BasicPerSerializerRoundTripAndCompare_InheritedFromIList()
    {
        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleListExplicitWithoutDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SampleListExplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListExplicitWithoutDC***""><anyType i:type=""b:dateTime"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</anyType><anyType i:type=""b:duration"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">P10675199DT2H48M5.4775807S</anyType><anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</anyType><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</anyType><anyType i:type=""b:guid"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</anyType></_data><_data2 i:type=""a:SerializationTestTypes.SampleListExplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListExplicitWithoutDC***""><anyType i:type=""b:dateTime"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</anyType><anyType i:type=""b:duration"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">P10675199DT2H48M5.4775807S</anyType><anyType i:type=""b:string"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</anyType><anyType i:type=""b:double"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</anyType><anyType i:type=""b:guid"" xmlns:b=""http://schemas.microsoft.com/2003/10/Serialization/"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</anyType></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CB1(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CB1***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CB1***""><anyType z:Id=""i2"" i:type=""b:SerializationTestTypes.PublicDC***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDC***""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></anyType><anyType z:Id=""i3"" i:type=""b:SerializationTestTypes.PublicDCClassPublicDM***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDCClassPublicDM***""><Data i:nil=""true""/></anyType><anyType z:Id=""i4"" i:type=""b:SerializationTestTypes.PublicDCClassPrivateDM_DerivedDCClassPublic***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDCClassPrivateDM_DerivedDCClassPublic***""><_data/></anyType></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CollectionBaseWithCDCFilledPublicTypes(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.CollectionBaseWithCDCFilledPublicTypes***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.CollectionBaseWithCDCFilledPublicTypes***""><anyType z:Id=""i2"" i:type=""b:SerializationTestTypes.PublicDC***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDC***""><Data>55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></anyType><anyType z:Id=""i3"" i:type=""b:SerializationTestTypes.PublicDCClassPublicDM***"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDCClassPublicDM***""><Data>No change</Data></anyType></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleListExplicitWithCDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleListExplicitWithCDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListExplicitWithCDC***""><Item i:type=""b:dateTime"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</Item><Item i:type=""z:duration"" xmlns=""Test"">P10675199DT2H48M5.4775807S</Item><Item i:type=""b:string"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</Item><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</Item><Item i:type=""z:guid"" xmlns=""Test"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</Item></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SampleListExplicitWithCDCContainsPrivateDC(true), @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleListExplicitWithCDCContainsPrivateDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleListExplicitWithCDCContainsPrivateDC***""><Item i:type=""b:dateTime"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">0001-01-01T00:00:00</Item><Item i:type=""z:duration"" xmlns=""Test"">P10675199DT2H48M5.4775807S</Item><Item i:type=""b:string"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema""/><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">1.7976931348623157E+308</Item><Item i:type=""b:double"" xmlns=""Test"" xmlns:b=""http://www.w3.org/2001/XMLSchema"">-INF</Item><Item i:type=""z:guid"" xmlns=""Test"">0c9e174e-cdd8-4b68-a70d-aaeb26c7deeb</Item><Item z:Id=""i2"" i:type=""b:PrivateDC"" xmlns=""Test"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""><b:Data>7b4ac88f-972b-43e5-8f6a-5ae64480eaad</b:Data></Item></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>");
    }

    [Fact]
    public static void DCS_BasicPerSerializerRoundTripAndCompare_SampleTypes_SampleICollectionTExplicit()
    {
        var setting = new DataContractSerializerSettings()
        {
            DataContractResolver = new SerializationTestTypes.SimpleResolver()
        };
        string assemblyName = typeof(DataContractSerializerTests).Assembly.FullName;

        var valueSampleICollectionTExplicitWithoutDC = new SerializationTestTypes.ObjectContainer(new SerializationTestTypes.SampleICollectionTExplicitWithoutDC(true));
        string netcorePayloadSampleICollectionTExplicitWithoutDC = @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SampleICollectionTExplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionTExplicitWithoutDC***""><DC z:Id=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Data>TestData</Data><Next i:nil=""true""/></DC><DC z:Id=""i2"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""><Data>TestData</Data><Next i:nil=""true""/></DC><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data><_data2 i:type=""a:SerializationTestTypes.SampleICollectionTExplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionTExplicitWithoutDC***""><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/><DC z:Ref=""i2"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data2></ObjectContainer>";
        string desktopPayloadSampleICollectionTExplicitWithoutDC = @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data i:type=""a:SerializationTestTypes.SampleICollectionTExplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionTExplicitWithoutDC***""><DC z:Id=""i1"" i:type=""b:SerializationTestTypes.DC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC***""><Data>TestData</Data><Next i:nil=""true""/></DC><DC z:Id=""i2"" i:type=""b:SerializationTestTypes.DC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC***""><Data>TestData</Data><Next i:nil=""true""/></DC><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data><_data2 i:type=""a:SerializationTestTypes.SampleICollectionTExplicitWithoutDC***"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionTExplicitWithoutDC***""><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/><DC z:Ref=""i2"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/><DC z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></_data2></ObjectContainer>";
        TestObjectWithDifferentPayload(valueSampleICollectionTExplicitWithoutDC, netcorePayloadSampleICollectionTExplicitWithoutDC, desktopPayloadSampleICollectionTExplicitWithoutDC, setting);

        var valueSampleICollectionTExplicitWithCDC = new SerializationTestTypes.ObjectContainer(new SerializationTestTypes.SampleICollectionTExplicitWithCDC(true));
        string netcorePayloadSampleICollectionTExplicitWithCDC = @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleICollectionTExplicitWithCDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionTExplicitWithCDC***""><Item z:Id=""i2"" xmlns=""Test""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">TestData</Data><Next i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></Item><Item z:Id=""i3"" xmlns=""Test""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">TestData</Data><Next i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></Item><Item z:Ref=""i2"" xmlns=""Test""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>";
        string desktopPayloadSampleICollectionTExplicitWithCDC = @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleICollectionTExplicitWithCDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionTExplicitWithCDC***""><Item z:Id=""i2"" i:type=""b:SerializationTestTypes.DC***"" xmlns=""Test"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC***""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">TestData</Data><Next i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></Item><Item z:Id=""i3"" i:type=""b:SerializationTestTypes.DC***"" xmlns=""Test"" xmlns:b=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.DC***""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">TestData</Data><Next i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes""/></Item><Item z:Ref=""i2"" xmlns=""Test""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>";
        TestObjectWithDifferentPayload(valueSampleICollectionTExplicitWithCDC, netcorePayloadSampleICollectionTExplicitWithCDC, desktopPayloadSampleICollectionTExplicitWithCDC, setting);

        var valueSampleICollectionTExplicitWithCDCContainsPrivateDC = new SerializationTestTypes.ObjectContainer(new SerializationTestTypes.SampleICollectionTExplicitWithCDCContainsPrivateDC(true));
        string netcorePayloadSampleICollectionTExplicitWithCDCContainsPrivateDC = @"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleICollectionTExplicitWithCDCContainsPrivateDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionTExplicitWithCDCContainsPrivateDC***""><Item z:Id=""i2"" xmlns=""Test""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></Item><Item z:Id=""i3"" xmlns=""Test""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></Item><Item z:Ref=""i2"" xmlns=""Test""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>";
        string desktopPayloadSampleICollectionTExplicitWithCDCContainsPrivateDC = $@"<ObjectContainer xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><_data z:Id=""i1"" i:type=""a:SerializationTestTypes.SampleICollectionTExplicitWithCDCContainsPrivateDC***"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/"" xmlns:a=""http://schemas.datacontract.org/2004/07/SerializationTestTypes.SampleICollectionTExplicitWithCDCContainsPrivateDC***""><Item z:Id=""i2"" i:type=""b:SerializationTestTypes.PrivateDC"" xmlns=""Test"" xmlns:b=""{assemblyName}""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></Item><Item z:Id=""i3"" i:type=""b:SerializationTestTypes.PrivateDC"" xmlns=""Test"" xmlns:b=""{assemblyName}""><Data xmlns=""http://schemas.datacontract.org/2004/07/SerializationTestTypes"">7b4ac88f-972b-43e5-8f6a-5ae64480eaad</Data></Item><Item z:Ref=""i2"" xmlns=""Test""/></_data><_data2 z:Ref=""i1"" xmlns:z=""http://schemas.microsoft.com/2003/10/Serialization/""/></ObjectContainer>";
        TestObjectWithDifferentPayload(valueSampleICollectionTExplicitWithCDCContainsPrivateDC, netcorePayloadSampleICollectionTExplicitWithCDCContainsPrivateDC, desktopPayloadSampleICollectionTExplicitWithCDCContainsPrivateDC, setting);
    }

    [Fact]
    public static void DCS_BasicPerSerializerRoundTripAndCompare_Collections()
    {
        string assemblyName = typeof(DataContractSerializerTests).Assembly.FullName;
        string corelibAssemblyName = typeof(String).Assembly.FullName;

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.ContainsLinkedList(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.ContainsLinkedList***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.ContainsLinkedList***\"><Data><SimpleDCWithRef z:Id=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/></SimpleDCWithRef><SimpleDCWithRef z:Id=\"i3\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"><Data z:Id=\"i4\"><Data>This is a string</Data></Data><RefData z:Ref=\"i4\"/></SimpleDCWithRef><SimpleDCWithRef z:Ref=\"i3\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/><SimpleDCWithRef z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/><SimpleDCWithRef z:Id=\"i5\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"><Data z:Id=\"i6\"><Data>This is a string</Data></Data><RefData z:Id=\"i7\"><Data>This is a string</Data></RefData></SimpleDCWithRef><SimpleDCWithRef z:Id=\"i8\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"><Data z:Ref=\"i6\"/><RefData z:Ref=\"i6\"/></SimpleDCWithRef><SimpleDCWithRef z:Id=\"i9\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"><Data z:Id=\"i10\"><Data>This is a string</Data></Data><RefData z:Id=\"i11\"><Data>This is a string</Data></RefData></SimpleDCWithRef></Data></_data><_data2 i:type=\"a:SerializationTestTypes.ContainsLinkedList***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.ContainsLinkedList***\"><Data><SimpleDCWithRef z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/><SimpleDCWithRef z:Ref=\"i3\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/><SimpleDCWithRef z:Ref=\"i3\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/><SimpleDCWithRef z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/><SimpleDCWithRef z:Ref=\"i5\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/><SimpleDCWithRef z:Ref=\"i8\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/><SimpleDCWithRef z:Ref=\"i9\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></Data></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SimpleCDC(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.SimpleCDC***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleCDC***\"><Item>One</Item><Item>Two</Item><Item>two</Item></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SimpleCDC2(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.SimpleCDC2***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleCDC2***\"><Item>One</Item><Item>Two</Item><Item>two</Item></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.ContainsSimpleCDC(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.ContainsSimpleCDC***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.ContainsSimpleCDC***\"><data1 z:Id=\"i2\"><Item>One</Item><Item>Two</Item><Item>two</Item></data1><data2 z:Id=\"i3\"><Item>One</Item><Item>Two</Item><Item>two</Item></data2></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DMInCollection1(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DMInCollection1***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DMInCollection1***\"><Data1 z:Id=\"i2\"><Data>This is a string</Data></Data1><List1><SimpleDC z:Ref=\"i2\"/><SimpleDC z:Id=\"i3\"><Data>This is a string</Data></SimpleDC><SimpleDC z:Ref=\"i3\"/></List1></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DMWithRefInCollection1(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DMWithRefInCollection1***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DMWithRefInCollection1***\"><Data1 z:Id=\"i2\"><Data>This is a string</Data><RefData>This is a string</RefData></Data1><InnerData1>a6d053ed-f7d4-42fb-8e56-e4b425f26fa9</InnerData1><List1><SimpleDCWithSimpleDMRef z:Ref=\"i2\"/><SimpleDCWithSimpleDMRef z:Id=\"i3\"><Data>a6d053ed-f7d4-42fb-8e56-e4b425f26fa9</Data><RefData>This is a string</RefData></SimpleDCWithSimpleDMRef><SimpleDCWithSimpleDMRef z:Ref=\"i3\"/></List1></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");
    }

    [Fact]
    public static void DCS_BasicPerSerializerRoundTripAndCompare_CollectionDataContract()
    {
        string assemblyName = typeof(DataContractSerializerTests).Assembly.FullName;
        string corelibAssemblyName = typeof(String).Assembly.FullName;

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DMInCollection2(true), $"<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DMInCollection2***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DMInCollection2***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><InnerContent>This is a string</InnerContent><InnerInnerContent>This is a string</InnerInnerContent><List1><SimpleDC z:Ref=\"i2\"/><SimpleDC z:Id=\"i3\"><Data>This is a string</Data></SimpleDC><SimpleDC z:Id=\"i4\"><Data>This is a string</Data></SimpleDC><SimpleDC z:Id=\"i5\"><Data>This is a string</Data></SimpleDC><SimpleDC z:Id=\"i6\"><Data>This is a string</Data></SimpleDC></List1><List2 i:type=\"b:System.Collections.Generic.List`1[[SerializationTestTypes.SimpleDC, {assemblyName}]]\" xmlns:b=\"{corelibAssemblyName}\"><SimpleDC z:Ref=\"i2\"/><SimpleDC z:Ref=\"i6\"/></List2><List3 i:type=\"b:System.Collections.Generic.List`1[[SerializationTestTypes.SimpleDC, {assemblyName}]]\" xmlns:b=\"{corelibAssemblyName}\"><SimpleDC z:Ref=\"i2\"/><SimpleDC z:Ref=\"i3\"/><SimpleDC z:Ref=\"i4\"/><SimpleDC z:Ref=\"i5\"/><SimpleDC z:Ref=\"i6\"/></List3><List4 i:type=\"b:System.Collections.Generic.List`1[[SerializationTestTypes.SimpleDC, {assemblyName}]]\" xmlns:b=\"{corelibAssemblyName}\"><SimpleDC z:Ref=\"i2\"/><SimpleDC z:Ref=\"i6\"/></List4></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DMInDict1(true), $"<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DMInDict1***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DMInDict1***\"><Data1 z:Id=\"i2\"><Data>This is a string</Data></Data1><Data2 z:Id=\"i3\"><Data>This is a string</Data></Data2><Dict1 xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:KeyValueOfSimpleDCSimpleDCzETuxydO><b:Key z:Id=\"i4\"><Data>This is a string</Data></b:Key><b:Value z:Id=\"i5\"><Data>cd4f6d1f-db5e-49c9-bb43-13e73508a549</Data></b:Value></b:KeyValueOfSimpleDCSimpleDCzETuxydO><b:KeyValueOfSimpleDCSimpleDCzETuxydO><b:Key z:Ref=\"i2\"/><b:Value z:Ref=\"i3\"/></b:KeyValueOfSimpleDCSimpleDCzETuxydO><b:KeyValueOfSimpleDCSimpleDCzETuxydO><b:Key z:Id=\"i6\"><Data>This is a string</Data></b:Key><b:Value z:Ref=\"i5\"/></b:KeyValueOfSimpleDCSimpleDCzETuxydO><b:KeyValueOfSimpleDCSimpleDCzETuxydO><b:Key z:Ref=\"i5\"/><b:Value z:Id=\"i7\"><Data>This is a string</Data></b:Value></b:KeyValueOfSimpleDCSimpleDCzETuxydO></Dict1><Dict2 i:type=\"c:System.Collections.Generic.Dictionary`2[[SerializationTestTypes.SimpleDC, {assemblyName}],[SerializationTestTypes.SimpleDC, {assemblyName}]]\" xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\" xmlns:c=\"{corelibAssemblyName}\"><b:KeyValueOfSimpleDCSimpleDCzETuxydO><b:Key z:Ref=\"i4\"/><b:Value z:Ref=\"i5\"/></b:KeyValueOfSimpleDCSimpleDCzETuxydO><b:KeyValueOfSimpleDCSimpleDCzETuxydO><b:Key z:Ref=\"i2\"/><b:Value z:Ref=\"i3\"/></b:KeyValueOfSimpleDCSimpleDCzETuxydO><b:KeyValueOfSimpleDCSimpleDCzETuxydO><b:Key z:Ref=\"i6\"/><b:Value z:Ref=\"i5\"/></b:KeyValueOfSimpleDCSimpleDCzETuxydO><b:KeyValueOfSimpleDCSimpleDCzETuxydO><b:Key z:Ref=\"i5\"/><b:Value z:Ref=\"i7\"/></b:KeyValueOfSimpleDCSimpleDCzETuxydO></Dict2><InnerData1>This is a string</InnerData1><InnerInnerData1>cd4f6d1f-db5e-49c9-bb43-13e73508a549</InnerInnerData1><Kvp1 xmlns:b=\"http://schemas.datacontract.org/2004/07/System.Collections.Generic\"><b:key z:Ref=\"i5\"/><b:value z:Ref=\"i7\"/></Kvp1></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DMWithRefInCollection2(true), $"<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DMWithRefInCollection2***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DMWithRefInCollection2***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><InnerContent>This is a string</InnerContent><InnerInnerContent>This is a string</InnerInnerContent><List1><SimpleDCWithRef z:Id=\"i3\"><Data z:Ref=\"i2\"/><RefData z:Ref=\"i2\"/></SimpleDCWithRef><SimpleDCWithRef z:Id=\"i4\"><Data z:Id=\"i5\"><Data>This is a string</Data></Data><RefData z:Ref=\"i5\"/></SimpleDCWithRef><SimpleDCWithRef z:Id=\"i6\"><Data z:Id=\"i7\"><Data>This is a string</Data></Data><RefData z:Ref=\"i7\"/></SimpleDCWithRef><SimpleDCWithRef z:Id=\"i8\"><Data z:Id=\"i9\"><Data>This is a string</Data></Data><RefData z:Ref=\"i9\"/></SimpleDCWithRef><SimpleDCWithRef z:Id=\"i10\"><Data z:Id=\"i11\"><Data>This is a string</Data></Data><RefData z:Ref=\"i11\"/></SimpleDCWithRef></List1><List2 i:type=\"b:System.Collections.Generic.List`1[[SerializationTestTypes.SimpleDCWithRef, {assemblyName}]]\" xmlns:b=\"{corelibAssemblyName}\"><SimpleDCWithRef z:Ref=\"i3\"/><SimpleDCWithRef z:Ref=\"i10\"/></List2><List3 i:type=\"b:System.Collections.Generic.List`1[[SerializationTestTypes.SimpleDCWithRef, {assemblyName}]]\" xmlns:b=\"{corelibAssemblyName}\"><SimpleDCWithRef z:Ref=\"i3\"/><SimpleDCWithRef z:Ref=\"i4\"/><SimpleDCWithRef z:Ref=\"i6\"/><SimpleDCWithRef z:Ref=\"i8\"/><SimpleDCWithRef z:Ref=\"i10\"/></List3><List4 i:type=\"b:System.Collections.Generic.List`1[[SerializationTestTypes.SimpleDCWithRef, {assemblyName}]]\" xmlns:b=\"{corelibAssemblyName}\"><SimpleDCWithRef z:Ref=\"i3\"/><SimpleDCWithRef z:Ref=\"i10\"/></List4><List5><SimpleDC z:Ref=\"i2\"/><SimpleDC z:Id=\"i12\"><Data>This is a string</Data></SimpleDC><SimpleDC z:Ref=\"i2\"/></List5><List6 xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:anyType z:Id=\"i13\" i:type=\"c:SerializationTestTypes.SimpleDC***\" xmlns:c=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleDC***\"><Data>This is a string</Data></b:anyType><b:anyType z:Id=\"i14\" i:type=\"c:SerializationTestTypes.SimpleDCWithRef***\" xmlns:c=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleDCWithRef***\"><Data z:Id=\"i15\"><Data>This is a string</Data></Data><RefData z:Ref=\"i15\"/></b:anyType><b:anyType z:Id=\"i16\" i:type=\"c:SerializationTestTypes.SimpleDCWithSimpleDMRef***\" xmlns:c=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleDCWithSimpleDMRef***\"><Data>This is a string</Data><RefData>This is a string</RefData></b:anyType><b:anyType i:type=\"ArrayOfSimpleDC\"/><b:anyType i:type=\"ArrayOfSimpleDCWithRef\"/><b:anyType i:type=\"ArrayOfSimpleDCWithSimpleDMRef\"/><b:anyType i:type=\"ArrayOfSimpleDCWithRef\"><SimpleDCWithRef z:Ref=\"i3\"/><SimpleDCWithRef z:Ref=\"i4\"/><SimpleDCWithRef z:Ref=\"i6\"/><SimpleDCWithRef z:Ref=\"i8\"/><SimpleDCWithRef z:Ref=\"i10\"/></b:anyType><b:anyType i:type=\"ArrayOfSimpleDCWithRef\"><SimpleDCWithRef z:Ref=\"i3\"/><SimpleDCWithRef z:Ref=\"i10\"/></b:anyType><b:anyType i:type=\"ArrayOfSimpleDCWithRef\"><SimpleDCWithRef z:Ref=\"i3\"/><SimpleDCWithRef z:Ref=\"i4\"/><SimpleDCWithRef z:Ref=\"i6\"/><SimpleDCWithRef z:Ref=\"i8\"/><SimpleDCWithRef z:Ref=\"i10\"/></b:anyType><b:anyType i:type=\"ArrayOfSimpleDCWithRef\"><SimpleDCWithRef z:Ref=\"i3\"/><SimpleDCWithRef z:Ref=\"i10\"/></b:anyType><b:anyType i:type=\"ArrayOfSimpleDC\"><SimpleDC z:Ref=\"i2\"/><SimpleDC z:Ref=\"i12\"/><SimpleDC z:Ref=\"i2\"/></b:anyType><b:anyType z:Ref=\"i2\"/><b:anyType i:type=\"c:string\" xmlns:c=\"http://www.w3.org/2001/XMLSchema\">This is a string</b:anyType><b:anyType i:type=\"c:string\" xmlns:c=\"http://www.w3.org/2001/XMLSchema\">This is a string</b:anyType></List6></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DMWithRefInDict1(true), $"<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DMWithRefInDict1***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DMWithRefInDict1***\"><Data1 z:Id=\"i2\"><Data z:Id=\"i3\"><Data>This is a string</Data></Data><RefData z:Ref=\"i3\"/></Data1><Data2 z:Id=\"i4\"><Data z:Id=\"i5\"><Data>This is a string</Data></Data><RefData z:Ref=\"i5\"/></Data2><Dict1 xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:KeyValueOfSimpleDCWithRefSimpleDCWithRefzETuxydO><b:Key z:Id=\"i6\"><Data z:Id=\"i7\"><Data>This is a string</Data></Data><RefData z:Ref=\"i7\"/></b:Key><b:Value z:Id=\"i8\"><Data z:Id=\"i9\"><Data>6d807157-536f-4794-a157-e463a11029aa</Data></Data><RefData z:Ref=\"i9\"/></b:Value></b:KeyValueOfSimpleDCWithRefSimpleDCWithRefzETuxydO><b:KeyValueOfSimpleDCWithRefSimpleDCWithRefzETuxydO><b:Key z:Ref=\"i2\"/><b:Value z:Ref=\"i4\"/></b:KeyValueOfSimpleDCWithRefSimpleDCWithRefzETuxydO><b:KeyValueOfSimpleDCWithRefSimpleDCWithRefzETuxydO><b:Key z:Id=\"i10\"><Data z:Id=\"i11\"><Data>This is a string</Data></Data><RefData z:Ref=\"i11\"/></b:Key><b:Value z:Ref=\"i8\"/></b:KeyValueOfSimpleDCWithRefSimpleDCWithRefzETuxydO><b:KeyValueOfSimpleDCWithRefSimpleDCWithRefzETuxydO><b:Key z:Ref=\"i8\"/><b:Value z:Id=\"i12\"><Data z:Id=\"i13\"><Data>This is a string</Data></Data><RefData z:Ref=\"i13\"/></b:Value></b:KeyValueOfSimpleDCWithRefSimpleDCWithRefzETuxydO></Dict1><Dict2 i:type=\"c:System.Collections.Generic.Dictionary`2[[SerializationTestTypes.SimpleDCWithRef, {assemblyName}],[SerializationTestTypes.SimpleDCWithRef, {assemblyName}]]\" xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\" xmlns:c=\"{corelibAssemblyName}\"><b:KeyValueOfSimpleDCWithRefSimpleDCWithRefzETuxydO><b:Key z:Ref=\"i6\"/><b:Value z:Ref=\"i8\"/></b:KeyValueOfSimpleDCWithRefSimpleDCWithRefzETuxydO><b:KeyValueOfSimpleDCWithRefSimpleDCWithRefzETuxydO><b:Key z:Ref=\"i2\"/><b:Value z:Ref=\"i4\"/></b:KeyValueOfSimpleDCWithRefSimpleDCWithRefzETuxydO><b:KeyValueOfSimpleDCWithRefSimpleDCWithRefzETuxydO><b:Key z:Ref=\"i10\"/><b:Value z:Ref=\"i8\"/></b:KeyValueOfSimpleDCWithRefSimpleDCWithRefzETuxydO><b:KeyValueOfSimpleDCWithRefSimpleDCWithRefzETuxydO><b:Key z:Ref=\"i8\"/><b:Value z:Ref=\"i12\"/></b:KeyValueOfSimpleDCWithRefSimpleDCWithRefzETuxydO></Dict2><InnerData1 z:Ref=\"i3\"/><InnerInnerData1>6d807157-536f-4794-a157-e463a11029aa</InnerInnerData1><Kvp1 xmlns:b=\"http://schemas.datacontract.org/2004/07/System.Collections.Generic\"><b:key z:Ref=\"i8\"/><b:value z:Ref=\"i12\"/></Kvp1></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");
    }

    [Fact]
    public static void DCS_BasicPerSerializerRoundTripAndCompare_ItRef()
    {
        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.TestInheritance9(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.TestInheritance9***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance9***\"><base1 i:type=\"b:SerializationTestTypes.Derived2Serializable***\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived2Serializable***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></base1><baseDC i:type=\"b:SerializationTestTypes.DerivedSerializable***\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedSerializable***\"><data>TestString</data><data2>TestString2</data2><days xmlns:c=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><c:string>Base1</c:string><c:string>Base2</c:string><c:string>Base3</c:string><c:string>Base4</c:string><c:string>Base5</c:string><c:string>Base6</c:string><c:string>Base7</c:string></days><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3></baseDC><derived2><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></derived2><derivedDC><data>TestString</data><data2>TestString2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3></derivedDC></_data><_data2 i:type=\"a:SerializationTestTypes.TestInheritance9***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance9***\"><base1 i:type=\"b:SerializationTestTypes.Derived2Serializable***\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived2Serializable***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></base1><baseDC i:type=\"b:SerializationTestTypes.DerivedSerializable***\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedSerializable***\"><data>TestString</data><data2>TestString2</data2><days xmlns:c=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><c:string>Base1</c:string><c:string>Base2</c:string><c:string>Base3</c:string><c:string>Base4</c:string><c:string>Base5</c:string><c:string>Base6</c:string><c:string>Base7</c:string></days><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3></baseDC><derived2><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></derived2><derivedDC><data>TestString</data><data2>TestString2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3></derivedDC></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SimpleDC(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.SimpleDC***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleDC***\"><Data>This is a string</Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SimpleDCWithSimpleDMRef(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.SimpleDCWithSimpleDMRef***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleDCWithSimpleDMRef***\"><Data>This is a string</Data><RefData>This is a string</RefData></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SimpleDCWithRef(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.SimpleDCWithRef***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleDCWithRef***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.ContainsSimpleDCWithRef(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.ContainsSimpleDCWithRef***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.ContainsSimpleDCWithRef***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data>This is a string</Data></Data><RefData z:Ref=\"i3\"/></Data><RefData z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SimpleDCWithIsRequiredFalse(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.SimpleDCWithIsRequiredFalse***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SimpleDCWithIsRequiredFalse***\"><Data>This is a string</Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Mixed1(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.Mixed1***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Mixed1***\"><Data1 z:Id=\"i2\"><Data>This is a string</Data></Data1><Data2 z:Id=\"i3\"><Data z:Id=\"i4\"><Data>This is a string</Data></Data><RefData z:Ref=\"i4\"/></Data2><Data3 z:Id=\"i5\"><Data>This is a string</Data><RefData>This is a string</RefData></Data3><Data4><Data>This is a string</Data></Data4><Data5><Data><Data>This is a string</Data></Data><RefData><Data>This is a string</Data></RefData></Data5></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SerIser(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.SerIser***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SerIser***\"><containedData z:Id=\"i1\" i:type=\"b:SerializationTestTypes.PublicDC***\" xmlns=\"\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.PublicDC***\"><Data xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\">55cb1688-dec7-4106-a6d8-7e57590cb20a</Data></containedData></_data><_data2 i:type=\"a:SerializationTestTypes.SerIser***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SerIser***\"><containedData z:Ref=\"i1\" xmlns=\"\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCVersioned1(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DCVersioned1***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCVersioned1***\"><Data z:Id=\"i2\" xmlns=\"SerializationTestTypes.ExtensionData\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\"><b:Data>This is a string</b:Data></Data><RefData z:Ref=\"i2\" xmlns=\"SerializationTestTypes.ExtensionData\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCVersioned2(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DCVersioned2***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCVersioned2***\"><Data z:Id=\"i2\" xmlns=\"SerializationTestTypes.ExtensionData\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\"><b:Data>This is a string</b:Data></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCVersionedContainer1(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DCVersionedContainer1***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCVersionedContainer1***\"><DataVersion1 z:Id=\"i2\" xmlns=\"SerializationTestTypes.ExtensionData\"><Data z:Id=\"i3\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\"><b:Data>This is a string</b:Data></Data><RefData z:Ref=\"i3\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\"/></DataVersion1></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCVersionedContainerVersion1(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DCVersionedContainerVersion1***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCVersionedContainerVersion1***\"><DataVersion1 z:Id=\"i2\" xmlns=\"SerializationTestTypes.ExtensionData\"><Data z:Id=\"i3\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\"><b:Data>This is a string</b:Data></Data><RefData z:Ref=\"i3\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\"/></DataVersion1><DataVersion2 z:Id=\"i4\" xmlns=\"SerializationTestTypes.ExtensionData\"><Data z:Id=\"i5\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\"><b:Data>This is a string</b:Data></Data></DataVersion2><RefDataVersion1 z:Ref=\"i2\" xmlns=\"SerializationTestTypes.ExtensionData\"/><RefDataVersion2 z:Ref=\"i4\" xmlns=\"SerializationTestTypes.ExtensionData\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCVersionedContainerVersion2(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DCVersionedContainerVersion2***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCVersionedContainerVersion2***\"><DataVersion1 z:Id=\"i2\" xmlns=\"SerializationTestTypes.ExtensionData\"><Data z:Id=\"i3\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\"><b:Data>This is a string</b:Data></Data><RefData z:Ref=\"i3\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\"/></DataVersion1><DataVersion2 z:Id=\"i4\" xmlns=\"SerializationTestTypes.ExtensionData\"><Data z:Id=\"i5\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\"><b:Data>This is a string</b:Data></Data></DataVersion2></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DCVersionedContainerVersion3(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DCVersionedContainerVersion3***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCVersionedContainerVersion3***\"><DCVersioned1 z:Id=\"i2\" i:type=\"b:SerializationTestTypes.DCVersioned1***\" xmlns=\"SerializationTestTypes.ExtensionData\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DCVersioned1***\"><Data z:Id=\"i3\" xmlns:c=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\"><c:Data>This is a string</c:Data></Data><RefData z:Ref=\"i3\" xmlns:c=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\"/></DCVersioned1><NewRefDataVersion1 z:Ref=\"i2\" xmlns=\"SerializationTestTypes.ExtensionData\"/><RefDataVersion2 i:nil=\"true\" xmlns=\"SerializationTestTypes.ExtensionData\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.BaseDC(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.BaseDC***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.BaseDC***\"><data>TestString</data><data2>TestString2</data2></_data><_data2 i:type=\"a:SerializationTestTypes.BaseDC***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.BaseDC***\"><data>TestString</data><data2>TestString2</data2></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.BaseSerializable(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.BaseSerializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.BaseSerializable***\"><data>TestString</data><data2>TestString2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days></_data><_data2 i:type=\"a:SerializationTestTypes.BaseSerializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.BaseSerializable***\"><data>TestString</data><data2>TestString2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedDC(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.DerivedDC***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedDC***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3></_data><_data2 i:type=\"a:SerializationTestTypes.DerivedDC***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedDC***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedSerializable(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.DerivedSerializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedSerializable***\"><data>TestString</data><data2>TestString2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3></_data><_data2 i:type=\"a:SerializationTestTypes.DerivedSerializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedSerializable***\"><data>TestString</data><data2>TestString2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedDCIsRefBaseSerializable(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.DerivedDCIsRefBaseSerializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedDCIsRefBaseSerializable***\"><data i:nil=\"true\"/><data2 i:nil=\"true\"/><days i:nil=\"true\" xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/><Data33 i:nil=\"true\"/></_data><_data2 i:type=\"a:SerializationTestTypes.DerivedDCIsRefBaseSerializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedDCIsRefBaseSerializable***\"><data i:nil=\"true\"/><data2 i:nil=\"true\"/><days i:nil=\"true\" xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/><Data33 i:nil=\"true\"/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedDCBaseSerializable(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.DerivedDCBaseSerializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedDCBaseSerializable***\"><data i:nil=\"true\"/><data2 i:nil=\"true\"/><days i:nil=\"true\" xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/><Data33 i:nil=\"true\"/></_data><_data2 i:type=\"a:SerializationTestTypes.DerivedDCBaseSerializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedDCBaseSerializable***\"><data i:nil=\"true\"/><data2 i:nil=\"true\"/><days i:nil=\"true\" xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/><Data33 i:nil=\"true\"/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Derived2DC(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Derived2DC***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived2DC***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data11>TestString11</data11><data12>TestString12</data12><data4>TestString4</data4></_data><_data2 i:type=\"a:SerializationTestTypes.Derived2DC***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived2DC***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data11>TestString11</data11><data12>TestString12</data12><data4>TestString4</data4></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.BaseDCNoIsRef(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.BaseDCNoIsRef***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.BaseDCNoIsRef***\"><_data/></_data><_data2 i:type=\"a:SerializationTestTypes.BaseDCNoIsRef***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.BaseDCNoIsRef***\"><_data/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedPOCOBaseDCNOISRef(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.DerivedPOCOBaseDCNOISRef***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedPOCOBaseDCNOISRef***\"><_data/><Data22 i:nil=\"true\"/></_data><_data2 i:type=\"a:SerializationTestTypes.DerivedPOCOBaseDCNOISRef***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedPOCOBaseDCNOISRef***\"><_data/><Data22 i:nil=\"true\"/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedIXmlSerializable_POCOBaseDCNOISRef(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.DerivedIXmlSerializable_POCOBaseDCNOISRef***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedIXmlSerializable_POCOBaseDCNOISRef***\">TestString</_data><_data2 i:type=\"a:SerializationTestTypes.DerivedIXmlSerializable_POCOBaseDCNOISRef***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedIXmlSerializable_POCOBaseDCNOISRef***\">TestString</_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedCDCFromBaseDC(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.DerivedCDCFromBaseDC***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedCDCFromBaseDC***\"/><_data2 i:type=\"a:SerializationTestTypes.DerivedCDCFromBaseDC***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedCDCFromBaseDC***\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Derived2Serializable(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Derived2Serializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived2Serializable***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></_data><_data2 i:type=\"a:SerializationTestTypes.Derived2Serializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived2Serializable***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Derived2SerializablePositive(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Derived2SerializablePositive***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived2SerializablePositive***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></_data><_data2 i:type=\"a:SerializationTestTypes.Derived2SerializablePositive***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived2SerializablePositive***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Derived2Derived2Serializable(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Derived2Derived2Serializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived2Derived2Serializable***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></_data><_data2 i:type=\"a:SerializationTestTypes.Derived2Derived2Serializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived2Derived2Serializable***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Derived3Derived2Serializable(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Derived3Derived2Serializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived3Derived2Serializable***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></_data><_data2 i:type=\"a:SerializationTestTypes.Derived3Derived2Serializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived3Derived2Serializable***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Derived31Derived2SerializablePOCO(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Derived31Derived2SerializablePOCO***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived31Derived2SerializablePOCO***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4><RefData z:Id=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/></RefData><SimpleDCWithRefData z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></_data><_data2 i:type=\"a:SerializationTestTypes.Derived31Derived2SerializablePOCO***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived31Derived2SerializablePOCO***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4><RefData z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/><SimpleDCWithRefData z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Derived4Derived2Serializable(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Derived4Derived2Serializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived4Derived2Serializable***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></_data><_data2 i:type=\"a:SerializationTestTypes.Derived4Derived2Serializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived4Derived2Serializable***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Derived5Derived2Serializable(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Derived5Derived2Serializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived5Derived2Serializable***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></_data><_data2 i:type=\"a:SerializationTestTypes.Derived5Derived2Serializable***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived5Derived2Serializable***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Derived6Derived2SerializablePOCO(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Derived6Derived2SerializablePOCO***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived6Derived2SerializablePOCO***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4><RefData z:Id=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/></RefData><SimpleDCWithRefData z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></_data><_data2 i:type=\"a:SerializationTestTypes.Derived6Derived2SerializablePOCO***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived6Derived2SerializablePOCO***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4><RefData z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/><SimpleDCWithRefData z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.BaseWithIsRefTrue(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.BaseWithIsRefTrue***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.BaseWithIsRefTrue***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedNoIsRef(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DerivedNoIsRef***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedNoIsRef***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedNoIsRef2(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DerivedNoIsRef2***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedNoIsRef2***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/><RefData2 z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedNoIsRef3(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DerivedNoIsRef3***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedNoIsRef3***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/><RefData2 z:Ref=\"i2\"/><RefData3 z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedNoIsRef4(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DerivedNoIsRef4***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedNoIsRef4***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/><RefData2 z:Ref=\"i2\"/><RefData3 z:Ref=\"i2\"/><RefData4 z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedNoIsRef5(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DerivedNoIsRef5***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedNoIsRef5***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/><RefData2 z:Ref=\"i2\"/><RefData3 z:Ref=\"i2\"/><RefData4 z:Ref=\"i2\"/><RefData5 z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedNoIsRefWithIsRefTrue6(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DerivedNoIsRefWithIsRefTrue6***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedNoIsRefWithIsRefTrue6***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/><RefData2 z:Ref=\"i2\"/><RefData3 z:Ref=\"i2\"/><RefData4 z:Ref=\"i2\"/><RefData5 z:Ref=\"i2\"/><RefData6 z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedWithIsRefFalse(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DerivedWithIsRefFalse***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedWithIsRefFalse***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedWithIsRefFalse2(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DerivedWithIsRefFalse2***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedWithIsRefFalse2***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/><RefData2 z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedWithIsRefFalse3(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DerivedWithIsRefFalse3***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedWithIsRefFalse3***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/><RefData2 z:Ref=\"i2\"/><RefData3 z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedWithIsRefFalse4(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DerivedWithIsRefFalse4***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedWithIsRefFalse4***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/><RefData2 z:Ref=\"i2\"/><RefData3 z:Ref=\"i2\"/><RefData4 z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedWithIsRefFalse5(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DerivedWithIsRefFalse5***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedWithIsRefFalse5***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/><RefData2 z:Ref=\"i2\"/><RefData3 z:Ref=\"i2\"/><RefData4 z:Ref=\"i2\"/><RefData5 z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedWithIsRefTrue6(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DerivedWithIsRefTrue6***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedWithIsRefTrue6***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/><RefData2 z:Ref=\"i2\"/><RefData3 z:Ref=\"i2\"/><RefData4 z:Ref=\"i2\"/><RefData5 z:Ref=\"i2\"/><RefData6 z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedWithIsRefTrueExplicit(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DerivedWithIsRefTrueExplicit***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedWithIsRefTrueExplicit***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedWithIsRefTrueExplicit2(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.DerivedWithIsRefTrueExplicit2***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedWithIsRefTrueExplicit2***\"><Data z:Id=\"i2\"><Data>This is a string</Data></Data><RefData z:Ref=\"i2\"/><RefData2 z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.BaseNoIsRef(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.BaseNoIsRef***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.BaseNoIsRef***\"><Data z:Id=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"><Data>This is a string</Data></Data></_data><_data2 i:type=\"a:SerializationTestTypes.BaseNoIsRef***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.BaseNoIsRef***\"><Data z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedWithIsRefFalseExplicit(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.DerivedWithIsRefFalseExplicit***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedWithIsRefFalseExplicit***\"><Data z:Id=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"><Data>This is a string</Data></Data><RefData z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></_data><_data2 i:type=\"a:SerializationTestTypes.DerivedWithIsRefFalseExplicit***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedWithIsRefFalseExplicit***\"><Data z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/><RefData z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.TestInheritance(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.TestInheritance***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance***\"><baseDC i:type=\"b:SerializationTestTypes.DerivedDC***\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedDC***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3></baseDC><derivedDC><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3></derivedDC></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.TestInheritance91(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.TestInheritance91***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance91***\"><base1 i:type=\"b:SerializationTestTypes.Derived2Serializable***\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived2Serializable***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></base1><baseDC i:type=\"b:SerializationTestTypes.DerivedSerializable***\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedSerializable***\"><data>TestString</data><data2>TestString2</data2><days xmlns:c=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><c:string>Base1</c:string><c:string>Base2</c:string><c:string>Base3</c:string><c:string>Base4</c:string><c:string>Base5</c:string><c:string>Base6</c:string><c:string>Base7</c:string></days><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3></baseDC><derived2 i:type=\"b:SerializationTestTypes.Derived3Derived2Serializable***\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived3Derived2Serializable***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></derived2><derived3><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></derived3><derivedDC><data>TestString</data><data2>TestString2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3></derivedDC></_data><_data2 i:type=\"a:SerializationTestTypes.TestInheritance91***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance91***\"><base1 i:type=\"b:SerializationTestTypes.Derived2Serializable***\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived2Serializable***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></base1><baseDC i:type=\"b:SerializationTestTypes.DerivedSerializable***\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedSerializable***\"><data>TestString</data><data2>TestString2</data2><days xmlns:c=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><c:string>Base1</c:string><c:string>Base2</c:string><c:string>Base3</c:string><c:string>Base4</c:string><c:string>Base5</c:string><c:string>Base6</c:string><c:string>Base7</c:string></days><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3></baseDC><derived2 i:type=\"b:SerializationTestTypes.Derived3Derived2Serializable***\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived3Derived2Serializable***\"><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></derived2><derived3><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>TestString00</data00><data122>TestString122</data122><data4>TestString4</data4></derived3><derivedDC><data>TestString</data><data2>TestString2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3></derivedDC></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.TestInheritance5(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.TestInheritance5***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance5***\"><baseDC i:nil=\"true\"/><derivedDC i:nil=\"true\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.TestInheritance10(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.TestInheritance10***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance10***\"/><_data2 i:type=\"a:SerializationTestTypes.TestInheritance10***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance10***\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.TestInheritance2(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.TestInheritance2***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance2***\"><baseDC><data>String3</data><data2>String3</data2></baseDC><derivedDC><data>TestString</data><data2>TestString2</data2><data0>String1</data0><data1>String2</data1><data3>TestString3</data3></derivedDC></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.TestInheritance11(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.TestInheritance11***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance11***\"><baseDC><data>String3</data><data2>String3</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days></baseDC><derivedDC><data>TestString</data><data2>TestString2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days><data0>String1</data0><data1>String2</data1><data3>TestString3</data3></derivedDC></_data><_data2 i:type=\"a:SerializationTestTypes.TestInheritance11***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance11***\"><baseDC><data>String3</data><data2>String3</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days></baseDC><derivedDC><data>TestString</data><data2>TestString2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days><data0>String1</data0><data1>String2</data1><data3>TestString3</data3></derivedDC></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.TestInheritance3(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.TestInheritance3***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance3***\"><baseDC><data>String1</data><data2>String2</data2></baseDC><derivedDC><data>TestString1</data><data2>TestString2</data2><data0>String3</data0><data1>String3</data1><data3>TestString3</data3></derivedDC></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.TestInheritance16(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.TestInheritance16***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance16***\"><baseDC><data>String1</data><data2>String2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days></baseDC><derivedDC><data>TestString1</data><data2>TestString2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days><data0>String3</data0><data1>String3</data1><data3>TestString3</data3></derivedDC></_data><_data2 i:type=\"a:SerializationTestTypes.TestInheritance16***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance16***\"><baseDC><data>String1</data><data2>String2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days></baseDC><derivedDC><data>TestString1</data><data2>TestString2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days><data0>String3</data0><data1>String3</data1><data3>TestString3</data3></derivedDC></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.TestInheritance4(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.TestInheritance4***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance4***\"><baseDC><data>TestString</data><data2>TestString2</data2></baseDC><derivedDC><data>TestString2</data><data2>String3</data2><data0>String3</data0><data1>String2</data1><data3>TestString3</data3></derivedDC></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.TestInheritance12(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.TestInheritance12***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance12***\"><baseDC><data>TestString</data><data2>TestString2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days></baseDC><derivedDC><data>TestString2</data><data2>String3</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days><data0>String3</data0><data1>String2</data1><data3>TestString3</data3></derivedDC></_data><_data2 i:type=\"a:SerializationTestTypes.TestInheritance12***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance12***\"><baseDC><data>TestString</data><data2>TestString2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days></baseDC><derivedDC><data>TestString2</data><data2>String3</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days><data0>String3</data0><data1>String2</data1><data3>TestString3</data3></derivedDC></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.TestInheritance6(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.TestInheritance6***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance6***\"><baseDC><data>TestString</data><data2>TestString2</data2></baseDC><derived2DC><data>TestString3</data><data2>String4</data2><data0>TestString0</data0><data1>TestString2</data1><data3>TestString3</data3><data11>String2</data11><data12>String4</data12><data4>String3</data4></derived2DC><derivedDC><data>TestString</data><data2>TestString2</data2><data0>String1</data0><data1>String3</data1><data3>String2</data3></derivedDC></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.TestInheritance7(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.TestInheritance7***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance7***\"><baseDC><data>String1</data><data2>String2</data2></baseDC><derived2DC><data>TestString2</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data11>String2</data11><data12>TestString12</data12><data4>TestString4</data4></derived2DC></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.TestInheritance14(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.TestInheritance14***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance14***\"><baseDC><data>String1</data><data2>String2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days></baseDC><derived2DC><data>TestString2</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>String2</data00><data122>TestString122</data122><data4>TestString4</data4></derived2DC></_data><_data2 i:type=\"a:SerializationTestTypes.TestInheritance14***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance14***\"><baseDC><data>String1</data><data2>String2</data2><days xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>Base1</b:string><b:string>Base2</b:string><b:string>Base3</b:string><b:string>Base4</b:string><b:string>Base5</b:string><b:string>Base6</b:string><b:string>Base7</b:string></days></baseDC><derived2DC><data>TestString2</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data00>String2</data00><data122>TestString122</data122><data4>TestString4</data4></derived2DC></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.TestInheritance8(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.TestInheritance8***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.TestInheritance8***\"><baseDC><data>String2</data><data2>TestString2</data2></baseDC><derived2DC><data>TestString</data><data2>TestString2</data2><data0>TestString0</data0><data1>TestString1</data1><data3>TestString3</data3><data11>String1</data11><data12>String2</data12><data4>TestString4</data4></derived2DC></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

    }

    [Fact]
    public static void DCS_BasicPerSerializerRoundTripAndCompare_SelfRefCycles()
    {
        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SelfRef1(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.SelfRef1***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SelfRef1***\"><Data z:Ref=\"i1\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SelfRef1DoubleDM(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.SelfRef1DoubleDM***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SelfRef1DoubleDM***\"><Data z:Ref=\"i1\"/><Data2 z:Ref=\"i1\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SelfRef2(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.SelfRef2***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SelfRef2***\"><Data z:Id=\"i2\"><Data z:Ref=\"i2\"/></Data><RefData z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SelfRef3(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.SelfRef3***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SelfRef3***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Ref=\"i3\"/></Data><RefData z:Ref=\"i3\"/></Data><RefData z:Ref=\"i2\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Cyclic1(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.Cyclic1***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Cyclic1***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Ref=\"i2\"/></Data></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Cyclic2(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.Cyclic2***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Cyclic2***\"><Data z:Id=\"i2\"><Data z:Ref=\"i1\"/></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CyclicA(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.CyclicA***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.CyclicA***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Ref=\"i1\"/></Data></Data></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CyclicB(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.CyclicB***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.CyclicB***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Ref=\"i1\"/></Data></Data></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CyclicC(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.CyclicC***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.CyclicC***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Ref=\"i1\"/></Data></Data></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CyclicD(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.CyclicD***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.CyclicD***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Ref=\"i1\"/></Data></Data></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CyclicABCD1(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.CyclicABCD1***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.CyclicABCD1***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Id=\"i5\"><Data z:Ref=\"i2\"/></Data></Data></Data></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CyclicABCD2(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.CyclicABCD2***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.CyclicABCD2***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Id=\"i5\"><Data i:nil=\"true\"/></Data></Data></Data></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CyclicABCD3(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.CyclicABCD3***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.CyclicABCD3***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Id=\"i5\"><Data z:Id=\"i6\"><Data z:Id=\"i7\"><Data z:Id=\"i8\"><Data z:Id=\"i9\"><Data z:Ref=\"i6\"/></Data></Data></Data></Data></Data></Data></Data></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CyclicABCD4(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.CyclicABCD4***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.CyclicABCD4***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Id=\"i5\"><Data z:Id=\"i6\"><Data z:Id=\"i7\"><Data z:Id=\"i8\"><Data z:Id=\"i9\"><Data z:Id=\"i10\"><Data z:Ref=\"i7\"/></Data></Data></Data></Data></Data></Data></Data></Data></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CyclicABCD5(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.CyclicABCD5***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.CyclicABCD5***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Id=\"i5\"><Data z:Id=\"i6\"><Data z:Id=\"i7\"><Data z:Id=\"i8\"><Data z:Id=\"i9\"><Data z:Ref=\"i2\"/></Data></Data></Data></Data></Data></Data></Data></Data><Data2 z:Ref=\"i6\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CyclicABCD6(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.CyclicABCD6***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.CyclicABCD6***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Id=\"i5\"><Data z:Id=\"i6\"><Data z:Id=\"i7\"><Data z:Id=\"i8\"><Data z:Id=\"i9\"><Data z:Ref=\"i2\"/></Data></Data></Data></Data></Data></Data></Data></Data><Data2 z:Ref=\"i6\"/><Data3 z:Ref=\"i3\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CyclicABCD7(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.CyclicABCD7***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.CyclicABCD7***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Id=\"i5\"><Data z:Ref=\"i2\"/></Data></Data></Data></Data><Data2 z:Id=\"i6\"><Data z:Id=\"i7\"><Data z:Id=\"i8\"><Data z:Id=\"i9\"><Data z:Ref=\"i2\"/></Data></Data></Data></Data2><Data3 z:Ref=\"i3\"/><Data4 z:Id=\"i10\"><Data z:Id=\"i11\"><Data z:Ref=\"i6\"/></Data></Data4></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CyclicABCD8(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.CyclicABCD8***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.CyclicABCD8***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Id=\"i5\"><Data z:Ref=\"i2\"/></Data></Data></Data></Data><Data2 z:Id=\"i6\"><Data z:Id=\"i7\"><Data z:Id=\"i8\"><Data z:Id=\"i9\"><Data z:Ref=\"i6\"/></Data></Data></Data></Data2><Data3 z:Ref=\"i7\"/><Data4 z:Id=\"i10\"><Data z:Id=\"i11\"><Data z:Ref=\"i2\"/></Data></Data4><Data5 z:Ref=\"i11\"/></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CyclicABCDNoCycles(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.CyclicABCDNoCycles***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.CyclicABCDNoCycles***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Id=\"i5\"><Data z:Id=\"i6\"><Data z:Id=\"i7\"><Data z:Id=\"i8\"><Data z:Id=\"i9\"><Data z:Ref=\"i6\"/></Data></Data></Data></Data></Data></Data></Data></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.A1(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.A1***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.A1***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Id=\"i5\"><Data z:Id=\"i6\"><Data z:Id=\"i7\"><Data z:Ref=\"i4\"/></Data><Data2 z:Id=\"i8\"><Data z:Ref=\"i6\"/></Data2></Data></Data></Data></Data><Data2 z:Id=\"i9\"><Data z:Id=\"i10\"><Data z:Id=\"i11\"><Data z:Id=\"i12\"><Data z:Id=\"i13\"><Data z:Id=\"i14\"><Data z:Id=\"i15\"><Data z:Ref=\"i12\"/></Data><Data2 z:Id=\"i16\"><Data z:Ref=\"i14\"/></Data2></Data></Data></Data></Data><Data2 z:Id=\"i17\"><Data i:nil=\"true\"/></Data2></Data></Data2></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.B1(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.B1***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.B1***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Id=\"i5\"><Data z:Id=\"i6\"><Data z:Ref=\"i3\"/></Data><Data2 z:Id=\"i7\"><Data z:Ref=\"i5\"/></Data2></Data></Data></Data></Data><Data2 z:Id=\"i8\"><Data z:Id=\"i9\"><Data z:Id=\"i10\"><Data z:Id=\"i11\"><Data z:Id=\"i12\"><Data z:Id=\"i13\"><Data z:Id=\"i14\"><Data z:Ref=\"i11\"/></Data><Data2 z:Id=\"i15\"><Data z:Ref=\"i13\"/></Data2></Data></Data></Data></Data><Data2 z:Id=\"i16\"><Data i:nil=\"true\"/></Data2></Data></Data2></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.C1(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.C1***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.C1***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Id=\"i5\"><Data z:Id=\"i6\"><Data z:Id=\"i7\"><Data z:Ref=\"i4\"/></Data><Data2 z:Id=\"i8\"><Data z:Ref=\"i6\"/></Data2></Data></Data></Data></Data><Data2 z:Id=\"i9\"><Data i:nil=\"true\"/></Data2></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.BB1(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.BB1***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.BB1***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Id=\"i5\"><Data z:Ref=\"i2\"/></Data><Data2 z:Id=\"i6\"><Data z:Ref=\"i4\"/></Data2></Data></Data></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.BBB1(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data z:Id=\"i1\" i:type=\"a:SerializationTestTypes.BBB1***\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.BBB1***\"><Data z:Id=\"i2\"><Data z:Id=\"i3\"><Data z:Id=\"i4\"><Data z:Ref=\"i1\"/></Data><Data2 z:Id=\"i5\"><Data z:Ref=\"i3\"/></Data2></Data></Data></_data><_data2 z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></ObjectContainer>");

    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/60462", TestPlatforms.iOS | TestPlatforms.tvOS)]
    public static void DCS_BasicPerSerializerRoundTripAndCompare_EnumStruct()
    {
        string assemblyName = typeof(DataContractSerializerTests).Assembly.FullName;

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.SeasonsEnumContainer(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.SeasonsEnumContainer***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SeasonsEnumContainer***\"><member1>Autumn</member1><member2>Spring</member2><member3>Winter</member3></_data><_data2 i:type=\"a:SerializationTestTypes.SeasonsEnumContainer***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.SeasonsEnumContainer***\"><member1>Autumn</member1><member2>Spring</member2><member3>Winter</member3></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Person("Hi"), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Person***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Person***\"><Age>6</Age><Name>smith</Name></_data><_data2 i:type=\"a:SerializationTestTypes.Person***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Person***\"><Age>6</Age><Name>smith</Name></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.CharClass(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.CharClass***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.CharClass***\"><c>0</c><c1>65535</c1><c2>0</c2><c3>99</c3></_data><_data2 i:type=\"a:SerializationTestTypes.CharClass***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.CharClass***\"><c>0</c><c1>65535</c1><c2>0</c2><c3>99</c3></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DictContainer(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.DictContainer***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DictContainer***\"><dictionaryData xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:KeyValueOfbase64Binarybase64Binary><b:Key>S3sf7NHCbkyVtgYKERsK/Q==</b:Key><b:Value>kNwxmEzKsk2TNVihAl7PKQ==</b:Value></b:KeyValueOfbase64Binarybase64Binary><b:KeyValueOfbase64Binarybase64Binary><b:Key>R5hoXhAack+qrhmyR80IeA==</b:Key><b:Value>kYav59VD50mHdRsBJr2UPA==</b:Value></b:KeyValueOfbase64Binarybase64Binary><b:KeyValueOfbase64Binarybase64Binary><b:Key>3WgRcQBK5U2fPjjd+9oBRA==</b:Key><b:Value>r7SFJrYJVkqB25UjGj0Cdg==</b:Value></b:KeyValueOfbase64Binarybase64Binary></dictionaryData></_data><_data2 i:type=\"a:SerializationTestTypes.DictContainer***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DictContainer***\"><dictionaryData xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:KeyValueOfbase64Binarybase64Binary><b:Key>S3sf7NHCbkyVtgYKERsK/Q==</b:Key><b:Value>kNwxmEzKsk2TNVihAl7PKQ==</b:Value></b:KeyValueOfbase64Binarybase64Binary><b:KeyValueOfbase64Binarybase64Binary><b:Key>R5hoXhAack+qrhmyR80IeA==</b:Key><b:Value>kYav59VD50mHdRsBJr2UPA==</b:Value></b:KeyValueOfbase64Binarybase64Binary><b:KeyValueOfbase64Binarybase64Binary><b:Key>3WgRcQBK5U2fPjjd+9oBRA==</b:Key><b:Value>r7SFJrYJVkqB25UjGj0Cdg==</b:Value></b:KeyValueOfbase64Binarybase64Binary></dictionaryData></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.ListContainer(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.ListContainer***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.ListContainer***\"><listData xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>TestData</b:string></listData></_data><_data2 i:type=\"a:SerializationTestTypes.ListContainer***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.ListContainer***\"><listData xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:string>TestData</b:string></listData></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.ArrayContainer(true), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.ArrayContainer***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.ArrayContainer***\"><listData xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:anyType i:type=\"c:string\" xmlns:c=\"http://www.w3.org/2001/XMLSchema\">TestData</b:anyType><b:anyType i:type=\"c:string\" xmlns:c=\"http://www.w3.org/2001/XMLSchema\">Test</b:anyType><b:anyType i:type=\"c:guid\" xmlns:c=\"http://schemas.microsoft.com/2003/10/Serialization/\">c0a7310f-f369-481e-a990-39b121eae513</b:anyType></listData></_data><_data2 i:type=\"a:SerializationTestTypes.ArrayContainer***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.ArrayContainer***\"><listData xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:anyType i:type=\"c:string\" xmlns:c=\"http://www.w3.org/2001/XMLSchema\">TestData</b:anyType><b:anyType i:type=\"c:string\" xmlns:c=\"http://www.w3.org/2001/XMLSchema\">Test</b:anyType><b:anyType i:type=\"c:guid\" xmlns:c=\"http://schemas.microsoft.com/2003/10/Serialization/\">c0a7310f-f369-481e-a990-39b121eae513</b:anyType></listData></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.EnumContainer1(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.EnumContainer1***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.EnumContainer1***\"><myPrivateEnum1 i:type=\"ArrayOfEnum1\"><Enum1>red</Enum1></myPrivateEnum1></_data><_data2 i:type=\"a:SerializationTestTypes.EnumContainer1***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.EnumContainer1***\"><myPrivateEnum1 i:type=\"ArrayOfEnum1\"><Enum1>red</Enum1></myPrivateEnum1></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.EnumContainer2(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.EnumContainer2***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.EnumContainer2***\"><myPrivateEnum2 i:type=\"ArrayOfMyPrivateEnum2\"><MyPrivateEnum2>red</MyPrivateEnum2></myPrivateEnum2></_data><_data2 i:type=\"a:SerializationTestTypes.EnumContainer2***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.EnumContainer2***\"><myPrivateEnum2 i:type=\"ArrayOfMyPrivateEnum2\"><MyPrivateEnum2>red</MyPrivateEnum2></myPrivateEnum2></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.EnumContainer3(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.EnumContainer3***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.EnumContainer3***\"><myPrivateEnum3 i:type=\"ArrayOfMyPrivateEnum3\"><MyPrivateEnum3>red</MyPrivateEnum3></myPrivateEnum3></_data><_data2 i:type=\"a:SerializationTestTypes.EnumContainer3***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.EnumContainer3***\"><myPrivateEnum3 i:type=\"ArrayOfMyPrivateEnum3\"><MyPrivateEnum3>red</MyPrivateEnum3></myPrivateEnum3></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.WithStatic(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.WithStatic***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.WithStatic***\"><str>instance string</str></_data><_data2 i:type=\"a:SerializationTestTypes.WithStatic***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.WithStatic***\"><str>instance string</str></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.DerivedFromPriC(0), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.DerivedFromPriC***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedFromPriC***\"><a>0</a><b i:nil=\"true\"/><c>0</c><d>0</d></_data><_data2 i:type=\"a:SerializationTestTypes.DerivedFromPriC***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.DerivedFromPriC***\"><a>0</a><b i:nil=\"true\"/><c>0</c><d>0</d></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.EmptyDC(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.EmptyDC***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.EmptyDC***\"><a>10</a></_data><_data2 i:type=\"a:SerializationTestTypes.EmptyDC***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.EmptyDC***\"><a>10</a></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Base(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Base***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Base***\"><A>0</A><B i:nil=\"true\"/></_data><_data2 i:type=\"a:SerializationTestTypes.Base***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Base***\"><A>0</A><B i:nil=\"true\"/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Derived(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Derived***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived***\"><A>0</A><B i:nil=\"true\"/></_data><_data2 i:type=\"a:SerializationTestTypes.Derived***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Derived***\"><A>0</A><B i:nil=\"true\"/></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.List(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.List***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.List***\"><next i:nil=\"true\"/><value>0</value></_data><_data2 i:type=\"a:SerializationTestTypes.List***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.List***\"><next i:nil=\"true\"/><value>0</value></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Arrays(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Arrays***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Arrays***\"><a1 xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/><a2 xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:int>1</b:int></a2><a3 xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:int>1</b:int><b:int>2</b:int><b:int>3</b:int><b:int>4</b:int></a3><a4 xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int></a4></_data><_data2 i:type=\"a:SerializationTestTypes.Arrays***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Arrays***\"><a1 xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/><a2 xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:int>1</b:int></a2><a3 xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:int>1</b:int><b:int>2</b:int><b:int>3</b:int><b:int>4</b:int></a3><a4 xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int><b:int>0</b:int></a4></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Array3(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Array3***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Array3***\"><a1 xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:ArrayOfint><b:int>1</b:int></b:ArrayOfint><b:ArrayOfint/></a1></_data><_data2 i:type=\"a:SerializationTestTypes.Array3***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Array3***\"><a1 xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:ArrayOfint><b:int>1</b:int></b:ArrayOfint><b:ArrayOfint/></a1></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Properties(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Properties***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Properties***\"><A>5</A></_data><_data2 i:type=\"a:SerializationTestTypes.Properties***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Properties***\"><A>5</A></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.HaveNS(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.HaveNS***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.HaveNS***\"><ns><a>0</a></ns></_data><_data2 i:type=\"a:SerializationTestTypes.HaveNS***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.HaveNS***\"><ns><a>0</a></ns></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.OutClass(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.OutClass***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.OutClass***\"><nc><a>10</a></nc></_data><_data2 i:type=\"a:SerializationTestTypes.OutClass***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.OutClass***\"><nc><a>10</a></nc></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Temp(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Temp***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Temp***\"><a>10</a></_data><_data2 i:type=\"a:SerializationTestTypes.Temp***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Temp***\"><a>10</a></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Array22(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Array22***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Array22***\"><p xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:int>1</b:int></p></_data><_data2 i:type=\"a:SerializationTestTypes.Array22***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Array22***\"><p xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><b:int>1</b:int></p></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.Person2(), $"<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.Person2***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Person2***\"><age>0</age><name i:nil=\"true\"/><Uid>ff816178-54df-2ea8-6511-cfeb4d14ab5a</Uid><XQAArray xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><q:QName xmlns:q=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\" xmlns:c=\"http://www.PlayForFun.com\">c:Name1</q:QName><q:QName xmlns:q=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\" xmlns:c=\"http://www.FunPlay.com\">c:Name2</q:QName></XQAArray><anyData i:type=\"b:SerializationTestTypes.Kid\" xmlns:b=\"{assemblyName}\"><age>3</age><name i:nil=\"true\"/><FavoriteToy i:type=\"b:SerializationTestTypes.Blocks\"><color>Orange</color></FavoriteToy></anyData></_data><_data2 i:type=\"a:SerializationTestTypes.Person2***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.Person2***\"><age>0</age><name i:nil=\"true\"/><Uid>ff816178-54df-2ea8-6511-cfeb4d14ab5a</Uid><XQAArray xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><q:QName xmlns:q=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\" xmlns:c=\"http://www.PlayForFun.com\">c:Name1</q:QName><q:QName xmlns:q=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\" xmlns:c=\"http://www.FunPlay.com\">c:Name2</q:QName></XQAArray><anyData i:type=\"b:SerializationTestTypes.Kid\" xmlns:b=\"{assemblyName}\"><age>3</age><name i:nil=\"true\"/><FavoriteToy i:type=\"b:SerializationTestTypes.Blocks\"><color>Orange</color></FavoriteToy></anyData></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.BoxedPrim(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.BoxedPrim***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.BoxedPrim***\"><p i:type=\"b:boolean\" xmlns:b=\"http://www.w3.org/2001/XMLSchema\">false</p><p2 i:type=\"VT\"><b>10</b></p2></_data><_data2 i:type=\"a:SerializationTestTypes.BoxedPrim***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.BoxedPrim***\"><p i:type=\"b:boolean\" xmlns:b=\"http://www.w3.org/2001/XMLSchema\">false</p><p2 i:type=\"VT\"><b>10</b></p2></_data2></ObjectContainer>");

        var typelist = new List<Type>
        {
            typeof(SerializationTestTypes.MyEnum),
            typeof(SerializationTestTypes.MyEnum1),
            typeof(SerializationTestTypes.MyEnum2),
            typeof(SerializationTestTypes.MyEnum3),
            typeof(SerializationTestTypes.MyEnum4),
            typeof(SerializationTestTypes.MyEnum7),
            typeof(SerializationTestTypes.MyEnum8),
            typeof(SerializationTestTypes.MyPrivateEnum1),
            typeof(SerializationTestTypes.MyPrivateEnum2),
            typeof(SerializationTestTypes.MyPrivateEnum3)
        };

        var setting = new DataContractSerializerSettings()
        {
            DataContractResolver = new SerializationTestTypes.SimpleResolver()
        };

        foreach (var type in typelist)
        {
            var possibleValues = Enum.GetValues(type);
            var input = possibleValues.GetValue(Random.Shared.Next(possibleValues.Length));
            string baseline = $"<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:{type}***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/{type}***\">{input}</_data><_data2 i:type=\"a:{type}***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/{type}***\">{input}</_data2></ObjectContainer>";
            var value = new SerializationTestTypes.ObjectContainer(input);
            var actual = DataContractSerializerHelper.SerializeAndDeserialize(value, baseline, setting);
            SerializationTestTypes.ComparisonHelper.CompareRecursively(value, actual);
        }
    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/60462", TestPlatforms.iOS | TestPlatforms.tvOS)]
    public static void DCS_BasicPerSerializerRoundTripAndCompare_EnumStruct_NotNetFramework()
    {
        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.AllTypes(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.AllTypes***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.AllTypes***\"><a>false</a><array1><anyType xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/><anyType xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/><anyType xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/></array1><b>255</b><c>0</c><d>65535</d><e>79228162514264337593543950335</e><enumArrayData><MyEnum1>red</MyEnum1></enumArrayData><enumBase1 i:type=\"b:SerializationTestTypes.MyEnum1***\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.MyEnum1***\">red</enumBase1><f>-1</f><f5>0001-01-01T00:00:00</f5><g>-79228162514264337593543950335</g><guidData>5642b5d2-87c3-a724-2390-997062f3f7a2</guidData><h>1</h><i>0</i><j>0</j><k>0</k><l>5E-324</l><lDTO xmlns:b=\"http://schemas.datacontract.org/2004/07/System\"/><m>1.7976931348623157E+308</m><n>-1.7976931348623157E+308</n><nDTO xmlns:b=\"http://schemas.datacontract.org/2004/07/System\"><b:DateTime>9999-12-31T23:59:59.9999999Z</b:DateTime><b:OffsetMinutes>0</b:OffsetMinutes></nDTO><o>NaN</o><obj/><p>-INF</p><q>INF</q><r>0</r><s>1E-45</s><strData i:nil=\"true\"/><t>-3.4028235E+38</t><timeSpan i:type=\"b:duration\" xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/\">P10675199DT2H48M5.4775807S</timeSpan><u>3.4028235E+38</u><uri>http://www.microsoft.com/</uri><v>NaN</v><valType i:type=\"PublicDCStruct\"><Data>Data</Data></valType><w>-INF</w><x>INF</x><q:xmlQualifiedName xmlns:q=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:b=\"http://www.microsoft.com\">b:WCF</q:xmlQualifiedName><y>0</y><z>2147483647</z><z1>-2147483648</z1><z2>0</z2><z3>9223372036854775807</z3><z4>-9223372036854775808</z4><z5/><z6>0</z6><z7>127</z7><z8>-128</z8><z9>0</z9><z91>32767</z91><z92>-32768</z92><z93>abc</z93><z94>0</z94><z95>65535</z95><z96>0</z96><z97>0</z97><z98>4294967295</z98><z99>0</z99><z990>0</z990><z991>18446744073709551615</z991><z992>0</z992></_data><_data2 i:type=\"a:SerializationTestTypes.AllTypes***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.AllTypes***\"><a>false</a><array1><anyType xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/><anyType xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/><anyType xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/></array1><b>255</b><c>0</c><d>65535</d><e>79228162514264337593543950335</e><enumArrayData><MyEnum1>red</MyEnum1></enumArrayData><enumBase1 i:type=\"b:SerializationTestTypes.MyEnum1***\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.MyEnum1***\">red</enumBase1><f>-1</f><f5>0001-01-01T00:00:00</f5><g>-79228162514264337593543950335</g><guidData>5642b5d2-87c3-a724-2390-997062f3f7a2</guidData><h>1</h><i>0</i><j>0</j><k>0</k><l>5E-324</l><lDTO xmlns:b=\"http://schemas.datacontract.org/2004/07/System\"/><m>1.7976931348623157E+308</m><n>-1.7976931348623157E+308</n><nDTO xmlns:b=\"http://schemas.datacontract.org/2004/07/System\"><b:DateTime>9999-12-31T23:59:59.9999999Z</b:DateTime><b:OffsetMinutes>0</b:OffsetMinutes></nDTO><o>NaN</o><obj/><p>-INF</p><q>INF</q><r>0</r><s>1E-45</s><strData i:nil=\"true\"/><t>-3.4028235E+38</t><timeSpan i:type=\"b:duration\" xmlns:b=\"http://schemas.microsoft.com/2003/10/Serialization/\">P10675199DT2H48M5.4775807S</timeSpan><u>3.4028235E+38</u><uri>http://www.microsoft.com/</uri><v>NaN</v><valType i:type=\"PublicDCStruct\"><Data>Data</Data></valType><w>-INF</w><x>INF</x><q:xmlQualifiedName xmlns:q=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:b=\"http://www.microsoft.com\">b:WCF</q:xmlQualifiedName><y>0</y><z>2147483647</z><z1>-2147483648</z1><z2>0</z2><z3>9223372036854775807</z3><z4>-9223372036854775808</z4><z5/><z6>0</z6><z7>127</z7><z8>-128</z8><z9>0</z9><z91>32767</z91><z92>-32768</z92><z93>abc</z93><z94>0</z94><z95>65535</z95><z96>0</z96><z97>0</z97><z98>4294967295</z98><z99>0</z99><z990>0</z990><z991>18446744073709551615</z991><z992>0</z992></_data2></ObjectContainer>");

        TestObjectInObjectContainerWithSimpleResolver(new SerializationTestTypes.AllTypes2(), "<ObjectContainer xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_data i:type=\"a:SerializationTestTypes.AllTypes2***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.AllTypes2***\"><a>false</a><array1><anyType xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/><anyType xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/><anyType xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/></array1><b>255</b><c>0</c><d>65535</d><e>79228162514264337593543950335</e><enumArrayData><MyEnum1>red</MyEnum1></enumArrayData><enumBase1 i:type=\"b:SerializationTestTypes.MyEnum1***\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.MyEnum1***\">red</enumBase1><f>-1</f><f5>0001-01-01T00:00:00</f5><g>-79228162514264337593543950335</g><guidData>cac76333-577f-7e1f-0389-789b0d97f395</guidData><h>1</h><i>0</i><j>0</j><k>0</k><l>5E-324</l><m>1.7976931348623157E+308</m><n>-1.7976931348623157E+308</n><nDTO xmlns:b=\"http://schemas.datacontract.org/2004/07/System\"><b:DateTime>9999-12-31T23:59:59.9999999Z</b:DateTime><b:OffsetMinutes>0</b:OffsetMinutes></nDTO><o>NaN</o><obj/><p>-INF</p><q>INF</q><r>0</r><s>1E-45</s><strData i:nil=\"true\"/><t>-3.4028235E+38</t><timeSpan>P10675199DT2H48M5.4775807S</timeSpan><u>3.4028235E+38</u><uri>http://www.microsoft.com/</uri><v>NaN</v><valType i:type=\"PublicDCStruct\"><Data>Data</Data></valType><w>-INF</w><x>INF</x><q:xmlQualifiedName xmlns:q=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:b=\"http://www.microsoft.com\">b:WCF</q:xmlQualifiedName><y>0</y><z>2147483647</z><z1>-2147483648</z1><z2>0</z2><z3>9223372036854775807</z3><z4>-9223372036854775808</z4><z5/><z6>0</z6><z7>127</z7><z8>-128</z8><z9>0</z9><z91>32767</z91><z92>-32768</z92><z93>abc</z93><z94>0</z94><z95>65535</z95><z96>0</z96><z97>0</z97><z98>4294967295</z98><z99>0</z99><z990>0</z990><z991>18446744073709551615</z991><z992>0</z992></_data><_data2 i:type=\"a:SerializationTestTypes.AllTypes2***\" xmlns:a=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.AllTypes2***\"><a>false</a><array1><anyType xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/><anyType xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/><anyType xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/></array1><b>255</b><c>0</c><d>65535</d><e>79228162514264337593543950335</e><enumArrayData><MyEnum1>red</MyEnum1></enumArrayData><enumBase1 i:type=\"b:SerializationTestTypes.MyEnum1***\" xmlns:b=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes.MyEnum1***\">red</enumBase1><f>-1</f><f5>0001-01-01T00:00:00</f5><g>-79228162514264337593543950335</g><guidData>cac76333-577f-7e1f-0389-789b0d97f395</guidData><h>1</h><i>0</i><j>0</j><k>0</k><l>5E-324</l><m>1.7976931348623157E+308</m><n>-1.7976931348623157E+308</n><nDTO xmlns:b=\"http://schemas.datacontract.org/2004/07/System\"><b:DateTime>9999-12-31T23:59:59.9999999Z</b:DateTime><b:OffsetMinutes>0</b:OffsetMinutes></nDTO><o>NaN</o><obj/><p>-INF</p><q>INF</q><r>0</r><s>1E-45</s><strData i:nil=\"true\"/><t>-3.4028235E+38</t><timeSpan>P10675199DT2H48M5.4775807S</timeSpan><u>3.4028235E+38</u><uri>http://www.microsoft.com/</uri><v>NaN</v><valType i:type=\"PublicDCStruct\"><Data>Data</Data></valType><w>-INF</w><x>INF</x><q:xmlQualifiedName xmlns:q=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:b=\"http://www.microsoft.com\">b:WCF</q:xmlQualifiedName><y>0</y><z>2147483647</z><z1>-2147483648</z1><z2>0</z2><z3>9223372036854775807</z3><z4>-9223372036854775808</z4><z5/><z6>0</z6><z7>127</z7><z8>-128</z8><z9>0</z9><z91>32767</z91><z92>-32768</z92><z93>abc</z93><z94>0</z94><z95>65535</z95><z96>0</z96><z97>0</z97><z98>4294967295</z98><z99>0</z99><z990>0</z990><z991>18446744073709551615</z991><z992>0</z992></_data2></ObjectContainer>");
    }


#endregion

    [Fact]
    public static void DCS_KnownSerializableTypes_KeyValuePair_2()
    {
        KeyValuePair<string, int> kvp = new KeyValuePair<string, int>("the_key", 42);
        Assert.StrictEqual(kvp, DataContractSerializerHelper.SerializeAndDeserialize<KeyValuePair<string, int>>(kvp, "<KeyValuePairOfstringint xmlns=\"http://schemas.datacontract.org/2004/07/System.Collections.Generic\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><key>the_key</key><value>42</value></KeyValuePairOfstringint>"));
    }

    [Fact]
    public static void DCS_KnownSerializableTypes_Queue_1()
    {
        Queue<string> q = new Queue<string>();
        q.Enqueue("first");
        q.Enqueue("second");
        Queue<string> q2 = DataContractSerializerHelper.SerializeAndDeserialize<Queue<string>>(q, "<QueueOfstring xmlns=\"http://schemas.datacontract.org/2004/07/System.Collections.Generic\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_array xmlns:a=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><a:string>first</a:string><a:string>second</a:string><a:string i:nil=\"true\"/><a:string i:nil=\"true\"/></_array><_head>0</_head><_size>2</_size><_tail>2</_tail><_version>3</_version></QueueOfstring>");
        Assert.Equal(q, q2);
        Assert.StrictEqual(q.Count, q2.Count);
        Assert.Equal(q.Dequeue(), q2.Dequeue());
        Assert.Equal(q.Dequeue(), q2.Dequeue());
    }

    [Fact]
    public static void DCS_KnownSerializableTypes_Stack_1()
    {
        Stack<string> stk = new Stack<string>();
        stk.Push("first");
        stk.Push("last");
        Stack<string> result = DataContractSerializerHelper.SerializeAndDeserialize<Stack<string>>(stk, "<StackOfstring xmlns=\"http://schemas.datacontract.org/2004/07/System.Collections.Generic\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_array xmlns:a=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><a:string>first</a:string><a:string>last</a:string><a:string i:nil=\"true\"/><a:string i:nil=\"true\"/></_array><_size>2</_size><_version>2</_version></StackOfstring>");
        Assert.Equal(stk, result);
        Assert.StrictEqual(stk.Count, result.Count);
        Assert.Equal(stk.Pop(), result.Pop());
        Assert.Equal(stk.Pop(), result.Pop());
    }

    [Fact]
    public static void DCS_KnownSerializableTypes_ReadOnlyCollection_1()
    {
        ReadOnlyCollection<string> roc = new ReadOnlyCollection<string>(new string[] { "one", "two", "three", "four" });
        ReadOnlyCollection<string> result = DataContractSerializerHelper.SerializeAndDeserialize<ReadOnlyCollection<string>>(roc, "<ReadOnlyCollectionOfstring xmlns=\"http://schemas.datacontract.org/2004/07/System.Collections.ObjectModel\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><list xmlns:a=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><a:string>one</a:string><a:string>two</a:string><a:string>three</a:string><a:string>four</a:string></list></ReadOnlyCollectionOfstring>");
        Assert.Equal(roc, result);
        Assert.StrictEqual(roc.Count, result.Count);

        for (int i = 0; i < roc.Count; i++)
            Assert.Equal(roc[i], result[i]);
    }

    [Fact]
    public static void DCS_KnownSerializableTypes_ReadOnlyDictionary_2()
    {
        ReadOnlyDictionary<string, int> rod = new ReadOnlyDictionary<string, int>(new Dictionary<string, int> { { "one", 1 }, { "two", 22 }, { "three", 333 }, { "four", 4444 } });
        ReadOnlyDictionary<string, int> result = DataContractSerializerHelper.SerializeAndDeserialize<ReadOnlyDictionary<string, int>>(rod, "<ReadOnlyDictionaryOfstringint xmlns=\"http://schemas.datacontract.org/2004/07/System.Collections.ObjectModel\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><m_dictionary xmlns:a=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><a:KeyValueOfstringint><a:Key>one</a:Key><a:Value>1</a:Value></a:KeyValueOfstringint><a:KeyValueOfstringint><a:Key>two</a:Key><a:Value>22</a:Value></a:KeyValueOfstringint><a:KeyValueOfstringint><a:Key>three</a:Key><a:Value>333</a:Value></a:KeyValueOfstringint><a:KeyValueOfstringint><a:Key>four</a:Key><a:Value>4444</a:Value></a:KeyValueOfstringint></m_dictionary></ReadOnlyDictionaryOfstringint>");
        Assert.Equal(rod, result);
        Assert.StrictEqual(rod.Count, result.Count);

        foreach (var kvp in rod)
        {
            Assert.True(result.ContainsKey(kvp.Key));
            Assert.Equal(kvp.Value, result[kvp.Key]);
        }
    }

    [Fact]
    public static void DCS_KnownSerializableTypes_Queue()
    {
        Queue q = new Queue();
        q.Enqueue("first");
        q.Enqueue("second");
        Queue q2 = DataContractSerializerHelper.SerializeAndDeserialize<Queue>(q, "<Queue xmlns=\"http://schemas.datacontract.org/2004/07/System.Collections\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_array xmlns:a=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><a:anyType i:type=\"b:string\" xmlns:b=\"http://www.w3.org/2001/XMLSchema\">first</a:anyType><a:anyType i:type=\"b:string\" xmlns:b=\"http://www.w3.org/2001/XMLSchema\">second</a:anyType><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/></_array><_growFactor>200</_growFactor><_head>0</_head><_size>2</_size><_tail>2</_tail><_version>2</_version></Queue>");
        Assert.Equal(q, q2);
        Assert.StrictEqual(q.Count, q2.Count);
        Assert.StrictEqual(q.Dequeue(), q2.Dequeue());
        Assert.StrictEqual(q.Dequeue(), q2.Dequeue());
    }

    [Fact]
    public static void DCS_KnownSerializableTypes_Stack()
    {
        Stack stk = new Stack();
        stk.Push("first");
        stk.Push("last");
        Stack result = DataContractSerializerHelper.SerializeAndDeserialize<Stack>(stk, "<Stack xmlns=\"http://schemas.datacontract.org/2004/07/System.Collections\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_array xmlns:a=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><a:anyType i:type=\"b:string\" xmlns:b=\"http://www.w3.org/2001/XMLSchema\">first</a:anyType><a:anyType i:type=\"b:string\" xmlns:b=\"http://www.w3.org/2001/XMLSchema\">last</a:anyType><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/><a:anyType i:nil=\"true\"/></_array><_size>2</_size><_version>2</_version></Stack>");
        Assert.Equal(stk, result);
        Assert.StrictEqual(stk.Count, result.Count);
        Assert.StrictEqual(stk.Pop(), result.Pop());
        Assert.StrictEqual(stk.Pop(), result.Pop());
    }

    [Fact]
    [ActiveIssue("No issue filed yet. Turns out, CultureInfo is not serialzable, even if it is included in s_knownSerializableTypeInfos")]
    public static void DCS_KnownSerializableTypes_CultureInfo()
    {
        CultureInfo ci = new CultureInfo("pl");
        Assert.StrictEqual(ci, DataContractSerializerHelper.SerializeAndDeserialize<CultureInfo>(ci, "", null, null, true));
    }

    [Fact]
    public static void DCS_KnownSerializableTypes_Version()
    {
        Version ver = new Version(5, 4, 3);
        Assert.StrictEqual(ver, DataContractSerializerHelper.SerializeAndDeserialize<Version>(ver, "<Version xmlns=\"http://schemas.datacontract.org/2004/07/System\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><_Build>3</_Build><_Major>5</_Major><_Minor>4</_Minor><_Revision>-1</_Revision></Version>"));
    }

    [Fact]
    public static void DCS_KnownSerializableTypes_Tuples()
    {
        Tuple<string> t1 = new Tuple<string>("first");
        Assert.StrictEqual(t1, DataContractSerializerHelper.SerializeAndDeserialize<Tuple<string>>(t1, "<TupleOfstring xmlns=\"http://schemas.datacontract.org/2004/07/System\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><m_Item1>first</m_Item1></TupleOfstring>"));

        Tuple<string, string> t2 = new Tuple<string, string>("first", "second");
        Assert.StrictEqual(t2, DataContractSerializerHelper.SerializeAndDeserialize<Tuple<string, string>>(t2, "<TupleOfstringstring xmlns=\"http://schemas.datacontract.org/2004/07/System\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><m_Item1>first</m_Item1><m_Item2>second</m_Item2></TupleOfstringstring>"));

        Tuple<string, string, string> t3 = new Tuple<string, string, string>("first", "second", "third");
        Assert.StrictEqual(t3, DataContractSerializerHelper.SerializeAndDeserialize<Tuple<string, string, string>>(t3, "<TupleOfstringstringstring xmlns=\"http://schemas.datacontract.org/2004/07/System\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><m_Item1>first</m_Item1><m_Item2>second</m_Item2><m_Item3>third</m_Item3></TupleOfstringstringstring>"));

        Tuple<string, string, string, string> t4 = new Tuple<string, string, string, string>("first", "second", "third", "fourth");
        Assert.StrictEqual(t4, DataContractSerializerHelper.SerializeAndDeserialize<Tuple<string, string, string, string>>(t4, "<TupleOfstringstringstringstring xmlns=\"http://schemas.datacontract.org/2004/07/System\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><m_Item1>first</m_Item1><m_Item2>second</m_Item2><m_Item3>third</m_Item3><m_Item4>fourth</m_Item4></TupleOfstringstringstringstring>"));

        Tuple<string, string, string, string, string> t5 = new Tuple<string, string, string, string, string>("first", "second", "third", "fourth", "fifth");
        Assert.StrictEqual(t5, DataContractSerializerHelper.SerializeAndDeserialize<Tuple<string, string, string, string, string>>(t5, "<TupleOfstringstringstringstringstring xmlns=\"http://schemas.datacontract.org/2004/07/System\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><m_Item1>first</m_Item1><m_Item2>second</m_Item2><m_Item3>third</m_Item3><m_Item4>fourth</m_Item4><m_Item5>fifth</m_Item5></TupleOfstringstringstringstringstring>"));

        Tuple<string, string, string, string, string, string> t6 = new Tuple<string, string, string, string, string, string>("first", "second", "third", "fourth", "fifth", "sixth");
        Assert.StrictEqual(t6, DataContractSerializerHelper.SerializeAndDeserialize<Tuple<string, string, string, string, string, string>>(t6, "<TupleOfstringstringstringstringstringstring xmlns=\"http://schemas.datacontract.org/2004/07/System\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><m_Item1>first</m_Item1><m_Item2>second</m_Item2><m_Item3>third</m_Item3><m_Item4>fourth</m_Item4><m_Item5>fifth</m_Item5><m_Item6>sixth</m_Item6></TupleOfstringstringstringstringstringstring>"));

        Tuple<string, string, string, string, string, string, string> t7 = new Tuple<string, string, string, string, string, string, string>("first", "second", "third", "fourth", "fifth", "sixth", "seventh");
        Assert.StrictEqual(t7, DataContractSerializerHelper.SerializeAndDeserialize<Tuple<string, string, string, string, string, string, string>>(t7, "<TupleOfstringstringstringstringstringstringstring xmlns=\"http://schemas.datacontract.org/2004/07/System\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><m_Item1>first</m_Item1><m_Item2>second</m_Item2><m_Item3>third</m_Item3><m_Item4>fourth</m_Item4><m_Item5>fifth</m_Item5><m_Item6>sixth</m_Item6><m_Item7>seventh</m_Item7></TupleOfstringstringstringstringstringstringstring>"));

        Tuple<string, string, string, string, string, string, string, Tuple<int, int, string>> t8 = new Tuple<string, string, string, string, string, string, string, Tuple<int, int, string>>("first", "second", "third", "fourth", "fifth", "sixth", "seventh", new Tuple<int, int, string>(8, 9, "tenth"));
        Assert.StrictEqual(t8, DataContractSerializerHelper.SerializeAndDeserialize<Tuple<string, string, string, string, string, string, string, Tuple<int, int, string>>>(t8, "<TupleOfstringstringstringstringstringstringstringTupleOfintintstringcd6ORBnm xmlns=\"http://schemas.datacontract.org/2004/07/System\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><m_Item1>first</m_Item1><m_Item2>second</m_Item2><m_Item3>third</m_Item3><m_Item4>fourth</m_Item4><m_Item5>fifth</m_Item5><m_Item6>sixth</m_Item6><m_Item7>seventh</m_Item7><m_Rest><m_Item1>8</m_Item1><m_Item2>9</m_Item2><m_Item3>tenth</m_Item3></m_Rest></TupleOfstringstringstringstringstringstringstringTupleOfintintstringcd6ORBnm>"));
    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/60462", TestPlatforms.iOS | TestPlatforms.tvOS)]
    public static void DCS_TypeWithVirtualGenericProperty()
    {
        var value1 = new TypeWithVirtualGenericProperty<int>() { Value = 1 };
        var actual1 = DataContractSerializerHelper.SerializeAndDeserialize(value1, string.Empty, skipStringCompare: true);
        Assert.NotNull(actual1);
        Assert.Equal(value1.Value, actual1.Value);

        var value2 = new TypeWithVirtualGenericPropertyDerived<int>() { Value = 2 };
        var actual2 = DataContractSerializerHelper.SerializeAndDeserialize(value2, string.Empty, skipStringCompare: true);
        Assert.NotNull(actual2);
        Assert.Equal(value2.Value, actual2.Value);
    }

    [Fact]
    public static void DCS_MyPersonSurrogate()
    {
        DataContractSerializer dcs = new DataContractSerializer(typeof(Family));
        dcs.SetSerializationSurrogateProvider(new MyPersonSurrogateProvider());
        MemoryStream ms = new MemoryStream();
        Family myFamily = new Family
        {
            Members = new NonSerializablePerson[]
            {
                new NonSerializablePerson("John", 34),
                new NonSerializablePerson("Jane", 32),
                new NonSerializablePerson("Bob", 5),
            }
        };
        dcs.WriteObject(ms, myFamily);
        ms.Position = 0;
        var newFamily = (Family)dcs.ReadObject(ms);
        Assert.StrictEqual(myFamily.Members.Length, newFamily.Members.Length);
        for (int i = 0; i < myFamily.Members.Length; ++i)
        {
            Assert.Equal(myFamily.Members[i].Name, newFamily.Members[i].Name);
        }
    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/85690", TestPlatforms.Wasi)]
    public static void DCS_FileStreamSurrogate()
    {
        using (var testFile = TempFile.Create())
        {
            const string TestFileData = "Some data for data contract surrogate test";

            // Create the serializer and specify the surrogate
            var dcs = new DataContractSerializer(typeof(MyFileStream));
            dcs.SetSerializationSurrogateProvider(MyFileStreamSurrogateProvider.Singleton);

            // Create and initialize the stream
            byte[] serializedStream;

            // Serialize the stream
            using (var stream1 = new MyFileStream(testFile.Path))
            {
                stream1.WriteLine(TestFileData);
                using (var memoryStream = new MemoryStream())
                {
                    dcs.WriteObject(memoryStream, stream1);
                    serializedStream = memoryStream.ToArray();
                }
            }

            // Deserialize the stream
            using (var stream = new MemoryStream(serializedStream))
            {
                using (var stream2 = (MyFileStream)dcs.ReadObject(stream))
                {
                    string fileData = stream2.ReadLine();
                    Assert.Equal(TestFileData, fileData);
                }
            }
        }
    }

    [Fact]
    public static void DCS_KnownTypeMethodName()
    {
        var emp1 = new EmployeeC("Steve");
        var emp2 = new EmployeeC("Lilian");
        var value = new Manager("Tony")
        {
            age = 30,
            emps = new EmployeeC[] { emp1, emp2 }
        };

        Manager actual = DataContractSerializerHelper.SerializeAndDeserialize(value, @"<Manager xmlns=""http://schemas.datacontract.org/2004/07/"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><Name>Tony</Name><age>30</age><emps><EmployeeC><Name>Steve</Name></EmployeeC><EmployeeC><Name>Lilian</Name></EmployeeC></emps></Manager>");
        Assert.NotNull(actual);
        Assert.Equal(value.age, actual.age);
        Assert.NotNull(actual.emps);
        Assert.Equal(value.emps.Count(), actual.emps.Count());
        Assert.Equal(value.emps[0].Name, actual.emps[0].Name);
        Assert.Equal(value.emps[1].Name, actual.emps[1].Name);
    }

    [Fact]
    public static void DCS_SampleICollectionTExplicitWithoutDC()
    {
        var value = new SerializationTestTypes.SampleICollectionTExplicitWithoutDC(true);
        string netcorePayload = "<SampleICollectionTExplicitWithoutDC xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><DC z:Id=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"><Data>TestData</Data><Next i:nil=\"true\"/></DC><DC z:Id=\"i2\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"><Data>TestData</Data><Next i:nil=\"true\"/></DC><DC z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></SampleICollectionTExplicitWithoutDC>";
        string desktopPayload = "<SampleICollectionTExplicitWithoutDC xmlns=\"http://schemas.datacontract.org/2004/07/SerializationTestTypes\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><DC z:Id=\"i1\" i:type=\"DC\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"><Data>TestData</Data><Next i:nil=\"true\"/></DC><DC z:Id=\"i2\" i:type=\"DC\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"><Data>TestData</Data><Next i:nil=\"true\"/></DC><DC z:Ref=\"i1\" xmlns:z=\"http://schemas.microsoft.com/2003/10/Serialization/\"/></SampleICollectionTExplicitWithoutDC>";
        TestObjectWithDifferentPayload(value, netcorePayload, desktopPayload);
    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/73961", typeof(PlatformDetection), nameof(PlatformDetection.IsBuiltWithAggressiveTrimming), nameof(PlatformDetection.IsBrowser))]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/73961", typeof(PlatformDetection), nameof(PlatformDetection.IsWasi))]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/73961", typeof(PlatformDetection), nameof(PlatformDetection.IsMonoRuntime), nameof(PlatformDetection.IsBuiltWithAggressiveTrimming), nameof(PlatformDetection.IsAppleMobile))]
    public static void DCS_MemoryStream_Serialize_UsesBuiltInAdapter()
    {
        ValidateObject(
            original: new MemoryStream(),
            expectedXml: @"<MemoryStream xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://schemas.datacontract.org/2004/07/System.IO""><__identity i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/System"" /><_buffer></_buffer><_capacity>0</_capacity><_expandable>false</_expandable><_exposable>true</_exposable><_isOpen>true</_isOpen><_length>0</_length><_origin>0</_origin><_position>0</_position><_writable>true</_writable></MemoryStream>",
            expectedData: new byte[0],
            expectedPosition: 0,
            expectedExposable: true);

        ValidateObject(
            original: new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }),
            expectedXml: @"<MemoryStream xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://schemas.datacontract.org/2004/07/System.IO""><__identity i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/System"" /><_buffer>AQIDBAU=</_buffer><_capacity>5</_capacity><_expandable>false</_expandable><_exposable>false</_exposable><_isOpen>true</_isOpen><_length>5</_length><_origin>0</_origin><_position>0</_position><_writable>true</_writable></MemoryStream>",
            expectedData: new byte[] { 1, 2, 3, 4, 5 },
            expectedPosition: 0,
            expectedExposable: false);

        ValidateObject(
            original: new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 2, 5, true, true), // partial buffer
            expectedXml: @"<MemoryStream xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://schemas.datacontract.org/2004/07/System.IO""><__identity i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/System"" /><_buffer>AwQFBgc=</_buffer><_capacity>5</_capacity><_expandable>false</_expandable><_exposable>true</_exposable><_isOpen>true</_isOpen><_length>5</_length><_origin>0</_origin><_position>0</_position><_writable>true</_writable></MemoryStream>",
            expectedData: new byte[] { 3, 4, 5, 6, 7 },
            expectedPosition: 0,
            expectedExposable: true);

        ValidateObject(
            original: new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }, 0, 4, writable: false, publiclyVisible: true) { Position = 2 },
            expectedXml: @"<MemoryStream xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://schemas.datacontract.org/2004/07/System.IO""><__identity i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/System"" /><_buffer>AQIDBA==</_buffer><_capacity>4</_capacity><_expandable>false</_expandable><_exposable>true</_exposable><_isOpen>true</_isOpen><_length>4</_length><_origin>0</_origin><_position>2</_position><_writable>false</_writable></MemoryStream>",
            expectedData: new byte[] { 1, 2, 3, 4 },
            expectedPosition: 2,
            expectedExposable: true);

        ValidateObject(
           original: new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }, 0, 4, writable: false, publiclyVisible: false) { Position = 4 },
           expectedXml: @"<MemoryStream xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""http://schemas.datacontract.org/2004/07/System.IO""><__identity i:nil=""true"" xmlns=""http://schemas.datacontract.org/2004/07/System"" /><_buffer>AQIDBA==</_buffer><_capacity>4</_capacity><_expandable>false</_expandable><_exposable>false</_exposable><_isOpen>true</_isOpen><_length>4</_length><_origin>0</_origin><_position>4</_position><_writable>false</_writable></MemoryStream>",
           expectedData: new byte[] { 1, 2, 3, 4 },
           expectedPosition: 4,
           expectedExposable: false);

        static void ValidateObject(MemoryStream original, string expectedXml, byte[] expectedData, int expectedPosition, bool expectedExposable)
        {
            MemoryStream roundTripped = DataContractSerializerHelper.SerializeAndDeserialize(original, expectedXml);

            Assert.NotNull(roundTripped);
            Assert.Equal(expectedData, roundTripped.ToArray());
            Assert.Equal(original.Position, roundTripped.Position);
            Assert.Equal(original.Length, roundTripped.Length);
            Assert.Equal(original.Capacity, roundTripped.Capacity);
            Assert.Equal(original.CanWrite, roundTripped.CanWrite);
            Assert.Equal(expectedExposable, roundTripped.TryGetBuffer(out ArraySegment<byte> innerBuffer));

            if (expectedExposable)
            {
                Assert.Equal(0, innerBuffer.Offset); // don't allow unused data around the original buffer to round-trip
                Assert.Equal(expectedData.Length, innerBuffer.Count);
            }
        }
    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/73961", typeof(PlatformDetection), nameof(PlatformDetection.IsBuiltWithAggressiveTrimming), nameof(PlatformDetection.IsBrowser))]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/73961", typeof(PlatformDetection), nameof(PlatformDetection.IsWasi))]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/73961", typeof(PlatformDetection), nameof(PlatformDetection.IsMonoRuntime), nameof(PlatformDetection.IsBuiltWithAggressiveTrimming), nameof(PlatformDetection.IsAppleMobile))]
    public static void DCS_MemoryStream_Deserialize_CompatibleWithFullFramework()
    {
        // The payloads in this test were generated by a Full Framework application.

        DeserializeObjectAndValidate(
            input: "<MemoryStream xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.datacontract.org/2004/07/System.IO\"><__identity i:nil=\"true\" xmlns=\"http://schemas.datacontract.org/2004/07/System\" /><_buffer>AQIDBAU=</_buffer><_capacity>5</_capacity><_expandable>false</_expandable><_exposable>false</_exposable><_isOpen>true</_isOpen><_length>5</_length><_origin>0</_origin><_position>0</_position><_writable>true</_writable></MemoryStream>",
            expectedData: new byte[] { 1, 2, 3, 4, 5 },
            expectedPosition: 0,
            expectedExposable: false,
            expectedWritable: true);

        DeserializeObjectAndValidate(
            input: "<MemoryStream xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.datacontract.org/2004/07/System.IO\"><__identity i:nil=\"true\" xmlns=\"http://schemas.datacontract.org/2004/07/System\" /><_buffer>AQIDBAUGBwgJCg==</_buffer><_capacity>7</_capacity><_expandable>false</_expandable><_exposable>true</_exposable><_isOpen>true</_isOpen><_length>7</_length><_origin>2</_origin><_position>2</_position><_writable>true</_writable></MemoryStream>",
            expectedData: new byte[] { 3, 4, 5, 6, 7 }, // origin is partway into the original buffer
            expectedPosition: 0,
            expectedExposable: true,
            expectedWritable: true);

        DeserializeObjectAndValidate(
            input: "<MemoryStream xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.datacontract.org/2004/07/System.IO\"><__identity i:nil=\"true\" xmlns=\"http://schemas.datacontract.org/2004/07/System\" /><_buffer>AQIDBAU=</_buffer><_capacity>4</_capacity><_expandable>false</_expandable><_exposable>true</_exposable><_isOpen>true</_isOpen><_length>4</_length><_origin>0</_origin><_position>2</_position><_writable>false</_writable></MemoryStream>",
            expectedData: new byte[] { 1, 2, 3, 4 },
            expectedPosition: 2, // partway into the buffer
            expectedExposable: true,
            expectedWritable: false);

        DeserializeObjectAndValidate(
            input: "<MemoryStream xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.datacontract.org/2004/07/System.IO\"><__identity i:nil=\"true\" xmlns=\"http://schemas.datacontract.org/2004/07/System\" /><_buffer>AQIDBAU=</_buffer><_capacity>4</_capacity><_expandable>false</_expandable><_exposable>false</_exposable><_isOpen>true</_isOpen><_length>4</_length><_origin>0</_origin><_position>4</_position><_writable>false</_writable></MemoryStream>",
            expectedData: new byte[] { 1, 2, 3, 4 },
            expectedPosition: 4, // partway into the buffer
            expectedExposable: false,
            expectedWritable: false);

        // Demonstrates that we ignore _capacity

        DeserializeObjectAndValidate(
            input: "<MemoryStream xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.datacontract.org/2004/07/System.IO\"><__identity i:nil=\"true\" xmlns=\"http://schemas.datacontract.org/2004/07/System\" /><_buffer>AQIDBAU=</_buffer><_capacity>500000</_capacity><_expandable>false</_expandable><_exposable>false</_exposable><_isOpen>false</_isOpen><_length>5</_length><_origin>0</_origin><_position>0</_position><_writable>true</_writable></MemoryStream>",
            expectedData: new byte[] { 1, 2, 3, 4, 5 },
            expectedPosition: 0,
            expectedExposable: false,
            expectedWritable: true);

        // Demonstrates that we ignore _expandable

        DeserializeObjectAndValidate(
            input: "<MemoryStream xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.datacontract.org/2004/07/System.IO\"><__identity i:nil=\"true\" xmlns=\"http://schemas.datacontract.org/2004/07/System\" /><_buffer>AQIDBAU=</_buffer><_capacity>5</_capacity><_expandable>true</_expandable><_exposable>false</_exposable><_isOpen>false</_isOpen><_length>5</_length><_origin>0</_origin><_position>0</_position><_writable>true</_writable></MemoryStream>",
            expectedData: new byte[] { 1, 2, 3, 4, 5 },
            expectedPosition: 0,
            expectedExposable: false,
            expectedWritable: true);

        static void DeserializeObjectAndValidate(string input, byte[] expectedData, int expectedPosition, bool expectedExposable, bool expectedWritable)
        {
            MemoryStream deserialized = (MemoryStream)new DataContractSerializer(typeof(MemoryStream))
                .ReadObject(new XmlTextReader(new StringReader(input)));

            Assert.NotNull(deserialized);
            Assert.Equal(expectedData, deserialized.ToArray());
            Assert.Equal(expectedPosition, deserialized.Position);
            Assert.Equal(expectedData.Length, deserialized.Length);
            Assert.Equal(expectedData.Length, deserialized.Capacity);
            Assert.Equal(expectedWritable, deserialized.CanWrite);
            Assert.Equal(expectedExposable, deserialized.TryGetBuffer(out ArraySegment<byte> innerBuffer));

            if (expectedExposable)
            {
                Assert.Equal(0, innerBuffer.Offset); // don't allow unused data around the original buffer to round-trip
                Assert.Equal(expectedData.Length, innerBuffer.Count);
            }
        }
    }

    [Fact]
    public static void DCS_MemoryStream_Deserialize_DisallowsBogusInputs()
    {
        // Bad origin (negative)
        DeserializeObjectAndThrow("<MemoryStream xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.datacontract.org/2004/07/System.IO\"><__identity i:nil=\"true\" xmlns=\"http://schemas.datacontract.org/2004/07/System\" /><_buffer>AQIDBAU=</_buffer><_capacity>5</_capacity><_expandable>false</_expandable><_exposable>false</_exposable><_isOpen>true</_isOpen><_length>5</_length><_origin>-1</_origin><_position>0</_position><_writable>true</_writable></MemoryStream>");

        // Bad origin (too large)
        DeserializeObjectAndThrow("<MemoryStream xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.datacontract.org/2004/07/System.IO\"><__identity i:nil=\"true\" xmlns=\"http://schemas.datacontract.org/2004/07/System\" /><_buffer>AQIDBAU=</_buffer><_capacity>5</_capacity><_expandable>false</_expandable><_exposable>false</_exposable><_isOpen>true</_isOpen><_length>5</_length><_origin>6</_origin><_position>0</_position><_writable>true</_writable></MemoryStream>");

        // Bad length
        DeserializeObjectAndThrow("<MemoryStream xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.datacontract.org/2004/07/System.IO\"><__identity i:nil=\"true\" xmlns=\"http://schemas.datacontract.org/2004/07/System\" /><_buffer>AQIDBAU=</_buffer><_capacity>5</_capacity><_expandable>false</_expandable><_exposable>false</_exposable><_isOpen>true</_isOpen><_length>6</_length><_origin>0</_origin><_position>0</_position><_writable>true</_writable></MemoryStream>");

        // Bad position (negative)
        DeserializeObjectAndThrow("<MemoryStream xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.datacontract.org/2004/07/System.IO\"><__identity i:nil=\"true\" xmlns=\"http://schemas.datacontract.org/2004/07/System\" /><_buffer>AQIDBAU=</_buffer><_capacity>5</_capacity><_expandable>false</_expandable><_exposable>false</_exposable><_isOpen>true</_isOpen><_length>5</_length><_origin>0</_origin><_position>-1</_position><_writable>true</_writable></MemoryStream>");

        // Bad position (too large)
        DeserializeObjectAndThrow("<MemoryStream xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.datacontract.org/2004/07/System.IO\"><__identity i:nil=\"true\" xmlns=\"http://schemas.datacontract.org/2004/07/System\" /><_buffer>AQIDBAU=</_buffer><_capacity>5</_capacity><_expandable>false</_expandable><_exposable>false</_exposable><_isOpen>true</_isOpen><_length>5</_length><_origin>0</_origin><_position>1024</_position><_writable>true</_writable></MemoryStream>");

        static void DeserializeObjectAndThrow(string input)
        {
            // Might be ArgumentException, InvalidOperationException, etc. Honestly it doesn't matter.
            Assert.ThrowsAny<Exception>(() => (MemoryStream)new DataContractSerializer(typeof(MemoryStream)).ReadObject(new XmlTextReader(new StringReader(input))));
        }
    }

    [Fact]
    public static void DCS_InvalidDataContract_Write_Invalid_Types_Throws()
    {
        // Attempting to serialize any invalid type should create an InvalidDataContract that throws
        foreach (NetNativeTestData td in NetNativeTestData.InvalidTypes)
        {
            object o = td.Instantiate();
            DataContractSerializer dcs = new DataContractSerializer(o.GetType());
            MemoryStream ms = new MemoryStream();
            Assert.Throws<InvalidDataContractException>(() =>
            {
                dcs.WriteObject(ms, o);
            });
        }
    }

    [Fact]
    public static void DCS_InvalidDataContract_Read_Invalid_Types_Throws()
    {
        // Attempting to deserialize any invalid type should create an InvalidDataContract that throws
        foreach (NetNativeTestData td in NetNativeTestData.InvalidTypes)
        {
            DataContractSerializer dcs = new DataContractSerializer(td.Type);
            MemoryStream ms = new MemoryStream();
            new DataContractSerializer(typeof(string)).WriteObject(ms, "test");
            ms.Seek(0L, SeekOrigin.Begin);
            if (td.Type.Equals(typeof(Invalid_Class_KnownType_Invalid_Type)))
            {
                Assert.Throws<SerializationException>(() =>
                {
                    dcs.ReadObject(ms);
                });
            }
            else
            {
                Assert.Throws<InvalidDataContractException>(() =>
                {
                    dcs.ReadObject(ms);
                });
            }
        }
    }

    [Fact]
    public static void DCS_ValidateExceptionOnUnspecifiedRootSerializationType()
    {
        var value = new UnspecifiedRootSerializationType();
        string baseline = @"<UnspecifiedRootSerializationType xmlns=""http://schemas.datacontract.org/2004/07/SerializationTypes"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><MyIntProperty>0</MyIntProperty><MyStringProperty i:nil=""true""/></UnspecifiedRootSerializationType>";
        var actual = DataContractSerializerHelper.SerializeAndDeserialize(value, baseline);

        Assert.Equal(value.MyIntProperty, actual.MyIntProperty);
        Assert.Equal(value.MyStringProperty, actual.MyStringProperty);
    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/60462", TestPlatforms.iOS | TestPlatforms.tvOS)]
    public static void DCS_TypeWithCollectionAndDateTimeOffset()
    {
        // Adding offsetMinutes so the DateTime component in serialized strings are time-zone independent
        int offsetMinutes = (int)TimeZoneInfo.Local.GetUtcOffset(new DateTime(2013, 1, 2)).TotalMinutes;
        DateTimeOffset dateTimeOffset = new DateTimeOffset(new DateTime(2013, 1, 2, 3, 4, 5, 6).AddMinutes(offsetMinutes));
        var value = new TypeWithCollectionAndDateTimeOffset(new List<int>() { 1, 2, 3 }, dateTimeOffset);

        TypeWithCollectionAndDateTimeOffset actual = DataContractSerializerHelper.SerializeAndDeserialize(value, $"<TypeWithCollectionAndDateTimeOffset xmlns=\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><AnIntList xmlns:a=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"><a:int>1</a:int><a:int>2</a:int><a:int>3</a:int></AnIntList><DateTimeOffset xmlns:a=\"http://schemas.datacontract.org/2004/07/System\"><a:DateTime>2013-01-02T03:04:05.006Z</a:DateTime><a:OffsetMinutes>{offsetMinutes}</a:OffsetMinutes></DateTimeOffset></TypeWithCollectionAndDateTimeOffset>");
        Assert.NotNull(actual);
        Assert.Equal(value.DateTimeOffset, actual.DateTimeOffset);
        Assert.NotNull(actual.AnIntList);
        Assert.True(Enumerable.SequenceEqual(value.AnIntList, actual.AnIntList));
    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/runtime/issues/60462", TestPlatforms.iOS | TestPlatforms.tvOS)]
    public static void DCS_TypeWithCollectionAndDateTimeOffset_ListIsNull()
    {
        // Adding offsetMinutes so the DateTime component in serialized strings are time-zone independent
        int offsetMinutes = (int)TimeZoneInfo.Local.GetUtcOffset(new DateTime(2013, 1, 2)).TotalMinutes;
        DateTimeOffset dateTimeOffset = new DateTimeOffset(new DateTime(2013, 1, 2, 3, 4, 5, 6).AddMinutes(offsetMinutes));
        var value = new TypeWithCollectionAndDateTimeOffset(null, dateTimeOffset);

        TypeWithCollectionAndDateTimeOffset actual = DataContractSerializerHelper.SerializeAndDeserialize(value, $"<TypeWithCollectionAndDateTimeOffset xmlns=\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><AnIntList i:nil=\"true\" xmlns:a=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"/><DateTimeOffset xmlns:a=\"http://schemas.datacontract.org/2004/07/System\"><a:DateTime>2013-01-02T03:04:05.006Z</a:DateTime><a:OffsetMinutes>{offsetMinutes}</a:OffsetMinutes></DateTimeOffset></TypeWithCollectionAndDateTimeOffset>");
        Assert.NotNull(actual);
        Assert.Equal(value.DateTimeOffset, actual.DateTimeOffset);
        Assert.NotNull(actual.AnIntList);
        Assert.Equal(0, actual.AnIntList.Count);
    }

    [Fact]
    public static void DCS_TypeWithPrimitiveKnownTypes()
    {
        var list = new TypeWithPrimitiveKnownTypes();
        list.Add(true);
        list.Add('c');
        list.Add(new DateTime(100));
        list.Add(11.1m);
        list.Add(22.2);
        list.Add(33.3f);
        list.Add(new Guid());
        list.Add(11);
        list.Add(1111111111111111111L);
        list.Add(new XmlQualifiedName("NAME", "NS"));
        list.Add((short)44);
        list.Add((sbyte)-1);
        list.Add("test string");
        list.Add(new TimeSpan(100));
        list.Add((byte)1);
        list.Add(uint.MaxValue);
        list.Add(ulong.MaxValue);
        list.Add(ushort.MaxValue);
        list.Add(new Uri("http://foo"));
        var actual = DataContractSerializerHelper.SerializeAndDeserialize(list, "<TypeWithPrimitiveKnownTypes xmlns=\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><anyType i:type=\"a:boolean\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\">true</anyType><anyType i:type=\"a:char\" xmlns:a=\"http://schemas.microsoft.com/2003/10/Serialization/\">99</anyType><anyType i:type=\"a:dateTime\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\">0001-01-01T00:00:00.00001</anyType><anyType i:type=\"a:decimal\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\">11.1</anyType><anyType i:type=\"a:double\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\">22.2</anyType><anyType i:type=\"a:float\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\">33.3</anyType><anyType i:type=\"a:guid\" xmlns:a=\"http://schemas.microsoft.com/2003/10/Serialization/\">00000000-0000-0000-0000-000000000000</anyType><anyType i:type=\"a:int\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\">11</anyType><anyType i:type=\"a:long\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\">1111111111111111111</anyType><anyType i:type=\"a:QName\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\" xmlns:b=\"NS\">b:NAME</anyType><anyType i:type=\"a:short\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\">44</anyType><anyType i:type=\"a:byte\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\">-1</anyType><anyType i:type=\"a:string\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\">test string</anyType><anyType i:type=\"a:duration\" xmlns:a=\"http://schemas.microsoft.com/2003/10/Serialization/\">PT0.00001S</anyType><anyType i:type=\"a:unsignedByte\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\">1</anyType><anyType i:type=\"a:unsignedInt\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\">4294967295</anyType><anyType i:type=\"a:unsignedLong\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\">18446744073709551615</anyType><anyType i:type=\"a:unsignedShort\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\">65535</anyType><anyType i:type=\"a:anyURI\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\">http://foo/</anyType></TypeWithPrimitiveKnownTypes>");
        Assert.True(Enumerable.SequenceEqual(list, actual));

        list.Clear();
        list.Add(new byte[] { 1, 2 });
        actual = DataContractSerializerHelper.SerializeAndDeserialize(list, "<TypeWithPrimitiveKnownTypes xmlns=\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><anyType i:type=\"a:base64Binary\" xmlns:a=\"http://www.w3.org/2001/XMLSchema\">AQI=</anyType></TypeWithPrimitiveKnownTypes>");
        Assert.NotNull(actual);
        Assert.Single(actual);
        Assert.True(actual[0] is byte[]);
        Assert.True(((byte[])list[0]).SequenceEqual(((byte[])actual[0])));

        list.Clear();
        list.Add(new object());
        actual = DataContractSerializerHelper.SerializeAndDeserialize(list, "<TypeWithPrimitiveKnownTypes xmlns=\"http://schemas.datacontract.org/2004/07/\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><anyType/></TypeWithPrimitiveKnownTypes>");
        Assert.NotNull(actual);
    }

    // Random OSR might cause a stack overflow on Windows x64
    private static bool IsNotWindowsRandomOSR => !PlatformDetection.IsWindows || (Environment.GetEnvironmentVariable("DOTNET_JitRandomOnStackReplacement") == null);

    [SkipOnPlatform(TestPlatforms.Browser, "Causes a stack overflow")]
    [ConditionalFact(nameof(IsNotWindowsRandomOSR))]
    public static void DCS_DeeplyLinkedData()
    {
        TypeWithLinkedProperty head = new TypeWithLinkedProperty();
        TypeWithLinkedProperty cur = head;
        for (int i = 0; i < 513; i++)
        {
            cur.Child = new TypeWithLinkedProperty();
            cur = cur.Child;
        }
        cur.Children = new List<TypeWithLinkedProperty> { new TypeWithLinkedProperty() };
        TypeWithLinkedProperty actual = DataContractSerializerHelper.SerializeAndDeserialize(head, baseline: null, skipStringCompare: true);
        Assert.NotNull(actual);
    }

    [Fact]
    public static void DCS_DifferentCollectionsOfSameTypeAsKnownTypes()
    {
        Assert.Throws<InvalidOperationException>(() => {
            (new DataContractSerializer(typeof(TypeWithKnownTypesOfCollectionsWithConflictingXmlName))).WriteObject(new MemoryStream(), new TypeWithKnownTypesOfCollectionsWithConflictingXmlName());
        });
    }

    [Fact]
    public static void DCS_ReadObject_XmlDictionaryReaderMaxStringContentLengthExceedsQuota()
    {
        DataContractSerializer dcs = new DataContractSerializer(typeof(TypeA));
        int maxStringContentLength = 1024;
        var type = new TypeA { Name = "BOOM!".PadLeft(maxStringContentLength + 1, ' ') };
        MemoryStream ms = new MemoryStream();
        dcs.WriteObject(ms, type);
        ms.Position = 0;
        XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(ms, new System.Xml.XmlDictionaryReaderQuotas() { MaxStringContentLength = maxStringContentLength });

        Assert.Throws<System.Runtime.Serialization.SerializationException>(() => { dcs.ReadObject(reader); });
    }

    private static T DeserializeString<T>(string stringToDeserialize, bool shouldReportDeserializationExceptions = true, DataContractSerializerSettings settings = null, Func<DataContractSerializer> serializerFactory = null)
    {
        DataContractSerializer dcs;
        if (serializerFactory != null)
        {
            dcs = serializerFactory();
        }
        else
        {
            dcs = (settings != null) ? new DataContractSerializer(typeof(T), settings) : new DataContractSerializer(typeof(T));
        }

        byte[] bytesToDeserialize = Encoding.UTF8.GetBytes(stringToDeserialize);
        using (MemoryStream ms = new MemoryStream(bytesToDeserialize))
        {
            ms.Position = 0;
            T deserialized = (T)dcs.ReadObject(ms);

            return deserialized;
        }
    }

    private static string s_errorMsg = "The field/property {0} value of deserialized object is wrong";
    private static string getCheckFailureMsg(string propertyName)
    {
        return string.Format(s_errorMsg, propertyName);
    }

    private static void TestObjectInObjectContainerWithSimpleResolver<T>(T o, string baseline, bool skipStringCompare = false)
    {
        var setting = new DataContractSerializerSettings()
        {
            DataContractResolver = new SerializationTestTypes.SimpleResolver()
        };

        var value = new SerializationTestTypes.ObjectContainer(o);
        var actual = DataContractSerializerHelper.SerializeAndDeserialize(value, baseline, setting, skipStringCompare: skipStringCompare);
        SerializationTestTypes.ComparisonHelper.CompareRecursively(value, actual);
    }

    private static void TestObjectWithDifferentPayload<T>(T value, string netcorePayload, string desktopPayload, DataContractSerializerSettings settings = null)
    {
        var roundtripObject = DataContractSerializerHelper.SerializeAndDeserialize(value, string.Empty, settings, skipStringCompare: true);
        Assert.NotNull(roundtripObject);
        SerializationTestTypes.ComparisonHelper.CompareRecursively(value, roundtripObject);

        //netcorePayload
        var deserializedNetcoreObject = DeserializeString<T>(netcorePayload, settings: settings);
        Assert.NotNull(deserializedNetcoreObject);
        SerializationTestTypes.ComparisonHelper.CompareRecursively(value, deserializedNetcoreObject);

        //desktopPayload
        var deserializedDesktopObject = DeserializeString<T>(desktopPayload, settings: settings);
        Assert.NotNull(deserializedDesktopObject);
        SerializationTestTypes.ComparisonHelper.CompareRecursively(value, deserializedDesktopObject);
    }
}
