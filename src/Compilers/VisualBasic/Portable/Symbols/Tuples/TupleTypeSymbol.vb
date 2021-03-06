﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.Globalization
Imports System.Runtime.InteropServices
Imports System.Threading
Imports Microsoft.CodeAnalysis.Collections
Imports Microsoft.CodeAnalysis.RuntimeMembers

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    Friend NotInheritable Class TupleTypeSymbol
        Inherits WrappedNamedTypeSymbol

        Private ReadOnly _locations As ImmutableArray(Of Location)

        Private ReadOnly _elementLocations As ImmutableArray(Of Location)

        Private ReadOnly _elementNames As ImmutableArray(Of String)

        Private ReadOnly _elementTypes As ImmutableArray(Of TypeSymbol)

        Private _lazyMembers As ImmutableArray(Of Symbol)

        Private _lazyFields As ImmutableArray(Of FieldSymbol)

        Private _lazyUnderlyingDefinitionToMemberMap As SmallDictionary(Of Symbol, Symbol)

        Friend Const RestPosition As Integer = 8

        Friend Const TupleTypeName As String = "ValueTuple"

        Private Shared ReadOnly tupleTypes As WellKnownType() = New WellKnownType() {WellKnownType.System_ValueTuple_T1, WellKnownType.System_ValueTuple_T2, WellKnownType.System_ValueTuple_T3, WellKnownType.System_ValueTuple_T4, WellKnownType.System_ValueTuple_T5, WellKnownType.System_ValueTuple_T6, WellKnownType.System_ValueTuple_T7, WellKnownType.System_ValueTuple_TRest}

        Private Shared ReadOnly tupleCtors As WellKnownMember() = New WellKnownMember() {WellKnownMember.System_ValueTuple_T1__ctor, WellKnownMember.System_ValueTuple_T2__ctor, WellKnownMember.System_ValueTuple_T3__ctor, WellKnownMember.System_ValueTuple_T4__ctor, WellKnownMember.System_ValueTuple_T5__ctor, WellKnownMember.System_ValueTuple_T6__ctor, WellKnownMember.System_ValueTuple_T7__ctor, WellKnownMember.System_ValueTuple_TRest__ctor}

        Private Shared ReadOnly tupleMembers As WellKnownMember()() = New WellKnownMember()() {
            New WellKnownMember() {WellKnownMember.System_ValueTuple_T1__Item1},
            New WellKnownMember() {WellKnownMember.System_ValueTuple_T2__Item1, WellKnownMember.System_ValueTuple_T2__Item2},
            New WellKnownMember() {WellKnownMember.System_ValueTuple_T3__Item1, WellKnownMember.System_ValueTuple_T3__Item2, WellKnownMember.System_ValueTuple_T3__Item3},
            New WellKnownMember() {WellKnownMember.System_ValueTuple_T4__Item1, WellKnownMember.System_ValueTuple_T4__Item2, WellKnownMember.System_ValueTuple_T4__Item3, WellKnownMember.System_ValueTuple_T4__Item4},
            New WellKnownMember() {WellKnownMember.System_ValueTuple_T5__Item1, WellKnownMember.System_ValueTuple_T5__Item2, WellKnownMember.System_ValueTuple_T5__Item3, WellKnownMember.System_ValueTuple_T5__Item4, WellKnownMember.System_ValueTuple_T5__Item5},
            New WellKnownMember() {WellKnownMember.System_ValueTuple_T6__Item1, WellKnownMember.System_ValueTuple_T6__Item2, WellKnownMember.System_ValueTuple_T6__Item3, WellKnownMember.System_ValueTuple_T6__Item4, WellKnownMember.System_ValueTuple_T6__Item5, WellKnownMember.System_ValueTuple_T6__Item6},
            New WellKnownMember() {WellKnownMember.System_ValueTuple_T7__Item1, WellKnownMember.System_ValueTuple_T7__Item2, WellKnownMember.System_ValueTuple_T7__Item3, WellKnownMember.System_ValueTuple_T7__Item4, WellKnownMember.System_ValueTuple_T7__Item5, WellKnownMember.System_ValueTuple_T7__Item6, WellKnownMember.System_ValueTuple_T7__Item7},
            New WellKnownMember() {WellKnownMember.System_ValueTuple_TRest__Item1, WellKnownMember.System_ValueTuple_TRest__Item2, WellKnownMember.System_ValueTuple_TRest__Item3, WellKnownMember.System_ValueTuple_TRest__Item4, WellKnownMember.System_ValueTuple_TRest__Item5, WellKnownMember.System_ValueTuple_TRest__Item6, WellKnownMember.System_ValueTuple_TRest__Item7, WellKnownMember.System_ValueTuple_TRest__Rest}}

        Public Overrides ReadOnly Property IsTupleType As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property TupleUnderlyingType As NamedTypeSymbol
            Get
                Return Me._underlyingType
            End Get
        End Property

        Public Overrides ReadOnly Property TupleElementTypes As ImmutableArray(Of TypeSymbol)
            Get
                Return Me._elementTypes
            End Get
        End Property

        Public Overrides ReadOnly Property TupleElementNames As ImmutableArray(Of String)
            Get
                Return Me._elementNames
            End Get
        End Property

        Public Overrides ReadOnly Property IsReferenceType As Boolean
            Get
                Return Not Me._underlyingType.IsErrorType() AndAlso Me._underlyingType.IsReferenceType
            End Get
        End Property

        Public Overrides ReadOnly Property IsValueType As Boolean
            Get
                Return Me._underlyingType.IsErrorType() OrElse Me._underlyingType.IsValueType
            End Get
        End Property

        Public Overrides ReadOnly Property IsImplicitlyDeclared As Boolean
            Get
                Return False
            End Get
        End Property

        Public ReadOnly Property TupleElementFields As ImmutableArray(Of FieldSymbol)
            Get
                Dim isDefault As Boolean = Me._lazyFields.IsDefault
                If isDefault Then
                    ImmutableInterlocked.InterlockedInitialize(Of FieldSymbol)(Me._lazyFields, Me.CollectTupleElementFields())
                End If
                Return Me._lazyFields
            End Get
        End Property

        Friend ReadOnly Property UnderlyingDefinitionToMemberMap As SmallDictionary(Of Symbol, Symbol)
            Get
                If Me._lazyUnderlyingDefinitionToMemberMap Is Nothing Then
                    Me._lazyUnderlyingDefinitionToMemberMap = Me.ComputeDefinitionToMemberMap()
                End If
                Return Me._lazyUnderlyingDefinitionToMemberMap
            End Get
        End Property

        Public Overrides ReadOnly Property EnumUnderlyingType As NamedTypeSymbol
            Get
                Return Me._underlyingType.EnumUnderlyingType
            End Get
        End Property

        Public Overrides ReadOnly Property Kind As SymbolKind
            Get
                Return SymbolKind.NamedType
            End Get
        End Property

        Public Overrides ReadOnly Property TypeKind As TypeKind
            Get
                Return If(Me._underlyingType.TypeKind = TypeKind.Class, TypeKind.Class, TypeKind.Struct)
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingSymbol As Symbol
            Get
                Return Me._underlyingType.ContainingSymbol
            End Get
        End Property

        Public Overrides ReadOnly Property Locations As ImmutableArray(Of Location)
            Get
                Return Me._locations
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference)
            Get
                Return Symbol.GetDeclaringSyntaxReferenceHelper(Of VisualBasicSyntaxNode)(Me._locations)
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
            Get
                Dim result As Accessibility
                If Me._underlyingType.IsErrorType() Then
                    result = Accessibility.[Public]
                Else
                    result = Me._underlyingType.DeclaredAccessibility
                End If
                Return result
            End Get
        End Property

        Public Overrides ReadOnly Property IsMustInherit As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property IsNotInheritable As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property Arity As Integer
            Get
                Return 0
            End Get
        End Property

        Public Overrides ReadOnly Property TypeParameters As ImmutableArray(Of TypeParameterSymbol)
            Get
                Return ImmutableArray(Of TypeParameterSymbol).Empty
            End Get
        End Property

        Friend Overrides ReadOnly Property TypeArgumentsCustomModifiers As ImmutableArray(Of ImmutableArray(Of CustomModifier))
            Get
                Return ImmutableArray(Of ImmutableArray(Of CustomModifier)).Empty
            End Get
        End Property

        Friend Overrides ReadOnly Property HasTypeArgumentsCustomModifiers As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property TypeArgumentsNoUseSiteDiagnostics As ImmutableArray(Of TypeSymbol)
            Get
                Return ImmutableArray(Of TypeSymbol).Empty
            End Get
        End Property

        Public Overrides ReadOnly Property ConstructedFrom As NamedTypeSymbol
            Get
                Return Me
            End Get
        End Property

        Public Overrides ReadOnly Property MightContainExtensionMethods As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return String.Empty
            End Get
        End Property

        Friend Overrides ReadOnly Property MangleName As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Iterator Property MemberNames As IEnumerable(Of String)
            Get
                Dim [set] = PooledHashSet(Of String).GetInstance()
                For Each member In GetMembers()
                    Dim name = member.Name
                    If [set].Add(name) Then
                        Yield name
                    End If
                Next

                [set].Free()
            End Get
        End Property

        Friend Overrides ReadOnly Property HasSpecialName As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property IsComImport As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property IsWindowsRuntimeImport As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property ShouldAddWinRTMembers As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property IsSerializable As Boolean
            Get
                Return Me._underlyingType.IsSerializable
            End Get
        End Property

        Friend Overrides ReadOnly Property Layout As TypeLayout
            Get
                Return Me._underlyingType.Layout
            End Get
        End Property

        Friend Overrides ReadOnly Property MarshallingCharSet As CharSet
            Get
                Return Me._underlyingType.MarshallingCharSet
            End Get
        End Property

        Friend Overrides ReadOnly Property HasDeclarativeSecurity As Boolean
            Get
                Return Me._underlyingType.HasDeclarativeSecurity
            End Get
        End Property

        Friend Overrides ReadOnly Property IsInterface As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property IsExtensibleInterfaceNoUseSiteDiagnostics As Boolean
            Get
                Return Me._underlyingType.IsExtensibleInterfaceNoUseSiteDiagnostics
            End Get
        End Property

        Friend Overrides ReadOnly Property CanConstruct As Boolean
            Get
                Return False
            End Get
        End Property

        Friend Overrides ReadOnly Property TypeSubstitution As TypeSubstitution
            Get
                Return Nothing
            End Get
        End Property

        Private Sub New(locationOpt As Location, underlyingType As NamedTypeSymbol, elementLocations As ImmutableArray(Of Location), elementNames As ImmutableArray(Of String), elementTypes As ImmutableArray(Of TypeSymbol))
            Me.New(If((locationOpt Is Nothing), ImmutableArray(Of Location).Empty, ImmutableArray.Create(Of Location)(locationOpt)), underlyingType, elementLocations, elementNames, elementTypes)
        End Sub

        Private Sub New(locations As ImmutableArray(Of Location), underlyingType As NamedTypeSymbol, elementLocations As ImmutableArray(Of Location), elementNames As ImmutableArray(Of String), elementTypes As ImmutableArray(Of TypeSymbol))
            MyBase.New(underlyingType)
            Debug.Assert(elementLocations.IsDefault OrElse elementLocations.Length = elementTypes.Length)
            Debug.Assert(elementNames.IsDefault OrElse elementNames.Length = elementTypes.Length)
            Debug.Assert(Not underlyingType.IsTupleType)
            Me._elementLocations = elementLocations
            Me._elementNames = elementNames
            Me._elementTypes = elementTypes
            Me._locations = locations
        End Sub

        Friend Shared Function Create(
                                     locationOpt As Location,
                                     elementTypes As ImmutableArray(Of TypeSymbol),
                                     elementLocations As ImmutableArray(Of Location),
                                     elementNames As ImmutableArray(Of String),
                                     compilation As VisualBasicCompilation,
                                     Optional syntax As SyntaxNode = Nothing,
                                     Optional diagnostics As DiagnosticBag = Nothing) As TupleTypeSymbol

            Debug.Assert(elementNames.IsDefault OrElse elementTypes.Length = elementNames.Length)
            Dim length As Integer = elementTypes.Length

            If length <= 1 Then
                Throw ExceptionUtilities.Unreachable
            End If

            Dim tupleUnderlyingType As NamedTypeSymbol = TupleTypeSymbol.GetTupleUnderlyingType(elementTypes, syntax, compilation, diagnostics)
            Return TupleTypeSymbol.Create(locationOpt, tupleUnderlyingType, elementLocations, elementNames)
        End Function

        Public Shared Function Create(tupleCompatibleType As NamedTypeSymbol) As TupleTypeSymbol
            Return TupleTypeSymbol.Create(ImmutableArray(Of Location).Empty, tupleCompatibleType, Nothing, Nothing)
        End Function

        Public Shared Function Create(locationOpt As Location, tupleCompatibleType As NamedTypeSymbol, elementLocations As ImmutableArray(Of Location), elementNames As ImmutableArray(Of String)) As TupleTypeSymbol
            Return TupleTypeSymbol.Create(If((locationOpt Is Nothing), ImmutableArray(Of Location).Empty, ImmutableArray.Create(Of Location)(locationOpt)), tupleCompatibleType, elementLocations, elementNames)
        End Function

        Public Shared Function Create(locations As ImmutableArray(Of Location), tupleCompatibleType As NamedTypeSymbol, elementLocations As ImmutableArray(Of Location), elementNames As ImmutableArray(Of String)) As TupleTypeSymbol
            Debug.Assert(tupleCompatibleType.IsTupleCompatible())

            Dim elementTypes As ImmutableArray(Of TypeSymbol)
            If tupleCompatibleType.Arity = TupleTypeSymbol.RestPosition Then
                tupleCompatibleType = TupleTypeSymbol.EnsureRestExtensionsAreTuples(tupleCompatibleType)
                Dim tupleElementTypes As ImmutableArray(Of TypeSymbol) = tupleCompatibleType.TypeArgumentsNoUseSiteDiagnostics(TupleTypeSymbol.RestPosition - 1).TupleElementTypes
                Dim instance As ArrayBuilder(Of TypeSymbol) = ArrayBuilder(Of TypeSymbol).GetInstance(TupleTypeSymbol.RestPosition - 1 + tupleElementTypes.Length)
                instance.AddRange(tupleCompatibleType.TypeArgumentsNoUseSiteDiagnostics, TupleTypeSymbol.RestPosition - 1)
                instance.AddRange(tupleElementTypes)
                elementTypes = instance.ToImmutableAndFree()
            Else
                elementTypes = tupleCompatibleType.TypeArgumentsNoUseSiteDiagnostics
            End If

            Return New TupleTypeSymbol(locations, tupleCompatibleType, elementLocations, elementNames, elementTypes)
        End Function

        Private Shared Function EnsureRestExtensionsAreTuples(tupleCompatibleType As NamedTypeSymbol) As NamedTypeSymbol
            If Not tupleCompatibleType.TypeArgumentsNoUseSiteDiagnostics(TupleTypeSymbol.RestPosition - 1).IsTupleType Then
                Dim nonTupleTypeChain As ArrayBuilder(Of NamedTypeSymbol) = ArrayBuilder(Of NamedTypeSymbol).GetInstance()
                Dim namedTypeSymbol As NamedTypeSymbol = tupleCompatibleType

                Do
                    nonTupleTypeChain.Add(namedTypeSymbol)
                    namedTypeSymbol = CType(namedTypeSymbol.TypeArgumentsNoUseSiteDiagnostics(TupleTypeSymbol.RestPosition - 1), NamedTypeSymbol)
                Loop While namedTypeSymbol.Arity = TupleTypeSymbol.RestPosition

                If Not namedTypeSymbol.IsTupleType Then
                    nonTupleTypeChain.Add(namedTypeSymbol)
                End If

                Debug.Assert(nonTupleTypeChain.Count > 1)
                tupleCompatibleType = nonTupleTypeChain.Pop()

                Dim typeArgumentsBuilder As ArrayBuilder(Of TypeWithModifiers) = ArrayBuilder(Of TypeWithModifiers).GetInstance(TupleTypeSymbol.RestPosition)
                Do
                    Dim extensionTuple As TupleTypeSymbol = TupleTypeSymbol.Create(CType(Nothing, Location), tupleCompatibleType, Nothing, Nothing)
                    tupleCompatibleType = nonTupleTypeChain.Pop()
                    tupleCompatibleType = TupleTypeSymbol.ReplaceRestExtensionType(tupleCompatibleType, typeArgumentsBuilder, extensionTuple)
                Loop While nonTupleTypeChain.Count <> 0

                typeArgumentsBuilder.Free()
                nonTupleTypeChain.Free()
            End If
            Return tupleCompatibleType
        End Function

        Private Shared Function ReplaceRestExtensionType(tupleCompatibleType As NamedTypeSymbol, typeArgumentsBuilder As ArrayBuilder(Of TypeWithModifiers), extensionTuple As TupleTypeSymbol) As NamedTypeSymbol
            Dim modifiers As ImmutableArray(Of ImmutableArray(Of CustomModifier)) = Nothing
            Dim hasTypeArgumentsCustomModifiers As Boolean = tupleCompatibleType.HasTypeArgumentsCustomModifiers

            If hasTypeArgumentsCustomModifiers Then
                modifiers = tupleCompatibleType.TypeArgumentsCustomModifiers
            End If

            Dim typeArgumentsNoUseSiteDiagnostics As ImmutableArray(Of TypeSymbol) = tupleCompatibleType.TypeArgumentsNoUseSiteDiagnostics
            typeArgumentsBuilder.Clear()

            For i As Integer = 0 To TupleTypeSymbol.RestPosition - 1 - 1
                typeArgumentsBuilder.Add(New TypeWithModifiers(typeArgumentsNoUseSiteDiagnostics(i), GetModifiers(modifiers, i)))
            Next

            typeArgumentsBuilder.Add(New TypeWithModifiers(extensionTuple, GetModifiers(modifiers, TupleTypeSymbol.RestPosition - 1)))

            Dim definition = tupleCompatibleType.ConstructedFrom
            Dim subst = TypeSubstitution.Create(definition, definition.TypeParameters, typeArgumentsBuilder.ToImmutable(), False)
            Return definition.Construct(subst)
        End Function

        Private Shared Function GetModifiers(modifiers As ImmutableArray(Of ImmutableArray(Of CustomModifier)), i As Integer) As ImmutableArray(Of CustomModifier)
            Return If(modifiers.IsDefaultOrEmpty, Nothing, modifiers(i))
        End Function

        Friend Function WithUnderlyingType(newUnderlyingType As NamedTypeSymbol) As TupleTypeSymbol
            Debug.Assert(Not newUnderlyingType.IsTupleType AndAlso newUnderlyingType.IsTupleOrCompatibleWithTupleOfCardinality(Me._elementTypes.Length))
            Return TupleTypeSymbol.Create(Me._locations, newUnderlyingType, Me._elementLocations, Me._elementNames)
        End Function

        Friend Function WithElementNames(newElementNames As ImmutableArray(Of String)) As TupleTypeSymbol
            Debug.Assert(newElementNames.IsDefault OrElse Me._elementTypes.Length = newElementNames.Length)

            If Me._elementNames.IsDefault Then
                If newElementNames.IsDefault Then
                    Return Me
                End If
            Else
                If Not newElementNames.IsDefault AndAlso Me._elementNames.SequenceEqual(newElementNames) Then
                    Return Me
                End If
            End If

            Return New TupleTypeSymbol(CType(Nothing, Location), Me._underlyingType, Nothing, newElementNames, Me._elementTypes)
        End Function

        Friend Shared Sub GetUnderlyingTypeChain(underlyingTupleType As NamedTypeSymbol, underlyingTupleTypeChain As ArrayBuilder(Of NamedTypeSymbol))
            Dim namedTypeSymbol As NamedTypeSymbol = underlyingTupleType
            While True
                underlyingTupleTypeChain.Add(namedTypeSymbol)

                If namedTypeSymbol.Arity <> TupleTypeSymbol.RestPosition Then
                    Exit While
                End If
                namedTypeSymbol = namedTypeSymbol.TypeArgumentsNoUseSiteDiagnostics(TupleTypeSymbol.RestPosition - 1).TupleUnderlyingType
            End While
        End Sub

        Friend Shared Sub AddElementTypes(underlyingTupleType As NamedTypeSymbol, tupleElementTypes As ArrayBuilder(Of TypeSymbol))
            Dim namedTypeSymbol As NamedTypeSymbol = underlyingTupleType
            While True
                Dim isTupleType As Boolean = namedTypeSymbol.IsTupleType
                If isTupleType Then
                    Exit While
                End If

                Dim length As Integer = Math.Min(namedTypeSymbol.Arity, TupleTypeSymbol.RestPosition - 1)
                tupleElementTypes.AddRange(namedTypeSymbol.TypeArgumentsNoUseSiteDiagnostics, length)
                If namedTypeSymbol.Arity <> TupleTypeSymbol.RestPosition Then
                    Return
                End If
                namedTypeSymbol = CType(namedTypeSymbol.TypeArgumentsNoUseSiteDiagnostics(TupleTypeSymbol.RestPosition - 1), NamedTypeSymbol)
            End While
            tupleElementTypes.AddRange(namedTypeSymbol.TupleElementTypes)
        End Sub

        Private Shared Function GetNestedTupleUnderlyingType(topLevelUnderlyingType As NamedTypeSymbol, depth As Integer) As NamedTypeSymbol
            Dim namedTypeSymbol As NamedTypeSymbol = topLevelUnderlyingType
            For i As Integer = 0 To depth - 1
                namedTypeSymbol = namedTypeSymbol.TypeArgumentsNoUseSiteDiagnostics(TupleTypeSymbol.RestPosition - 1).TupleUnderlyingType
            Next
            Return namedTypeSymbol
        End Function

        Private Shared Function NumberOfValueTuples(numElements As Integer, <Out()> ByRef remainder As Integer) As Integer
            remainder = (numElements - 1) Mod (RestPosition - 1) + 1
            Return (numElements - 1) \ (RestPosition - 1) + 1
        End Function

        Private Shared Function GetTupleUnderlyingType(elementTypes As ImmutableArray(Of TypeSymbol), syntax As SyntaxNode, compilation As VisualBasicCompilation, diagnostics As DiagnosticBag) As NamedTypeSymbol
            Dim numElements As Integer = elementTypes.Length
            Dim remainder As Integer
            Dim chainLength As Integer = TupleTypeSymbol.NumberOfValueTuples(numElements, remainder)

            Dim wellKnownType As NamedTypeSymbol = compilation.GetWellKnownType(TupleTypeSymbol.GetTupleType(remainder))

            If diagnostics IsNot Nothing AndAlso syntax IsNot Nothing Then
                Binder.ReportUseSiteError(diagnostics, syntax, wellKnownType)
            End If

            Dim namedTypeSymbol As NamedTypeSymbol = wellKnownType.Construct(ImmutableArray.Create(Of TypeSymbol)(elementTypes, (chainLength - 1) * (TupleTypeSymbol.RestPosition - 1), remainder))
            Dim [loop] As Integer = chainLength - 1
            If [loop] > 0 Then
                Dim wellKnownType2 As NamedTypeSymbol = compilation.GetWellKnownType(TupleTypeSymbol.GetTupleType(TupleTypeSymbol.RestPosition))

                If diagnostics IsNot Nothing AndAlso syntax IsNot Nothing Then
                    Binder.ReportUseSiteError(diagnostics, syntax, wellKnownType2)
                End If
                Do
                    Dim typeArguments As ImmutableArray(Of TypeSymbol) = ImmutableArray.Create(Of TypeSymbol)(elementTypes, ([loop] - 1) * (TupleTypeSymbol.RestPosition - 1), TupleTypeSymbol.RestPosition - 1).Add(namedTypeSymbol)
                    namedTypeSymbol = wellKnownType2.Construct(typeArguments)
                    [loop] -= 1
                Loop While [loop] > 0
            End If
            Return namedTypeSymbol
        End Function

        Friend Shared Sub VerifyTupleTypePresent(cardinality As Integer, syntax As VisualBasicSyntaxNode, compilation As VisualBasicCompilation, diagnostics As DiagnosticBag)
            Debug.Assert(diagnostics IsNot Nothing AndAlso syntax IsNot Nothing)
            Dim arity As Integer
            Dim num As Integer = TupleTypeSymbol.NumberOfValueTuples(cardinality, arity)
            Dim wellKnownType As NamedTypeSymbol = compilation.GetWellKnownType(TupleTypeSymbol.GetTupleType(arity))
            Binder.ReportUseSiteError(diagnostics, syntax, wellKnownType)

            If num > 1 Then
                Dim wellKnownType2 As NamedTypeSymbol = compilation.GetWellKnownType(TupleTypeSymbol.GetTupleType(TupleTypeSymbol.RestPosition))
                Binder.ReportUseSiteError(diagnostics, syntax, wellKnownType2)
            End If
        End Sub

        Private Shared Function GetTupleType(arity As Integer) As WellKnownType
            If arity > TupleTypeSymbol.RestPosition Then
                Throw ExceptionUtilities.Unreachable
            End If

            Return TupleTypeSymbol.tupleTypes(arity - 1)
        End Function

        Friend Shared Function GetTupleCtor(arity As Integer) As WellKnownMember
            If arity > TupleTypeSymbol.RestPosition Then
                Throw ExceptionUtilities.Unreachable
            End If

            Return TupleTypeSymbol.tupleCtors(arity - 1)
        End Function

        Friend Shared Function GetTupleTypeMember(arity As Integer, position As Integer) As WellKnownMember
            Return TupleTypeSymbol.tupleMembers(arity - 1)(position - 1)
        End Function

        Friend Shared Function TupleMemberName(position As Integer) As String
            Return "Item" & position
        End Function

        Private Shared ForbiddenNames As HashSet(Of String) = New HashSet(Of String)(
            {"CompareTo", "Deconstruct", "Equals", "GetHashCode", "Rest", "ToString"},
            IdentifierComparison.Comparer)

        Private Shared Function IsElementNameForbidden(name As String) As Boolean
            Return ForbiddenNames.Contains(name)
        End Function

        Friend Shared Function IsElementNameReserved(name As String) As Integer
            Dim result As Integer
            If TupleTypeSymbol.IsElementNameForbidden(name) Then
                result = 0
            Else
                If IdentifierComparison.StartsWith(name, "Item") Then
                    Dim s As String = name.Substring(4)
                    Dim num As Integer

                    If Integer.TryParse(s, num) Then
                        If num > 0 AndAlso IdentifierComparison.Equals(name, TupleTypeSymbol.TupleMemberName(num)) Then
                            result = num
                            Return result
                        End If
                    End If
                End If
                result = -1
            End If
            Return result
        End Function

        Private Shared Function GetWellKnownMemberInType(type As NamedTypeSymbol, relativeMember As WellKnownMember) As Symbol
            Debug.Assert(relativeMember >= WellKnownMember.System_ValueTuple_T1__Item1 AndAlso relativeMember <= WellKnownMember.System_ValueTuple_TRest__ctor)
            Debug.Assert(type.IsDefinition)
            Dim descriptor As MemberDescriptor = WellKnownMembers.GetDescriptor(relativeMember)
            Return VisualBasicCompilation.GetRuntimeMember(type, descriptor, VisualBasicCompilation.SpecialMembersSignatureComparer.Instance, Nothing)
        End Function

        Friend Shared Function GetWellKnownMemberInType(type As NamedTypeSymbol, relativeMember As WellKnownMember, diagnostics As DiagnosticBag, syntax As SyntaxNode) As Symbol
            Dim wellKnownMemberInType As Symbol = TupleTypeSymbol.GetWellKnownMemberInType(type, relativeMember)

            If wellKnownMemberInType Is Nothing Then
                Dim descriptor As MemberDescriptor = WellKnownMembers.GetDescriptor(relativeMember)
                Binder.ReportDiagnostic(diagnostics, syntax, ERRID.ERR_MissingRuntimeHelper, type.Name & "."c & descriptor.Name)
            Else
                Dim useSiteDiagnostic As DiagnosticInfo = wellKnownMemberInType.GetUseSiteErrorInfo
                If useSiteDiagnostic IsNot Nothing AndAlso useSiteDiagnostic.Severity = DiagnosticSeverity.[Error] Then
                    diagnostics.Add(useSiteDiagnostic, syntax.GetLocation())
                End If
            End If
            Return wellKnownMemberInType
        End Function

        Private Function CollectTupleElementFields() As ImmutableArray(Of FieldSymbol)
            Dim builder = ArrayBuilder(Of FieldSymbol).GetInstance(_elementTypes.Length, Nothing)

            For Each member In GetMembers()
                If member.Kind <> SymbolKind.Field Then
                    Continue For
                End If

                Dim index = If(TryCast(member, TupleFieldSymbol)?.TupleFieldId,
                                DirectCast(member, TupleErrorFieldSymbol).TupleFieldId)

                If index >= 0 Then
                    Debug.Assert(builder(index) Is Nothing)
                    builder(index) = DirectCast(member, FieldSymbol)
                End If
            Next

            Debug.Assert(builder.All(Function(symbol) symbol IsNot Nothing))

            Return builder.ToImmutableAndFree()
        End Function

        Public Overrides Function GetMembers() As ImmutableArray(Of Symbol)
            Dim isDefault As Boolean = Me._lazyMembers.IsDefault
            If isDefault Then
                ImmutableInterlocked.InterlockedInitialize(Of Symbol)(Me._lazyMembers, Me.CreateMembers())
            End If
            Return Me._lazyMembers
        End Function

        Private Function CreateMembers() As ImmutableArray(Of Symbol)
            Dim namesOfVirtualFields = ArrayBuilder(Of String).GetInstance(_elementTypes.Length)

            If _elementNames.IsDefault Then
                For i As Integer = 1 To _elementTypes.Length
                    namesOfVirtualFields.Add(TupleMemberName(i))
                Next
            Else
                namesOfVirtualFields.AddRange(_elementNames)
            End If

            Dim members = ArrayBuilder(Of Symbol).GetInstance(Math.Max(_elementTypes.Length, _underlyingType.OriginalDefinition.GetMembers().Length))

            Dim currentUnderlying As NamedTypeSymbol = _underlyingType
            Dim currentNestingLevel = 0

            Dim currentFieldsForElements = ArrayBuilder(Of FieldSymbol).GetInstance(currentUnderlying.Arity)

            ' Lookup field definitions that we are interested in
            CollectTargetTupleFields(currentUnderlying, currentFieldsForElements)

            Dim underlyingMembers As ImmutableArray(Of Symbol) = currentUnderlying.OriginalDefinition.GetMembers()

            Do
                For Each member In underlyingMembers
                    Select Case member.Kind
                        Case SymbolKind.Method
                            If currentNestingLevel = 0 Then
                                members.Add(New TupleMethodSymbol(Me, DirectCast(member, MethodSymbol).AsMember(currentUnderlying)))
                            End If

                        Case SymbolKind.Field
                            Dim field = DirectCast(member, FieldSymbol)

                            Dim tupleFieldIndex = currentFieldsForElements.IndexOf(field, ReferenceEqualityComparer.Instance)
                            If tupleFieldIndex >= 0 Then
                                ' This Is a tuple backing field
                                Dim FieldSymbol = field.AsMember(currentUnderlying)

                                If currentNestingLevel <> 0 Then
                                    ' This is a matching field, but it is in the extension tuple
                                    tupleFieldIndex += (RestPosition - 1) * currentNestingLevel


                                    Dim defaultName As String = TupleMemberName(tupleFieldIndex + 1)

                                    ' Add a field with default name regardless
                                    members.Add(New TupleRenamedElementFieldSymbol(Me, FieldSymbol, defaultName, -members.Count - 1, Nothing))

                                    If Not IdentifierComparison.Equals(namesOfVirtualFields(tupleFieldIndex), defaultName) Then
                                        ' The name given doesn't match default name ItemTupleTypeSymbol.RestPosition, etc.
                                        ' Add a field with the given name
                                        Dim location = If(_elementLocations.IsDefault, Nothing, _elementLocations(tupleFieldIndex))

                                        If IdentifierComparison.Equals(field.Name, namesOfVirtualFields(tupleFieldIndex)) Then
                                            members.Add(New TupleElementFieldSymbol(Me, FieldSymbol, tupleFieldIndex, location))
                                        Else
                                            members.Add(New TupleRenamedElementFieldSymbol(Me, FieldSymbol, namesOfVirtualFields(tupleFieldIndex), tupleFieldIndex, location))
                                        End If
                                    End If
                                ElseIf IdentifierComparison.Equals(field.Name, namesOfVirtualFields(tupleFieldIndex)) Then
                                    members.Add(New TupleElementFieldSymbol(Me, FieldSymbol, tupleFieldIndex,
                                                                                    If(_elementLocations.IsDefault, Nothing, _elementLocations(tupleFieldIndex))))
                                Else
                                    ' Add a field with default name
                                    members.Add(New TupleFieldSymbol(Me, FieldSymbol, -members.Count - 1))

                                    ' Add a field with the given name
                                    members.Add(New TupleRenamedElementFieldSymbol(Me, FieldSymbol, namesOfVirtualFields(tupleFieldIndex), tupleFieldIndex,
                                                                                            If(_elementLocations.IsDefault, Nothing, _elementLocations(tupleFieldIndex))))
                                End If

                                namesOfVirtualFields(tupleFieldIndex) = Nothing ' mark as handled

                            ElseIf currentNestingLevel = 0 Then
                                ' field at the top level didn't match a tuple backing field, simply add.
                                members.Add(New TupleFieldSymbol(Me, field.AsMember(currentUnderlying), -members.Count - 1))
                            End If

                        Case SymbolKind.NamedType
                                        ' We are dropping nested types, if any. Pending real need.

                        Case SymbolKind.Property
                            If currentNestingLevel = 0 Then
                                members.Add(New TuplePropertySymbol(Me, DirectCast(member, PropertySymbol).AsMember(currentUnderlying)))
                            End If

                        Case SymbolKind.Event
                            If currentNestingLevel = 0 Then
                                members.Add(New TupleEventSymbol(Me, DirectCast(member, EventSymbol).AsMember(currentUnderlying)))
                            End If

                        Case Else
                            If currentNestingLevel = 0 Then
                                Throw ExceptionUtilities.UnexpectedValue(member.Kind)
                            End If
                    End Select
                Next

                If currentUnderlying.Arity <> RestPosition Then
                    Exit Do
                End If

                Dim oldUnderlying = currentUnderlying
                currentUnderlying = oldUnderlying.TypeArgumentsNoUseSiteDiagnostics(RestPosition - 1).TupleUnderlyingType
                currentNestingLevel += 1

                If currentUnderlying.Arity <> RestPosition Then
                    ' refresh members And target fields
                    underlyingMembers = currentUnderlying.OriginalDefinition.GetMembers()
                    currentFieldsForElements.Clear()
                    CollectTargetTupleFields(currentUnderlying, currentFieldsForElements)
                Else
                    Debug.Assert(oldUnderlying.OriginalDefinition Is currentUnderlying.OriginalDefinition)
                End If
            Loop

            currentFieldsForElements.Free()

            ' At the end, add remaining virtual fields
            For i As Integer = 0 To namesOfVirtualFields.Count - 1
                Dim Name As String = namesOfVirtualFields(i)
                If Name IsNot Nothing Then
                    ' We couldn't find a backing field for this vitual field. It will be an error to access it. 
                    Dim fieldRemainder As Integer ' one - based
                    Dim fieldChainLength = NumberOfValueTuples(i + 1, fieldRemainder)
                    Dim container As NamedTypeSymbol = GetNestedTupleUnderlyingType(_underlyingType, fieldChainLength - 1).OriginalDefinition

                    Dim diagnosticInfo = If(container.IsErrorType(),
                                                          Nothing,
                                                          ErrorFactory.ErrorInfo(ERRID.ERR_MissingRuntimeHelper,
                                                                               container.Name & "." & TupleMemberName(fieldRemainder)))

                    Dim defaultName As String = TupleMemberName(i + 1)
                    ' Add a field with default name if the given name Is different
                    If Name <> defaultName Then
                        members.Add(New TupleErrorFieldSymbol(Me, defaultName, -members.Count - 1, Nothing, _elementTypes(i), diagnosticInfo))
                    End If

                    members.Add(New TupleErrorFieldSymbol(Me, Name, i,
                                                      If(_elementLocations.IsDefault, Nothing, _elementLocations(i)),
                                                      _elementTypes(i),
                                                      diagnosticInfo))
                End If
            Next

            Return members.ToImmutableAndFree()
        End Function

        Private Shared Sub CollectTargetTupleFields(underlying As NamedTypeSymbol, fieldsForElements As ArrayBuilder(Of FieldSymbol))
            underlying = underlying.OriginalDefinition
            Dim num As Integer = Math.Min(underlying.Arity, TupleTypeSymbol.RestPosition - 1)
            For i As Integer = 0 To num - 1
                Dim tupleTypeMember As WellKnownMember = TupleTypeSymbol.GetTupleTypeMember(underlying.Arity, i + 1)
                fieldsForElements.Add(CType(TupleTypeSymbol.GetWellKnownMemberInType(underlying, tupleTypeMember), FieldSymbol))
            Next
        End Sub

        Private Function ComputeDefinitionToMemberMap() As SmallDictionary(Of Symbol, Symbol)
            Dim smallDictionary As SmallDictionary(Of Symbol, Symbol) = New SmallDictionary(Of Symbol, Symbol)(ReferenceEqualityComparer.Instance)
            Dim originalDefinition As NamedTypeSymbol = Me._underlyingType.OriginalDefinition
            Dim members As ImmutableArray(Of Symbol) = Me.GetMembers()
            Dim i As Integer = members.Length - 1
            While i >= 0
                Dim symbol As Symbol = members(i)
                Dim kind As SymbolKind = symbol.Kind
                Select Case kind
                    Case SymbolKind.[Event]
                        Dim tupleUnderlyingEvent As EventSymbol = DirectCast(symbol, EventSymbol).TupleUnderlyingEvent
                        Dim associatedField As FieldSymbol = tupleUnderlyingEvent.AssociatedField

                        If associatedField IsNot Nothing Then
                            Debug.Assert(associatedField.ContainingSymbol Is Me._underlyingType)
                            Debug.Assert(Me._underlyingType.GetMembers(associatedField.Name).IndexOf(associatedField) < 0)
                            smallDictionary.Add(associatedField.OriginalDefinition, New TupleFieldSymbol(Me, associatedField, -i - 1))
                        End If

                        smallDictionary.Add(tupleUnderlyingEvent.OriginalDefinition, symbol)

                    Case SymbolKind.Field
                        Dim tupleUnderlyingField As FieldSymbol = DirectCast(symbol, FieldSymbol).TupleUnderlyingField
                        If tupleUnderlyingField IsNot Nothing Then
                            smallDictionary(tupleUnderlyingField.OriginalDefinition) = symbol
                        End If

                    Case SymbolKind.Method
                        smallDictionary.Add(DirectCast(symbol, MethodSymbol).TupleUnderlyingMethod.OriginalDefinition, symbol)

                    Case SymbolKind.Property
                        smallDictionary.Add(DirectCast(symbol, PropertySymbol).TupleUnderlyingProperty.OriginalDefinition, symbol)

                    Case Else
                        Throw ExceptionUtilities.UnexpectedValue(symbol.Kind)
                End Select

                i -= 1
            End While

            Return smallDictionary
        End Function

        Public Function GetTupleMemberSymbolForUnderlyingMember(Of TMember As Symbol)(underlyingMemberOpt As TMember) As TMember
            Dim result As TMember
            If underlyingMemberOpt Is Nothing Then
                result = Nothing
            Else
                Dim originalDefinition As Symbol = underlyingMemberOpt.OriginalDefinition
                If originalDefinition.ContainingType Is Me._underlyingType.OriginalDefinition Then
                    Dim symbol As Symbol = Nothing

                    If Me.UnderlyingDefinitionToMemberMap.TryGetValue(originalDefinition, symbol) Then
                        result = CType(symbol, TMember)
                        Return result
                    End If
                End If
                result = Nothing
            End If
            Return result
        End Function

        Public Overrides Function GetMembers(name As String) As ImmutableArray(Of Symbol)
            Return Me.GetMembers().WhereAsArray(Function(m As Symbol) IdentifierComparison.Equals(m.Name, name))
        End Function

        Public Overrides Function GetTypeMembers() As ImmutableArray(Of NamedTypeSymbol)
            ' do not support nested types at the moment
            Debug.Assert(Not GetMembers().Any(Function(m) m.Kind = SymbolKind.NamedType))
            Return ImmutableArray(Of NamedTypeSymbol).Empty
        End Function

        Public Overrides Function GetTypeMembers(name As String) As ImmutableArray(Of NamedTypeSymbol)
            ' do not support nested types at the moment
            Debug.Assert(Not GetMembers().Any(Function(m) m.Kind = SymbolKind.NamedType))
            Return ImmutableArray(Of NamedTypeSymbol).Empty
        End Function

        Public Overrides Function GetTypeMembers(name As String, arity As Integer) As ImmutableArray(Of NamedTypeSymbol)
            ' do not support nested types at the moment
            Debug.Assert(Not GetMembers().Any(Function(m) m.Kind = SymbolKind.NamedType))
            Return ImmutableArray(Of NamedTypeSymbol).Empty
        End Function

        Public Overrides Function GetAttributes() As ImmutableArray(Of VisualBasicAttributeData)
            Return Me._underlyingType.GetAttributes()
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            Dim otherTuple = TryCast(obj, TupleTypeSymbol)
            If otherTuple Is Nothing Then
                Return False
            End If

            Dim otherUnderlying = otherTuple.TupleUnderlyingType
            If (Me.TupleUnderlyingType <> otherUnderlying) Then
                Return False
            End If

            Dim myNames = Me.TupleElementNames
            Dim otherNames = otherTuple.TupleElementNames

            If myNames.IsDefault Then
                Return otherNames.IsDefault
            End If

            If otherNames.IsDefault Then
                Return False
            End If

            Debug.Assert(myNames.Length = otherNames.Length)

            For i As Integer = 0 To myNames.Length - 1
                If Not IdentifierComparison.Equals(myNames(i), otherNames(i)) Then
                    Return False
                End If
            Next

            Return True
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return Me._underlyingType.GetHashCode()
        End Function

        Friend Overrides Function GetUseSiteErrorInfo() As DiagnosticInfo
            Return Me._underlyingType.GetUseSiteErrorInfo()
        End Function

        Friend Overrides Function GetUnificationUseSiteDiagnosticRecursive(owner As Symbol, ByRef checkedTypes As HashSet(Of TypeSymbol)) As DiagnosticInfo
            Return Me._underlyingType.GetUnificationUseSiteDiagnosticRecursive(owner, checkedTypes)
        End Function

        Friend Overrides Function GetAttributeUsageInfo() As AttributeUsageInfo
            Return AttributeUsageInfo.Null
        End Function

        Friend Overrides Function GetAppliedConditionalSymbols() As ImmutableArray(Of String)
            Return ImmutableArray(Of String).Empty
        End Function

        Friend Overrides Function GetFieldsToEmit() As IEnumerable(Of FieldSymbol)
            Throw ExceptionUtilities.Unreachable
        End Function

        Friend Overrides Function GetEventsToEmit() As IEnumerable(Of EventSymbol)
            Throw ExceptionUtilities.Unreachable
        End Function

        Friend Overrides Function GetMethodsToEmit() As IEnumerable(Of MethodSymbol)
            Throw ExceptionUtilities.Unreachable
        End Function

        Friend Overrides Function GetPropertiesToEmit() As IEnumerable(Of PropertySymbol)
            Throw ExceptionUtilities.Unreachable
        End Function

        Friend Overrides Function GetInterfacesToEmit() As IEnumerable(Of NamedTypeSymbol)
            Throw ExceptionUtilities.Unreachable
        End Function

        Friend Overrides Function GetCustomAttributesToEmit(compilationState As ModuleCompilationState) As IEnumerable(Of VisualBasicAttributeData)
            Throw ExceptionUtilities.Unreachable
        End Function

        Public Shared Function TransformToTupleIfCompatible(target As TypeSymbol) As TypeSymbol
            Dim result As TypeSymbol
            If target.IsTupleCompatible() Then
                result = TupleTypeSymbol.Create(CType(target, NamedTypeSymbol))
            Else
                result = target
            End If
            Return result
        End Function

        Public Overrides Function Construct(typeArguments As ImmutableArray(Of TypeSymbol)) As NamedTypeSymbol
            Throw ExceptionUtilities.Unreachable
        End Function

        Friend Overrides Function InternalSubstituteTypeParameters(substitution As TypeSubstitution) As TypeWithModifiers
            Dim substitutedUnderlying = DirectCast(Me.TupleUnderlyingType.InternalSubstituteTypeParameters(substitution).Type, NamedTypeSymbol)
            Dim tupleType = TupleTypeSymbol.Create(Me._locations, substitutedUnderlying, Me._elementLocations, Me._elementNames)

            Return New TypeWithModifiers(tupleType, Nothing)
        End Function

        Friend Overrides Function MakeDeclaredBase(basesBeingResolved As ConsList(Of Symbol), diagnostics As DiagnosticBag) As NamedTypeSymbol
            Return Me._underlyingType.MakeDeclaredBase(basesBeingResolved, diagnostics)
        End Function

        Friend Overrides Function MakeDeclaredInterfaces(basesBeingResolved As ConsList(Of Symbol), diagnostics As DiagnosticBag) As ImmutableArray(Of NamedTypeSymbol)
            Return Me._underlyingType.MakeDeclaredInterfaces(basesBeingResolved, diagnostics)
        End Function

        Friend Overrides Function MakeAcyclicBaseType(diagnostics As DiagnosticBag) As NamedTypeSymbol
            Return Me._underlyingType.MakeAcyclicBaseType(diagnostics)
        End Function

        Friend Overrides Function MakeAcyclicInterfaces(diagnostics As DiagnosticBag) As ImmutableArray(Of NamedTypeSymbol)
            Return Me._underlyingType.MakeAcyclicInterfaces(diagnostics)
        End Function

        Friend Overrides Sub GenerateDeclarationErrors(cancellationToken As CancellationToken)
            Me._underlyingType.GenerateDeclarationErrors(cancellationToken)
        End Sub
    End Class
End Namespace
