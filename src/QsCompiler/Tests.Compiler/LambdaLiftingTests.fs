﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Quantum.QsCompiler.Testing

open System.Collections.Immutable
open System.Text.RegularExpressions
open Microsoft.Quantum.QsCompiler.SyntaxTokens
open Microsoft.Quantum.QsCompiler.SyntaxTree
open Microsoft.Quantum.QsCompiler.Transformations.LiftLambdas
open Xunit

type ResolvedTypeKind = QsTypeKind<ResolvedType, UserDefinedType, QsTypeParameter, CallableInformation>

type LambdaLiftingTests() =

    let compileLambdaLiftingTest testNumber =
        let srcChunks = TestUtils.readAndChunkSourceFile "LambdaLifting.qs"
        srcChunks.Length >= testNumber + 1 |> Assert.True
        let shared = srcChunks.[0]
        let compilationDataStructures = TestUtils.buildContent <| shared + srcChunks.[testNumber]
        let processedCompilation = LiftLambdaExpressions.Apply compilationDataStructures.BuiltCompilation
        Assert.NotNull processedCompilation

        Signatures.SignatureCheck
            [ Signatures.LambdaLiftingNS ]
            Signatures.LambdaLiftingSignatures.[testNumber - 1]
            processedCompilation

        processedCompilation

    let assertLambdaFunctorsByLine result line parentName expectedFunctors =
        let regexMatch = Regex.Match(line, sprintf "_[a-z0-9]{32}_%s" parentName)
        Assert.True(regexMatch.Success, "The original callable did not have the expected content.")

        TestUtils.getCallableWithName result Signatures.LambdaLiftingNS regexMatch.Value
        |> TestUtils.assertCallSupportsFunctors expectedFunctors

    [<Fact>]
    [<Trait("Category", "Return Values")>]
    member this.``With Return Value``() =
        let result = compileLambdaLiftingTest 1

        let original = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "Foo" |> TestUtils.getBodyFromCallable

        let generated =
            TestUtils.getCallablesWithSuffix result Signatures.LambdaLiftingNS "_Foo"
            |> Seq.exactlyOne
            |> TestUtils.getBodyFromCallable

        let lines = generated |> TestUtils.getLinesFromSpecialization

        let expectedContent = [| "return 0;" |]
        Assert.True((lines = expectedContent), "The generated callable did not have the expected content.")

        let lines = original |> TestUtils.getLinesFromSpecialization
        
        let expectedContent = [| sprintf "let lambda = %O(_);" generated.Parent |]
        Assert.True((lines = expectedContent), "The original callable did not have the expected content.")

    [<Fact>]
    [<Trait("Category", "Return Values")>]
    member this.``Without Return Value``() =
        let result = compileLambdaLiftingTest 2

        let original = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "Foo" |> TestUtils.getBodyFromCallable

        let generated =
            TestUtils.getCallablesWithSuffix result Signatures.LambdaLiftingNS "_Foo"
            |> Seq.exactlyOne
            |> TestUtils.getBodyFromCallable

        let lines = generated |> TestUtils.getLinesFromSpecialization

        Assert.True(
            0 = Seq.length lines,
            sprintf "Callable %O(%A) did not have the expected number of statements." generated.Parent generated.Kind
        )

        let lines = original |> TestUtils.getLinesFromSpecialization

        let expectedContent = [| sprintf "let lambda = %O(_);" generated.Parent |]
        Assert.True((lines = expectedContent), "The original callable did not have the expected content.")

    [<Fact>]
    [<Trait("Category", "Return Values")>]
    member this.``Call Valued Callable``() =
        let result = compileLambdaLiftingTest 3

        let original = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "Foo" |> TestUtils.getBodyFromCallable

        let generated =
            TestUtils.getCallablesWithSuffix result Signatures.LambdaLiftingNS "_Foo"
            |> Seq.exactlyOne
            |> TestUtils.getBodyFromCallable

        let lines = generated |> TestUtils.getLinesFromSpecialization

        let expectedContent = [| "return Microsoft.Quantum.Testing.LambdaLifting.Bar();" |]
        Assert.True((lines = expectedContent), "The generated callable did not have the expected content.")

        let lines = original |> TestUtils.getLinesFromSpecialization

        let expectedContent = [| sprintf "let lambda = %O(_);" generated.Parent |]
        Assert.True((lines = expectedContent), "The original callable did not have the expected content.")

    [<Fact>]
    [<Trait("Category", "Return Values")>]
    member this.``Call Unit Callable``() =
        let result = compileLambdaLiftingTest 4

        let original = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "Foo" |> TestUtils.getBodyFromCallable

        let generated =
            TestUtils.getCallablesWithSuffix result Signatures.LambdaLiftingNS "_Foo"
            |> Seq.exactlyOne
            |> TestUtils.getBodyFromCallable

        let lines = generated |> TestUtils.getLinesFromSpecialization

        let expectedContent = [| "Microsoft.Quantum.Testing.LambdaLifting.Bar();" |]
        Assert.True((lines = expectedContent), "The generated callable did not have the expected content.")

        let lines = original |> TestUtils.getLinesFromSpecialization

        let expectedContent = [| sprintf "let lambda = %O(_);" generated.Parent |]
        Assert.True((lines = expectedContent), "The original callable did not have the expected content.")

    [<Fact>]
    [<Trait("Category", "Return Values")>]
    member this.``Call Valued Callable Recursive``() =
        let result = compileLambdaLiftingTest 5

        let original = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "Foo" |> TestUtils.getBodyFromCallable

        let generated =
            TestUtils.getCallablesWithSuffix result Signatures.LambdaLiftingNS "_Foo"
            |> Seq.exactlyOne
            |> TestUtils.getBodyFromCallable

        let lines = generated |> TestUtils.getLinesFromSpecialization

        let expectedContent = [| "return Microsoft.Quantum.Testing.LambdaLifting.Foo();" |]
        Assert.True((lines = expectedContent), "The generated callable did not have the expected content.")

        let lines = original |> TestUtils.getLinesFromSpecialization

        Assert.True(
            2 = Seq.length lines,
            sprintf "Callable %O(%A) did not have the expected number of statements." original.Parent original.Kind
        )

        let expected = sprintf "let lambda = %O(_);" generated.Parent
        Assert.True(lines.[0] = expected, "The generated call expression did not have the correct arguments.")

    [<Fact>]
    [<Trait("Category", "Return Values")>]
    member this.``Call Unit Callable Recursive``() =
        let result = compileLambdaLiftingTest 6

        let original = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "Foo" |> TestUtils.getBodyFromCallable

        let generated =
            TestUtils.getCallablesWithSuffix result Signatures.LambdaLiftingNS "_Foo"
            |> Seq.exactlyOne
            |> TestUtils.getBodyFromCallable

        let lines = generated |> TestUtils.getLinesFromSpecialization

        let expectedContent = [| "Microsoft.Quantum.Testing.LambdaLifting.Foo();" |]
        Assert.True((lines = expectedContent), "The generated callable did not have the expected content.")

        let lines = original |> TestUtils.getLinesFromSpecialization

        let expectedContent = [| sprintf "let lambda = %O(_);" generated.Parent |]
        Assert.True((lines = expectedContent), "The original callable did not have the expected content.")

    [<Fact>]
    [<Trait("Category", "Parameters")>]
    member this.``Use Closure``() =
        let result = compileLambdaLiftingTest 7

        let original = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "Foo" |> TestUtils.getBodyFromCallable

        let generated =
            TestUtils.getCallablesWithSuffix result Signatures.LambdaLiftingNS "_Foo"
            |> Seq.exactlyOne
            |> TestUtils.getBodyFromCallable

        let lines = generated |> TestUtils.getLinesFromSpecialization

        let expectedContent = [| "return (x, y, z);" |]
        Assert.True((lines = expectedContent), "The generated callable did not have the expected content.")

        let lines = original |> TestUtils.getLinesFromSpecialization

        Assert.True(
            5 = Seq.length lines,
            sprintf "Callable %O(%A) did not have the expected number of statements." original.Parent original.Kind
        )

        let expected = sprintf "let lambda = %O(x, y, z, _);" generated.Parent
        Assert.True(lines.[4] = expected, "The generated call expression did not have the correct arguments.")

    [<Fact>]
    [<Trait("Category", "Parameters")>]
    member this.``With Lots of Params``() = compileLambdaLiftingTest 8 |> ignore

    [<Fact>]
    [<Trait("Category", "Parameters")>]
    member this.``Use Closure With Params``() = compileLambdaLiftingTest 9 |> ignore

    [<Fact>]
    [<Trait("Category", "Return Values")>]
    member this.``Function Lambda``() =
        let result = compileLambdaLiftingTest 10

        let generated =
            TestUtils.getCallablesWithSuffix result Signatures.LambdaLiftingNS "_Foo"
            |> Seq.exactlyOne

        Assert.True(generated.Kind = QsCallableKind.Function, "The generated callable was expected to be a function.")

    [<Fact>]
    [<Trait("Category", "Return Values")>]
    member this.``With Type Parameters``() =
        let testNumber = 11
        let srcChunks = TestUtils.readAndChunkSourceFile "LambdaLifting.qs"
        srcChunks.Length >= testNumber + 1 |> Assert.True
        let shared = srcChunks.[0]
        let compilationDataStructures = TestUtils.buildContent <| shared + srcChunks.[testNumber]
        let result = LiftLambdaExpressions.Apply compilationDataStructures.BuiltCompilation
        Assert.NotNull result

        let generated =
            TestUtils.getCallablesWithSuffix result Signatures.LambdaLiftingNS "_Foo"
            |> Seq.exactlyOne

        let originalExpectedName = { Namespace = Signatures.LambdaLiftingNS; Name = "Foo" }
        let ``Foo.A`` = QsTypeParameter.New(originalExpectedName, "A") |> TypeParameter |> ResolvedType.New
        let ``Foo.B`` = QsTypeParameter.New(originalExpectedName, "B") |> TypeParameter |> ResolvedType.New
        let ``Foo.C`` = QsTypeParameter.New(originalExpectedName, "C") |> TypeParameter |> ResolvedType.New

        let originalExpectedArgType =
            [| ``Foo.A``; ``Foo.B``; ``Foo.C`` |]
            |> ImmutableArray.ToImmutableArray
            |> QsTypeKind.TupleType
            |> ResolvedType.New

        let originalExpectedReturnType = ResolvedType.New(ResolvedTypeKind.UnitType)

        let originalSigExpected = originalExpectedName, originalExpectedArgType, originalExpectedReturnType

        let generatedExpectedName = { Namespace = Signatures.LambdaLiftingNS; Name = generated.FullName.Name }
        let ``_Foo.A`` = QsTypeParameter.New(generatedExpectedName, "A") |> TypeParameter |> ResolvedType.New
        let ``_Foo.C`` = QsTypeParameter.New(generatedExpectedName, "C") |> TypeParameter |> ResolvedType.New

        let generatedExpectedArgType =
            [| ``_Foo.A``; ``_Foo.C``; ResolvedType.New(ResolvedTypeKind.UnitType) |]
            |> ImmutableArray.ToImmutableArray
            |> QsTypeKind.TupleType
            |> ResolvedType.New

        let generatedExpectedReturnType =
            [| ``_Foo.C``; ``_Foo.A`` |]
            |> ImmutableArray.ToImmutableArray
            |> QsTypeKind.TupleType
            |> ResolvedType.New

        let generatedSigExpected = generatedExpectedName, generatedExpectedArgType, generatedExpectedReturnType

        Signatures.SignatureCheck
            [ Signatures.LambdaLiftingNS ]
            (seq {
                originalSigExpected
                generatedSigExpected
             })
            result

    [<Fact>]
    [<Trait("Category", "Return Values")>]
    member this.``With Nested Lambda Call``() = compileLambdaLiftingTest 12

    [<Fact>]
    [<Trait("Category", "Return Values")>]
    member this.``With Nested Lambda``() = compileLambdaLiftingTest 13

    [<Fact(Skip = "Known Bug: https://github.com/microsoft/qsharp-compiler/issues/1113")>]
    [<Trait("Category", "Functor Support")>]
    member this.``Functor Support Basic Return``() =
        let result = compileLambdaLiftingTest 14

        let original = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "Foo" |> TestUtils.getBodyFromCallable
        let lines = original |> TestUtils.getLinesFromSpecialization

        Assert.True(
            2 = Seq.length lines,
            sprintf "Callable %O(%A) did not have the expected number of statements." original.Parent original.Kind
        )

        assertLambdaFunctorsByLine result lines.[0] "Foo" []
        assertLambdaFunctorsByLine result lines.[1] "Foo" [ QsFunctor.Adjoint; QsFunctor.Controlled ]

    [<Fact>]
    [<Trait("Category", "Functor Support")>]
    member this.``Functor Support Call``() =
        let result = compileLambdaLiftingTest 15

        let original = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "Foo" |> TestUtils.getBodyFromCallable
        let lines = original |> TestUtils.getLinesFromSpecialization

        Assert.True(
            5 = Seq.length lines,
            sprintf "Callable %O(%A) did not have the expected number of statements." original.Parent original.Kind
        )

        assertLambdaFunctorsByLine result lines.[0] "Foo" []
        assertLambdaFunctorsByLine result lines.[1] "Foo" []
        assertLambdaFunctorsByLine result lines.[2] "Foo" [ QsFunctor.Adjoint ]
        assertLambdaFunctorsByLine result lines.[3] "Foo" [ QsFunctor.Controlled ]
        assertLambdaFunctorsByLine result lines.[4] "Foo" [ QsFunctor.Adjoint; QsFunctor.Controlled ]

    [<Fact(Skip = "Known Bug: https://github.com/microsoft/qsharp-compiler/issues/1113")>]
    [<Trait("Category", "Functor Support")>]
    member this.``Functor Support Lambda Call``() =
        let result = compileLambdaLiftingTest 16

        let original = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "Foo" |> TestUtils.getBodyFromCallable
        let lines = original |> TestUtils.getLinesFromSpecialization

        Assert.True(
            4 = Seq.length lines,
            sprintf "Callable %O(%A) did not have the expected number of statements." original.Parent original.Kind
        )

        assertLambdaFunctorsByLine result lines.[0] "Foo" [] // This line fails due to known bug. See skip reason.
        assertLambdaFunctorsByLine result lines.[1] "Foo" [ QsFunctor.Adjoint ]
        assertLambdaFunctorsByLine result lines.[2] "Foo" [ QsFunctor.Controlled ]
        assertLambdaFunctorsByLine result lines.[3] "Foo" [ QsFunctor.Adjoint; QsFunctor.Controlled ]

    [<Fact>]
    [<Trait("Category", "Functor Support")>]
    member this.``Functor Support Recursive``() =
        let result = compileLambdaLiftingTest 17

        let Foo = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "Foo" |> TestUtils.getBodyFromCallable
        let lines = Foo |> TestUtils.getLinesFromSpecialization

        Assert.True(
            1 = Seq.length lines,
            sprintf "Callable %O(%A) did not have the expected number of statements." Foo.Parent Foo.Kind
        )

        assertLambdaFunctorsByLine result lines.[0] "Foo" []

        let FooAdj = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "FooAdj" |> TestUtils.getBodyFromCallable
        let lines = FooAdj |> TestUtils.getLinesFromSpecialization

        Assert.True(
            1 = Seq.length lines,
            sprintf "Callable %O(%A) did not have the expected number of statements." FooAdj.Parent FooAdj.Kind
        )

        assertLambdaFunctorsByLine result lines.[0] "FooAdj" [ QsFunctor.Adjoint ]

        let FooCtl = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "FooCtl" |> TestUtils.getBodyFromCallable
        let lines = FooCtl |> TestUtils.getLinesFromSpecialization

        Assert.True(
            1 = Seq.length lines,
            sprintf "Callable %O(%A) did not have the expected number of statements." FooCtl.Parent FooCtl.Kind
        )

        assertLambdaFunctorsByLine result lines.[0] "FooCtl" [ QsFunctor.Controlled ]

        let FooAdjCtl = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "FooAdjCtl" |> TestUtils.getBodyFromCallable
        let lines = FooAdjCtl |> TestUtils.getLinesFromSpecialization

        Assert.True(
            1 = Seq.length lines,
            sprintf "Callable %O(%A) did not have the expected number of statements." FooAdjCtl.Parent FooAdjCtl.Kind
        )

        assertLambdaFunctorsByLine result lines.[0] "FooAdjCtl" [ QsFunctor.Adjoint; QsFunctor.Controlled ]

    [<Fact>]
    [<Trait("Category", "Parameters")>]
    member this.``With Missing Params``() = compileLambdaLiftingTest 18

    [<Fact>]
    [<Trait("Category", "Return Values")>]
    member this.``Use Parameter Single``() =
        let result = compileLambdaLiftingTest 19

        let original = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "Foo" |> TestUtils.getBodyFromCallable

        let generated =
            TestUtils.getCallablesWithSuffix result Signatures.LambdaLiftingNS "_Foo"
            |> Seq.exactlyOne
            |> TestUtils.getBodyFromCallable

        let lines = generated |> TestUtils.getLinesFromSpecialization

        let expectedContent = [| "return x;" |]
        Assert.True((lines = expectedContent), "The generated callable did not have the expected content.")

        let lines = original |> TestUtils.getLinesFromSpecialization
        
        let expectedContent = [| sprintf "let lambda = %O(_);" generated.Parent; "let result = lambda(0);" |]
        Assert.True((lines = expectedContent), "The original callable did not have the expected content.")

    [<Fact>]
    [<Trait("Category", "Return Values")>]
    member this.``Use Parameter Tuple``() =
        let result = compileLambdaLiftingTest 20

        let original = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "Foo" |> TestUtils.getBodyFromCallable

        let generated =
            TestUtils.getCallablesWithSuffix result Signatures.LambdaLiftingNS "_Foo"
            |> Seq.exactlyOne
            |> TestUtils.getBodyFromCallable

        let lines = generated |> TestUtils.getLinesFromSpecialization

        let expectedContent = [| "return (y, x);" |]
        Assert.True((lines = expectedContent), "The generated callable did not have the expected content.")

        let lines = original |> TestUtils.getLinesFromSpecialization
        
        let expectedContent = [| sprintf "let lambda = %O(_, _);" generated.Parent; "let result = lambda(0.0, 0);" |]
        Assert.True((lines = expectedContent), "The original callable did not have the expected content.")

    [<Fact>]
    [<Trait("Category", "Return Values")>]
    member this.``Use Parameter and Closure``() =
        let result = compileLambdaLiftingTest 21
        
        let original = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "Foo" |> TestUtils.getBodyFromCallable

        let generated =
            TestUtils.getCallablesWithSuffix result Signatures.LambdaLiftingNS "_Foo"
            |> Seq.exactlyOne
            |> TestUtils.getBodyFromCallable

        let lines = generated |> TestUtils.getLinesFromSpecialization

        let expectedContent = [| "return (a, x);" |]
        Assert.True((lines = expectedContent), "The generated callable did not have the expected content.")

        let lines = original |> TestUtils.getLinesFromSpecialization
        
        let expectedContent = [| "let a = 0;"; sprintf "let lambda = %O(a, _);" generated.Parent; "let result = lambda(0.0);" |]
        Assert.True((lines = expectedContent), "The original callable did not have the expected content.")

    [<Fact>]
    [<Trait("Category", "Return Values")>]
    member this.``Use Parameter with Missing Params``() =
        let result = compileLambdaLiftingTest 22
    
        let original = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "Foo" |> TestUtils.getBodyFromCallable

        let generated =
            TestUtils.getCallablesWithSuffix result Signatures.LambdaLiftingNS "_Foo"
            |> Seq.exactlyOne
            |> TestUtils.getBodyFromCallable

        let lines = generated |> TestUtils.getLinesFromSpecialization

        let expectedContent = [| "return x;" |]
        Assert.True((lines = expectedContent), "The generated callable did not have the expected content.")

        let lines = original |> TestUtils.getLinesFromSpecialization
        
        let expectedContent = [| sprintf "let lambda = %O(_, _, _);" generated.Parent; "let result = lambda(0, Zero, \"Zero\");" |]
        Assert.True((lines = expectedContent), "The original callable did not have the expected content.")

    [<Fact>]
    [<Trait("Category", "Return Values")>]
    member this.``Multiple Lambdas in One Expression``() =
        let result = compileLambdaLiftingTest 23
    
        let original = TestUtils.getCallableWithName result Signatures.LambdaLiftingNS "Foo" |> TestUtils.getBodyFromCallable

        let generated =
            TestUtils.getCallablesWithSuffix result Signatures.LambdaLiftingNS "_Foo"
            |> (fun x ->
                Assert.True(2 = Seq.length x)
                x |> Seq.map TestUtils.getBodyFromCallable)

        let hasFirstContent spec =
            let lines = spec |> TestUtils.getLinesFromSpecialization
            lines = [| "return x + 1;" |]
        let hasSecondContent spec =
            let lines = spec |> TestUtils.getLinesFromSpecialization
            lines = [| "return x + 2;" |]

        let first, second =
            let temp1 = Seq.item 0 generated
            let temp2 = Seq.item 1 generated
            
            if (hasFirstContent temp1) then
                Assert.True(hasSecondContent temp2, sprintf "Callable %O(%A) did not have expected content" temp2.Parent QsSpecializationKind.QsBody)
                temp1, temp2
            else
                Assert.True(hasFirstContent temp2, sprintf "Callable %O(%A) did not have expected content" temp2.Parent QsSpecializationKind.QsBody)
                Assert.True(hasSecondContent temp1, sprintf "Callable %O(%A) did not have expected content" temp1.Parent QsSpecializationKind.QsBody)
                temp2, temp1

        let lines = original |> TestUtils.getLinesFromSpecialization
        
        let expectedContent = [| sprintf "let lambdaTuple = (%O(_), %O(_));" first.Parent second.Parent |]
        Assert.True((lines = expectedContent), "The original callable did not have the expected content.")
