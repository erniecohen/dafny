// RUN: %verify --integer-bitwise --standard-libraries:false --resource-limit 200000 --cores 2 "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-runtime-go.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: python3 "%repositoryRoot/.github/review/issue193-runtime-go.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.dir" --resolver refreshed --quick > "%t"
// RUN: %diff "%s.runtime.expect" "%t"
// https://github.com/erniecohen/dafny/issues/193
include "../../../../../DafnyStandardLibraries/optional/IntegerBitwisePopulation.dfy"
import F = Std.Arithmetic.IntegerBitwiseCompilerFoundation
import R = Std.Arithmetic.IntegerBitwise
import C = Std.Arithmetic.IntegerBitwisePopulation
newtype {:bitWidth 8} {:nativeType "byte"} U8=x:nat | x<256
function Population8(a:U8):nat
 ensures Population8(a)==IntegerBitwiseModel.popNat(8,a as int)
 ensures Population8(a)==R.count(R.bitsNat(8,a as int))
 ensures Population8(a)<=8
{ C.View(8,a as int); a.PopCount() }
lemma Zero8() ensures Population8(0)==0
{ R.PopZero(8); F.Population(8,0); }
lemma Ones8(a:U8) requires a as int==R.P(8)-1 ensures Population8(a)==8
{ C.AllOnes(8); }
lemma Power8(a:U8,i:nat) requires i<8 requires a as int==R.P(i)
 ensures Population8(a)==1 { R.PopPower(8,i); F.Population(8,a as int); }
lemma High8(a:U8) requires a as int==R.P(7) ensures Population8(a)==1
{ Power8(a,7); }
newtype {:bitWidth 8} {:nativeType "byte"} Strict=x:nat | 16<=x<256 && x%2==0 witness 16
function StrictPopulation(a:Strict):nat ensures StrictPopulation(a)<=8
 ensures StrictPopulation(a)==IntegerBitwiseModel.popNat(8,a as int)
{ C.View(8,a as int); a.PopCount() }
type Word = U8
function Alias(a:Word):nat ensures Alias(a)<=8 { C.View(8,a as int); a.PopCount() }
lemma Generic(n:nat,a:int) ensures 0<=IntegerBitwiseModel.popNat(n,a)<=n
 ensures IntegerBitwiseModel.popNat(n,a)==R.count(R.bitsNat(n,a)) { C.View(n,a); }
lemma ZeroWidth(a:int) ensures IntegerBitwiseModel.popNat(0,a)==0 { C.View(0,a); }
