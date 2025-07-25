// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using StructLayoutAttribute = System.Runtime.InteropServices.StructLayoutAttribute;

namespace System.Reflection.TypeLoading
{
    /// <summary>
    /// Base class for all Type and TypeInfo objects created by a MetadataLoadContext.
    /// </summary>
    internal abstract partial class RoType : LeveledTypeInfo
    {
        private const TypeAttributes TypeAttributesSentinel = (TypeAttributes)(-1);

        private protected RoType() : base() { }

        public sealed override Type AsType() => this;
        public override Type UnderlyingSystemType => this;

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is RoType objType)
            {
                if (obj is not RoModifiedType)
                {
                    return base.Equals(objType);
                }
            }

            return false;
        }

        public override int GetHashCode() => base.GetHashCode();
        public abstract override bool IsTypeDefinition { get; }
        public abstract override bool IsGenericTypeDefinition { get; }
        protected abstract override bool HasElementTypeImpl();
        internal bool Call_HasElementTypeImpl() => HasElementTypeImpl();
        protected abstract override bool IsArrayImpl();
        internal bool Call_IsArrayImpl() => IsArrayImpl();
        public abstract override bool IsSZArray { get; }
        public abstract override bool IsVariableBoundArray { get; }
        protected abstract override bool IsByRefImpl();
        internal bool Call_IsByRefImpl() => IsByRefImpl();
        protected abstract override bool IsPointerImpl();
        internal bool Call_IsPointerImpl() => IsPointerImpl();
        public abstract override bool IsConstructedGenericType { get; }
        public abstract override bool IsGenericParameter { get; }
        public abstract override bool IsGenericTypeParameter { get; }
        public abstract override bool IsGenericMethodParameter { get; }
        public sealed override bool IsByRefLike => (GetClassification() & TypeClassification.IsByRefLike) != 0;

        public abstract override bool IsFunctionPointer { get; }
        public abstract override bool IsUnmanagedFunctionPointer { get; }

        public override Type[] GetFunctionPointerCallingConventions()
        {
            if (!IsFunctionPointer)
            {
                throw new InvalidOperationException(SR.InvalidOperation_NotFunctionPointer);
            }

            // Requires a modified type to return the modifiers.
            return EmptyTypes;
        }

        public abstract override Type GetFunctionPointerReturnType();
        public abstract override Type[] GetFunctionPointerParameterTypes();

        // RoModifiedType overrides these.
        public override Type[] GetOptionalCustomModifiers() => EmptyTypes;
        public override Type[] GetRequiredCustomModifiers() => EmptyTypes;

        public abstract override bool ContainsGenericParameters { get; }

        // Applies if IsGenericTypeDefinition == true
        public sealed override Type[] GenericTypeParameters => GetGenericTypeParametersNoCopy().CloneArray<Type>();
        internal abstract RoType[] GetGenericTypeParametersNoCopy();

        // Applies if HasElementType == true
        public sealed override Type? GetElementType() => GetRoElementType();
        internal abstract RoType? GetRoElementType();

        // Applies if IsArray == true
        public abstract override int GetArrayRank();

        // Applies if IsConstructedGenericType == true
        public abstract override Type GetGenericTypeDefinition();
        public sealed override Type[] GenericTypeArguments => GetGenericTypeArgumentsNoCopy().CloneArray<Type>();
        internal abstract RoType[] GetGenericTypeArgumentsNoCopy();

        // Applies if IsGenericParameter == true
        public abstract override GenericParameterAttributes GenericParameterAttributes { get; }
        public abstract override int GenericParameterPosition { get; }
        public abstract override Type[] GetGenericParameterConstraints();

        // .NET 2.0 apis for detecting/deconstructing generic type definition/constructed generic types.
        public sealed override bool IsGenericType => IsConstructedGenericType || IsGenericTypeDefinition;

        //  Don't seal since we may need to convert any modified types to unmodified.
        public override Type[] GetGenericArguments() => GetGenericArgumentsNoCopy().CloneArray();

        protected internal abstract RoType[] GetGenericArgumentsNoCopy();

        // Naming
        public sealed override string Name => field ??= ComputeName();
        protected abstract string ComputeName();
        internal string Call_ComputeName() => ComputeName();

        public sealed override string? Namespace => field ??= ComputeNamespace();
        protected abstract string? ComputeNamespace();
        internal string? Call_ComputeNamespace() => ComputeNamespace();

        public sealed override string? FullName => field ??= ComputeFullName();
        protected abstract string? ComputeFullName();
        internal string? Call_ComputeFullName() => ComputeFullName();
        public override string? AssemblyQualifiedName => field ??= ComputeAssemblyQualifiedName();
        private string? ComputeAssemblyQualifiedName()
        {
            string? fullName = FullName;
            if (fullName == null)   // Open types return null for FullName by design.
                return null;
            string? assemblyName = Assembly.FullName;
            return fullName + ", " + assemblyName;
        }

        // Assembly and module
        public sealed override Assembly Assembly => Module.Assembly;
        public sealed override Module Module => GetRoModule();
        internal abstract RoModule GetRoModule();

        // Nesting
        public sealed override Type? DeclaringType => GetRoDeclaringType();
        protected abstract RoType? ComputeDeclaringType();
        internal RoType? GetRoDeclaringType() => _lazyDeclaringType ??= ComputeDeclaringType();
        internal RoType? Call_ComputeDeclaringType() => ComputeDeclaringType();
        private volatile RoType? _lazyDeclaringType;

        public abstract override MethodBase? DeclaringMethod { get; }
        // .NET Framework compat: For types, ReflectedType == DeclaringType. Nested types are always looked up as if BindingFlags.DeclaredOnly was passed.
        // For non-nested types, the concept of a ReflectedType doesn't even make sense.
        public sealed override Type? ReflectedType => DeclaringType;

        // CustomAttributeData
        public sealed override IList<CustomAttributeData> GetCustomAttributesData() => CustomAttributes.ToReadOnlyCollection();
        public abstract override IEnumerable<CustomAttributeData> CustomAttributes { get; }

        // Optimized routines that find a custom attribute by type name only.
        internal abstract bool IsCustomAttributeDefined(ReadOnlySpan<byte> ns, ReadOnlySpan<byte> name);
        internal abstract CustomAttributeData? TryFindCustomAttribute(ReadOnlySpan<byte> ns, ReadOnlySpan<byte> name);

        // Inheritance
        public sealed override Type? BaseType => GetRoBaseType();
        internal RoType? GetRoBaseType() => object.ReferenceEquals(_lazyBaseType, Sentinels.RoType) ? (_lazyBaseType = ComputeBaseType()) : _lazyBaseType;
        private RoType? ComputeBaseType()
        {
            RoType? baseType = ComputeBaseTypeWithoutDesktopQuirk();
            if (baseType != null && baseType.IsGenericParameter)
            {
                // .NET Framework quirk: a generic parameter whose constraint is another generic parameter reports its BaseType as System.Object
                // unless that other generic parameter has a "class" constraint.
                GenericParameterAttributes genericParameterAttributes = baseType.GenericParameterAttributes;
                if (0 == (genericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint))
                    baseType = Loader.GetCoreType(CoreType.Object);
            }
            return baseType;
        }
        private volatile RoType? _lazyBaseType = Sentinels.RoType;

        //
        // This internal method implements BaseType without the following .NET Framework quirk:
        //
        //     class Foo<X,Y>
        //       where X:Y
        //       where Y:MyReferenceClass
        //
        // .NET Framework reports "X"'s base type as "System.Object" rather than "Y", even though it does
        // report any interfaces that are in MyReferenceClass's interface list.
        //
        // This seriously messes up the implementation of Type.GetInterfaces() which assumes
        // that it can recover the transitive interface closure by combining the directly mentioned interfaces and
        // the BaseType's own interface closure.
        //
        // To implement this with the least amount of code smell, we'll implement the idealized version of BaseType here
        // and make the special-case adjustment in the public version of BaseType.
        //
        internal abstract RoType? ComputeBaseTypeWithoutDesktopQuirk();

        public sealed override Type[] GetInterfaces() => GetInterfacesNoCopy().CloneArray<Type>();

        public sealed override IEnumerable<Type> ImplementedInterfaces
        {
            get
            {
                foreach (Type ifc in GetInterfacesNoCopy())
                {
                    yield return ifc;
                }
            }
        }

        internal abstract IEnumerable<RoType> ComputeDirectlyImplementedInterfaces();

        internal RoType[] GetInterfacesNoCopy() => _lazyInterfaces ??= ComputeInterfaceClosure();
        private RoType[] ComputeInterfaceClosure()
        {
            HashSet<RoType> ifcs = new HashSet<RoType>();

            RoType? baseType = ComputeBaseTypeWithoutDesktopQuirk();
            if (baseType != null)
            {
                foreach (RoType ifc in baseType.GetInterfacesNoCopy())
                {
                    ifcs.Add(ifc);
                }
            }

            foreach (RoType ifc in ComputeDirectlyImplementedInterfaces())
            {
                bool notSeenBefore = ifcs.Add(ifc);
                if (!notSeenBefore)
                {
                    foreach (RoType indirectIfc in ifc.GetInterfacesNoCopy())
                    {
                        ifcs.Add(indirectIfc);
                    }
                }
            }

            if (ifcs.Count == 0)
            {
                return Array.Empty<RoType>();
            }

            var arr = new RoType[ifcs.Count];
            ifcs.CopyTo(arr);
            return arr;
        }

        private volatile RoType[]? _lazyInterfaces;

        public sealed override InterfaceMapping GetInterfaceMap(Type interfaceType) => throw new NotSupportedException(SR.NotSupported_InterfaceMapping);

        // Assignability
        public sealed override bool IsAssignableFrom(TypeInfo? typeInfo) => IsAssignableFrom((Type?)typeInfo);
        public sealed override bool IsAssignableFrom(Type? c)
        {
            if (c == null)
                return false;

            if (object.ReferenceEquals(c, this))
                return true;

            c = c.UnderlyingSystemType;
            if (!(c is RoType roType && roType.Loader == Loader))
                return false;

            return Assignability.IsAssignableFrom(this, c, Loader.GetAllFoundCoreTypes());
        }

        // Identify interesting subgroups of Types
        protected sealed override bool IsCOMObjectImpl() => false;   // RCW's are irrelevant in a MetadataLoadContext without object creation.
        public override bool IsEnum => (GetBaseTypeClassification() & BaseTypeClassification.IsEnum) != 0;
        protected override bool IsValueTypeImpl() => (GetBaseTypeClassification() & BaseTypeClassification.IsValueType) != 0;

        // Metadata
        public abstract override int MetadataToken { get; }
        public sealed override bool HasSameMetadataDefinitionAs(MemberInfo other) => this.HasSameMetadataDefinitionAsCore(other);

        // TypeAttributes
        protected sealed override TypeAttributes GetAttributeFlagsImpl() => (_lazyTypeAttributes == TypeAttributesSentinel) ? (_lazyTypeAttributes = ComputeAttributeFlags()) : _lazyTypeAttributes;
        protected abstract TypeAttributes ComputeAttributeFlags();
        internal TypeAttributes Call_ComputeAttributeFlags() => ComputeAttributeFlags();
        private volatile TypeAttributes _lazyTypeAttributes = TypeAttributesSentinel;

        // Miscellaneous properties
        public sealed override MemberTypes MemberType => IsPublic || IsNotPublic ? MemberTypes.TypeInfo : MemberTypes.NestedType;
        protected abstract override TypeCode GetTypeCodeImpl();
        internal TypeCode Call_GetTypeCodeImpl() => GetTypeCodeImpl();
        public abstract override string ToString();

        // Random interop stuff
        public abstract override Guid GUID { get; }
        public abstract override StructLayoutAttribute? StructLayoutAttribute { get; }

        public sealed override MemberInfo[] GetDefaultMembers()
        {
            string? defaultMemberName = GetDefaultMemberName();
            return defaultMemberName != null ? GetMember(defaultMemberName) : Array.Empty<MemberInfo>();
        }

        private string? GetDefaultMemberName()
        {
            for (RoType? type = this; type != null; type = type.GetRoBaseType())
            {
                CustomAttributeData? attribute = type.TryFindCustomAttribute(Utf8Constants.SystemReflection, Utf8Constants.DefaultMemberAttribute);
                if (attribute != null)
                {
                    IList<CustomAttributeTypedArgument> fixedArguments = attribute.ConstructorArguments;
                    if (fixedArguments.Count == 1 && fixedArguments[0].Value is string memberName)
                        return memberName;
                }
            }
            return null;
        }

        // Type Factories
        public sealed override Type MakeArrayType() => this.GetUniqueArrayType();
        public sealed override Type MakeArrayType(int rank)
        {
            if (rank <= 0)
                throw new IndexOutOfRangeException(); // This is an impressively uninformative exception, unfortunately, this is the compatible behavior.

            return this.GetUniqueArrayType(rank);
        }

        public sealed override Type MakeByRefType() => this.GetUniqueByRefType();
        public sealed override Type MakePointerType() => this.GetUniquePointerType();
        [RequiresUnreferencedCode("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
        public abstract override Type MakeGenericType(params Type[] typeArguments);

        // Enum methods
        public sealed override Type GetEnumUnderlyingType() => _lazyUnderlyingEnumType ??= ComputeEnumUnderlyingType();
        protected internal abstract RoType ComputeEnumUnderlyingType();
        private volatile RoType? _lazyUnderlyingEnumType;
        public sealed override Array GetEnumValues() => throw new InvalidOperationException(SR.Arg_InvalidOperation_Reflection);

#if NET
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2085:UnrecognizedReflectionPattern",
            Justification = "Enum Types are not trimmed.")]
        public override Array GetEnumValuesAsUnderlyingType()
        {
            if (!IsEnum)
                throw new ArgumentException(SR.Arg_MustBeEnum, "enumType");

            FieldInfo[] enumFields = GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            int numValues = enumFields.Length;
            Array ret = Type.GetTypeCode(GetEnumUnderlyingType()) switch
            {
                TypeCode.Byte => new byte[numValues],
                TypeCode.SByte => new sbyte[numValues],
                TypeCode.UInt16 => new ushort[numValues],
                TypeCode.Int16 => new short[numValues],
                TypeCode.UInt32 => new uint[numValues],
                TypeCode.Int32 => new int[numValues],
                TypeCode.UInt64 => new ulong[numValues],
                TypeCode.Int64 => new long[numValues],
                _ => throw new NotSupportedException(),
            };

            for (int i = 0; i < numValues; i++)
            {
                ret.SetValue(enumFields[i].GetRawConstantValue(), i);
            }

            return ret;
        }
#endif

        // No trust environment to apply these to.
        public sealed override bool IsSecurityCritical => throw new InvalidOperationException(SR.InvalidOperation_IsSecurity);
        public sealed override bool IsSecuritySafeCritical => throw new InvalidOperationException(SR.InvalidOperation_IsSecurity);
        public sealed override bool IsSecurityTransparent => throw new InvalidOperationException(SR.InvalidOperation_IsSecurity);

        // Prohibited for ReflectionOnly types
        public sealed override RuntimeTypeHandle TypeHandle => throw new InvalidOperationException(SR.Arg_InvalidOperation_Reflection);
        public sealed override object[] GetCustomAttributes(bool inherit) => throw new InvalidOperationException(SR.Arg_ReflectionOnlyCA);
        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => throw new InvalidOperationException(SR.Arg_ReflectionOnlyCA);
        public sealed override bool IsDefined(Type attributeType, bool inherit) => throw new InvalidOperationException(SR.Arg_ReflectionOnlyCA);
        public sealed override object? InvokeMember(string name, BindingFlags invokeAttr, Binder? binder, object? target, object?[]? args, ParameterModifier[]? modifiers, CultureInfo? culture, string[]? namedParameters) => throw new InvalidOperationException(SR.Arg_ReflectionOnlyInvoke);

        // Low level support for the BindingFlag-driven enumerator apis. These return members declared (not inherited) on the current
        // type, possibly doing case-sensitive/case-insensitive filtering on a supplied name.
        internal abstract IEnumerable<ConstructorInfo> GetConstructorsCore(NameFilter? filter);
        internal abstract IEnumerable<MethodInfo> GetMethodsCore(NameFilter? filter, Type reflectedType);
        internal abstract IEnumerable<EventInfo> GetEventsCore(NameFilter? filter, Type reflectedType);
        internal abstract IEnumerable<FieldInfo> GetFieldsCore(NameFilter? filter, Type reflectedType);
        internal abstract IEnumerable<PropertyInfo> GetPropertiesCore(NameFilter? filter, Type reflectedType);
        internal abstract IEnumerable<RoType> GetNestedTypesCore(NameFilter? filter);

        // Backdoor for RoModule to invoke GetMethodImpl();
        internal MethodInfo? InternalGetMethodImpl(string name, BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[]? types, ParameterModifier[]? modifiers)
        {
            return GetMethodImpl(name, bindingAttr, binder, callConvention, types, modifiers);
        }

        // Returns the MetadataLoadContext used to load this type.
        internal MetadataLoadContext Loader => GetRoModule().Loader;
    }
}
