// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// Part of Node factory that deals with nodes describing results of generic lookups.
    /// See: <see cref="GenericLookupResult"/>.
    public partial class NodeFactory
    {
        /// <summary>
        /// Helper class that provides a level of grouping for all the generic lookup result kinds.
        /// </summary>
        public class GenericLookupResults
        {
            public GenericLookupResults()
            {
                CreateNodeCaches();
            }

            private void CreateNodeCaches()
            {
                _typeSymbols = new NodeCache<TypeDesc, GenericLookupResult>(type =>
                {
                    return new TypeHandleGenericLookupResult(type);
                });

                _necessaryTypeSymbols = new NodeCache<TypeDesc, GenericLookupResult>(type =>
                {
                    return new NecessaryTypeHandleGenericLookupResult(type);
                });

                _unwrapNullableSymbols = new NodeCache<TypeDesc, GenericLookupResult>(type =>
                {
                    return new UnwrapNullableTypeHandleGenericLookupResult(type);
                });

                _methodHandles = new NodeCache<MethodDesc, GenericLookupResult>(method =>
                {
                    return new MethodHandleGenericLookupResult(method);
                });

                _fieldHandles = new NodeCache<FieldDesc, GenericLookupResult>(field =>
                {
                    return new FieldHandleGenericLookupResult(field);
                });

                _methodDictionaries = new NodeCache<MethodDesc, GenericLookupResult>(method =>
                {
                    return new MethodDictionaryGenericLookupResult(method);
                });

                _methodEntrypoints = new NodeCache<MethodKey, GenericLookupResult>(key =>
                {
                    return new MethodEntryGenericLookupResult(key.Method, key.IsUnboxingStub);
                });

                _virtualDispatchCells = new NodeCache<MethodDesc, GenericLookupResult>(method =>
                {
                    return new VirtualDispatchCellGenericLookupResult(method);
                });

                _typeThreadStaticBaseIndexSymbols = new NodeCache<TypeDesc, GenericLookupResult>(type =>
                {
                    return new TypeThreadStaticBaseIndexGenericLookupResult(type);
                });

                _typeGCStaticBaseSymbols = new NodeCache<TypeDesc, GenericLookupResult>(type =>
                {
                    return new TypeGCStaticBaseGenericLookupResult(type);
                });

                _typeNonGCStaticBaseSymbols = new NodeCache<TypeDesc, GenericLookupResult>(type =>
                {
                    return new TypeNonGCStaticBaseGenericLookupResult(type);
                });

                _objectAllocators = new NodeCache<TypeDesc, GenericLookupResult>(type =>
                {
                    return new ObjectAllocatorGenericLookupResult(type);
                });

                _defaultCtors = new NodeCache<TypeDesc, GenericLookupResult>(type =>
                {
                    return new DefaultConstructorLookupResult(type);
                });

                _constrainedMethodUses = new NodeCache<ConstrainedMethodUseKey, GenericLookupResult>(constrainedMethodUse =>
                {
                    return new ConstrainedMethodUseLookupResult(constrainedMethodUse.ConstrainedMethod, constrainedMethodUse.ConstraintType, constrainedMethodUse.DirectCall);
                });
            }

            private NodeCache<TypeDesc, GenericLookupResult> _typeSymbols;

            public GenericLookupResult Type(TypeDesc type)
            {
                return _typeSymbols.GetOrAdd(type);
            }

            private NodeCache<TypeDesc, GenericLookupResult> _necessaryTypeSymbols;

            public GenericLookupResult NecessaryType(TypeDesc type)
            {
                return _necessaryTypeSymbols.GetOrAdd(type);
            }

            private NodeCache<TypeDesc, GenericLookupResult> _unwrapNullableSymbols;

            public GenericLookupResult UnwrapNullableType(TypeDesc type)
            {
                // An actual unwrap nullable lookup is only required if the type is exactly
                // a runtime determined instance of Nullable.
                if (type.IsRuntimeDeterminedType && ((RuntimeDeterminedType)type).CanonicalType.IsNullable)
                    return _unwrapNullableSymbols.GetOrAdd(type);
                else
                {
                    // Perform the unwrap or not eagerly, and use a normal Type GenericLookupResult
                    if (type.IsNullable)
                        return Type(type.Instantiation[0]);
                    else
                        return Type(type);
                }
            }

            private NodeCache<MethodDesc, GenericLookupResult> _methodHandles;

            public GenericLookupResult MethodHandle(MethodDesc method)
            {
                return _methodHandles.GetOrAdd(method);
            }

            private NodeCache<FieldDesc, GenericLookupResult> _fieldHandles;

            public GenericLookupResult FieldHandle(FieldDesc field)
            {
                return _fieldHandles.GetOrAdd(field);
            }

            private NodeCache<TypeDesc, GenericLookupResult> _typeThreadStaticBaseIndexSymbols;

            public GenericLookupResult TypeThreadStaticBaseIndex(TypeDesc type)
            {
                return _typeThreadStaticBaseIndexSymbols.GetOrAdd(type);
            }

            private NodeCache<TypeDesc, GenericLookupResult> _typeGCStaticBaseSymbols;

            public GenericLookupResult TypeGCStaticBase(TypeDesc type)
            {
                return _typeGCStaticBaseSymbols.GetOrAdd(type);
            }

            private NodeCache<TypeDesc, GenericLookupResult> _typeNonGCStaticBaseSymbols;

            public GenericLookupResult TypeNonGCStaticBase(TypeDesc type)
            {
                return _typeNonGCStaticBaseSymbols.GetOrAdd(type);
            }

            private NodeCache<MethodDesc, GenericLookupResult> _methodDictionaries;

            public GenericLookupResult MethodDictionary(MethodDesc method)
            {
                return _methodDictionaries.GetOrAdd(method);
            }

            private NodeCache<MethodDesc, GenericLookupResult> _virtualDispatchCells;

            public GenericLookupResult VirtualDispatchCell(MethodDesc method)
            {
                return _virtualDispatchCells.GetOrAdd(method);
            }

            private NodeCache<MethodKey, GenericLookupResult> _methodEntrypoints;

            public GenericLookupResult MethodEntry(MethodDesc method, bool isUnboxingThunk = false)
            {
                return _methodEntrypoints.GetOrAdd(new MethodKey(method, isUnboxingThunk));
            }

            private NodeCache<TypeDesc, GenericLookupResult> _objectAllocators;

            public GenericLookupResult ObjectAllocator(TypeDesc type)
            {
                return _objectAllocators.GetOrAdd(type);
            }

            private NodeCache<TypeDesc, GenericLookupResult> _defaultCtors;

            public GenericLookupResult DefaultCtorLookupResult(TypeDesc type)
            {
                return _defaultCtors.GetOrAdd(type);
            }

            private NodeCache<ConstrainedMethodUseKey, GenericLookupResult> _constrainedMethodUses;
            public GenericLookupResult ConstrainedMethodUse(MethodDesc constrainedMethod, TypeDesc constraintType, bool directCall)
            {
                return _constrainedMethodUses.GetOrAdd(new ConstrainedMethodUseKey(constrainedMethod, constraintType, directCall));
            }
        }

        public GenericLookupResults GenericLookup = new GenericLookupResults();

        private struct ConstrainedMethodUseKey : IEquatable<ConstrainedMethodUseKey>
        {
            public ConstrainedMethodUseKey(MethodDesc constrainedMethod, TypeDesc constraintType, bool directCall)
            {
                ConstrainedMethod = constrainedMethod;
                ConstraintType = constraintType;
                DirectCall = directCall;
            }

            public readonly MethodDesc ConstrainedMethod;
            public readonly TypeDesc ConstraintType;
            public readonly bool DirectCall;

            public override int GetHashCode()
            {
                return ConstraintType.GetHashCode() ^ ConstrainedMethod.GetHashCode() ^ DirectCall.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                return (obj is ConstrainedMethodUseKey) && Equals((ConstrainedMethodUseKey)obj);
            }

            public bool Equals(ConstrainedMethodUseKey other)
            {
                if (ConstraintType != other.ConstraintType)
                    return false;
                if (ConstrainedMethod != other.ConstrainedMethod)
                    return false;
                if (DirectCall != other.DirectCall)
                    return false;

                return true;
            }
        }

    }
}
