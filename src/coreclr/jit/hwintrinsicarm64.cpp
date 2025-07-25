// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "jitpch.h"
#include "hwintrinsic.h"

#ifdef FEATURE_HW_INTRINSICS

//------------------------------------------------------------------------
// Arm64VersionOfIsa: Gets the corresponding 64-bit only InstructionSet for a given InstructionSet
//
// Arguments:
//    isa -- The InstructionSet ID
//
// Return Value:
//    The 64-bit only InstructionSet associated with isa
static CORINFO_InstructionSet Arm64VersionOfIsa(CORINFO_InstructionSet isa)
{
    switch (isa)
    {
        case InstructionSet_AdvSimd:
            return InstructionSet_AdvSimd_Arm64;
        case InstructionSet_Aes:
            return InstructionSet_Aes_Arm64;
        case InstructionSet_ArmBase:
            return InstructionSet_ArmBase_Arm64;
        case InstructionSet_Crc32:
            return InstructionSet_Crc32_Arm64;
        case InstructionSet_Dp:
            return InstructionSet_Dp_Arm64;
        case InstructionSet_Sha1:
            return InstructionSet_Sha1_Arm64;
        case InstructionSet_Sha256:
            return InstructionSet_Sha256_Arm64;
        case InstructionSet_Rdm:
            return InstructionSet_Rdm_Arm64;
        case InstructionSet_Sve:
            return InstructionSet_Sve_Arm64;
        case InstructionSet_Sve2:
            return InstructionSet_Sve2_Arm64;
        default:
            return InstructionSet_NONE;
    }
}

//------------------------------------------------------------------------
// lookupInstructionSet: Gets the InstructionSet for a given class name
//
// Arguments:
//    className -- The name of the class associated with the InstructionSet to lookup
//
// Return Value:
//    The InstructionSet associated with className
CORINFO_InstructionSet Compiler::lookupInstructionSet(const char* className)
{
    assert(className != nullptr);

    if (className[0] == 'A')
    {
        if (strcmp(className, "AdvSimd") == 0)
        {
            return InstructionSet_AdvSimd;
        }
        if (strcmp(className, "Aes") == 0)
        {
            return InstructionSet_Aes;
        }
        if (strcmp(className, "ArmBase") == 0)
        {
            return InstructionSet_ArmBase;
        }
    }
    else if (className[0] == 'C')
    {
        if (strcmp(className, "Crc32") == 0)
        {
            return InstructionSet_Crc32;
        }
    }
    else if (className[0] == 'D')
    {
        if (strcmp(className, "Dp") == 0)
        {
            return InstructionSet_Dp;
        }
    }
    else if (className[0] == 'R')
    {
        if (strcmp(className, "Rdm") == 0)
        {
            return InstructionSet_Rdm;
        }
    }
    else if (className[0] == 'S')
    {
        if (strcmp(className, "Sha1") == 0)
        {
            return InstructionSet_Sha1;
        }
        if (strcmp(className, "Sha256") == 0)
        {
            return InstructionSet_Sha256;
        }
        if (strcmp(className, "Sve2") == 0)
        {
            return InstructionSet_Sve2;
        }
        if (strcmp(className, "Sve") == 0)
        {
            return InstructionSet_Sve;
        }
    }
    else if (className[0] == 'V')
    {
        if (strncmp(className, "Vector64", 8) == 0)
        {
            return InstructionSet_Vector64;
        }
        else if (strncmp(className, "Vector128", 9) == 0)
        {
            return InstructionSet_Vector128;
        }
    }

    return InstructionSet_ILLEGAL;
}

//------------------------------------------------------------------------
// lookupIsa: Gets the InstructionSet for a given class name and enclsoing class name
//
// Arguments:
//    className -- The name of the class associated with the InstructionSet to lookup
//    innerEnclosingClassName -- The name of the inner enclosing class or nullptr if one doesn't exist
//    outerEnclosingClassName -- The name of the outer enclosing class or nullptr if one doesn't exist
//
// Return Value:
//    The InstructionSet associated with className and enclosingClassName
//
CORINFO_InstructionSet Compiler::lookupIsa(const char* className,
                                           const char* innerEnclosingClassName,
                                           const char* outerEnclosingClassName)
{
    assert(className != nullptr);

    if (innerEnclosingClassName == nullptr)
    {
        // No nested class is the most common, so fast path it
        return lookupInstructionSet(className);
    }

    // Since lookupId is only called for the xplat intrinsics
    // or intrinsics in the platform specific namespace, we assume
    // that it will be one we can handle and don't try to early out.

    CORINFO_InstructionSet enclosingIsa = lookupIsa(innerEnclosingClassName, outerEnclosingClassName, nullptr);

    if (strcmp(className, "Arm64") == 0)
    {
        return Arm64VersionOfIsa(enclosingIsa);
    }

    return InstructionSet_ILLEGAL;
}

//------------------------------------------------------------------------
// lookupIval: Gets a the implicit immediate value for the given intrinsic
//
// Arguments:
//    id           - The intrinsic for which to get the ival
//
// Return Value:
//    The immediate value for the given intrinsic or -1 if none exists
int HWIntrinsicInfo::lookupIval(NamedIntrinsic id)
{
    switch (id)
    {
        case NI_Sve_Compute16BitAddresses:
            return 1;
        case NI_Sve_Compute32BitAddresses:
            return 2;
        case NI_Sve_Compute64BitAddresses:
            return 3;
        case NI_Sve_Compute8BitAddresses:
            return 0;
        default:
            unreached();
    }
    return -1;
}

//------------------------------------------------------------------------
// getHWIntrinsicImmOps: Gets the immediate Ops for an intrinsic
//
// Arguments:
//    intrinsic       -- NamedIntrinsic associated with the HWIntrinsic to lookup
//    sig             -- signature of the intrinsic call.
//    immOp1Ptr [OUT] -- The first immediate Op
//    immOp2Ptr [OUT] -- The second immediate Op, if any. Otherwise unchanged.
//
void Compiler::getHWIntrinsicImmOps(NamedIntrinsic    intrinsic,
                                    CORINFO_SIG_INFO* sig,
                                    GenTree**         immOp1Ptr,
                                    GenTree**         immOp2Ptr)
{
    if (!HWIntrinsicInfo::HasImmediateOperand(intrinsic))
    {
        return;
    }

    // Position of the immediates from top of stack
    int imm1Pos = -1;
    int imm2Pos = -1;

    HWIntrinsicInfo::GetImmOpsPositions(intrinsic, sig, &imm1Pos, &imm2Pos);

    if (imm1Pos >= 0)
    {
        *immOp1Ptr = impStackTop(imm1Pos).val;
        assert(HWIntrinsicInfo::isImmOp(intrinsic, *immOp1Ptr));
    }

    if (imm2Pos >= 0)
    {
        *immOp2Ptr = impStackTop(imm2Pos).val;
        assert(HWIntrinsicInfo::isImmOp(intrinsic, *immOp2Ptr));
    }
}

//------------------------------------------------------------------------
// getHWIntrinsicImmTypes: Gets the type/size for an immediate for an intrinsic
//                         if it differs from the default type/size of the instrinsic
//
// Arguments:
//    intrinsic                -- NamedIntrinsic associated with the HWIntrinsic to lookup
//    sig                      -- signature of the intrinsic call.
//    immNumber                -- Which immediate to use (1 for most intrinsics)
//    immSimdSize [IN/OUT]     -- Size of the immediate to override
//    immSimdBaseType [IN/OUT] -- Base type of the immediate to override
//
void Compiler::getHWIntrinsicImmTypes(NamedIntrinsic    intrinsic,
                                      CORINFO_SIG_INFO* sig,
                                      unsigned          immNumber,
                                      unsigned*         immSimdSize,
                                      var_types*        immSimdBaseType)
{
    HWIntrinsicCategory category = HWIntrinsicInfo::lookupCategory(intrinsic);

    if (category == HW_Category_SIMDByIndexedElement)
    {
        assert(immNumber == 1);
        *immSimdSize                   = 0;
        CORINFO_ARG_LIST_HANDLE immArg = sig->args;

        switch (sig->numArgs)
        {
            case 4:
                immArg = info.compCompHnd->getArgNext(immArg);
                FALLTHROUGH;
            case 3:
                immArg = info.compCompHnd->getArgNext(immArg);
                FALLTHROUGH;
            case 2:
            {
                CORINFO_CLASS_HANDLE typeHnd = info.compCompHnd->getArgClass(sig, immArg);
                getBaseJitTypeAndSizeOfSIMDType(typeHnd, immSimdSize);
                break;
            }
            default:
                unreached();
        }
    }
    else if (intrinsic == NI_AdvSimd_Arm64_InsertSelectedScalar)
    {
        if (immNumber == 2)
        {
            CORINFO_ARG_LIST_HANDLE immArg        = sig->args;
            immArg                                = info.compCompHnd->getArgNext(immArg);
            immArg                                = info.compCompHnd->getArgNext(immArg);
            CORINFO_CLASS_HANDLE typeHnd          = info.compCompHnd->getArgClass(sig, immArg);
            CorInfoType          otherBaseJitType = getBaseJitTypeAndSizeOfSIMDType(typeHnd, immSimdSize);
            *immSimdBaseType                      = JitType2PreciseVarType(otherBaseJitType);
        }
        // For imm1 use default simd sizes.
    }

    // For all other imms, use default simd sizes
}

//------------------------------------------------------------------------
// lookupImmBounds: Gets the lower and upper bounds for the imm-value of a given NamedIntrinsic
//
// Arguments:
//    intrinsic -- NamedIntrinsic associated with the HWIntrinsic to lookup
//    simdType  -- vector size
//    baseType  -- base type of the Vector64/128<T>
//    immNumber -- which immediate operand to check for (most intrinsics only have one)
//    pImmLowerBound [OUT] - The lower incl. bound for a value of the intrinsic immediate operand
//    pImmUpperBound [OUT] - The upper incl. bound for a value of the intrinsic immediate operand
//
void HWIntrinsicInfo::lookupImmBounds(
    NamedIntrinsic intrinsic, int simdSize, var_types baseType, int immNumber, int* pImmLowerBound, int* pImmUpperBound)
{
    HWIntrinsicCategory category            = HWIntrinsicInfo::lookupCategory(intrinsic);
    bool                hasImmediateOperand = HasImmediateOperand(intrinsic);

    assert(hasImmediateOperand);

    assert(pImmLowerBound != nullptr);
    assert(pImmUpperBound != nullptr);

    int immLowerBound = 0;
    int immUpperBound = 0;

    if (category == HW_Category_ShiftLeftByImmediate)
    {
        int size = genTypeSize(baseType);

        if (intrinsic == NI_Sve2_ShiftLeftLogicalWideningEven || intrinsic == NI_Sve2_ShiftLeftLogicalWideningOdd)
        {
            // Edge case for widening shifts. The base type is the wide type, but the maximum shift is the number
            // of bits in the narrow type.
            size /= 2;
        }

        // The left shift amount is in the range 0 to the element width in bits minus 1.
        immUpperBound = BITS_PER_BYTE * size - 1;
    }
    else if (category == HW_Category_ShiftRightByImmediate)
    {
        // The right shift amount, in the range 1 to the element width in bits.
        immLowerBound = 1;
        immUpperBound = BITS_PER_BYTE * genTypeSize(baseType);
    }
    else if (category == HW_Category_SIMDByIndexedElement)
    {
        switch (intrinsic)
        {
            case NI_Sve_DuplicateSelectedScalarToVector:
                // For SVE_DUP, the upper bound on index does not depend on the vector length.
                immUpperBound = (512 / (BITS_PER_BYTE * genTypeSize(baseType))) - 1;
                break;
            case NI_Sve2_MultiplyBySelectedScalarWideningEven:
            case NI_Sve2_MultiplyBySelectedScalarWideningEvenAndAdd:
            case NI_Sve2_MultiplyBySelectedScalarWideningEvenAndSubtract:
            case NI_Sve2_MultiplyBySelectedScalarWideningOdd:
            case NI_Sve2_MultiplyBySelectedScalarWideningOddAndAdd:
            case NI_Sve2_MultiplyBySelectedScalarWideningOddAndSubtract:
            case NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndAddSaturateEven:
            case NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndAddSaturateOdd:
            case NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndSubtractSaturateEven:
            case NI_Sve2_MultiplyDoublingWideningBySelectedScalarAndSubtractSaturateOdd:
                // Index is on the half-width vector, hence double the maximum index.
                immUpperBound = Compiler::getSIMDVectorLength(simdSize, baseType) * 2 - 1;
                break;
            default:
                immUpperBound = Compiler::getSIMDVectorLength(simdSize, baseType) - 1;
                break;
        }
    }
    else
    {
        switch (intrinsic)
        {
            case NI_AdvSimd_DuplicateSelectedScalarToVector64:
            case NI_AdvSimd_DuplicateSelectedScalarToVector128:
            case NI_AdvSimd_Extract:
            case NI_AdvSimd_ExtractVector128:
            case NI_AdvSimd_ExtractVector64:
            case NI_AdvSimd_Insert:
            case NI_AdvSimd_InsertScalar:
            case NI_AdvSimd_LoadAndInsertScalar:
            case NI_AdvSimd_LoadAndInsertScalarVector64x2:
            case NI_AdvSimd_LoadAndInsertScalarVector64x3:
            case NI_AdvSimd_LoadAndInsertScalarVector64x4:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3:
            case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4:
            case NI_AdvSimd_StoreSelectedScalar:
            case NI_AdvSimd_Arm64_StoreSelectedScalar:
            case NI_AdvSimd_Arm64_DuplicateSelectedScalarToVector128:
            case NI_AdvSimd_Arm64_InsertSelectedScalar:
            case NI_Sve_FusedMultiplyAddBySelectedScalar:
            case NI_Sve_FusedMultiplySubtractBySelectedScalar:
            case NI_Sve_ExtractVector:
                immUpperBound = Compiler::getSIMDVectorLength(simdSize, baseType) - 1;
                break;

            case NI_Sve_CreateTrueMaskByte:
            case NI_Sve_CreateTrueMaskDouble:
            case NI_Sve_CreateTrueMaskInt16:
            case NI_Sve_CreateTrueMaskInt32:
            case NI_Sve_CreateTrueMaskInt64:
            case NI_Sve_CreateTrueMaskSByte:
            case NI_Sve_CreateTrueMaskSingle:
            case NI_Sve_CreateTrueMaskUInt16:
            case NI_Sve_CreateTrueMaskUInt32:
            case NI_Sve_CreateTrueMaskUInt64:
            case NI_Sve_Count16BitElements:
            case NI_Sve_Count32BitElements:
            case NI_Sve_Count64BitElements:
            case NI_Sve_Count8BitElements:
                immLowerBound = (int)SVE_PATTERN_POW2;
                immUpperBound = (int)SVE_PATTERN_ALL;
                break;

            case NI_Sve_SaturatingDecrementBy16BitElementCount:
            case NI_Sve_SaturatingDecrementBy32BitElementCount:
            case NI_Sve_SaturatingDecrementBy64BitElementCount:
            case NI_Sve_SaturatingDecrementBy8BitElementCount:
            case NI_Sve_SaturatingIncrementBy16BitElementCount:
            case NI_Sve_SaturatingIncrementBy32BitElementCount:
            case NI_Sve_SaturatingIncrementBy64BitElementCount:
            case NI_Sve_SaturatingIncrementBy8BitElementCount:
            case NI_Sve_SaturatingDecrementBy16BitElementCountScalar:
            case NI_Sve_SaturatingDecrementBy32BitElementCountScalar:
            case NI_Sve_SaturatingDecrementBy64BitElementCountScalar:
            case NI_Sve_SaturatingIncrementBy16BitElementCountScalar:
            case NI_Sve_SaturatingIncrementBy32BitElementCountScalar:
            case NI_Sve_SaturatingIncrementBy64BitElementCountScalar:
                if (immNumber == 1)
                {
                    immLowerBound = 1;
                    immUpperBound = 16;
                }
                else
                {
                    assert(immNumber == 2);
                    immLowerBound = (int)SVE_PATTERN_POW2;
                    immUpperBound = (int)SVE_PATTERN_ALL;
                }
                break;

            case NI_Sve_GatherPrefetch8Bit:
            case NI_Sve_GatherPrefetch16Bit:
            case NI_Sve_GatherPrefetch32Bit:
            case NI_Sve_GatherPrefetch64Bit:
            case NI_Sve_Prefetch16Bit:
            case NI_Sve_Prefetch32Bit:
            case NI_Sve_Prefetch64Bit:
            case NI_Sve_Prefetch8Bit:
                immLowerBound = (int)SVE_PRFOP_PLDL1KEEP;
                immUpperBound = (int)SVE_PRFOP_CONST15;
                break;

            case NI_Sve_AddRotateComplex:
                immLowerBound = 0;
                immUpperBound = 1;
                break;

            case NI_Sve_MultiplyAddRotateComplex:
            case NI_Sve2_DotProductRotateComplex:
                immLowerBound = 0;
                immUpperBound = 3;
                break;

            case NI_Sve_MultiplyAddRotateComplexBySelectedScalar:
                // rotation comes after index in the intrinsic's signature,
                // but flip the order here so we check the larger range first.
                // This conforms to the existing logic in LinearScan::BuildHWIntrinsic
                // when determining if we need an internal register for the jump table.
                // This flipped ordering is reflected in HWIntrinsicInfo::GetImmOpsPositions.
                if (immNumber == 1)
                {
                    // Bounds for rotation
                    immLowerBound = 0;
                    immUpperBound = 3;
                }
                else
                {
                    // Bounds for index
                    assert(immNumber == 2);
                    immLowerBound = 0;
                    immUpperBound = 1;
                }
                break;

            case NI_Sve2_DotProductRotateComplexBySelectedIndex:
                if (immNumber == 1)
                {
                    // Bounds for rotation
                    immLowerBound = 0;
                    immUpperBound = 3;
                }
                else
                {
                    // Bounds for index
                    assert(immNumber == 2);
                    assert(baseType == TYP_BYTE || baseType == TYP_SHORT);
                    immLowerBound = 0;
                    immUpperBound = (baseType == TYP_BYTE) ? 3 : 1;
                }
                break;

            case NI_Sve_TrigonometricMultiplyAddCoefficient:
                immLowerBound = 0;
                immUpperBound = 7;
                break;

            default:
                unreached();
        }
    }

    assert(immLowerBound <= immUpperBound);

    *pImmLowerBound = immLowerBound;
    *pImmUpperBound = immUpperBound;
}

//------------------------------------------------------------------------
// impNonConstFallback: generate alternate code when the imm-arg is not a compile-time constant
//
// Arguments:
//    intrinsic       -- intrinsic ID
//    simdType        -- Vector type
//    simdBaseJitType -- base JIT type of the Vector64/128<T>
//
// Return Value:
//     return the IR of semantic alternative on non-const imm-arg
//
GenTree* Compiler::impNonConstFallback(NamedIntrinsic intrinsic, var_types simdType, CorInfoType simdBaseJitType)
{
    bool isRightShift = true;

    switch (intrinsic)
    {
        case NI_AdvSimd_ShiftLeftLogical:
        case NI_AdvSimd_ShiftLeftLogicalScalar:
            isRightShift = false;
            FALLTHROUGH;

        case NI_AdvSimd_ShiftRightLogical:
        case NI_AdvSimd_ShiftRightLogicalScalar:
        case NI_AdvSimd_ShiftRightArithmetic:
        case NI_AdvSimd_ShiftRightArithmeticScalar:
        {
            // AdvSimd.ShiftLeft* and AdvSimd.ShiftRight* can be replaced with AdvSimd.Shift*, which takes op2 in a simd
            // register

            GenTree* op2 = impPopStack().val;
            GenTree* op1 = impSIMDPopStack();

            // AdvSimd.ShiftLogical does right-shifts with negative immediates, hence the negation
            if (isRightShift)
            {
                op2 = gtNewOperNode(GT_NEG, genActualType(op2->TypeGet()), op2);
            }

            NamedIntrinsic fallbackIntrinsic;
            switch (intrinsic)
            {
                case NI_AdvSimd_ShiftLeftLogical:
                case NI_AdvSimd_ShiftRightLogical:
                    fallbackIntrinsic = NI_AdvSimd_ShiftLogical;
                    break;

                case NI_AdvSimd_ShiftLeftLogicalScalar:
                case NI_AdvSimd_ShiftRightLogicalScalar:
                    fallbackIntrinsic = NI_AdvSimd_ShiftLogicalScalar;
                    break;

                case NI_AdvSimd_ShiftRightArithmetic:
                    fallbackIntrinsic = NI_AdvSimd_ShiftArithmetic;
                    break;

                case NI_AdvSimd_ShiftRightArithmeticScalar:
                    fallbackIntrinsic = NI_AdvSimd_ShiftArithmeticScalar;
                    break;

                default:
                    unreached();
            }

            GenTree* tmpOp = gtNewSimdCreateBroadcastNode(simdType, op2, simdBaseJitType, genTypeSize(simdType));
            return gtNewSimdHWIntrinsicNode(simdType, op1, tmpOp, fallbackIntrinsic, simdBaseJitType,
                                            genTypeSize(simdType));
        }

        default:
            return nullptr;
    }
}

//------------------------------------------------------------------------
// impSpecialIntrinsic: Import a hardware intrinsic that requires special handling as a GT_HWINTRINSIC node if possible
//
// Arguments:
//    intrinsic       -- id of the intrinsic function.
//    clsHnd          -- class handle containing the intrinsic function.
//    method          -- method handle of the intrinsic function.
//    sig             -- signature of the intrinsic call.
//    entryPoint      -- The entry point information required for R2R scenarios
//    simdBaseJitType -- generic argument of the intrinsic.
//    retType         -- return type of the intrinsic.
//    mustExpand      -- true if the intrinsic must return a GenTree*; otherwise, false
//
// Return Value:
//    The GT_HWINTRINSIC node, or nullptr if not a supported intrinsic
//
GenTree* Compiler::impSpecialIntrinsic(NamedIntrinsic        intrinsic,
                                       CORINFO_CLASS_HANDLE  clsHnd,
                                       CORINFO_METHOD_HANDLE method,
                                       CORINFO_SIG_INFO* sig R2RARG(CORINFO_CONST_LOOKUP* entryPoint),
                                       CorInfoType           simdBaseJitType,
                                       var_types             retType,
                                       unsigned              simdSize,
                                       bool                  mustExpand)
{
    const HWIntrinsicCategory category = HWIntrinsicInfo::lookupCategory(intrinsic);
    const int                 numArgs  = sig->numArgs;

    // The vast majority of "special" intrinsics are Vector64/Vector128 methods.
    // The only exception is ArmBase.Yield which should be treated differently.
    if (intrinsic == NI_ArmBase_Yield)
    {
        assert(sig->numArgs == 0);
        assert(JITtype2varType(sig->retType) == TYP_VOID);
        assert(simdSize == 0);

        return gtNewScalarHWIntrinsicNode(TYP_VOID, intrinsic);
    }

    bool isScalar = (category == HW_Category_Scalar);
    assert(numArgs >= 0);

    var_types simdBaseType = JitType2PreciseVarType(simdBaseJitType);
    assert(varTypeIsArithmetic(simdBaseType));

    GenTree* retNode = nullptr;
    GenTree* op1     = nullptr;
    GenTree* op2     = nullptr;
    GenTree* op3     = nullptr;
    GenTree* op4     = nullptr;

#ifdef DEBUG
    bool isValidScalarIntrinsic = false;
#endif

    bool isMinMaxIntrinsic = false;
    bool isMax             = false;
    bool isMagnitude       = false;
    bool isNative          = false;
    bool isNumber          = false;

    switch (intrinsic)
    {
        case NI_Vector64_Abs:
        case NI_Vector128_Abs:
        {
            assert(sig->numArgs == 1);
            op1     = impSIMDPopStack();
            retNode = gtNewSimdAbsNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_op_Addition:
        case NI_Vector128_op_Addition:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdBinOpNode(GT_ADD, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_AddSaturate:
        case NI_Vector128_AddSaturate:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            if (varTypeIsFloating(simdBaseType))
            {
                retNode = gtNewSimdBinOpNode(GT_ADD, retType, op1, op2, simdBaseJitType, simdSize);
            }
            else
            {
                intrinsic = NI_AdvSimd_AddSaturate;

                if ((simdSize == 8) && varTypeIsLong(simdBaseType))
                {
                    intrinsic = NI_AdvSimd_AddSaturateScalar;
                }

                retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, intrinsic, simdBaseJitType, simdSize);
            }
            break;
        }

        case NI_AdvSimd_BitwiseClear:
        case NI_Vector64_AndNot:
        case NI_Vector128_AndNot:
        {
            assert(sig->numArgs == 2);

            // We don't want to support creating AND_NOT nodes prior to LIR
            // as it can break important optimizations. We'll produces this
            // in lowering instead so decompose into the individual operations
            // on import

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            op2     = gtFoldExpr(gtNewSimdUnOpNode(GT_NOT, retType, op2, simdBaseJitType, simdSize));
            retNode = gtNewSimdBinOpNode(GT_AND, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_AdvSimd_OrNot:
        {
            assert(sig->numArgs == 2);

            // We don't want to support creating OR_NOT nodes prior to LIR
            // as it can break important optimizations. We'll produces this
            // in lowering instead so decompose into the individual operations
            // on import

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            op2     = gtFoldExpr(gtNewSimdUnOpNode(GT_NOT, retType, op2, simdBaseJitType, simdSize));
            retNode = gtNewSimdBinOpNode(GT_OR, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_As:
        case NI_Vector64_AsByte:
        case NI_Vector64_AsDouble:
        case NI_Vector64_AsInt16:
        case NI_Vector64_AsInt32:
        case NI_Vector64_AsInt64:
        case NI_Vector64_AsNInt:
        case NI_Vector64_AsNUInt:
        case NI_Vector64_AsSByte:
        case NI_Vector64_AsSingle:
        case NI_Vector64_AsUInt16:
        case NI_Vector64_AsUInt32:
        case NI_Vector64_AsUInt64:
        case NI_Vector128_As:
        case NI_Vector128_AsByte:
        case NI_Vector128_AsDouble:
        case NI_Vector128_AsInt16:
        case NI_Vector128_AsInt32:
        case NI_Vector128_AsInt64:
        case NI_Vector128_AsNInt:
        case NI_Vector128_AsNUInt:
        case NI_Vector128_AsSByte:
        case NI_Vector128_AsSingle:
        case NI_Vector128_AsUInt16:
        case NI_Vector128_AsUInt32:
        case NI_Vector128_AsUInt64:
        case NI_Vector128_AsVector:
        case NI_Vector128_AsVector4:
        {
            assert(!sig->hasThis());
            assert(numArgs == 1);

            // We fold away the cast here, as it only exists to satisfy
            // the type system. It is safe to do this here since the retNode type
            // and the signature return type are both the same TYP_SIMD.

            retNode = impSIMDPopStack();
            SetOpLclRelatedToSIMDIntrinsic(retNode);
            assert(retNode->gtType == getSIMDTypeForSize(getSIMDTypeSizeInBytes(sig->retTypeSigClass)));
            break;
        }

        case NI_Vector128_AsVector2:
        {
            assert(sig->numArgs == 1);
            assert((simdSize == 16) && (simdBaseType == TYP_FLOAT));
            assert(retType == TYP_SIMD8);

            op1     = impSIMDPopStack();
            retNode = gtNewSimdGetLowerNode(TYP_SIMD8, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector128_AsVector3:
        {
            assert(sig->numArgs == 1);
            assert((simdSize == 16) && (simdBaseType == TYP_FLOAT));
            assert(retType == TYP_SIMD12);

            op1     = impSIMDPopStack();
            retNode = gtNewSimdHWIntrinsicNode(retType, op1, intrinsic, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector128_AsVector128:
        {
            assert(!sig->hasThis());
            assert(numArgs == 1);
            assert(retType == TYP_SIMD16);

            switch (getSIMDTypeForSize(simdSize))
            {
                case TYP_SIMD8:
                {
                    assert((simdSize == 8) && (simdBaseType == TYP_FLOAT));

                    op1 = impSIMDPopStack();

                    if (op1->IsCnsVec())
                    {
                        GenTreeVecCon* vecCon = op1->AsVecCon();
                        vecCon->gtType        = TYP_SIMD16;

                        vecCon->gtSimdVal.f32[2] = 0.0f;
                        vecCon->gtSimdVal.f32[3] = 0.0f;

                        return vecCon;
                    }

                    op1 = gtNewSimdHWIntrinsicNode(retType, op1, NI_Vector64_ToVector128Unsafe, simdBaseJitType, 8);

                    GenTree* idx  = gtNewIconNode(2, TYP_INT);
                    GenTree* zero = gtNewZeroConNode(TYP_FLOAT);
                    op1           = gtNewSimdWithElementNode(retType, op1, idx, zero, simdBaseJitType, 16);

                    idx     = gtNewIconNode(3, TYP_INT);
                    zero    = gtNewZeroConNode(TYP_FLOAT);
                    retNode = gtNewSimdWithElementNode(retType, op1, idx, zero, simdBaseJitType, 16);

                    break;
                }

                case TYP_SIMD12:
                {
                    assert((simdSize == 12) && (simdBaseType == TYP_FLOAT));

                    op1 = impSIMDPopStack();

                    if (op1->IsCnsVec())
                    {
                        GenTreeVecCon* vecCon = op1->AsVecCon();
                        vecCon->gtType        = TYP_SIMD16;

                        vecCon->gtSimdVal.f32[3] = 0.0f;
                        return vecCon;
                    }

                    op1 = gtNewSimdHWIntrinsicNode(retType, op1, NI_Vector128_AsVector128Unsafe, simdBaseJitType, 12);

                    GenTree* idx  = gtNewIconNode(3, TYP_INT);
                    GenTree* zero = gtNewZeroConNode(TYP_FLOAT);
                    retNode       = gtNewSimdWithElementNode(retType, op1, idx, zero, simdBaseJitType, 16);
                    break;
                }

                case TYP_SIMD16:
                {
                    // We fold away the cast here, as it only exists to satisfy
                    // the type system. It is safe to do this here since the retNode type
                    // and the signature return type are both the same TYP_SIMD.

                    retNode = impSIMDPopStack();
                    SetOpLclRelatedToSIMDIntrinsic(retNode);
                    assert(retNode->gtType == getSIMDTypeForSize(getSIMDTypeSizeInBytes(sig->retTypeSigClass)));
                    break;
                }

                default:
                {
                    unreached();
                }
            }

            break;
        }

        case NI_Vector128_AsVector128Unsafe:
        {
            assert(sig->numArgs == 1);
            assert(retType == TYP_SIMD16);
            assert(simdBaseJitType == CORINFO_TYPE_FLOAT);
            assert((simdSize == 8) || (simdSize == 12));

            op1     = impSIMDPopStack();
            retNode = gtNewSimdHWIntrinsicNode(retType, op1, NI_Vector128_AsVector128Unsafe, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_op_BitwiseAnd:
        case NI_Vector128_op_BitwiseAnd:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdBinOpNode(GT_AND, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_op_BitwiseOr:
        case NI_Vector128_op_BitwiseOr:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdBinOpNode(GT_OR, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_Ceiling:
        case NI_Vector128_Ceiling:
        {
            assert(sig->numArgs == 1);

            if (!varTypeIsFloating(simdBaseType))
            {
                retNode = impSIMDPopStack();
                break;
            }

            op1     = impSIMDPopStack();
            retNode = gtNewSimdCeilNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_ConditionalSelect:
        case NI_Vector128_ConditionalSelect:
        {
            assert(sig->numArgs == 3);

            op3 = impSIMDPopStack();
            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCndSelNode(retType, op1, op2, op3, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_ConvertToDouble:
        case NI_Vector128_ConvertToDouble:
        {
            assert(sig->numArgs == 1);
            assert((simdBaseType == TYP_LONG) || (simdBaseType == TYP_ULONG));

            intrinsic = (simdSize == 8) ? NI_AdvSimd_Arm64_ConvertToDoubleScalar : NI_AdvSimd_Arm64_ConvertToDouble;

            op1     = impSIMDPopStack();
            retNode = gtNewSimdHWIntrinsicNode(retType, op1, intrinsic, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_ConvertToInt32Native:
        case NI_Vector128_ConvertToInt32Native:
        {
            if (BlockNonDeterministicIntrinsics(mustExpand))
            {
                break;
            }
            FALLTHROUGH;
        }

        case NI_Vector64_ConvertToInt32:
        case NI_Vector128_ConvertToInt32:
        {
            assert(sig->numArgs == 1);
            assert(simdBaseType == TYP_FLOAT);

            op1     = impSIMDPopStack();
            retNode = gtNewSimdCvtNativeNode(retType, op1, CORINFO_TYPE_INT, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_ConvertToInt64Native:
        case NI_Vector128_ConvertToInt64Native:
        {
            if (BlockNonDeterministicIntrinsics(mustExpand))
            {
                break;
            }
            FALLTHROUGH;
        }

        case NI_Vector64_ConvertToInt64:
        case NI_Vector128_ConvertToInt64:
        {
            assert(sig->numArgs == 1);
            assert(simdBaseType == TYP_DOUBLE);

            op1     = impSIMDPopStack();
            retNode = gtNewSimdCvtNativeNode(retType, op1, CORINFO_TYPE_LONG, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_ConvertToSingle:
        case NI_Vector128_ConvertToSingle:
        {
            assert(sig->numArgs == 1);
            assert((simdBaseType == TYP_INT) || (simdBaseType == TYP_UINT));

            op1     = impSIMDPopStack();
            retNode = gtNewSimdHWIntrinsicNode(retType, op1, NI_AdvSimd_ConvertToSingle, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_ConvertToUInt32Native:
        case NI_Vector128_ConvertToUInt32Native:
        {
            if (BlockNonDeterministicIntrinsics(mustExpand))
            {
                break;
            }
            FALLTHROUGH;
        }

        case NI_Vector64_ConvertToUInt32:
        case NI_Vector128_ConvertToUInt32:
        {
            assert(sig->numArgs == 1);
            assert(simdBaseType == TYP_FLOAT);

            op1     = impSIMDPopStack();
            retNode = gtNewSimdCvtNativeNode(retType, op1, CORINFO_TYPE_UINT, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_ConvertToUInt64Native:
        case NI_Vector128_ConvertToUInt64Native:
        {
            if (BlockNonDeterministicIntrinsics(mustExpand))
            {
                break;
            }
            FALLTHROUGH;
        }

        case NI_Vector64_ConvertToUInt64:
        case NI_Vector128_ConvertToUInt64:
        {
            assert(sig->numArgs == 1);
            assert(simdBaseType == TYP_DOUBLE);

            op1     = impSIMDPopStack();
            retNode = gtNewSimdCvtNativeNode(retType, op1, CORINFO_TYPE_ULONG, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_Create:
        case NI_Vector128_Create:
        {
            if (sig->numArgs == 1)
            {
                op1     = impPopStack().val;
                retNode = gtNewSimdCreateBroadcastNode(retType, op1, simdBaseJitType, simdSize);
                break;
            }

            uint32_t simdLength = getSIMDVectorLength(simdSize, simdBaseType);
            assert(sig->numArgs == simdLength);

            bool isConstant = true;

            if (varTypeIsFloating(simdBaseType))
            {
                for (uint32_t index = 0; index < sig->numArgs; index++)
                {
                    GenTree* arg = impStackTop(index).val;

                    if (!arg->IsCnsFltOrDbl())
                    {
                        isConstant = false;
                        break;
                    }
                }
            }
            else
            {
                assert(varTypeIsIntegral(simdBaseType));

                for (uint32_t index = 0; index < sig->numArgs; index++)
                {
                    GenTree* arg = impStackTop(index).val;

                    if (!arg->IsIntegralConst())
                    {
                        isConstant = false;
                        break;
                    }
                }
            }

            if (isConstant)
            {
                // Some of the below code assumes 8 or 16 byte SIMD types
                assert((simdSize == 8) || (simdSize == 16));

                GenTreeVecCon* vecCon = gtNewVconNode(retType);

                switch (simdBaseType)
                {
                    case TYP_BYTE:
                    case TYP_UBYTE:
                    {
                        uint8_t cnsVal = 0;

                        for (uint32_t index = 0; index < sig->numArgs; index++)
                        {
                            cnsVal = static_cast<uint8_t>(impPopStack().val->AsIntConCommon()->IntegralValue());
                            vecCon->gtSimdVal.u8[simdLength - 1 - index] = cnsVal;
                        }
                        break;
                    }

                    case TYP_SHORT:
                    case TYP_USHORT:
                    {
                        uint16_t cnsVal = 0;

                        for (uint32_t index = 0; index < sig->numArgs; index++)
                        {
                            cnsVal = static_cast<uint16_t>(impPopStack().val->AsIntConCommon()->IntegralValue());
                            vecCon->gtSimdVal.u16[simdLength - 1 - index] = cnsVal;
                        }
                        break;
                    }

                    case TYP_INT:
                    case TYP_UINT:
                    {
                        uint32_t cnsVal = 0;

                        for (uint32_t index = 0; index < sig->numArgs; index++)
                        {
                            cnsVal = static_cast<uint32_t>(impPopStack().val->AsIntConCommon()->IntegralValue());
                            vecCon->gtSimdVal.u32[simdLength - 1 - index] = cnsVal;
                        }
                        break;
                    }

                    case TYP_LONG:
                    case TYP_ULONG:
                    {
                        uint64_t cnsVal = 0;

                        for (uint32_t index = 0; index < sig->numArgs; index++)
                        {
                            cnsVal = static_cast<uint64_t>(impPopStack().val->AsIntConCommon()->IntegralValue());
                            vecCon->gtSimdVal.u64[simdLength - 1 - index] = cnsVal;
                        }
                        break;
                    }

                    case TYP_FLOAT:
                    {
                        float cnsVal = 0;

                        for (uint32_t index = 0; index < sig->numArgs; index++)
                        {
                            cnsVal = static_cast<float>(impPopStack().val->AsDblCon()->DconValue());
                            vecCon->gtSimdVal.f32[simdLength - 1 - index] = cnsVal;
                        }
                        break;
                    }

                    case TYP_DOUBLE:
                    {
                        double cnsVal = 0;

                        for (uint32_t index = 0; index < sig->numArgs; index++)
                        {
                            cnsVal = static_cast<double>(impPopStack().val->AsDblCon()->DconValue());
                            vecCon->gtSimdVal.f64[simdLength - 1 - index] = cnsVal;
                        }
                        break;
                    }

                    default:
                    {
                        unreached();
                    }
                }

                retNode = vecCon;
                break;
            }

            IntrinsicNodeBuilder nodeBuilder(getAllocator(CMK_ASTNode), sig->numArgs);

            // TODO-CQ: We don't handle contiguous args for anything except TYP_FLOAT today

            GenTree* prevArg           = nullptr;
            bool     areArgsContiguous = (simdBaseType == TYP_FLOAT);

            for (int i = sig->numArgs - 1; i >= 0; i--)
            {
                GenTree* arg = impPopStack().val;

                if (areArgsContiguous)
                {
                    if (prevArg != nullptr)
                    {
                        // Recall that we are popping the args off the stack in reverse order.
                        areArgsContiguous = areArgumentsContiguous(arg, prevArg);
                    }

                    prevArg = arg;
                }

                nodeBuilder.AddOperand(i, arg);
            }

            if (areArgsContiguous)
            {
                op1                 = nodeBuilder.GetOperand(0);
                GenTree* op1Address = CreateAddressNodeForSimdHWIntrinsicCreate(op1, simdBaseType, simdSize);
                retNode             = gtNewIndir(retType, op1Address);
            }
            else
            {
                retNode =
                    gtNewSimdHWIntrinsicNode(retType, std::move(nodeBuilder), intrinsic, simdBaseJitType, simdSize);
            }
            break;
        }

        case NI_Vector64_CreateScalar:
        case NI_Vector128_CreateScalar:
        {
            assert(sig->numArgs == 1);

            op1     = impPopStack().val;
            retNode = gtNewSimdCreateScalarNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_CreateSequence:
        case NI_Vector128_CreateSequence:
        {
            assert(sig->numArgs == 2);

            if (varTypeIsLong(simdBaseType) && !impStackTop(0).val->OperIsConst())
            {
                // TODO-ARM64-CQ: We should support long/ulong multiplication.
                break;
            }

            impSpillSideEffect(true, stackState.esStackDepth -
                                         2 DEBUGARG("Spilling op1 side effects for vector CreateSequence"));

            op2 = impPopStack().val;
            op1 = impPopStack().val;

            retNode = gtNewSimdCreateSequenceNode(retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_CreateScalarUnsafe:
        case NI_Vector128_CreateScalarUnsafe:
        {
            assert(sig->numArgs == 1);

            op1     = impPopStack().val;
            retNode = gtNewSimdCreateScalarUnsafeNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_op_Division:
        case NI_Vector128_op_Division:
        {
            assert(sig->numArgs == 2);

            if (!varTypeIsFloating(simdBaseType))
            {
                // We can't trivially handle division for integral types using SIMD
                break;
            }

            CORINFO_ARG_LIST_HANDLE arg1     = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2     = info.compCompHnd->getArgNext(arg1);
            var_types               argType  = TYP_UNKNOWN;
            CORINFO_CLASS_HANDLE    argClass = NO_CLASS_HANDLE;

            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
            op2     = getArgForHWIntrinsic(argType, argClass);

            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
            op1     = getArgForHWIntrinsic(argType, argClass);

            retNode = gtNewSimdBinOpNode(GT_DIV, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_Dot:
        case NI_Vector128_Dot:
        {
            assert(sig->numArgs == 2);

            if (!varTypeIsLong(simdBaseType))
            {
                var_types simdType = getSIMDTypeForSize(simdSize);

                op2 = impSIMDPopStack();
                op1 = impSIMDPopStack();

                retNode = gtNewSimdDotProdNode(simdType, op1, op2, simdBaseJitType, simdSize);
                retNode = gtNewSimdGetElementNode(retType, retNode, gtNewIconNode(0), simdBaseJitType, simdSize);
            }
            break;
        }

        case NI_Vector64_Equals:
        case NI_Vector128_Equals:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCmpOpNode(GT_EQ, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_op_Equality:
        case NI_Vector128_op_Equality:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCmpOpAllNode(GT_EQ, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_EqualsAny:
        case NI_Vector128_EqualsAny:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCmpOpAnyNode(GT_EQ, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_ExtractMostSignificantBits:
        case NI_Vector128_ExtractMostSignificantBits:
        {
            assert(sig->numArgs == 1);
            op1     = impSIMDPopStack();
            retNode = gtNewSimdHWIntrinsicNode(retType, op1, intrinsic, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_Floor:
        case NI_Vector128_Floor:
        {
            assert(sig->numArgs == 1);

            if (!varTypeIsFloating(simdBaseType))
            {
                retNode = impSIMDPopStack();
                break;
            }

            op1     = impSIMDPopStack();
            retNode = gtNewSimdFloorNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_FusedMultiplyAdd:
        case NI_Vector128_FusedMultiplyAdd:
        {
            assert(sig->numArgs == 3);
            assert(varTypeIsFloating(simdBaseType));

            impSpillSideEffect(true,
                               stackState.esStackDepth - 3 DEBUGARG("Spilling op1 side effects for FusedMultiplyAdd"));

            impSpillSideEffect(true,
                               stackState.esStackDepth - 2 DEBUGARG("Spilling op2 side effects for FusedMultiplyAdd"));

            op3 = impSIMDPopStack();
            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdFmaNode(retType, op1, op2, op3, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_get_AllBitsSet:
        case NI_Vector128_get_AllBitsSet:
        {
            assert(sig->numArgs == 0);
            retNode = gtNewAllBitsSetConNode(retType);
            break;
        }

        case NI_Vector64_get_Indices:
        case NI_Vector128_get_Indices:
        {
            assert(sig->numArgs == 0);
            retNode = gtNewSimdGetIndicesNode(retType, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_get_One:
        case NI_Vector128_get_One:
        {
            assert(sig->numArgs == 0);
            retNode = gtNewOneConNode(retType, simdBaseType);
            break;
        }

        case NI_Vector64_get_Zero:
        case NI_Vector128_get_Zero:
        {
            assert(sig->numArgs == 0);
            retNode = gtNewZeroConNode(retType);
            break;
        }

        case NI_Vector64_GetElement:
        case NI_Vector128_GetElement:
        {
            assert(!sig->hasThis());
            assert(numArgs == 2);

            op2 = impPopStack().val;
            op1 = impSIMDPopStack();

            retNode = gtNewSimdGetElementNode(retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector128_GetLower:
        {
            assert(sig->numArgs == 1);

            op1     = impSIMDPopStack();
            retNode = gtNewSimdGetLowerNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector128_GetUpper:
        {
            assert(sig->numArgs == 1);

            op1     = impSIMDPopStack();
            retNode = gtNewSimdGetUpperNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_GreaterThan:
        case NI_Vector128_GreaterThan:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCmpOpNode(GT_GT, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_GreaterThanAll:
        case NI_Vector128_GreaterThanAll:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCmpOpAllNode(GT_GT, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_GreaterThanAny:
        case NI_Vector128_GreaterThanAny:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCmpOpAnyNode(GT_GT, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_GreaterThanOrEqual:
        case NI_Vector128_GreaterThanOrEqual:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCmpOpNode(GT_GE, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_GreaterThanOrEqualAll:
        case NI_Vector128_GreaterThanOrEqualAll:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCmpOpAllNode(GT_GE, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_GreaterThanOrEqualAny:
        case NI_Vector128_GreaterThanOrEqualAny:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCmpOpAnyNode(GT_GE, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_IsEvenInteger:
        case NI_Vector128_IsEvenInteger:
        {
            assert(sig->numArgs == 1);

            if (varTypeIsFloating(simdBaseType))
            {
                // The code for handling floating-point is decently complex but also expected
                // to be rare, so we fallback to the managed implementation, which is accelerated
                break;
            }

            op1     = impSIMDPopStack();
            retNode = gtNewSimdIsEvenIntegerNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_IsFinite:
        case NI_Vector128_IsFinite:
        {
            assert(sig->numArgs == 1);
            op1     = impSIMDPopStack();
            retNode = gtNewSimdIsFiniteNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_IsInfinity:
        case NI_Vector128_IsInfinity:
        {
            assert(sig->numArgs == 1);
            op1     = impSIMDPopStack();
            retNode = gtNewSimdIsInfinityNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_IsInteger:
        case NI_Vector128_IsInteger:
        {
            assert(sig->numArgs == 1);
            op1     = impSIMDPopStack();
            retNode = gtNewSimdIsIntegerNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_IsNaN:
        case NI_Vector128_IsNaN:
        {
            assert(sig->numArgs == 1);
            op1     = impSIMDPopStack();
            retNode = gtNewSimdIsNaNNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_IsNegative:
        case NI_Vector128_IsNegative:
        {
            assert(sig->numArgs == 1);
            op1     = impSIMDPopStack();
            retNode = gtNewSimdIsNegativeNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_IsNegativeInfinity:
        case NI_Vector128_IsNegativeInfinity:
        {
            assert(sig->numArgs == 1);
            op1     = impSIMDPopStack();
            retNode = gtNewSimdIsNegativeInfinityNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_IsNormal:
        case NI_Vector128_IsNormal:
        {
            assert(sig->numArgs == 1);
            op1     = impSIMDPopStack();
            retNode = gtNewSimdIsNormalNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_IsOddInteger:
        case NI_Vector128_IsOddInteger:
        {
            assert(sig->numArgs == 1);

            if (varTypeIsFloating(simdBaseType))
            {
                // The code for handling floating-point is decently complex but also expected
                // to be rare, so we fallback to the managed implementation, which is accelerated
                break;
            }

            op1     = impSIMDPopStack();
            retNode = gtNewSimdIsOddIntegerNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_IsPositive:
        case NI_Vector128_IsPositive:
        {
            assert(sig->numArgs == 1);
            op1     = impSIMDPopStack();
            retNode = gtNewSimdIsPositiveNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_IsPositiveInfinity:
        case NI_Vector128_IsPositiveInfinity:
        {
            assert(sig->numArgs == 1);
            op1     = impSIMDPopStack();
            retNode = gtNewSimdIsPositiveInfinityNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_IsSubnormal:
        case NI_Vector128_IsSubnormal:
        {
            assert(sig->numArgs == 1);
            op1     = impSIMDPopStack();
            retNode = gtNewSimdIsSubnormalNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_IsZero:
        case NI_Vector128_IsZero:
        {
            assert(sig->numArgs == 1);
            op1     = impSIMDPopStack();
            retNode = gtNewSimdIsZeroNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_LessThan:
        case NI_Vector128_LessThan:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCmpOpNode(GT_LT, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_LessThanAll:
        case NI_Vector128_LessThanAll:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCmpOpAllNode(GT_LT, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_LessThanAny:
        case NI_Vector128_LessThanAny:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCmpOpAnyNode(GT_LT, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_LessThanOrEqual:
        case NI_Vector128_LessThanOrEqual:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCmpOpNode(GT_LE, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_LessThanOrEqualAll:
        case NI_Vector128_LessThanOrEqualAll:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCmpOpAllNode(GT_LE, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_LessThanOrEqualAny:
        case NI_Vector128_LessThanOrEqualAny:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCmpOpAnyNode(GT_LE, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_AdvSimd_LoadVector64:
        case NI_AdvSimd_LoadVector128:
        case NI_Vector64_LoadUnsafe:
        case NI_Vector128_LoadUnsafe:
        {
            if (sig->numArgs == 2)
            {
                op2 = impPopStack().val;
            }
            else
            {
                assert(sig->numArgs == 1);
            }

            op1 = impPopStack().val;

            if (op1->OperIs(GT_CAST) && op1->gtGetOp1()->TypeIs(TYP_BYREF))
            {
                // If what we have is a BYREF, that's what we really want, so throw away the cast.
                op1 = op1->gtGetOp1();
            }

            if (sig->numArgs == 2)
            {
                op3 = gtNewIconNode(genTypeSize(simdBaseType), op2->TypeGet());
                op2 = gtNewOperNode(GT_MUL, op2->TypeGet(), op2, op3);
                op1 = gtNewOperNode(GT_ADD, op1->TypeGet(), op1, op2);
            }

            retNode = gtNewSimdLoadNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_LoadAligned:
        case NI_Vector128_LoadAligned:
        {
            assert(sig->numArgs == 1);

            if (opts.OptimizationDisabled())
            {
                // ARM64 doesn't have aligned loads, but aligned loads are only validated to be
                // aligned when optimizations are disable, so only skip the intrinsic handling
                // if optimizations are enabled
                break;
            }

            op1 = impPopStack().val;

            if (op1->OperIs(GT_CAST) && op1->gtGetOp1()->TypeIs(TYP_BYREF))
            {
                // If what we have is a BYREF, that's what we really want, so throw away the cast.
                op1 = op1->gtGetOp1();
            }

            retNode = gtNewSimdLoadAlignedNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_LoadAlignedNonTemporal:
        case NI_Vector128_LoadAlignedNonTemporal:
        {
            assert(sig->numArgs == 1);

            if (opts.OptimizationDisabled())
            {
                // ARM64 doesn't have aligned loads, but aligned loads are only validated to be
                // aligned when optimizations are disable, so only skip the intrinsic handling
                // if optimizations are enabled
                break;
            }

            op1 = impPopStack().val;

            if (op1->OperIs(GT_CAST) && op1->gtGetOp1()->TypeIs(TYP_BYREF))
            {
                // If what we have is a BYREF, that's what we really want, so throw away the cast.
                op1 = op1->gtGetOp1();
            }

            retNode = gtNewSimdLoadNonTemporalNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_Max:
        case NI_Vector128_Max:
        {
            isMinMaxIntrinsic = true;
            isMax             = true;
            break;
        }

        case NI_Vector64_MaxMagnitude:
        case NI_Vector128_MaxMagnitude:
        {
            isMinMaxIntrinsic = true;
            isMax             = true;
            isMagnitude       = true;
            break;
        }

        case NI_Vector64_MaxMagnitudeNumber:
        case NI_Vector128_MaxMagnitudeNumber:
        {
            isMinMaxIntrinsic = true;
            isMax             = true;
            isMagnitude       = true;
            isNumber          = true;
            break;
        }

        case NI_Vector64_MaxNative:
        case NI_Vector128_MaxNative:
        {
            isMinMaxIntrinsic = true;
            isMax             = true;
            isNative          = true;
            break;
        }

        case NI_Vector64_MaxNumber:
        case NI_Vector128_MaxNumber:
        {
            isMinMaxIntrinsic = true;
            isMax             = true;
            isNumber          = true;
            break;
        }

        case NI_Vector64_Min:
        case NI_Vector128_Min:
        {
            isMinMaxIntrinsic = true;
            break;
        }

        case NI_Vector64_MinMagnitude:
        case NI_Vector128_MinMagnitude:
        {
            isMinMaxIntrinsic = true;
            isMagnitude       = true;
            break;
        }

        case NI_Vector64_MinMagnitudeNumber:
        case NI_Vector128_MinMagnitudeNumber:
        {
            isMinMaxIntrinsic = true;
            isMagnitude       = true;
            isNumber          = true;
            break;
        }

        case NI_Vector64_MinNative:
        case NI_Vector128_MinNative:
        {
            isMinMaxIntrinsic = true;
            isNative          = true;
            break;
        }

        case NI_Vector64_MinNumber:
        case NI_Vector128_MinNumber:
        {
            isMinMaxIntrinsic = true;
            isNumber          = true;
            break;
        }

        case NI_Vector64_op_Multiply:
        case NI_Vector128_op_Multiply:
        {
            assert(sig->numArgs == 2);

            CORINFO_ARG_LIST_HANDLE arg1     = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2     = info.compCompHnd->getArgNext(arg1);
            var_types               argType  = TYP_UNKNOWN;
            CORINFO_CLASS_HANDLE    argClass = NO_CLASS_HANDLE;

            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
            op2     = getArgForHWIntrinsic(argType, argClass);

            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
            op1     = getArgForHWIntrinsic(argType, argClass);

            retNode = gtNewSimdBinOpNode(GT_MUL, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_MultiplyAddEstimate:
        case NI_Vector128_MultiplyAddEstimate:
        {
            assert(sig->numArgs == 3);

            if (BlockNonDeterministicIntrinsics(mustExpand))
            {
                break;
            }

            if (varTypeIsFloating(simdBaseType))
            {
                impSpillSideEffect(true, stackState.esStackDepth -
                                             3 DEBUGARG("Spilling op1 side effects for MultiplyAddEstimate"));

                impSpillSideEffect(true, stackState.esStackDepth -
                                             2 DEBUGARG("Spilling op2 side effects for MultiplyAddEstimate"));
            }

            op3 = impSIMDPopStack();
            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            if (varTypeIsFloating(simdBaseType))
            {
                retNode = gtNewSimdFmaNode(retType, op1, op2, op3, simdBaseJitType, simdSize);
            }
            else
            {
                GenTree* mulNode = gtNewSimdBinOpNode(GT_MUL, retType, op1, op2, simdBaseJitType, simdSize);
                retNode          = gtNewSimdBinOpNode(GT_ADD, retType, mulNode, op3, simdBaseJitType, simdSize);
            }
            break;
        }

        case NI_Vector64_Narrow:
        case NI_Vector128_Narrow:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdNarrowNode(retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_NarrowWithSaturation:
        case NI_Vector128_NarrowWithSaturation:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            if (varTypeIsFloating(simdBaseType))
            {
                retNode = gtNewSimdNarrowNode(retType, op1, op2, simdBaseJitType, simdSize);
            }
            else if (simdSize == 16)
            {
                intrinsic = NI_AdvSimd_ExtractNarrowingSaturateLower;
                op1       = gtNewSimdHWIntrinsicNode(TYP_SIMD8, op1, intrinsic, simdBaseJitType, 8);

                intrinsic = NI_AdvSimd_ExtractNarrowingSaturateUpper;
                retNode   = gtNewSimdHWIntrinsicNode(retType, op1, op2, intrinsic, simdBaseJitType, simdSize);
            }
            else
            {
                intrinsic = NI_Vector64_ToVector128Unsafe;
                op1       = gtNewSimdHWIntrinsicNode(TYP_SIMD16, op1, intrinsic, simdBaseJitType, simdSize);

                op1 = gtNewSimdWithUpperNode(TYP_SIMD16, op1, op2, simdBaseJitType, 16);

                intrinsic = NI_AdvSimd_ExtractNarrowingSaturateLower;
                retNode   = gtNewSimdHWIntrinsicNode(retType, op1, intrinsic, simdBaseJitType, simdSize);
            }
            break;
        }

        case NI_Vector64_op_UnaryNegation:
        case NI_Vector128_op_UnaryNegation:
        {
            assert(sig->numArgs == 1);
            op1     = impSIMDPopStack();
            retNode = gtNewSimdUnOpNode(GT_NEG, retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_op_OnesComplement:
        case NI_Vector128_op_OnesComplement:
        {
            assert(sig->numArgs == 1);
            op1     = impSIMDPopStack();
            retNode = gtNewSimdUnOpNode(GT_NOT, retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_op_Inequality:
        case NI_Vector128_op_Inequality:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdCmpOpAnyNode(GT_NE, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_op_UnaryPlus:
        case NI_Vector128_op_UnaryPlus:
        {
            assert(sig->numArgs == 1);
            retNode = impSIMDPopStack();
            break;
        }

        case NI_Vector64_op_Subtraction:
        case NI_Vector128_op_Subtraction:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdBinOpNode(GT_SUB, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_op_LeftShift:
        case NI_Vector128_op_LeftShift:
        {
            assert(sig->numArgs == 2);

            op2 = impPopStack().val;
            op1 = impSIMDPopStack();

            retNode = gtNewSimdBinOpNode(GT_LSH, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_op_RightShift:
        case NI_Vector128_op_RightShift:
        {
            assert(sig->numArgs == 2);
            genTreeOps op = varTypeIsUnsigned(simdBaseType) ? GT_RSZ : GT_RSH;

            op2 = impPopStack().val;
            op1 = impSIMDPopStack();

            retNode = gtNewSimdBinOpNode(op, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_op_UnsignedRightShift:
        case NI_Vector128_op_UnsignedRightShift:
        {
            assert(sig->numArgs == 2);

            op2 = impPopStack().val;
            op1 = impSIMDPopStack();

            retNode = gtNewSimdBinOpNode(GT_RSZ, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_Round:
        case NI_Vector128_Round:
        {
            if (sig->numArgs != 1)
            {
                break;
            }

            if (!varTypeIsFloating(simdBaseType))
            {
                retNode = impSIMDPopStack();
                break;
            }

            op1     = impSIMDPopStack();
            retNode = gtNewSimdRoundNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_ShiftLeft:
        case NI_Vector128_ShiftLeft:
        {
            assert(sig->numArgs == 2);

            if (!varTypeIsSIMD(impStackTop(0).val))
            {
                // We just want the inlining profitability boost for the helper intrinsics/
                // that have operator alternatives like `simd << int`
                break;
            }

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            if (simdSize == 8)
            {
                intrinsic = varTypeIsLong(simdBaseType) ? NI_AdvSimd_ShiftLogicalScalar : NI_AdvSimd_ShiftLogical;
            }
            else
            {
                assert(simdSize == 16);
                intrinsic = NI_AdvSimd_ShiftLogical;
            }

            retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, intrinsic, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_Shuffle:
        case NI_Vector128_Shuffle:
        case NI_Vector64_ShuffleNative:
        case NI_Vector128_ShuffleNative:
        case NI_Vector64_ShuffleNativeFallback:
        case NI_Vector128_ShuffleNativeFallback:
        {
            assert((sig->numArgs == 2) || (sig->numArgs == 3));
            assert((simdSize == 8) || (simdSize == 16));

            // The Native variants are non-deterministic on arm64 (for element size > 1)
            bool isShuffleNative = (intrinsic != NI_Vector64_Shuffle) && (intrinsic != NI_Vector128_Shuffle);
            if (isShuffleNative && (genTypeSize(simdBaseType) > 1) && BlockNonDeterministicIntrinsics(mustExpand))
            {
                break;
            }

            GenTree* indices = impStackTop(0).val;

            // Check if the required intrinsics to emit are available.
            bool canBecomeValidForShuffle = false;
            if (!IsValidForShuffle(indices, simdSize, simdBaseType, &canBecomeValidForShuffle, isShuffleNative))
            {
                // All cases on arm64 are either valid or invalid, they cannot become valid later
                assert(!canBecomeValidForShuffle);
                break;
            }

            // If the indices might become constant later, then we don't emit for now, delay until later.
            if (!indices->IsCnsVec())
            {
                assert(sig->numArgs == 2);

                if (opts.OptimizationEnabled())
                {
                    // Only enable late stage rewriting if optimizations are enabled
                    // as we won't otherwise encounter a constant at the later point
                    op2 = impSIMDPopStack();
                    op1 = impSIMDPopStack();

                    retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, intrinsic, simdBaseJitType, simdSize);

                    retNode->AsHWIntrinsic()->SetMethodHandle(this, method R2RARG(*entryPoint));
                    break;
                }
            }

            if (sig->numArgs == 2)
            {
                op2     = impSIMDPopStack();
                op1     = impSIMDPopStack();
                retNode = gtNewSimdShuffleNode(retType, op1, op2, simdBaseJitType, simdSize, isShuffleNative);
            }
            break;
        }

        case NI_Vector64_Sqrt:
        case NI_Vector128_Sqrt:
        {
            assert(sig->numArgs == 1);

            if (varTypeIsFloating(simdBaseType))
            {
                op1     = impSIMDPopStack();
                retNode = gtNewSimdSqrtNode(retType, op1, simdBaseJitType, simdSize);
            }
            break;
        }

        case NI_AdvSimd_Store:
        case NI_AdvSimd_Arm64_Store:
        {
            CORINFO_ARG_LIST_HANDLE arg1     = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2     = info.compCompHnd->getArgNext(arg1);
            var_types               argType  = TYP_UNKNOWN;
            CORINFO_CLASS_HANDLE    argClass = NO_CLASS_HANDLE;
            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
            op2     = impPopStack().val;

            if (op2->TypeIs(TYP_STRUCT))
            {
                info.compNeedsConsecutiveRegisters = true;
                unsigned fieldCount                = info.compCompHnd->getClassNumInstanceFields(argClass);

                if (!op2->OperIs(GT_LCL_VAR))
                {
                    unsigned tmp = lvaGrabTemp(true DEBUGARG("StoreVectorN"));

                    impStoreToTemp(tmp, op2, CHECK_SPILL_NONE);
                    op2 = gtNewLclvNode(tmp, argType);
                }
                op2     = gtConvertTableOpToFieldList(op2, fieldCount);
                argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
                op1     = getArgForHWIntrinsic(argType, argClass);

                if (op1->OperIs(GT_CAST))
                {
                    // Although the API specifies a pointer, if what we have is a BYREF, that's what
                    // we really want, so throw away the cast.
                    if (op1->gtGetOp1()->TypeIs(TYP_BYREF))
                    {
                        op1 = op1->gtGetOp1();
                    }
                }

                retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, intrinsic, simdBaseJitType, simdSize);
            }
            else
            {
                if (op2->TypeIs(TYP_SIMD16))
                {
                    // Update the simdSize explicitly as Vector128 variant of Store() is present in AdvSimd instead of
                    // AdvSimd.Arm64.
                    simdSize = 16;
                }

                op1 = impPopStack().val;

                if (op1->OperIs(GT_CAST) && op1->gtGetOp1()->TypeIs(TYP_BYREF))
                {
                    // If what we have is a BYREF, that's what we really want, so throw away the cast.
                    op1 = op1->gtGetOp1();
                }

                retNode = gtNewSimdStoreNode(op1, op2, simdBaseJitType, simdSize);
            }
            break;
        }

        case NI_Vector64_StoreUnsafe:
        case NI_Vector128_StoreUnsafe:
        {
            assert(retType == TYP_VOID);

            if (sig->numArgs == 3)
            {
                impSpillSideEffect(true,
                                   stackState.esStackDepth - 3 DEBUGARG("Spilling op1 side effects for HWIntrinsic"));

                op3 = impPopStack().val;
            }
            else
            {
                assert(sig->numArgs == 2);

                impSpillSideEffect(true,
                                   stackState.esStackDepth - 2 DEBUGARG("Spilling op1 side effects for HWIntrinsic"));
            }

            op2 = impPopStack().val;

            if (op2->OperIs(GT_CAST) && op2->gtGetOp1()->TypeIs(TYP_BYREF))
            {
                // If what we have is a BYREF, that's what we really want, so throw away the cast.
                op2 = op2->gtGetOp1();
            }

            if (sig->numArgs == 3)
            {
                op4 = gtNewIconNode(genTypeSize(simdBaseType), op3->TypeGet());
                op3 = gtNewOperNode(GT_MUL, op3->TypeGet(), op3, op4);
                op2 = gtNewOperNode(GT_ADD, op2->TypeGet(), op2, op3);
            }

            op1 = impSIMDPopStack();

            retNode = gtNewSimdStoreNode(op2, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_StoreAligned:
        case NI_Vector128_StoreAligned:
        {
            assert(sig->numArgs == 2);
            assert(retType == TYP_VOID);

            if (opts.OptimizationDisabled())
            {
                // ARM64 doesn't have aligned stores, but aligned stores are only validated to be
                // aligned when optimizations are disable, so only skip the intrinsic handling
                // if optimizations are enabled
                break;
            }

            impSpillSideEffect(true, stackState.esStackDepth - 2 DEBUGARG("Spilling op1 side effects for HWIntrinsic"));

            op2 = impPopStack().val;

            if (op2->OperIs(GT_CAST) && op2->gtGetOp1()->TypeIs(TYP_BYREF))
            {
                // If what we have is a BYREF, that's what we really want, so throw away the cast.
                op2 = op2->gtGetOp1();
            }

            op1 = impSIMDPopStack();

            retNode = gtNewSimdStoreAlignedNode(op2, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_StoreAlignedNonTemporal:
        case NI_Vector128_StoreAlignedNonTemporal:
        {
            assert(sig->numArgs == 2);
            assert(retType == TYP_VOID);

            if (opts.OptimizationDisabled())
            {
                // ARM64 doesn't have aligned stores, but aligned stores are only validated to be
                // aligned when optimizations are disable, so only skip the intrinsic handling
                // if optimizations are enabled
                break;
            }

            impSpillSideEffect(true, stackState.esStackDepth - 2 DEBUGARG("Spilling op1 side effects for HWIntrinsic"));

            op2 = impPopStack().val;

            if (op2->OperIs(GT_CAST) && op2->gtGetOp1()->TypeIs(TYP_BYREF))
            {
                // If what we have is a BYREF, that's what we really want, so throw away the cast.
                op2 = op2->gtGetOp1();
            }

            op1 = impSIMDPopStack();

            retNode = gtNewSimdStoreNonTemporalNode(op2, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_AdvSimd_StoreVectorAndZip:
        case NI_AdvSimd_Arm64_StoreVectorAndZip:
        {
            assert(sig->numArgs == 2);
            assert(retType == TYP_VOID);

            CORINFO_ARG_LIST_HANDLE arg1     = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2     = info.compCompHnd->getArgNext(arg1);
            var_types               argType  = TYP_UNKNOWN;
            CORINFO_CLASS_HANDLE    argClass = NO_CLASS_HANDLE;

            argType             = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
            op2                 = impPopStack().val;
            unsigned fieldCount = info.compCompHnd->getClassNumInstanceFields(argClass);
            argType             = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
            op1                 = getArgForHWIntrinsic(argType, argClass);

            assert(op2->TypeIs(TYP_STRUCT));
            if (op1->OperIs(GT_CAST))
            {
                // Although the API specifies a pointer, if what we have is a BYREF, that's what
                // we really want, so throw away the cast.
                if (op1->gtGetOp1()->TypeIs(TYP_BYREF))
                {
                    op1 = op1->gtGetOp1();
                }
            }

            if (!op2->OperIs(GT_LCL_VAR))
            {
                unsigned tmp = lvaGrabTemp(true DEBUGARG("StoreVectorNx2 temp tree"));

                impStoreToTemp(tmp, op2, CHECK_SPILL_NONE);
                op2 = gtNewLclvNode(tmp, argType);
            }
            op2 = gtConvertTableOpToFieldList(op2, fieldCount);

            intrinsic = simdSize == 8 ? NI_AdvSimd_StoreVectorAndZip : NI_AdvSimd_Arm64_StoreVectorAndZip;

            info.compNeedsConsecutiveRegisters = true;
            retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, intrinsic, simdBaseJitType, simdSize);
            break;
        }

        case NI_AdvSimd_StoreSelectedScalar:
        case NI_AdvSimd_Arm64_StoreSelectedScalar:
        {
            assert(sig->numArgs == 3);
            assert(retType == TYP_VOID);

            if (!mustExpand && !impStackTop(0).val->IsCnsIntOrI() && impStackTop(1).val->TypeIs(TYP_STRUCT))
            {
                // TODO-ARM64-CQ: Support rewriting nodes that involves
                // GenTreeFieldList as user calls during rationalization
                return nullptr;
            }

            CORINFO_ARG_LIST_HANDLE arg1     = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2     = info.compCompHnd->getArgNext(arg1);
            CORINFO_ARG_LIST_HANDLE arg3     = info.compCompHnd->getArgNext(arg2);
            var_types               argType  = TYP_UNKNOWN;
            CORINFO_CLASS_HANDLE    argClass = NO_CLASS_HANDLE;
            argType                = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg3, &argClass)));
            op3                    = impPopStack().val;
            argType                = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
            op2                    = impPopStack().val;
            unsigned fieldCount    = info.compCompHnd->getClassNumInstanceFields(argClass);
            int      immLowerBound = 0;
            int      immUpperBound = 0;

            if (op2->TypeIs(TYP_STRUCT))
            {
                info.compNeedsConsecutiveRegisters = true;
                intrinsic = simdSize == 8 ? NI_AdvSimd_StoreSelectedScalar : NI_AdvSimd_Arm64_StoreSelectedScalar;

                if (!op2->OperIs(GT_LCL_VAR))
                {
                    unsigned tmp = lvaGrabTemp(true DEBUGARG("StoreSelectedScalarN"));

                    impStoreToTemp(tmp, op2, CHECK_SPILL_NONE);
                    op2 = gtNewLclvNode(tmp, argType);
                }
                op2 = gtConvertTableOpToFieldList(op2, fieldCount);
            }
            else
            {
                // While storing from a single vector, both Vector128 and Vector64 API calls are in AdvSimd class.
                // Thus, we get simdSize as 8 for both of the calls. We re-calculate that simd size for such API calls.
                getBaseJitTypeAndSizeOfSIMDType(argClass, &simdSize);
            }

            assert(HWIntrinsicInfo::isImmOp(intrinsic, op3));
            HWIntrinsicInfo::lookupImmBounds(intrinsic, simdSize, simdBaseType, 1, &immLowerBound, &immUpperBound);
            op3     = addRangeCheckIfNeeded(intrinsic, op3, immLowerBound, immUpperBound);
            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
            op1     = getArgForHWIntrinsic(argType, argClass);

            if (op1->OperIs(GT_CAST))
            {
                // Although the API specifies a pointer, if what we have is a BYREF, that's what
                // we really want, so throw away the cast.
                if (op1->gtGetOp1()->TypeIs(TYP_BYREF))
                {
                    op1 = op1->gtGetOp1();
                }
            }

            retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, op3, intrinsic, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_SubtractSaturate:
        case NI_Vector128_SubtractSaturate:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            if (varTypeIsFloating(simdBaseType))
            {
                retNode = gtNewSimdBinOpNode(GT_SUB, retType, op1, op2, simdBaseJitType, simdSize);
            }
            else
            {
                intrinsic = NI_AdvSimd_SubtractSaturate;

                if ((simdSize == 8) && varTypeIsLong(simdBaseType))
                {
                    intrinsic = NI_AdvSimd_SubtractSaturateScalar;
                }

                retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, intrinsic, simdBaseJitType, simdSize);
            }
            break;
        }

        case NI_Vector64_Sum:
        case NI_Vector128_Sum:
        {
            assert(sig->numArgs == 1);
            op1     = impSIMDPopStack();
            retNode = gtNewSimdSumNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_Truncate:
        case NI_Vector128_Truncate:
        {
            assert(sig->numArgs == 1);

            if (!varTypeIsFloating(simdBaseType))
            {
                retNode = impSIMDPopStack();
                break;
            }

            op1     = impSIMDPopStack();
            retNode = gtNewSimdTruncNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_WidenLower:
        case NI_Vector128_WidenLower:
        {
            assert(sig->numArgs == 1);

            op1 = impSIMDPopStack();

            retNode = gtNewSimdWidenLowerNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_WidenUpper:
        case NI_Vector128_WidenUpper:
        {
            assert(sig->numArgs == 1);

            op1 = impSIMDPopStack();

            retNode = gtNewSimdWidenUpperNode(retType, op1, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_WithElement:
        case NI_Vector128_WithElement:
        {
            assert(numArgs == 3);
            GenTree* indexOp = impStackTop(1).val;

            if (!indexOp->OperIsConst())
            {
                if (!opts.OptimizationEnabled())
                {
                    // Only enable late stage rewriting if optimizations are enabled
                    // as we won't otherwise encounter a constant at the later point
                    return nullptr;
                }

                op3 = impPopStack().val;
                op2 = impPopStack().val;
                op1 = impSIMDPopStack();

                retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, op3, intrinsic, simdBaseJitType, simdSize);

                retNode->AsHWIntrinsic()->SetMethodHandle(this, method R2RARG(*entryPoint));
                break;
            }

            ssize_t imm8  = indexOp->AsIntCon()->IconValue();
            ssize_t count = simdSize / genTypeSize(simdBaseType);

            if ((imm8 >= count) || (imm8 < 0))
            {
                // Using software fallback if index is out of range (throw exception)
                return nullptr;
            }

            GenTree* valueOp = impPopStack().val;
            impPopStack(); // pop the indexOp that we already have.
            GenTree* vectorOp = impSIMDPopStack();

            retNode = gtNewSimdWithElementNode(retType, vectorOp, indexOp, valueOp, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector128_WithLower:
        {
            assert(sig->numArgs == 2);

            op2     = impSIMDPopStack();
            op1     = impSIMDPopStack();
            retNode = gtNewSimdWithLowerNode(retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector128_WithUpper:
        {
            assert(sig->numArgs == 2);

            op2     = impSIMDPopStack();
            op1     = impSIMDPopStack();
            retNode = gtNewSimdWithUpperNode(retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_Vector64_op_ExclusiveOr:
        case NI_Vector128_op_ExclusiveOr:
        {
            assert(sig->numArgs == 2);

            op2 = impSIMDPopStack();
            op1 = impSIMDPopStack();

            retNode = gtNewSimdBinOpNode(GT_XOR, retType, op1, op2, simdBaseJitType, simdSize);
            break;
        }

        case NI_AdvSimd_Load2xVector64AndUnzip:
        case NI_AdvSimd_Load3xVector64AndUnzip:
        case NI_AdvSimd_Load4xVector64AndUnzip:
        case NI_AdvSimd_Arm64_Load2xVector128AndUnzip:
        case NI_AdvSimd_Arm64_Load3xVector128AndUnzip:
        case NI_AdvSimd_Arm64_Load4xVector128AndUnzip:
        case NI_AdvSimd_Load2xVector64:
        case NI_AdvSimd_Load3xVector64:
        case NI_AdvSimd_Load4xVector64:
        case NI_AdvSimd_Arm64_Load2xVector128:
        case NI_AdvSimd_Arm64_Load3xVector128:
        case NI_AdvSimd_Arm64_Load4xVector128:
        case NI_AdvSimd_LoadAndReplicateToVector64x2:
        case NI_AdvSimd_LoadAndReplicateToVector64x3:
        case NI_AdvSimd_LoadAndReplicateToVector64x4:
        case NI_AdvSimd_Arm64_LoadAndReplicateToVector128x2:
        case NI_AdvSimd_Arm64_LoadAndReplicateToVector128x3:
        case NI_AdvSimd_Arm64_LoadAndReplicateToVector128x4:
            info.compNeedsConsecutiveRegisters = true;
            FALLTHROUGH;
        case NI_AdvSimd_Arm64_LoadPairScalarVector64:
        case NI_AdvSimd_Arm64_LoadPairScalarVector64NonTemporal:
        case NI_AdvSimd_Arm64_LoadPairVector128:
        case NI_AdvSimd_Arm64_LoadPairVector128NonTemporal:
        case NI_AdvSimd_Arm64_LoadPairVector64:
        case NI_AdvSimd_Arm64_LoadPairVector64NonTemporal:
        {
            op1 = impPopStack().val;

            if (op1->OperIs(GT_CAST))
            {
                // Although the API specifies a pointer, if what we have is a BYREF, that's what
                // we really want, so throw away the cast.
                if (op1->gtGetOp1()->TypeIs(TYP_BYREF))
                {
                    op1 = op1->gtGetOp1();
                }
            }

            assert(HWIntrinsicInfo::IsMultiReg(intrinsic));

            op1     = gtNewSimdHWIntrinsicNode(retType, op1, intrinsic, simdBaseJitType, simdSize);
            retNode = impStoreMultiRegValueToVar(op1, sig->retTypeSigClass DEBUGARG(CorInfoCallConvExtension::Managed));
            break;
        }

        case NI_Sve_CreateFalseMaskByte:
        case NI_Sve_CreateFalseMaskDouble:
        case NI_Sve_CreateFalseMaskInt16:
        case NI_Sve_CreateFalseMaskInt32:
        case NI_Sve_CreateFalseMaskInt64:
        case NI_Sve_CreateFalseMaskSByte:
        case NI_Sve_CreateFalseMaskSingle:
        case NI_Sve_CreateFalseMaskUInt16:
        case NI_Sve_CreateFalseMaskUInt32:
        case NI_Sve_CreateFalseMaskUInt64:
        {
            // Import as a constant vector 0
            GenTreeVecCon* vecCon = gtNewVconNode(retType);
            vecCon->gtSimdVal     = simd_t::Zero();
            retNode               = vecCon;
            break;
        }

        case NI_Sve_CreateTrueMaskByte:
        case NI_Sve_CreateTrueMaskDouble:
        case NI_Sve_CreateTrueMaskInt16:
        case NI_Sve_CreateTrueMaskInt32:
        case NI_Sve_CreateTrueMaskInt64:
        case NI_Sve_CreateTrueMaskSByte:
        case NI_Sve_CreateTrueMaskSingle:
        case NI_Sve_CreateTrueMaskUInt16:
        case NI_Sve_CreateTrueMaskUInt32:
        case NI_Sve_CreateTrueMaskUInt64:
        {
            assert(sig->numArgs == 1);
            op1 = impPopStack().val;

            // Where possible, import a constant mask to allow for optimisations.
            if (op1->IsIntegralConst())
            {
                int64_t pattern = op1->AsIntConCommon()->IntegralValue();
                simd_t  simdVal;

                if (EvaluateSimdPatternToVector(simdBaseType, &simdVal, (SveMaskPattern)pattern))
                {
                    retNode = gtNewVconNode(retType, &simdVal);
                    break;
                }
            }

            // Was not able to generate a pattern, instead import a truemaskall
            retNode = gtNewSimdHWIntrinsicNode(TYP_MASK, op1, intrinsic, simdBaseJitType, simdSize);
            break;
        }

        case NI_Sve_Load2xVectorAndUnzip:
        case NI_Sve_Load3xVectorAndUnzip:
        case NI_Sve_Load4xVectorAndUnzip:
        {
            info.compNeedsConsecutiveRegisters = true;

            assert(sig->numArgs == 2);

            op2 = impPopStack().val;
            op1 = impPopStack().val;

            if (op2->OperIs(GT_CAST))
            {
                // Although the API specifies a pointer, if what we have is a BYREF, that's what
                // we really want, so throw away the cast.
                if (op2->gtGetOp1()->TypeIs(TYP_BYREF))
                {
                    op2 = op2->gtGetOp1();
                }
            }

            assert(HWIntrinsicInfo::IsMultiReg(intrinsic));
            assert(HWIntrinsicInfo::IsExplicitMaskedOperation(intrinsic));

            retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, intrinsic, simdBaseJitType, simdSize);
            break;
        }

        case NI_AdvSimd_LoadAndInsertScalarVector64x2:
        case NI_AdvSimd_LoadAndInsertScalarVector64x3:
        case NI_AdvSimd_LoadAndInsertScalarVector64x4:
        case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2:
        case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3:
        case NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4:
        {
            assert(sig->numArgs == 3);

            if (!mustExpand && !impStackTop(1).val->IsCnsIntOrI())
            {
                // TODO-ARM64-CQ: Support rewriting nodes that involves
                // GenTreeFieldList as user calls during rationalization
                return nullptr;
            }

            CORINFO_ARG_LIST_HANDLE arg1     = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2     = info.compCompHnd->getArgNext(arg1);
            CORINFO_ARG_LIST_HANDLE arg3     = info.compCompHnd->getArgNext(arg2);
            var_types               argType  = TYP_UNKNOWN;
            CORINFO_CLASS_HANDLE    argClass = NO_CLASS_HANDLE;

            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg3, &argClass)));
            op3     = getArgForHWIntrinsic(argType, argClass);
            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
            op2     = getArgForHWIntrinsic(argType, argClass);
            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
            op1     = impPopStack().val;

            if (op3->OperIs(GT_CAST))
            {
                // Although the API specifies a pointer, if what we have is a BYREF, that's what
                // we really want, so throw away the cast.
                if (op3->gtGetOp1()->TypeIs(TYP_BYREF))
                {
                    op3 = op3->gtGetOp1();
                }
            }

            assert(HWIntrinsicInfo::IsMultiReg(intrinsic));
            assert(op1->TypeIs(TYP_STRUCT));

            info.compNeedsConsecutiveRegisters = true;
            unsigned fieldCount                = info.compCompHnd->getClassNumInstanceFields(argClass);

            if (!op1->OperIs(GT_LCL_VAR))
            {
                unsigned tmp = lvaGrabTemp(true DEBUGARG("LoadAndInsertScalar temp tree"));

                impStoreToTemp(tmp, op1, CHECK_SPILL_NONE);
                op1 = gtNewLclvNode(tmp, argType);
            }

            op1     = gtConvertParamOpToFieldList(op1, fieldCount, argClass);
            op1     = gtNewSimdHWIntrinsicNode(retType, op1, op2, op3, intrinsic, simdBaseJitType, simdSize);
            retNode = impStoreMultiRegValueToVar(op1, sig->retTypeSigClass DEBUGARG(CorInfoCallConvExtension::Managed));
            break;
        }
        case NI_AdvSimd_VectorTableLookup:
        case NI_AdvSimd_Arm64_VectorTableLookup:
        {
            assert(sig->numArgs == 2);

            CORINFO_ARG_LIST_HANDLE arg1     = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2     = info.compCompHnd->getArgNext(arg1);
            var_types               argType  = TYP_UNKNOWN;
            CORINFO_CLASS_HANDLE    argClass = NO_CLASS_HANDLE;

            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
            op2     = getArgForHWIntrinsic(argType, argClass);
            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
            op1     = impPopStack().val;

            if (op1->TypeIs(TYP_STRUCT))
            {
                info.compNeedsConsecutiveRegisters = true;
                unsigned fieldCount                = info.compCompHnd->getClassNumInstanceFields(argClass);

                if (!op1->OperIs(GT_LCL_VAR))
                {
                    unsigned tmp = lvaGrabTemp(true DEBUGARG("VectorTableLookup temp tree"));

                    impStoreToTemp(tmp, op1, CHECK_SPILL_NONE);
                    op1 = gtNewLclvNode(tmp, argType);
                }

                op1 = gtConvertTableOpToFieldList(op1, fieldCount);
            }
            else
            {
                assert(varTypeIsSIMD(op1->TypeGet()));
            }

            retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, intrinsic, simdBaseJitType, simdSize);
            break;
        }
        case NI_AdvSimd_VectorTableLookupExtension:
        case NI_AdvSimd_Arm64_VectorTableLookupExtension:
        {
            assert(sig->numArgs == 3);

            CORINFO_ARG_LIST_HANDLE arg1     = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2     = info.compCompHnd->getArgNext(arg1);
            CORINFO_ARG_LIST_HANDLE arg3     = info.compCompHnd->getArgNext(arg2);
            var_types               argType  = TYP_UNKNOWN;
            CORINFO_CLASS_HANDLE    argClass = NO_CLASS_HANDLE;

            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg3, &argClass)));
            op3     = getArgForHWIntrinsic(argType, argClass);
            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
            op2     = impPopStack().val;
            op1     = impPopStack().val;

            if (op2->TypeIs(TYP_STRUCT))
            {
                info.compNeedsConsecutiveRegisters = true;
                unsigned fieldCount                = info.compCompHnd->getClassNumInstanceFields(argClass);

                if (!op2->OperIs(GT_LCL_VAR))
                {
                    unsigned tmp = lvaGrabTemp(true DEBUGARG("VectorTableLookupExtension temp tree"));

                    impStoreToTemp(tmp, op2, CHECK_SPILL_NONE);
                    op2 = gtNewLclvNode(tmp, argType);
                }

                op2 = gtConvertTableOpToFieldList(op2, fieldCount);
            }
            else
            {
                assert(varTypeIsSIMD(op1->TypeGet()));
            }

            retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, op3, intrinsic, simdBaseJitType, simdSize);
            break;
        }

        case NI_Sve_StoreAndZip:
        {
            assert(sig->numArgs == 3);
            assert(retType == TYP_VOID);

            CORINFO_ARG_LIST_HANDLE arg1     = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2     = info.compCompHnd->getArgNext(arg1);
            CORINFO_ARG_LIST_HANDLE arg3     = info.compCompHnd->getArgNext(arg2);
            var_types               argType  = TYP_UNKNOWN;
            CORINFO_CLASS_HANDLE    argClass = NO_CLASS_HANDLE;

            argType             = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg3, &argClass)));
            op3                 = impPopStack().val;
            unsigned fieldCount = info.compCompHnd->getClassNumInstanceFields(argClass);

            if (op3->TypeIs(TYP_STRUCT))
            {
                info.compNeedsConsecutiveRegisters = true;
                switch (fieldCount)
                {
                    case 2:
                        intrinsic = NI_Sve_StoreAndZipx2;
                        break;

                    case 3:
                        intrinsic = NI_Sve_StoreAndZipx3;
                        break;

                    case 4:
                        intrinsic = NI_Sve_StoreAndZipx4;
                        break;

                    default:
                        assert("unsupported");
                }

                if (!op3->OperIs(GT_LCL_VAR))
                {
                    unsigned tmp = lvaGrabTemp(true DEBUGARG("SveStoreN"));

                    impStoreToTemp(tmp, op3, CHECK_SPILL_NONE);
                    op3 = gtNewLclvNode(tmp, argType);
                }
                op3 = gtConvertTableOpToFieldList(op3, fieldCount);
            }

            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
            op2     = getArgForHWIntrinsic(argType, argClass);
            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
            op1     = getArgForHWIntrinsic(argType, argClass);
            retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, op3, intrinsic, simdBaseJitType, simdSize);
            break;
        }

        case NI_Sve_StoreNarrowing:
        {
            assert(sig->numArgs == 3);
            assert(retType == TYP_VOID);

            CORINFO_ARG_LIST_HANDLE arg   = sig->args;
            arg                           = info.compCompHnd->getArgNext(arg);
            CORINFO_CLASS_HANDLE argClass = info.compCompHnd->getArgClass(sig, arg);
            CorInfoType          ptrType  = getBaseJitTypeAndSizeOfSIMDType(argClass);
            CORINFO_CLASS_HANDLE tmpClass = NO_CLASS_HANDLE;

            // The size of narrowed target elements is determined from the second argument of StoreNarrowing().
            // Thus, we first extract the datatype of a pointer passed in the second argument and then store it as the
            // auxiliary type of intrinsic. This auxiliary type is then used in the codegen to choose the correct
            // instruction to emit.
            ptrType = strip(info.compCompHnd->getArgType(sig, arg, &tmpClass));
            assert(ptrType == CORINFO_TYPE_PTR);
            ptrType = info.compCompHnd->getChildType(argClass, &tmpClass);
            assert(ptrType < simdBaseJitType);

            op3     = impPopStack().val;
            op2     = impPopStack().val;
            op1     = impPopStack().val;
            retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, op3, intrinsic, simdBaseJitType, simdSize);
            retNode->AsHWIntrinsic()->SetAuxiliaryJitType(ptrType);
            break;
        }

        case NI_Sve_SaturatingDecrementBy8BitElementCount:
        case NI_Sve_SaturatingIncrementBy8BitElementCount:
        case NI_Sve_SaturatingDecrementBy16BitElementCountScalar:
        case NI_Sve_SaturatingDecrementBy32BitElementCountScalar:
        case NI_Sve_SaturatingDecrementBy64BitElementCountScalar:
        case NI_Sve_SaturatingIncrementBy16BitElementCountScalar:
        case NI_Sve_SaturatingIncrementBy32BitElementCountScalar:
        case NI_Sve_SaturatingIncrementBy64BitElementCountScalar:
#ifdef DEBUG
            isValidScalarIntrinsic = true;
            FALLTHROUGH;
#endif
        case NI_Sve_SaturatingDecrementBy16BitElementCount:
        case NI_Sve_SaturatingDecrementBy32BitElementCount:
        case NI_Sve_SaturatingDecrementBy64BitElementCount:
        case NI_Sve_SaturatingIncrementBy16BitElementCount:
        case NI_Sve_SaturatingIncrementBy32BitElementCount:
        case NI_Sve_SaturatingIncrementBy64BitElementCount:

        {
            assert(sig->numArgs == 3);

            CORINFO_ARG_LIST_HANDLE arg1          = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2          = info.compCompHnd->getArgNext(arg1);
            CORINFO_ARG_LIST_HANDLE arg3          = info.compCompHnd->getArgNext(arg2);
            var_types               argType       = TYP_UNKNOWN;
            CORINFO_CLASS_HANDLE    argClass      = NO_CLASS_HANDLE;
            int                     immLowerBound = 0;
            int                     immUpperBound = 0;

            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg3, &argClass)));
            op3     = getArgForHWIntrinsic(argType, argClass);
            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
            op2     = getArgForHWIntrinsic(argType, argClass);
            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
            op1     = impPopStack().val;

            assert(HWIntrinsicInfo::isImmOp(intrinsic, op2));
            HWIntrinsicInfo::lookupImmBounds(intrinsic, simdSize, simdBaseType, 1, &immLowerBound, &immUpperBound);
            op2 = addRangeCheckIfNeeded(intrinsic, op2, immLowerBound, immUpperBound);

            assert(HWIntrinsicInfo::isImmOp(intrinsic, op3));
            HWIntrinsicInfo::lookupImmBounds(intrinsic, simdSize, simdBaseType, 2, &immLowerBound, &immUpperBound);
            op3 = addRangeCheckIfNeeded(intrinsic, op3, immLowerBound, immUpperBound);

            retNode = isScalar ? gtNewScalarHWIntrinsicNode(retType, op1, op2, op3, intrinsic)
                               : gtNewSimdHWIntrinsicNode(retType, op1, op2, op3, intrinsic, simdBaseJitType, simdSize);

            retNode->AsHWIntrinsic()->SetSimdBaseJitType(simdBaseJitType);
            break;
        }

        case NI_Sve_SaturatingDecrementByActiveElementCount:
        case NI_Sve_SaturatingIncrementByActiveElementCount:
        {
            assert(sig->numArgs == 2);

            CORINFO_ARG_LIST_HANDLE arg1     = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2     = info.compCompHnd->getArgNext(arg1);
            var_types               argType  = TYP_UNKNOWN;
            CORINFO_CLASS_HANDLE    argClass = NO_CLASS_HANDLE;

            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
            op2     = getArgForHWIntrinsic(argType, argClass);
            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
            op1     = impPopStack().val;

            CorInfoType op1BaseJitType = getBaseJitTypeOfSIMDType(argClass);

            // HWInstrinsic requires a mask for op2
            if (!varTypeIsMask(op2))
            {
                op2 = gtNewSimdCvtVectorToMaskNode(TYP_MASK, op2, simdBaseJitType, simdSize);
            }

            retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, intrinsic, simdBaseJitType, simdSize);

            retNode->AsHWIntrinsic()->SetSimdBaseJitType(simdBaseJitType);
            retNode->AsHWIntrinsic()->SetAuxiliaryJitType(op1BaseJitType);
            break;
        }
        case NI_Sve_GatherPrefetch8Bit:
        case NI_Sve_GatherPrefetch16Bit:
        case NI_Sve_GatherPrefetch32Bit:
        case NI_Sve_GatherPrefetch64Bit:
        case NI_Sve_Prefetch16Bit:
        case NI_Sve_Prefetch32Bit:
        case NI_Sve_Prefetch64Bit:
        case NI_Sve_Prefetch8Bit:
        {
            assert((sig->numArgs == 3) || (sig->numArgs == 4));
            assert(!isScalar);

            var_types            argType       = TYP_UNKNOWN;
            CORINFO_CLASS_HANDLE argClass      = NO_CLASS_HANDLE;
            int                  immLowerBound = 0;
            int                  immUpperBound = 0;

            CORINFO_ARG_LIST_HANDLE arg1 = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2 = info.compCompHnd->getArgNext(arg1);
            CORINFO_ARG_LIST_HANDLE arg3 = info.compCompHnd->getArgNext(arg2);

            HWIntrinsicInfo::lookupImmBounds(intrinsic, simdSize, simdBaseType, 1, &immLowerBound, &immUpperBound);

            if (sig->numArgs == 3)
            {
                argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg3, &argClass)));
                op3     = getArgForHWIntrinsic(argType, argClass);

                assert(HWIntrinsicInfo::isImmOp(intrinsic, op3));
                op3 = addRangeCheckIfNeeded(intrinsic, op3, immLowerBound, immUpperBound);

                argType                    = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
                op2                        = getArgForHWIntrinsic(argType, argClass);
                CorInfoType op2BaseJitType = getBaseJitTypeOfSIMDType(argClass);
                argType                    = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
                op1                        = impPopStack().val;

#ifdef DEBUG

                if ((intrinsic == NI_Sve_GatherPrefetch8Bit) || (intrinsic == NI_Sve_GatherPrefetch16Bit) ||
                    (intrinsic == NI_Sve_GatherPrefetch32Bit) || (intrinsic == NI_Sve_GatherPrefetch64Bit))
                {
                    assert(varTypeIsSIMD(op2->TypeGet()));
                }
                else
                {
                    assert(varTypeIsIntegral(op2->TypeGet()));
                }
#endif
                retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, op3, intrinsic, simdBaseJitType, simdSize);
                retNode->AsHWIntrinsic()->SetAuxiliaryJitType(op2BaseJitType);
            }
            else
            {
                CORINFO_ARG_LIST_HANDLE arg4 = info.compCompHnd->getArgNext(arg3);
                argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg4, &argClass)));
                op4     = getArgForHWIntrinsic(argType, argClass);

                assert(HWIntrinsicInfo::isImmOp(intrinsic, op4));
                op4 = addRangeCheckIfNeeded(intrinsic, op4, immLowerBound, immUpperBound);

                argType                    = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg3, &argClass)));
                op3                        = getArgForHWIntrinsic(argType, argClass);
                CorInfoType op3BaseJitType = getBaseJitTypeOfSIMDType(argClass);
                argType                    = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
                op2                        = getArgForHWIntrinsic(argType, argClass);
                argType                    = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
                op1                        = impPopStack().val;

                assert(varTypeIsSIMD(op3->TypeGet()));
                retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, op3, op4, intrinsic, simdBaseJitType, simdSize);
                retNode->AsHWIntrinsic()->SetAuxiliaryJitType(op3BaseJitType);
            }

            break;
        }
        case NI_Sve_ConditionalExtractAfterLastActiveElementScalar:
        case NI_Sve_ConditionalExtractLastActiveElementScalar:
        {
            assert(sig->numArgs == 3);

#ifdef DEBUG
            isValidScalarIntrinsic = true;
#endif

            CORINFO_ARG_LIST_HANDLE arg1     = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2     = info.compCompHnd->getArgNext(arg1);
            CORINFO_ARG_LIST_HANDLE arg3     = info.compCompHnd->getArgNext(arg2);
            var_types               argType  = TYP_UNKNOWN;
            CORINFO_CLASS_HANDLE    argClass = NO_CLASS_HANDLE;

            argType                    = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg3, &argClass)));
            op3                        = getArgForHWIntrinsic(argType, argClass);
            argType                    = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
            op2                        = getArgForHWIntrinsic(argType, argClass);
            CorInfoType op2BaseJitType = getBaseJitTypeOfSIMDType(argClass);
            argType                    = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
            op1                        = getArgForHWIntrinsic(argType, argClass);

            retNode = gtNewScalarHWIntrinsicNode(retType, op1, op2, op3, intrinsic);

            retNode->AsHWIntrinsic()->SetSimdBaseJitType(simdBaseJitType);
            retNode->AsHWIntrinsic()->SetAuxiliaryJitType(op2BaseJitType);
            break;
        }

        case NI_Sve_ExtractAfterLastActiveElementScalar:
        case NI_Sve_ExtractLastActiveElementScalar:
        {
            assert(sig->numArgs == 2);

#ifdef DEBUG
            isValidScalarIntrinsic = true;
#endif

            CORINFO_ARG_LIST_HANDLE arg1     = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2     = info.compCompHnd->getArgNext(arg1);
            var_types               argType  = TYP_UNKNOWN;
            CORINFO_CLASS_HANDLE    argClass = NO_CLASS_HANDLE;

            argType                    = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
            op2                        = getArgForHWIntrinsic(argType, argClass);
            CorInfoType op2BaseJitType = getBaseJitTypeOfSIMDType(argClass);
            argType                    = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
            op1                        = getArgForHWIntrinsic(argType, argClass);

            retNode = gtNewScalarHWIntrinsicNode(retType, op1, op2, intrinsic);

            retNode->AsHWIntrinsic()->SetSimdBaseJitType(simdBaseJitType);
            break;
        }

        case NI_Sve_MultiplyAddRotateComplexBySelectedScalar:
        case NI_Sve2_DotProductRotateComplexBySelectedIndex:
        {
            assert(sig->numArgs == 5);
            assert(!isScalar);

            CORINFO_ARG_LIST_HANDLE arg1     = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2     = info.compCompHnd->getArgNext(arg1);
            CORINFO_ARG_LIST_HANDLE arg3     = info.compCompHnd->getArgNext(arg2);
            CORINFO_ARG_LIST_HANDLE arg4     = info.compCompHnd->getArgNext(arg3);
            CORINFO_ARG_LIST_HANDLE arg5     = info.compCompHnd->getArgNext(arg4);
            var_types               argType  = TYP_UNKNOWN;
            CORINFO_CLASS_HANDLE    argClass = NO_CLASS_HANDLE;

            int imm1LowerBound, imm1UpperBound; // Range for rotation
            int imm2LowerBound, imm2UpperBound; // Range for index
            HWIntrinsicInfo::lookupImmBounds(intrinsic, simdSize, simdBaseType, 1, &imm1LowerBound, &imm1UpperBound);
            HWIntrinsicInfo::lookupImmBounds(intrinsic, simdSize, simdBaseType, 2, &imm2LowerBound, &imm2UpperBound);

            argType      = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg5, &argClass)));
            GenTree* op5 = getArgForHWIntrinsic(argType, argClass);
            assert(HWIntrinsicInfo::isImmOp(intrinsic, op5));
            op5 = addRangeCheckIfNeeded(intrinsic, op5, imm1LowerBound, imm1UpperBound);

            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg4, &argClass)));
            op4     = getArgForHWIntrinsic(argType, argClass);
            assert(HWIntrinsicInfo::isImmOp(intrinsic, op4));
            op4 = addRangeCheckIfNeeded(intrinsic, op4, imm2LowerBound, imm2UpperBound);

            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg3, &argClass)));
            op3     = getArgForHWIntrinsic(argType, argClass);
            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
            op2     = getArgForHWIntrinsic(argType, argClass);
            argType = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
            op1     = getArgForHWIntrinsic(argType, argClass);

            SetOpLclRelatedToSIMDIntrinsic(op1);
            SetOpLclRelatedToSIMDIntrinsic(op2);
            SetOpLclRelatedToSIMDIntrinsic(op3);
            SetOpLclRelatedToSIMDIntrinsic(op4);
            SetOpLclRelatedToSIMDIntrinsic(op5);
            retNode = new (this, GT_HWINTRINSIC) GenTreeHWIntrinsic(retType, getAllocator(CMK_ASTNode), intrinsic,
                                                                    simdBaseJitType, simdSize, op1, op2, op3, op4, op5);
            break;
        }

        case NI_Sve2_VectorTableLookup:
        {
            assert(sig->numArgs == 2);
            assert(retType != TYP_VOID);

            CORINFO_ARG_LIST_HANDLE arg1     = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2     = info.compCompHnd->getArgNext(arg1);
            CORINFO_CLASS_HANDLE    argClass = NO_CLASS_HANDLE;
            var_types argType1 = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
            var_types argType2 = JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));

            var_types   simdBaseType   = JitType2PreciseVarType(simdBaseJitType);
            CorInfoType op1BaseJitType = getBaseJitTypeOfSIMDType(argClass);

            op2 = impPopStack().val;
            op1 = impPopStack().val;

            if (op1->TypeIs(TYP_STRUCT))
            {
                info.compNeedsConsecutiveRegisters = true;
                unsigned fieldCount                = info.compCompHnd->getClassNumInstanceFields(argClass);
                op1                                = gtConvertTableOpToFieldList(op1, fieldCount);
            }
            retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, intrinsic, simdBaseJitType, simdSize);
            retNode->AsHWIntrinsic()->SetAuxiliaryJitType(op1BaseJitType);
            break;
        }

        case NI_Sve2_AddWideningEven:
        case NI_Sve2_AddWideningOdd:
        case NI_Sve2_SubtractWideningEven:
        case NI_Sve2_SubtractWideningOdd:
        {
            assert(sig->numArgs == 2);

            CORINFO_ARG_LIST_HANDLE arg1     = sig->args;
            CORINFO_ARG_LIST_HANDLE arg2     = info.compCompHnd->getArgNext(arg1);
            CORINFO_CLASS_HANDLE    argClass = NO_CLASS_HANDLE;

            JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg2, &argClass)));
            JITtype2varType(strip(info.compCompHnd->getArgType(sig, arg1, &argClass)));
            op2 = impPopStack().val;
            op1 = impPopStack().val;

            CorInfoType op1BaseJitType = getBaseJitTypeOfSIMDType(argClass);
            retNode = gtNewSimdHWIntrinsicNode(retType, op1, op2, intrinsic, simdBaseJitType, simdSize);
            retNode->AsHWIntrinsic()->SetSimdBaseJitType(simdBaseJitType);
            retNode->AsHWIntrinsic()->SetAuxiliaryJitType(op1BaseJitType);
            break;
        }

        default:
        {
            return nullptr;
        }
    }

    if (isMinMaxIntrinsic)
    {
        assert(sig->numArgs == 2);
        assert(retNode == nullptr);

        if (isNative && BlockNonDeterministicIntrinsics(mustExpand))
        {
            return nullptr;
        }

        op2 = impSIMDPopStack();
        op1 = impSIMDPopStack();

        if (isNative)
        {
            assert(!isMagnitude && !isNumber);
            retNode = gtNewSimdMinMaxNativeNode(retType, op1, op2, simdBaseJitType, simdSize, isMax);
        }
        else
        {
            retNode = gtNewSimdMinMaxNode(retType, op1, op2, simdBaseJitType, simdSize, isMax, isMagnitude, isNumber);
        }
    }

    assert(!isScalar || isValidScalarIntrinsic);

    return retNode;
}

//------------------------------------------------------------------------
// gtNewSimdAllTrueMaskNode: Create a mask with all bits set to true
//
// Arguments:
//    simdBaseJitType -- the base jit type of the nodes being masked
//
// Return Value:
//    The mask
//
GenTree* Compiler::gtNewSimdAllTrueMaskNode(CorInfoType simdBaseJitType)
{
    // Import as a constant mask

    var_types      simdBaseType = JitType2PreciseVarType(simdBaseJitType);
    GenTreeMskCon* mskCon       = gtNewMskConNode(TYP_MASK);

    // TODO-SVE: For agnostic VL, vector type may not be simd16_t

    bool found = EvaluateSimdPatternToMask<simd16_t>(simdBaseType, &mskCon->gtSimdMaskVal, SveMaskPatternAll);
    assert(found);

    return mskCon;
}

//------------------------------------------------------------------------
// gtNewSimdFalseMaskByteNode: Create a mask with all bits set to false
//
// Return Value:
//    The mask
//
GenTree* Compiler::gtNewSimdFalseMaskByteNode()
{
    // Import as a constant mask 0
    GenTreeMskCon* mskCon = gtNewMskConNode(TYP_MASK);
    mskCon->gtSimdMaskVal = simdmask_t::Zero();
    return mskCon;
}

#endif // FEATURE_HW_INTRINSICS
