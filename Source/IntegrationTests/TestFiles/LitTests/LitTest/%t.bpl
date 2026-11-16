
const $$Language$Dafny: bool
uses {
axiom $$Language$Dafny;
}

type Ty;

type Bv0 = int;

const unique TBool: Ty
uses {
axiom Tag(TBool) == TagBool;
}

const unique TChar: Ty
uses {
axiom Tag(TChar) == TagChar;
}

const unique TInt: Ty
uses {
axiom Tag(TInt) == TagInt;
}

const unique TField: Ty
uses {
axiom Tag(TField) == TagField;
}

const unique TReal: Ty
uses {
axiom Tag(TReal) == TagReal;
}

const unique TORDINAL: Ty
uses {
axiom Tag(TORDINAL) == TagORDINAL;
}

revealed function TBitvector(int) : Ty;

axiom (forall w: int :: { TBitvector(w) } Inv0_TBitvector(TBitvector(w)) == w);

revealed function TSet(Ty) : Ty;

axiom (forall t: Ty :: { TSet(t) } Inv0_TSet(TSet(t)) == t);

axiom (forall t: Ty :: { TSet(t) } Tag(TSet(t)) == TagSet);

revealed function TISet(Ty) : Ty;

axiom (forall t: Ty :: { TISet(t) } Inv0_TISet(TISet(t)) == t);

axiom (forall t: Ty :: { TISet(t) } Tag(TISet(t)) == TagISet);

revealed function TMultiSet(Ty) : Ty;

axiom (forall t: Ty :: { TMultiSet(t) } Inv0_TMultiSet(TMultiSet(t)) == t);

axiom (forall t: Ty :: { TMultiSet(t) } Tag(TMultiSet(t)) == TagMultiSet);

revealed function TSeq(Ty) : Ty;

axiom (forall t: Ty :: { TSeq(t) } Inv0_TSeq(TSeq(t)) == t);

axiom (forall t: Ty :: { TSeq(t) } Tag(TSeq(t)) == TagSeq);

revealed function TMap(Ty, Ty) : Ty;

axiom (forall t: Ty, u: Ty :: { TMap(t, u) } Inv0_TMap(TMap(t, u)) == t);

axiom (forall t: Ty, u: Ty :: { TMap(t, u) } Inv1_TMap(TMap(t, u)) == u);

axiom (forall t: Ty, u: Ty :: { TMap(t, u) } Tag(TMap(t, u)) == TagMap);

revealed function TIMap(Ty, Ty) : Ty;

axiom (forall t: Ty, u: Ty :: { TIMap(t, u) } Inv0_TIMap(TIMap(t, u)) == t);

axiom (forall t: Ty, u: Ty :: { TIMap(t, u) } Inv1_TIMap(TIMap(t, u)) == u);

axiom (forall t: Ty, u: Ty :: { TIMap(t, u) } Tag(TIMap(t, u)) == TagIMap);

revealed function Inv0_TBitvector(Ty) : int;

revealed function Inv0_TSet(Ty) : Ty;

revealed function Inv0_TISet(Ty) : Ty;

revealed function Inv0_TSeq(Ty) : Ty;

revealed function Inv0_TMultiSet(Ty) : Ty;

revealed function Inv0_TMap(Ty) : Ty;

revealed function Inv1_TMap(Ty) : Ty;

revealed function Inv0_TIMap(Ty) : Ty;

revealed function Inv1_TIMap(Ty) : Ty;

type TyTag;

revealed function Tag(Ty) : TyTag;

const unique TagBool: TyTag;

const unique TagChar: TyTag;

const unique TagInt: TyTag;

const unique TagField: TyTag;

const unique TagReal: TyTag;

const unique TagORDINAL: TyTag;

const unique TagSet: TyTag;

const unique TagISet: TyTag;

const unique TagMultiSet: TyTag;

const unique TagSeq: TyTag;

const unique TagMap: TyTag;

const unique TagIMap: TyTag;

const unique TagClass: TyTag;

type TyTagFamily;

revealed function TagFamily(Ty) : TyTagFamily;

revealed function {:identity} Lit<T>(x: T) : T
uses {
axiom (forall<T> x: T :: {:identity} { Lit(x): T } Lit(x): T == x);
}

axiom (forall<T> x: T :: { $Box(Lit(x)) } $Box(Lit(x)) == Lit($Box(x)));

revealed function {:identity} LitInt(x: int) : int
uses {
axiom (forall x: int :: {:identity} { LitInt(x): int } LitInt(x): int == x);
}

axiom (forall x: int :: { $Box(LitInt(x)) } $Box(LitInt(x)) == Lit($Box(x)));

revealed function {:identity} LitReal(x: real) : real
uses {
axiom (forall x: real :: {:identity} { LitReal(x): real } LitReal(x): real == x);
}

axiom (forall x: real :: { $Box(LitReal(x)) } $Box(LitReal(x)) == Lit($Box(x)));

revealed function {:inline} char#IsChar(n: int) : bool
{
  (0 <= n && n < 55296) || (57344 <= n && n < 1114112)
}

type char;

revealed function char#FromInt(int) : char;

axiom (forall n: int :: 
  { char#FromInt(n) } 
  char#IsChar(n) ==> char#ToInt(char#FromInt(n)) == n);

revealed function char#ToInt(char) : int;

axiom (forall ch: char :: 
  { char#ToInt(ch) } 
  $Is(ch, TChar)
     ==> char#FromInt(char#ToInt(ch)) == ch && char#IsChar(char#ToInt(ch)));

revealed function char#Plus(char, char) : char;

axiom (forall a: char, b: char :: 
  { char#Plus(a, b) } 
  char#Plus(a, b) == char#FromInt(char#ToInt(a) + char#ToInt(b)));

revealed function char#Minus(char, char) : char;

axiom (forall a: char, b: char :: 
  { char#Minus(a, b) } 
  char#Minus(a, b) == char#FromInt(char#ToInt(a) - char#ToInt(b)));

type ref;

const null: ref;

const locals: ref;

type FieldFamily;

const unique object_field: FieldFamily;

revealed function field_depth(f: Field) : int;

revealed function field_family(f: Field) : FieldFamily;

revealed function local_field(ff: FieldFamily, depth: int) : Field
uses {
axiom (forall ff: FieldFamily, depth: int :: 
  {:trigger local_field(ff, depth)} 
  field_depth(local_field(ff, depth)) == depth
     && field_family(local_field(ff, depth)) == ff);
}

type Box;

const $ArbitraryBoxValue: Box;

revealed function $Box<T>(T) : Box;

revealed function $Unbox<T>(Box) : T;

axiom (forall<T> x: T :: {:weight 3} { $Box(x) } $Unbox($Box(x)) == x);

revealed function $IsBox(Box, Ty) : bool;

revealed function $IsAllocBox(Box, Ty, Heap) : bool;

axiom (forall bx: Box :: 
  { $IsBox(bx, TInt) } 
  $IsBox(bx, TInt) ==> $Box($Unbox(bx): int) == bx && $Is($Unbox(bx): int, TInt));

axiom (forall bx: Box :: 
  { $IsBox(bx, TReal) } 
  $IsBox(bx, TReal)
     ==> $Box($Unbox(bx): real) == bx && $Is($Unbox(bx): real, TReal));

axiom (forall bx: Box :: 
  { $IsBox(bx, TBool) } 
  $IsBox(bx, TBool)
     ==> $Box($Unbox(bx): bool) == bx && $Is($Unbox(bx): bool, TBool));

axiom (forall bx: Box :: 
  { $IsBox(bx, TChar) } 
  $IsBox(bx, TChar)
     ==> $Box($Unbox(bx): char) == bx && $Is($Unbox(bx): char, TChar));

axiom (forall bx: Box :: 
  { $IsBox(bx, TBitvector(0)) } 
  $IsBox(bx, TBitvector(0))
     ==> $Box($Unbox(bx): Bv0) == bx && $Is($Unbox(bx): Bv0, TBitvector(0)));

axiom (forall bx: Box, t: Ty :: 
  { $IsBox(bx, TSet(t)) } 
  $IsBox(bx, TSet(t))
     ==> $Box($Unbox(bx): Set) == bx && $Is($Unbox(bx): Set, TSet(t)));

axiom (forall bx: Box, t: Ty :: 
  { $IsBox(bx, TISet(t)) } 
  $IsBox(bx, TISet(t))
     ==> $Box($Unbox(bx): ISet) == bx && $Is($Unbox(bx): ISet, TISet(t)));

axiom (forall bx: Box, t: Ty :: 
  { $IsBox(bx, TMultiSet(t)) } 
  $IsBox(bx, TMultiSet(t))
     ==> $Box($Unbox(bx): MultiSet) == bx && $Is($Unbox(bx): MultiSet, TMultiSet(t)));

axiom (forall bx: Box, t: Ty :: 
  { $IsBox(bx, TSeq(t)) } 
  $IsBox(bx, TSeq(t))
     ==> $Box($Unbox(bx): Seq) == bx && $Is($Unbox(bx): Seq, TSeq(t)));

axiom (forall bx: Box, s: Ty, t: Ty :: 
  { $IsBox(bx, TMap(s, t)) } 
  $IsBox(bx, TMap(s, t))
     ==> $Box($Unbox(bx): Map) == bx && $Is($Unbox(bx): Map, TMap(s, t)));

axiom (forall bx: Box, s: Ty, t: Ty :: 
  { $IsBox(bx, TIMap(s, t)) } 
  $IsBox(bx, TIMap(s, t))
     ==> $Box($Unbox(bx): IMap) == bx && $Is($Unbox(bx): IMap, TIMap(s, t)));

axiom (forall bx: Box :: 
  { $IsBox(bx, TORDINAL) } 
  $IsBox(bx, TORDINAL)
     ==> $Box($Unbox(bx): ORDINAL) == bx && $Is($Unbox(bx): ORDINAL, TORDINAL));

axiom (forall bx: Box :: 
  { $IsBox(bx, TField) } 
  $IsBox(bx, TField)
     ==> $Box($Unbox(bx): Field) == bx && $Is($Unbox(bx): Field, TField));

axiom (forall<T> v: T, t: Ty :: 
  { $IsBox($Box(v), t) } 
  $IsBox($Box(v), t) <==> $Is(v, t));

axiom (forall<T> v: T, t: Ty, h: Heap :: 
  { $IsAllocBox($Box(v), t, h) } 
  $IsAllocBox($Box(v), t, h) <==> $IsAlloc(v, t, h));

revealed function $Is<T>(T, Ty) : bool;

axiom (forall v: int :: { $Is(v, TInt) } $Is(v, TInt));

axiom (forall v: real :: { $Is(v, TReal) } $Is(v, TReal));

axiom (forall v: bool :: { $Is(v, TBool) } $Is(v, TBool));

axiom (forall ch: char :: 
  { $Is(ch, TChar) } 
  $Is(ch, TChar)
     <==> char#IsChar(char#ToInt(ch)) && char#FromInt(char#ToInt(ch)) == ch);

axiom (forall v: Field :: { $Is(v, TField) } $Is(v, TField));

axiom (forall v: ORDINAL :: { $Is(v, TORDINAL) } $Is(v, TORDINAL));

axiom (forall v: Bv0 :: { $Is(v, TBitvector(0)) } $Is(v, TBitvector(0)));

axiom (forall v: Set, t0: Ty :: 
  { $Is(v, TSet(t0)) } 
  $Is(v, TSet(t0))
     <==> (forall bx: Box :: 
      { Set#IsMember(v, bx) } 
      Set#IsMember(v, bx) ==> $IsBox(bx, t0)));

axiom (forall v: ISet, t0: Ty :: 
  { $Is(v, TISet(t0)) } 
  $Is(v, TISet(t0)) <==> (forall bx: Box :: { v[bx] } v[bx] ==> $IsBox(bx, t0)));

axiom (forall v: MultiSet, t0: Ty :: 
  { $Is(v, TMultiSet(t0)) } 
  $Is(v, TMultiSet(t0))
     <==> (forall bx: Box :: 
      { MultiSet#Multiplicity(v, bx) } 
      0 < MultiSet#Multiplicity(v, bx) ==> $IsBox(bx, t0)));

axiom (forall v: MultiSet, t0: Ty :: 
  { $Is(v, TMultiSet(t0)) } 
  $Is(v, TMultiSet(t0)) ==> $IsGoodMultiSet(v));

axiom (forall v: Seq, t0: Ty :: 
  { $Is(v, TSeq(t0)) } 
  $Is(v, TSeq(t0))
     <==> (forall i: int :: 
      { Seq#Index(v, i) } 
      0 <= i && i < Seq#Length(v) ==> $IsBox(Seq#Index(v, i), t0)));

axiom (forall v: Map, t0: Ty, t1: Ty :: 
  { $Is(v, TMap(t0, t1)) } 
  $Is(v, TMap(t0, t1))
     <==> (forall bx: Box :: 
      { Map#Elements(v)[bx] } { Set#IsMember(Map#Domain(v), bx) } 
      Set#IsMember(Map#Domain(v), bx)
         ==> $IsBox(Map#Elements(v)[bx], t1) && $IsBox(bx, t0)));

axiom (forall v: Map, t0: Ty, t1: Ty :: 
  { $Is(v, TMap(t0, t1)) } 
  $Is(v, TMap(t0, t1))
     ==> $Is(Map#Domain(v), TSet(t0))
       && $Is(Map#Values(v), TSet(t1))
       && $Is(Map#Items(v), TSet(Tclass._System.Tuple2(t0, t1))));

axiom (forall v: IMap, t0: Ty, t1: Ty :: 
  { $Is(v, TIMap(t0, t1)) } 
  $Is(v, TIMap(t0, t1))
     <==> (forall bx: Box :: 
      { IMap#Elements(v)[bx] } { IMap#Domain(v)[bx] } 
      IMap#Domain(v)[bx] ==> $IsBox(IMap#Elements(v)[bx], t1) && $IsBox(bx, t0)));

axiom (forall v: IMap, t0: Ty, t1: Ty :: 
  { $Is(v, TIMap(t0, t1)) } 
  $Is(v, TIMap(t0, t1))
     ==> $Is(IMap#Domain(v), TISet(t0))
       && $Is(IMap#Values(v), TISet(t1))
       && $Is(IMap#Items(v), TISet(Tclass._System.Tuple2(t0, t1))));

revealed function $IsAlloc<T>(T, Ty, Heap) : bool;

axiom (forall h: Heap, v: int :: { $IsAlloc(v, TInt, h) } $IsAlloc(v, TInt, h));

axiom (forall h: Heap, v: real :: { $IsAlloc(v, TReal, h) } $IsAlloc(v, TReal, h));

axiom (forall h: Heap, v: bool :: { $IsAlloc(v, TBool, h) } $IsAlloc(v, TBool, h));

axiom (forall h: Heap, v: char :: { $IsAlloc(v, TChar, h) } $IsAlloc(v, TChar, h));

axiom (forall h: Heap, v: ORDINAL :: 
  { $IsAlloc(v, TORDINAL, h) } 
  $IsAlloc(v, TORDINAL, h));

axiom (forall v: Bv0, h: Heap :: 
  { $IsAlloc(v, TBitvector(0), h) } 
  $IsAlloc(v, TBitvector(0), h));

axiom (forall v: Set, t0: Ty, h: Heap :: 
  { $IsAlloc(v, TSet(t0), h) } 
  $IsAlloc(v, TSet(t0), h)
     <==> (forall bx: Box :: 
      { Set#IsMember(v, bx) } 
      Set#IsMember(v, bx) ==> $IsAllocBox(bx, t0, h)));

axiom (forall v: ISet, t0: Ty, h: Heap :: 
  { $IsAlloc(v, TISet(t0), h) } 
  $IsAlloc(v, TISet(t0), h)
     <==> (forall bx: Box :: { v[bx] } v[bx] ==> $IsAllocBox(bx, t0, h)));

axiom (forall v: MultiSet, t0: Ty, h: Heap :: 
  { $IsAlloc(v, TMultiSet(t0), h) } 
  $IsAlloc(v, TMultiSet(t0), h)
     <==> (forall bx: Box :: 
      { MultiSet#Multiplicity(v, bx) } 
      0 < MultiSet#Multiplicity(v, bx) ==> $IsAllocBox(bx, t0, h)));

axiom (forall v: Seq, t0: Ty, h: Heap :: 
  { $IsAlloc(v, TSeq(t0), h) } 
  $IsAlloc(v, TSeq(t0), h)
     <==> (forall i: int :: 
      { Seq#Index(v, i) } 
      0 <= i && i < Seq#Length(v) ==> $IsAllocBox(Seq#Index(v, i), t0, h)));

axiom (forall v: Map, t0: Ty, t1: Ty, h: Heap :: 
  { $IsAlloc(v, TMap(t0, t1), h) } 
  $IsAlloc(v, TMap(t0, t1), h)
     <==> (forall bx: Box :: 
      { Map#Elements(v)[bx] } { Set#IsMember(Map#Domain(v), bx) } 
      Set#IsMember(Map#Domain(v), bx)
         ==> $IsAllocBox(Map#Elements(v)[bx], t1, h) && $IsAllocBox(bx, t0, h)));

axiom (forall v: IMap, t0: Ty, t1: Ty, h: Heap :: 
  { $IsAlloc(v, TIMap(t0, t1), h) } 
  $IsAlloc(v, TIMap(t0, t1), h)
     <==> (forall bx: Box :: 
      { IMap#Elements(v)[bx] } { IMap#Domain(v)[bx] } 
      IMap#Domain(v)[bx]
         ==> $IsAllocBox(IMap#Elements(v)[bx], t1, h) && $IsAllocBox(bx, t0, h)));

revealed function $AlwaysAllocated(Ty) : bool;

axiom (forall ty: Ty :: 
  { $AlwaysAllocated(ty) } 
  $AlwaysAllocated(ty)
     ==> (forall h: Heap, v: Box :: 
      { $IsAllocBox(v, ty, h) } 
      $IsBox(v, ty) ==> $IsAllocBox(v, ty, h)));

revealed function $OlderTag(Heap) : bool;

type ClassName;

const unique class._System.int: ClassName;

const unique class._System.bool: ClassName;

const unique class._System.set: ClassName;

const unique class._System.seq: ClassName;

const unique class._System.multiset: ClassName;

revealed function Tclass._System.object?() : Ty
uses {
// Tclass._System.object? Tag
axiom Tag(Tclass._System.object?()) == Tagclass._System.object?
   && TagFamily(Tclass._System.object?()) == tytagFamily$object;
}

revealed function Tclass._System.Tuple2(Ty, Ty) : Ty;

revealed function dtype(ref) : Ty;

revealed function TypeTuple(a: ClassName, b: ClassName) : ClassName;

revealed function TypeTupleCar(ClassName) : ClassName;

revealed function TypeTupleCdr(ClassName) : ClassName;

axiom (forall a: ClassName, b: ClassName :: 
  { TypeTuple(a, b) } 
  TypeTupleCar(TypeTuple(a, b)) == a && TypeTupleCdr(TypeTuple(a, b)) == b);

type HandleType;

revealed function Apply1(Ty, Ty, Heap, HandleType, Box) : Box;

type DatatypeType;

type DtCtorId;

revealed function DatatypeCtorId(DatatypeType) : DtCtorId;

revealed function DtRank(DatatypeType) : int;

revealed function BoxRank(Box) : int;

axiom (forall d: DatatypeType :: { BoxRank($Box(d)) } BoxRank($Box(d)) == DtRank(d));

type ORDINAL = Box;

revealed function ORD#IsNat(ORDINAL) : bool;

revealed function ORD#Offset(ORDINAL) : int;

axiom (forall o: ORDINAL :: { ORD#Offset(o) } 0 <= ORD#Offset(o));

revealed function {:inline} ORD#IsLimit(o: ORDINAL) : bool
{
  ORD#Offset(o) == 0
}

revealed function {:inline} ORD#IsSucc(o: ORDINAL) : bool
{
  0 < ORD#Offset(o)
}

revealed function ORD#FromNat(int) : ORDINAL;

axiom (forall n: int :: 
  { ORD#FromNat(n) } 
  0 <= n ==> ORD#IsNat(ORD#FromNat(n)) && ORD#Offset(ORD#FromNat(n)) == n);

axiom (forall o: ORDINAL :: 
  { ORD#Offset(o) } { ORD#IsNat(o) } 
  ORD#IsNat(o) ==> o == ORD#FromNat(ORD#Offset(o)));

revealed function ORD#Less(ORDINAL, ORDINAL) : bool;

axiom (forall o: ORDINAL, p: ORDINAL :: 
  { ORD#Less(o, p) } 
  (ORD#Less(o, p) ==> o != p)
     && (ORD#IsNat(o) && !ORD#IsNat(p) ==> ORD#Less(o, p))
     && (ORD#IsNat(o) && ORD#IsNat(p)
       ==> ORD#Less(o, p) == (ORD#Offset(o) < ORD#Offset(p)))
     && (ORD#Less(o, p) && ORD#IsNat(p) ==> ORD#IsNat(o)));

axiom (forall o: ORDINAL, p: ORDINAL :: 
  { ORD#Less(o, p), ORD#Less(p, o) } 
  ORD#Less(o, p) || o == p || ORD#Less(p, o));

axiom (forall o: ORDINAL, p: ORDINAL, r: ORDINAL :: 
  { ORD#Less(o, p), ORD#Less(p, r) } { ORD#Less(o, p), ORD#Less(o, r) } 
  ORD#Less(o, p) && ORD#Less(p, r) ==> ORD#Less(o, r));

revealed function ORD#LessThanLimit(ORDINAL, ORDINAL) : bool;

axiom (forall o: ORDINAL, p: ORDINAL :: 
  { ORD#LessThanLimit(o, p) } 
  ORD#LessThanLimit(o, p) == ORD#Less(o, p));

revealed function ORD#Plus(ORDINAL, ORDINAL) : ORDINAL;

axiom (forall o: ORDINAL, p: ORDINAL :: 
  { ORD#Plus(o, p) } 
  (ORD#IsNat(ORD#Plus(o, p)) ==> ORD#IsNat(o) && ORD#IsNat(p))
     && (ORD#IsNat(p)
       ==> ORD#IsNat(ORD#Plus(o, p)) == ORD#IsNat(o)
         && ORD#Offset(ORD#Plus(o, p)) == ORD#Offset(o) + ORD#Offset(p)));

axiom (forall o: ORDINAL, p: ORDINAL :: 
  { ORD#Plus(o, p) } 
  (o == ORD#Plus(o, p) || ORD#Less(o, ORD#Plus(o, p)))
     && (p == ORD#Plus(o, p) || ORD#Less(p, ORD#Plus(o, p))));

axiom (forall o: ORDINAL, p: ORDINAL :: 
  { ORD#Plus(o, p) } 
  (o == ORD#FromNat(0) ==> ORD#Plus(o, p) == p)
     && (p == ORD#FromNat(0) ==> ORD#Plus(o, p) == o));

revealed function ORD#Minus(ORDINAL, ORDINAL) : ORDINAL;

axiom (forall o: ORDINAL, p: ORDINAL :: 
  { ORD#Minus(o, p) } 
  ORD#IsNat(p) && ORD#Offset(p) <= ORD#Offset(o)
     ==> ORD#IsNat(ORD#Minus(o, p)) == ORD#IsNat(o)
       && ORD#Offset(ORD#Minus(o, p)) == ORD#Offset(o) - ORD#Offset(p));

axiom (forall o: ORDINAL, p: ORDINAL :: 
  { ORD#Minus(o, p) } 
  ORD#IsNat(p) && ORD#Offset(p) <= ORD#Offset(o)
     ==> (p == ORD#FromNat(0) && ORD#Minus(o, p) == o)
       || (p != ORD#FromNat(0) && ORD#Less(ORD#Minus(o, p), o)));

axiom (forall o: ORDINAL, m: int, n: int :: 
  { ORD#Plus(ORD#Plus(o, ORD#FromNat(m)), ORD#FromNat(n)) } 
  0 <= m && 0 <= n
     ==> ORD#Plus(ORD#Plus(o, ORD#FromNat(m)), ORD#FromNat(n))
       == ORD#Plus(o, ORD#FromNat(m + n)));

axiom (forall o: ORDINAL, m: int, n: int :: 
  { ORD#Minus(ORD#Minus(o, ORD#FromNat(m)), ORD#FromNat(n)) } 
  0 <= m && 0 <= n && m + n <= ORD#Offset(o)
     ==> ORD#Minus(ORD#Minus(o, ORD#FromNat(m)), ORD#FromNat(n))
       == ORD#Minus(o, ORD#FromNat(m + n)));

axiom (forall o: ORDINAL, m: int, n: int :: 
  { ORD#Minus(ORD#Plus(o, ORD#FromNat(m)), ORD#FromNat(n)) } 
  0 <= m && 0 <= n && n <= ORD#Offset(o) + m
     ==> (0 <= m - n
         ==> ORD#Minus(ORD#Plus(o, ORD#FromNat(m)), ORD#FromNat(n))
           == ORD#Plus(o, ORD#FromNat(m - n)))
       && (m - n <= 0
         ==> ORD#Minus(ORD#Plus(o, ORD#FromNat(m)), ORD#FromNat(n))
           == ORD#Minus(o, ORD#FromNat(n - m))));

axiom (forall o: ORDINAL, m: int, n: int :: 
  { ORD#Plus(ORD#Minus(o, ORD#FromNat(m)), ORD#FromNat(n)) } 
  0 <= m && 0 <= n && m <= ORD#Offset(o)
     ==> (0 <= m - n
         ==> ORD#Plus(ORD#Minus(o, ORD#FromNat(m)), ORD#FromNat(n))
           == ORD#Minus(o, ORD#FromNat(m - n)))
       && (m - n <= 0
         ==> ORD#Plus(ORD#Minus(o, ORD#FromNat(m)), ORD#FromNat(n))
           == ORD#Plus(o, ORD#FromNat(n - m))));

type LayerType;

const $LZ: LayerType;

revealed function $LS(LayerType) : LayerType;

revealed function AsFuelBottom(LayerType) : LayerType;

revealed function AtLayer<A>([LayerType]A, LayerType) : A;

axiom (forall<A> f: [LayerType]A, ly: LayerType :: 
  { AtLayer(f, ly) } 
  AtLayer(f, ly) == f[ly]);

axiom (forall<A> f: [LayerType]A, ly: LayerType :: 
  { AtLayer(f, $LS(ly)) } 
  AtLayer(f, $LS(ly)) == AtLayer(f, ly));

type Field;

revealed function FDim(Field) : int
uses {
axiom FDim(alloc) == 0;
}

revealed function IndexField(int) : Field;

axiom (forall i: int :: { IndexField(i) } FDim(IndexField(i)) == 1);

revealed function IndexField_Inverse(Field) : int;

axiom (forall i: int :: { IndexField(i) } IndexField_Inverse(IndexField(i)) == i);

revealed function MultiIndexField(Field, int) : Field;

axiom (forall f: Field, i: int :: 
  { MultiIndexField(f, i) } 
  FDim(MultiIndexField(f, i)) == FDim(f) + 1);

revealed function MultiIndexField_Inverse0(Field) : Field;

revealed function MultiIndexField_Inverse1(Field) : int;

axiom (forall f: Field, i: int :: 
  { MultiIndexField(f, i) } 
  MultiIndexField_Inverse0(MultiIndexField(f, i)) == f
     && MultiIndexField_Inverse1(MultiIndexField(f, i)) == i);

revealed function DeclType(Field) : ClassName;

type NameFamily;

revealed function DeclName(Field) : NameFamily
uses {
axiom DeclName(alloc) == allocName;
}

revealed function FieldOfDecl(ClassName, NameFamily) : Field;

axiom (forall cl: ClassName, nm: NameFamily :: 
  { FieldOfDecl(cl, nm): Field } 
  DeclType(FieldOfDecl(cl, nm): Field) == cl
     && DeclName(FieldOfDecl(cl, nm): Field) == nm);

revealed function $IsGhostField(Field) : bool
uses {
axiom $IsGhostField(alloc);
}

axiom (forall h: Heap, k: Heap :: 
  { $HeapSuccGhost(h, k) } 
  $HeapSuccGhost(h, k)
     ==> $HeapSucc(h, k)
       && (forall o: ref, f: Field :: 
        { read(k, o, f) } 
        !$IsGhostField(f) ==> read(h, o, f) == read(k, o, f)));

axiom (forall<T> h: Heap, k: Heap, v: T, t: Ty :: 
  { $HeapSucc(h, k), $IsAlloc(v, t, h) } 
  $HeapSucc(h, k) ==> $IsAlloc(v, t, h) ==> $IsAlloc(v, t, k));

axiom (forall h: Heap, k: Heap, bx: Box, t: Ty :: 
  { $HeapSucc(h, k), $IsAllocBox(bx, t, h) } 
  $HeapSucc(h, k) ==> $IsAllocBox(bx, t, h) ==> $IsAllocBox(bx, t, k));

const unique alloc: Field;

const unique allocName: NameFamily;

revealed function _System.array.Length(a: ref) : int;

axiom (forall o: ref :: { _System.array.Length(o) } 0 <= _System.array.Length(o));

revealed function Int(x: real) : int
uses {
axiom (forall x: real :: { Int(x): int } Int(x): int == int(x));
}

revealed function Real(x: int) : real
uses {
axiom (forall x: int :: { Real(x): real } Real(x): real == real(x));
}

axiom (forall i: int :: { Int(Real(i)) } Int(Real(i)) == i);

revealed function {:inline} _System.real.Floor(x: real) : int
{
  Int(x)
}

type Heap = [ref][Field]Box;

revealed function {:inline} read(H: Heap, r: ref, f: Field) : Box
{
  H[r][f]
}

revealed function {:inline} update(H: Heap, r: ref, f: Field, v: Box) : Heap
{
  H[r := H[r][f := v]]
}

revealed function $IsGoodHeap(Heap) : bool;

revealed function $IsHeapAnchor(Heap) : bool;

var $Heap: Heap where $IsGoodHeap($Heap) && $IsHeapAnchor($Heap);

const $OneHeap: Heap
uses {
axiom $IsGoodHeap($OneHeap);
}

revealed function $HeapSucc(Heap, Heap) : bool;

axiom (forall h: Heap, r: ref, f: Field, x: Box :: 
  { update(h, r, f, x) } 
  f != alloc && $IsGoodHeap(update(h, r, f, x))
     ==> $HeapSucc(h, update(h, r, f, x)));

axiom (forall h: Heap, r: ref :: 
  { update(h, r, alloc, $Box(true)) } 
  $IsGoodHeap(update(h, r, alloc, $Box(true)))
     ==> $HeapSucc(h, update(h, r, alloc, $Box(true))));

axiom (forall a: Heap, b: Heap, c: Heap :: 
  { $HeapSucc(a, b), $HeapSucc(b, c) } 
  a != c ==> $HeapSucc(a, b) && $HeapSucc(b, c) ==> $HeapSucc(a, c));

axiom (forall h: Heap, k: Heap :: 
  { $HeapSucc(h, k) } 
  $HeapSucc(h, k)
     ==> (forall o: ref :: 
      { read(k, o, alloc) } 
      $Unbox(read(h, o, alloc)) ==> $Unbox(read(k, o, alloc))));

revealed function $HeapSuccGhost(Heap, Heap) : bool;

procedure $YieldHavoc(this: ref, rds: Set, nw: Set);
  modifies $Heap;
  ensures (forall $o: ref, $f: Field :: 
    { read($Heap, $o, $f) } 
    $o != null && $Unbox(read(old($Heap), $o, alloc))
       ==> 
      $o == this || Set#IsMember(rds, $Box($o)) || Set#IsMember(nw, $Box($o))
       ==> read($Heap, $o, $f) == read(old($Heap), $o, $f));
  ensures $HeapSucc(old($Heap), $Heap);



procedure $IterHavoc0(this: ref, rds: Set, modi: Set);
  modifies $Heap;
  ensures (forall $o: ref, $f: Field :: 
    { read($Heap, $o, $f) } 
    $o != null && $Unbox(read(old($Heap), $o, alloc))
       ==> 
      Set#IsMember(rds, $Box($o)) && !Set#IsMember(modi, $Box($o)) && $o != this
       ==> read($Heap, $o, $f) == read(old($Heap), $o, $f));
  ensures $HeapSucc(old($Heap), $Heap);



procedure $IterHavoc1(this: ref, modi: Set, nw: Set);
  modifies $Heap;
  ensures (forall $o: ref, $f: Field :: 
    { read($Heap, $o, $f) } 
    $o != null && $Unbox(read(old($Heap), $o, alloc))
       ==> read($Heap, $o, $f) == read(old($Heap), $o, $f)
         || $o == this
         || Set#IsMember(modi, $Box($o))
         || Set#IsMember(nw, $Box($o)));
  ensures $HeapSucc(old($Heap), $Heap);



procedure $IterCollectNewObjects(prevHeap: Heap, newHeap: Heap, this: ref, NW: Field) returns (s: Set);
  ensures (forall bx: Box :: 
    { Set#IsMember(s, bx) } 
    Set#IsMember(s, bx)
       <==> Set#IsMember($Unbox(read(newHeap, this, NW)): Set, bx)
         || (
          $Unbox(bx) != null
           && !$Unbox(read(prevHeap, $Unbox(bx): ref, alloc))
           && $Unbox(read(newHeap, $Unbox(bx): ref, alloc))));



type Set;

revealed function Set#Card(s: Set) : int;

axiom (forall s: Set :: { Set#Card(s) } 0 <= Set#Card(s));

revealed function Set#Empty() : Set;

revealed function Set#IsMember(s: Set, o: Box) : bool;

axiom (forall o: Box :: 
  { Set#IsMember(Set#Empty(), o) } 
  !Set#IsMember(Set#Empty(), o));

axiom (forall s: Set :: 
  { Set#Card(s) } 
  (Set#Card(s) == 0 <==> s == Set#Empty())
     && (Set#Card(s) != 0
       ==> (exists x: Box :: { Set#IsMember(s, x) } Set#IsMember(s, x))));

revealed function Set#UnionOne(s: Set, o: Box) : Set;

axiom (forall a: Set, x: Box, o: Box :: 
  { Set#IsMember(Set#UnionOne(a, x), o) } 
  Set#IsMember(Set#UnionOne(a, x), o) <==> o == x || Set#IsMember(a, o));

axiom (forall a: Set, x: Box :: 
  { Set#UnionOne(a, x) } 
  Set#IsMember(Set#UnionOne(a, x), x));

axiom (forall a: Set, x: Box, y: Box :: 
  { Set#UnionOne(a, x), Set#IsMember(a, y) } 
  Set#IsMember(a, y) ==> Set#IsMember(Set#UnionOne(a, x), y));

axiom (forall a: Set, x: Box :: 
  { Set#Card(Set#UnionOne(a, x)) } 
  Set#IsMember(a, x) ==> Set#Card(Set#UnionOne(a, x)) == Set#Card(a));

axiom (forall a: Set, x: Box :: 
  { Set#Card(Set#UnionOne(a, x)) } 
  !Set#IsMember(a, x) ==> Set#Card(Set#UnionOne(a, x)) == Set#Card(a) + 1);

revealed function Set#Union(a: Set, b: Set) : Set;

axiom (forall a: Set, b: Set, o: Box :: 
  { Set#IsMember(Set#Union(a, b), o) } 
  Set#IsMember(Set#Union(a, b), o) <==> Set#IsMember(a, o) || Set#IsMember(b, o));

axiom (forall a: Set, b: Set, y: Box :: 
  { Set#Union(a, b), Set#IsMember(a, y) } 
  Set#IsMember(a, y) ==> Set#IsMember(Set#Union(a, b), y));

axiom (forall a: Set, b: Set, y: Box :: 
  { Set#Union(a, b), Set#IsMember(b, y) } 
  Set#IsMember(b, y) ==> Set#IsMember(Set#Union(a, b), y));

axiom (forall a: Set, b: Set :: 
  { Set#Union(a, b) } 
  Set#Disjoint(a, b)
     ==> Set#Difference(Set#Union(a, b), a) == b
       && Set#Difference(Set#Union(a, b), b) == a);

revealed function Set#Intersection(a: Set, b: Set) : Set;

axiom (forall a: Set, b: Set, o: Box :: 
  { Set#IsMember(Set#Intersection(a, b), o) } 
  Set#IsMember(Set#Intersection(a, b), o)
     <==> Set#IsMember(a, o) && Set#IsMember(b, o));

axiom (forall a: Set, b: Set :: 
  { Set#Union(Set#Union(a, b), b) } 
  Set#Union(Set#Union(a, b), b) == Set#Union(a, b));

axiom (forall a: Set, b: Set :: 
  { Set#Union(a, Set#Union(a, b)) } 
  Set#Union(a, Set#Union(a, b)) == Set#Union(a, b));

axiom (forall a: Set, b: Set :: 
  { Set#Intersection(Set#Intersection(a, b), b) } 
  Set#Intersection(Set#Intersection(a, b), b) == Set#Intersection(a, b));

axiom (forall a: Set, b: Set :: 
  { Set#Intersection(a, Set#Intersection(a, b)) } 
  Set#Intersection(a, Set#Intersection(a, b)) == Set#Intersection(a, b));

axiom (forall a: Set, b: Set :: 
  { Set#Card(Set#Union(a, b)) } { Set#Card(Set#Intersection(a, b)) } 
  Set#Card(Set#Union(a, b)) + Set#Card(Set#Intersection(a, b))
     == Set#Card(a) + Set#Card(b));

revealed function Set#Difference(a: Set, b: Set) : Set;

axiom (forall a: Set, b: Set, o: Box :: 
  { Set#IsMember(Set#Difference(a, b), o) } 
  Set#IsMember(Set#Difference(a, b), o)
     <==> Set#IsMember(a, o) && !Set#IsMember(b, o));

axiom (forall a: Set, b: Set, y: Box :: 
  { Set#Difference(a, b), Set#IsMember(b, y) } 
  Set#IsMember(b, y) ==> !Set#IsMember(Set#Difference(a, b), y));

axiom (forall a: Set, b: Set :: 
  { Set#Card(Set#Difference(a, b)) } 
  Set#Card(Set#Difference(a, b))
         + Set#Card(Set#Difference(b, a))
         + Set#Card(Set#Intersection(a, b))
       == Set#Card(Set#Union(a, b))
     && Set#Card(Set#Difference(a, b)) == Set#Card(a) - Set#Card(Set#Intersection(a, b)));

revealed function Set#Subset(a: Set, b: Set) : bool;

axiom (forall a: Set, b: Set :: 
  { Set#Subset(a, b) } 
  Set#Subset(a, b)
     <==> (forall o: Box :: 
      { Set#IsMember(a, o) } { Set#IsMember(b, o) } 
      Set#IsMember(a, o) ==> Set#IsMember(b, o)));

revealed function Set#Equal(a: Set, b: Set) : bool;

axiom (forall a: Set, b: Set :: 
  { Set#Equal(a, b) } 
  Set#Equal(a, b)
     <==> (forall o: Box :: 
      { Set#IsMember(a, o) } { Set#IsMember(b, o) } 
      Set#IsMember(a, o) <==> Set#IsMember(b, o)));

axiom (forall a: Set, b: Set :: { Set#Equal(a, b) } Set#Equal(a, b) ==> a == b);

revealed function Set#Disjoint(a: Set, b: Set) : bool;

axiom (forall a: Set, b: Set :: 
  { Set#Disjoint(a, b) } 
  Set#Disjoint(a, b)
     <==> (forall o: Box :: 
      { Set#IsMember(a, o) } { Set#IsMember(b, o) } 
      !Set#IsMember(a, o) || !Set#IsMember(b, o)));

revealed function Set#FromBoogieMap([Box]bool) : Set;

type ISet = [Box]bool;

revealed function ISet#Empty() : ISet;

axiom (forall o: Box :: { ISet#Empty()[o] } !ISet#Empty()[o]);

revealed function ISet#FromSet(Set) : ISet;

axiom (forall s: Set, bx: Box :: 
  { ISet#FromSet(s)[bx] } 
  ISet#FromSet(s)[bx] == Set#IsMember(s, bx));

revealed function ISet#UnionOne(ISet, Box) : ISet;

axiom (forall a: ISet, x: Box, o: Box :: 
  { ISet#UnionOne(a, x)[o] } 
  ISet#UnionOne(a, x)[o] <==> o == x || a[o]);

axiom (forall a: ISet, x: Box :: { ISet#UnionOne(a, x) } ISet#UnionOne(a, x)[x]);

axiom (forall a: ISet, x: Box, y: Box :: 
  { ISet#UnionOne(a, x), a[y] } 
  a[y] ==> ISet#UnionOne(a, x)[y]);

revealed function ISet#Union(ISet, ISet) : ISet;

axiom (forall a: ISet, b: ISet, o: Box :: 
  { ISet#Union(a, b)[o] } 
  ISet#Union(a, b)[o] <==> a[o] || b[o]);

axiom (forall a: ISet, b: ISet, y: Box :: 
  { ISet#Union(a, b), a[y] } 
  a[y] ==> ISet#Union(a, b)[y]);

axiom (forall a: ISet, b: ISet, y: Box :: 
  { ISet#Union(a, b), b[y] } 
  b[y] ==> ISet#Union(a, b)[y]);

axiom (forall a: ISet, b: ISet :: 
  { ISet#Union(a, b) } 
  ISet#Disjoint(a, b)
     ==> ISet#Difference(ISet#Union(a, b), a) == b
       && ISet#Difference(ISet#Union(a, b), b) == a);

revealed function ISet#Intersection(ISet, ISet) : ISet;

axiom (forall a: ISet, b: ISet, o: Box :: 
  { ISet#Intersection(a, b)[o] } 
  ISet#Intersection(a, b)[o] <==> a[o] && b[o]);

axiom (forall a: ISet, b: ISet :: 
  { ISet#Union(ISet#Union(a, b), b) } 
  ISet#Union(ISet#Union(a, b), b) == ISet#Union(a, b));

axiom (forall a: ISet, b: ISet :: 
  { ISet#Union(a, ISet#Union(a, b)) } 
  ISet#Union(a, ISet#Union(a, b)) == ISet#Union(a, b));

axiom (forall a: ISet, b: ISet :: 
  { ISet#Intersection(ISet#Intersection(a, b), b) } 
  ISet#Intersection(ISet#Intersection(a, b), b) == ISet#Intersection(a, b));

axiom (forall a: ISet, b: ISet :: 
  { ISet#Intersection(a, ISet#Intersection(a, b)) } 
  ISet#Intersection(a, ISet#Intersection(a, b)) == ISet#Intersection(a, b));

revealed function ISet#Difference(ISet, ISet) : ISet;

axiom (forall a: ISet, b: ISet, o: Box :: 
  { ISet#Difference(a, b)[o] } 
  ISet#Difference(a, b)[o] <==> a[o] && !b[o]);

axiom (forall a: ISet, b: ISet, y: Box :: 
  { ISet#Difference(a, b), b[y] } 
  b[y] ==> !ISet#Difference(a, b)[y]);

revealed function ISet#Subset(ISet, ISet) : bool;

axiom (forall a: ISet, b: ISet :: 
  { ISet#Subset(a, b) } 
  ISet#Subset(a, b) <==> (forall o: Box :: { a[o] } { b[o] } a[o] ==> b[o]));

revealed function ISet#Equal(ISet, ISet) : bool;

axiom (forall a: ISet, b: ISet :: 
  { ISet#Equal(a, b) } 
  ISet#Equal(a, b) <==> (forall o: Box :: { a[o] } { b[o] } a[o] <==> b[o]));

axiom (forall a: ISet, b: ISet :: { ISet#Equal(a, b) } ISet#Equal(a, b) ==> a == b);

revealed function ISet#Disjoint(ISet, ISet) : bool;

axiom (forall a: ISet, b: ISet :: 
  { ISet#Disjoint(a, b) } 
  ISet#Disjoint(a, b) <==> (forall o: Box :: { a[o] } { b[o] } !a[o] || !b[o]));

revealed function Math#min(a: int, b: int) : int;

axiom (forall a: int, b: int :: { Math#min(a, b) } a <= b <==> Math#min(a, b) == a);

axiom (forall a: int, b: int :: { Math#min(a, b) } b <= a <==> Math#min(a, b) == b);

axiom (forall a: int, b: int :: 
  { Math#min(a, b) } 
  Math#min(a, b) == a || Math#min(a, b) == b);

revealed function Math#clip(a: int) : int;

axiom (forall a: int :: { Math#clip(a) } 0 <= a ==> Math#clip(a) == a);

axiom (forall a: int :: { Math#clip(a) } a < 0 ==> Math#clip(a) == 0);

type MultiSet;

revealed function MultiSet#Multiplicity(m: MultiSet, o: Box) : int;

revealed function MultiSet#UpdateMultiplicity(m: MultiSet, o: Box, n: int) : MultiSet;

axiom (forall m: MultiSet, o: Box, n: int, p: Box :: 
  { MultiSet#Multiplicity(MultiSet#UpdateMultiplicity(m, o, n), p) } 
  0 <= n
     ==> (o == p ==> MultiSet#Multiplicity(MultiSet#UpdateMultiplicity(m, o, n), p) == n)
       && (o != p
         ==> MultiSet#Multiplicity(MultiSet#UpdateMultiplicity(m, o, n), p)
           == MultiSet#Multiplicity(m, p)));

revealed function $IsGoodMultiSet(ms: MultiSet) : bool;

axiom (forall ms: MultiSet :: 
  { $IsGoodMultiSet(ms) } 
  $IsGoodMultiSet(ms)
     <==> (forall bx: Box :: 
      { MultiSet#Multiplicity(ms, bx) } 
      0 <= MultiSet#Multiplicity(ms, bx)
         && MultiSet#Multiplicity(ms, bx) <= MultiSet#Card(ms)));

revealed function MultiSet#Card(m: MultiSet) : int;

axiom (forall s: MultiSet :: { MultiSet#Card(s) } 0 <= MultiSet#Card(s));

axiom (forall s: MultiSet, x: Box, n: int :: 
  { MultiSet#Card(MultiSet#UpdateMultiplicity(s, x, n)) } 
  0 <= n
     ==> MultiSet#Card(MultiSet#UpdateMultiplicity(s, x, n))
       == MultiSet#Card(s) - MultiSet#Multiplicity(s, x) + n);

revealed function MultiSet#Empty() : MultiSet;

axiom (forall o: Box :: 
  { MultiSet#Multiplicity(MultiSet#Empty(), o) } 
  MultiSet#Multiplicity(MultiSet#Empty(), o) == 0);

axiom (forall s: MultiSet :: 
  { MultiSet#Card(s) } 
  (MultiSet#Card(s) == 0 <==> s == MultiSet#Empty())
     && (MultiSet#Card(s) != 0
       ==> (exists x: Box :: 
        { MultiSet#Multiplicity(s, x) } 
        0 < MultiSet#Multiplicity(s, x))));

revealed function MultiSet#Singleton(o: Box) : MultiSet;

axiom (forall r: Box, o: Box :: 
  { MultiSet#Multiplicity(MultiSet#Singleton(r), o) } 
  (MultiSet#Multiplicity(MultiSet#Singleton(r), o) == 1 <==> r == o)
     && (MultiSet#Multiplicity(MultiSet#Singleton(r), o) == 0 <==> r != o));

axiom (forall r: Box :: 
  { MultiSet#Singleton(r) } 
  MultiSet#Singleton(r) == MultiSet#UnionOne(MultiSet#Empty(), r));

revealed function MultiSet#UnionOne(m: MultiSet, o: Box) : MultiSet;

axiom (forall a: MultiSet, x: Box, o: Box :: 
  { MultiSet#Multiplicity(MultiSet#UnionOne(a, x), o) } 
  0 < MultiSet#Multiplicity(MultiSet#UnionOne(a, x), o)
     <==> o == x || 0 < MultiSet#Multiplicity(a, o));

axiom (forall a: MultiSet, x: Box :: 
  { MultiSet#UnionOne(a, x) } 
  MultiSet#Multiplicity(MultiSet#UnionOne(a, x), x)
     == MultiSet#Multiplicity(a, x) + 1);

axiom (forall a: MultiSet, x: Box, y: Box :: 
  { MultiSet#UnionOne(a, x), MultiSet#Multiplicity(a, y) } 
  0 < MultiSet#Multiplicity(a, y)
     ==> 0 < MultiSet#Multiplicity(MultiSet#UnionOne(a, x), y));

axiom (forall a: MultiSet, x: Box, y: Box :: 
  { MultiSet#UnionOne(a, x), MultiSet#Multiplicity(a, y) } 
  x != y
     ==> MultiSet#Multiplicity(a, y) == MultiSet#Multiplicity(MultiSet#UnionOne(a, x), y));

axiom (forall a: MultiSet, x: Box :: 
  { MultiSet#Card(MultiSet#UnionOne(a, x)) } 
  MultiSet#Card(MultiSet#UnionOne(a, x)) == MultiSet#Card(a) + 1);

revealed function MultiSet#Union(a: MultiSet, b: MultiSet) : MultiSet;

axiom (forall a: MultiSet, b: MultiSet, o: Box :: 
  { MultiSet#Multiplicity(MultiSet#Union(a, b), o) } 
  MultiSet#Multiplicity(MultiSet#Union(a, b), o)
     == MultiSet#Multiplicity(a, o) + MultiSet#Multiplicity(b, o));

axiom (forall a: MultiSet, b: MultiSet :: 
  { MultiSet#Card(MultiSet#Union(a, b)) } 
  MultiSet#Card(MultiSet#Union(a, b)) == MultiSet#Card(a) + MultiSet#Card(b));

revealed function MultiSet#Intersection(a: MultiSet, b: MultiSet) : MultiSet;

axiom (forall a: MultiSet, b: MultiSet, o: Box :: 
  { MultiSet#Multiplicity(MultiSet#Intersection(a, b), o) } 
  MultiSet#Multiplicity(MultiSet#Intersection(a, b), o)
     == Math#min(MultiSet#Multiplicity(a, o), MultiSet#Multiplicity(b, o)));

axiom (forall a: MultiSet, b: MultiSet :: 
  { MultiSet#Intersection(MultiSet#Intersection(a, b), b) } 
  MultiSet#Intersection(MultiSet#Intersection(a, b), b)
     == MultiSet#Intersection(a, b));

axiom (forall a: MultiSet, b: MultiSet :: 
  { MultiSet#Intersection(a, MultiSet#Intersection(a, b)) } 
  MultiSet#Intersection(a, MultiSet#Intersection(a, b))
     == MultiSet#Intersection(a, b));

revealed function MultiSet#Difference(a: MultiSet, b: MultiSet) : MultiSet;

axiom (forall a: MultiSet, b: MultiSet, o: Box :: 
  { MultiSet#Multiplicity(MultiSet#Difference(a, b), o) } 
  MultiSet#Multiplicity(MultiSet#Difference(a, b), o)
     == Math#clip(MultiSet#Multiplicity(a, o) - MultiSet#Multiplicity(b, o)));

axiom (forall a: MultiSet, b: MultiSet, y: Box :: 
  { MultiSet#Difference(a, b), MultiSet#Multiplicity(b, y), MultiSet#Multiplicity(a, y) } 
  MultiSet#Multiplicity(a, y) <= MultiSet#Multiplicity(b, y)
     ==> MultiSet#Multiplicity(MultiSet#Difference(a, b), y) == 0);

axiom (forall a: MultiSet, b: MultiSet :: 
  { MultiSet#Card(MultiSet#Difference(a, b)) } 
  MultiSet#Card(MultiSet#Difference(a, b))
         + MultiSet#Card(MultiSet#Difference(b, a))
         + 2 * MultiSet#Card(MultiSet#Intersection(a, b))
       == MultiSet#Card(MultiSet#Union(a, b))
     && MultiSet#Card(MultiSet#Difference(a, b))
       == MultiSet#Card(a) - MultiSet#Card(MultiSet#Intersection(a, b)));

revealed function MultiSet#Subset(a: MultiSet, b: MultiSet) : bool;

axiom (forall a: MultiSet, b: MultiSet :: 
  { MultiSet#Subset(a, b) } 
  MultiSet#Subset(a, b)
     <==> (forall o: Box :: 
      { MultiSet#Multiplicity(a, o) } { MultiSet#Multiplicity(b, o) } 
      MultiSet#Multiplicity(a, o) <= MultiSet#Multiplicity(b, o)));

revealed function MultiSet#Equal(a: MultiSet, b: MultiSet) : bool;

axiom (forall a: MultiSet, b: MultiSet :: 
  { MultiSet#Equal(a, b) } 
  MultiSet#Equal(a, b)
     <==> (forall o: Box :: 
      { MultiSet#Multiplicity(a, o) } { MultiSet#Multiplicity(b, o) } 
      MultiSet#Multiplicity(a, o) == MultiSet#Multiplicity(b, o)));

axiom (forall a: MultiSet, b: MultiSet :: 
  { MultiSet#Equal(a, b) } 
  MultiSet#Equal(a, b) ==> a == b);

revealed function MultiSet#Disjoint(a: MultiSet, b: MultiSet) : bool;

axiom (forall a: MultiSet, b: MultiSet :: 
  { MultiSet#Disjoint(a, b) } 
  MultiSet#Disjoint(a, b)
     <==> (forall o: Box :: 
      { MultiSet#Multiplicity(a, o) } { MultiSet#Multiplicity(b, o) } 
      MultiSet#Multiplicity(a, o) == 0 || MultiSet#Multiplicity(b, o) == 0));

revealed function MultiSet#FromSet(s: Set) : MultiSet;

axiom (forall s: Set, a: Box :: 
  { MultiSet#Multiplicity(MultiSet#FromSet(s), a) } 
  (MultiSet#Multiplicity(MultiSet#FromSet(s), a) == 0 <==> !Set#IsMember(s, a))
     && (MultiSet#Multiplicity(MultiSet#FromSet(s), a) == 1 <==> Set#IsMember(s, a)));

axiom (forall s: Set :: 
  { MultiSet#Card(MultiSet#FromSet(s)) } 
  MultiSet#Card(MultiSet#FromSet(s)) == Set#Card(s));

revealed function MultiSet#FromSeq(s: Seq) : MultiSet
uses {
axiom MultiSet#FromSeq(Seq#Empty()) == MultiSet#Empty();
}

axiom (forall s: Seq :: { MultiSet#FromSeq(s) } $IsGoodMultiSet(MultiSet#FromSeq(s)));

axiom (forall s: Seq :: 
  { MultiSet#Card(MultiSet#FromSeq(s)) } 
  MultiSet#Card(MultiSet#FromSeq(s)) == Seq#Length(s));

axiom (forall s: Seq, v: Box :: 
  { MultiSet#FromSeq(Seq#Build(s, v)) } 
  MultiSet#FromSeq(Seq#Build(s, v)) == MultiSet#UnionOne(MultiSet#FromSeq(s), v));

axiom (forall a: Seq, b: Seq :: 
  { MultiSet#FromSeq(Seq#Append(a, b)) } 
  MultiSet#FromSeq(Seq#Append(a, b))
     == MultiSet#Union(MultiSet#FromSeq(a), MultiSet#FromSeq(b)));

axiom (forall s: Seq, i: int, v: Box, x: Box :: 
  { MultiSet#Multiplicity(MultiSet#FromSeq(Seq#Update(s, i, v)), x) } 
  0 <= i && i < Seq#Length(s)
     ==> MultiSet#Multiplicity(MultiSet#FromSeq(Seq#Update(s, i, v)), x)
       == MultiSet#Multiplicity(MultiSet#Union(MultiSet#Difference(MultiSet#FromSeq(s), MultiSet#Singleton(Seq#Index(s, i))), 
          MultiSet#Singleton(v)), 
        x));

axiom (forall s: Seq, x: Box :: 
  { MultiSet#Multiplicity(MultiSet#FromSeq(s), x) } 
  (exists i: int :: 
      { Seq#Index(s, i) } 
      0 <= i && i < Seq#Length(s) && x == Seq#Index(s, i))
     <==> 0 < MultiSet#Multiplicity(MultiSet#FromSeq(s), x));

type Seq;

revealed function Seq#Length(s: Seq) : int;

axiom (forall s: Seq :: { Seq#Length(s) } 0 <= Seq#Length(s));

revealed function Seq#Empty() : Seq
uses {
axiom Seq#Length(Seq#Empty()) == 0;
}

axiom (forall s: Seq :: { Seq#Length(s) } Seq#Length(s) == 0 ==> s == Seq#Empty());

revealed function Seq#Build(s: Seq, val: Box) : Seq;

revealed function Seq#Build_inv0(s: Seq) : Seq;

revealed function Seq#Build_inv1(s: Seq) : Box;

axiom (forall s: Seq, val: Box :: 
  { Seq#Build(s, val) } 
  Seq#Build_inv0(Seq#Build(s, val)) == s
     && Seq#Build_inv1(Seq#Build(s, val)) == val);

axiom (forall s: Seq, v: Box :: 
  { Seq#Build(s, v) } 
  Seq#Length(Seq#Build(s, v)) == 1 + Seq#Length(s));

axiom (forall s: Seq, i: int, v: Box :: 
  { Seq#Index(Seq#Build(s, v), i) } 
  (i == Seq#Length(s) ==> Seq#Index(Seq#Build(s, v), i) == v)
     && (i != Seq#Length(s) ==> Seq#Index(Seq#Build(s, v), i) == Seq#Index(s, i)));

axiom (forall s0: Seq, s1: Seq :: 
  { Seq#Length(Seq#Append(s0, s1)) } 
  Seq#Length(Seq#Append(s0, s1)) == Seq#Length(s0) + Seq#Length(s1));

revealed function Seq#Index(s: Seq, i: int) : Box;

axiom (forall s0: Seq, s1: Seq, n: int :: 
  { Seq#Index(Seq#Append(s0, s1), n) } 
  (n < Seq#Length(s0) ==> Seq#Index(Seq#Append(s0, s1), n) == Seq#Index(s0, n))
     && (Seq#Length(s0) <= n
       ==> Seq#Index(Seq#Append(s0, s1), n) == Seq#Index(s1, n - Seq#Length(s0))));

revealed function Seq#Update(s: Seq, i: int, val: Box) : Seq;

axiom (forall s: Seq, i: int, v: Box :: 
  { Seq#Length(Seq#Update(s, i, v)) } 
  0 <= i && i < Seq#Length(s) ==> Seq#Length(Seq#Update(s, i, v)) == Seq#Length(s));

axiom (forall s: Seq, i: int, v: Box, n: int :: 
  { Seq#Index(Seq#Update(s, i, v), n) } 
  0 <= n && n < Seq#Length(s)
     ==> (i == n ==> Seq#Index(Seq#Update(s, i, v), n) == v)
       && (i != n ==> Seq#Index(Seq#Update(s, i, v), n) == Seq#Index(s, n)));

revealed function Seq#Append(s0: Seq, s1: Seq) : Seq;

revealed function Seq#Contains(s: Seq, val: Box) : bool;

axiom (forall s: Seq, x: Box :: 
  { Seq#Contains(s, x) } 
  Seq#Contains(s, x)
     <==> (exists i: int :: 
      { Seq#Index(s, i) } 
      0 <= i && i < Seq#Length(s) && Seq#Index(s, i) == x));

axiom (forall x: Box :: 
  { Seq#Contains(Seq#Empty(), x) } 
  !Seq#Contains(Seq#Empty(), x));

axiom (forall s0: Seq, s1: Seq, x: Box :: 
  { Seq#Contains(Seq#Append(s0, s1), x) } 
  Seq#Contains(Seq#Append(s0, s1), x)
     <==> Seq#Contains(s0, x) || Seq#Contains(s1, x));

axiom (forall s: Seq, v: Box, x: Box :: 
  { Seq#Contains(Seq#Build(s, v), x) } 
  Seq#Contains(Seq#Build(s, v), x) <==> v == x || Seq#Contains(s, x));

axiom (forall s: Seq, n: int, x: Box :: 
  { Seq#Contains(Seq#Take(s, n), x) } 
  Seq#Contains(Seq#Take(s, n), x)
     <==> (exists i: int :: 
      { Seq#Index(s, i) } 
      0 <= i && i < n && i < Seq#Length(s) && Seq#Index(s, i) == x));

axiom (forall s: Seq, n: int, x: Box :: 
  { Seq#Contains(Seq#Drop(s, n), x) } 
  Seq#Contains(Seq#Drop(s, n), x)
     <==> (exists i: int :: 
      { Seq#Index(s, i) } 
      0 <= n && n <= i && i < Seq#Length(s) && Seq#Index(s, i) == x));

revealed function Seq#Equal(s0: Seq, s1: Seq) : bool;

axiom (forall s0: Seq, s1: Seq :: 
  { Seq#Equal(s0, s1) } 
  Seq#Equal(s0, s1)
     <==> Seq#Length(s0) == Seq#Length(s1)
       && (forall j: int :: 
        { Seq#Index(s0, j) } { Seq#Index(s1, j) } 
        0 <= j && j < Seq#Length(s0) ==> Seq#Index(s0, j) == Seq#Index(s1, j)));

axiom (forall a: Seq, b: Seq :: { Seq#Equal(a, b) } Seq#Equal(a, b) ==> a == b);

revealed function Seq#SameUntil(s0: Seq, s1: Seq, n: int) : bool;

axiom (forall s0: Seq, s1: Seq, n: int :: 
  { Seq#SameUntil(s0, s1, n) } 
  Seq#SameUntil(s0, s1, n)
     <==> (forall j: int :: 
      { Seq#Index(s0, j) } { Seq#Index(s1, j) } 
      0 <= j && j < n ==> Seq#Index(s0, j) == Seq#Index(s1, j)));

revealed function Seq#Take(s: Seq, howMany: int) : Seq;

axiom (forall s: Seq, n: int :: 
  { Seq#Length(Seq#Take(s, n)) } 
  0 <= n && n <= Seq#Length(s) ==> Seq#Length(Seq#Take(s, n)) == n);

axiom (forall s: Seq, n: int, j: int :: 
  {:weight 11} { Seq#Index(Seq#Take(s, n), j) } { Seq#Index(s, j), Seq#Take(s, n) } 
  0 <= j && j < n && j < Seq#Length(s)
     ==> Seq#Index(Seq#Take(s, n), j) == Seq#Index(s, j));

revealed function Seq#Drop(s: Seq, howMany: int) : Seq;

axiom (forall s: Seq, n: int :: 
  { Seq#Length(Seq#Drop(s, n)) } 
  0 <= n && n <= Seq#Length(s) ==> Seq#Length(Seq#Drop(s, n)) == Seq#Length(s) - n);

axiom (forall s: Seq, n: int, j: int :: 
  {:weight 11} { Seq#Index(Seq#Drop(s, n), j) } 
  0 <= n && 0 <= j && j < Seq#Length(s) - n
     ==> Seq#Index(Seq#Drop(s, n), j) == Seq#Index(s, j + n));

axiom (forall s: Seq, n: int, k: int :: 
  {:weight 11} { Seq#Index(s, k), Seq#Drop(s, n) } 
  0 <= n && n <= k && k < Seq#Length(s)
     ==> Seq#Index(Seq#Drop(s, n), k - n) == Seq#Index(s, k));

axiom (forall s: Seq, t: Seq, n: int :: 
  { Seq#Take(Seq#Append(s, t), n) } { Seq#Drop(Seq#Append(s, t), n) } 
  n == Seq#Length(s)
     ==> Seq#Take(Seq#Append(s, t), n) == s && Seq#Drop(Seq#Append(s, t), n) == t);

axiom (forall s: Seq, i: int, v: Box, n: int :: 
  { Seq#Take(Seq#Update(s, i, v), n) } 
  0 <= i && i < n && n <= Seq#Length(s)
     ==> Seq#Take(Seq#Update(s, i, v), n) == Seq#Update(Seq#Take(s, n), i, v));

axiom (forall s: Seq, i: int, v: Box, n: int :: 
  { Seq#Take(Seq#Update(s, i, v), n) } 
  n <= i && i < Seq#Length(s)
     ==> Seq#Take(Seq#Update(s, i, v), n) == Seq#Take(s, n));

axiom (forall s: Seq, i: int, v: Box, n: int :: 
  { Seq#Drop(Seq#Update(s, i, v), n) } 
  0 <= n && n <= i && i < Seq#Length(s)
     ==> Seq#Drop(Seq#Update(s, i, v), n) == Seq#Update(Seq#Drop(s, n), i - n, v));

axiom (forall s: Seq, i: int, v: Box, n: int :: 
  { Seq#Drop(Seq#Update(s, i, v), n) } 
  0 <= i && i < n && n <= Seq#Length(s)
     ==> Seq#Drop(Seq#Update(s, i, v), n) == Seq#Drop(s, n));

axiom (forall s: Seq, v: Box, n: int :: 
  { Seq#Drop(Seq#Build(s, v), n) } 
  0 <= n && n <= Seq#Length(s)
     ==> Seq#Drop(Seq#Build(s, v), n) == Seq#Build(Seq#Drop(s, n), v));

axiom (forall s: Seq, n: int :: { Seq#Drop(s, n) } n == 0 ==> Seq#Drop(s, n) == s);

axiom (forall s: Seq, n: int :: 
  { Seq#Take(s, n) } 
  n == 0 ==> Seq#Take(s, n) == Seq#Empty());

axiom (forall s: Seq, m: int, n: int :: 
  { Seq#Drop(Seq#Drop(s, m), n) } 
  0 <= m && 0 <= n && m + n <= Seq#Length(s)
     ==> Seq#Drop(Seq#Drop(s, m), n) == Seq#Drop(s, m + n));

axiom (forall s: Seq, bx: Box, t: Ty :: 
  { $Is(Seq#Build(s, bx), TSeq(t)) } 
  $Is(s, TSeq(t)) && $IsBox(bx, t) ==> $Is(Seq#Build(s, bx), TSeq(t)));

revealed function Seq#Create(ty: Ty, heap: Heap, len: int, init: HandleType) : Seq;

axiom (forall ty: Ty, heap: Heap, len: int, init: HandleType :: 
  { Seq#Length(Seq#Create(ty, heap, len, init): Seq) } 
  $IsGoodHeap(heap) && 0 <= len
     ==> Seq#Length(Seq#Create(ty, heap, len, init): Seq) == len);

axiom (forall ty: Ty, heap: Heap, len: int, init: HandleType, i: int :: 
  { Seq#Index(Seq#Create(ty, heap, len, init), i) } 
  $IsGoodHeap(heap) && 0 <= i && i < len
     ==> Seq#Index(Seq#Create(ty, heap, len, init), i)
       == Apply1(TInt, ty, heap, init, $Box(i)));

revealed function Seq#FromArray(h: Heap, a: ref) : Seq;

axiom (forall h: Heap, a: ref :: 
  { Seq#Length(Seq#FromArray(h, a)) } 
  Seq#Length(Seq#FromArray(h, a)) == _System.array.Length(a));

axiom (forall h: Heap, a: ref :: 
  { Seq#FromArray(h, a) } 
  (forall i: int :: 
    { read(h, a, IndexField(i)) } { Seq#Index(Seq#FromArray(h, a): Seq, i) } 
    0 <= i && i < Seq#Length(Seq#FromArray(h, a))
       ==> Seq#Index(Seq#FromArray(h, a), i) == read(h, a, IndexField(i))));

axiom (forall h0: Heap, h1: Heap, a: ref :: 
  { Seq#FromArray(h1, a), $HeapSucc(h0, h1) } 
  $IsGoodHeap(h0) && $IsGoodHeap(h1) && $HeapSucc(h0, h1) && h0[a] == h1[a]
     ==> Seq#FromArray(h0, a) == Seq#FromArray(h1, a));

axiom (forall h: Heap, i: int, v: Box, a: ref :: 
  { Seq#FromArray(update(h, a, IndexField(i), v), a) } 
  0 <= i && i < _System.array.Length(a)
     ==> Seq#FromArray(update(h, a, IndexField(i), v), a)
       == Seq#Update(Seq#FromArray(h, a), i, v));

axiom (forall h: Heap, a: ref, n0: int, n1: int :: 
  { Seq#Take(Seq#FromArray(h, a), n0), Seq#Take(Seq#FromArray(h, a), n1) } 
  n0 + 1 == n1 && 0 <= n0 && n1 <= _System.array.Length(a)
     ==> Seq#Take(Seq#FromArray(h, a), n1)
       == Seq#Build(Seq#Take(Seq#FromArray(h, a), n0), read(h, a, IndexField(n0): Field)));

revealed function Seq#Rank(Seq) : int;

axiom (forall s: Seq, i: int :: 
  { DtRank($Unbox(Seq#Index(s, i)): DatatypeType) } 
  0 <= i && i < Seq#Length(s)
     ==> DtRank($Unbox(Seq#Index(s, i)): DatatypeType) < Seq#Rank(s));

axiom (forall s: Seq, i: int :: 
  { Seq#Rank(Seq#Drop(s, i)) } 
  0 < i && i <= Seq#Length(s) ==> Seq#Rank(Seq#Drop(s, i)) < Seq#Rank(s));

axiom (forall s: Seq, i: int :: 
  { Seq#Rank(Seq#Take(s, i)) } 
  0 <= i && i < Seq#Length(s) ==> Seq#Rank(Seq#Take(s, i)) < Seq#Rank(s));

axiom (forall s: Seq, i: int, j: int :: 
  { Seq#Rank(Seq#Append(Seq#Take(s, i), Seq#Drop(s, j))) } 
  0 <= i && i < j && j <= Seq#Length(s)
     ==> Seq#Rank(Seq#Append(Seq#Take(s, i), Seq#Drop(s, j))) < Seq#Rank(s));

type Map;

revealed function Map#Domain(Map) : Set;

revealed function Map#Elements(Map) : [Box]Box;

revealed function Map#Card(Map) : int;

axiom (forall m: Map :: { Map#Card(m) } 0 <= Map#Card(m));

axiom (forall m: Map :: { Map#Card(m) } Map#Card(m) == 0 <==> m == Map#Empty());

axiom (forall m: Map :: 
  { Map#Domain(m) } 
  m == Map#Empty() || (exists k: Box :: Set#IsMember(Map#Domain(m), k)));

axiom (forall m: Map :: 
  { Map#Values(m) } 
  m == Map#Empty() || (exists v: Box :: Set#IsMember(Map#Values(m), v)));

axiom (forall m: Map :: 
  { Map#Items(m) } 
  m == Map#Empty()
     || (exists k: Box, v: Box :: 
      Set#IsMember(Map#Items(m), $Box(#_System._tuple#2._#Make2(k, v)))));

axiom (forall m: Map :: 
  { Set#Card(Map#Domain(m)) } { Map#Card(m) } 
  Set#Card(Map#Domain(m)) == Map#Card(m));

axiom (forall m: Map :: 
  { Set#Card(Map#Values(m)) } { Map#Card(m) } 
  Set#Card(Map#Values(m)) <= Map#Card(m));

axiom (forall m: Map :: 
  { Set#Card(Map#Items(m)) } { Map#Card(m) } 
  Set#Card(Map#Items(m)) == Map#Card(m));

revealed function Map#Values(Map) : Set;

axiom (forall m: Map, v: Box :: 
  { Set#IsMember(Map#Values(m), v) } 
  Set#IsMember(Map#Values(m), v)
     == (exists u: Box :: 
      { Set#IsMember(Map#Domain(m), u) } { Map#Elements(m)[u] } 
      Set#IsMember(Map#Domain(m), u) && v == Map#Elements(m)[u]));

revealed function Map#Items(Map) : Set;

revealed function #_System._tuple#2._#Make2(Box, Box) : DatatypeType;

revealed function _System.Tuple2._0(DatatypeType) : Box;

revealed function _System.Tuple2._1(DatatypeType) : Box;

axiom (forall m: Map, item: Box :: 
  { Set#IsMember(Map#Items(m), item) } 
  Set#IsMember(Map#Items(m), item)
     <==> item
         == $Box(#_System._tuple#2._#Make2(_System.Tuple2._0($Unbox(item)), _System.Tuple2._1($Unbox(item))))
       && Set#IsMember(Map#Domain(m), _System.Tuple2._0($Unbox(item)))
       && Map#Elements(m)[_System.Tuple2._0($Unbox(item))]
         == _System.Tuple2._1($Unbox(item)));

revealed function Map#Empty() : Map;

axiom (forall u: Box :: 
  { Set#IsMember(Map#Domain(Map#Empty(): Map), u) } 
  !Set#IsMember(Map#Domain(Map#Empty(): Map), u));

revealed function Map#Glue(Set, [Box]Box, Ty) : Map;

axiom (forall a: Set, b: [Box]Box, t: Ty :: 
  { Map#Domain(Map#Glue(a, b, t)) } 
  Map#Domain(Map#Glue(a, b, t)) == a);

axiom (forall a: Set, b: [Box]Box, t: Ty, bx: Box :: 
  { Map#Elements(Map#Glue(a, b, t))[bx] } 
  Set#IsMember(Map#Domain(Map#Glue(a, b, t)), bx)
     ==> Map#Elements(Map#Glue(a, b, t))[bx] == b[bx]);

axiom (forall a: Set, b: [Box]Box, t0: Ty, t1: Ty :: 
  { Map#Glue(a, b, TMap(t0, t1)) } 
  (forall bx: Box :: Set#IsMember(a, bx) ==> $IsBox(bx, t0) && $IsBox(b[bx], t1))
     ==> $Is(Map#Glue(a, b, TMap(t0, t1)), TMap(t0, t1)));

revealed function Map#Build(Map, Box, Box) : Map;

axiom (forall m: Map, u: Box, u': Box, v: Box :: 
  { Set#IsMember(Map#Domain(Map#Build(m, u, v)), u') } 
    { Map#Elements(Map#Build(m, u, v))[u'] } 
  (u' == u
       ==> Set#IsMember(Map#Domain(Map#Build(m, u, v)), u')
         && Map#Elements(Map#Build(m, u, v))[u'] == v)
     && (u' != u
       ==> Set#IsMember(Map#Domain(Map#Build(m, u, v)), u')
           == Set#IsMember(Map#Domain(m), u')
         && Map#Elements(Map#Build(m, u, v))[u'] == Map#Elements(m)[u']));

axiom (forall m: Map, u: Box, v: Box :: 
  { Map#Card(Map#Build(m, u, v)) } 
  Set#IsMember(Map#Domain(m), u) ==> Map#Card(Map#Build(m, u, v)) == Map#Card(m));

axiom (forall m: Map, u: Box, v: Box :: 
  { Map#Card(Map#Build(m, u, v)) } 
  !Set#IsMember(Map#Domain(m), u)
     ==> Map#Card(Map#Build(m, u, v)) == Map#Card(m) + 1);

revealed function Map#Merge(Map, Map) : Map;

axiom (forall m: Map, n: Map :: 
  { Map#Domain(Map#Merge(m, n)) } 
  Map#Domain(Map#Merge(m, n)) == Set#Union(Map#Domain(m), Map#Domain(n)));

axiom (forall m: Map, n: Map, u: Box :: 
  { Map#Elements(Map#Merge(m, n))[u] } 
  Set#IsMember(Map#Domain(Map#Merge(m, n)), u)
     ==> (!Set#IsMember(Map#Domain(n), u)
         ==> Map#Elements(Map#Merge(m, n))[u] == Map#Elements(m)[u])
       && (Set#IsMember(Map#Domain(n), u)
         ==> Map#Elements(Map#Merge(m, n))[u] == Map#Elements(n)[u]));

revealed function Map#Subtract(Map, Set) : Map;

axiom (forall m: Map, s: Set :: 
  { Map#Domain(Map#Subtract(m, s)) } 
  Map#Domain(Map#Subtract(m, s)) == Set#Difference(Map#Domain(m), s));

axiom (forall m: Map, s: Set, u: Box :: 
  { Map#Elements(Map#Subtract(m, s))[u] } 
  Set#IsMember(Map#Domain(Map#Subtract(m, s)), u)
     ==> Map#Elements(Map#Subtract(m, s))[u] == Map#Elements(m)[u]);

revealed function Map#Equal(Map, Map) : bool;

axiom (forall m: Map, m': Map :: 
  { Map#Equal(m, m') } 
  Map#Equal(m, m')
     <==> (forall u: Box :: 
        Set#IsMember(Map#Domain(m), u) == Set#IsMember(Map#Domain(m'), u))
       && (forall u: Box :: 
        Set#IsMember(Map#Domain(m), u) ==> Map#Elements(m)[u] == Map#Elements(m')[u]));

axiom (forall m: Map, m': Map :: { Map#Equal(m, m') } Map#Equal(m, m') ==> m == m');

revealed function Map#Disjoint(Map, Map) : bool;

axiom (forall m: Map, m': Map :: 
  { Map#Disjoint(m, m') } 
  Map#Disjoint(m, m')
     <==> (forall o: Box :: 
      { Set#IsMember(Map#Domain(m), o) } { Set#IsMember(Map#Domain(m'), o) } 
      !Set#IsMember(Map#Domain(m), o) || !Set#IsMember(Map#Domain(m'), o)));

type IMap;

revealed function IMap#Domain(IMap) : ISet;

revealed function IMap#Elements(IMap) : [Box]Box;

axiom (forall m: IMap :: 
  { IMap#Domain(m) } 
  m == IMap#Empty() || (exists k: Box :: IMap#Domain(m)[k]));

axiom (forall m: IMap :: 
  { IMap#Values(m) } 
  m == IMap#Empty() || (exists v: Box :: IMap#Values(m)[v]));

axiom (forall m: IMap :: 
  { IMap#Items(m) } 
  m == IMap#Empty()
     || (exists k: Box, v: Box :: IMap#Items(m)[$Box(#_System._tuple#2._#Make2(k, v))]));

axiom (forall m: IMap :: 
  { IMap#Domain(m) } 
  m == IMap#Empty() <==> IMap#Domain(m) == ISet#Empty());

axiom (forall m: IMap :: 
  { IMap#Values(m) } 
  m == IMap#Empty() <==> IMap#Values(m) == ISet#Empty());

axiom (forall m: IMap :: 
  { IMap#Items(m) } 
  m == IMap#Empty() <==> IMap#Items(m) == ISet#Empty());

revealed function IMap#Values(IMap) : ISet;

axiom (forall m: IMap, v: Box :: 
  { IMap#Values(m)[v] } 
  IMap#Values(m)[v]
     == (exists u: Box :: 
      { IMap#Domain(m)[u] } { IMap#Elements(m)[u] } 
      IMap#Domain(m)[u] && v == IMap#Elements(m)[u]));

revealed function IMap#Items(IMap) : ISet;

axiom (forall m: IMap, item: Box :: 
  { IMap#Items(m)[item] } 
  IMap#Items(m)[item]
     <==> item
         == $Box(#_System._tuple#2._#Make2(_System.Tuple2._0($Unbox(item)), _System.Tuple2._1($Unbox(item))))
       && IMap#Domain(m)[_System.Tuple2._0($Unbox(item))]
       && IMap#Elements(m)[_System.Tuple2._0($Unbox(item))]
         == _System.Tuple2._1($Unbox(item)));

revealed function IMap#Empty() : IMap;

axiom (forall u: Box :: 
  { IMap#Domain(IMap#Empty(): IMap)[u] } 
  !IMap#Domain(IMap#Empty(): IMap)[u]);

revealed function IMap#Glue([Box]bool, [Box]Box, Ty) : IMap;

axiom (forall a: [Box]bool, b: [Box]Box, t: Ty :: 
  { IMap#Domain(IMap#Glue(a, b, t)) } 
  IMap#Domain(IMap#Glue(a, b, t)) == a);

axiom (forall a: [Box]bool, b: [Box]Box, t: Ty, bx: Box :: 
  { IMap#Elements(IMap#Glue(a, b, t))[bx] } 
  IMap#Domain(IMap#Glue(a, b, t))[bx]
     ==> IMap#Elements(IMap#Glue(a, b, t))[bx] == b[bx]);

axiom (forall a: [Box]bool, b: [Box]Box, t0: Ty, t1: Ty :: 
  { IMap#Glue(a, b, TIMap(t0, t1)) } 
  (forall bx: Box :: a[bx] ==> $IsBox(bx, t0) && $IsBox(b[bx], t1))
     ==> $Is(IMap#Glue(a, b, TIMap(t0, t1)), TIMap(t0, t1)));

revealed function IMap#Build(IMap, Box, Box) : IMap;

axiom (forall m: IMap, u: Box, u': Box, v: Box :: 
  { IMap#Domain(IMap#Build(m, u, v))[u'] } 
    { IMap#Elements(IMap#Build(m, u, v))[u'] } 
  (u' == u
       ==> IMap#Domain(IMap#Build(m, u, v))[u']
         && IMap#Elements(IMap#Build(m, u, v))[u'] == v)
     && (u' != u
       ==> IMap#Domain(IMap#Build(m, u, v))[u'] == IMap#Domain(m)[u']
         && IMap#Elements(IMap#Build(m, u, v))[u'] == IMap#Elements(m)[u']));

revealed function IMap#Equal(IMap, IMap) : bool;

axiom (forall m: IMap, m': IMap :: 
  { IMap#Equal(m, m') } 
  IMap#Equal(m, m')
     <==> (forall u: Box :: IMap#Domain(m)[u] == IMap#Domain(m')[u])
       && (forall u: Box :: 
        IMap#Domain(m)[u] ==> IMap#Elements(m)[u] == IMap#Elements(m')[u]));

axiom (forall m: IMap, m': IMap :: 
  { IMap#Equal(m, m') } 
  IMap#Equal(m, m') ==> m == m');

revealed function IMap#Merge(IMap, IMap) : IMap;

axiom (forall m: IMap, n: IMap :: 
  { IMap#Domain(IMap#Merge(m, n)) } 
  IMap#Domain(IMap#Merge(m, n)) == ISet#Union(IMap#Domain(m), IMap#Domain(n)));

axiom (forall m: IMap, n: IMap, u: Box :: 
  { IMap#Elements(IMap#Merge(m, n))[u] } 
  IMap#Domain(IMap#Merge(m, n))[u]
     ==> (!IMap#Domain(n)[u]
         ==> IMap#Elements(IMap#Merge(m, n))[u] == IMap#Elements(m)[u])
       && (IMap#Domain(n)[u]
         ==> IMap#Elements(IMap#Merge(m, n))[u] == IMap#Elements(n)[u]));

revealed function IMap#Subtract(IMap, Set) : IMap;

axiom (forall m: IMap, s: Set :: 
  { IMap#Domain(IMap#Subtract(m, s)) } 
  IMap#Domain(IMap#Subtract(m, s))
     == ISet#Difference(IMap#Domain(m), ISet#FromSet(s)));

axiom (forall m: IMap, s: Set, u: Box :: 
  { IMap#Elements(IMap#Subtract(m, s))[u] } 
  IMap#Domain(IMap#Subtract(m, s))[u]
     ==> IMap#Elements(IMap#Subtract(m, s))[u] == IMap#Elements(m)[u]);

revealed function INTERNAL_add_boogie(x: int, y: int) : int
uses {
axiom (forall x: int, y: int :: 
  { INTERNAL_add_boogie(x, y): int } 
  INTERNAL_add_boogie(x, y): int == x + y);
}

revealed function INTERNAL_sub_boogie(x: int, y: int) : int
uses {
axiom (forall x: int, y: int :: 
  { INTERNAL_sub_boogie(x, y): int } 
  INTERNAL_sub_boogie(x, y): int == x - y);
}

revealed function INTERNAL_mul_boogie(x: int, y: int) : int
uses {
axiom (forall x: int, y: int :: 
  { INTERNAL_mul_boogie(x, y): int } 
  INTERNAL_mul_boogie(x, y): int == x * y);
}

revealed function INTERNAL_div_boogie(x: int, y: int) : int
uses {
axiom (forall x: int, y: int :: 
  { INTERNAL_div_boogie(x, y): int } 
  INTERNAL_div_boogie(x, y): int == x div y);
}

revealed function INTERNAL_mod_boogie(x: int, y: int) : int
uses {
axiom (forall x: int, y: int :: 
  { INTERNAL_mod_boogie(x, y): int } 
  INTERNAL_mod_boogie(x, y): int == x mod y);
}

revealed function {:never_pattern true} INTERNAL_lt_boogie(x: int, y: int) : bool
uses {
axiom (forall x: int, y: int :: 
  {:never_pattern true} { INTERNAL_lt_boogie(x, y): bool } 
  INTERNAL_lt_boogie(x, y): bool == (x < y));
}

revealed function {:never_pattern true} INTERNAL_le_boogie(x: int, y: int) : bool
uses {
axiom (forall x: int, y: int :: 
  {:never_pattern true} { INTERNAL_le_boogie(x, y): bool } 
  INTERNAL_le_boogie(x, y): bool == (x <= y));
}

revealed function {:never_pattern true} INTERNAL_gt_boogie(x: int, y: int) : bool
uses {
axiom (forall x: int, y: int :: 
  {:never_pattern true} { INTERNAL_gt_boogie(x, y): bool } 
  INTERNAL_gt_boogie(x, y): bool == (x > y));
}

revealed function {:never_pattern true} INTERNAL_ge_boogie(x: int, y: int) : bool
uses {
axiom (forall x: int, y: int :: 
  {:never_pattern true} { INTERNAL_ge_boogie(x, y): bool } 
  INTERNAL_ge_boogie(x, y): bool == (x >= y));
}

revealed function Mul(x: int, y: int) : int
uses {
axiom (forall x: int, y: int :: { Mul(x, y): int } Mul(x, y): int == x * y);
}

revealed function Div(x: int, y: int) : int
uses {
axiom (forall x: int, y: int :: { Div(x, y): int } Div(x, y): int == x div y);
}

revealed function Mod(x: int, y: int) : int
uses {
axiom (forall x: int, y: int :: { Mod(x, y): int } Mod(x, y): int == x mod y);
}

revealed function Add(x: int, y: int) : int
uses {
axiom (forall x: int, y: int :: { Add(x, y): int } Add(x, y): int == x + y);
}

revealed function Sub(x: int, y: int) : int
uses {
axiom (forall x: int, y: int :: { Sub(x, y): int } Sub(x, y): int == x - y);
}

function Tclass._System.___hFunc0(Ty) : Ty;

const unique Tagclass._System.___hFunc0: TyTag;

// Tclass._System.___hFunc0 Tag
axiom (forall #$R: Ty :: 
  { Tclass._System.___hFunc0(#$R) } 
  Tag(Tclass._System.___hFunc0(#$R)) == Tagclass._System.___hFunc0
     && TagFamily(Tclass._System.___hFunc0(#$R)) == tytagFamily$_#Func0);

function Tclass._System.___hFunc0_0(Ty) : Ty;

// Tclass._System.___hFunc0 injectivity 0
axiom (forall #$R: Ty :: 
  { Tclass._System.___hFunc0(#$R) } 
  Tclass._System.___hFunc0_0(Tclass._System.___hFunc0(#$R)) == #$R);

// Box/unbox axiom for Tclass._System.___hFunc0
axiom (forall #$R: Ty, bx: Box :: 
  { $IsBox(bx, Tclass._System.___hFunc0(#$R)) } 
  $IsBox(bx, Tclass._System.___hFunc0(#$R))
     ==> $Box($Unbox(bx): HandleType) == bx
       && $Is($Unbox(bx): HandleType, Tclass._System.___hFunc0(#$R)));

function Handle0([Heap]Box, [Heap]bool, [Heap]Set) : HandleType;

function Apply0(Ty, Heap, HandleType) : Box;

function Requires0(Ty, Heap, HandleType) : bool;

function Reads0(Ty, Heap, HandleType) : Set;

axiom (forall t0: Ty, heap: Heap, h: [Heap]Box, r: [Heap]bool, rd: [Heap]Set :: 
  { Apply0(t0, heap, Handle0(h, r, rd)) } 
  Apply0(t0, heap, Handle0(h, r, rd)) == h[heap]);

axiom (forall t0: Ty, heap: Heap, h: [Heap]Box, r: [Heap]bool, rd: [Heap]Set :: 
  { Requires0(t0, heap, Handle0(h, r, rd)) } 
  r[heap] ==> Requires0(t0, heap, Handle0(h, r, rd)));

axiom (forall t0: Ty, heap: Heap, h: [Heap]Box, r: [Heap]bool, rd: [Heap]Set, bx: Box :: 
  { Set#IsMember(Reads0(t0, heap, Handle0(h, r, rd)), bx) } 
  Set#IsMember(Reads0(t0, heap, Handle0(h, r, rd)), bx)
     == Set#IsMember(rd[heap], bx));

function {:inline} Requires0#canCall(t0: Ty, heap: Heap, f: HandleType) : bool
{
  true
}

function {:inline} Reads0#canCall(t0: Ty, heap: Heap, f: HandleType) : bool
{
  true
}

// frame axiom for Reads0
axiom (forall t0: Ty, h0: Heap, h1: Heap, f: HandleType :: 
  { $HeapSucc(h0, h1), Reads0(t0, h1, f) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && $Is(f, Tclass._System.___hFunc0(t0))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads0(t0, h0, f), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Reads0(t0, h0, f) == Reads0(t0, h1, f));

// frame axiom for Reads0
axiom (forall t0: Ty, h0: Heap, h1: Heap, f: HandleType :: 
  { $HeapSucc(h0, h1), Reads0(t0, h1, f) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && $Is(f, Tclass._System.___hFunc0(t0))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads0(t0, h1, f), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Reads0(t0, h0, f) == Reads0(t0, h1, f));

// frame axiom for Requires0
axiom (forall t0: Ty, h0: Heap, h1: Heap, f: HandleType :: 
  { $HeapSucc(h0, h1), Requires0(t0, h1, f) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && $Is(f, Tclass._System.___hFunc0(t0))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads0(t0, h0, f), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Requires0(t0, h0, f) == Requires0(t0, h1, f));

// frame axiom for Requires0
axiom (forall t0: Ty, h0: Heap, h1: Heap, f: HandleType :: 
  { $HeapSucc(h0, h1), Requires0(t0, h1, f) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && $Is(f, Tclass._System.___hFunc0(t0))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads0(t0, h1, f), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Requires0(t0, h0, f) == Requires0(t0, h1, f));

// frame axiom for Apply0
axiom (forall t0: Ty, h0: Heap, h1: Heap, f: HandleType :: 
  { $HeapSucc(h0, h1), Apply0(t0, h1, f) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && $Is(f, Tclass._System.___hFunc0(t0))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads0(t0, h0, f), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Apply0(t0, h0, f) == Apply0(t0, h1, f));

// frame axiom for Apply0
axiom (forall t0: Ty, h0: Heap, h1: Heap, f: HandleType :: 
  { $HeapSucc(h0, h1), Apply0(t0, h1, f) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && $Is(f, Tclass._System.___hFunc0(t0))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads0(t0, h1, f), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Apply0(t0, h0, f) == Apply0(t0, h1, f));

// empty-reads property for Reads0 
axiom (forall t0: Ty, heap: Heap, f: HandleType :: 
  { Reads0(t0, $OneHeap, f), $IsGoodHeap(heap) } { Reads0(t0, heap, f) } 
  $IsGoodHeap(heap) && $Is(f, Tclass._System.___hFunc0(t0))
     ==> (Set#Equal(Reads0(t0, $OneHeap, f), Set#Empty(): Set)
       <==> Set#Equal(Reads0(t0, heap, f), Set#Empty(): Set)));

// empty-reads property for Requires0
axiom (forall t0: Ty, heap: Heap, f: HandleType :: 
  { Requires0(t0, $OneHeap, f), $IsGoodHeap(heap) } { Requires0(t0, heap, f) } 
  $IsGoodHeap(heap)
       && $Is(f, Tclass._System.___hFunc0(t0))
       && Set#Equal(Reads0(t0, $OneHeap, f), Set#Empty(): Set)
     ==> Requires0(t0, $OneHeap, f) == Requires0(t0, heap, f));

axiom (forall f: HandleType, t0: Ty :: 
  { $Is(f, Tclass._System.___hFunc0(t0)) } 
  $Is(f, Tclass._System.___hFunc0(t0))
     <==> (forall h: Heap :: 
      { Apply0(t0, h, f) } 
      $IsGoodHeap(h) && Requires0(t0, h, f) ==> $IsBox(Apply0(t0, h, f), t0)));

axiom (forall f: HandleType, t0: Ty, u0: Ty :: 
  { $Is(f, Tclass._System.___hFunc0(t0)), $Is(f, Tclass._System.___hFunc0(u0)) } 
  $Is(f, Tclass._System.___hFunc0(t0))
       && (forall bx: Box :: 
        { $IsBox(bx, t0) } { $IsBox(bx, u0) } 
        $IsBox(bx, t0) ==> $IsBox(bx, u0))
     ==> $Is(f, Tclass._System.___hFunc0(u0)));

axiom (forall f: HandleType, t0: Ty, h: Heap :: 
  { $IsAlloc(f, Tclass._System.___hFunc0(t0), h) } 
  $IsGoodHeap(h)
     ==> 
    $IsAlloc(f, Tclass._System.___hFunc0(t0), h)
     ==> 
    Requires0(t0, h, f)
     ==> (forall r: ref :: 
      { Set#IsMember(Reads0(t0, h, f), $Box(r)) } 
      r != null && Set#IsMember(Reads0(t0, h, f), $Box(r))
         ==> $Unbox(read(h, r, alloc)): bool));

axiom (forall f: HandleType, t0: Ty, h: Heap :: 
  { $IsAlloc(f, Tclass._System.___hFunc0(t0), h) } 
  $IsGoodHeap(h) && $IsAlloc(f, Tclass._System.___hFunc0(t0), h)
     ==> 
    Requires0(t0, h, f)
     ==> $IsAllocBox(Apply0(t0, h, f), t0, h));

function Tclass._System.___hFunc1(Ty, Ty) : Ty;

const unique Tagclass._System.___hFunc1: TyTag;

// Tclass._System.___hFunc1 Tag
axiom (forall #$T0: Ty, #$R: Ty :: 
  { Tclass._System.___hFunc1(#$T0, #$R) } 
  Tag(Tclass._System.___hFunc1(#$T0, #$R)) == Tagclass._System.___hFunc1
     && TagFamily(Tclass._System.___hFunc1(#$T0, #$R)) == tytagFamily$_#Func1);

function Tclass._System.___hFunc1_0(Ty) : Ty;

// Tclass._System.___hFunc1 injectivity 0
axiom (forall #$T0: Ty, #$R: Ty :: 
  { Tclass._System.___hFunc1(#$T0, #$R) } 
  Tclass._System.___hFunc1_0(Tclass._System.___hFunc1(#$T0, #$R)) == #$T0);

function Tclass._System.___hFunc1_1(Ty) : Ty;

// Tclass._System.___hFunc1 injectivity 1
axiom (forall #$T0: Ty, #$R: Ty :: 
  { Tclass._System.___hFunc1(#$T0, #$R) } 
  Tclass._System.___hFunc1_1(Tclass._System.___hFunc1(#$T0, #$R)) == #$R);

// Box/unbox axiom for Tclass._System.___hFunc1
axiom (forall #$T0: Ty, #$R: Ty, bx: Box :: 
  { $IsBox(bx, Tclass._System.___hFunc1(#$T0, #$R)) } 
  $IsBox(bx, Tclass._System.___hFunc1(#$T0, #$R))
     ==> $Box($Unbox(bx): HandleType) == bx
       && $Is($Unbox(bx): HandleType, Tclass._System.___hFunc1(#$T0, #$R)));

function Handle1([Heap,Box]Box, [Heap,Box]bool, [Heap,Box]Set) : HandleType;

function Requires1(Ty, Ty, Heap, HandleType, Box) : bool;

function Reads1(Ty, Ty, Heap, HandleType, Box) : Set;

axiom (forall t0: Ty, 
    t1: Ty, 
    heap: Heap, 
    h: [Heap,Box]Box, 
    r: [Heap,Box]bool, 
    rd: [Heap,Box]Set, 
    bx0: Box :: 
  { Apply1(t0, t1, heap, Handle1(h, r, rd), bx0) } 
  Apply1(t0, t1, heap, Handle1(h, r, rd), bx0) == h[heap, bx0]);

axiom (forall t0: Ty, 
    t1: Ty, 
    heap: Heap, 
    h: [Heap,Box]Box, 
    r: [Heap,Box]bool, 
    rd: [Heap,Box]Set, 
    bx0: Box :: 
  { Requires1(t0, t1, heap, Handle1(h, r, rd), bx0) } 
  r[heap, bx0] ==> Requires1(t0, t1, heap, Handle1(h, r, rd), bx0));

axiom (forall t0: Ty, 
    t1: Ty, 
    heap: Heap, 
    h: [Heap,Box]Box, 
    r: [Heap,Box]bool, 
    rd: [Heap,Box]Set, 
    bx0: Box, 
    bx: Box :: 
  { Set#IsMember(Reads1(t0, t1, heap, Handle1(h, r, rd), bx0), bx) } 
  Set#IsMember(Reads1(t0, t1, heap, Handle1(h, r, rd), bx0), bx)
     == Set#IsMember(rd[heap, bx0], bx));

function {:inline} Requires1#canCall(t0: Ty, t1: Ty, heap: Heap, f: HandleType, bx0: Box) : bool
{
  true
}

function {:inline} Reads1#canCall(t0: Ty, t1: Ty, heap: Heap, f: HandleType, bx0: Box) : bool
{
  true
}

// frame axiom for Reads1
axiom (forall t0: Ty, t1: Ty, h0: Heap, h1: Heap, f: HandleType, bx0: Box :: 
  { $HeapSucc(h0, h1), Reads1(t0, t1, h1, f, bx0) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      ($IsBox(bx0, t0)
       && $Is(f, Tclass._System.___hFunc1(t0, t1)))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads1(t0, t1, h0, f, bx0), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Reads1(t0, t1, h0, f, bx0) == Reads1(t0, t1, h1, f, bx0));

// frame axiom for Reads1
axiom (forall t0: Ty, t1: Ty, h0: Heap, h1: Heap, f: HandleType, bx0: Box :: 
  { $HeapSucc(h0, h1), Reads1(t0, t1, h1, f, bx0) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      ($IsBox(bx0, t0)
       && $Is(f, Tclass._System.___hFunc1(t0, t1)))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads1(t0, t1, h1, f, bx0), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Reads1(t0, t1, h0, f, bx0) == Reads1(t0, t1, h1, f, bx0));

// frame axiom for Requires1
axiom (forall t0: Ty, t1: Ty, h0: Heap, h1: Heap, f: HandleType, bx0: Box :: 
  { $HeapSucc(h0, h1), Requires1(t0, t1, h1, f, bx0) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      ($IsBox(bx0, t0)
       && $Is(f, Tclass._System.___hFunc1(t0, t1)))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads1(t0, t1, h0, f, bx0), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Requires1(t0, t1, h0, f, bx0) == Requires1(t0, t1, h1, f, bx0));

// frame axiom for Requires1
axiom (forall t0: Ty, t1: Ty, h0: Heap, h1: Heap, f: HandleType, bx0: Box :: 
  { $HeapSucc(h0, h1), Requires1(t0, t1, h1, f, bx0) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      ($IsBox(bx0, t0)
       && $Is(f, Tclass._System.___hFunc1(t0, t1)))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads1(t0, t1, h1, f, bx0), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Requires1(t0, t1, h0, f, bx0) == Requires1(t0, t1, h1, f, bx0));

// frame axiom for Apply1
axiom (forall t0: Ty, t1: Ty, h0: Heap, h1: Heap, f: HandleType, bx0: Box :: 
  { $HeapSucc(h0, h1), Apply1(t0, t1, h1, f, bx0) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      ($IsBox(bx0, t0)
       && $Is(f, Tclass._System.___hFunc1(t0, t1)))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads1(t0, t1, h0, f, bx0), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Apply1(t0, t1, h0, f, bx0) == Apply1(t0, t1, h1, f, bx0));

// frame axiom for Apply1
axiom (forall t0: Ty, t1: Ty, h0: Heap, h1: Heap, f: HandleType, bx0: Box :: 
  { $HeapSucc(h0, h1), Apply1(t0, t1, h1, f, bx0) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      ($IsBox(bx0, t0)
       && $Is(f, Tclass._System.___hFunc1(t0, t1)))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads1(t0, t1, h1, f, bx0), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Apply1(t0, t1, h0, f, bx0) == Apply1(t0, t1, h1, f, bx0));

// empty-reads property for Reads1 
axiom (forall t0: Ty, t1: Ty, heap: Heap, f: HandleType, bx0: Box :: 
  { Reads1(t0, t1, $OneHeap, f, bx0), $IsGoodHeap(heap) } 
    { Reads1(t0, t1, heap, f, bx0) } 
  $IsGoodHeap(heap)
       && 
      ($IsBox(bx0, t0)
       && $Is(f, Tclass._System.___hFunc1(t0, t1)))
     ==> (Set#Equal(Reads1(t0, t1, $OneHeap, f, bx0), Set#Empty(): Set)
       <==> Set#Equal(Reads1(t0, t1, heap, f, bx0), Set#Empty(): Set)));

// empty-reads property for Requires1
axiom (forall t0: Ty, t1: Ty, heap: Heap, f: HandleType, bx0: Box :: 
  { Requires1(t0, t1, $OneHeap, f, bx0), $IsGoodHeap(heap) } 
    { Requires1(t0, t1, heap, f, bx0) } 
  $IsGoodHeap(heap)
       && 
      ($IsBox(bx0, t0)
       && $Is(f, Tclass._System.___hFunc1(t0, t1)))
       && Set#Equal(Reads1(t0, t1, $OneHeap, f, bx0), Set#Empty(): Set)
     ==> Requires1(t0, t1, $OneHeap, f, bx0) == Requires1(t0, t1, heap, f, bx0));

axiom (forall f: HandleType, t0: Ty, t1: Ty :: 
  { $Is(f, Tclass._System.___hFunc1(t0, t1)) } 
  $Is(f, Tclass._System.___hFunc1(t0, t1))
     <==> (forall h: Heap, bx0: Box :: 
      { Apply1(t0, t1, h, f, bx0) } 
      $IsGoodHeap(h) && $IsBox(bx0, t0) && Requires1(t0, t1, h, f, bx0)
         ==> $IsBox(Apply1(t0, t1, h, f, bx0), t1)));

axiom (forall f: HandleType, t0: Ty, t1: Ty, u0: Ty, u1: Ty :: 
  { $Is(f, Tclass._System.___hFunc1(t0, t1)), $Is(f, Tclass._System.___hFunc1(u0, u1)) } 
  $Is(f, Tclass._System.___hFunc1(t0, t1))
       && (forall bx: Box :: 
        { $IsBox(bx, u0) } { $IsBox(bx, t0) } 
        $IsBox(bx, u0) ==> $IsBox(bx, t0))
       && (forall bx: Box :: 
        { $IsBox(bx, t1) } { $IsBox(bx, u1) } 
        $IsBox(bx, t1) ==> $IsBox(bx, u1))
     ==> $Is(f, Tclass._System.___hFunc1(u0, u1)));

axiom (forall f: HandleType, t0: Ty, t1: Ty, h: Heap :: 
  { $IsAlloc(f, Tclass._System.___hFunc1(t0, t1), h) } 
  $IsGoodHeap(h)
     ==> 
    $IsAlloc(f, Tclass._System.___hFunc1(t0, t1), h)
     ==> (forall bx0: Box :: 
      { Apply1(t0, t1, h, f, bx0) } { Reads1(t0, t1, h, f, bx0) } 
      $IsBox(bx0, t0) && $IsAllocBox(bx0, t0, h) && Requires1(t0, t1, h, f, bx0)
         ==> (forall r: ref :: 
          { Set#IsMember(Reads1(t0, t1, h, f, bx0), $Box(r)) } 
          r != null && Set#IsMember(Reads1(t0, t1, h, f, bx0), $Box(r))
             ==> $Unbox(read(h, r, alloc)): bool)));

axiom (forall f: HandleType, t0: Ty, t1: Ty, h: Heap :: 
  { $IsAlloc(f, Tclass._System.___hFunc1(t0, t1), h) } 
  $IsGoodHeap(h) && $IsAlloc(f, Tclass._System.___hFunc1(t0, t1), h)
     ==> (forall bx0: Box :: 
      { Apply1(t0, t1, h, f, bx0) } 
      $IsAllocBox(bx0, t0, h) && Requires1(t0, t1, h, f, bx0)
         ==> $IsAllocBox(Apply1(t0, t1, h, f, bx0), t1, h)));

function Tclass._System.___hFunc3(Ty, Ty, Ty, Ty) : Ty;

const unique Tagclass._System.___hFunc3: TyTag;

// Tclass._System.___hFunc3 Tag
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty :: 
  { Tclass._System.___hFunc3(#$T0, #$T1, #$T2, #$R) } 
  Tag(Tclass._System.___hFunc3(#$T0, #$T1, #$T2, #$R))
       == Tagclass._System.___hFunc3
     && TagFamily(Tclass._System.___hFunc3(#$T0, #$T1, #$T2, #$R))
       == tytagFamily$_#Func3);

function Tclass._System.___hFunc3_0(Ty) : Ty;

// Tclass._System.___hFunc3 injectivity 0
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty :: 
  { Tclass._System.___hFunc3(#$T0, #$T1, #$T2, #$R) } 
  Tclass._System.___hFunc3_0(Tclass._System.___hFunc3(#$T0, #$T1, #$T2, #$R))
     == #$T0);

function Tclass._System.___hFunc3_1(Ty) : Ty;

// Tclass._System.___hFunc3 injectivity 1
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty :: 
  { Tclass._System.___hFunc3(#$T0, #$T1, #$T2, #$R) } 
  Tclass._System.___hFunc3_1(Tclass._System.___hFunc3(#$T0, #$T1, #$T2, #$R))
     == #$T1);

function Tclass._System.___hFunc3_2(Ty) : Ty;

// Tclass._System.___hFunc3 injectivity 2
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty :: 
  { Tclass._System.___hFunc3(#$T0, #$T1, #$T2, #$R) } 
  Tclass._System.___hFunc3_2(Tclass._System.___hFunc3(#$T0, #$T1, #$T2, #$R))
     == #$T2);

function Tclass._System.___hFunc3_3(Ty) : Ty;

// Tclass._System.___hFunc3 injectivity 3
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty :: 
  { Tclass._System.___hFunc3(#$T0, #$T1, #$T2, #$R) } 
  Tclass._System.___hFunc3_3(Tclass._System.___hFunc3(#$T0, #$T1, #$T2, #$R))
     == #$R);

// Box/unbox axiom for Tclass._System.___hFunc3
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty, bx: Box :: 
  { $IsBox(bx, Tclass._System.___hFunc3(#$T0, #$T1, #$T2, #$R)) } 
  $IsBox(bx, Tclass._System.___hFunc3(#$T0, #$T1, #$T2, #$R))
     ==> $Box($Unbox(bx): HandleType) == bx
       && $Is($Unbox(bx): HandleType, Tclass._System.___hFunc3(#$T0, #$T1, #$T2, #$R)));

function Handle3([Heap,Box,Box,Box]Box, [Heap,Box,Box,Box]bool, [Heap,Box,Box,Box]Set)
   : HandleType;

function Apply3(Ty, Ty, Ty, Ty, Heap, HandleType, Box, Box, Box) : Box;

function Requires3(Ty, Ty, Ty, Ty, Heap, HandleType, Box, Box, Box) : bool;

function Reads3(Ty, Ty, Ty, Ty, Heap, HandleType, Box, Box, Box) : Set;

axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    heap: Heap, 
    h: [Heap,Box,Box,Box]Box, 
    r: [Heap,Box,Box,Box]bool, 
    rd: [Heap,Box,Box,Box]Set, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box :: 
  { Apply3(t0, t1, t2, t3, heap, Handle3(h, r, rd), bx0, bx1, bx2) } 
  Apply3(t0, t1, t2, t3, heap, Handle3(h, r, rd), bx0, bx1, bx2)
     == h[heap, bx0, bx1, bx2]);

axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    heap: Heap, 
    h: [Heap,Box,Box,Box]Box, 
    r: [Heap,Box,Box,Box]bool, 
    rd: [Heap,Box,Box,Box]Set, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box :: 
  { Requires3(t0, t1, t2, t3, heap, Handle3(h, r, rd), bx0, bx1, bx2) } 
  r[heap, bx0, bx1, bx2]
     ==> Requires3(t0, t1, t2, t3, heap, Handle3(h, r, rd), bx0, bx1, bx2));

axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    heap: Heap, 
    h: [Heap,Box,Box,Box]Box, 
    r: [Heap,Box,Box,Box]bool, 
    rd: [Heap,Box,Box,Box]Set, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box, 
    bx: Box :: 
  { Set#IsMember(Reads3(t0, t1, t2, t3, heap, Handle3(h, r, rd), bx0, bx1, bx2), bx) } 
  Set#IsMember(Reads3(t0, t1, t2, t3, heap, Handle3(h, r, rd), bx0, bx1, bx2), bx)
     == Set#IsMember(rd[heap, bx0, bx1, bx2], bx));

function {:inline} Requires3#canCall(t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    heap: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box)
   : bool
{
  true
}

function {:inline} Reads3#canCall(t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    heap: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box)
   : bool
{
  true
}

// frame axiom for Reads3
axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    h0: Heap, 
    h1: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box :: 
  { $HeapSucc(h0, h1), Reads3(t0, t1, t2, t3, h1, f, bx0, bx1, bx2) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      (
      $IsBox(bx0, t0)
       && $IsBox(bx1, t1)
       && $IsBox(bx2, t2)
       && $Is(f, Tclass._System.___hFunc3(t0, t1, t2, t3)))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads3(t0, t1, t2, t3, h0, f, bx0, bx1, bx2), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Reads3(t0, t1, t2, t3, h0, f, bx0, bx1, bx2)
       == Reads3(t0, t1, t2, t3, h1, f, bx0, bx1, bx2));

// frame axiom for Reads3
axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    h0: Heap, 
    h1: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box :: 
  { $HeapSucc(h0, h1), Reads3(t0, t1, t2, t3, h1, f, bx0, bx1, bx2) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      (
      $IsBox(bx0, t0)
       && $IsBox(bx1, t1)
       && $IsBox(bx2, t2)
       && $Is(f, Tclass._System.___hFunc3(t0, t1, t2, t3)))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads3(t0, t1, t2, t3, h1, f, bx0, bx1, bx2), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Reads3(t0, t1, t2, t3, h0, f, bx0, bx1, bx2)
       == Reads3(t0, t1, t2, t3, h1, f, bx0, bx1, bx2));

// frame axiom for Requires3
axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    h0: Heap, 
    h1: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box :: 
  { $HeapSucc(h0, h1), Requires3(t0, t1, t2, t3, h1, f, bx0, bx1, bx2) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      (
      $IsBox(bx0, t0)
       && $IsBox(bx1, t1)
       && $IsBox(bx2, t2)
       && $Is(f, Tclass._System.___hFunc3(t0, t1, t2, t3)))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads3(t0, t1, t2, t3, h0, f, bx0, bx1, bx2), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Requires3(t0, t1, t2, t3, h0, f, bx0, bx1, bx2)
       == Requires3(t0, t1, t2, t3, h1, f, bx0, bx1, bx2));

// frame axiom for Requires3
axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    h0: Heap, 
    h1: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box :: 
  { $HeapSucc(h0, h1), Requires3(t0, t1, t2, t3, h1, f, bx0, bx1, bx2) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      (
      $IsBox(bx0, t0)
       && $IsBox(bx1, t1)
       && $IsBox(bx2, t2)
       && $Is(f, Tclass._System.___hFunc3(t0, t1, t2, t3)))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads3(t0, t1, t2, t3, h1, f, bx0, bx1, bx2), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Requires3(t0, t1, t2, t3, h0, f, bx0, bx1, bx2)
       == Requires3(t0, t1, t2, t3, h1, f, bx0, bx1, bx2));

// frame axiom for Apply3
axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    h0: Heap, 
    h1: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box :: 
  { $HeapSucc(h0, h1), Apply3(t0, t1, t2, t3, h1, f, bx0, bx1, bx2) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      (
      $IsBox(bx0, t0)
       && $IsBox(bx1, t1)
       && $IsBox(bx2, t2)
       && $Is(f, Tclass._System.___hFunc3(t0, t1, t2, t3)))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads3(t0, t1, t2, t3, h0, f, bx0, bx1, bx2), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Apply3(t0, t1, t2, t3, h0, f, bx0, bx1, bx2)
       == Apply3(t0, t1, t2, t3, h1, f, bx0, bx1, bx2));

// frame axiom for Apply3
axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    h0: Heap, 
    h1: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box :: 
  { $HeapSucc(h0, h1), Apply3(t0, t1, t2, t3, h1, f, bx0, bx1, bx2) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      (
      $IsBox(bx0, t0)
       && $IsBox(bx1, t1)
       && $IsBox(bx2, t2)
       && $Is(f, Tclass._System.___hFunc3(t0, t1, t2, t3)))
       && (forall o: ref, fld: Field :: 
        o != null && Set#IsMember(Reads3(t0, t1, t2, t3, h1, f, bx0, bx1, bx2), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Apply3(t0, t1, t2, t3, h0, f, bx0, bx1, bx2)
       == Apply3(t0, t1, t2, t3, h1, f, bx0, bx1, bx2));

// empty-reads property for Reads3 
axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    heap: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box :: 
  { Reads3(t0, t1, t2, t3, $OneHeap, f, bx0, bx1, bx2), $IsGoodHeap(heap) } 
    { Reads3(t0, t1, t2, t3, heap, f, bx0, bx1, bx2) } 
  $IsGoodHeap(heap)
       && 
      (
      $IsBox(bx0, t0)
       && $IsBox(bx1, t1)
       && $IsBox(bx2, t2)
       && $Is(f, Tclass._System.___hFunc3(t0, t1, t2, t3)))
     ==> (Set#Equal(Reads3(t0, t1, t2, t3, $OneHeap, f, bx0, bx1, bx2), Set#Empty(): Set)
       <==> Set#Equal(Reads3(t0, t1, t2, t3, heap, f, bx0, bx1, bx2), Set#Empty(): Set)));

// empty-reads property for Requires3
axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    heap: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box :: 
  { Requires3(t0, t1, t2, t3, $OneHeap, f, bx0, bx1, bx2), $IsGoodHeap(heap) } 
    { Requires3(t0, t1, t2, t3, heap, f, bx0, bx1, bx2) } 
  $IsGoodHeap(heap)
       && 
      (
      $IsBox(bx0, t0)
       && $IsBox(bx1, t1)
       && $IsBox(bx2, t2)
       && $Is(f, Tclass._System.___hFunc3(t0, t1, t2, t3)))
       && Set#Equal(Reads3(t0, t1, t2, t3, $OneHeap, f, bx0, bx1, bx2), Set#Empty(): Set)
     ==> Requires3(t0, t1, t2, t3, $OneHeap, f, bx0, bx1, bx2)
       == Requires3(t0, t1, t2, t3, heap, f, bx0, bx1, bx2));

axiom (forall f: HandleType, t0: Ty, t1: Ty, t2: Ty, t3: Ty :: 
  { $Is(f, Tclass._System.___hFunc3(t0, t1, t2, t3)) } 
  $Is(f, Tclass._System.___hFunc3(t0, t1, t2, t3))
     <==> (forall h: Heap, bx0: Box, bx1: Box, bx2: Box :: 
      { Apply3(t0, t1, t2, t3, h, f, bx0, bx1, bx2) } 
      $IsGoodHeap(h)
           && 
          (
          $IsBox(bx0, t0)
           && $IsBox(bx1, t1)
           && $IsBox(bx2, t2))
           && Requires3(t0, t1, t2, t3, h, f, bx0, bx1, bx2)
         ==> $IsBox(Apply3(t0, t1, t2, t3, h, f, bx0, bx1, bx2), t3)));

axiom (forall f: HandleType, t0: Ty, t1: Ty, t2: Ty, t3: Ty, u0: Ty, u1: Ty, u2: Ty, u3: Ty :: 
  { $Is(f, Tclass._System.___hFunc3(t0, t1, t2, t3)), $Is(f, Tclass._System.___hFunc3(u0, u1, u2, u3)) } 
  $Is(f, Tclass._System.___hFunc3(t0, t1, t2, t3))
       && (forall bx: Box :: 
        { $IsBox(bx, u0) } { $IsBox(bx, t0) } 
        $IsBox(bx, u0) ==> $IsBox(bx, t0))
       && (forall bx: Box :: 
        { $IsBox(bx, u1) } { $IsBox(bx, t1) } 
        $IsBox(bx, u1) ==> $IsBox(bx, t1))
       && (forall bx: Box :: 
        { $IsBox(bx, u2) } { $IsBox(bx, t2) } 
        $IsBox(bx, u2) ==> $IsBox(bx, t2))
       && (forall bx: Box :: 
        { $IsBox(bx, t3) } { $IsBox(bx, u3) } 
        $IsBox(bx, t3) ==> $IsBox(bx, u3))
     ==> $Is(f, Tclass._System.___hFunc3(u0, u1, u2, u3)));

axiom (forall f: HandleType, t0: Ty, t1: Ty, t2: Ty, t3: Ty, h: Heap :: 
  { $IsAlloc(f, Tclass._System.___hFunc3(t0, t1, t2, t3), h) } 
  $IsGoodHeap(h)
     ==> 
    $IsAlloc(f, Tclass._System.___hFunc3(t0, t1, t2, t3), h)
     ==> (forall bx0: Box, bx1: Box, bx2: Box :: 
      { Apply3(t0, t1, t2, t3, h, f, bx0, bx1, bx2) } 
        { Reads3(t0, t1, t2, t3, h, f, bx0, bx1, bx2) } 
      $IsBox(bx0, t0)
           && $IsAllocBox(bx0, t0, h)
           && 
          ($IsBox(bx1, t1)
           && $IsAllocBox(bx1, t1, h))
           && 
          ($IsBox(bx2, t2)
           && $IsAllocBox(bx2, t2, h))
           && Requires3(t0, t1, t2, t3, h, f, bx0, bx1, bx2)
         ==> (forall r: ref :: 
          { Set#IsMember(Reads3(t0, t1, t2, t3, h, f, bx0, bx1, bx2), $Box(r)) } 
          r != null && Set#IsMember(Reads3(t0, t1, t2, t3, h, f, bx0, bx1, bx2), $Box(r))
             ==> $Unbox(read(h, r, alloc)): bool)));

axiom (forall f: HandleType, t0: Ty, t1: Ty, t2: Ty, t3: Ty, h: Heap :: 
  { $IsAlloc(f, Tclass._System.___hFunc3(t0, t1, t2, t3), h) } 
  $IsGoodHeap(h) && $IsAlloc(f, Tclass._System.___hFunc3(t0, t1, t2, t3), h)
     ==> (forall bx0: Box, bx1: Box, bx2: Box :: 
      { Apply3(t0, t1, t2, t3, h, f, bx0, bx1, bx2) } 
      $IsAllocBox(bx0, t0, h)
           && $IsAllocBox(bx1, t1, h)
           && $IsAllocBox(bx2, t2, h)
           && Requires3(t0, t1, t2, t3, h, f, bx0, bx1, bx2)
         ==> $IsAllocBox(Apply3(t0, t1, t2, t3, h, f, bx0, bx1, bx2), t3, h)));

function Tclass._System.___hFunc4(Ty, Ty, Ty, Ty, Ty) : Ty;

const unique Tagclass._System.___hFunc4: TyTag;

// Tclass._System.___hFunc4 Tag
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tag(Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
       == Tagclass._System.___hFunc4
     && TagFamily(Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
       == tytagFamily$_#Func4);

function Tclass._System.___hFunc4_0(Ty) : Ty;

// Tclass._System.___hFunc4 injectivity 0
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tclass._System.___hFunc4_0(Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     == #$T0);

function Tclass._System.___hFunc4_1(Ty) : Ty;

// Tclass._System.___hFunc4 injectivity 1
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tclass._System.___hFunc4_1(Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     == #$T1);

function Tclass._System.___hFunc4_2(Ty) : Ty;

// Tclass._System.___hFunc4 injectivity 2
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tclass._System.___hFunc4_2(Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     == #$T2);

function Tclass._System.___hFunc4_3(Ty) : Ty;

// Tclass._System.___hFunc4 injectivity 3
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tclass._System.___hFunc4_3(Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     == #$T3);

function Tclass._System.___hFunc4_4(Ty) : Ty;

// Tclass._System.___hFunc4 injectivity 4
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tclass._System.___hFunc4_4(Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     == #$R);

// Box/unbox axiom for Tclass._System.___hFunc4
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty, bx: Box :: 
  { $IsBox(bx, Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R)) } 
  $IsBox(bx, Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     ==> $Box($Unbox(bx): HandleType) == bx
       && $Is($Unbox(bx): HandleType, Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R)));

function Handle4([Heap,Box,Box,Box,Box]Box, [Heap,Box,Box,Box,Box]bool, [Heap,Box,Box,Box,Box]Set)
   : HandleType;

function Apply4(Ty, Ty, Ty, Ty, Ty, Heap, HandleType, Box, Box, Box, Box) : Box;

function Requires4(Ty, Ty, Ty, Ty, Ty, Heap, HandleType, Box, Box, Box, Box) : bool;

function Reads4(Ty, Ty, Ty, Ty, Ty, Heap, HandleType, Box, Box, Box, Box) : Set;

axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    t4: Ty, 
    heap: Heap, 
    h: [Heap,Box,Box,Box,Box]Box, 
    r: [Heap,Box,Box,Box,Box]bool, 
    rd: [Heap,Box,Box,Box,Box]Set, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box, 
    bx3: Box :: 
  { Apply4(t0, t1, t2, t3, t4, heap, Handle4(h, r, rd), bx0, bx1, bx2, bx3) } 
  Apply4(t0, t1, t2, t3, t4, heap, Handle4(h, r, rd), bx0, bx1, bx2, bx3)
     == h[heap, bx0, bx1, bx2, bx3]);

axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    t4: Ty, 
    heap: Heap, 
    h: [Heap,Box,Box,Box,Box]Box, 
    r: [Heap,Box,Box,Box,Box]bool, 
    rd: [Heap,Box,Box,Box,Box]Set, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box, 
    bx3: Box :: 
  { Requires4(t0, t1, t2, t3, t4, heap, Handle4(h, r, rd), bx0, bx1, bx2, bx3) } 
  r[heap, bx0, bx1, bx2, bx3]
     ==> Requires4(t0, t1, t2, t3, t4, heap, Handle4(h, r, rd), bx0, bx1, bx2, bx3));

axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    t4: Ty, 
    heap: Heap, 
    h: [Heap,Box,Box,Box,Box]Box, 
    r: [Heap,Box,Box,Box,Box]bool, 
    rd: [Heap,Box,Box,Box,Box]Set, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box, 
    bx3: Box, 
    bx: Box :: 
  { Set#IsMember(Reads4(t0, t1, t2, t3, t4, heap, Handle4(h, r, rd), bx0, bx1, bx2, bx3), bx) } 
  Set#IsMember(Reads4(t0, t1, t2, t3, t4, heap, Handle4(h, r, rd), bx0, bx1, bx2, bx3), bx)
     == Set#IsMember(rd[heap, bx0, bx1, bx2, bx3], bx));

function {:inline} Requires4#canCall(t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    t4: Ty, 
    heap: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box, 
    bx3: Box)
   : bool
{
  true
}

function {:inline} Reads4#canCall(t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    t4: Ty, 
    heap: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box, 
    bx3: Box)
   : bool
{
  true
}

// frame axiom for Reads4
axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    t4: Ty, 
    h0: Heap, 
    h1: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box, 
    bx3: Box :: 
  { $HeapSucc(h0, h1), Reads4(t0, t1, t2, t3, t4, h1, f, bx0, bx1, bx2, bx3) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      (
      $IsBox(bx0, t0)
       && $IsBox(bx1, t1)
       && $IsBox(bx2, t2)
       && $IsBox(bx3, t3)
       && $Is(f, Tclass._System.___hFunc4(t0, t1, t2, t3, t4)))
       && (forall o: ref, fld: Field :: 
        o != null
             && Set#IsMember(Reads4(t0, t1, t2, t3, t4, h0, f, bx0, bx1, bx2, bx3), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Reads4(t0, t1, t2, t3, t4, h0, f, bx0, bx1, bx2, bx3)
       == Reads4(t0, t1, t2, t3, t4, h1, f, bx0, bx1, bx2, bx3));

// frame axiom for Reads4
axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    t4: Ty, 
    h0: Heap, 
    h1: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box, 
    bx3: Box :: 
  { $HeapSucc(h0, h1), Reads4(t0, t1, t2, t3, t4, h1, f, bx0, bx1, bx2, bx3) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      (
      $IsBox(bx0, t0)
       && $IsBox(bx1, t1)
       && $IsBox(bx2, t2)
       && $IsBox(bx3, t3)
       && $Is(f, Tclass._System.___hFunc4(t0, t1, t2, t3, t4)))
       && (forall o: ref, fld: Field :: 
        o != null
             && Set#IsMember(Reads4(t0, t1, t2, t3, t4, h1, f, bx0, bx1, bx2, bx3), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Reads4(t0, t1, t2, t3, t4, h0, f, bx0, bx1, bx2, bx3)
       == Reads4(t0, t1, t2, t3, t4, h1, f, bx0, bx1, bx2, bx3));

// frame axiom for Requires4
axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    t4: Ty, 
    h0: Heap, 
    h1: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box, 
    bx3: Box :: 
  { $HeapSucc(h0, h1), Requires4(t0, t1, t2, t3, t4, h1, f, bx0, bx1, bx2, bx3) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      (
      $IsBox(bx0, t0)
       && $IsBox(bx1, t1)
       && $IsBox(bx2, t2)
       && $IsBox(bx3, t3)
       && $Is(f, Tclass._System.___hFunc4(t0, t1, t2, t3, t4)))
       && (forall o: ref, fld: Field :: 
        o != null
             && Set#IsMember(Reads4(t0, t1, t2, t3, t4, h0, f, bx0, bx1, bx2, bx3), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Requires4(t0, t1, t2, t3, t4, h0, f, bx0, bx1, bx2, bx3)
       == Requires4(t0, t1, t2, t3, t4, h1, f, bx0, bx1, bx2, bx3));

// frame axiom for Requires4
axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    t4: Ty, 
    h0: Heap, 
    h1: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box, 
    bx3: Box :: 
  { $HeapSucc(h0, h1), Requires4(t0, t1, t2, t3, t4, h1, f, bx0, bx1, bx2, bx3) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      (
      $IsBox(bx0, t0)
       && $IsBox(bx1, t1)
       && $IsBox(bx2, t2)
       && $IsBox(bx3, t3)
       && $Is(f, Tclass._System.___hFunc4(t0, t1, t2, t3, t4)))
       && (forall o: ref, fld: Field :: 
        o != null
             && Set#IsMember(Reads4(t0, t1, t2, t3, t4, h1, f, bx0, bx1, bx2, bx3), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Requires4(t0, t1, t2, t3, t4, h0, f, bx0, bx1, bx2, bx3)
       == Requires4(t0, t1, t2, t3, t4, h1, f, bx0, bx1, bx2, bx3));

// frame axiom for Apply4
axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    t4: Ty, 
    h0: Heap, 
    h1: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box, 
    bx3: Box :: 
  { $HeapSucc(h0, h1), Apply4(t0, t1, t2, t3, t4, h1, f, bx0, bx1, bx2, bx3) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      (
      $IsBox(bx0, t0)
       && $IsBox(bx1, t1)
       && $IsBox(bx2, t2)
       && $IsBox(bx3, t3)
       && $Is(f, Tclass._System.___hFunc4(t0, t1, t2, t3, t4)))
       && (forall o: ref, fld: Field :: 
        o != null
             && Set#IsMember(Reads4(t0, t1, t2, t3, t4, h0, f, bx0, bx1, bx2, bx3), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Apply4(t0, t1, t2, t3, t4, h0, f, bx0, bx1, bx2, bx3)
       == Apply4(t0, t1, t2, t3, t4, h1, f, bx0, bx1, bx2, bx3));

// frame axiom for Apply4
axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    t4: Ty, 
    h0: Heap, 
    h1: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box, 
    bx3: Box :: 
  { $HeapSucc(h0, h1), Apply4(t0, t1, t2, t3, t4, h1, f, bx0, bx1, bx2, bx3) } 
  $HeapSucc(h0, h1)
       && 
      ($IsGoodHeap(h0)
       && $IsGoodHeap(h1))
       && 
      (
      $IsBox(bx0, t0)
       && $IsBox(bx1, t1)
       && $IsBox(bx2, t2)
       && $IsBox(bx3, t3)
       && $Is(f, Tclass._System.___hFunc4(t0, t1, t2, t3, t4)))
       && (forall o: ref, fld: Field :: 
        o != null
             && Set#IsMember(Reads4(t0, t1, t2, t3, t4, h1, f, bx0, bx1, bx2, bx3), $Box(o))
           ==> read(h0, o, fld) == read(h1, o, fld))
     ==> Apply4(t0, t1, t2, t3, t4, h0, f, bx0, bx1, bx2, bx3)
       == Apply4(t0, t1, t2, t3, t4, h1, f, bx0, bx1, bx2, bx3));

// empty-reads property for Reads4 
axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    t4: Ty, 
    heap: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box, 
    bx3: Box :: 
  { Reads4(t0, t1, t2, t3, t4, $OneHeap, f, bx0, bx1, bx2, bx3), $IsGoodHeap(heap) } 
    { Reads4(t0, t1, t2, t3, t4, heap, f, bx0, bx1, bx2, bx3) } 
  $IsGoodHeap(heap)
       && 
      (
      $IsBox(bx0, t0)
       && $IsBox(bx1, t1)
       && $IsBox(bx2, t2)
       && $IsBox(bx3, t3)
       && $Is(f, Tclass._System.___hFunc4(t0, t1, t2, t3, t4)))
     ==> (Set#Equal(Reads4(t0, t1, t2, t3, t4, $OneHeap, f, bx0, bx1, bx2, bx3), Set#Empty(): Set)
       <==> Set#Equal(Reads4(t0, t1, t2, t3, t4, heap, f, bx0, bx1, bx2, bx3), Set#Empty(): Set)));

// empty-reads property for Requires4
axiom (forall t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    t4: Ty, 
    heap: Heap, 
    f: HandleType, 
    bx0: Box, 
    bx1: Box, 
    bx2: Box, 
    bx3: Box :: 
  { Requires4(t0, t1, t2, t3, t4, $OneHeap, f, bx0, bx1, bx2, bx3), $IsGoodHeap(heap) } 
    { Requires4(t0, t1, t2, t3, t4, heap, f, bx0, bx1, bx2, bx3) } 
  $IsGoodHeap(heap)
       && 
      (
      $IsBox(bx0, t0)
       && $IsBox(bx1, t1)
       && $IsBox(bx2, t2)
       && $IsBox(bx3, t3)
       && $Is(f, Tclass._System.___hFunc4(t0, t1, t2, t3, t4)))
       && Set#Equal(Reads4(t0, t1, t2, t3, t4, $OneHeap, f, bx0, bx1, bx2, bx3), Set#Empty(): Set)
     ==> Requires4(t0, t1, t2, t3, t4, $OneHeap, f, bx0, bx1, bx2, bx3)
       == Requires4(t0, t1, t2, t3, t4, heap, f, bx0, bx1, bx2, bx3));

axiom (forall f: HandleType, t0: Ty, t1: Ty, t2: Ty, t3: Ty, t4: Ty :: 
  { $Is(f, Tclass._System.___hFunc4(t0, t1, t2, t3, t4)) } 
  $Is(f, Tclass._System.___hFunc4(t0, t1, t2, t3, t4))
     <==> (forall h: Heap, bx0: Box, bx1: Box, bx2: Box, bx3: Box :: 
      { Apply4(t0, t1, t2, t3, t4, h, f, bx0, bx1, bx2, bx3) } 
      $IsGoodHeap(h)
           && 
          (
          $IsBox(bx0, t0)
           && $IsBox(bx1, t1)
           && $IsBox(bx2, t2)
           && $IsBox(bx3, t3))
           && Requires4(t0, t1, t2, t3, t4, h, f, bx0, bx1, bx2, bx3)
         ==> $IsBox(Apply4(t0, t1, t2, t3, t4, h, f, bx0, bx1, bx2, bx3), t4)));

axiom (forall f: HandleType, 
    t0: Ty, 
    t1: Ty, 
    t2: Ty, 
    t3: Ty, 
    t4: Ty, 
    u0: Ty, 
    u1: Ty, 
    u2: Ty, 
    u3: Ty, 
    u4: Ty :: 
  { $Is(f, Tclass._System.___hFunc4(t0, t1, t2, t3, t4)), $Is(f, Tclass._System.___hFunc4(u0, u1, u2, u3, u4)) } 
  $Is(f, Tclass._System.___hFunc4(t0, t1, t2, t3, t4))
       && (forall bx: Box :: 
        { $IsBox(bx, u0) } { $IsBox(bx, t0) } 
        $IsBox(bx, u0) ==> $IsBox(bx, t0))
       && (forall bx: Box :: 
        { $IsBox(bx, u1) } { $IsBox(bx, t1) } 
        $IsBox(bx, u1) ==> $IsBox(bx, t1))
       && (forall bx: Box :: 
        { $IsBox(bx, u2) } { $IsBox(bx, t2) } 
        $IsBox(bx, u2) ==> $IsBox(bx, t2))
       && (forall bx: Box :: 
        { $IsBox(bx, u3) } { $IsBox(bx, t3) } 
        $IsBox(bx, u3) ==> $IsBox(bx, t3))
       && (forall bx: Box :: 
        { $IsBox(bx, t4) } { $IsBox(bx, u4) } 
        $IsBox(bx, t4) ==> $IsBox(bx, u4))
     ==> $Is(f, Tclass._System.___hFunc4(u0, u1, u2, u3, u4)));

axiom (forall f: HandleType, t0: Ty, t1: Ty, t2: Ty, t3: Ty, t4: Ty, h: Heap :: 
  { $IsAlloc(f, Tclass._System.___hFunc4(t0, t1, t2, t3, t4), h) } 
  $IsGoodHeap(h)
     ==> 
    $IsAlloc(f, Tclass._System.___hFunc4(t0, t1, t2, t3, t4), h)
     ==> (forall bx0: Box, bx1: Box, bx2: Box, bx3: Box :: 
      { Apply4(t0, t1, t2, t3, t4, h, f, bx0, bx1, bx2, bx3) } 
        { Reads4(t0, t1, t2, t3, t4, h, f, bx0, bx1, bx2, bx3) } 
      $IsBox(bx0, t0)
           && $IsAllocBox(bx0, t0, h)
           && 
          ($IsBox(bx1, t1)
           && $IsAllocBox(bx1, t1, h))
           && 
          ($IsBox(bx2, t2)
           && $IsAllocBox(bx2, t2, h))
           && 
          ($IsBox(bx3, t3)
           && $IsAllocBox(bx3, t3, h))
           && Requires4(t0, t1, t2, t3, t4, h, f, bx0, bx1, bx2, bx3)
         ==> (forall r: ref :: 
          { Set#IsMember(Reads4(t0, t1, t2, t3, t4, h, f, bx0, bx1, bx2, bx3), $Box(r)) } 
          r != null
               && Set#IsMember(Reads4(t0, t1, t2, t3, t4, h, f, bx0, bx1, bx2, bx3), $Box(r))
             ==> $Unbox(read(h, r, alloc)): bool)));

axiom (forall f: HandleType, t0: Ty, t1: Ty, t2: Ty, t3: Ty, t4: Ty, h: Heap :: 
  { $IsAlloc(f, Tclass._System.___hFunc4(t0, t1, t2, t3, t4), h) } 
  $IsGoodHeap(h) && $IsAlloc(f, Tclass._System.___hFunc4(t0, t1, t2, t3, t4), h)
     ==> (forall bx0: Box, bx1: Box, bx2: Box, bx3: Box :: 
      { Apply4(t0, t1, t2, t3, t4, h, f, bx0, bx1, bx2, bx3) } 
      $IsAllocBox(bx0, t0, h)
           && $IsAllocBox(bx1, t1, h)
           && $IsAllocBox(bx2, t2, h)
           && $IsAllocBox(bx3, t3, h)
           && Requires4(t0, t1, t2, t3, t4, h, f, bx0, bx1, bx2, bx3)
         ==> $IsAllocBox(Apply4(t0, t1, t2, t3, t4, h, f, bx0, bx1, bx2, bx3), t4, h)));

function Tclass._System.___hPartialFunc0(Ty) : Ty;

const unique Tagclass._System.___hPartialFunc0: TyTag;

// Tclass._System.___hPartialFunc0 Tag
axiom (forall #$R: Ty :: 
  { Tclass._System.___hPartialFunc0(#$R) } 
  Tag(Tclass._System.___hPartialFunc0(#$R)) == Tagclass._System.___hPartialFunc0
     && TagFamily(Tclass._System.___hPartialFunc0(#$R)) == tytagFamily$_#PartialFunc0);

function Tclass._System.___hPartialFunc0_0(Ty) : Ty;

// Tclass._System.___hPartialFunc0 injectivity 0
axiom (forall #$R: Ty :: 
  { Tclass._System.___hPartialFunc0(#$R) } 
  Tclass._System.___hPartialFunc0_0(Tclass._System.___hPartialFunc0(#$R)) == #$R);

// Box/unbox axiom for Tclass._System.___hPartialFunc0
axiom (forall #$R: Ty, bx: Box :: 
  { $IsBox(bx, Tclass._System.___hPartialFunc0(#$R)) } 
  $IsBox(bx, Tclass._System.___hPartialFunc0(#$R))
     ==> $Box($Unbox(bx): HandleType) == bx
       && $Is($Unbox(bx): HandleType, Tclass._System.___hPartialFunc0(#$R)));

// $Is axiom for subset type _System._#PartialFunc0
axiom (forall #$R: Ty, f#0: HandleType :: 
  { $Is(f#0, Tclass._System.___hPartialFunc0(#$R)) } 
  $Is(f#0, Tclass._System.___hPartialFunc0(#$R))
     <==> $Is(f#0, Tclass._System.___hFunc0(#$R))
       && Set#Equal(Reads0(#$R, $OneHeap, f#0), Set#Empty(): Set));

// $IsAlloc axiom for subset type _System._#PartialFunc0
axiom (forall #$R: Ty, f#0: HandleType, $h: Heap :: 
  { $IsAlloc(f#0, Tclass._System.___hPartialFunc0(#$R), $h) } 
  $IsAlloc(f#0, Tclass._System.___hPartialFunc0(#$R), $h)
     <==> $IsAlloc(f#0, Tclass._System.___hFunc0(#$R), $h));

function Tclass._System.___hPartialFunc1(Ty, Ty) : Ty;

const unique Tagclass._System.___hPartialFunc1: TyTag;

// Tclass._System.___hPartialFunc1 Tag
axiom (forall #$T0: Ty, #$R: Ty :: 
  { Tclass._System.___hPartialFunc1(#$T0, #$R) } 
  Tag(Tclass._System.___hPartialFunc1(#$T0, #$R))
       == Tagclass._System.___hPartialFunc1
     && TagFamily(Tclass._System.___hPartialFunc1(#$T0, #$R))
       == tytagFamily$_#PartialFunc1);

function Tclass._System.___hPartialFunc1_0(Ty) : Ty;

// Tclass._System.___hPartialFunc1 injectivity 0
axiom (forall #$T0: Ty, #$R: Ty :: 
  { Tclass._System.___hPartialFunc1(#$T0, #$R) } 
  Tclass._System.___hPartialFunc1_0(Tclass._System.___hPartialFunc1(#$T0, #$R))
     == #$T0);

function Tclass._System.___hPartialFunc1_1(Ty) : Ty;

// Tclass._System.___hPartialFunc1 injectivity 1
axiom (forall #$T0: Ty, #$R: Ty :: 
  { Tclass._System.___hPartialFunc1(#$T0, #$R) } 
  Tclass._System.___hPartialFunc1_1(Tclass._System.___hPartialFunc1(#$T0, #$R))
     == #$R);

// Box/unbox axiom for Tclass._System.___hPartialFunc1
axiom (forall #$T0: Ty, #$R: Ty, bx: Box :: 
  { $IsBox(bx, Tclass._System.___hPartialFunc1(#$T0, #$R)) } 
  $IsBox(bx, Tclass._System.___hPartialFunc1(#$T0, #$R))
     ==> $Box($Unbox(bx): HandleType) == bx
       && $Is($Unbox(bx): HandleType, Tclass._System.___hPartialFunc1(#$T0, #$R)));

// $Is axiom for subset type _System._#PartialFunc1
axiom (forall #$T0: Ty, #$R: Ty, f#0: HandleType :: 
  { $Is(f#0, Tclass._System.___hPartialFunc1(#$T0, #$R)) } 
  $Is(f#0, Tclass._System.___hPartialFunc1(#$T0, #$R))
     <==> $Is(f#0, Tclass._System.___hFunc1(#$T0, #$R))
       && (forall x0#0: Box :: 
        $IsBox(x0#0, #$T0)
           ==> Set#Equal(Reads1(#$T0, #$R, $OneHeap, f#0, x0#0), Set#Empty(): Set)));

// $IsAlloc axiom for subset type _System._#PartialFunc1
axiom (forall #$T0: Ty, #$R: Ty, f#0: HandleType, $h: Heap :: 
  { $IsAlloc(f#0, Tclass._System.___hPartialFunc1(#$T0, #$R), $h) } 
  $IsAlloc(f#0, Tclass._System.___hPartialFunc1(#$T0, #$R), $h)
     <==> $IsAlloc(f#0, Tclass._System.___hFunc1(#$T0, #$R), $h));

function Tclass._System.___hPartialFunc3(Ty, Ty, Ty, Ty) : Ty;

const unique Tagclass._System.___hPartialFunc3: TyTag;

// Tclass._System.___hPartialFunc3 Tag
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty :: 
  { Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R) } 
  Tag(Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R))
       == Tagclass._System.___hPartialFunc3
     && TagFamily(Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R))
       == tytagFamily$_#PartialFunc3);

function Tclass._System.___hPartialFunc3_0(Ty) : Ty;

// Tclass._System.___hPartialFunc3 injectivity 0
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty :: 
  { Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R) } 
  Tclass._System.___hPartialFunc3_0(Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R))
     == #$T0);

function Tclass._System.___hPartialFunc3_1(Ty) : Ty;

// Tclass._System.___hPartialFunc3 injectivity 1
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty :: 
  { Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R) } 
  Tclass._System.___hPartialFunc3_1(Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R))
     == #$T1);

function Tclass._System.___hPartialFunc3_2(Ty) : Ty;

// Tclass._System.___hPartialFunc3 injectivity 2
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty :: 
  { Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R) } 
  Tclass._System.___hPartialFunc3_2(Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R))
     == #$T2);

function Tclass._System.___hPartialFunc3_3(Ty) : Ty;

// Tclass._System.___hPartialFunc3 injectivity 3
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty :: 
  { Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R) } 
  Tclass._System.___hPartialFunc3_3(Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R))
     == #$R);

// Box/unbox axiom for Tclass._System.___hPartialFunc3
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty, bx: Box :: 
  { $IsBox(bx, Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R)) } 
  $IsBox(bx, Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R))
     ==> $Box($Unbox(bx): HandleType) == bx
       && $Is($Unbox(bx): HandleType, Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R)));

// $Is axiom for subset type _System._#PartialFunc3
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty, f#0: HandleType :: 
  { $Is(f#0, Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R)) } 
  $Is(f#0, Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R))
     <==> $Is(f#0, Tclass._System.___hFunc3(#$T0, #$T1, #$T2, #$R))
       && (forall x0#0: Box, x1#0: Box, x2#0: Box :: 
        $IsBox(x0#0, #$T0) && $IsBox(x1#0, #$T1) && $IsBox(x2#0, #$T2)
           ==> Set#Equal(Reads3(#$T0, #$T1, #$T2, #$R, $OneHeap, f#0, x0#0, x1#0, x2#0), Set#Empty(): Set)));

// $IsAlloc axiom for subset type _System._#PartialFunc3
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty, f#0: HandleType, $h: Heap :: 
  { $IsAlloc(f#0, Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R), $h) } 
  $IsAlloc(f#0, Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R), $h)
     <==> $IsAlloc(f#0, Tclass._System.___hFunc3(#$T0, #$T1, #$T2, #$R), $h));

function Tclass._System.___hPartialFunc4(Ty, Ty, Ty, Ty, Ty) : Ty;

const unique Tagclass._System.___hPartialFunc4: TyTag;

// Tclass._System.___hPartialFunc4 Tag
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tag(Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
       == Tagclass._System.___hPartialFunc4
     && TagFamily(Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
       == tytagFamily$_#PartialFunc4);

function Tclass._System.___hPartialFunc4_0(Ty) : Ty;

// Tclass._System.___hPartialFunc4 injectivity 0
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tclass._System.___hPartialFunc4_0(Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     == #$T0);

function Tclass._System.___hPartialFunc4_1(Ty) : Ty;

// Tclass._System.___hPartialFunc4 injectivity 1
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tclass._System.___hPartialFunc4_1(Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     == #$T1);

function Tclass._System.___hPartialFunc4_2(Ty) : Ty;

// Tclass._System.___hPartialFunc4 injectivity 2
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tclass._System.___hPartialFunc4_2(Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     == #$T2);

function Tclass._System.___hPartialFunc4_3(Ty) : Ty;

// Tclass._System.___hPartialFunc4 injectivity 3
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tclass._System.___hPartialFunc4_3(Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     == #$T3);

function Tclass._System.___hPartialFunc4_4(Ty) : Ty;

// Tclass._System.___hPartialFunc4 injectivity 4
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tclass._System.___hPartialFunc4_4(Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     == #$R);

// Box/unbox axiom for Tclass._System.___hPartialFunc4
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty, bx: Box :: 
  { $IsBox(bx, Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R)) } 
  $IsBox(bx, Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     ==> $Box($Unbox(bx): HandleType) == bx
       && $Is($Unbox(bx): HandleType, 
        Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R)));

// $Is axiom for subset type _System._#PartialFunc4
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty, f#0: HandleType :: 
  { $Is(f#0, Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R)) } 
  $Is(f#0, Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     <==> $Is(f#0, Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
       && (forall x0#0: Box, x1#0: Box, x2#0: Box, x3#0: Box :: 
        $IsBox(x0#0, #$T0)
             && $IsBox(x1#0, #$T1)
             && $IsBox(x2#0, #$T2)
             && $IsBox(x3#0, #$T3)
           ==> Set#Equal(Reads4(#$T0, #$T1, #$T2, #$T3, #$R, $OneHeap, f#0, x0#0, x1#0, x2#0, x3#0), 
            Set#Empty(): Set)));

// $IsAlloc axiom for subset type _System._#PartialFunc4
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty, f#0: HandleType, $h: Heap :: 
  { $IsAlloc(f#0, Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R), $h) } 
  $IsAlloc(f#0, Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R), $h)
     <==> $IsAlloc(f#0, Tclass._System.___hFunc4(#$T0, #$T1, #$T2, #$T3, #$R), $h));

function Tclass._System.___hTotalFunc0(Ty) : Ty;

const unique Tagclass._System.___hTotalFunc0: TyTag;

// Tclass._System.___hTotalFunc0 Tag
axiom (forall #$R: Ty :: 
  { Tclass._System.___hTotalFunc0(#$R) } 
  Tag(Tclass._System.___hTotalFunc0(#$R)) == Tagclass._System.___hTotalFunc0
     && TagFamily(Tclass._System.___hTotalFunc0(#$R)) == tytagFamily$_#TotalFunc0);

function Tclass._System.___hTotalFunc0_0(Ty) : Ty;

// Tclass._System.___hTotalFunc0 injectivity 0
axiom (forall #$R: Ty :: 
  { Tclass._System.___hTotalFunc0(#$R) } 
  Tclass._System.___hTotalFunc0_0(Tclass._System.___hTotalFunc0(#$R)) == #$R);

// Box/unbox axiom for Tclass._System.___hTotalFunc0
axiom (forall #$R: Ty, bx: Box :: 
  { $IsBox(bx, Tclass._System.___hTotalFunc0(#$R)) } 
  $IsBox(bx, Tclass._System.___hTotalFunc0(#$R))
     ==> $Box($Unbox(bx): HandleType) == bx
       && $Is($Unbox(bx): HandleType, Tclass._System.___hTotalFunc0(#$R)));

// $Is axioms for subset type _System._#TotalFunc0
axiom (forall #$R: Ty, f#0: HandleType :: 
  { $Is(f#0, Tclass._System.___hTotalFunc0(#$R)) } 
  $Is(f#0, Tclass._System.___hTotalFunc0(#$R))
     ==> $Is(f#0, Tclass._System.___hPartialFunc0(#$R))
       && 
      (Requires0#canCall(#$R, $OneHeap, f#0)
       && Requires0(#$R, $OneHeap, f#0)));

axiom (forall #$R: Ty, f#0: HandleType :: 
  { $Is(f#0, Tclass._System.___hTotalFunc0(#$R)) } 
  $Is(f#0, Tclass._System.___hPartialFunc0(#$R))
       && (Requires0#canCall(#$R, $OneHeap, f#0) ==> Requires0(#$R, $OneHeap, f#0))
     ==> $Is(f#0, Tclass._System.___hTotalFunc0(#$R)));

// $IsAlloc axiom for subset type _System._#TotalFunc0
axiom (forall #$R: Ty, f#0: HandleType, $h: Heap :: 
  { $IsAlloc(f#0, Tclass._System.___hTotalFunc0(#$R), $h) } 
  $IsAlloc(f#0, Tclass._System.___hTotalFunc0(#$R), $h)
     <==> $IsAlloc(f#0, Tclass._System.___hPartialFunc0(#$R), $h));

function Tclass._System.___hTotalFunc1(Ty, Ty) : Ty;

const unique Tagclass._System.___hTotalFunc1: TyTag;

// Tclass._System.___hTotalFunc1 Tag
axiom (forall #$T0: Ty, #$R: Ty :: 
  { Tclass._System.___hTotalFunc1(#$T0, #$R) } 
  Tag(Tclass._System.___hTotalFunc1(#$T0, #$R)) == Tagclass._System.___hTotalFunc1
     && TagFamily(Tclass._System.___hTotalFunc1(#$T0, #$R)) == tytagFamily$_#TotalFunc1);

function Tclass._System.___hTotalFunc1_0(Ty) : Ty;

// Tclass._System.___hTotalFunc1 injectivity 0
axiom (forall #$T0: Ty, #$R: Ty :: 
  { Tclass._System.___hTotalFunc1(#$T0, #$R) } 
  Tclass._System.___hTotalFunc1_0(Tclass._System.___hTotalFunc1(#$T0, #$R))
     == #$T0);

function Tclass._System.___hTotalFunc1_1(Ty) : Ty;

// Tclass._System.___hTotalFunc1 injectivity 1
axiom (forall #$T0: Ty, #$R: Ty :: 
  { Tclass._System.___hTotalFunc1(#$T0, #$R) } 
  Tclass._System.___hTotalFunc1_1(Tclass._System.___hTotalFunc1(#$T0, #$R)) == #$R);

// Box/unbox axiom for Tclass._System.___hTotalFunc1
axiom (forall #$T0: Ty, #$R: Ty, bx: Box :: 
  { $IsBox(bx, Tclass._System.___hTotalFunc1(#$T0, #$R)) } 
  $IsBox(bx, Tclass._System.___hTotalFunc1(#$T0, #$R))
     ==> $Box($Unbox(bx): HandleType) == bx
       && $Is($Unbox(bx): HandleType, Tclass._System.___hTotalFunc1(#$T0, #$R)));

// $Is axioms for subset type _System._#TotalFunc1
axiom (forall #$T0: Ty, #$R: Ty, f#0: HandleType :: 
  { $Is(f#0, Tclass._System.___hTotalFunc1(#$T0, #$R)) } 
  $Is(f#0, Tclass._System.___hTotalFunc1(#$T0, #$R))
     ==> $Is(f#0, Tclass._System.___hPartialFunc1(#$T0, #$R))
       && 
      ((forall x0#0: Box :: 
        $IsBox(x0#0, #$T0) ==> Requires1#canCall(#$T0, #$R, $OneHeap, f#0, x0#0))
       && (forall x0#0: Box :: 
        $IsBox(x0#0, #$T0) ==> Requires1(#$T0, #$R, $OneHeap, f#0, x0#0))));

axiom (forall #$T0: Ty, #$R: Ty, f#0: HandleType :: 
  { $Is(f#0, Tclass._System.___hTotalFunc1(#$T0, #$R)) } 
  $Is(f#0, Tclass._System.___hPartialFunc1(#$T0, #$R))
       && ((forall x0#0: Box :: 
          $IsBox(x0#0, #$T0) ==> Requires1#canCall(#$T0, #$R, $OneHeap, f#0, x0#0))
         ==> (forall x0#0: Box :: 
          $IsBox(x0#0, #$T0) ==> Requires1(#$T0, #$R, $OneHeap, f#0, x0#0)))
     ==> $Is(f#0, Tclass._System.___hTotalFunc1(#$T0, #$R)));

// $IsAlloc axiom for subset type _System._#TotalFunc1
axiom (forall #$T0: Ty, #$R: Ty, f#0: HandleType, $h: Heap :: 
  { $IsAlloc(f#0, Tclass._System.___hTotalFunc1(#$T0, #$R), $h) } 
  $IsAlloc(f#0, Tclass._System.___hTotalFunc1(#$T0, #$R), $h)
     <==> $IsAlloc(f#0, Tclass._System.___hPartialFunc1(#$T0, #$R), $h));

function Tclass._System.___hTotalFunc3(Ty, Ty, Ty, Ty) : Ty;

const unique Tagclass._System.___hTotalFunc3: TyTag;

// Tclass._System.___hTotalFunc3 Tag
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty :: 
  { Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R) } 
  Tag(Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R))
       == Tagclass._System.___hTotalFunc3
     && TagFamily(Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R))
       == tytagFamily$_#TotalFunc3);

function Tclass._System.___hTotalFunc3_0(Ty) : Ty;

// Tclass._System.___hTotalFunc3 injectivity 0
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty :: 
  { Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R) } 
  Tclass._System.___hTotalFunc3_0(Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R))
     == #$T0);

function Tclass._System.___hTotalFunc3_1(Ty) : Ty;

// Tclass._System.___hTotalFunc3 injectivity 1
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty :: 
  { Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R) } 
  Tclass._System.___hTotalFunc3_1(Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R))
     == #$T1);

function Tclass._System.___hTotalFunc3_2(Ty) : Ty;

// Tclass._System.___hTotalFunc3 injectivity 2
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty :: 
  { Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R) } 
  Tclass._System.___hTotalFunc3_2(Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R))
     == #$T2);

function Tclass._System.___hTotalFunc3_3(Ty) : Ty;

// Tclass._System.___hTotalFunc3 injectivity 3
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty :: 
  { Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R) } 
  Tclass._System.___hTotalFunc3_3(Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R))
     == #$R);

// Box/unbox axiom for Tclass._System.___hTotalFunc3
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty, bx: Box :: 
  { $IsBox(bx, Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R)) } 
  $IsBox(bx, Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R))
     ==> $Box($Unbox(bx): HandleType) == bx
       && $Is($Unbox(bx): HandleType, Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R)));

// $Is axioms for subset type _System._#TotalFunc3
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty, f#0: HandleType :: 
  { $Is(f#0, Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R)) } 
  $Is(f#0, Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R))
     ==> $Is(f#0, Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R))
       && 
      ((forall x0#0: Box, x1#0: Box, x2#0: Box :: 
        $IsBox(x0#0, #$T0) && $IsBox(x1#0, #$T1) && $IsBox(x2#0, #$T2)
           ==> Requires3#canCall(#$T0, #$T1, #$T2, #$R, $OneHeap, f#0, x0#0, x1#0, x2#0))
       && (forall x0#0: Box, x1#0: Box, x2#0: Box :: 
        $IsBox(x0#0, #$T0) && $IsBox(x1#0, #$T1) && $IsBox(x2#0, #$T2)
           ==> Requires3(#$T0, #$T1, #$T2, #$R, $OneHeap, f#0, x0#0, x1#0, x2#0))));

axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty, f#0: HandleType :: 
  { $Is(f#0, Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R)) } 
  $Is(f#0, Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R))
       && ((forall x0#0: Box, x1#0: Box, x2#0: Box :: 
          $IsBox(x0#0, #$T0) && $IsBox(x1#0, #$T1) && $IsBox(x2#0, #$T2)
             ==> Requires3#canCall(#$T0, #$T1, #$T2, #$R, $OneHeap, f#0, x0#0, x1#0, x2#0))
         ==> (forall x0#0: Box, x1#0: Box, x2#0: Box :: 
          $IsBox(x0#0, #$T0) && $IsBox(x1#0, #$T1) && $IsBox(x2#0, #$T2)
             ==> Requires3(#$T0, #$T1, #$T2, #$R, $OneHeap, f#0, x0#0, x1#0, x2#0)))
     ==> $Is(f#0, Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R)));

// $IsAlloc axiom for subset type _System._#TotalFunc3
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$R: Ty, f#0: HandleType, $h: Heap :: 
  { $IsAlloc(f#0, Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R), $h) } 
  $IsAlloc(f#0, Tclass._System.___hTotalFunc3(#$T0, #$T1, #$T2, #$R), $h)
     <==> $IsAlloc(f#0, Tclass._System.___hPartialFunc3(#$T0, #$T1, #$T2, #$R), $h));

function Tclass._System.___hTotalFunc4(Ty, Ty, Ty, Ty, Ty) : Ty;

const unique Tagclass._System.___hTotalFunc4: TyTag;

// Tclass._System.___hTotalFunc4 Tag
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tag(Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
       == Tagclass._System.___hTotalFunc4
     && TagFamily(Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
       == tytagFamily$_#TotalFunc4);

function Tclass._System.___hTotalFunc4_0(Ty) : Ty;

// Tclass._System.___hTotalFunc4 injectivity 0
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tclass._System.___hTotalFunc4_0(Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     == #$T0);

function Tclass._System.___hTotalFunc4_1(Ty) : Ty;

// Tclass._System.___hTotalFunc4 injectivity 1
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tclass._System.___hTotalFunc4_1(Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     == #$T1);

function Tclass._System.___hTotalFunc4_2(Ty) : Ty;

// Tclass._System.___hTotalFunc4 injectivity 2
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tclass._System.___hTotalFunc4_2(Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     == #$T2);

function Tclass._System.___hTotalFunc4_3(Ty) : Ty;

// Tclass._System.___hTotalFunc4 injectivity 3
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tclass._System.___hTotalFunc4_3(Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     == #$T3);

function Tclass._System.___hTotalFunc4_4(Ty) : Ty;

// Tclass._System.___hTotalFunc4 injectivity 4
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty :: 
  { Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R) } 
  Tclass._System.___hTotalFunc4_4(Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     == #$R);

// Box/unbox axiom for Tclass._System.___hTotalFunc4
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty, bx: Box :: 
  { $IsBox(bx, Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R)) } 
  $IsBox(bx, Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     ==> $Box($Unbox(bx): HandleType) == bx
       && $Is($Unbox(bx): HandleType, 
        Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R)));

// $Is axioms for subset type _System._#TotalFunc4
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty, f#0: HandleType :: 
  { $Is(f#0, Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R)) } 
  $Is(f#0, Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
     ==> $Is(f#0, Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
       && 
      ((forall x0#0: Box, x1#0: Box, x2#0: Box, x3#0: Box :: 
        $IsBox(x0#0, #$T0)
             && $IsBox(x1#0, #$T1)
             && $IsBox(x2#0, #$T2)
             && $IsBox(x3#0, #$T3)
           ==> Requires4#canCall(#$T0, #$T1, #$T2, #$T3, #$R, $OneHeap, f#0, x0#0, x1#0, x2#0, x3#0))
       && (forall x0#0: Box, x1#0: Box, x2#0: Box, x3#0: Box :: 
        $IsBox(x0#0, #$T0)
             && $IsBox(x1#0, #$T1)
             && $IsBox(x2#0, #$T2)
             && $IsBox(x3#0, #$T3)
           ==> Requires4(#$T0, #$T1, #$T2, #$T3, #$R, $OneHeap, f#0, x0#0, x1#0, x2#0, x3#0))));

axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty, f#0: HandleType :: 
  { $Is(f#0, Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R)) } 
  $Is(f#0, Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R))
       && ((forall x0#0: Box, x1#0: Box, x2#0: Box, x3#0: Box :: 
          $IsBox(x0#0, #$T0)
               && $IsBox(x1#0, #$T1)
               && $IsBox(x2#0, #$T2)
               && $IsBox(x3#0, #$T3)
             ==> Requires4#canCall(#$T0, #$T1, #$T2, #$T3, #$R, $OneHeap, f#0, x0#0, x1#0, x2#0, x3#0))
         ==> (forall x0#0: Box, x1#0: Box, x2#0: Box, x3#0: Box :: 
          $IsBox(x0#0, #$T0)
               && $IsBox(x1#0, #$T1)
               && $IsBox(x2#0, #$T2)
               && $IsBox(x3#0, #$T3)
             ==> Requires4(#$T0, #$T1, #$T2, #$T3, #$R, $OneHeap, f#0, x0#0, x1#0, x2#0, x3#0)))
     ==> $Is(f#0, Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R)));

// $IsAlloc axiom for subset type _System._#TotalFunc4
axiom (forall #$T0: Ty, #$T1: Ty, #$T2: Ty, #$T3: Ty, #$R: Ty, f#0: HandleType, $h: Heap :: 
  { $IsAlloc(f#0, Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R), $h) } 
  $IsAlloc(f#0, Tclass._System.___hTotalFunc4(#$T0, #$T1, #$T2, #$T3, #$R), $h)
     <==> $IsAlloc(f#0, Tclass._System.___hPartialFunc4(#$T0, #$T1, #$T2, #$T3, #$R), $h));

// Constructor function declaration
function #_System._tuple#0._#Make0() : DatatypeType
uses {
// Constructor identifier
axiom DatatypeCtorId(#_System._tuple#0._#Make0()) == ##_System._tuple#0._#Make0;
// Constructor $Is
axiom $Is(#_System._tuple#0._#Make0(), Tclass._System.Tuple0());
// Constructor literal
axiom #_System._tuple#0._#Make0() == Lit(#_System._tuple#0._#Make0());
}

const unique ##_System._tuple#0._#Make0: DtCtorId
uses {
// Constructor identifier
axiom DatatypeCtorId(#_System._tuple#0._#Make0()) == ##_System._tuple#0._#Make0;
}

function _System.Tuple0.___hMake0_q(DatatypeType) : bool;

// Questionmark and identifier
axiom (forall d: DatatypeType :: 
  { _System.Tuple0.___hMake0_q(d) } 
  _System.Tuple0.___hMake0_q(d)
     <==> DatatypeCtorId(d) == ##_System._tuple#0._#Make0);

// Constructor questionmark has arguments
axiom (forall d: DatatypeType :: 
  { _System.Tuple0.___hMake0_q(d) } 
  _System.Tuple0.___hMake0_q(d) ==> d == #_System._tuple#0._#Make0());

function Tclass._System.Tuple0() : Ty
uses {
// Tclass._System.Tuple0 Tag
axiom Tag(Tclass._System.Tuple0()) == Tagclass._System.Tuple0
   && TagFamily(Tclass._System.Tuple0()) == tytagFamily$_tuple#0;
}

const unique Tagclass._System.Tuple0: TyTag;

// Box/unbox axiom for Tclass._System.Tuple0
axiom (forall bx: Box :: 
  { $IsBox(bx, Tclass._System.Tuple0()) } 
  $IsBox(bx, Tclass._System.Tuple0())
     ==> $Box($Unbox(bx): DatatypeType) == bx
       && $Is($Unbox(bx): DatatypeType, Tclass._System.Tuple0()));

// Datatype $IsAlloc
axiom (forall d: DatatypeType, $h: Heap :: 
  { $IsAlloc(d, Tclass._System.Tuple0(), $h) } 
  $IsGoodHeap($h) && $Is(d, Tclass._System.Tuple0())
     ==> $IsAlloc(d, Tclass._System.Tuple0(), $h));

// Depth-one case-split function
function $IsA#_System.Tuple0(DatatypeType) : bool;

// Depth-one case-split axiom
axiom (forall d: DatatypeType :: 
  { $IsA#_System.Tuple0(d) } 
  $IsA#_System.Tuple0(d) ==> _System.Tuple0.___hMake0_q(d));

// Questionmark data type disjunctivity
axiom (forall d: DatatypeType :: 
  { _System.Tuple0.___hMake0_q(d), $Is(d, Tclass._System.Tuple0()) } 
  $Is(d, Tclass._System.Tuple0()) ==> _System.Tuple0.___hMake0_q(d));

// Datatype extensional equality declaration
function _System.Tuple0#Equal(DatatypeType, DatatypeType) : bool;

// Datatype extensional equality definition: #_System._tuple#0._#Make0
axiom (forall a: DatatypeType, b: DatatypeType :: 
  { _System.Tuple0#Equal(a, b) } 
  _System.Tuple0.___hMake0_q(a) && _System.Tuple0.___hMake0_q(b)
     ==> _System.Tuple0#Equal(a, b));

// Datatype extensionality axiom: _System._tuple#0
axiom (forall a: DatatypeType, b: DatatypeType :: 
  { _System.Tuple0#Equal(a, b) } 
  _System.Tuple0#Equal(a, b) <==> a == b);

const unique class._System.Tuple0: ClassName;

const unique ##_System._tuple#2._#Make2: DtCtorId
uses {
// Constructor identifier
axiom (forall a#4#0#0: Box, a#4#1#0: Box :: 
  { #_System._tuple#2._#Make2(a#4#0#0, a#4#1#0) } 
  DatatypeCtorId(#_System._tuple#2._#Make2(a#4#0#0, a#4#1#0))
     == ##_System._tuple#2._#Make2);
}

function _System.Tuple2.___hMake2_q(DatatypeType) : bool;

// Questionmark and identifier
axiom (forall d: DatatypeType :: 
  { _System.Tuple2.___hMake2_q(d) } 
  _System.Tuple2.___hMake2_q(d)
     <==> DatatypeCtorId(d) == ##_System._tuple#2._#Make2);

// Constructor questionmark has arguments
axiom (forall d: DatatypeType :: 
  { _System.Tuple2.___hMake2_q(d) } 
  _System.Tuple2.___hMake2_q(d)
     ==> (exists a#5#0#0: Box, a#5#1#0: Box :: 
      d == #_System._tuple#2._#Make2(a#5#0#0, a#5#1#0)));

const unique Tagclass._System.Tuple2: TyTag;

// Tclass._System.Tuple2 Tag
axiom (forall _System._tuple#2$T0: Ty, _System._tuple#2$T1: Ty :: 
  { Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1) } 
  Tag(Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1))
       == Tagclass._System.Tuple2
     && TagFamily(Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1))
       == tytagFamily$_tuple#2);

function Tclass._System.Tuple2_0(Ty) : Ty;

// Tclass._System.Tuple2 injectivity 0
axiom (forall _System._tuple#2$T0: Ty, _System._tuple#2$T1: Ty :: 
  { Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1) } 
  Tclass._System.Tuple2_0(Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1))
     == _System._tuple#2$T0);

function Tclass._System.Tuple2_1(Ty) : Ty;

// Tclass._System.Tuple2 injectivity 1
axiom (forall _System._tuple#2$T0: Ty, _System._tuple#2$T1: Ty :: 
  { Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1) } 
  Tclass._System.Tuple2_1(Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1))
     == _System._tuple#2$T1);

// Box/unbox axiom for Tclass._System.Tuple2
axiom (forall _System._tuple#2$T0: Ty, _System._tuple#2$T1: Ty, bx: Box :: 
  { $IsBox(bx, Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1)) } 
  $IsBox(bx, Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1))
     ==> $Box($Unbox(bx): DatatypeType) == bx
       && $Is($Unbox(bx): DatatypeType, 
        Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1)));

// Constructor $Is
axiom (forall _System._tuple#2$T0: Ty, _System._tuple#2$T1: Ty, a#6#0#0: Box, a#6#1#0: Box :: 
  { $Is(#_System._tuple#2._#Make2(a#6#0#0, a#6#1#0), 
      Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1)) } 
  $Is(#_System._tuple#2._#Make2(a#6#0#0, a#6#1#0), 
      Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1))
     <==> $IsBox(a#6#0#0, _System._tuple#2$T0) && $IsBox(a#6#1#0, _System._tuple#2$T1));

// Constructor $IsAlloc
axiom (forall _System._tuple#2$T0: Ty, 
    _System._tuple#2$T1: Ty, 
    a#6#0#0: Box, 
    a#6#1#0: Box, 
    $h: Heap :: 
  { $IsAlloc(#_System._tuple#2._#Make2(a#6#0#0, a#6#1#0), 
      Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1), 
      $h) } 
  $IsGoodHeap($h)
     ==> ($IsAlloc(#_System._tuple#2._#Make2(a#6#0#0, a#6#1#0), 
        Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1), 
        $h)
       <==> $IsAllocBox(a#6#0#0, _System._tuple#2$T0, $h)
         && $IsAllocBox(a#6#1#0, _System._tuple#2$T1, $h)));

// Destructor $IsAlloc
axiom (forall d: DatatypeType, _System._tuple#2$T0: Ty, $h: Heap :: 
  { $IsAllocBox(_System.Tuple2._0(d), _System._tuple#2$T0, $h) } 
  $IsGoodHeap($h)
       && 
      (_System.Tuple2.___hMake2_q(d)
       && (exists _System._tuple#2$T1: Ty :: 
        { $IsAlloc(d, Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1), $h) } 
        $IsAlloc(d, Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1), $h)))
     ==> $IsAllocBox(_System.Tuple2._0(d), _System._tuple#2$T0, $h));

// Destructor $IsAlloc
axiom (forall d: DatatypeType, _System._tuple#2$T1: Ty, $h: Heap :: 
  { $IsAllocBox(_System.Tuple2._1(d), _System._tuple#2$T1, $h) } 
  $IsGoodHeap($h)
       && 
      (_System.Tuple2.___hMake2_q(d)
       && (exists _System._tuple#2$T0: Ty :: 
        { $IsAlloc(d, Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1), $h) } 
        $IsAlloc(d, Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1), $h)))
     ==> $IsAllocBox(_System.Tuple2._1(d), _System._tuple#2$T1, $h));

// Constructor literal
axiom (forall a#7#0#0: Box, a#7#1#0: Box :: 
  { #_System._tuple#2._#Make2(Lit(a#7#0#0), Lit(a#7#1#0)) } 
  #_System._tuple#2._#Make2(Lit(a#7#0#0), Lit(a#7#1#0))
     == Lit(#_System._tuple#2._#Make2(a#7#0#0, a#7#1#0)));

// Constructor injectivity
axiom (forall a#8#0#0: Box, a#8#1#0: Box :: 
  { #_System._tuple#2._#Make2(a#8#0#0, a#8#1#0) } 
  _System.Tuple2._0(#_System._tuple#2._#Make2(a#8#0#0, a#8#1#0)) == a#8#0#0);

// Inductive rank
axiom (forall a#9#0#0: Box, a#9#1#0: Box :: 
  { DtRank(#_System._tuple#2._#Make2(a#9#0#0, a#9#1#0)) } 
  BoxRank(a#9#0#0) < DtRank(#_System._tuple#2._#Make2(a#9#0#0, a#9#1#0)));

// Constructor injectivity
axiom (forall a#10#0#0: Box, a#10#1#0: Box :: 
  { #_System._tuple#2._#Make2(a#10#0#0, a#10#1#0) } 
  _System.Tuple2._1(#_System._tuple#2._#Make2(a#10#0#0, a#10#1#0)) == a#10#1#0);

// Inductive rank
axiom (forall a#11#0#0: Box, a#11#1#0: Box :: 
  { DtRank(#_System._tuple#2._#Make2(a#11#0#0, a#11#1#0)) } 
  BoxRank(a#11#1#0) < DtRank(#_System._tuple#2._#Make2(a#11#0#0, a#11#1#0)));

// Depth-one case-split function
function $IsA#_System.Tuple2(DatatypeType) : bool;

// Depth-one case-split axiom
axiom (forall d: DatatypeType :: 
  { $IsA#_System.Tuple2(d) } 
  $IsA#_System.Tuple2(d) ==> _System.Tuple2.___hMake2_q(d));

// Questionmark data type disjunctivity
axiom (forall _System._tuple#2$T0: Ty, _System._tuple#2$T1: Ty, d: DatatypeType :: 
  { _System.Tuple2.___hMake2_q(d), $Is(d, Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1)) } 
  $Is(d, Tclass._System.Tuple2(_System._tuple#2$T0, _System._tuple#2$T1))
     ==> _System.Tuple2.___hMake2_q(d));

// Datatype extensional equality declaration
function _System.Tuple2#Equal(DatatypeType, DatatypeType) : bool;

// Datatype extensional equality definition: #_System._tuple#2._#Make2
axiom (forall a: DatatypeType, b: DatatypeType :: 
  { _System.Tuple2#Equal(a, b) } 
  (_System.Tuple2#Equal(a, b)
       ==> _System.Tuple2._0(a) == _System.Tuple2._0(b)
         && _System.Tuple2._1(a) == _System.Tuple2._1(b))
     && (_System.Tuple2.___hMake2_q(a)
         && _System.Tuple2.___hMake2_q(b)
         && 
        (_System.Tuple2._0(a) == _System.Tuple2._0(b)
         && _System.Tuple2._1(a) == _System.Tuple2._1(b))
       ==> _System.Tuple2#Equal(a, b)));

// Datatype extensionality axiom: _System._tuple#2
axiom (forall a: DatatypeType, b: DatatypeType :: 
  { _System.Tuple2#Equal(a, b) } 
  _System.Tuple2#Equal(a, b) <==> a == b);

const unique class._System.Tuple2: ClassName;

const unique class._System.array?: ClassName;

function Tclass._System.array?(Ty) : Ty;

const unique Tagclass._System.array?: TyTag;

// Tclass._System.array? Tag
axiom (forall _System.array$arg: Ty :: 
  { Tclass._System.array?(_System.array$arg) } 
  Tag(Tclass._System.array?(_System.array$arg)) == Tagclass._System.array?
     && TagFamily(Tclass._System.array?(_System.array$arg)) == tytagFamily$array);

function Tclass._System.array?_0(Ty) : Ty;

// Tclass._System.array? injectivity 0
axiom (forall _System.array$arg: Ty :: 
  { Tclass._System.array?(_System.array$arg) } 
  Tclass._System.array?_0(Tclass._System.array?(_System.array$arg))
     == _System.array$arg);

// Box/unbox axiom for Tclass._System.array?
axiom (forall _System.array$arg: Ty, bx: Box :: 
  { $IsBox(bx, Tclass._System.array?(_System.array$arg)) } 
  $IsBox(bx, Tclass._System.array?(_System.array$arg))
     ==> $Box($Unbox(bx): ref) == bx
       && $Is($Unbox(bx): ref, Tclass._System.array?(_System.array$arg)));

// array.: Type axiom
axiom (forall _System.array$arg: Ty, $h: Heap, $o: ref, $i0: int :: 
  { read($h, $o, IndexField($i0)), Tclass._System.array?(_System.array$arg) } 
  $IsGoodHeap($h)
       && 
      ($o != null
       && dtype($o) == Tclass._System.array?(_System.array$arg))
       && 
      (0 <= $i0
       && $i0 < _System.array.Length($o))
     ==> $IsBox(read($h, $o, IndexField($i0)), _System.array$arg));

// array.: Allocation axiom
axiom (forall _System.array$arg: Ty, $h: Heap, $o: ref, $i0: int :: 
  { read($h, $o, IndexField($i0)), Tclass._System.array?(_System.array$arg) } 
  $IsGoodHeap($h)
       && 
      ($o != null
       && dtype($o) == Tclass._System.array?(_System.array$arg))
       && 
      (0 <= $i0
       && $i0 < _System.array.Length($o))
       && $Unbox(read($h, $o, alloc)): bool
     ==> $IsAllocBox(read($h, $o, IndexField($i0)), _System.array$arg, $h));

// $Is axiom for array type array
axiom (forall _System.array$arg: Ty, $o: ref :: 
  { $Is($o, Tclass._System.array?(_System.array$arg)) } 
  $Is($o, Tclass._System.array?(_System.array$arg))
     <==> $o == null || dtype($o) == Tclass._System.array?(_System.array$arg));

// $IsAlloc axiom for array type array
axiom (forall _System.array$arg: Ty, $o: ref, $h: Heap :: 
  { $IsAlloc($o, Tclass._System.array?(_System.array$arg), $h) } 
  $IsAlloc($o, Tclass._System.array?(_System.array$arg), $h)
     <==> $o == null || $Unbox(read($h, $o, alloc)): bool);

// array.Length: Type axiom
axiom (forall _System.array$arg: Ty, $o: ref :: 
  { _System.array.Length($o), Tclass._System.array?(_System.array$arg) } 
  $o != null && dtype($o) == Tclass._System.array?(_System.array$arg)
     ==> $Is(_System.array.Length($o), TInt));

// array.Length: Allocation axiom
axiom (forall _System.array$arg: Ty, $h: Heap, $o: ref :: 
  { _System.array.Length($o), $Unbox(read($h, $o, alloc)): bool, Tclass._System.array?(_System.array$arg) } 
  $IsGoodHeap($h)
       && 
      ($o != null
       && dtype($o) == Tclass._System.array?(_System.array$arg))
       && $Unbox(read($h, $o, alloc)): bool
     ==> $IsAlloc(_System.array.Length($o), TInt, $h));

function Tclass._System.array(Ty) : Ty;

const unique Tagclass._System.array: TyTag;

// Tclass._System.array Tag
axiom (forall _System.array$arg: Ty :: 
  { Tclass._System.array(_System.array$arg) } 
  Tag(Tclass._System.array(_System.array$arg)) == Tagclass._System.array
     && TagFamily(Tclass._System.array(_System.array$arg)) == tytagFamily$array);

function Tclass._System.array_0(Ty) : Ty;

// Tclass._System.array injectivity 0
axiom (forall _System.array$arg: Ty :: 
  { Tclass._System.array(_System.array$arg) } 
  Tclass._System.array_0(Tclass._System.array(_System.array$arg))
     == _System.array$arg);

// Box/unbox axiom for Tclass._System.array
axiom (forall _System.array$arg: Ty, bx: Box :: 
  { $IsBox(bx, Tclass._System.array(_System.array$arg)) } 
  $IsBox(bx, Tclass._System.array(_System.array$arg))
     ==> $Box($Unbox(bx): ref) == bx
       && $Is($Unbox(bx): ref, Tclass._System.array(_System.array$arg)));

// $Is axiom for non-null type _System.array
axiom (forall _System.array$arg: Ty, c#0: ref :: 
  { $Is(c#0, Tclass._System.array(_System.array$arg)) } 
    { $Is(c#0, Tclass._System.array?(_System.array$arg)) } 
  $Is(c#0, Tclass._System.array(_System.array$arg))
     <==> $Is(c#0, Tclass._System.array?(_System.array$arg)) && c#0 != null);

// $IsAlloc axiom for non-null type _System.array
axiom (forall _System.array$arg: Ty, c#0: ref, $h: Heap :: 
  { $IsAlloc(c#0, Tclass._System.array(_System.array$arg), $h) } 
  $IsAlloc(c#0, Tclass._System.array(_System.array$arg), $h)
     <==> $IsAlloc(c#0, Tclass._System.array?(_System.array$arg), $h));

function Tclass._System.nat() : Ty
uses {
// Tclass._System.nat Tag
axiom Tag(Tclass._System.nat()) == Tagclass._System.nat
   && TagFamily(Tclass._System.nat()) == tytagFamily$nat;
}

const unique Tagclass._System.nat: TyTag;

// Box/unbox axiom for Tclass._System.nat
axiom (forall bx: Box :: 
  { $IsBox(bx, Tclass._System.nat()) } 
  $IsBox(bx, Tclass._System.nat())
     ==> $Box($Unbox(bx): int) == bx && $Is($Unbox(bx): int, Tclass._System.nat()));

// $Is axiom for subset type _System.nat
axiom (forall x#0: int :: 
  { $Is(x#0, Tclass._System.nat()) } 
  $Is(x#0, Tclass._System.nat()) <==> LitInt(0) <= x#0);

// $IsAlloc axiom for subset type _System.nat
axiom (forall x#0: int, $h: Heap :: 
  { $IsAlloc(x#0, Tclass._System.nat(), $h) } 
  $IsAlloc(x#0, Tclass._System.nat(), $h));

const unique class._System.object?: ClassName;

const unique Tagclass._System.object?: TyTag;

// Box/unbox axiom for Tclass._System.object?
axiom (forall bx: Box :: 
  { $IsBox(bx, Tclass._System.object?()) } 
  $IsBox(bx, Tclass._System.object?())
     ==> $Box($Unbox(bx): ref) == bx && $Is($Unbox(bx): ref, Tclass._System.object?()));

// $Is axiom for trait object
axiom (forall $o: ref :: 
  { $Is($o, Tclass._System.object?()) } 
  $Is($o, Tclass._System.object?()));

// $IsAlloc axiom for trait object
axiom (forall $o: ref, $h: Heap :: 
  { $IsAlloc($o, Tclass._System.object?(), $h) } 
  $IsAlloc($o, Tclass._System.object?(), $h)
     <==> $o == null || $Unbox(read($h, $o, alloc)): bool);

function implements$_System.object(ty: Ty) : bool;

function Tclass._System.object() : Ty
uses {
// Tclass._System.object Tag
axiom Tag(Tclass._System.object()) == Tagclass._System.object
   && TagFamily(Tclass._System.object()) == tytagFamily$object;
}

const unique Tagclass._System.object: TyTag;

// Box/unbox axiom for Tclass._System.object
axiom (forall bx: Box :: 
  { $IsBox(bx, Tclass._System.object()) } 
  $IsBox(bx, Tclass._System.object())
     ==> $Box($Unbox(bx): ref) == bx && $Is($Unbox(bx): ref, Tclass._System.object()));

// $Is axiom for non-null type _System.object
axiom (forall c#0: ref :: 
  { $Is(c#0, Tclass._System.object()) } { $Is(c#0, Tclass._System.object?()) } 
  $Is(c#0, Tclass._System.object())
     <==> $Is(c#0, Tclass._System.object?()) && c#0 != null);

// $IsAlloc axiom for non-null type _System.object
axiom (forall c#0: ref, $h: Heap :: 
  { $IsAlloc(c#0, Tclass._System.object(), $h) } 
  $IsAlloc(c#0, Tclass._System.object(), $h)
     <==> $IsAlloc(c#0, Tclass._System.object?(), $h));

const unique class._module.__default: ClassName;

function Tclass._module.Node() : Ty
uses {
// Tclass._module.Node Tag
axiom Tag(Tclass._module.Node()) == Tagclass._module.Node
   && TagFamily(Tclass._module.Node()) == tytagFamily$Node;
}

const unique Tagclass._module.Node: TyTag;

// Box/unbox axiom for Tclass._module.Node
axiom (forall bx: Box :: 
  { $IsBox(bx, Tclass._module.Node()) } 
  $IsBox(bx, Tclass._module.Node())
     ==> $Box($Unbox(bx): ref) == bx && $Is($Unbox(bx): ref, Tclass._module.Node()));

procedure {:verboseName "RecursiveMark (well-formedness)"} CheckWellFormed$$_module.__default.RecursiveMark(root#0: ref
       where $Is(root#0, Tclass._module.Node())
         && $IsAlloc(root#0, Tclass._module.Node(), $Heap), 
    S#0: Set
       where $Is(S#0, TSet(Tclass._module.Node()))
         && $IsAlloc(S#0, TSet(Tclass._module.Node()), $Heap));
  modifies $Heap;



function Tclass._module.Node?() : Ty
uses {
// Tclass._module.Node? Tag
axiom Tag(Tclass._module.Node?()) == Tagclass._module.Node?
   && TagFamily(Tclass._module.Node?()) == tytagFamily$Node;
}

const unique Tagclass._module.Node?: TyTag;

// Box/unbox axiom for Tclass._module.Node?
axiom (forall bx: Box :: 
  { $IsBox(bx, Tclass._module.Node?()) } 
  $IsBox(bx, Tclass._module.Node?())
     ==> $Box($Unbox(bx): ref) == bx && $Is($Unbox(bx): ref, Tclass._module.Node?()));

implementation {:smt_option "smt.refine_inj_axioms", "false"} {:smt_option "smt.arith.solver", "2"} {:verboseName "RecursiveMark (well-formedness)"} CheckWellFormed$$_module.__default.RecursiveMark(root#0: ref, S#0: Set)
{
  var $_ModifiesFrame: [ref,Field]bool;
  var n#0: ref;
  var ch#0: ref;
  var newtype$check#0: ref;
  var n#2: ref;
  var n#4: ref;
  var ch#3: ref;
  var newtype$check#1: ref;
  var n#6: ref;


    // AddMethodImpl: RecursiveMark, CheckWellFormed$$_module.__default.RecursiveMark
    $_ModifiesFrame := (lambda $o: ref, $f: Field :: 
      $o != null && $Unbox(read($Heap, $o, alloc)): bool
         ==> Set#IsMember(S#0, $Box($o)));
    assume {:captureState "module:_module:state:0"} true;
    assume {:id "id0"} Set#IsMember(S#0, $Box(root#0));
    havoc n#0;
    assume $Is(n#0, Tclass._module.Node()) && $IsAlloc(n#0, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id1"} Set#IsMember(S#0, $Box(n#0));
        havoc ch#0;
        assume $Is(ch#0, Tclass._module.Node?())
           && $IsAlloc(ch#0, Tclass._module.Node?(), $Heap);
        if (*)
        {
            assert {:id "id2"} n#0 != null;
            assume true;
            assume {:id "id3"} Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#0));
            if (*)
            {
                newtype$check#0 := null;
                assume {:id "id4"} ch#0 == null;
            }
            else
            {
                assume true;
                assume {:id "id5"} ch#0 != null;
                assume {:id "id6"} Set#IsMember(S#0, $Box(ch#0));
            }
        }
        else
        {
            assume true;
            assume {:id "id7"} Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#0))
               ==> ch#0 == null || Set#IsMember(S#0, $Box(ch#0));
        }

        assume {:id "id8"} (forall ch#1: ref :: 
          { Set#IsMember(S#0, $Box(ch#1)) } 
            { Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#1)) } 
          $Is(ch#1, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#1))
             ==> ch#1 == null || Set#IsMember(S#0, $Box(ch#1)));
    }
    else
    {
        assume true;
        assume {:id "id9"} Set#IsMember(S#0, $Box(n#0))
           ==> (forall ch#1: ref :: 
            { Set#IsMember(S#0, $Box(ch#1)) } 
              { Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#1)) } 
            $Is(ch#1, Tclass._module.Node?())
               ==> 
              Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#1))
               ==> ch#1 == null || Set#IsMember(S#0, $Box(ch#1)));
    }

    assume {:id "id10"} (forall n#1: ref :: 
      { read($Heap, n#1, _module.Node.children) } { Set#IsMember(S#0, $Box(n#1)) } 
      $Is(n#1, Tclass._module.Node())
         ==> 
        Set#IsMember(S#0, $Box(n#1))
         ==> (forall ch#2: ref :: 
          { Set#IsMember(S#0, $Box(ch#2)) } 
            { Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2)) } 
          $Is(ch#2, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2))
             ==> ch#2 == null || Set#IsMember(S#0, $Box(ch#2))));
    havoc n#2;
    assume $Is(n#2, Tclass._module.Node()) && $IsAlloc(n#2, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id11"} Set#IsMember(S#0, $Box(n#2));
        assert {:id "id12"} n#2 != null;
        assume true;
        assume {:id "id13"} !$Unbox(read($Heap, n#2, _module.Node.marked)): bool;
        assert {:id "id14"} n#2 != null;
        assume true;
        assume {:id "id15"} $Unbox(read($Heap, n#2, _module.Node.childrenVisited)): int == LitInt(0);
    }
    else
    {
        assume true;
        assume {:id "id16"} Set#IsMember(S#0, $Box(n#2))
           ==> !$Unbox(read($Heap, n#2, _module.Node.marked)): bool
             && $Unbox(read($Heap, n#2, _module.Node.childrenVisited)): int == LitInt(0);
    }

    assume {:id "id17"} (forall n#3: ref :: 
      { read($Heap, n#3, _module.Node.childrenVisited) } 
        { read($Heap, n#3, _module.Node.marked) } 
        { Set#IsMember(S#0, $Box(n#3)) } 
      $Is(n#3, Tclass._module.Node())
         ==> (Set#IsMember(S#0, $Box(n#3))
             ==> !$Unbox(read($Heap, n#3, _module.Node.marked)): bool)
           && (Set#IsMember(S#0, $Box(n#3))
             ==> $Unbox(read($Heap, n#3, _module.Node.childrenVisited)): int == LitInt(0)));
    havoc $Heap;
    assume (forall $o: ref :: 
      { $Heap[$o] } 
      $o != null && $Unbox(read(old($Heap), $o, alloc)): bool
         ==> $Heap[$o] == old($Heap)[$o] || Set#IsMember(S#0, $Box($o)));
    assume $HeapSucc(old($Heap), $Heap);
    assume {:captureState "module:_module:state:1"} true;
    assert {:id "id18"} root#0 != null;
    assume true;
    assume {:id "id19"} $Unbox(read($Heap, root#0, _module.Node.marked)): bool;
    havoc n#4;
    assume $Is(n#4, Tclass._module.Node()) && $IsAlloc(n#4, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id20"} Set#IsMember(S#0, $Box(n#4));
        assert {:id "id21"} n#4 != null;
        assume true;
        assume {:id "id22"} $Unbox(read($Heap, n#4, _module.Node.marked)): bool;
        havoc ch#3;
        assume $Is(ch#3, Tclass._module.Node?())
           && $IsAlloc(ch#3, Tclass._module.Node?(), $Heap);
        if (*)
        {
            assert {:id "id23"} n#4 != null;
            assume true;
            assume {:id "id24"} Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#3));
            newtype$check#1 := null;
            assume {:id "id25"} ch#3 != null;
            assert {:id "id26"} ch#3 != null;
            assume true;
            assume {:id "id27"} $Unbox(read($Heap, ch#3, _module.Node.marked)): bool;
        }
        else
        {
            assume true;
            assume {:id "id28"} Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#3))
                 && ch#3 != null
               ==> $Unbox(read($Heap, ch#3, _module.Node.marked)): bool;
        }

        assume {:id "id29"} (forall ch#4: ref :: 
          { read($Heap, ch#4, _module.Node.marked) } 
            { Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#4)) } 
          $Is(ch#4, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#4))
               && ch#4 != null
             ==> $Unbox(read($Heap, ch#4, _module.Node.marked)): bool);
    }
    else
    {
        assume true;
        assume {:id "id30"} Set#IsMember(S#0, $Box(n#4))
             && $Unbox(read($Heap, n#4, _module.Node.marked)): bool
           ==> (forall ch#4: ref :: 
            { read($Heap, ch#4, _module.Node.marked) } 
              { Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#4)) } 
            $Is(ch#4, Tclass._module.Node?())
               ==> 
              Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#4))
                 && ch#4 != null
               ==> $Unbox(read($Heap, ch#4, _module.Node.marked)): bool);
    }

    assume {:id "id31"} (forall n#5: ref :: 
      { read($Heap, n#5, _module.Node.children) } 
        { read($Heap, n#5, _module.Node.marked) } 
        { Set#IsMember(S#0, $Box(n#5)) } 
      $Is(n#5, Tclass._module.Node())
         ==> 
        Set#IsMember(S#0, $Box(n#5))
           && $Unbox(read($Heap, n#5, _module.Node.marked)): bool
         ==> (forall ch#5: ref :: 
          { read($Heap, ch#5, _module.Node.marked) } 
            { Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5)) } 
          $Is(ch#5, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5))
               && ch#5 != null
             ==> $Unbox(read($Heap, ch#5, _module.Node.marked)): bool));
    havoc n#6;
    assume $Is(n#6, Tclass._module.Node()) && $IsAlloc(n#6, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id32"} Set#IsMember(S#0, $Box(n#6));
        assert {:id "id33"} n#6 != null;
        assume true;
        assert {:id "id34"} n#6 != null;
        assert {:id "id35"} $IsAlloc(n#6, Tclass._module.Node(), old($Heap));
        assume true;
        assume {:id "id36"} $Unbox(read($Heap, n#6, _module.Node.childrenVisited)): int
           == $Unbox(read(old($Heap), n#6, _module.Node.childrenVisited)): int;
        assert {:id "id37"} n#6 != null;
        assume true;
        assert {:id "id38"} n#6 != null;
        assert {:id "id39"} $IsAlloc(n#6, Tclass._module.Node(), old($Heap));
        assume true;
        assume {:id "id40"} Seq#Equal($Unbox(read($Heap, n#6, _module.Node.children)): Seq, 
          $Unbox(read(old($Heap), n#6, _module.Node.children)): Seq);
    }
    else
    {
        assume true;
        assume {:id "id41"} Set#IsMember(S#0, $Box(n#6))
           ==> $Unbox(read($Heap, n#6, _module.Node.childrenVisited)): int
               == $Unbox(read(old($Heap), n#6, _module.Node.childrenVisited)): int
             && Seq#Equal($Unbox(read($Heap, n#6, _module.Node.children)): Seq, 
              $Unbox(read(old($Heap), n#6, _module.Node.children)): Seq);
    }

    assume {:id "id42"} (forall n#7: ref :: 
      { read(old($Heap), n#7, _module.Node.children) } 
        { read($Heap, n#7, _module.Node.children) } 
        { read(old($Heap), n#7, _module.Node.childrenVisited) } 
        { read($Heap, n#7, _module.Node.childrenVisited) } 
        { Set#IsMember(S#0, $Box(n#7)) } 
      $Is(n#7, Tclass._module.Node())
         ==> (Set#IsMember(S#0, $Box(n#7))
             ==> $Unbox(read($Heap, n#7, _module.Node.childrenVisited)): int
               == $Unbox(read(old($Heap), n#7, _module.Node.childrenVisited)): int)
           && (Set#IsMember(S#0, $Box(n#7))
             ==> Seq#Equal($Unbox(read($Heap, n#7, _module.Node.children)): Seq, 
              $Unbox(read(old($Heap), n#7, _module.Node.children)): Seq)));
}



procedure {:verboseName "RecursiveMark (call)"} Call$$_module.__default.RecursiveMark(root#0: ref
       where $Is(root#0, Tclass._module.Node())
         && $IsAlloc(root#0, Tclass._module.Node(), $Heap), 
    S#0: Set
       where $Is(S#0, TSet(Tclass._module.Node()))
         && $IsAlloc(S#0, TSet(Tclass._module.Node()), $Heap));
  // user-defined preconditions
  free requires {:always_assume} true;
  requires {:id "id43"} Set#IsMember(S#0, $Box(root#0));
  free requires {:always_assume} true;
  requires {:id "id44"} (forall n#1: ref :: 
    { read($Heap, n#1, _module.Node.children) } { Set#IsMember(S#0, $Box(n#1)) } 
    $Is(n#1, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#1))
       ==> (forall ch#2: ref :: 
        { Set#IsMember(S#0, $Box(ch#2)) } 
          { Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2)) } 
        $Is(ch#2, Tclass._module.Node?())
           ==> 
          Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2))
           ==> ch#2 == null || Set#IsMember(S#0, $Box(ch#2))));
  free requires {:always_assume} true;
  requires {:id "id45"} (forall n#3: ref :: 
    { read($Heap, n#3, _module.Node.childrenVisited) } 
      { read($Heap, n#3, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#3)) } 
    $Is(n#3, Tclass._module.Node())
       ==> (Set#IsMember(S#0, $Box(n#3))
           ==> !$Unbox(read($Heap, n#3, _module.Node.marked)): bool)
         && (Set#IsMember(S#0, $Box(n#3))
           ==> $Unbox(read($Heap, n#3, _module.Node.childrenVisited)): int == LitInt(0)));
  // user-defined frame expressions
  free requires {:always_assume} true;
  modifies $Heap;
  // user-defined postconditions
  free ensures {:always_assume} true;
  ensures {:id "id46"} $Unbox(read($Heap, root#0, _module.Node.marked)): bool;
  free ensures {:always_assume} true;
  ensures {:id "id47"} (forall n#5: ref :: 
    { read($Heap, n#5, _module.Node.children) } 
      { read($Heap, n#5, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#5)) } 
    $Is(n#5, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#5))
         && $Unbox(read($Heap, n#5, _module.Node.marked)): bool
       ==> (forall ch#5: ref :: 
        { read($Heap, ch#5, _module.Node.marked) } 
          { Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5)) } 
        $Is(ch#5, Tclass._module.Node?())
           ==> 
          Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5))
             && ch#5 != null
           ==> $Unbox(read($Heap, ch#5, _module.Node.marked)): bool));
  free ensures {:always_assume} true;
  ensures {:id "id48"} (forall n#7: ref :: 
    { read(old($Heap), n#7, _module.Node.children) } 
      { read($Heap, n#7, _module.Node.children) } 
      { read(old($Heap), n#7, _module.Node.childrenVisited) } 
      { read($Heap, n#7, _module.Node.childrenVisited) } 
      { Set#IsMember(S#0, $Box(n#7)) } 
    $Is(n#7, Tclass._module.Node())
       ==> (Set#IsMember(S#0, $Box(n#7))
           ==> $Unbox(read($Heap, n#7, _module.Node.childrenVisited)): int
             == $Unbox(read(old($Heap), n#7, _module.Node.childrenVisited)): int)
         && (Set#IsMember(S#0, $Box(n#7))
           ==> Seq#Equal($Unbox(read($Heap, n#7, _module.Node.children)): Seq, 
            $Unbox(read(old($Heap), n#7, _module.Node.children)): Seq)));
  // frame condition: object granularity
  free ensures (forall $o: ref :: 
    { $Heap[$o] } 
    $o != null && $Unbox(read(old($Heap), $o, alloc)): bool
       ==> $Heap[$o] == old($Heap)[$o] || Set#IsMember(S#0, $Box($o)));
  // boilerplate
  free ensures $HeapSucc(old($Heap), $Heap);



procedure {:verboseName "RecursiveMark (correctness)"} Impl$$_module.__default.RecursiveMark(root#0: ref
       where $Is(root#0, Tclass._module.Node())
         && $IsAlloc(root#0, Tclass._module.Node(), $Heap), 
    S#0: Set
       where $Is(S#0, TSet(Tclass._module.Node()))
         && $IsAlloc(S#0, TSet(Tclass._module.Node()), $Heap))
   returns ($_reverifyPost: bool);
  // user-defined preconditions
  free requires {:always_assume} true;
  requires {:id "id49"} Set#IsMember(S#0, $Box(root#0));
  free requires {:always_assume} true;
  requires {:id "id50"} (forall n#1: ref :: 
    { read($Heap, n#1, _module.Node.children) } { Set#IsMember(S#0, $Box(n#1)) } 
    $Is(n#1, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#1))
       ==> (forall ch#2: ref :: 
        { Set#IsMember(S#0, $Box(ch#2)) } 
          { Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2)) } 
        $Is(ch#2, Tclass._module.Node?())
           ==> 
          Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2))
           ==> ch#2 == null || Set#IsMember(S#0, $Box(ch#2))));
  free requires {:always_assume} true;
  requires {:id "id51"} (forall n#3: ref :: 
    { read($Heap, n#3, _module.Node.childrenVisited) } 
      { read($Heap, n#3, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#3)) } 
    $Is(n#3, Tclass._module.Node())
       ==> (Set#IsMember(S#0, $Box(n#3))
           ==> !$Unbox(read($Heap, n#3, _module.Node.marked)): bool)
         && (Set#IsMember(S#0, $Box(n#3))
           ==> $Unbox(read($Heap, n#3, _module.Node.childrenVisited)): int == LitInt(0)));
  // user-defined frame expressions
  free requires {:always_assume} true;
  modifies $Heap;
  // user-defined postconditions
  free ensures {:always_assume} true;
  ensures {:id "id52"} $Unbox(read($Heap, root#0, _module.Node.marked)): bool;
  free ensures {:always_assume} true;
  ensures {:id "id53"} (forall n#5: ref :: 
    { read($Heap, n#5, _module.Node.children) } 
      { read($Heap, n#5, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#5)) } 
    $Is(n#5, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#5))
         && $Unbox(read($Heap, n#5, _module.Node.marked)): bool
       ==> (forall ch#5: ref :: 
        { read($Heap, ch#5, _module.Node.marked) } 
          { Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5)) } 
        $Is(ch#5, Tclass._module.Node?())
           ==> 
          Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5))
             && ch#5 != null
           ==> $Unbox(read($Heap, ch#5, _module.Node.marked)): bool));
  free ensures {:always_assume} true;
  ensures {:id "id54"} (forall n#7: ref :: 
    { read(old($Heap), n#7, _module.Node.children) } 
      { read($Heap, n#7, _module.Node.children) } 
      { read(old($Heap), n#7, _module.Node.childrenVisited) } 
      { read($Heap, n#7, _module.Node.childrenVisited) } 
      { Set#IsMember(S#0, $Box(n#7)) } 
    $Is(n#7, Tclass._module.Node())
       ==> (Set#IsMember(S#0, $Box(n#7))
           ==> $Unbox(read($Heap, n#7, _module.Node.childrenVisited)): int
             == $Unbox(read(old($Heap), n#7, _module.Node.childrenVisited)): int)
         && (Set#IsMember(S#0, $Box(n#7))
           ==> Seq#Equal($Unbox(read($Heap, n#7, _module.Node.children)): Seq, 
            $Unbox(read(old($Heap), n#7, _module.Node.children)): Seq)));
  // frame condition: object granularity
  free ensures (forall $o: ref :: 
    { $Heap[$o] } 
    $o != null && $Unbox(read(old($Heap), $o, alloc)): bool
       ==> $Heap[$o] == old($Heap)[$o] || Set#IsMember(S#0, $Box($o)));
  // boilerplate
  free ensures $HeapSucc(old($Heap), $Heap);



implementation {:smt_option "smt.refine_inj_axioms", "false"} {:smt_option "smt.arith.solver", "2"} {:verboseName "RecursiveMark (correctness)"} Impl$$_module.__default.RecursiveMark(root#0: ref, S#0: Set) returns ($_reverifyPost: bool)
{
  var $_ModifiesFrame: [ref,Field]bool;
  var root##0: ref;
  var S##0: Set;
  var stackNodes##0: Set;

    // AddMethodImpl: RecursiveMark, Impl$$_module.__default.RecursiveMark
    $_ModifiesFrame := (lambda $o: ref, $f: Field :: 
      $o != null && $Unbox(read($Heap, $o, alloc)): bool
         ==> Set#IsMember(S#0, $Box($o)));
    assume {:captureState "module:_module:state:2"} true;
    $_reverifyPost := false;
    // ----- call statement ----- module:_module:statement:0
    // TrCallStmt: Before ProcessCallStmt
    assume true;
    // ProcessCallStmt: CheckSubrange
    root##0 := root#0;
    assume true;
    // ProcessCallStmt: CheckSubrange
    S##0 := S#0;
    assume true;
    // ProcessCallStmt: CheckSubrange
    stackNodes##0 := Lit(Set#Empty(): Set);
    assume true;
    assert {:id "id55"} (forall $o: ref, $f: Field :: 
      $o != null
           && $Unbox(read($Heap, $o, alloc)): bool
           && Set#IsMember(S##0, $Box($o))
         ==> $_ModifiesFrame[$o, $f]);
    call {:id "id56"} Call$$_module.__default.RecursiveMarkWorker(root##0, S##0, stackNodes##0);
    // TrCallStmt: After ProcessCallStmt
    assume {:captureState "module:_module:state:3"} true;
}



procedure {:verboseName "RecursiveMarkWorker (well-formedness)"} CheckWellFormed$$_module.__default.RecursiveMarkWorker(root#0: ref
       where $Is(root#0, Tclass._module.Node())
         && $IsAlloc(root#0, Tclass._module.Node(), $Heap), 
    S#0: Set
       where $Is(S#0, TSet(Tclass._module.Node()))
         && $IsAlloc(S#0, TSet(Tclass._module.Node()), $Heap), 
    stackNodes#0: Set
       where $Is(stackNodes#0, TSet(Tclass._module.Node()))
         && $IsAlloc(stackNodes#0, TSet(Tclass._module.Node()), $Heap));
  modifies $Heap;



implementation {:smt_option "smt.refine_inj_axioms", "false"} {:smt_option "smt.arith.solver", "2"} {:verboseName "RecursiveMarkWorker (well-formedness)"} CheckWellFormed$$_module.__default.RecursiveMarkWorker(root#0: ref, S#0: Set, stackNodes#0: Set)
{
  var $_ModifiesFrame: [ref,Field]bool;
  var n#0: ref;
  var ch#0: ref;
  var newtype$check#0: ref;
  var n#2: ref;
  var ch#3: ref;
  var newtype$check#1: ref;
  var n#4: ref;
  var n#6: ref;
  var ch#6: ref;
  var newtype$check#2: ref;
  var n#8: ref;
  var n#10: ref;


    // AddMethodImpl: RecursiveMarkWorker, CheckWellFormed$$_module.__default.RecursiveMarkWorker
    $_ModifiesFrame := (lambda $o: ref, $f: Field :: 
      $o != null && $Unbox(read($Heap, $o, alloc)): bool
         ==> Set#IsMember(S#0, $Box($o)));
    assume {:captureState "module:_module:state:4"} true;
    assume {:id "id57"} Set#IsMember(S#0, $Box(root#0));
    havoc n#0;
    assume $Is(n#0, Tclass._module.Node()) && $IsAlloc(n#0, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id58"} Set#IsMember(S#0, $Box(n#0));
        havoc ch#0;
        assume $Is(ch#0, Tclass._module.Node?())
           && $IsAlloc(ch#0, Tclass._module.Node?(), $Heap);
        if (*)
        {
            assert {:id "id59"} n#0 != null;
            assume true;
            assume {:id "id60"} Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#0));
            if (*)
            {
                newtype$check#0 := null;
                assume {:id "id61"} ch#0 == null;
            }
            else
            {
                assume true;
                assume {:id "id62"} ch#0 != null;
                assume {:id "id63"} Set#IsMember(S#0, $Box(ch#0));
            }
        }
        else
        {
            assume true;
            assume {:id "id64"} Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#0))
               ==> ch#0 == null || Set#IsMember(S#0, $Box(ch#0));
        }

        assume {:id "id65"} (forall ch#1: ref :: 
          { Set#IsMember(S#0, $Box(ch#1)) } 
            { Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#1)) } 
          $Is(ch#1, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#1))
             ==> ch#1 == null || Set#IsMember(S#0, $Box(ch#1)));
    }
    else
    {
        assume true;
        assume {:id "id66"} Set#IsMember(S#0, $Box(n#0))
           ==> (forall ch#1: ref :: 
            { Set#IsMember(S#0, $Box(ch#1)) } 
              { Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#1)) } 
            $Is(ch#1, Tclass._module.Node?())
               ==> 
              Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#1))
               ==> ch#1 == null || Set#IsMember(S#0, $Box(ch#1)));
    }

    assume {:id "id67"} (forall n#1: ref :: 
      { read($Heap, n#1, _module.Node.children) } { Set#IsMember(S#0, $Box(n#1)) } 
      $Is(n#1, Tclass._module.Node())
         ==> 
        Set#IsMember(S#0, $Box(n#1))
         ==> (forall ch#2: ref :: 
          { Set#IsMember(S#0, $Box(ch#2)) } 
            { Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2)) } 
          $Is(ch#2, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2))
             ==> ch#2 == null || Set#IsMember(S#0, $Box(ch#2))));
    havoc n#2;
    assume $Is(n#2, Tclass._module.Node()) && $IsAlloc(n#2, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id68"} Set#IsMember(S#0, $Box(n#2));
        assert {:id "id69"} n#2 != null;
        assume true;
        assume {:id "id70"} $Unbox(read($Heap, n#2, _module.Node.marked)): bool;
        if (*)
        {
            assume {:id "id71"} Set#IsMember(stackNodes#0, $Box(n#2));
        }
        else
        {
            assume true;
            assume {:id "id72"} !Set#IsMember(stackNodes#0, $Box(n#2));
            havoc ch#3;
            assume $Is(ch#3, Tclass._module.Node?())
               && $IsAlloc(ch#3, Tclass._module.Node?(), $Heap);
            if (*)
            {
                assert {:id "id73"} n#2 != null;
                assume true;
                assume {:id "id74"} Seq#Contains($Unbox(read($Heap, n#2, _module.Node.children)): Seq, $Box(ch#3));
                newtype$check#1 := null;
                assume {:id "id75"} ch#3 != null;
                assert {:id "id76"} ch#3 != null;
                assume true;
                assume {:id "id77"} $Unbox(read($Heap, ch#3, _module.Node.marked)): bool;
            }
            else
            {
                assume true;
                assume {:id "id78"} Seq#Contains($Unbox(read($Heap, n#2, _module.Node.children)): Seq, $Box(ch#3))
                     && ch#3 != null
                   ==> $Unbox(read($Heap, ch#3, _module.Node.marked)): bool;
            }

            assume {:id "id79"} (forall ch#4: ref :: 
              { read($Heap, ch#4, _module.Node.marked) } 
                { Seq#Contains($Unbox(read($Heap, n#2, _module.Node.children)): Seq, $Box(ch#4)) } 
              $Is(ch#4, Tclass._module.Node?())
                 ==> 
                Seq#Contains($Unbox(read($Heap, n#2, _module.Node.children)): Seq, $Box(ch#4))
                   && ch#4 != null
                 ==> $Unbox(read($Heap, ch#4, _module.Node.marked)): bool);
        }
    }
    else
    {
        assume true;
        assume {:id "id80"} Set#IsMember(S#0, $Box(n#2))
             && $Unbox(read($Heap, n#2, _module.Node.marked)): bool
           ==> Set#IsMember(stackNodes#0, $Box(n#2))
             || (forall ch#4: ref :: 
              { read($Heap, ch#4, _module.Node.marked) } 
                { Seq#Contains($Unbox(read($Heap, n#2, _module.Node.children)): Seq, $Box(ch#4)) } 
              $Is(ch#4, Tclass._module.Node?())
                 ==> 
                Seq#Contains($Unbox(read($Heap, n#2, _module.Node.children)): Seq, $Box(ch#4))
                   && ch#4 != null
                 ==> $Unbox(read($Heap, ch#4, _module.Node.marked)): bool);
    }

    assume {:id "id81"} (forall n#3: ref :: 
      { read($Heap, n#3, _module.Node.children) } 
        { Set#IsMember(stackNodes#0, $Box(n#3)) } 
        { read($Heap, n#3, _module.Node.marked) } 
        { Set#IsMember(S#0, $Box(n#3)) } 
      $Is(n#3, Tclass._module.Node())
         ==> 
        Set#IsMember(S#0, $Box(n#3))
           && $Unbox(read($Heap, n#3, _module.Node.marked)): bool
         ==> Set#IsMember(stackNodes#0, $Box(n#3))
           || (forall ch#5: ref :: 
            { read($Heap, ch#5, _module.Node.marked) } 
              { Seq#Contains($Unbox(read($Heap, n#3, _module.Node.children)): Seq, $Box(ch#5)) } 
            $Is(ch#5, Tclass._module.Node?())
               ==> 
              Seq#Contains($Unbox(read($Heap, n#3, _module.Node.children)): Seq, $Box(ch#5))
                 && ch#5 != null
               ==> $Unbox(read($Heap, ch#5, _module.Node.marked)): bool));
    havoc n#4;
    assume $Is(n#4, Tclass._module.Node()) && $IsAlloc(n#4, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id82"} Set#IsMember(stackNodes#0, $Box(n#4));
        assert {:id "id83"} n#4 != null;
        assume true;
        assume {:id "id84"} $Unbox(read($Heap, n#4, _module.Node.marked)): bool;
    }
    else
    {
        assume true;
        assume {:id "id85"} Set#IsMember(stackNodes#0, $Box(n#4))
           ==> $Unbox(read($Heap, n#4, _module.Node.marked)): bool;
    }

    assume {:id "id86"} (forall n#5: ref :: 
      { read($Heap, n#5, _module.Node.marked) } 
        { Set#IsMember(stackNodes#0, $Box(n#5)) } 
      $Is(n#5, Tclass._module.Node())
         ==> 
        Set#IsMember(stackNodes#0, $Box(n#5))
         ==> $Unbox(read($Heap, n#5, _module.Node.marked)): bool);
    havoc $Heap;
    assume (forall $o: ref :: 
      { $Heap[$o] } 
      $o != null && $Unbox(read(old($Heap), $o, alloc)): bool
         ==> $Heap[$o] == old($Heap)[$o] || Set#IsMember(S#0, $Box($o)));
    assume $HeapSucc(old($Heap), $Heap);
    assume {:captureState "module:_module:state:5"} true;
    assert {:id "id87"} root#0 != null;
    assume true;
    assume {:id "id88"} $Unbox(read($Heap, root#0, _module.Node.marked)): bool;
    havoc n#6;
    assume $Is(n#6, Tclass._module.Node()) && $IsAlloc(n#6, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id89"} Set#IsMember(S#0, $Box(n#6));
        assert {:id "id90"} n#6 != null;
        assume true;
        assume {:id "id91"} $Unbox(read($Heap, n#6, _module.Node.marked)): bool;
        if (*)
        {
            assume {:id "id92"} Set#IsMember(stackNodes#0, $Box(n#6));
        }
        else
        {
            assume true;
            assume {:id "id93"} !Set#IsMember(stackNodes#0, $Box(n#6));
            havoc ch#6;
            assume $Is(ch#6, Tclass._module.Node?())
               && $IsAlloc(ch#6, Tclass._module.Node?(), $Heap);
            if (*)
            {
                assert {:id "id94"} n#6 != null;
                assume true;
                assume {:id "id95"} Seq#Contains($Unbox(read($Heap, n#6, _module.Node.children)): Seq, $Box(ch#6));
                newtype$check#2 := null;
                assume {:id "id96"} ch#6 != null;
                assert {:id "id97"} ch#6 != null;
                assume true;
                assume {:id "id98"} $Unbox(read($Heap, ch#6, _module.Node.marked)): bool;
            }
            else
            {
                assume true;
                assume {:id "id99"} Seq#Contains($Unbox(read($Heap, n#6, _module.Node.children)): Seq, $Box(ch#6))
                     && ch#6 != null
                   ==> $Unbox(read($Heap, ch#6, _module.Node.marked)): bool;
            }

            assume {:id "id100"} (forall ch#7: ref :: 
              { read($Heap, ch#7, _module.Node.marked) } 
                { Seq#Contains($Unbox(read($Heap, n#6, _module.Node.children)): Seq, $Box(ch#7)) } 
              $Is(ch#7, Tclass._module.Node?())
                 ==> 
                Seq#Contains($Unbox(read($Heap, n#6, _module.Node.children)): Seq, $Box(ch#7))
                   && ch#7 != null
                 ==> $Unbox(read($Heap, ch#7, _module.Node.marked)): bool);
        }
    }
    else
    {
        assume true;
        assume {:id "id101"} Set#IsMember(S#0, $Box(n#6))
             && $Unbox(read($Heap, n#6, _module.Node.marked)): bool
           ==> Set#IsMember(stackNodes#0, $Box(n#6))
             || (forall ch#7: ref :: 
              { read($Heap, ch#7, _module.Node.marked) } 
                { Seq#Contains($Unbox(read($Heap, n#6, _module.Node.children)): Seq, $Box(ch#7)) } 
              $Is(ch#7, Tclass._module.Node?())
                 ==> 
                Seq#Contains($Unbox(read($Heap, n#6, _module.Node.children)): Seq, $Box(ch#7))
                   && ch#7 != null
                 ==> $Unbox(read($Heap, ch#7, _module.Node.marked)): bool);
    }

    assume {:id "id102"} (forall n#7: ref :: 
      { read($Heap, n#7, _module.Node.children) } 
        { Set#IsMember(stackNodes#0, $Box(n#7)) } 
        { read($Heap, n#7, _module.Node.marked) } 
        { Set#IsMember(S#0, $Box(n#7)) } 
      $Is(n#7, Tclass._module.Node())
         ==> 
        Set#IsMember(S#0, $Box(n#7))
           && $Unbox(read($Heap, n#7, _module.Node.marked)): bool
         ==> Set#IsMember(stackNodes#0, $Box(n#7))
           || (forall ch#8: ref :: 
            { read($Heap, ch#8, _module.Node.marked) } 
              { Seq#Contains($Unbox(read($Heap, n#7, _module.Node.children)): Seq, $Box(ch#8)) } 
            $Is(ch#8, Tclass._module.Node?())
               ==> 
              Seq#Contains($Unbox(read($Heap, n#7, _module.Node.children)): Seq, $Box(ch#8))
                 && ch#8 != null
               ==> $Unbox(read($Heap, ch#8, _module.Node.marked)): bool));
    havoc n#8;
    assume $Is(n#8, Tclass._module.Node()) && $IsAlloc(n#8, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id103"} Set#IsMember(S#0, $Box(n#8));
        assert {:id "id104"} n#8 != null;
        assert {:id "id105"} $IsAlloc(n#8, Tclass._module.Node(), old($Heap));
        assume true;
        assume {:id "id106"} $Unbox(read(old($Heap), n#8, _module.Node.marked)): bool;
        assert {:id "id107"} n#8 != null;
        assume true;
        assume {:id "id108"} $Unbox(read($Heap, n#8, _module.Node.marked)): bool;
    }
    else
    {
        assume true;
        assume {:id "id109"} Set#IsMember(S#0, $Box(n#8))
             && $Unbox(read(old($Heap), n#8, _module.Node.marked)): bool
           ==> $Unbox(read($Heap, n#8, _module.Node.marked)): bool;
    }

    assume {:id "id110"} (forall n#9: ref :: 
      { read($Heap, n#9, _module.Node.marked) } 
        { read(old($Heap), n#9, _module.Node.marked) } 
        { Set#IsMember(S#0, $Box(n#9)) } 
      $Is(n#9, Tclass._module.Node())
         ==> 
        Set#IsMember(S#0, $Box(n#9))
           && $Unbox(read(old($Heap), n#9, _module.Node.marked)): bool
         ==> $Unbox(read($Heap, n#9, _module.Node.marked)): bool);
    havoc n#10;
    assume $Is(n#10, Tclass._module.Node()) && $IsAlloc(n#10, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id111"} Set#IsMember(S#0, $Box(n#10));
        assert {:id "id112"} n#10 != null;
        assume true;
        assert {:id "id113"} n#10 != null;
        assert {:id "id114"} $IsAlloc(n#10, Tclass._module.Node(), old($Heap));
        assume true;
        assume {:id "id115"} $Unbox(read($Heap, n#10, _module.Node.childrenVisited)): int
           == $Unbox(read(old($Heap), n#10, _module.Node.childrenVisited)): int;
        assert {:id "id116"} n#10 != null;
        assume true;
        assert {:id "id117"} n#10 != null;
        assert {:id "id118"} $IsAlloc(n#10, Tclass._module.Node(), old($Heap));
        assume true;
        assume {:id "id119"} Seq#Equal($Unbox(read($Heap, n#10, _module.Node.children)): Seq, 
          $Unbox(read(old($Heap), n#10, _module.Node.children)): Seq);
    }
    else
    {
        assume true;
        assume {:id "id120"} Set#IsMember(S#0, $Box(n#10))
           ==> $Unbox(read($Heap, n#10, _module.Node.childrenVisited)): int
               == $Unbox(read(old($Heap), n#10, _module.Node.childrenVisited)): int
             && Seq#Equal($Unbox(read($Heap, n#10, _module.Node.children)): Seq, 
              $Unbox(read(old($Heap), n#10, _module.Node.children)): Seq);
    }

    assume {:id "id121"} (forall n#11: ref :: 
      { read(old($Heap), n#11, _module.Node.children) } 
        { read($Heap, n#11, _module.Node.children) } 
        { read(old($Heap), n#11, _module.Node.childrenVisited) } 
        { read($Heap, n#11, _module.Node.childrenVisited) } 
        { Set#IsMember(S#0, $Box(n#11)) } 
      $Is(n#11, Tclass._module.Node())
         ==> (Set#IsMember(S#0, $Box(n#11))
             ==> $Unbox(read($Heap, n#11, _module.Node.childrenVisited)): int
               == $Unbox(read(old($Heap), n#11, _module.Node.childrenVisited)): int)
           && (Set#IsMember(S#0, $Box(n#11))
             ==> Seq#Equal($Unbox(read($Heap, n#11, _module.Node.children)): Seq, 
              $Unbox(read(old($Heap), n#11, _module.Node.children)): Seq)));
}



procedure {:verboseName "RecursiveMarkWorker (call)"} Call$$_module.__default.RecursiveMarkWorker(root#0: ref
       where $Is(root#0, Tclass._module.Node())
         && $IsAlloc(root#0, Tclass._module.Node(), $Heap), 
    S#0: Set
       where $Is(S#0, TSet(Tclass._module.Node()))
         && $IsAlloc(S#0, TSet(Tclass._module.Node()), $Heap), 
    stackNodes#0: Set
       where $Is(stackNodes#0, TSet(Tclass._module.Node()))
         && $IsAlloc(stackNodes#0, TSet(Tclass._module.Node()), $Heap));
  // user-defined preconditions
  free requires {:always_assume} true;
  requires {:id "id122"} Set#IsMember(S#0, $Box(root#0));
  free requires {:always_assume} true;
  requires {:id "id123"} (forall n#1: ref :: 
    { read($Heap, n#1, _module.Node.children) } { Set#IsMember(S#0, $Box(n#1)) } 
    $Is(n#1, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#1))
       ==> (forall ch#2: ref :: 
        { Set#IsMember(S#0, $Box(ch#2)) } 
          { Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2)) } 
        $Is(ch#2, Tclass._module.Node?())
           ==> 
          Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2))
           ==> ch#2 == null || Set#IsMember(S#0, $Box(ch#2))));
  free requires {:always_assume} true;
  requires {:id "id124"} (forall n#3: ref :: 
    { read($Heap, n#3, _module.Node.children) } 
      { Set#IsMember(stackNodes#0, $Box(n#3)) } 
      { read($Heap, n#3, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#3)) } 
    $Is(n#3, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#3))
         && $Unbox(read($Heap, n#3, _module.Node.marked)): bool
       ==> Set#IsMember(stackNodes#0, $Box(n#3))
         || (forall ch#5: ref :: 
          { read($Heap, ch#5, _module.Node.marked) } 
            { Seq#Contains($Unbox(read($Heap, n#3, _module.Node.children)): Seq, $Box(ch#5)) } 
          $Is(ch#5, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#3, _module.Node.children)): Seq, $Box(ch#5))
               && ch#5 != null
             ==> $Unbox(read($Heap, ch#5, _module.Node.marked)): bool));
  free requires {:always_assume} true;
  requires {:id "id125"} (forall n#5: ref :: 
    { read($Heap, n#5, _module.Node.marked) } 
      { Set#IsMember(stackNodes#0, $Box(n#5)) } 
    $Is(n#5, Tclass._module.Node())
       ==> 
      Set#IsMember(stackNodes#0, $Box(n#5))
       ==> $Unbox(read($Heap, n#5, _module.Node.marked)): bool);
  // user-defined frame expressions
  free requires {:always_assume} true;
  modifies $Heap;
  // user-defined postconditions
  free ensures {:always_assume} true;
  ensures {:id "id126"} $Unbox(read($Heap, root#0, _module.Node.marked)): bool;
  free ensures {:always_assume} true;
  ensures {:id "id127"} (forall n#7: ref :: 
    { read($Heap, n#7, _module.Node.children) } 
      { Set#IsMember(stackNodes#0, $Box(n#7)) } 
      { read($Heap, n#7, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#7)) } 
    $Is(n#7, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#7))
         && $Unbox(read($Heap, n#7, _module.Node.marked)): bool
       ==> Set#IsMember(stackNodes#0, $Box(n#7))
         || (forall ch#8: ref :: 
          { read($Heap, ch#8, _module.Node.marked) } 
            { Seq#Contains($Unbox(read($Heap, n#7, _module.Node.children)): Seq, $Box(ch#8)) } 
          $Is(ch#8, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#7, _module.Node.children)): Seq, $Box(ch#8))
               && ch#8 != null
             ==> $Unbox(read($Heap, ch#8, _module.Node.marked)): bool));
  free ensures {:always_assume} true;
  ensures {:id "id128"} (forall n#9: ref :: 
    { read($Heap, n#9, _module.Node.marked) } 
      { read(old($Heap), n#9, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#9)) } 
    $Is(n#9, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#9))
         && $Unbox(read(old($Heap), n#9, _module.Node.marked)): bool
       ==> $Unbox(read($Heap, n#9, _module.Node.marked)): bool);
  free ensures {:always_assume} true;
  ensures {:id "id129"} (forall n#11: ref :: 
    { read(old($Heap), n#11, _module.Node.children) } 
      { read($Heap, n#11, _module.Node.children) } 
      { read(old($Heap), n#11, _module.Node.childrenVisited) } 
      { read($Heap, n#11, _module.Node.childrenVisited) } 
      { Set#IsMember(S#0, $Box(n#11)) } 
    $Is(n#11, Tclass._module.Node())
       ==> (Set#IsMember(S#0, $Box(n#11))
           ==> $Unbox(read($Heap, n#11, _module.Node.childrenVisited)): int
             == $Unbox(read(old($Heap), n#11, _module.Node.childrenVisited)): int)
         && (Set#IsMember(S#0, $Box(n#11))
           ==> Seq#Equal($Unbox(read($Heap, n#11, _module.Node.children)): Seq, 
            $Unbox(read(old($Heap), n#11, _module.Node.children)): Seq)));
  // frame condition: object granularity
  free ensures (forall $o: ref :: 
    { $Heap[$o] } 
    $o != null && $Unbox(read(old($Heap), $o, alloc)): bool
       ==> $Heap[$o] == old($Heap)[$o] || Set#IsMember(S#0, $Box($o)));
  // boilerplate
  free ensures $HeapSucc(old($Heap), $Heap);



procedure {:verboseName "RecursiveMarkWorker (correctness)"} Impl$$_module.__default.RecursiveMarkWorker(root#0: ref
       where $Is(root#0, Tclass._module.Node())
         && $IsAlloc(root#0, Tclass._module.Node(), $Heap), 
    S#0: Set
       where $Is(S#0, TSet(Tclass._module.Node()))
         && $IsAlloc(S#0, TSet(Tclass._module.Node()), $Heap), 
    stackNodes#0: Set
       where $Is(stackNodes#0, TSet(Tclass._module.Node()))
         && $IsAlloc(stackNodes#0, TSet(Tclass._module.Node()), $Heap))
   returns ($_reverifyPost: bool);
  // user-defined preconditions
  free requires {:always_assume} true;
  requires {:id "id130"} Set#IsMember(S#0, $Box(root#0));
  free requires {:always_assume} true;
  requires {:id "id131"} (forall n#1: ref :: 
    { read($Heap, n#1, _module.Node.children) } { Set#IsMember(S#0, $Box(n#1)) } 
    $Is(n#1, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#1))
       ==> (forall ch#2: ref :: 
        { Set#IsMember(S#0, $Box(ch#2)) } 
          { Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2)) } 
        $Is(ch#2, Tclass._module.Node?())
           ==> 
          Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2))
           ==> ch#2 == null || Set#IsMember(S#0, $Box(ch#2))));
  free requires {:always_assume} true;
  requires {:id "id132"} (forall n#3: ref :: 
    { read($Heap, n#3, _module.Node.children) } 
      { Set#IsMember(stackNodes#0, $Box(n#3)) } 
      { read($Heap, n#3, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#3)) } 
    $Is(n#3, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#3))
         && $Unbox(read($Heap, n#3, _module.Node.marked)): bool
       ==> Set#IsMember(stackNodes#0, $Box(n#3))
         || (forall ch#5: ref :: 
          { read($Heap, ch#5, _module.Node.marked) } 
            { Seq#Contains($Unbox(read($Heap, n#3, _module.Node.children)): Seq, $Box(ch#5)) } 
          $Is(ch#5, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#3, _module.Node.children)): Seq, $Box(ch#5))
               && ch#5 != null
             ==> $Unbox(read($Heap, ch#5, _module.Node.marked)): bool));
  free requires {:always_assume} true;
  requires {:id "id133"} (forall n#5: ref :: 
    { read($Heap, n#5, _module.Node.marked) } 
      { Set#IsMember(stackNodes#0, $Box(n#5)) } 
    $Is(n#5, Tclass._module.Node())
       ==> 
      Set#IsMember(stackNodes#0, $Box(n#5))
       ==> $Unbox(read($Heap, n#5, _module.Node.marked)): bool);
  // user-defined frame expressions
  free requires {:always_assume} true;
  modifies $Heap;
  // user-defined postconditions
  free ensures {:always_assume} true;
  ensures {:id "id134"} $Unbox(read($Heap, root#0, _module.Node.marked)): bool;
  free ensures {:always_assume} true;
  ensures {:id "id135"} (forall n#7: ref :: 
    { read($Heap, n#7, _module.Node.children) } 
      { Set#IsMember(stackNodes#0, $Box(n#7)) } 
      { read($Heap, n#7, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#7)) } 
    $Is(n#7, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#7))
         && $Unbox(read($Heap, n#7, _module.Node.marked)): bool
       ==> Set#IsMember(stackNodes#0, $Box(n#7))
         || (forall ch#8: ref :: 
          { read($Heap, ch#8, _module.Node.marked) } 
            { Seq#Contains($Unbox(read($Heap, n#7, _module.Node.children)): Seq, $Box(ch#8)) } 
          $Is(ch#8, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#7, _module.Node.children)): Seq, $Box(ch#8))
               && ch#8 != null
             ==> $Unbox(read($Heap, ch#8, _module.Node.marked)): bool));
  free ensures {:always_assume} true;
  ensures {:id "id136"} (forall n#9: ref :: 
    { read($Heap, n#9, _module.Node.marked) } 
      { read(old($Heap), n#9, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#9)) } 
    $Is(n#9, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#9))
         && $Unbox(read(old($Heap), n#9, _module.Node.marked)): bool
       ==> $Unbox(read($Heap, n#9, _module.Node.marked)): bool);
  free ensures {:always_assume} true;
  ensures {:id "id137"} (forall n#11: ref :: 
    { read(old($Heap), n#11, _module.Node.children) } 
      { read($Heap, n#11, _module.Node.children) } 
      { read(old($Heap), n#11, _module.Node.childrenVisited) } 
      { read($Heap, n#11, _module.Node.childrenVisited) } 
      { Set#IsMember(S#0, $Box(n#11)) } 
    $Is(n#11, Tclass._module.Node())
       ==> (Set#IsMember(S#0, $Box(n#11))
           ==> $Unbox(read($Heap, n#11, _module.Node.childrenVisited)): int
             == $Unbox(read(old($Heap), n#11, _module.Node.childrenVisited)): int)
         && (Set#IsMember(S#0, $Box(n#11))
           ==> Seq#Equal($Unbox(read($Heap, n#11, _module.Node.children)): Seq, 
            $Unbox(read(old($Heap), n#11, _module.Node.children)): Seq)));
  // frame condition: object granularity
  free ensures (forall $o: ref :: 
    { $Heap[$o] } 
    $o != null && $Unbox(read(old($Heap), $o, alloc)): bool
       ==> $Heap[$o] == old($Heap)[$o] || Set#IsMember(S#0, $Box($o)));
  // boilerplate
  free ensures $HeapSucc(old($Heap), $Heap);



implementation {:smt_option "smt.refine_inj_axioms", "false"} {:smt_option "smt.arith.solver", "2"} {:verboseName "RecursiveMarkWorker (correctness)"} Impl$$_module.__default.RecursiveMarkWorker(root#0: ref, S#0: Set, stackNodes#0: Set) returns ($_reverifyPost: bool)
{
  var $_ModifiesFrame: [ref,Field]bool;
  var $rhs#0_0: bool;
  var i#0_0: int;
  var $PreLoopHeap$loop#0_0: Heap;
  var $decr_init$loop#0_00: int;
  var $w$loop#0_0: bool;
  var n#0_0: ref;
  var ch#0_0: ref;
  var newtype$check#0_0: ref;
  var j#0_0: int;
  var newtype$check#0_1: ref;
  var n#0_2: ref;
  var n#0_4: ref;
  var $decr$loop#0_00: int;
  var c#0_0_0: ref
     where $Is(c#0_0_0, Tclass._module.Node?())
       && $IsAlloc(c#0_0_0, Tclass._module.Node?(), $Heap);
  var newtype$check#0_0_0: ref;
  var root##0_0_0_0: ref;
  var S##0_0_0_0: Set;
  var stackNodes##0_0_0_0: Set;

    // AddMethodImpl: RecursiveMarkWorker, Impl$$_module.__default.RecursiveMarkWorker
    $_ModifiesFrame := (lambda $o: ref, $f: Field :: 
      $o != null && $Unbox(read($Heap, $o, alloc)): bool
         ==> Set#IsMember(S#0, $Box($o)));
    assume {:captureState "module:_module:state:6"} true;
    $_reverifyPost := false;
    // ----- if statement ----- module:_module:statement:1
    assert {:id "id138"} root#0 != null;
    assume true;
    assume true;
    if {:allow_path_isolation} (!$Unbox(read($Heap, root#0, _module.Node.marked)): bool)
    {
        // ----- assignment statement ----- module:_module:statement:2
        assert {:id "id139"} root#0 != null;
        assume true;
        assume true;
        assert {:id "id140"} $_ModifiesFrame[root#0, _module.Node.marked];
        assume true;
        $rhs#0_0 := {:id "id141"} Lit(true);
        $Heap := {:id "id142"} update($Heap, root#0, _module.Node.marked, $Box($rhs#0_0));
        assume $IsGoodHeap($Heap);
        assume {:captureState "module:_module:state:7"} true;
        // ----- assignment statement ----- module:_module:statement:3
        assume true;
        assume true;
        i#0_0 := {:id "id143"} LitInt(0);
        assume {:captureState "module:_module:state:8"} true;
        // ----- while statement ----- module:_module:statement:4
        // Assume Fuel Constant
        $PreLoopHeap$loop#0_0 := $Heap;
        $decr_init$loop#0_00 := Seq#Length($Unbox(read($Heap, root#0, _module.Node.children)): Seq) - i#0_0;
        havoc $w$loop#0_0;
        assume true;
        assume true;
        assume true;
        assume true;
        assume true;
        assume $w$loop#0_0 ==> true;
        while (true)
          free invariant true;
          invariant {:id "id147"} $w$loop#0_0 ==> $Unbox(read($Heap, root#0, _module.Node.marked)): bool;
          invariant {:id "id148"} $w$loop#0_0
             ==> i#0_0 <= Seq#Length($Unbox(read($Heap, root#0, _module.Node.children)): Seq);
          free invariant true;
          invariant {:id "id153"} $w$loop#0_0
             ==> (forall n#0_1: ref :: 
              { read($Heap, n#0_1, _module.Node.children) } 
                { Set#IsMember(stackNodes#0, $Box(n#0_1)) } 
                { read($Heap, n#0_1, _module.Node.marked) } 
                { Set#IsMember(S#0, $Box(n#0_1)) } 
              $Is(n#0_1, Tclass._module.Node())
                 ==> 
                Set#IsMember(S#0, $Box(n#0_1))
                   && $Unbox(read($Heap, n#0_1, _module.Node.marked)): bool
                 ==> n#0_1 == root#0
                   || Set#IsMember(stackNodes#0, $Box(n#0_1))
                   || (forall ch#0_2: ref :: 
                    { read($Heap, ch#0_2, _module.Node.marked) } 
                      { Seq#Contains($Unbox(read($Heap, n#0_1, _module.Node.children)): Seq, $Box(ch#0_2)) } 
                    $Is(ch#0_2, Tclass._module.Node?())
                       ==> 
                      Seq#Contains($Unbox(read($Heap, n#0_1, _module.Node.children)): Seq, $Box(ch#0_2))
                         && ch#0_2 != null
                       ==> $Unbox(read($Heap, ch#0_2, _module.Node.marked)): bool));
          free invariant true;
          invariant {:id "id160"} $w$loop#0_0
             ==> (forall j#0_1: int :: 
              { Seq#Index($Unbox(read($Heap, root#0, _module.Node.children)): Seq, j#0_1) } 
              LitInt(0) <= j#0_1 && j#0_1 < i#0_0
                 ==> $Unbox(Seq#Index($Unbox(read($Heap, root#0, _module.Node.children)): Seq, j#0_1)): ref
                     == null
                   || $Unbox(read($Heap, 
                      $Unbox(Seq#Index($Unbox(read($Heap, root#0, _module.Node.children)): Seq, j#0_1)): ref, 
                      _module.Node.marked)): bool);
          free invariant true;
          invariant {:id "id165"} $w$loop#0_0
             ==> (forall n#0_3: ref :: 
              { read($Heap, n#0_3, _module.Node.marked) } 
                { read(old($Heap), n#0_3, _module.Node.marked) } 
                { Set#IsMember(S#0, $Box(n#0_3)) } 
              $Is(n#0_3, Tclass._module.Node())
                 ==> 
                Set#IsMember(S#0, $Box(n#0_3))
                   && $Unbox(read(old($Heap), n#0_3, _module.Node.marked)): bool
                 ==> $Unbox(read($Heap, n#0_3, _module.Node.marked)): bool);
          free invariant true;
          invariant {:id "id173"} $w$loop#0_0
             ==> (forall n#0_5: ref :: 
              { read(old($Heap), n#0_5, _module.Node.children) } 
                { read($Heap, n#0_5, _module.Node.children) } 
                { read(old($Heap), n#0_5, _module.Node.childrenVisited) } 
                { read($Heap, n#0_5, _module.Node.childrenVisited) } 
                { Set#IsMember(S#0, $Box(n#0_5)) } 
              $Is(n#0_5, Tclass._module.Node())
                 ==> (Set#IsMember(S#0, $Box(n#0_5))
                     ==> $Unbox(read($Heap, n#0_5, _module.Node.childrenVisited)): int
                       == $Unbox(read(old($Heap), n#0_5, _module.Node.childrenVisited)): int)
                   && (Set#IsMember(S#0, $Box(n#0_5))
                     ==> Seq#Equal($Unbox(read($Heap, n#0_5, _module.Node.children)): Seq, 
                      $Unbox(read(old($Heap), n#0_5, _module.Node.children)): Seq)));
          free invariant (forall $o: ref :: 
            { $Heap[$o] } 
            $o != null && $Unbox(read(old($Heap), $o, alloc)): bool
               ==> $Heap[$o] == $PreLoopHeap$loop#0_0[$o] || Set#IsMember(S#0, $Box($o)));
          free invariant $HeapSucc($PreLoopHeap$loop#0_0, $Heap);
          free invariant (forall $o: ref, $f: Field :: 
            { read($Heap, $o, $f) } 
            $o != null && $Unbox(read($PreLoopHeap$loop#0_0, $o, alloc)): bool
               ==> read($Heap, $o, $f) == read($PreLoopHeap$loop#0_0, $o, $f)
                 || $_ModifiesFrame[$o, $f]);
          free invariant (0 <= $decr_init$loop#0_00
               && Seq#Length($Unbox(read($Heap, root#0, _module.Node.children)): Seq) - i#0_0
                 < $decr_init$loop#0_00)
             || 
            (Seq#Length($Unbox(read($Heap, root#0, _module.Node.children)): Seq) - i#0_0
               == $decr_init$loop#0_00
             || (0 <= $decr_init$loop#0_00
               && Seq#Length($Unbox(read($Heap, root#0, _module.Node.children)): Seq) - i#0_0
                 <= $decr_init$loop#0_00));
        {
            assume {:captureState "module:_module:state:9"} true;
            if (!$w$loop#0_0)
            {
                assert {:id "id144"} {:subsumption 0} root#0 != null;
                assume true;
                if ($Unbox(read($Heap, root#0, _module.Node.marked)): bool)
                {
                    assert {:id "id145"} {:subsumption 0} root#0 != null;
                    assume true;
                }

                assume true;
                assume {:id "id146"} $Unbox(read($Heap, root#0, _module.Node.marked)): bool
                   && i#0_0 <= Seq#Length($Unbox(read($Heap, root#0, _module.Node.children)): Seq);
                // Begin Comprehension WF check
                havoc n#0_0;
                if ($Is(n#0_0, Tclass._module.Node())
                   && $IsAlloc(n#0_0, Tclass._module.Node(), $Heap))
                {
                    if (Set#IsMember(S#0, $Box(n#0_0)))
                    {
                        assert {:id "id149"} {:subsumption 0} n#0_0 != null;
                        assume true;
                    }

                    if (Set#IsMember(S#0, $Box(n#0_0))
                       && $Unbox(read($Heap, n#0_0, _module.Node.marked)): bool)
                    {
                        if (n#0_0 != root#0)
                        {
                        }

                        if (!(n#0_0 == root#0 || Set#IsMember(stackNodes#0, $Box(n#0_0))))
                        {
                            // Begin Comprehension WF check
                            havoc ch#0_0;
                            if ($Is(ch#0_0, Tclass._module.Node?())
                               && $IsAlloc(ch#0_0, Tclass._module.Node?(), $Heap))
                            {
                                assert {:id "id150"} {:subsumption 0} n#0_0 != null;
                                assume true;
                                if (Seq#Contains($Unbox(read($Heap, n#0_0, _module.Node.children)): Seq, $Box(ch#0_0)))
                                {
                                    newtype$check#0_0 := null;
                                }

                                if (Seq#Contains($Unbox(read($Heap, n#0_0, _module.Node.children)): Seq, $Box(ch#0_0))
                                   && ch#0_0 != null)
                                {
                                    assert {:id "id151"} {:subsumption 0} ch#0_0 != null;
                                    assume true;
                                }
                            }

                            assume true;
                            // End Comprehension WF check
                            assume true;
                        }
                    }
                }

                assume true;
                // End Comprehension WF check
                assume true;
                assume true;
                assume {:id "id152"} (forall n#0_1: ref :: 
                  { read($Heap, n#0_1, _module.Node.children) } 
                    { Set#IsMember(stackNodes#0, $Box(n#0_1)) } 
                    { read($Heap, n#0_1, _module.Node.marked) } 
                    { Set#IsMember(S#0, $Box(n#0_1)) } 
                  $Is(n#0_1, Tclass._module.Node())
                     ==> 
                    Set#IsMember(S#0, $Box(n#0_1))
                       && $Unbox(read($Heap, n#0_1, _module.Node.marked)): bool
                     ==> n#0_1 == root#0
                       || Set#IsMember(stackNodes#0, $Box(n#0_1))
                       || (forall ch#0_2: ref :: 
                        { read($Heap, ch#0_2, _module.Node.marked) } 
                          { Seq#Contains($Unbox(read($Heap, n#0_1, _module.Node.children)): Seq, $Box(ch#0_2)) } 
                        $Is(ch#0_2, Tclass._module.Node?())
                           ==> 
                          Seq#Contains($Unbox(read($Heap, n#0_1, _module.Node.children)): Seq, $Box(ch#0_2))
                             && ch#0_2 != null
                           ==> $Unbox(read($Heap, ch#0_2, _module.Node.marked)): bool));
                // Begin Comprehension WF check
                havoc j#0_0;
                if (true)
                {
                    if (LitInt(0) <= j#0_0)
                    {
                    }

                    if (LitInt(0) <= j#0_0 && j#0_0 < i#0_0)
                    {
                        assert {:id "id154"} {:subsumption 0} root#0 != null;
                        assume true;
                        assert {:id "id155"} {:subsumption 0} 0 <= j#0_0
                           && j#0_0 < Seq#Length($Unbox(read($Heap, root#0, _module.Node.children)): Seq);
                        newtype$check#0_1 := null;
                        if ($Unbox(Seq#Index($Unbox(read($Heap, root#0, _module.Node.children)): Seq, j#0_0)): ref
                           != null)
                        {
                            assert {:id "id156"} {:subsumption 0} root#0 != null;
                            assume true;
                            assert {:id "id157"} {:subsumption 0} 0 <= j#0_0
                               && j#0_0 < Seq#Length($Unbox(read($Heap, root#0, _module.Node.children)): Seq);
                            assert {:id "id158"} {:subsumption 0} $Unbox(Seq#Index($Unbox(read($Heap, root#0, _module.Node.children)): Seq, j#0_0)): ref
                               != null;
                            assume true;
                        }
                    }
                }

                assume true;
                // End Comprehension WF check
                assume true;
                assume true;
                assume {:id "id159"} (forall j#0_1: int :: 
                  { Seq#Index($Unbox(read($Heap, root#0, _module.Node.children)): Seq, j#0_1) } 
                  LitInt(0) <= j#0_1 && j#0_1 < i#0_0
                     ==> $Unbox(Seq#Index($Unbox(read($Heap, root#0, _module.Node.children)): Seq, j#0_1)): ref
                         == null
                       || $Unbox(read($Heap, 
                          $Unbox(Seq#Index($Unbox(read($Heap, root#0, _module.Node.children)): Seq, j#0_1)): ref, 
                          _module.Node.marked)): bool);
                // Begin Comprehension WF check
                havoc n#0_2;
                if ($Is(n#0_2, Tclass._module.Node())
                   && $IsAlloc(n#0_2, Tclass._module.Node(), $Heap))
                {
                    if (Set#IsMember(S#0, $Box(n#0_2)))
                    {
                        assert {:id "id161"} {:subsumption 0} n#0_2 != null;
                        assert {:id "id162"} $IsAlloc(n#0_2, Tclass._module.Node(), old($Heap));
                        assume true;
                    }

                    if (Set#IsMember(S#0, $Box(n#0_2))
                       && $Unbox(read(old($Heap), n#0_2, _module.Node.marked)): bool)
                    {
                        assert {:id "id163"} {:subsumption 0} n#0_2 != null;
                        assume true;
                    }
                }

                assume true;
                // End Comprehension WF check
                assume true;
                assume true;
                assume {:id "id164"} (forall n#0_3: ref :: 
                  { read($Heap, n#0_3, _module.Node.marked) } 
                    { read(old($Heap), n#0_3, _module.Node.marked) } 
                    { Set#IsMember(S#0, $Box(n#0_3)) } 
                  $Is(n#0_3, Tclass._module.Node())
                     ==> 
                    Set#IsMember(S#0, $Box(n#0_3))
                       && $Unbox(read(old($Heap), n#0_3, _module.Node.marked)): bool
                     ==> $Unbox(read($Heap, n#0_3, _module.Node.marked)): bool);
                // Begin Comprehension WF check
                havoc n#0_4;
                if ($Is(n#0_4, Tclass._module.Node())
                   && $IsAlloc(n#0_4, Tclass._module.Node(), $Heap))
                {
                    if (Set#IsMember(S#0, $Box(n#0_4)))
                    {
                        assert {:id "id166"} {:subsumption 0} n#0_4 != null;
                        assume true;
                        assert {:id "id167"} {:subsumption 0} n#0_4 != null;
                        assert {:id "id168"} $IsAlloc(n#0_4, Tclass._module.Node(), old($Heap));
                        assume true;
                        if ($Unbox(read($Heap, n#0_4, _module.Node.childrenVisited)): int
                           == $Unbox(read(old($Heap), n#0_4, _module.Node.childrenVisited)): int)
                        {
                            assert {:id "id169"} {:subsumption 0} n#0_4 != null;
                            assume true;
                            assert {:id "id170"} {:subsumption 0} n#0_4 != null;
                            assert {:id "id171"} $IsAlloc(n#0_4, Tclass._module.Node(), old($Heap));
                            assume true;
                        }
                    }
                }

                assume true;
                // End Comprehension WF check
                assume true;
                assume true;
                assume {:id "id172"} (forall n#0_5: ref :: 
                  { read(old($Heap), n#0_5, _module.Node.children) } 
                    { read($Heap, n#0_5, _module.Node.children) } 
                    { read(old($Heap), n#0_5, _module.Node.childrenVisited) } 
                    { read($Heap, n#0_5, _module.Node.childrenVisited) } 
                    { Set#IsMember(S#0, $Box(n#0_5)) } 
                  $Is(n#0_5, Tclass._module.Node())
                     ==> (Set#IsMember(S#0, $Box(n#0_5))
                         ==> $Unbox(read($Heap, n#0_5, _module.Node.childrenVisited)): int
                           == $Unbox(read(old($Heap), n#0_5, _module.Node.childrenVisited)): int)
                       && (Set#IsMember(S#0, $Box(n#0_5))
                         ==> Seq#Equal($Unbox(read($Heap, n#0_5, _module.Node.children)): Seq, 
                          $Unbox(read(old($Heap), n#0_5, _module.Node.children)): Seq)));
                assert {:id "id174"} root#0 != null;
                assume true;
                assume true;
                assume false;
            }

            assert {:id "id175"} root#0 != null;
            assume true;
            assume true;
            if (Seq#Length($Unbox(read($Heap, root#0, _module.Node.children)): Seq) <= i#0_0)
            {
                break;
            }

            assume true;
            $decr$loop#0_00 := Seq#Length($Unbox(read($Heap, root#0, _module.Node.children)): Seq) - i#0_0;
            // ----- assignment statement ----- module:_module:statement:5
            assume true;
            assert {:id "id176"} root#0 != null;
            assume true;
            assert {:id "id177"} 0 <= i#0_0
               && i#0_0 < Seq#Length($Unbox(read($Heap, root#0, _module.Node.children)): Seq);
            assume true;
            c#0_0_0 := {:id "id178"} $Unbox(Seq#Index($Unbox(read($Heap, root#0, _module.Node.children)): Seq, i#0_0)): ref;
            assume {:captureState "module:_module:state:10"} true;
            // ----- if statement ----- module:_module:statement:6
            newtype$check#0_0_0 := null;
            assume true;
            if {:allow_path_isolation} (c#0_0_0 != null)
            {
                push;
                // ----- call statement ----- module:_module:statement:7
                // TrCallStmt: Before ProcessCallStmt
                assume true;
                // ProcessCallStmt: CheckSubrange
                assert {:id "id179"} $Is(c#0_0_0, Tclass._module.Node());
                root##0_0_0_0 := c#0_0_0;
                assume true;
                // ProcessCallStmt: CheckSubrange
                S##0_0_0_0 := S#0;
                assume true;
                // ProcessCallStmt: CheckSubrange
                stackNodes##0_0_0_0 := Set#Union(stackNodes#0, Set#UnionOne(Set#Empty(): Set, $Box(root#0)));
                assume true;
                assert {:id "id180"} (forall $o: ref, $f: Field :: 
                  $o != null
                       && $Unbox(read($Heap, $o, alloc)): bool
                       && Set#IsMember(S##0_0_0_0, $Box($o))
                     ==> $_ModifiesFrame[$o, $f]);
                assume true;
                assert {:id "id181"} Set#Subset(Set#Difference(S##0_0_0_0, stackNodes##0_0_0_0), 
                    Set#Difference(S#0, stackNodes#0))
                   && !Set#Subset(Set#Difference(S#0, stackNodes#0), 
                    Set#Difference(S##0_0_0_0, stackNodes##0_0_0_0));
                call {:id "id182"} Call$$_module.__default.RecursiveMarkWorker(root##0_0_0_0, S##0_0_0_0, stackNodes##0_0_0_0);
                // TrCallStmt: After ProcessCallStmt
                assume {:captureState "module:_module:state:11"} true;
                pop;
            }
            else
            {
            }

            // ----- assignment statement ----- module:_module:statement:8
            assume true;
            assume true;
            i#0_0 := {:id "id183"} i#0_0 + 1;
            assume {:captureState "module:_module:state:12"} true;
            assume true;
            // ----- loop termination check ----- module:_module:statement:9
            assert {:id "id184"} 0 <= $decr$loop#0_00
               || Seq#Length($Unbox(read($Heap, root#0, _module.Node.children)): Seq) - i#0_0
                 == $decr$loop#0_00;
            assert {:id "id185"} Seq#Length($Unbox(read($Heap, root#0, _module.Node.children)): Seq) - i#0_0
               < $decr$loop#0_00;
            assume true;
        }
    }
    else
    {
    }
}



procedure {:verboseName "IterativeMark (well-formedness)"} CheckWellFormed$$_module.__default.IterativeMark(root#0: ref
       where $Is(root#0, Tclass._module.Node())
         && $IsAlloc(root#0, Tclass._module.Node(), $Heap), 
    S#0: Set
       where $Is(S#0, TSet(Tclass._module.Node()))
         && $IsAlloc(S#0, TSet(Tclass._module.Node()), $Heap));
  modifies $Heap;



implementation {:smt_option "smt.refine_inj_axioms", "false"} {:smt_option "smt.arith.solver", "2"} {:verboseName "IterativeMark (well-formedness)"} CheckWellFormed$$_module.__default.IterativeMark(root#0: ref, S#0: Set)
{
  var $_ModifiesFrame: [ref,Field]bool;
  var n#0: ref;
  var ch#0: ref;
  var newtype$check#0: ref;
  var n#2: ref;
  var n#4: ref;
  var ch#3: ref;
  var newtype$check#1: ref;
  var n#6: ref;


    // AddMethodImpl: IterativeMark, CheckWellFormed$$_module.__default.IterativeMark
    $_ModifiesFrame := (lambda $o: ref, $f: Field :: 
      $o != null && $Unbox(read($Heap, $o, alloc)): bool
         ==> Set#IsMember(S#0, $Box($o)));
    assume {:captureState "module:_module:state:13"} true;
    assume {:id "id186"} Set#IsMember(S#0, $Box(root#0));
    havoc n#0;
    assume $Is(n#0, Tclass._module.Node()) && $IsAlloc(n#0, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id187"} Set#IsMember(S#0, $Box(n#0));
        havoc ch#0;
        assume $Is(ch#0, Tclass._module.Node?())
           && $IsAlloc(ch#0, Tclass._module.Node?(), $Heap);
        if (*)
        {
            assert {:id "id188"} n#0 != null;
            assume true;
            assume {:id "id189"} Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#0));
            if (*)
            {
                newtype$check#0 := null;
                assume {:id "id190"} ch#0 == null;
            }
            else
            {
                assume true;
                assume {:id "id191"} ch#0 != null;
                assume {:id "id192"} Set#IsMember(S#0, $Box(ch#0));
            }
        }
        else
        {
            assume true;
            assume {:id "id193"} Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#0))
               ==> ch#0 == null || Set#IsMember(S#0, $Box(ch#0));
        }

        assume {:id "id194"} (forall ch#1: ref :: 
          { Set#IsMember(S#0, $Box(ch#1)) } 
            { Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#1)) } 
          $Is(ch#1, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#1))
             ==> ch#1 == null || Set#IsMember(S#0, $Box(ch#1)));
    }
    else
    {
        assume true;
        assume {:id "id195"} Set#IsMember(S#0, $Box(n#0))
           ==> (forall ch#1: ref :: 
            { Set#IsMember(S#0, $Box(ch#1)) } 
              { Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#1)) } 
            $Is(ch#1, Tclass._module.Node?())
               ==> 
              Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#1))
               ==> ch#1 == null || Set#IsMember(S#0, $Box(ch#1)));
    }

    assume {:id "id196"} (forall n#1: ref :: 
      { read($Heap, n#1, _module.Node.children) } { Set#IsMember(S#0, $Box(n#1)) } 
      $Is(n#1, Tclass._module.Node())
         ==> 
        Set#IsMember(S#0, $Box(n#1))
         ==> (forall ch#2: ref :: 
          { Set#IsMember(S#0, $Box(ch#2)) } 
            { Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2)) } 
          $Is(ch#2, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2))
             ==> ch#2 == null || Set#IsMember(S#0, $Box(ch#2))));
    havoc n#2;
    assume $Is(n#2, Tclass._module.Node()) && $IsAlloc(n#2, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id197"} Set#IsMember(S#0, $Box(n#2));
        assert {:id "id198"} n#2 != null;
        assume true;
        assume {:id "id199"} !$Unbox(read($Heap, n#2, _module.Node.marked)): bool;
        assert {:id "id200"} n#2 != null;
        assume true;
        assume {:id "id201"} $Unbox(read($Heap, n#2, _module.Node.childrenVisited)): int == LitInt(0);
    }
    else
    {
        assume true;
        assume {:id "id202"} Set#IsMember(S#0, $Box(n#2))
           ==> !$Unbox(read($Heap, n#2, _module.Node.marked)): bool
             && $Unbox(read($Heap, n#2, _module.Node.childrenVisited)): int == LitInt(0);
    }

    assume {:id "id203"} (forall n#3: ref :: 
      { read($Heap, n#3, _module.Node.childrenVisited) } 
        { read($Heap, n#3, _module.Node.marked) } 
        { Set#IsMember(S#0, $Box(n#3)) } 
      $Is(n#3, Tclass._module.Node())
         ==> (Set#IsMember(S#0, $Box(n#3))
             ==> !$Unbox(read($Heap, n#3, _module.Node.marked)): bool)
           && (Set#IsMember(S#0, $Box(n#3))
             ==> $Unbox(read($Heap, n#3, _module.Node.childrenVisited)): int == LitInt(0)));
    havoc $Heap;
    assume (forall $o: ref :: 
      { $Heap[$o] } 
      $o != null && $Unbox(read(old($Heap), $o, alloc)): bool
         ==> $Heap[$o] == old($Heap)[$o] || Set#IsMember(S#0, $Box($o)));
    assume $HeapSucc(old($Heap), $Heap);
    assume {:captureState "module:_module:state:14"} true;
    assert {:id "id204"} root#0 != null;
    assume true;
    assume {:id "id205"} $Unbox(read($Heap, root#0, _module.Node.marked)): bool;
    havoc n#4;
    assume $Is(n#4, Tclass._module.Node()) && $IsAlloc(n#4, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id206"} Set#IsMember(S#0, $Box(n#4));
        assert {:id "id207"} n#4 != null;
        assume true;
        assume {:id "id208"} $Unbox(read($Heap, n#4, _module.Node.marked)): bool;
        havoc ch#3;
        assume $Is(ch#3, Tclass._module.Node?())
           && $IsAlloc(ch#3, Tclass._module.Node?(), $Heap);
        if (*)
        {
            assert {:id "id209"} n#4 != null;
            assume true;
            assume {:id "id210"} Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#3));
            newtype$check#1 := null;
            assume {:id "id211"} ch#3 != null;
            assert {:id "id212"} ch#3 != null;
            assume true;
            assume {:id "id213"} $Unbox(read($Heap, ch#3, _module.Node.marked)): bool;
        }
        else
        {
            assume true;
            assume {:id "id214"} Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#3))
                 && ch#3 != null
               ==> $Unbox(read($Heap, ch#3, _module.Node.marked)): bool;
        }

        assume {:id "id215"} (forall ch#4: ref :: 
          { read($Heap, ch#4, _module.Node.marked) } 
            { Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#4)) } 
          $Is(ch#4, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#4))
               && ch#4 != null
             ==> $Unbox(read($Heap, ch#4, _module.Node.marked)): bool);
    }
    else
    {
        assume true;
        assume {:id "id216"} Set#IsMember(S#0, $Box(n#4))
             && $Unbox(read($Heap, n#4, _module.Node.marked)): bool
           ==> (forall ch#4: ref :: 
            { read($Heap, ch#4, _module.Node.marked) } 
              { Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#4)) } 
            $Is(ch#4, Tclass._module.Node?())
               ==> 
              Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#4))
                 && ch#4 != null
               ==> $Unbox(read($Heap, ch#4, _module.Node.marked)): bool);
    }

    assume {:id "id217"} (forall n#5: ref :: 
      { read($Heap, n#5, _module.Node.children) } 
        { read($Heap, n#5, _module.Node.marked) } 
        { Set#IsMember(S#0, $Box(n#5)) } 
      $Is(n#5, Tclass._module.Node())
         ==> 
        Set#IsMember(S#0, $Box(n#5))
           && $Unbox(read($Heap, n#5, _module.Node.marked)): bool
         ==> (forall ch#5: ref :: 
          { read($Heap, ch#5, _module.Node.marked) } 
            { Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5)) } 
          $Is(ch#5, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5))
               && ch#5 != null
             ==> $Unbox(read($Heap, ch#5, _module.Node.marked)): bool));
    havoc n#6;
    assume $Is(n#6, Tclass._module.Node()) && $IsAlloc(n#6, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id218"} Set#IsMember(S#0, $Box(n#6));
        assert {:id "id219"} n#6 != null;
        assume true;
        assert {:id "id220"} n#6 != null;
        assert {:id "id221"} $IsAlloc(n#6, Tclass._module.Node(), old($Heap));
        assume true;
        assume {:id "id222"} $Unbox(read($Heap, n#6, _module.Node.childrenVisited)): int
           == $Unbox(read(old($Heap), n#6, _module.Node.childrenVisited)): int;
        assert {:id "id223"} n#6 != null;
        assume true;
        assert {:id "id224"} n#6 != null;
        assert {:id "id225"} $IsAlloc(n#6, Tclass._module.Node(), old($Heap));
        assume true;
        assume {:id "id226"} Seq#Equal($Unbox(read($Heap, n#6, _module.Node.children)): Seq, 
          $Unbox(read(old($Heap), n#6, _module.Node.children)): Seq);
    }
    else
    {
        assume true;
        assume {:id "id227"} Set#IsMember(S#0, $Box(n#6))
           ==> $Unbox(read($Heap, n#6, _module.Node.childrenVisited)): int
               == $Unbox(read(old($Heap), n#6, _module.Node.childrenVisited)): int
             && Seq#Equal($Unbox(read($Heap, n#6, _module.Node.children)): Seq, 
              $Unbox(read(old($Heap), n#6, _module.Node.children)): Seq);
    }

    assume {:id "id228"} (forall n#7: ref :: 
      { read(old($Heap), n#7, _module.Node.children) } 
        { read($Heap, n#7, _module.Node.children) } 
        { read(old($Heap), n#7, _module.Node.childrenVisited) } 
        { read($Heap, n#7, _module.Node.childrenVisited) } 
        { Set#IsMember(S#0, $Box(n#7)) } 
      $Is(n#7, Tclass._module.Node())
         ==> (Set#IsMember(S#0, $Box(n#7))
             ==> $Unbox(read($Heap, n#7, _module.Node.childrenVisited)): int
               == $Unbox(read(old($Heap), n#7, _module.Node.childrenVisited)): int)
           && (Set#IsMember(S#0, $Box(n#7))
             ==> Seq#Equal($Unbox(read($Heap, n#7, _module.Node.children)): Seq, 
              $Unbox(read(old($Heap), n#7, _module.Node.children)): Seq)));
}



procedure {:verboseName "IterativeMark (call)"} Call$$_module.__default.IterativeMark(root#0: ref
       where $Is(root#0, Tclass._module.Node())
         && $IsAlloc(root#0, Tclass._module.Node(), $Heap), 
    S#0: Set
       where $Is(S#0, TSet(Tclass._module.Node()))
         && $IsAlloc(S#0, TSet(Tclass._module.Node()), $Heap));
  // user-defined preconditions
  free requires {:always_assume} true;
  requires {:id "id229"} Set#IsMember(S#0, $Box(root#0));
  free requires {:always_assume} true;
  requires {:id "id230"} (forall n#1: ref :: 
    { read($Heap, n#1, _module.Node.children) } { Set#IsMember(S#0, $Box(n#1)) } 
    $Is(n#1, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#1))
       ==> (forall ch#2: ref :: 
        { Set#IsMember(S#0, $Box(ch#2)) } 
          { Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2)) } 
        $Is(ch#2, Tclass._module.Node?())
           ==> 
          Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2))
           ==> ch#2 == null || Set#IsMember(S#0, $Box(ch#2))));
  free requires {:always_assume} true;
  requires {:id "id231"} (forall n#3: ref :: 
    { read($Heap, n#3, _module.Node.childrenVisited) } 
      { read($Heap, n#3, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#3)) } 
    $Is(n#3, Tclass._module.Node())
       ==> (Set#IsMember(S#0, $Box(n#3))
           ==> !$Unbox(read($Heap, n#3, _module.Node.marked)): bool)
         && (Set#IsMember(S#0, $Box(n#3))
           ==> $Unbox(read($Heap, n#3, _module.Node.childrenVisited)): int == LitInt(0)));
  // user-defined frame expressions
  free requires {:always_assume} true;
  modifies $Heap;
  // user-defined postconditions
  free ensures {:always_assume} true;
  ensures {:id "id232"} $Unbox(read($Heap, root#0, _module.Node.marked)): bool;
  free ensures {:always_assume} true;
  ensures {:id "id233"} (forall n#5: ref :: 
    { read($Heap, n#5, _module.Node.children) } 
      { read($Heap, n#5, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#5)) } 
    $Is(n#5, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#5))
         && $Unbox(read($Heap, n#5, _module.Node.marked)): bool
       ==> (forall ch#5: ref :: 
        { read($Heap, ch#5, _module.Node.marked) } 
          { Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5)) } 
        $Is(ch#5, Tclass._module.Node?())
           ==> 
          Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5))
             && ch#5 != null
           ==> $Unbox(read($Heap, ch#5, _module.Node.marked)): bool));
  free ensures {:always_assume} true;
  ensures {:id "id234"} (forall n#7: ref :: 
    { read(old($Heap), n#7, _module.Node.children) } 
      { read($Heap, n#7, _module.Node.children) } 
      { read(old($Heap), n#7, _module.Node.childrenVisited) } 
      { read($Heap, n#7, _module.Node.childrenVisited) } 
      { Set#IsMember(S#0, $Box(n#7)) } 
    $Is(n#7, Tclass._module.Node())
       ==> (Set#IsMember(S#0, $Box(n#7))
           ==> $Unbox(read($Heap, n#7, _module.Node.childrenVisited)): int
             == $Unbox(read(old($Heap), n#7, _module.Node.childrenVisited)): int)
         && (Set#IsMember(S#0, $Box(n#7))
           ==> Seq#Equal($Unbox(read($Heap, n#7, _module.Node.children)): Seq, 
            $Unbox(read(old($Heap), n#7, _module.Node.children)): Seq)));
  // frame condition: object granularity
  free ensures (forall $o: ref :: 
    { $Heap[$o] } 
    $o != null && $Unbox(read(old($Heap), $o, alloc)): bool
       ==> $Heap[$o] == old($Heap)[$o] || Set#IsMember(S#0, $Box($o)));
  // boilerplate
  free ensures $HeapSucc(old($Heap), $Heap);



procedure {:verboseName "IterativeMark (correctness)"} Impl$$_module.__default.IterativeMark(root#0: ref
       where $Is(root#0, Tclass._module.Node())
         && $IsAlloc(root#0, Tclass._module.Node(), $Heap), 
    S#0: Set
       where $Is(S#0, TSet(Tclass._module.Node()))
         && $IsAlloc(S#0, TSet(Tclass._module.Node()), $Heap))
   returns ($_reverifyPost: bool);
  // user-defined preconditions
  free requires {:always_assume} true;
  requires {:id "id235"} Set#IsMember(S#0, $Box(root#0));
  free requires {:always_assume} true;
  requires {:id "id236"} (forall n#1: ref :: 
    { read($Heap, n#1, _module.Node.children) } { Set#IsMember(S#0, $Box(n#1)) } 
    $Is(n#1, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#1))
       ==> (forall ch#2: ref :: 
        { Set#IsMember(S#0, $Box(ch#2)) } 
          { Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2)) } 
        $Is(ch#2, Tclass._module.Node?())
           ==> 
          Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2))
           ==> ch#2 == null || Set#IsMember(S#0, $Box(ch#2))));
  free requires {:always_assume} true;
  requires {:id "id237"} (forall n#3: ref :: 
    { read($Heap, n#3, _module.Node.childrenVisited) } 
      { read($Heap, n#3, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#3)) } 
    $Is(n#3, Tclass._module.Node())
       ==> (Set#IsMember(S#0, $Box(n#3))
           ==> !$Unbox(read($Heap, n#3, _module.Node.marked)): bool)
         && (Set#IsMember(S#0, $Box(n#3))
           ==> $Unbox(read($Heap, n#3, _module.Node.childrenVisited)): int == LitInt(0)));
  // user-defined frame expressions
  free requires {:always_assume} true;
  modifies $Heap;
  // user-defined postconditions
  free ensures {:always_assume} true;
  ensures {:id "id238"} $Unbox(read($Heap, root#0, _module.Node.marked)): bool;
  free ensures {:always_assume} true;
  ensures {:id "id239"} (forall n#5: ref :: 
    { read($Heap, n#5, _module.Node.children) } 
      { read($Heap, n#5, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#5)) } 
    $Is(n#5, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#5))
         && $Unbox(read($Heap, n#5, _module.Node.marked)): bool
       ==> (forall ch#5: ref :: 
        { read($Heap, ch#5, _module.Node.marked) } 
          { Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5)) } 
        $Is(ch#5, Tclass._module.Node?())
           ==> 
          Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5))
             && ch#5 != null
           ==> $Unbox(read($Heap, ch#5, _module.Node.marked)): bool));
  free ensures {:always_assume} true;
  ensures {:id "id240"} (forall n#7: ref :: 
    { read(old($Heap), n#7, _module.Node.children) } 
      { read($Heap, n#7, _module.Node.children) } 
      { read(old($Heap), n#7, _module.Node.childrenVisited) } 
      { read($Heap, n#7, _module.Node.childrenVisited) } 
      { Set#IsMember(S#0, $Box(n#7)) } 
    $Is(n#7, Tclass._module.Node())
       ==> (Set#IsMember(S#0, $Box(n#7))
           ==> $Unbox(read($Heap, n#7, _module.Node.childrenVisited)): int
             == $Unbox(read(old($Heap), n#7, _module.Node.childrenVisited)): int)
         && (Set#IsMember(S#0, $Box(n#7))
           ==> Seq#Equal($Unbox(read($Heap, n#7, _module.Node.children)): Seq, 
            $Unbox(read(old($Heap), n#7, _module.Node.children)): Seq)));
  // frame condition: object granularity
  free ensures (forall $o: ref :: 
    { $Heap[$o] } 
    $o != null && $Unbox(read(old($Heap), $o, alloc)): bool
       ==> $Heap[$o] == old($Heap)[$o] || Set#IsMember(S#0, $Box($o)));
  // boilerplate
  free ensures $HeapSucc(old($Heap), $Heap);



implementation {:smt_option "smt.refine_inj_axioms", "false"} {:smt_option "smt.arith.solver", "2"} {:verboseName "IterativeMark (correctness)"} Impl$$_module.__default.IterativeMark(root#0: ref, S#0: Set) returns ($_reverifyPost: bool)
{
  var $_ModifiesFrame: [ref,Field]bool;
  var t#0: ref
     where $Is(t#0, Tclass._module.Node?()) && $IsAlloc(t#0, Tclass._module.Node?(), $Heap);
  var $rhs#0: bool;
  var stackNodes#0: Seq
     where $Is(stackNodes#0, TSeq(Tclass._module.Node()))
       && $IsAlloc(stackNodes#0, TSeq(Tclass._module.Node()), $Heap);
  var unmarkedNodes#0: Set
     where $Is(unmarkedNodes#0, TSet(Tclass._module.Node?()))
       && $IsAlloc(unmarkedNodes#0, TSet(Tclass._module.Node?()), $Heap);
  var $PreLoopHeap$loop#0: Heap;
  var $decr_init$loop#00: Set;
  var $decr_init$loop#01: Seq;
  var $decr_init$loop#02: int;
  var $w$loop#0: bool;
  var i#0: int;
  var j#0: int;
  var n#8: ref;
  var n#10: ref;
  var j#2: int;
  var newtype$check#2: ref;
  var n#12: ref;
  var j#5: int;
  var n#14: ref;
  var ch#6: ref;
  var newtype$check#3: ref;
  var n#16: ref;
  var n#18: ref;
  var n#20: ref;
  var $decr$loop#00: Set;
  var $decr$loop#01: Seq;
  var $decr$loop#02: int;
  var $rhs#0_0_0: int;
  var $rhs#0_0_1: int;
  var newtype$check#0_1_0: ref;
  var $rhs#0_1_0_0: int;
  var $rhs#0_1_1_0: bool;

    // AddMethodImpl: IterativeMark, Impl$$_module.__default.IterativeMark
    $_ModifiesFrame := (lambda $o: ref, $f: Field :: 
      $o != null && $Unbox(read($Heap, $o, alloc)): bool
         ==> Set#IsMember(S#0, $Box($o)));
    assume {:captureState "module:_module:state:15"} true;
    $_reverifyPost := false;
    // ----- assignment statement ----- module:_module:statement:10
    assume true;
    assume true;
    t#0 := {:id "id241"} root#0;
    assume {:captureState "module:_module:state:16"} true;
    // ----- assignment statement ----- module:_module:statement:11
    assert {:id "id242"} t#0 != null;
    assume true;
    assume true;
    assert {:id "id243"} $_ModifiesFrame[t#0, _module.Node.marked];
    assume true;
    $rhs#0 := {:id "id244"} Lit(true);
    $Heap := {:id "id245"} update($Heap, t#0, _module.Node.marked, $Box($rhs#0));
    assume $IsGoodHeap($Heap);
    assume {:captureState "module:_module:state:17"} true;
    // ----- assignment statement ----- module:_module:statement:12
    assume true;
    assume true;
    stackNodes#0 := {:id "id246"} Lit(Seq#Empty(): Seq);
    assume {:captureState "module:_module:state:18"} true;
    // ----- assignment statement ----- module:_module:statement:13
    assume true;
    assume true;
    unmarkedNodes#0 := {:id "id247"} Set#Difference(S#0, Set#UnionOne(Set#Empty(): Set, $Box(t#0)));
    assume {:captureState "module:_module:state:19"} true;
    // ----- while statement ----- module:_module:statement:14
    // Assume Fuel Constant
    $PreLoopHeap$loop#0 := $Heap;
    $decr_init$loop#00 := unmarkedNodes#0;
    $decr_init$loop#01 := stackNodes#0;
    $decr_init$loop#02 := Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq)
       - $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int;
    havoc $w$loop#0;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume $w$loop#0 ==> true;
    assume $w$loop#0 ==> true;
    assume $w$loop#0 ==> true;
    while (true)
      free invariant true;
      invariant {:id "id250"} $w$loop#0 ==> $Unbox(read($Heap, root#0, _module.Node.marked)): bool;
      invariant {:id "id251"} $w$loop#0 ==> Set#IsMember(S#0, $Box(t#0));
      invariant {:id "id252"} $w$loop#0 ==> !Seq#Contains(stackNodes#0, $Box(t#0));
      free invariant true;
      invariant {:id "id256"} $w$loop#0
         ==> (forall i#1: int, j#1: int :: 
          { Seq#Index(stackNodes#0, j#1), Seq#Index(stackNodes#0, i#1) } 
          LitInt(0) <= i#1 && i#1 < j#1 && j#1 < Seq#Length(stackNodes#0)
             ==> $Unbox(Seq#Index(stackNodes#0, i#1)): ref
               != $Unbox(Seq#Index(stackNodes#0, j#1)): ref);
      free invariant true;
      invariant {:id "id258"} $w$loop#0
         ==> (forall n#9: ref :: 
          { Set#IsMember(S#0, $Box(n#9)) } { Seq#Contains(stackNodes#0, $Box(n#9)) } 
          $Is(n#9, Tclass._module.Node())
             ==> 
            Seq#Contains(stackNodes#0, $Box(n#9))
             ==> Set#IsMember(S#0, $Box(n#9)));
      free invariant true;
      invariant {:id "id270"} $w$loop#0
         ==> (forall n#11: ref :: 
          { read($Heap, n#11, _module.Node.marked) } 
            { Seq#Contains(stackNodes#0, $Box(n#11)) } 
          $Is(n#11, Tclass._module.Node()) && $IsAlloc(n#11, Tclass._module.Node(), $Heap)
             ==> 
            Seq#Contains(stackNodes#0, $Box(n#11)) || n#11 == t#0
             ==> $Unbox(read($Heap, n#11, _module.Node.marked)): bool);
      invariant {:id "id271"} $w$loop#0
         ==> (forall n#11: ref :: 
          { read($Heap, n#11, _module.Node.childrenVisited) } 
            { Seq#Contains(stackNodes#0, $Box(n#11)) } 
          $Is(n#11, Tclass._module.Node()) && $IsAlloc(n#11, Tclass._module.Node(), $Heap)
             ==> 
            Seq#Contains(stackNodes#0, $Box(n#11)) || n#11 == t#0
             ==> LitInt(0) <= $Unbox(read($Heap, n#11, _module.Node.childrenVisited)): int);
      invariant {:id "id272"} $w$loop#0
         ==> (forall n#11: ref :: 
          { read($Heap, n#11, _module.Node.children) } 
            { read($Heap, n#11, _module.Node.childrenVisited) } 
            { Seq#Contains(stackNodes#0, $Box(n#11)) } 
          $Is(n#11, Tclass._module.Node()) && $IsAlloc(n#11, Tclass._module.Node(), $Heap)
             ==> (Seq#Contains(stackNodes#0, $Box(n#11)) || n#11 == t#0
                 ==> $Unbox(read($Heap, n#11, _module.Node.childrenVisited)): int
                   <= Seq#Length($Unbox(read($Heap, n#11, _module.Node.children)): Seq))
               && (Seq#Contains(stackNodes#0, $Box(n#11)) || n#11 == t#0
                 ==> (forall j#4: int :: 
                  { Seq#Index($Unbox(read($Heap, n#11, _module.Node.children)): Seq, j#4) } 
                  LitInt(0) <= j#4
                       && j#4 < $Unbox(read($Heap, n#11, _module.Node.childrenVisited)): int
                     ==> $Unbox(Seq#Index($Unbox(read($Heap, n#11, _module.Node.children)): Seq, j#4)): ref
                         == null
                       || $Unbox(read($Heap, 
                          $Unbox(Seq#Index($Unbox(read($Heap, n#11, _module.Node.children)): Seq, j#4)): ref, 
                          _module.Node.marked)): bool)));
      free invariant true;
      invariant {:id "id276"} $w$loop#0
         ==> (forall n#13: ref :: 
          { read($Heap, n#13, _module.Node.children) } 
            { read($Heap, n#13, _module.Node.childrenVisited) } 
            { Seq#Contains(stackNodes#0, $Box(n#13)) } 
          $Is(n#13, Tclass._module.Node())
             ==> 
            Seq#Contains(stackNodes#0, $Box(n#13))
             ==> $Unbox(read($Heap, n#13, _module.Node.childrenVisited)): int
               < Seq#Length($Unbox(read($Heap, n#13, _module.Node.children)): Seq));
      free invariant true;
      invariant {:id "id284"} $w$loop#0
         ==> (forall j#6: int, _t#0#0: int :: 
          { Seq#Index(stackNodes#0, _t#0#0), Seq#Index(stackNodes#0, j#6) } 
          _t#0#0 == j#6 + 1
             ==> 
            LitInt(0) <= j#6 && _t#0#0 < Seq#Length(stackNodes#0)
             ==> $Unbox(Seq#Index($Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, j#6)): ref, _module.Node.children)): Seq, 
                  $Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, j#6)): ref, _module.Node.childrenVisited)): int)): ref
               == $Unbox(Seq#Index(stackNodes#0, _t#0#0)): ref);
      free invariant true;
      invariant {:id "id291"} $w$loop#0
         ==> 
        0 < Seq#Length(stackNodes#0)
         ==> $Unbox(Seq#Index($Unbox(read($Heap, 
                  $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                  _module.Node.children)): Seq, 
              $Unbox(read($Heap, 
                  $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                  _module.Node.childrenVisited)): int)): ref
           == t#0;
      free invariant true;
      invariant {:id "id296"} $w$loop#0
         ==> (forall n#15: ref :: 
          { read($Heap, n#15, _module.Node.children) } 
            { Seq#Contains(stackNodes#0, $Box(n#15)) } 
            { read($Heap, n#15, _module.Node.marked) } 
            { Set#IsMember(S#0, $Box(n#15)) } 
          $Is(n#15, Tclass._module.Node())
             ==> 
            Set#IsMember(S#0, $Box(n#15))
               && $Unbox(read($Heap, n#15, _module.Node.marked)): bool
               && !Seq#Contains(stackNodes#0, $Box(n#15))
               && n#15 != t#0
             ==> (forall ch#8: ref :: 
              { read($Heap, ch#8, _module.Node.marked) } 
                { Seq#Contains($Unbox(read($Heap, n#15, _module.Node.children)): Seq, $Box(ch#8)) } 
              $Is(ch#8, Tclass._module.Node?())
                 ==> 
                Seq#Contains($Unbox(read($Heap, n#15, _module.Node.children)): Seq, $Box(ch#8))
                   && ch#8 != null
                 ==> $Unbox(read($Heap, ch#8, _module.Node.marked)): bool));
      free invariant true;
      invariant {:id "id301"} $w$loop#0
         ==> (forall n#17: ref :: 
          { read(old($Heap), n#17, _module.Node.childrenVisited) } 
            { read($Heap, n#17, _module.Node.childrenVisited) } 
            { Seq#Contains(stackNodes#0, $Box(n#17)) } 
            { Set#IsMember(S#0, $Box(n#17)) } 
          $Is(n#17, Tclass._module.Node())
             ==> 
            Set#IsMember(S#0, $Box(n#17))
               && !Seq#Contains(stackNodes#0, $Box(n#17))
               && n#17 != t#0
             ==> $Unbox(read($Heap, n#17, _module.Node.childrenVisited)): int
               == $Unbox(read(old($Heap), n#17, _module.Node.childrenVisited)): int);
      free invariant true;
      invariant {:id "id306"} $w$loop#0
         ==> (forall n#19: ref :: 
          { read(old($Heap), n#19, _module.Node.children) } 
            { read($Heap, n#19, _module.Node.children) } 
            { Set#IsMember(S#0, $Box(n#19)) } 
          $Is(n#19, Tclass._module.Node())
             ==> 
            Set#IsMember(S#0, $Box(n#19))
             ==> Seq#Equal($Unbox(read($Heap, n#19, _module.Node.children)): Seq, 
              $Unbox(read(old($Heap), n#19, _module.Node.children)): Seq));
      free invariant true;
      invariant {:id "id309"} $w$loop#0
         ==> (forall n#21: ref :: 
          { Set#IsMember(unmarkedNodes#0, $Box(n#21)) } 
            { read($Heap, n#21, _module.Node.marked) } 
            { Set#IsMember(S#0, $Box(n#21)) } 
          $Is(n#21, Tclass._module.Node())
             ==> 
            Set#IsMember(S#0, $Box(n#21))
               && !$Unbox(read($Heap, n#21, _module.Node.marked)): bool
             ==> Set#IsMember(unmarkedNodes#0, $Box(n#21)));
      free invariant (forall $o: ref :: 
        { $Heap[$o] } 
        $o != null && $Unbox(read(old($Heap), $o, alloc)): bool
           ==> $Heap[$o] == $PreLoopHeap$loop#0[$o] || Set#IsMember(S#0, $Box($o)));
      free invariant $HeapSucc($PreLoopHeap$loop#0, $Heap);
      free invariant (forall $o: ref, $f: Field :: 
        { read($Heap, $o, $f) } 
        $o != null && $Unbox(read($PreLoopHeap$loop#0, $o, alloc)): bool
           ==> read($Heap, $o, $f) == read($PreLoopHeap$loop#0, $o, $f)
             || $_ModifiesFrame[$o, $f]);
      free invariant (Set#Subset(unmarkedNodes#0, $decr_init$loop#00)
           && !Set#Subset($decr_init$loop#00, unmarkedNodes#0))
         || (Set#Subset(unmarkedNodes#0, $decr_init$loop#00)
           && (Seq#Rank(stackNodes#0) < Seq#Rank($decr_init$loop#01)
             || (Seq#Rank(stackNodes#0) <= Seq#Rank($decr_init$loop#01)
               && ((0 <= $decr_init$loop#02
                   && Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq)
                       - $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                     < $decr_init$loop#02)
                 || 
                (Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq)
                     - $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                   == $decr_init$loop#02
                 || (0 <= $decr_init$loop#02
                   && Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq)
                       - $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                     <= $decr_init$loop#02))))));
    {
        assume {:captureState "module:_module:state:20"} true;
        if (!$w$loop#0)
        {
            assert {:id "id248"} {:subsumption 0} root#0 != null;
            assume true;
            if ($Unbox(read($Heap, root#0, _module.Node.marked)): bool)
            {
            }

            if ($Unbox(read($Heap, root#0, _module.Node.marked)): bool
               && Set#IsMember(S#0, $Box(t#0)))
            {
            }

            assume true;
            assume {:id "id249"} $Unbox(read($Heap, root#0, _module.Node.marked)): bool
               && Set#IsMember(S#0, $Box(t#0))
               && !Seq#Contains(stackNodes#0, $Box(t#0));
            // Begin Comprehension WF check
            havoc i#0;
            havoc j#0;
            if (true)
            {
                if (LitInt(0) <= i#0)
                {
                }

                if (LitInt(0) <= i#0 && i#0 < j#0)
                {
                }

                if (LitInt(0) <= i#0 && i#0 < j#0 && j#0 < Seq#Length(stackNodes#0))
                {
                    assert {:id "id253"} {:subsumption 0} 0 <= i#0 && i#0 < Seq#Length(stackNodes#0);
                    assert {:id "id254"} {:subsumption 0} 0 <= j#0 && j#0 < Seq#Length(stackNodes#0);
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id255"} (forall i#1: int, j#1: int :: 
              { Seq#Index(stackNodes#0, j#1), Seq#Index(stackNodes#0, i#1) } 
              LitInt(0) <= i#1 && i#1 < j#1 && j#1 < Seq#Length(stackNodes#0)
                 ==> $Unbox(Seq#Index(stackNodes#0, i#1)): ref
                   != $Unbox(Seq#Index(stackNodes#0, j#1)): ref);
            // Begin Comprehension WF check
            havoc n#8;
            if ($Is(n#8, Tclass._module.Node()) && $IsAlloc(n#8, Tclass._module.Node(), $Heap))
            {
                if (Seq#Contains(stackNodes#0, $Box(n#8)))
                {
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id257"} (forall n#9: ref :: 
              { Set#IsMember(S#0, $Box(n#9)) } { Seq#Contains(stackNodes#0, $Box(n#9)) } 
              $Is(n#9, Tclass._module.Node())
                 ==> 
                Seq#Contains(stackNodes#0, $Box(n#9))
                 ==> Set#IsMember(S#0, $Box(n#9)));
            // Begin Comprehension WF check
            havoc n#10;
            if ($Is(n#10, Tclass._module.Node()) && $IsAlloc(n#10, Tclass._module.Node(), $Heap))
            {
                if (!Seq#Contains(stackNodes#0, $Box(n#10)))
                {
                }

                if (Seq#Contains(stackNodes#0, $Box(n#10)) || n#10 == t#0)
                {
                    assert {:id "id259"} {:subsumption 0} n#10 != null;
                    assume true;
                    if ($Unbox(read($Heap, n#10, _module.Node.marked)): bool)
                    {
                        assert {:id "id260"} {:subsumption 0} n#10 != null;
                        assume true;
                        if (LitInt(0) <= $Unbox(read($Heap, n#10, _module.Node.childrenVisited)): int)
                        {
                            assert {:id "id261"} {:subsumption 0} n#10 != null;
                            assume true;
                            assert {:id "id262"} {:subsumption 0} n#10 != null;
                            assume true;
                        }
                    }

                    if ($Unbox(read($Heap, n#10, _module.Node.marked)): bool
                       && 
                      (LitInt(0) <= $Unbox(read($Heap, n#10, _module.Node.childrenVisited)): int
                       && $Unbox(read($Heap, n#10, _module.Node.childrenVisited)): int
                         <= Seq#Length($Unbox(read($Heap, n#10, _module.Node.children)): Seq)))
                    {
                        // Begin Comprehension WF check
                        havoc j#2;
                        if (true)
                        {
                            if (LitInt(0) <= j#2)
                            {
                                assert {:id "id263"} {:subsumption 0} n#10 != null;
                                assume true;
                            }

                            if (LitInt(0) <= j#2
                               && j#2 < $Unbox(read($Heap, n#10, _module.Node.childrenVisited)): int)
                            {
                                assert {:id "id264"} {:subsumption 0} n#10 != null;
                                assume true;
                                assert {:id "id265"} {:subsumption 0} 0 <= j#2
                                   && j#2 < Seq#Length($Unbox(read($Heap, n#10, _module.Node.children)): Seq);
                                newtype$check#2 := null;
                                if ($Unbox(Seq#Index($Unbox(read($Heap, n#10, _module.Node.children)): Seq, j#2)): ref
                                   != null)
                                {
                                    assert {:id "id266"} {:subsumption 0} n#10 != null;
                                    assume true;
                                    assert {:id "id267"} {:subsumption 0} 0 <= j#2
                                       && j#2 < Seq#Length($Unbox(read($Heap, n#10, _module.Node.children)): Seq);
                                    assert {:id "id268"} {:subsumption 0} $Unbox(Seq#Index($Unbox(read($Heap, n#10, _module.Node.children)): Seq, j#2)): ref
                                       != null;
                                    assume true;
                                }
                            }
                        }

                        assume true;
                        // End Comprehension WF check
                        assume true;
                    }
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id269"} (forall n#11: ref :: 
                { read($Heap, n#11, _module.Node.marked) } 
                  { Seq#Contains(stackNodes#0, $Box(n#11)) } 
                $Is(n#11, Tclass._module.Node()) && $IsAlloc(n#11, Tclass._module.Node(), $Heap)
                   ==> 
                  Seq#Contains(stackNodes#0, $Box(n#11)) || n#11 == t#0
                   ==> $Unbox(read($Heap, n#11, _module.Node.marked)): bool)
               && (forall n#11: ref :: 
                { read($Heap, n#11, _module.Node.childrenVisited) } 
                  { Seq#Contains(stackNodes#0, $Box(n#11)) } 
                $Is(n#11, Tclass._module.Node()) && $IsAlloc(n#11, Tclass._module.Node(), $Heap)
                   ==> 
                  Seq#Contains(stackNodes#0, $Box(n#11)) || n#11 == t#0
                   ==> LitInt(0) <= $Unbox(read($Heap, n#11, _module.Node.childrenVisited)): int)
               && (forall n#11: ref :: 
                { read($Heap, n#11, _module.Node.children) } 
                  { read($Heap, n#11, _module.Node.childrenVisited) } 
                  { Seq#Contains(stackNodes#0, $Box(n#11)) } 
                $Is(n#11, Tclass._module.Node()) && $IsAlloc(n#11, Tclass._module.Node(), $Heap)
                   ==> (Seq#Contains(stackNodes#0, $Box(n#11)) || n#11 == t#0
                       ==> $Unbox(read($Heap, n#11, _module.Node.childrenVisited)): int
                         <= Seq#Length($Unbox(read($Heap, n#11, _module.Node.children)): Seq))
                     && (Seq#Contains(stackNodes#0, $Box(n#11)) || n#11 == t#0
                       ==> (forall j#4: int :: 
                        { Seq#Index($Unbox(read($Heap, n#11, _module.Node.children)): Seq, j#4) } 
                        LitInt(0) <= j#4
                             && j#4 < $Unbox(read($Heap, n#11, _module.Node.childrenVisited)): int
                           ==> $Unbox(Seq#Index($Unbox(read($Heap, n#11, _module.Node.children)): Seq, j#4)): ref
                               == null
                             || $Unbox(read($Heap, 
                                $Unbox(Seq#Index($Unbox(read($Heap, n#11, _module.Node.children)): Seq, j#4)): ref, 
                                _module.Node.marked)): bool)));
            // Begin Comprehension WF check
            havoc n#12;
            if ($Is(n#12, Tclass._module.Node()) && $IsAlloc(n#12, Tclass._module.Node(), $Heap))
            {
                if (Seq#Contains(stackNodes#0, $Box(n#12)))
                {
                    assert {:id "id273"} {:subsumption 0} n#12 != null;
                    assume true;
                    assert {:id "id274"} {:subsumption 0} n#12 != null;
                    assume true;
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id275"} (forall n#13: ref :: 
              { read($Heap, n#13, _module.Node.children) } 
                { read($Heap, n#13, _module.Node.childrenVisited) } 
                { Seq#Contains(stackNodes#0, $Box(n#13)) } 
              $Is(n#13, Tclass._module.Node())
                 ==> 
                Seq#Contains(stackNodes#0, $Box(n#13))
                 ==> $Unbox(read($Heap, n#13, _module.Node.childrenVisited)): int
                   < Seq#Length($Unbox(read($Heap, n#13, _module.Node.children)): Seq));
            // Begin Comprehension WF check
            havoc j#5;
            if (true)
            {
                if (LitInt(0) <= j#5)
                {
                }

                if (LitInt(0) <= j#5 && j#5 + 1 < Seq#Length(stackNodes#0))
                {
                    assert {:id "id277"} {:subsumption 0} 0 <= j#5 && j#5 < Seq#Length(stackNodes#0);
                    assert {:id "id278"} {:subsumption 0} $Unbox(Seq#Index(stackNodes#0, j#5)): ref != null;
                    assume true;
                    assert {:id "id279"} {:subsumption 0} 0 <= j#5 && j#5 < Seq#Length(stackNodes#0);
                    assert {:id "id280"} {:subsumption 0} $Unbox(Seq#Index(stackNodes#0, j#5)): ref != null;
                    assume true;
                    assert {:id "id281"} {:subsumption 0} 0
                         <= $Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, j#5)): ref, _module.Node.childrenVisited)): int
                       && $Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, j#5)): ref, _module.Node.childrenVisited)): int
                         < Seq#Length($Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, j#5)): ref, _module.Node.children)): Seq);
                    assert {:id "id282"} {:subsumption 0} 0 <= j#5 + 1 && j#5 + 1 < Seq#Length(stackNodes#0);
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id283"} (forall j#6: int, _t#0#0: int :: 
              { Seq#Index(stackNodes#0, _t#0#0), Seq#Index(stackNodes#0, j#6) } 
              _t#0#0 == j#6 + 1
                 ==> 
                LitInt(0) <= j#6 && _t#0#0 < Seq#Length(stackNodes#0)
                 ==> $Unbox(Seq#Index($Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, j#6)): ref, _module.Node.children)): Seq, 
                      $Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, j#6)): ref, _module.Node.childrenVisited)): int)): ref
                   == $Unbox(Seq#Index(stackNodes#0, _t#0#0)): ref);
            if (0 < Seq#Length(stackNodes#0))
            {
                assert {:id "id285"} {:subsumption 0} 0 <= Seq#Length(stackNodes#0) - 1
                   && Seq#Length(stackNodes#0) - 1 < Seq#Length(stackNodes#0);
                assert {:id "id286"} {:subsumption 0} $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref != null;
                assume true;
                assert {:id "id287"} {:subsumption 0} 0 <= Seq#Length(stackNodes#0) - 1
                   && Seq#Length(stackNodes#0) - 1 < Seq#Length(stackNodes#0);
                assert {:id "id288"} {:subsumption 0} $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref != null;
                assume true;
                assert {:id "id289"} {:subsumption 0} 0
                     <= $Unbox(read($Heap, 
                        $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                        _module.Node.childrenVisited)): int
                   && $Unbox(read($Heap, 
                        $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                        _module.Node.childrenVisited)): int
                     < Seq#Length($Unbox(read($Heap, 
                          $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                          _module.Node.children)): Seq);
            }

            assume true;
            assume {:id "id290"} 0 < Seq#Length(stackNodes#0)
               ==> $Unbox(Seq#Index($Unbox(read($Heap, 
                        $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                        _module.Node.children)): Seq, 
                    $Unbox(read($Heap, 
                        $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                        _module.Node.childrenVisited)): int)): ref
                 == t#0;
            // Begin Comprehension WF check
            havoc n#14;
            if ($Is(n#14, Tclass._module.Node()) && $IsAlloc(n#14, Tclass._module.Node(), $Heap))
            {
                if (Set#IsMember(S#0, $Box(n#14)))
                {
                    assert {:id "id292"} {:subsumption 0} n#14 != null;
                    assume true;
                }

                if (Set#IsMember(S#0, $Box(n#14))
                   && $Unbox(read($Heap, n#14, _module.Node.marked)): bool)
                {
                }

                if (Set#IsMember(S#0, $Box(n#14))
                   && $Unbox(read($Heap, n#14, _module.Node.marked)): bool
                   && !Seq#Contains(stackNodes#0, $Box(n#14)))
                {
                }

                if (Set#IsMember(S#0, $Box(n#14))
                   && $Unbox(read($Heap, n#14, _module.Node.marked)): bool
                   && !Seq#Contains(stackNodes#0, $Box(n#14))
                   && n#14 != t#0)
                {
                    // Begin Comprehension WF check
                    havoc ch#6;
                    if ($Is(ch#6, Tclass._module.Node?())
                       && $IsAlloc(ch#6, Tclass._module.Node?(), $Heap))
                    {
                        assert {:id "id293"} {:subsumption 0} n#14 != null;
                        assume true;
                        if (Seq#Contains($Unbox(read($Heap, n#14, _module.Node.children)): Seq, $Box(ch#6)))
                        {
                            newtype$check#3 := null;
                        }

                        if (Seq#Contains($Unbox(read($Heap, n#14, _module.Node.children)): Seq, $Box(ch#6))
                           && ch#6 != null)
                        {
                            assert {:id "id294"} {:subsumption 0} ch#6 != null;
                            assume true;
                        }
                    }

                    assume true;
                    // End Comprehension WF check
                    assume true;
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id295"} (forall n#15: ref :: 
              { read($Heap, n#15, _module.Node.children) } 
                { Seq#Contains(stackNodes#0, $Box(n#15)) } 
                { read($Heap, n#15, _module.Node.marked) } 
                { Set#IsMember(S#0, $Box(n#15)) } 
              $Is(n#15, Tclass._module.Node())
                 ==> 
                Set#IsMember(S#0, $Box(n#15))
                   && $Unbox(read($Heap, n#15, _module.Node.marked)): bool
                   && !Seq#Contains(stackNodes#0, $Box(n#15))
                   && n#15 != t#0
                 ==> (forall ch#8: ref :: 
                  { read($Heap, ch#8, _module.Node.marked) } 
                    { Seq#Contains($Unbox(read($Heap, n#15, _module.Node.children)): Seq, $Box(ch#8)) } 
                  $Is(ch#8, Tclass._module.Node?())
                     ==> 
                    Seq#Contains($Unbox(read($Heap, n#15, _module.Node.children)): Seq, $Box(ch#8))
                       && ch#8 != null
                     ==> $Unbox(read($Heap, ch#8, _module.Node.marked)): bool));
            // Begin Comprehension WF check
            havoc n#16;
            if ($Is(n#16, Tclass._module.Node()) && $IsAlloc(n#16, Tclass._module.Node(), $Heap))
            {
                if (Set#IsMember(S#0, $Box(n#16)))
                {
                }

                if (Set#IsMember(S#0, $Box(n#16)) && !Seq#Contains(stackNodes#0, $Box(n#16)))
                {
                }

                if (Set#IsMember(S#0, $Box(n#16))
                   && !Seq#Contains(stackNodes#0, $Box(n#16))
                   && n#16 != t#0)
                {
                    assert {:id "id297"} {:subsumption 0} n#16 != null;
                    assume true;
                    assert {:id "id298"} {:subsumption 0} n#16 != null;
                    assert {:id "id299"} $IsAlloc(n#16, Tclass._module.Node(), old($Heap));
                    assume true;
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id300"} (forall n#17: ref :: 
              { read(old($Heap), n#17, _module.Node.childrenVisited) } 
                { read($Heap, n#17, _module.Node.childrenVisited) } 
                { Seq#Contains(stackNodes#0, $Box(n#17)) } 
                { Set#IsMember(S#0, $Box(n#17)) } 
              $Is(n#17, Tclass._module.Node())
                 ==> 
                Set#IsMember(S#0, $Box(n#17))
                   && !Seq#Contains(stackNodes#0, $Box(n#17))
                   && n#17 != t#0
                 ==> $Unbox(read($Heap, n#17, _module.Node.childrenVisited)): int
                   == $Unbox(read(old($Heap), n#17, _module.Node.childrenVisited)): int);
            // Begin Comprehension WF check
            havoc n#18;
            if ($Is(n#18, Tclass._module.Node()) && $IsAlloc(n#18, Tclass._module.Node(), $Heap))
            {
                if (Set#IsMember(S#0, $Box(n#18)))
                {
                    assert {:id "id302"} {:subsumption 0} n#18 != null;
                    assume true;
                    assert {:id "id303"} {:subsumption 0} n#18 != null;
                    assert {:id "id304"} $IsAlloc(n#18, Tclass._module.Node(), old($Heap));
                    assume true;
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id305"} (forall n#19: ref :: 
              { read(old($Heap), n#19, _module.Node.children) } 
                { read($Heap, n#19, _module.Node.children) } 
                { Set#IsMember(S#0, $Box(n#19)) } 
              $Is(n#19, Tclass._module.Node())
                 ==> 
                Set#IsMember(S#0, $Box(n#19))
                 ==> Seq#Equal($Unbox(read($Heap, n#19, _module.Node.children)): Seq, 
                  $Unbox(read(old($Heap), n#19, _module.Node.children)): Seq));
            // Begin Comprehension WF check
            havoc n#20;
            if ($Is(n#20, Tclass._module.Node()) && $IsAlloc(n#20, Tclass._module.Node(), $Heap))
            {
                if (Set#IsMember(S#0, $Box(n#20)))
                {
                    assert {:id "id307"} {:subsumption 0} n#20 != null;
                    assume true;
                }

                if (Set#IsMember(S#0, $Box(n#20))
                   && !$Unbox(read($Heap, n#20, _module.Node.marked)): bool)
                {
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id308"} (forall n#21: ref :: 
              { Set#IsMember(unmarkedNodes#0, $Box(n#21)) } 
                { read($Heap, n#21, _module.Node.marked) } 
                { Set#IsMember(S#0, $Box(n#21)) } 
              $Is(n#21, Tclass._module.Node())
                 ==> 
                Set#IsMember(S#0, $Box(n#21))
                   && !$Unbox(read($Heap, n#21, _module.Node.marked)): bool
                 ==> Set#IsMember(unmarkedNodes#0, $Box(n#21)));
            assume true;
            assume true;
            assert {:id "id310"} t#0 != null;
            assume true;
            assert {:id "id311"} t#0 != null;
            assume true;
            assume true;
            assume false;
        }

        assume true;
        if (!Lit(true))
        {
            break;
        }

        assume true;
        assume true;
        assume true;
        $decr$loop#00 := unmarkedNodes#0;
        $decr$loop#01 := stackNodes#0;
        $decr$loop#02 := Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq)
           - $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int;
        // ----- if statement ----- module:_module:statement:15
        assert {:id "id312"} t#0 != null;
        assume true;
        assert {:id "id313"} t#0 != null;
        assume true;
        assume true;
        if {:allow_path_isolation} ($Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
           == Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq))
        {
            // ----- assert statement ----- module:_module:statement:16
            assume true;
            assert {:id "id314"} {:focus} Lit(true);
            // ----- assignment statement ----- module:_module:statement:17
            assert {:id "id315"} t#0 != null;
            assume true;
            assume true;
            assert {:id "id316"} $_ModifiesFrame[t#0, _module.Node.childrenVisited];
            assert {:id "id317"} $Is(LitInt(0), Tclass._System.nat());
            assume true;
            $rhs#0_0_0 := {:id "id318"} LitInt(0);
            $Heap := {:id "id319"} update($Heap, t#0, _module.Node.childrenVisited, $Box($rhs#0_0_0));
            assume $IsGoodHeap($Heap);
            assume {:captureState "module:_module:state:21"} true;
            // ----- if statement ----- module:_module:statement:18
            assume true;
            if {:allow_path_isolation} (Seq#Length(stackNodes#0) == LitInt(0))
            {
                push;
                // ----- return statement ----- module:_module:statement:19
                pop;
                return;

                pop;
            }
            else
            {
            }

            // ----- assignment statement ----- module:_module:statement:20
            assume true;
            assert {:id "id320"} 0 <= Seq#Length(stackNodes#0) - 1
               && Seq#Length(stackNodes#0) - 1 < Seq#Length(stackNodes#0);
            assume true;
            t#0 := {:id "id321"} $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref;
            assume {:captureState "module:_module:state:22"} true;
            // ----- assignment statement ----- module:_module:statement:21
            assume true;
            assert {:id "id322"} 0 <= Seq#Length(stackNodes#0) - 1
               && Seq#Length(stackNodes#0) - 1 <= Seq#Length(stackNodes#0);
            assume true;
            stackNodes#0 := {:id "id323"} Seq#Take(stackNodes#0, Seq#Length(stackNodes#0) - 1);
            assume {:captureState "module:_module:state:23"} true;
            // ----- assignment statement ----- module:_module:statement:22
            assert {:id "id324"} t#0 != null;
            assume true;
            assume true;
            assert {:id "id325"} $_ModifiesFrame[t#0, _module.Node.childrenVisited];
            assert {:id "id326"} t#0 != null;
            assume true;
            assert {:id "id327"} $Is($Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int + 1, 
              Tclass._System.nat());
            assume true;
            $rhs#0_0_1 := {:id "id328"} $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int + 1;
            $Heap := {:id "id329"} update($Heap, t#0, _module.Node.childrenVisited, $Box($rhs#0_0_1));
            assume $IsGoodHeap($Heap);
            assume {:captureState "module:_module:state:24"} true;
        }
        else
        {
            // ----- if statement ----- module:_module:statement:23
            assert {:id "id330"} t#0 != null;
            assume true;
            assert {:id "id331"} t#0 != null;
            assume true;
            assert {:id "id332"} 0 <= $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
               && $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                 < Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq);
            newtype$check#0_1_0 := null;
            if ($Unbox(Seq#Index($Unbox(read($Heap, t#0, _module.Node.children)): Seq, 
                  $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int)): ref
               != null)
            {
                assert {:id "id333"} t#0 != null;
                assume true;
                assert {:id "id334"} t#0 != null;
                assume true;
                assert {:id "id335"} 0 <= $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                   && $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                     < Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq);
                assert {:id "id336"} $Unbox(Seq#Index($Unbox(read($Heap, t#0, _module.Node.children)): Seq, 
                      $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int)): ref
                   != null;
                assume true;
            }

            assume true;
            if {:allow_path_isolation} ($Unbox(Seq#Index($Unbox(read($Heap, t#0, _module.Node.children)): Seq, 
                    $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int)): ref
                 == null
               || $Unbox(read($Heap, 
                  $Unbox(Seq#Index($Unbox(read($Heap, t#0, _module.Node.children)): Seq, 
                      $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int)): ref, 
                  _module.Node.marked)): bool)
            {
                // ----- assignment statement ----- module:_module:statement:24
                assert {:id "id337"} t#0 != null;
                assume true;
                assume true;
                assert {:id "id338"} $_ModifiesFrame[t#0, _module.Node.childrenVisited];
                assert {:id "id339"} t#0 != null;
                assume true;
                assert {:id "id340"} $Is($Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int + 1, 
                  Tclass._System.nat());
                assume true;
                $rhs#0_1_0_0 := {:id "id341"} $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int + 1;
                $Heap := {:id "id342"} update($Heap, t#0, _module.Node.childrenVisited, $Box($rhs#0_1_0_0));
                assume $IsGoodHeap($Heap);
                assume {:captureState "module:_module:state:25"} true;
            }
            else
            {
                // ----- assert statement ----- module:_module:statement:25
                assume true;
                assert {:id "id343"} {:focus} Lit(true);
                // ----- assignment statement ----- module:_module:statement:26
                assume true;
                assert {:id "id344"} $Is(Seq#Append(stackNodes#0, Seq#Build(Seq#Empty(): Seq, $Box(t#0))), 
                  TSeq(Tclass._module.Node()));
                assume true;
                stackNodes#0 := {:id "id345"} Seq#Append(stackNodes#0, Seq#Build(Seq#Empty(): Seq, $Box(t#0)));
                assume {:captureState "module:_module:state:26"} true;
                // ----- assignment statement ----- module:_module:statement:27
                assume true;
                assert {:id "id346"} t#0 != null;
                assume true;
                assert {:id "id347"} t#0 != null;
                assume true;
                assert {:id "id348"} 0 <= $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                   && $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                     < Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq);
                assume true;
                t#0 := {:id "id349"} $Unbox(Seq#Index($Unbox(read($Heap, t#0, _module.Node.children)): Seq, 
                    $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int)): ref;
                assume {:captureState "module:_module:state:27"} true;
                // ----- assignment statement ----- module:_module:statement:28
                assert {:id "id350"} t#0 != null;
                assume true;
                assume true;
                assert {:id "id351"} $_ModifiesFrame[t#0, _module.Node.marked];
                assume true;
                $rhs#0_1_1_0 := {:id "id352"} Lit(true);
                $Heap := {:id "id353"} update($Heap, t#0, _module.Node.marked, $Box($rhs#0_1_1_0));
                assume $IsGoodHeap($Heap);
                assume {:captureState "module:_module:state:28"} true;
                // ----- assignment statement ----- module:_module:statement:29
                assume true;
                assume true;
                unmarkedNodes#0 := {:id "id354"} Set#Difference(unmarkedNodes#0, Set#UnionOne(Set#Empty(): Set, $Box(t#0)));
                assume {:captureState "module:_module:state:29"} true;
            }
        }

        assume true;
        assume true;
        assume true;
        // ----- loop termination check ----- module:_module:statement:30
        assert {:id "id355"} (Set#Subset(unmarkedNodes#0, $decr$loop#00)
             && !Set#Subset($decr$loop#00, unmarkedNodes#0))
           || (Set#Subset(unmarkedNodes#0, $decr$loop#00)
             && (Seq#Rank(stackNodes#0) < Seq#Rank($decr$loop#01)
               || (Seq#Rank(stackNodes#0) <= Seq#Rank($decr$loop#01)
                 && 
                (0 <= $decr$loop#02
                 && Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq)
                     - $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                   < $decr$loop#02))));
        assume true;
    }
}



// function declaration for _module._default.Reachable
function _module.__default.Reachable($heap: Heap, source#0: ref, sink#0: ref, S#0: Set) : bool;

function _module.__default.Reachable#canCall($heap: Heap, source#0: ref, sink#0: ref, S#0: Set) : bool;

// frame axiom for _module.__default.Reachable
axiom (forall $h0: Heap, $h1: Heap, source#0: ref, sink#0: ref, S#0: Set :: 
  { $IsHeapAnchor($h0), $HeapSucc($h0, $h1), _module.__default.Reachable($h1, source#0, sink#0, S#0) } 
  $IsGoodHeap($h0)
       && $IsGoodHeap($h1)
       && (
        _module.__default.Reachable#canCall($h0, source#0, sink#0, S#0)
         || (
          $Is(source#0, Tclass._module.Node())
           && $Is(sink#0, Tclass._module.Node())
           && $Is(S#0, TSet(Tclass._module.Node())))
         || 
        (_module.__default.Reachable#canCall($h1, source#0, sink#0, S#0)
         || (
          $Is(source#0, Tclass._module.Node())
           && $Is(sink#0, Tclass._module.Node())
           && $Is(S#0, TSet(Tclass._module.Node())))))
       && 
      ($IsHeapAnchor($h0)
       && $HeapSucc($h0, $h1))
     ==> 
    (forall $o: ref, $f: Field :: 
      $o != null && Set#IsMember(S#0, $Box($o))
         ==> read($h0, $o, $f) == read($h1, $o, $f))
     ==> _module.__default.Reachable($h0, source#0, sink#0, S#0)
         == _module.__default.Reachable($h1, source#0, sink#0, S#0)
       && _module.__default.Reachable#canCall($h0, source#0, sink#0, S#0)
         == _module.__default.Reachable#canCall($h1, source#0, sink#0, S#0));

function _module.__default.Reachable#requires(Heap, ref, ref, Set) : bool;

// #requires axiom for _module.__default.Reachable
axiom (forall $Heap: Heap, source#0: ref, sink#0: ref, S#0: Set :: 
  { _module.__default.Reachable#requires($Heap, source#0, sink#0, S#0), $IsGoodHeap($Heap) } 
  $IsGoodHeap($Heap)
       && $Is(source#0, Tclass._module.Node())
       && $Is(sink#0, Tclass._module.Node())
       && $Is(S#0, TSet(Tclass._module.Node()))
     ==> _module.__default.Reachable#requires($Heap, source#0, sink#0, S#0) == true);

// #requires ==> #canCall for _module.__default.Reachable
axiom (forall $Heap: Heap, source#0: ref, sink#0: ref, S#0: Set :: 
  { _module.__default.Reachable#requires($Heap, source#0, sink#0, S#0), $IsGoodHeap($Heap) } 
  _module.__default.Reachable#requires($Heap, source#0, sink#0, S#0)
     ==> _module.__default.Reachable#canCall($Heap, source#0, sink#0, S#0));

function Tclass._module.Path() : Ty
uses {
// Tclass._module.Path Tag
axiom Tag(Tclass._module.Path()) == Tagclass._module.Path
   && TagFamily(Tclass._module.Path()) == tytagFamily$Path;
}

const unique Tagclass._module.Path: TyTag;

// Box/unbox axiom for Tclass._module.Path
axiom (forall bx: Box :: 
  { $IsBox(bx, Tclass._module.Path()) } 
  $IsBox(bx, Tclass._module.Path())
     ==> $Box($Unbox(bx): DatatypeType) == bx
       && $Is($Unbox(bx): DatatypeType, Tclass._module.Path()));

// definition axiom for _module.__default.Reachable (revealed)
hideable axiom {:id "id356"} (forall $Heap: Heap, source#0: ref, sink#0: ref, S#0: Set :: 
  { _module.__default.Reachable($Heap, source#0, sink#0, S#0), $IsGoodHeap($Heap) } 
  _module.__default.Reachable#canCall($Heap, source#0, sink#0, S#0)
     ==> (forall via#0: DatatypeType :: 
        { _module.__default.ReachableVia($LS($LZ), $Heap, source#0, via#0, sink#0, S#0) } 
        $Is(via#0, Tclass._module.Path())
           ==> _module.__default.ReachableVia#canCall($Heap, source#0, via#0, sink#0, S#0))
       && _module.__default.Reachable($Heap, source#0, sink#0, S#0)
         == (exists via#0: DatatypeType :: 
          { _module.__default.ReachableVia($LS($LZ), $Heap, source#0, via#0, sink#0, S#0) } 
          $Is(via#0, Tclass._module.Path())
             && _module.__default.ReachableVia($LS($LZ), $Heap, source#0, via#0, sink#0, S#0)));

procedure {:verboseName "Reachable (well-formedness)"} CheckWellformed$$_module.__default.Reachable(source#0: ref where $Is(source#0, Tclass._module.Node()), 
    sink#0: ref where $Is(sink#0, Tclass._module.Node()), 
    S#0: Set where $Is(S#0, TSet(Tclass._module.Node())));
  modifies $Heap;



implementation {:smt_option "smt.refine_inj_axioms", "false"} {:smt_option "smt.arith.solver", "2"} {:verboseName "Reachable (well-formedness)"} CheckWellformed$$_module.__default.Reachable(source#0: ref, sink#0: ref, S#0: Set)
{
  var $_ReadsFrame: [ref,Field]bool;
  var via#1: DatatypeType;
  var ##source#0: ref;
  var ##p#0: DatatypeType;
  var ##sink#0: ref;
  var ##S#0: Set;
  var b$reqreads#0: bool;

    b$reqreads#0 := true;

    assume {:captureState "module:_module:state:30"} true;
    $_ReadsFrame := (lambda $o: ref, $f: Field :: 
      $o != null && $Unbox(read($Heap, $o, alloc)): bool
         ==> Set#IsMember(S#0, $Box($o)));
    // Check well-formedness of preconditions, and then assume them
    // Check well-formedness of the reads clause
    // Check well-formedness of the decreases clause
    // Check body and ensures clauses
    if (*)
    {
        // Check well-formedness of postcondition and assume false
        assume false;
    }
    else
    {
        // Check well-formedness of body and result subset type constraint
        // Begin Comprehension WF check
        havoc via#1;
        if ($Is(via#1, Tclass._module.Path())
           && $IsAlloc(via#1, Tclass._module.Path(), $Heap))
        {
            ##source#0 := source#0;
            // assume allocatedness for argument to function
            assume $IsAlloc(##source#0, Tclass._module.Node(), $Heap);
            ##p#0 := via#1;
            // assume allocatedness for argument to function
            assume $IsAlloc(##p#0, Tclass._module.Path(), $Heap);
            ##sink#0 := sink#0;
            // assume allocatedness for argument to function
            assume $IsAlloc(##sink#0, Tclass._module.Node(), $Heap);
            ##S#0 := S#0;
            // assume allocatedness for argument to function
            assume $IsAlloc(##S#0, TSet(Tclass._module.Node()), $Heap);
            assume true;
            b$reqreads#0 := (forall $o: ref, $f: Field :: 
              $o != null
                   && $Unbox(read($Heap, $o, alloc)): bool
                   && Set#IsMember(##S#0, $Box($o))
                 ==> $_ReadsFrame[$o, $f]);
            assume _module.__default.ReachableVia#canCall($Heap, source#0, via#1, sink#0, S#0);
        }

        assume true;
        // End Comprehension WF check
        assume (forall via#2: DatatypeType :: 
          { _module.__default.ReachableVia($LS($LZ), $Heap, source#0, via#2, sink#0, S#0) } 
          $Is(via#2, Tclass._module.Path())
             ==> _module.__default.ReachableVia#canCall($Heap, source#0, via#2, sink#0, S#0));
        assume (forall via#2: DatatypeType :: 
          { _module.__default.ReachableVia($LS($LZ), $Heap, source#0, via#2, sink#0, S#0) } 
          $Is(via#2, Tclass._module.Path())
             ==> _module.__default.ReachableVia#canCall($Heap, source#0, via#2, sink#0, S#0));
        assume {:id "id357"} _module.__default.Reachable($Heap, source#0, sink#0, S#0)
           == (exists via#2: DatatypeType :: 
            { _module.__default.ReachableVia($LS($LZ), $Heap, source#0, via#2, sink#0, S#0) } 
            $Is(via#2, Tclass._module.Path())
               && _module.__default.ReachableVia($LS($LZ), $Heap, source#0, via#2, sink#0, S#0));
        // CheckWellformedWithResult: any expression
        assume $Is(_module.__default.Reachable($Heap, source#0, sink#0, S#0), TBool);
        assert {:id "id358"} b$reqreads#0;
        return;

        assume false;
    }
}



// function declaration for _module._default.ReachableVia
function _module.__default.ReachableVia($ly: LayerType, 
    $heap: Heap, 
    source#0: ref, 
    p#0: DatatypeType, 
    sink#0: ref, 
    S#0: Set)
   : bool;

function _module.__default.ReachableVia#canCall($heap: Heap, source#0: ref, p#0: DatatypeType, sink#0: ref, S#0: Set) : bool;

// layer synonym axiom
axiom (forall $ly: LayerType, 
    $Heap: Heap, 
    source#0: ref, 
    p#0: DatatypeType, 
    sink#0: ref, 
    S#0: Set :: 
  { _module.__default.ReachableVia($LS($ly), $Heap, source#0, p#0, sink#0, S#0) } 
  _module.__default.ReachableVia($LS($ly), $Heap, source#0, p#0, sink#0, S#0)
     == _module.__default.ReachableVia($ly, $Heap, source#0, p#0, sink#0, S#0));

// fuel synonym axiom
axiom (forall $ly: LayerType, 
    $Heap: Heap, 
    source#0: ref, 
    p#0: DatatypeType, 
    sink#0: ref, 
    S#0: Set :: 
  { _module.__default.ReachableVia(AsFuelBottom($ly), $Heap, source#0, p#0, sink#0, S#0) } 
  _module.__default.ReachableVia($ly, $Heap, source#0, p#0, sink#0, S#0)
     == _module.__default.ReachableVia($LZ, $Heap, source#0, p#0, sink#0, S#0));

// frame axiom for _module.__default.ReachableVia
axiom (forall $ly: LayerType, 
    $h0: Heap, 
    $h1: Heap, 
    source#0: ref, 
    p#0: DatatypeType, 
    sink#0: ref, 
    S#0: Set :: 
  { $IsHeapAnchor($h0), $HeapSucc($h0, $h1), _module.__default.ReachableVia($ly, $h1, source#0, p#0, sink#0, S#0) } 
  $IsGoodHeap($h0)
       && $IsGoodHeap($h1)
       && (
        _module.__default.ReachableVia#canCall($h0, source#0, p#0, sink#0, S#0)
         || (
          $Is(source#0, Tclass._module.Node())
           && $Is(p#0, Tclass._module.Path())
           && $Is(sink#0, Tclass._module.Node())
           && $Is(S#0, TSet(Tclass._module.Node())))
         || 
        (_module.__default.ReachableVia#canCall($h1, source#0, p#0, sink#0, S#0)
         || (
          $Is(source#0, Tclass._module.Node())
           && $Is(p#0, Tclass._module.Path())
           && $Is(sink#0, Tclass._module.Node())
           && $Is(S#0, TSet(Tclass._module.Node())))))
       && 
      ($IsHeapAnchor($h0)
       && $HeapSucc($h0, $h1))
     ==> 
    (forall $o: ref, $f: Field :: 
      $o != null && Set#IsMember(S#0, $Box($o))
         ==> read($h0, $o, $f) == read($h1, $o, $f))
     ==> _module.__default.ReachableVia($ly, $h0, source#0, p#0, sink#0, S#0)
         == _module.__default.ReachableVia($ly, $h1, source#0, p#0, sink#0, S#0)
       && _module.__default.ReachableVia#canCall($h0, source#0, p#0, sink#0, S#0)
         == _module.__default.ReachableVia#canCall($h1, source#0, p#0, sink#0, S#0));

// consequence axiom for _module.__default.ReachableVia
axiom (forall $ly: LayerType, 
    $Heap: Heap, 
    source#0: ref, 
    p#0: DatatypeType, 
    sink#0: ref, 
    S#0: Set :: 
  { _module.__default.ReachableVia($ly, $Heap, source#0, p#0, sink#0, S#0) } 
  _module.__default.ReachableVia#canCall($Heap, source#0, p#0, sink#0, S#0)
     ==> (forall $olderHeap: Heap :: 
      { $OlderTag($olderHeap) } 
      $IsGoodHeap($olderHeap)
           && $OlderTag($olderHeap)
           && 
          (_module.__default.ReachableVia($ly, $Heap, source#0, p#0, sink#0, S#0)
           && 
          (
          $IsAlloc(source#0, Tclass._module.Node(), $olderHeap)
           && $IsAlloc(sink#0, Tclass._module.Node(), $olderHeap)
           && $IsAlloc(S#0, TSet(Tclass._module.Node()), $olderHeap)))
         ==> $IsAlloc(p#0, Tclass._module.Path(), $olderHeap)));

function _module.__default.ReachableVia#requires(LayerType, Heap, ref, DatatypeType, ref, Set) : bool;

// #requires axiom for _module.__default.ReachableVia
axiom (forall $ly: LayerType, 
    $Heap: Heap, 
    source#0: ref, 
    p#0: DatatypeType, 
    sink#0: ref, 
    S#0: Set :: 
  { _module.__default.ReachableVia#requires($ly, $Heap, source#0, p#0, sink#0, S#0), $IsGoodHeap($Heap) } 
  $IsGoodHeap($Heap)
       && $Is(source#0, Tclass._module.Node())
       && $Is(p#0, Tclass._module.Path())
       && $Is(sink#0, Tclass._module.Node())
       && $Is(S#0, TSet(Tclass._module.Node()))
     ==> _module.__default.ReachableVia#requires($ly, $Heap, source#0, p#0, sink#0, S#0)
       == true);

// #requires ==> #canCall for _module.__default.ReachableVia
axiom (forall $ly: LayerType, 
    $Heap: Heap, 
    source#0: ref, 
    p#0: DatatypeType, 
    sink#0: ref, 
    S#0: Set :: 
  { _module.__default.ReachableVia#requires($ly, $Heap, source#0, p#0, sink#0, S#0), $IsGoodHeap($Heap) } 
  _module.__default.ReachableVia#requires($ly, $Heap, source#0, p#0, sink#0, S#0)
     ==> _module.__default.ReachableVia#canCall($Heap, source#0, p#0, sink#0, S#0));

// definition axiom for _module.__default.ReachableVia (revealed)
hideable axiom {:id "id359"} (forall $ly: LayerType, 
    $Heap: Heap, 
    source#0: ref, 
    p#0: DatatypeType, 
    sink#0: ref, 
    S#0: Set :: 
  { _module.__default.ReachableVia($LS($ly), $Heap, source#0, p#0, sink#0, S#0), $IsGoodHeap($Heap) } 
  _module.__default.ReachableVia#canCall($Heap, source#0, p#0, sink#0, S#0)
     ==> (!_module.Path.Empty_q(p#0)
         ==> (var n#1 := _module.Path._h1(p#0); 
          (var prefix#1 := _module.Path._h0(p#0); 
            Set#IsMember(S#0, $Box(n#1))
               ==> 
              Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(sink#0))
               ==> _module.__default.ReachableVia#canCall($Heap, source#0, prefix#1, n#1, S#0))))
       && _module.__default.ReachableVia($LS($ly), $Heap, source#0, p#0, sink#0, S#0)
         == (if _module.Path.Empty_q(p#0)
           then source#0 == sink#0
           else (var n#0 := _module.Path._h1(p#0); 
            (var prefix#0 := _module.Path._h0(p#0); 
              Set#IsMember(S#0, $Box(n#0))
                 && Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(sink#0))
                 && _module.__default.ReachableVia($ly, $Heap, source#0, prefix#0, n#0, S#0)))));

procedure {:verboseName "ReachableVia (well-formedness)"} CheckWellformed$$_module.__default.ReachableVia(source#0: ref where $Is(source#0, Tclass._module.Node()), 
    p#0: DatatypeType where $Is(p#0, Tclass._module.Path()), 
    sink#0: ref where $Is(sink#0, Tclass._module.Node()), 
    S#0: Set where $Is(S#0, TSet(Tclass._module.Node())));
  modifies $Heap;
  ensures (forall $olderHeap: Heap :: 
    { $OlderTag($olderHeap) } 
    $IsGoodHeap($olderHeap)
         && $OlderTag($olderHeap)
         && 
        (_module.__default.ReachableVia($LS($LZ), $Heap, source#0, p#0, sink#0, S#0)
         && 
        (
        $IsAlloc(source#0, Tclass._module.Node(), $olderHeap)
         && $IsAlloc(sink#0, Tclass._module.Node(), $olderHeap)
         && $IsAlloc(S#0, TSet(Tclass._module.Node()), $olderHeap)))
       ==> $IsAlloc(p#0, Tclass._module.Path(), $olderHeap));



implementation {:smt_option "smt.refine_inj_axioms", "false"} {:smt_option "smt.arith.solver", "2"} {:verboseName "ReachableVia (well-formedness)"} CheckWellformed$$_module.__default.ReachableVia(source#0: ref, p#0: DatatypeType, sink#0: ref, S#0: Set)
{
  var $_ReadsFrame: [ref,Field]bool;
  var _mcc#0#0: DatatypeType;
  var _mcc#1#0: ref;
  var n#Z#0: ref;
  var let#0#0#0: ref;
  var prefix#Z#0: DatatypeType;
  var let#1#0#0: DatatypeType;
  var ##source#0: ref;
  var ##p#0: DatatypeType;
  var ##sink#0: ref;
  var ##S#0: Set;
  var b$reqreads#0: bool;
  var b$reqreads#1: bool;

    b$reqreads#0 := true;
    b$reqreads#1 := true;

    assume {:captureState "module:_module:state:31"} true;
    $_ReadsFrame := (lambda $o: ref, $f: Field :: 
      $o != null && $Unbox(read($Heap, $o, alloc)): bool
         ==> Set#IsMember(S#0, $Box($o)));
    // Check well-formedness of preconditions, and then assume them
    // Check well-formedness of the reads clause
    // Check well-formedness of the decreases clause
    // Check body and ensures clauses
    if (*)
    {
        // Check well-formedness of postcondition and assume false
        assume false;
    }
    else
    {
        // Check well-formedness of body and result subset type constraint
        if (p#0 == #_module.Path.Empty())
        {
            assume true;
            assume {:id "id369"} _module.__default.ReachableVia($LS($LZ), $Heap, source#0, p#0, sink#0, S#0)
               == 
              (source#0
               == sink#0);
            // CheckWellformedWithResult: any expression
            assume $Is(_module.__default.ReachableVia($LS($LZ), $Heap, source#0, p#0, sink#0, S#0), 
              TBool);
            assert {:id "id370"} b$reqreads#0;
            assert {:id "id371"} b$reqreads#1;
            return;
        }
        else if (p#0 == #_module.Path.Extend(_mcc#0#0, _mcc#1#0))
        {
            assume $Is(_mcc#0#0, Tclass._module.Path());
            assume $Is(_mcc#1#0, Tclass._module.Node());
            havoc n#Z#0;
            assume true;
            assume {:id "id360"} let#0#0#0 == _mcc#1#0;
            // CheckWellformedWithResult: any expression
            assume $Is(let#0#0#0, Tclass._module.Node());
            assume {:id "id361"} n#Z#0 == let#0#0#0;
            havoc prefix#Z#0;
            assume true;
            assume {:id "id362"} let#1#0#0 == _mcc#0#0;
            // CheckWellformedWithResult: any expression
            assume $Is(let#1#0#0, Tclass._module.Path());
            assume {:id "id363"} prefix#Z#0 == let#1#0#0;
            if (Set#IsMember(S#0, $Box(n#Z#0)))
            {
                assert {:id "id364"} n#Z#0 != null;
                b$reqreads#0 := $_ReadsFrame[n#Z#0, _module.Node.children];
                assume true;
            }

            if (Set#IsMember(S#0, $Box(n#Z#0))
               && Seq#Contains($Unbox(read($Heap, n#Z#0, _module.Node.children)): Seq, $Box(sink#0)))
            {
                ##source#0 := source#0;
                // assume allocatedness for argument to function
                assume $IsAlloc(##source#0, Tclass._module.Node(), $Heap);
                ##p#0 := prefix#Z#0;
                // assume allocatedness for argument to function
                assume $IsAlloc(##p#0, Tclass._module.Path(), $Heap);
                ##sink#0 := n#Z#0;
                // assume allocatedness for argument to function
                assume $IsAlloc(##sink#0, Tclass._module.Node(), $Heap);
                ##S#0 := S#0;
                // assume allocatedness for argument to function
                assume $IsAlloc(##S#0, TSet(Tclass._module.Node()), $Heap);
                assume true;
                b$reqreads#1 := (forall $o: ref, $f: Field :: 
                  $o != null
                       && $Unbox(read($Heap, $o, alloc)): bool
                       && Set#IsMember(##S#0, $Box($o))
                     ==> $_ReadsFrame[$o, $f]);
                assume true;
                assert {:id "id365"} DtRank(##p#0) < DtRank(p#0);
                assume _module.__default.ReachableVia#canCall($Heap, source#0, prefix#Z#0, n#Z#0, S#0);
            }

            assume Set#IsMember(S#0, $Box(n#Z#0))
               ==> 
              Seq#Contains($Unbox(read($Heap, n#Z#0, _module.Node.children)): Seq, $Box(sink#0))
               ==> _module.__default.ReachableVia#canCall($Heap, source#0, prefix#Z#0, n#Z#0, S#0);
            assume {:id "id366"} _module.__default.ReachableVia($LS($LZ), $Heap, source#0, p#0, sink#0, S#0)
               == (
                Set#IsMember(S#0, $Box(n#Z#0))
                 && Seq#Contains($Unbox(read($Heap, n#Z#0, _module.Node.children)): Seq, $Box(sink#0))
                 && _module.__default.ReachableVia($LS($LZ), $Heap, source#0, prefix#Z#0, n#Z#0, S#0));
            // CheckWellformedWithResult: any expression
            assume $Is(_module.__default.ReachableVia($LS($LZ), $Heap, source#0, p#0, sink#0, S#0), 
              TBool);
            assert {:id "id367"} b$reqreads#0;
            assert {:id "id368"} b$reqreads#1;
            return;
        }
        else
        {
            assume false;
        }

        assume false;
    }
}



procedure {:verboseName "SchorrWaite (well-formedness)"} {:vcs_split_on_every_assert} CheckWellFormed$$_module.__default.SchorrWaite(root#0: ref
       where $Is(root#0, Tclass._module.Node())
         && $IsAlloc(root#0, Tclass._module.Node(), $Heap), 
    S#0: Set
       where $Is(S#0, TSet(Tclass._module.Node()))
         && $IsAlloc(S#0, TSet(Tclass._module.Node()), $Heap));
  modifies $Heap;



implementation {:smt_option "smt.refine_inj_axioms", "false"} {:smt_option "smt.arith.solver", "2"} {:verboseName "SchorrWaite (well-formedness)"} {:vcs_split_on_every_assert} CheckWellFormed$$_module.__default.SchorrWaite(root#0: ref, S#0: Set)
{
  var $_ModifiesFrame: [ref,Field]bool;
  var n#0: ref;
  var ch#0: ref;
  var newtype$check#0: ref;
  var n#2: ref;
  var n#4: ref;
  var ch#3: ref;
  var newtype$check#1: ref;
  var n#6: ref;
  var ##source#0: ref;
  var ##sink#0: ref;
  var ##S#0: Set;
  var n#8: ref;


    // AddMethodImpl: SchorrWaite, CheckWellFormed$$_module.__default.SchorrWaite
    $_ModifiesFrame := (lambda $o: ref, $f: Field :: 
      $o != null && $Unbox(read($Heap, $o, alloc)): bool
         ==> Set#IsMember(S#0, $Box($o)));
    assume {:captureState "module:_module:state:32"} true;
    assume {:id "id372"} Set#IsMember(S#0, $Box(root#0));
    havoc n#0;
    assume $Is(n#0, Tclass._module.Node()) && $IsAlloc(n#0, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id373"} Set#IsMember(S#0, $Box(n#0));
        havoc ch#0;
        assume $Is(ch#0, Tclass._module.Node?())
           && $IsAlloc(ch#0, Tclass._module.Node?(), $Heap);
        if (*)
        {
            assert {:id "id374"} n#0 != null;
            assume true;
            assume {:id "id375"} Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#0));
            if (*)
            {
                newtype$check#0 := null;
                assume {:id "id376"} ch#0 == null;
            }
            else
            {
                assume true;
                assume {:id "id377"} ch#0 != null;
                assume {:id "id378"} Set#IsMember(S#0, $Box(ch#0));
            }
        }
        else
        {
            assume true;
            assume {:id "id379"} Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#0))
               ==> ch#0 == null || Set#IsMember(S#0, $Box(ch#0));
        }

        assume {:id "id380"} (forall ch#1: ref :: 
          { Set#IsMember(S#0, $Box(ch#1)) } 
            { Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#1)) } 
          $Is(ch#1, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#1))
             ==> ch#1 == null || Set#IsMember(S#0, $Box(ch#1)));
    }
    else
    {
        assume true;
        assume {:id "id381"} Set#IsMember(S#0, $Box(n#0))
           ==> (forall ch#1: ref :: 
            { Set#IsMember(S#0, $Box(ch#1)) } 
              { Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#1)) } 
            $Is(ch#1, Tclass._module.Node?())
               ==> 
              Seq#Contains($Unbox(read($Heap, n#0, _module.Node.children)): Seq, $Box(ch#1))
               ==> ch#1 == null || Set#IsMember(S#0, $Box(ch#1)));
    }

    assume {:id "id382"} (forall n#1: ref :: 
      { read($Heap, n#1, _module.Node.children) } { Set#IsMember(S#0, $Box(n#1)) } 
      $Is(n#1, Tclass._module.Node())
         ==> 
        Set#IsMember(S#0, $Box(n#1))
         ==> (forall ch#2: ref :: 
          { Set#IsMember(S#0, $Box(ch#2)) } 
            { Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2)) } 
          $Is(ch#2, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2))
             ==> ch#2 == null || Set#IsMember(S#0, $Box(ch#2))));
    havoc n#2;
    assume $Is(n#2, Tclass._module.Node()) && $IsAlloc(n#2, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id383"} Set#IsMember(S#0, $Box(n#2));
        assert {:id "id384"} n#2 != null;
        assume true;
        assume {:id "id385"} !$Unbox(read($Heap, n#2, _module.Node.marked)): bool;
        assert {:id "id386"} n#2 != null;
        assume true;
        assume {:id "id387"} $Unbox(read($Heap, n#2, _module.Node.childrenVisited)): int == LitInt(0);
    }
    else
    {
        assume true;
        assume {:id "id388"} Set#IsMember(S#0, $Box(n#2))
           ==> !$Unbox(read($Heap, n#2, _module.Node.marked)): bool
             && $Unbox(read($Heap, n#2, _module.Node.childrenVisited)): int == LitInt(0);
    }

    assume {:id "id389"} (forall n#3: ref :: 
      { read($Heap, n#3, _module.Node.childrenVisited) } 
        { read($Heap, n#3, _module.Node.marked) } 
        { Set#IsMember(S#0, $Box(n#3)) } 
      $Is(n#3, Tclass._module.Node())
         ==> (Set#IsMember(S#0, $Box(n#3))
             ==> !$Unbox(read($Heap, n#3, _module.Node.marked)): bool)
           && (Set#IsMember(S#0, $Box(n#3))
             ==> $Unbox(read($Heap, n#3, _module.Node.childrenVisited)): int == LitInt(0)));
    havoc $Heap;
    assume (forall $o: ref :: 
      { $Heap[$o] } 
      $o != null && $Unbox(read(old($Heap), $o, alloc)): bool
         ==> $Heap[$o] == old($Heap)[$o] || Set#IsMember(S#0, $Box($o)));
    assume $HeapSucc(old($Heap), $Heap);
    assume {:captureState "module:_module:state:33"} true;
    assert {:id "id390"} root#0 != null;
    assume true;
    assume {:id "id391"} $Unbox(read($Heap, root#0, _module.Node.marked)): bool;
    havoc n#4;
    assume $Is(n#4, Tclass._module.Node()) && $IsAlloc(n#4, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id392"} Set#IsMember(S#0, $Box(n#4));
        assert {:id "id393"} n#4 != null;
        assume true;
        assume {:id "id394"} $Unbox(read($Heap, n#4, _module.Node.marked)): bool;
        havoc ch#3;
        assume $Is(ch#3, Tclass._module.Node?())
           && $IsAlloc(ch#3, Tclass._module.Node?(), $Heap);
        if (*)
        {
            assert {:id "id395"} n#4 != null;
            assume true;
            assume {:id "id396"} Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#3));
            newtype$check#1 := null;
            assume {:id "id397"} ch#3 != null;
            assert {:id "id398"} ch#3 != null;
            assume true;
            assume {:id "id399"} $Unbox(read($Heap, ch#3, _module.Node.marked)): bool;
        }
        else
        {
            assume true;
            assume {:id "id400"} Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#3))
                 && ch#3 != null
               ==> $Unbox(read($Heap, ch#3, _module.Node.marked)): bool;
        }

        assume {:id "id401"} (forall ch#4: ref :: 
          { read($Heap, ch#4, _module.Node.marked) } 
            { Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#4)) } 
          $Is(ch#4, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#4))
               && ch#4 != null
             ==> $Unbox(read($Heap, ch#4, _module.Node.marked)): bool);
    }
    else
    {
        assume true;
        assume {:id "id402"} Set#IsMember(S#0, $Box(n#4))
             && $Unbox(read($Heap, n#4, _module.Node.marked)): bool
           ==> (forall ch#4: ref :: 
            { read($Heap, ch#4, _module.Node.marked) } 
              { Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#4)) } 
            $Is(ch#4, Tclass._module.Node?())
               ==> 
              Seq#Contains($Unbox(read($Heap, n#4, _module.Node.children)): Seq, $Box(ch#4))
                 && ch#4 != null
               ==> $Unbox(read($Heap, ch#4, _module.Node.marked)): bool);
    }

    assume {:id "id403"} (forall n#5: ref :: 
      { read($Heap, n#5, _module.Node.children) } 
        { read($Heap, n#5, _module.Node.marked) } 
        { Set#IsMember(S#0, $Box(n#5)) } 
      $Is(n#5, Tclass._module.Node())
         ==> 
        Set#IsMember(S#0, $Box(n#5))
           && $Unbox(read($Heap, n#5, _module.Node.marked)): bool
         ==> (forall ch#5: ref :: 
          { read($Heap, ch#5, _module.Node.marked) } 
            { Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5)) } 
          $Is(ch#5, Tclass._module.Node?())
             ==> 
            Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5))
               && ch#5 != null
             ==> $Unbox(read($Heap, ch#5, _module.Node.marked)): bool));
    havoc n#6;
    assume $Is(n#6, Tclass._module.Node()) && $IsAlloc(n#6, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id404"} Set#IsMember(S#0, $Box(n#6));
        assert {:id "id405"} n#6 != null;
        assume true;
        assume {:id "id406"} $Unbox(read($Heap, n#6, _module.Node.marked)): bool;
        ##source#0 := root#0;
        ##sink#0 := n#6;
        ##S#0 := S#0;
        assert {:id "id407"} $IsAlloc(root#0, Tclass._module.Node(), old($Heap));
        assert {:id "id408"} $IsAlloc(n#6, Tclass._module.Node(), old($Heap));
        assert {:id "id409"} $IsAlloc(S#0, TSet(Tclass._module.Node()), old($Heap));
        assume _module.__default.Reachable#canCall(old($Heap), root#0, n#6, S#0);
        assume {:id "id410"} _module.__default.Reachable(old($Heap), root#0, n#6, S#0);
    }
    else
    {
        assume Set#IsMember(S#0, $Box(n#6))
           ==> 
          $Unbox(read($Heap, n#6, _module.Node.marked)): bool
           ==> _module.__default.Reachable#canCall(old($Heap), root#0, n#6, S#0);
        assume {:id "id411"} Set#IsMember(S#0, $Box(n#6))
             && $Unbox(read($Heap, n#6, _module.Node.marked)): bool
           ==> _module.__default.Reachable(old($Heap), root#0, n#6, S#0);
    }

    assume {:id "id412"} (forall n#7: ref :: 
      { _module.__default.Reachable(old($Heap), root#0, n#7, S#0) } 
        { read($Heap, n#7, _module.Node.marked) } 
        { Set#IsMember(S#0, $Box(n#7)) } 
      $Is(n#7, Tclass._module.Node())
         ==> 
        Set#IsMember(S#0, $Box(n#7))
           && $Unbox(read($Heap, n#7, _module.Node.marked)): bool
         ==> _module.__default.Reachable(old($Heap), root#0, n#7, S#0));
    havoc n#8;
    assume $Is(n#8, Tclass._module.Node()) && $IsAlloc(n#8, Tclass._module.Node(), $Heap);
    if (*)
    {
        assume {:id "id413"} Set#IsMember(S#0, $Box(n#8));
        assert {:id "id414"} n#8 != null;
        assume true;
        assert {:id "id415"} n#8 != null;
        assert {:id "id416"} $IsAlloc(n#8, Tclass._module.Node(), old($Heap));
        assume true;
        assume {:id "id417"} $Unbox(read($Heap, n#8, _module.Node.childrenVisited)): int
           == $Unbox(read(old($Heap), n#8, _module.Node.childrenVisited)): int;
        assert {:id "id418"} n#8 != null;
        assume true;
        assert {:id "id419"} n#8 != null;
        assert {:id "id420"} $IsAlloc(n#8, Tclass._module.Node(), old($Heap));
        assume true;
        assume {:id "id421"} Seq#Equal($Unbox(read($Heap, n#8, _module.Node.children)): Seq, 
          $Unbox(read(old($Heap), n#8, _module.Node.children)): Seq);
    }
    else
    {
        assume true;
        assume {:id "id422"} Set#IsMember(S#0, $Box(n#8))
           ==> $Unbox(read($Heap, n#8, _module.Node.childrenVisited)): int
               == $Unbox(read(old($Heap), n#8, _module.Node.childrenVisited)): int
             && Seq#Equal($Unbox(read($Heap, n#8, _module.Node.children)): Seq, 
              $Unbox(read(old($Heap), n#8, _module.Node.children)): Seq);
    }

    assume {:id "id423"} (forall n#9: ref :: 
      { read(old($Heap), n#9, _module.Node.children) } 
        { read($Heap, n#9, _module.Node.children) } 
        { read(old($Heap), n#9, _module.Node.childrenVisited) } 
        { read($Heap, n#9, _module.Node.childrenVisited) } 
        { Set#IsMember(S#0, $Box(n#9)) } 
      $Is(n#9, Tclass._module.Node())
         ==> (Set#IsMember(S#0, $Box(n#9))
             ==> $Unbox(read($Heap, n#9, _module.Node.childrenVisited)): int
               == $Unbox(read(old($Heap), n#9, _module.Node.childrenVisited)): int)
           && (Set#IsMember(S#0, $Box(n#9))
             ==> Seq#Equal($Unbox(read($Heap, n#9, _module.Node.children)): Seq, 
              $Unbox(read(old($Heap), n#9, _module.Node.children)): Seq)));
}



procedure {:verboseName "SchorrWaite (call)"} {:vcs_split_on_every_assert} Call$$_module.__default.SchorrWaite(root#0: ref
       where $Is(root#0, Tclass._module.Node())
         && $IsAlloc(root#0, Tclass._module.Node(), $Heap), 
    S#0: Set
       where $Is(S#0, TSet(Tclass._module.Node()))
         && $IsAlloc(S#0, TSet(Tclass._module.Node()), $Heap));
  // user-defined preconditions
  free requires {:always_assume} true;
  requires {:id "id424"} Set#IsMember(S#0, $Box(root#0));
  free requires {:always_assume} true;
  requires {:id "id425"} (forall n#1: ref :: 
    { read($Heap, n#1, _module.Node.children) } { Set#IsMember(S#0, $Box(n#1)) } 
    $Is(n#1, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#1))
       ==> (forall ch#2: ref :: 
        { Set#IsMember(S#0, $Box(ch#2)) } 
          { Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2)) } 
        $Is(ch#2, Tclass._module.Node?())
           ==> 
          Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2))
           ==> ch#2 == null || Set#IsMember(S#0, $Box(ch#2))));
  free requires {:always_assume} true;
  requires {:id "id426"} (forall n#3: ref :: 
    { read($Heap, n#3, _module.Node.childrenVisited) } 
      { read($Heap, n#3, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#3)) } 
    $Is(n#3, Tclass._module.Node())
       ==> (Set#IsMember(S#0, $Box(n#3))
           ==> !$Unbox(read($Heap, n#3, _module.Node.marked)): bool)
         && (Set#IsMember(S#0, $Box(n#3))
           ==> $Unbox(read($Heap, n#3, _module.Node.childrenVisited)): int == LitInt(0)));
  // user-defined frame expressions
  free requires {:always_assume} true;
  modifies $Heap;
  // user-defined postconditions
  free ensures {:always_assume} true;
  ensures {:id "id427"} $Unbox(read($Heap, root#0, _module.Node.marked)): bool;
  free ensures {:always_assume} true;
  ensures {:id "id428"} (forall n#5: ref :: 
    { read($Heap, n#5, _module.Node.children) } 
      { read($Heap, n#5, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#5)) } 
    $Is(n#5, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#5))
         && $Unbox(read($Heap, n#5, _module.Node.marked)): bool
       ==> (forall ch#5: ref :: 
        { read($Heap, ch#5, _module.Node.marked) } 
          { Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5)) } 
        $Is(ch#5, Tclass._module.Node?())
           ==> 
          Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5))
             && ch#5 != null
           ==> $Unbox(read($Heap, ch#5, _module.Node.marked)): bool));
  free ensures {:always_assume} (forall n#7: ref :: 
    { _module.__default.Reachable(old($Heap), root#0, n#7, S#0) } 
      { read($Heap, n#7, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#7)) } 
    $Is(n#7, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#7))
       ==> 
      $Unbox(read($Heap, n#7, _module.Node.marked)): bool
       ==> _module.__default.Reachable#canCall(old($Heap), root#0, n#7, S#0));
  ensures {:id "id429"} (forall n#7: ref :: 
    { _module.__default.Reachable(old($Heap), root#0, n#7, S#0) } 
      { read($Heap, n#7, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#7)) } 
    $Is(n#7, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#7))
         && $Unbox(read($Heap, n#7, _module.Node.marked)): bool
       ==> _module.__default.Reachable(old($Heap), root#0, n#7, S#0));
  free ensures {:always_assume} true;
  ensures {:id "id430"} (forall n#9: ref :: 
    { read(old($Heap), n#9, _module.Node.children) } 
      { read($Heap, n#9, _module.Node.children) } 
      { read(old($Heap), n#9, _module.Node.childrenVisited) } 
      { read($Heap, n#9, _module.Node.childrenVisited) } 
      { Set#IsMember(S#0, $Box(n#9)) } 
    $Is(n#9, Tclass._module.Node())
       ==> (Set#IsMember(S#0, $Box(n#9))
           ==> $Unbox(read($Heap, n#9, _module.Node.childrenVisited)): int
             == $Unbox(read(old($Heap), n#9, _module.Node.childrenVisited)): int)
         && (Set#IsMember(S#0, $Box(n#9))
           ==> Seq#Equal($Unbox(read($Heap, n#9, _module.Node.children)): Seq, 
            $Unbox(read(old($Heap), n#9, _module.Node.children)): Seq)));
  // frame condition: object granularity
  free ensures (forall $o: ref :: 
    { $Heap[$o] } 
    $o != null && $Unbox(read(old($Heap), $o, alloc)): bool
       ==> $Heap[$o] == old($Heap)[$o] || Set#IsMember(S#0, $Box($o)));
  // boilerplate
  free ensures $HeapSucc(old($Heap), $Heap);



procedure {:verboseName "SchorrWaite (correctness)"} {:vcs_split_on_every_assert} Impl$$_module.__default.SchorrWaite(root#0: ref
       where $Is(root#0, Tclass._module.Node())
         && $IsAlloc(root#0, Tclass._module.Node(), $Heap), 
    S#0: Set
       where $Is(S#0, TSet(Tclass._module.Node()))
         && $IsAlloc(S#0, TSet(Tclass._module.Node()), $Heap))
   returns ($_reverifyPost: bool);
  // user-defined preconditions
  free requires {:always_assume} true;
  requires {:id "id431"} Set#IsMember(S#0, $Box(root#0));
  free requires {:always_assume} true;
  requires {:id "id432"} (forall n#1: ref :: 
    { read($Heap, n#1, _module.Node.children) } { Set#IsMember(S#0, $Box(n#1)) } 
    $Is(n#1, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#1))
       ==> (forall ch#2: ref :: 
        { Set#IsMember(S#0, $Box(ch#2)) } 
          { Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2)) } 
        $Is(ch#2, Tclass._module.Node?())
           ==> 
          Seq#Contains($Unbox(read($Heap, n#1, _module.Node.children)): Seq, $Box(ch#2))
           ==> ch#2 == null || Set#IsMember(S#0, $Box(ch#2))));
  free requires {:always_assume} true;
  requires {:id "id433"} (forall n#3: ref :: 
    { read($Heap, n#3, _module.Node.childrenVisited) } 
      { read($Heap, n#3, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#3)) } 
    $Is(n#3, Tclass._module.Node())
       ==> (Set#IsMember(S#0, $Box(n#3))
           ==> !$Unbox(read($Heap, n#3, _module.Node.marked)): bool)
         && (Set#IsMember(S#0, $Box(n#3))
           ==> $Unbox(read($Heap, n#3, _module.Node.childrenVisited)): int == LitInt(0)));
  // user-defined frame expressions
  free requires {:always_assume} true;
  modifies $Heap;
  // user-defined postconditions
  free ensures {:always_assume} true;
  ensures {:id "id434"} $Unbox(read($Heap, root#0, _module.Node.marked)): bool;
  free ensures {:always_assume} true;
  ensures {:id "id435"} (forall n#5: ref :: 
    { read($Heap, n#5, _module.Node.children) } 
      { read($Heap, n#5, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#5)) } 
    $Is(n#5, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#5))
         && $Unbox(read($Heap, n#5, _module.Node.marked)): bool
       ==> (forall ch#5: ref :: 
        { read($Heap, ch#5, _module.Node.marked) } 
          { Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5)) } 
        $Is(ch#5, Tclass._module.Node?())
           ==> 
          Seq#Contains($Unbox(read($Heap, n#5, _module.Node.children)): Seq, $Box(ch#5))
             && ch#5 != null
           ==> $Unbox(read($Heap, ch#5, _module.Node.marked)): bool));
  free ensures {:always_assume} (forall n#7: ref :: 
    { _module.__default.Reachable(old($Heap), root#0, n#7, S#0) } 
      { read($Heap, n#7, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#7)) } 
    $Is(n#7, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#7))
       ==> 
      $Unbox(read($Heap, n#7, _module.Node.marked)): bool
       ==> _module.__default.Reachable#canCall(old($Heap), root#0, n#7, S#0));
  ensures {:id "id436"} (forall n#7: ref :: 
    { _module.__default.Reachable(old($Heap), root#0, n#7, S#0) } 
      { read($Heap, n#7, _module.Node.marked) } 
      { Set#IsMember(S#0, $Box(n#7)) } 
    $Is(n#7, Tclass._module.Node())
       ==> 
      Set#IsMember(S#0, $Box(n#7))
         && $Unbox(read($Heap, n#7, _module.Node.marked)): bool
       ==> _module.__default.Reachable(old($Heap), root#0, n#7, S#0));
  free ensures {:always_assume} true;
  ensures {:id "id437"} (forall n#9: ref :: 
    { read(old($Heap), n#9, _module.Node.children) } 
      { read($Heap, n#9, _module.Node.children) } 
      { read(old($Heap), n#9, _module.Node.childrenVisited) } 
      { read($Heap, n#9, _module.Node.childrenVisited) } 
      { Set#IsMember(S#0, $Box(n#9)) } 
    $Is(n#9, Tclass._module.Node())
       ==> (Set#IsMember(S#0, $Box(n#9))
           ==> $Unbox(read($Heap, n#9, _module.Node.childrenVisited)): int
             == $Unbox(read(old($Heap), n#9, _module.Node.childrenVisited)): int)
         && (Set#IsMember(S#0, $Box(n#9))
           ==> Seq#Equal($Unbox(read($Heap, n#9, _module.Node.children)): Seq, 
            $Unbox(read(old($Heap), n#9, _module.Node.children)): Seq)));
  // frame condition: object granularity
  free ensures (forall $o: ref :: 
    { $Heap[$o] } 
    $o != null && $Unbox(read(old($Heap), $o, alloc)): bool
       ==> $Heap[$o] == old($Heap)[$o] || Set#IsMember(S#0, $Box($o)));
  // boilerplate
  free ensures $HeapSucc(old($Heap), $Heap);



implementation {:smt_option "smt.refine_inj_axioms", "false"} {:smt_option "smt.arith.solver", "2"} {:verboseName "SchorrWaite (correctness)"} {:vcs_split_on_every_assert} Impl$$_module.__default.SchorrWaite(root#0: ref, S#0: Set) returns ($_reverifyPost: bool)
{
  var $_ModifiesFrame: [ref,Field]bool;
  var t#0: ref
     where $Is(t#0, Tclass._module.Node?()) && $IsAlloc(t#0, Tclass._module.Node?(), $Heap);
  var p#0: ref
     where $Is(p#0, Tclass._module.Node?()) && $IsAlloc(p#0, Tclass._module.Node?(), $Heap);
  var newtype$check#2: ref;
  var path#0: DatatypeType
     where $Is(path#0, Tclass._module.Path())
       && $IsAlloc(path#0, Tclass._module.Path(), $Heap);
  var $rhs#0: bool;
  var $rhs#1: DatatypeType;
  var stackNodes#0: Seq
     where $Is(stackNodes#0, TSeq(Tclass._module.Node()))
       && $IsAlloc(stackNodes#0, TSeq(Tclass._module.Node()), $Heap);
  var unmarkedNodes#0: Set
     where $Is(unmarkedNodes#0, TSet(Tclass._module.Node?()))
       && $IsAlloc(unmarkedNodes#0, TSet(Tclass._module.Node?()), $Heap);
  var $PreLoopHeap$loop#0: Heap;
  var $decr_init$loop#00: Set;
  var $decr_init$loop#01: Seq;
  var $decr_init$loop#02: int;
  var $w$loop#0: bool;
  var i#0: int;
  var newtype$check#3: ref;
  var n#10: ref;
  var n#12: ref;
  var n#14: ref;
  var j#0: int;
  var n#16: ref;
  var newtype$check#4: ref;
  var k#0: int;
  var k#2: int;
  var n#18: ref;
  var ch#6: ref;
  var newtype$check#5: ref;
  var n#20: ref;
  var j#3: int;
  var newtype$check#6: ref;
  var ##source#1: ref;
  var ##p#0: DatatypeType;
  var ##sink#1: ref;
  var ##S#1: Set;
  var n#24: ref;
  var pth#Z#0: DatatypeType;
  var let#0#0#0: DatatypeType;
  var ##source#2: ref;
  var ##p#1: DatatypeType;
  var ##sink#2: ref;
  var ##S#2: Set;
  var n#26: ref;
  var ##source#3: ref;
  var ##sink#3: ref;
  var ##S#3: Set;
  var n#28: ref;
  var $decr$loop#00: Set;
  var $decr$loop#01: Seq;
  var $decr$loop#02: int;
  var $rhs#0_0_0: int;
  var newtype$check#0_0_0: ref;
  var oldP#0_0_0: ref
     where $Is(oldP#0_0_0, Tclass._module.Node?())
       && $IsAlloc(oldP#0_0_0, Tclass._module.Node?(), $Heap);
  var $rhs#0_0_1: Seq;
  var $rhs#0_0_2: int;
  var newtype$check#0_1_0: ref;
  var $rhs#0_1_0_0: int;
  var $Heap_at_0_1_1_0: Heap;
  var newT#0_1_1_0: ref
     where $Is(newT#0_1_1_0, Tclass._module.Node?())
       && $IsAlloc(newT#0_1_1_0, Tclass._module.Node?(), $Heap);
  var $rhs#0_1_1_0: Seq;
  var $rhs#0_1_1_1: bool;
  var $rhs#0_1_1_2: DatatypeType;
  var n#0_1_1_0_0: ref;
  var pth#Z#0_1_1_0_0: DatatypeType;
  var let#0_1_1_0_0#0#0: DatatypeType;
  var pth#Z#0_1_1_0_1: DatatypeType;
  var let#0_1_1_0_1#0#0: DatatypeType;
  var ##source#0_1_1_0_0: ref;
  var ##p#0_1_1_0_0: DatatypeType;
  var ##sink#0_1_1_0_0: ref;
  var ##S#0_1_1_0_0: Set;
  var ##source#0_1_1_0_0_0: ref;
  var ##p#0_1_1_0_0_0: DatatypeType;
  var ##sink#0_1_1_0_0_0: ref;
  var ##S#0_1_1_0_0_0: Set;

    // AddMethodImpl: SchorrWaite, Impl$$_module.__default.SchorrWaite
    $_ModifiesFrame := (lambda $o: ref, $f: Field :: 
      $o != null && $Unbox(read($Heap, $o, alloc)): bool
         ==> Set#IsMember(S#0, $Box($o)));
    assume {:captureState "module:_module:state:34"} true;
    $_reverifyPost := false;
    // ----- assignment statement ----- module:_module:statement:31
    assume true;
    assume true;
    t#0 := {:id "id438"} root#0;
    assume {:captureState "module:_module:state:35"} true;
    // ----- assignment statement ----- module:_module:statement:32
    assume true;
    newtype$check#2 := null;
    assume true;
    p#0 := {:id "id439"} null;
    assume {:captureState "module:_module:state:36"} true;
    // ----- assignment statement ----- module:_module:statement:33
    assume true;
    assume true;
    path#0 := {:id "id440"} Lit(#_module.Path.Empty());
    assume {:captureState "module:_module:state:37"} true;
    // ----- assignment statement ----- module:_module:statement:34
    assert {:id "id441"} t#0 != null;
    assume true;
    assume true;
    assert {:id "id442"} $_ModifiesFrame[t#0, _module.Node.marked];
    assume true;
    $rhs#0 := {:id "id443"} Lit(true);
    $Heap := {:id "id444"} update($Heap, t#0, _module.Node.marked, $Box($rhs#0));
    assume $IsGoodHeap($Heap);
    assume {:captureState "module:_module:state:38"} true;
    // ----- assignment statement ----- module:_module:statement:35
    assert {:id "id445"} t#0 != null;
    assume true;
    assume true;
    assert {:id "id446"} $_ModifiesFrame[t#0, _module.Node.pathFromRoot];
    assume true;
    $rhs#1 := {:id "id447"} path#0;
    $Heap := {:id "id448"} update($Heap, t#0, _module.Node.pathFromRoot, $Box($rhs#1));
    assume $IsGoodHeap($Heap);
    assume {:captureState "module:_module:state:39"} true;
    // ----- assignment statement ----- module:_module:statement:36
    assume true;
    assume true;
    stackNodes#0 := {:id "id449"} Lit(Seq#Empty(): Seq);
    assume {:captureState "module:_module:state:40"} true;
    // ----- assignment statement ----- module:_module:statement:37
    assume true;
    assume true;
    unmarkedNodes#0 := {:id "id450"} Set#Difference(S#0, Set#UnionOne(Set#Empty(): Set, $Box(t#0)));
    assume {:captureState "module:_module:state:41"} true;
    // ----- while statement ----- module:_module:statement:38
    // Assume Fuel Constant
    $PreLoopHeap$loop#0 := $Heap;
    $decr_init$loop#00 := unmarkedNodes#0;
    $decr_init$loop#01 := stackNodes#0;
    $decr_init$loop#02 := Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq)
       - $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int;
    havoc $w$loop#0;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume true;
    assume $w$loop#0
       ==> 
      $IsAllocBox($Box(path#0), Tclass._module.Path(), old($Heap))
       ==> _module.__default.ReachableVia#canCall(old($Heap), root#0, path#0, t#0, S#0);
    assume $w$loop#0
       ==> (forall n#25: ref :: 
        { _module.__default.ReachableVia($LS($LZ), 
            old($Heap), 
            root#0, 
            $Unbox(read(old($Heap), n#25, _module.Node.pathFromRoot)): DatatypeType, 
            n#25, 
            S#0) } 
          { read($Heap, n#25, _module.Node.pathFromRoot) } 
          { read($Heap, n#25, _module.Node.marked) } 
          { Set#IsMember(S#0, $Box(n#25)) } 
        $Is(n#25, Tclass._module.Node())
           ==> 
          Set#IsMember(S#0, $Box(n#25))
           ==> 
          $Unbox(read($Heap, n#25, _module.Node.marked)): bool
           ==> (var pth#0 := $Unbox(read($Heap, n#25, _module.Node.pathFromRoot)): DatatypeType; 
            $IsAllocBox($Box(pth#0), Tclass._module.Path(), old($Heap))
               ==> _module.__default.ReachableVia#canCall(old($Heap), root#0, pth#0, n#25, S#0)));
    assume $w$loop#0
       ==> (forall n#27: ref :: 
        { _module.__default.Reachable(old($Heap), root#0, n#27, S#0) } 
          { read($Heap, n#27, _module.Node.marked) } 
          { Set#IsMember(S#0, $Box(n#27)) } 
        $Is(n#27, Tclass._module.Node())
           ==> 
          Set#IsMember(S#0, $Box(n#27))
           ==> 
          $Unbox(read($Heap, n#27, _module.Node.marked)): bool
           ==> _module.__default.Reachable#canCall(old($Heap), root#0, n#27, S#0));
    assume true;
    assume $w$loop#0 ==> true;
    assume $w$loop#0 ==> true;
    assume $w$loop#0 ==> true;
    while (true)
      free invariant true;
      invariant {:id "id453"} $w$loop#0
         ==> (forall i#1: int :: 
          { Seq#Index(stackNodes#0, i#1) } 
          LitInt(0) <= i#1 && i#1 < Seq#Length(stackNodes#0)
             ==> Set#IsMember(S#0, Seq#Index(stackNodes#0, i#1)));
      free invariant true;
      invariant {:id "id455"} $w$loop#0 ==> Set#IsMember(S#0, $Box(t#0));
      invariant {:id "id456"} $w$loop#0 ==> !Seq#Contains(stackNodes#0, $Box(t#0));
      free invariant true;
      invariant {:id "id459"} $w$loop#0
         ==> p#0
           == (if Seq#Length(stackNodes#0) == LitInt(0)
             then null
             else $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref);
      free invariant true;
      invariant {:id "id463"} $w$loop#0
         ==> (forall n#11: ref :: 
          { read($Heap, n#11, _module.Node.children) } 
            { read($Heap, n#11, _module.Node.childrenVisited) } 
            { Seq#Contains(stackNodes#0, $Box(n#11)) } 
          $Is(n#11, Tclass._module.Node())
             ==> 
            Seq#Contains(stackNodes#0, $Box(n#11))
             ==> $Unbox(read($Heap, n#11, _module.Node.childrenVisited)): int
               < Seq#Length($Unbox(read($Heap, n#11, _module.Node.children)): Seq));
      free invariant true;
      invariant {:id "id467"} $w$loop#0
         ==> $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
           <= Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq);
      free invariant true;
      invariant {:id "id470"} $w$loop#0
         ==> (forall n#13: ref :: 
          { read($Heap, n#13, _module.Node.childrenVisited) } 
            { Seq#Contains(stackNodes#0, $Box(n#13)) } 
            { Set#IsMember(S#0, $Box(n#13)) } 
          $Is(n#13, Tclass._module.Node())
             ==> 
            Set#IsMember(S#0, $Box(n#13))
               && !Seq#Contains(stackNodes#0, $Box(n#13))
               && n#13 != t#0
             ==> $Unbox(read($Heap, n#13, _module.Node.childrenVisited)): int == LitInt(0));
      free invariant true;
      invariant {:id "id482"} $w$loop#0
         ==> (forall n#15: ref :: 
          { read($Heap, n#15, _module.Node.childrenVisited) } 
            { read(old($Heap), n#15, _module.Node.children) } 
            { read($Heap, n#15, _module.Node.children) } 
            { Seq#Contains(stackNodes#0, $Box(n#15)) } 
          $Is(n#15, Tclass._module.Node())
             ==> (Seq#Contains(stackNodes#0, $Box(n#15))
                 ==> Seq#Length($Unbox(read($Heap, n#15, _module.Node.children)): Seq)
                   == Seq#Length($Unbox(read(old($Heap), n#15, _module.Node.children)): Seq))
               && (Seq#Contains(stackNodes#0, $Box(n#15))
                 ==> (forall j#2: int :: 
                  { Seq#Index($Unbox(read(old($Heap), n#15, _module.Node.children)): Seq, j#2) } 
                    { Seq#Index($Unbox(read($Heap, n#15, _module.Node.children)): Seq, j#2) } 
                  LitInt(0) <= j#2
                       && j#2 < Seq#Length($Unbox(read($Heap, n#15, _module.Node.children)): Seq)
                     ==> j#2 == $Unbox(read($Heap, n#15, _module.Node.childrenVisited)): int
                       || $Unbox(Seq#Index($Unbox(read($Heap, n#15, _module.Node.children)): Seq, j#2)): ref
                         == $Unbox(Seq#Index($Unbox(read(old($Heap), n#15, _module.Node.children)): Seq, j#2)): ref)));
      free invariant true;
      invariant {:id "id487"} $w$loop#0
         ==> (forall n#17: ref :: 
          { read(old($Heap), n#17, _module.Node.children) } 
            { read($Heap, n#17, _module.Node.children) } 
            { Seq#Contains(stackNodes#0, $Box(n#17)) } 
            { Set#IsMember(S#0, $Box(n#17)) } 
          $Is(n#17, Tclass._module.Node())
             ==> 
            Set#IsMember(S#0, $Box(n#17)) && !Seq#Contains(stackNodes#0, $Box(n#17))
             ==> Seq#Equal($Unbox(read($Heap, n#17, _module.Node.children)): Seq, 
              $Unbox(read(old($Heap), n#17, _module.Node.children)): Seq));
      free invariant true;
      invariant {:id "id501"} $w$loop#0
         ==> 
        0 < Seq#Length(stackNodes#0)
         ==> $Unbox(Seq#Index($Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, LitInt(0))): ref, _module.Node.children)): Seq, 
              $Unbox(read($Heap, 
                  $Unbox(Seq#Index(stackNodes#0, LitInt(0))): ref, 
                  _module.Node.childrenVisited)): int)): ref
           == null;
      free invariant true;
      invariant {:id "id509"} $w$loop#0
         ==> (forall k#1: int :: 
          { read($Heap, $Unbox(Seq#Index(stackNodes#0, k#1)): ref, _module.Node.childrenVisited) } 
            { read($Heap, $Unbox(Seq#Index(stackNodes#0, k#1)): ref, _module.Node.children) } 
          0 < k#1 && k#1 < Seq#Length(stackNodes#0)
             ==> $Unbox(Seq#Index($Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, k#1)): ref, _module.Node.children)): Seq, 
                  $Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, k#1)): ref, _module.Node.childrenVisited)): int)): ref
               == $Unbox(Seq#Index(stackNodes#0, k#1 - 1)): ref);
      free invariant true;
      invariant {:id "id518"} $w$loop#0
         ==> (forall k#3: int :: 
          { read($Heap, $Unbox(Seq#Index(stackNodes#0, k#3)): ref, _module.Node.childrenVisited) } 
            { Seq#Index(stackNodes#0, k#3) } 
          LitInt(0) <= k#3 && k#3 < Seq#Length(stackNodes#0) - 1
             ==> $Unbox(Seq#Index($Unbox(read(old($Heap), $Unbox(Seq#Index(stackNodes#0, k#3)): ref, _module.Node.children)): Seq, 
                  $Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, k#3)): ref, _module.Node.childrenVisited)): int)): ref
               == $Unbox(Seq#Index(stackNodes#0, k#3 + 1)): ref);
      free invariant true;
      invariant {:id "id526"} $w$loop#0
         ==> 
        0 < Seq#Length(stackNodes#0)
         ==> $Unbox(Seq#Index($Unbox(read(old($Heap), 
                  $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                  _module.Node.children)): Seq, 
              $Unbox(read($Heap, 
                  $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                  _module.Node.childrenVisited)): int)): ref
           == t#0;
      free invariant true;
      invariant {:id "id529"} $w$loop#0 ==> $Unbox(read($Heap, root#0, _module.Node.marked)): bool;
      free invariant true;
      invariant {:id "id534"} $w$loop#0
         ==> (forall n#19: ref :: 
          { read($Heap, n#19, _module.Node.children) } 
            { Seq#Contains(stackNodes#0, $Box(n#19)) } 
            { read($Heap, n#19, _module.Node.marked) } 
            { Set#IsMember(S#0, $Box(n#19)) } 
          $Is(n#19, Tclass._module.Node())
             ==> 
            Set#IsMember(S#0, $Box(n#19))
               && $Unbox(read($Heap, n#19, _module.Node.marked)): bool
               && !Seq#Contains(stackNodes#0, $Box(n#19))
               && n#19 != t#0
             ==> (forall ch#8: ref :: 
              { read($Heap, ch#8, _module.Node.marked) } 
                { Seq#Contains($Unbox(read($Heap, n#19, _module.Node.children)): Seq, $Box(ch#8)) } 
              $Is(ch#8, Tclass._module.Node?())
                 ==> 
                Seq#Contains($Unbox(read($Heap, n#19, _module.Node.children)): Seq, $Box(ch#8))
                   && ch#8 != null
                 ==> $Unbox(read($Heap, ch#8, _module.Node.marked)): bool));
      free invariant true;
      invariant {:id "id543"} $w$loop#0
         ==> (forall n#21: ref :: 
          { read($Heap, n#21, _module.Node.marked) } 
            { Seq#Contains(stackNodes#0, $Box(n#21)) } 
          $Is(n#21, Tclass._module.Node()) && $IsAlloc(n#21, Tclass._module.Node(), $Heap)
             ==> 
            Seq#Contains(stackNodes#0, $Box(n#21)) || n#21 == t#0
             ==> $Unbox(read($Heap, n#21, _module.Node.marked)): bool);
      invariant {:id "id544"} $w$loop#0
         ==> (forall n#21: ref :: 
          { read($Heap, n#21, _module.Node.children) } 
            { read($Heap, n#21, _module.Node.childrenVisited) } 
            { Seq#Contains(stackNodes#0, $Box(n#21)) } 
          $Is(n#21, Tclass._module.Node()) && $IsAlloc(n#21, Tclass._module.Node(), $Heap)
             ==> 
            Seq#Contains(stackNodes#0, $Box(n#21)) || n#21 == t#0
             ==> (forall j#5: int :: 
              { Seq#Index($Unbox(read($Heap, n#21, _module.Node.children)): Seq, j#5) } 
              LitInt(0) <= j#5
                   && j#5 < $Unbox(read($Heap, n#21, _module.Node.childrenVisited)): int
                 ==> $Unbox(Seq#Index($Unbox(read($Heap, n#21, _module.Node.children)): Seq, j#5)): ref
                     == null
                   || $Unbox(read($Heap, 
                      $Unbox(Seq#Index($Unbox(read($Heap, n#21, _module.Node.children)): Seq, j#5)): ref, 
                      _module.Node.marked)): bool));
      free invariant $w$loop#0
         ==> 
        $IsAllocBox($Box(path#0), Tclass._module.Path(), old($Heap))
         ==> _module.__default.ReachableVia#canCall(old($Heap), root#0, path#0, t#0, S#0);
      invariant {:id "id551"} $w$loop#0 ==> $IsAllocBox($Box(path#0), Tclass._module.Path(), old($Heap));
      invariant {:id "id552"} $w$loop#0
         ==> 
        _module.__default.ReachableVia#canCall(old($Heap), root#0, path#0, t#0, S#0)
         ==> _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, path#0, t#0, S#0)
           || (_module.Path.Empty_q(path#0) ==> root#0 == t#0);
      invariant {:id "id553"} $w$loop#0
         ==> 
        _module.__default.ReachableVia#canCall(old($Heap), root#0, path#0, t#0, S#0)
         ==> _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, path#0, t#0, S#0)
           || (!_module.Path.Empty_q(path#0)
             ==> (var n#22 := _module.Path._h1(path#0); 
              (var prefix#0 := _module.Path._h0(path#0); Set#IsMember(S#0, $Box(n#22)))));
      invariant {:id "id554"} $w$loop#0
         ==> 
        _module.__default.ReachableVia#canCall(old($Heap), root#0, path#0, t#0, S#0)
         ==> _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, path#0, t#0, S#0)
           || (!_module.Path.Empty_q(path#0)
             ==> (var n#22 := _module.Path._h1(path#0); 
              (var prefix#0 := _module.Path._h0(path#0); 
                Seq#Contains($Unbox(read(old($Heap), n#22, _module.Node.children)): Seq, $Box(t#0)))));
      invariant {:id "id555"} $w$loop#0
         ==> 
        _module.__default.ReachableVia#canCall(old($Heap), root#0, path#0, t#0, S#0)
         ==> _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, path#0, t#0, S#0)
           || (!_module.Path.Empty_q(path#0)
             ==> (var n#22 := _module.Path._h1(path#0); 
              (var prefix#0 := _module.Path._h0(path#0); 
                _module.__default.ReachableVia($LS($LS($LZ)), old($Heap), root#0, prefix#0, n#22, S#0))));
      free invariant {:id "id556"} $w$loop#0
         ==> _module.__default.ReachableVia#canCall(old($Heap), root#0, path#0, t#0, S#0)
           && 
          (_module.__default.ReachableVia($LS($LZ), old($Heap), root#0, path#0, t#0, S#0)
           && (if _module.Path.Empty_q(path#0)
             then root#0 == t#0
             else (var n#23 := _module.Path._h1(path#0); 
              (var prefix#1 := _module.Path._h0(path#0); 
                Set#IsMember(S#0, $Box(n#23))
                   && Seq#Contains($Unbox(read(old($Heap), n#23, _module.Node.children)): Seq, $Box(t#0))
                   && _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, prefix#1, n#23, S#0)))));
      free invariant $w$loop#0
         ==> (forall n#25: ref :: 
          { _module.__default.ReachableVia($LS($LZ), 
              old($Heap), 
              root#0, 
              $Unbox(read(old($Heap), n#25, _module.Node.pathFromRoot)): DatatypeType, 
              n#25, 
              S#0) } 
            { read($Heap, n#25, _module.Node.pathFromRoot) } 
            { read($Heap, n#25, _module.Node.marked) } 
            { Set#IsMember(S#0, $Box(n#25)) } 
          $Is(n#25, Tclass._module.Node())
             ==> 
            Set#IsMember(S#0, $Box(n#25))
             ==> 
            $Unbox(read($Heap, n#25, _module.Node.marked)): bool
             ==> (var pth#0 := $Unbox(read($Heap, n#25, _module.Node.pathFromRoot)): DatatypeType; 
              $IsAllocBox($Box(pth#0), Tclass._module.Path(), old($Heap))
                 ==> _module.__default.ReachableVia#canCall(old($Heap), root#0, pth#0, n#25, S#0)));
      invariant {:id "id566"} $w$loop#0
         ==> (forall n#25: ref :: 
          { _module.__default.ReachableVia($LS($LZ), 
              old($Heap), 
              root#0, 
              $Unbox(read(old($Heap), n#25, _module.Node.pathFromRoot)): DatatypeType, 
              n#25, 
              S#0) } 
            { read($Heap, n#25, _module.Node.pathFromRoot) } 
            { read($Heap, n#25, _module.Node.marked) } 
            { Set#IsMember(S#0, $Box(n#25)) } 
          $Is(n#25, Tclass._module.Node())
             ==> 
            Set#IsMember(S#0, $Box(n#25))
               && $Unbox(read($Heap, n#25, _module.Node.marked)): bool
             ==> (var pth#0 := $Unbox(read($Heap, n#25, _module.Node.pathFromRoot)): DatatypeType; 
              $IsAllocBox($Box(pth#0), Tclass._module.Path(), old($Heap))
                 && _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, pth#0, n#25, S#0)));
      free invariant $w$loop#0
         ==> (forall n#27: ref :: 
          { _module.__default.Reachable(old($Heap), root#0, n#27, S#0) } 
            { read($Heap, n#27, _module.Node.marked) } 
            { Set#IsMember(S#0, $Box(n#27)) } 
          $Is(n#27, Tclass._module.Node())
             ==> 
            Set#IsMember(S#0, $Box(n#27))
             ==> 
            $Unbox(read($Heap, n#27, _module.Node.marked)): bool
             ==> _module.__default.Reachable#canCall(old($Heap), root#0, n#27, S#0));
      invariant {:id "id572"} $w$loop#0
         ==> (forall n#27: ref :: 
          { _module.__default.Reachable(old($Heap), root#0, n#27, S#0) } 
            { read($Heap, n#27, _module.Node.marked) } 
            { Set#IsMember(S#0, $Box(n#27)) } 
          $Is(n#27, Tclass._module.Node())
             ==> 
            Set#IsMember(S#0, $Box(n#27))
               && $Unbox(read($Heap, n#27, _module.Node.marked)): bool
             ==> _module.__default.Reachable(old($Heap), root#0, n#27, S#0));
      free invariant true;
      invariant {:id "id575"} $w$loop#0
         ==> (forall n#29: ref :: 
          { Set#IsMember(unmarkedNodes#0, $Box(n#29)) } 
            { read($Heap, n#29, _module.Node.marked) } 
            { Set#IsMember(S#0, $Box(n#29)) } 
          $Is(n#29, Tclass._module.Node())
             ==> 
            Set#IsMember(S#0, $Box(n#29))
               && !$Unbox(read($Heap, n#29, _module.Node.marked)): bool
             ==> Set#IsMember(unmarkedNodes#0, $Box(n#29)));
      free invariant (forall $o: ref :: 
        { $Heap[$o] } 
        $o != null && $Unbox(read(old($Heap), $o, alloc)): bool
           ==> $Heap[$o] == $PreLoopHeap$loop#0[$o] || Set#IsMember(S#0, $Box($o)));
      free invariant $HeapSucc($PreLoopHeap$loop#0, $Heap);
      free invariant (forall $o: ref, $f: Field :: 
        { read($Heap, $o, $f) } 
        $o != null && $Unbox(read($PreLoopHeap$loop#0, $o, alloc)): bool
           ==> read($Heap, $o, $f) == read($PreLoopHeap$loop#0, $o, $f)
             || $_ModifiesFrame[$o, $f]);
      free invariant (Set#Subset(unmarkedNodes#0, $decr_init$loop#00)
           && !Set#Subset($decr_init$loop#00, unmarkedNodes#0))
         || (Set#Subset(unmarkedNodes#0, $decr_init$loop#00)
           && (Seq#Rank(stackNodes#0) < Seq#Rank($decr_init$loop#01)
             || (Seq#Rank(stackNodes#0) <= Seq#Rank($decr_init$loop#01)
               && ((0 <= $decr_init$loop#02
                   && Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq)
                       - $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                     < $decr_init$loop#02)
                 || 
                (Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq)
                     - $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                   == $decr_init$loop#02
                 || (0 <= $decr_init$loop#02
                   && Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq)
                       - $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                     <= $decr_init$loop#02))))));
    {
        assume {:captureState "module:_module:state:42"} true;
        if (!$w$loop#0)
        {
            // Begin Comprehension WF check
            havoc i#0;
            if (true)
            {
                if (LitInt(0) <= i#0)
                {
                }

                if (LitInt(0) <= i#0 && i#0 < Seq#Length(stackNodes#0))
                {
                    assert {:id "id451"} {:subsumption 0} 0 <= i#0 && i#0 < Seq#Length(stackNodes#0);
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id452"} (forall i#1: int :: 
              { Seq#Index(stackNodes#0, i#1) } 
              LitInt(0) <= i#1 && i#1 < Seq#Length(stackNodes#0)
                 ==> Set#IsMember(S#0, Seq#Index(stackNodes#0, i#1)));
            if (Set#IsMember(S#0, $Box(t#0)))
            {
            }

            assume true;
            assume {:id "id454"} Set#IsMember(S#0, $Box(t#0)) && !Seq#Contains(stackNodes#0, $Box(t#0));
            if (Seq#Length(stackNodes#0) == LitInt(0))
            {
                newtype$check#3 := null;
            }
            else
            {
                assert {:id "id457"} {:subsumption 0} 0 <= Seq#Length(stackNodes#0) - 1
                   && Seq#Length(stackNodes#0) - 1 < Seq#Length(stackNodes#0);
            }

            assume true;
            assume {:id "id458"} p#0
               == (if Seq#Length(stackNodes#0) == LitInt(0)
                 then null
                 else $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref);
            // Begin Comprehension WF check
            havoc n#10;
            if ($Is(n#10, Tclass._module.Node()) && $IsAlloc(n#10, Tclass._module.Node(), $Heap))
            {
                if (Seq#Contains(stackNodes#0, $Box(n#10)))
                {
                    assert {:id "id460"} {:subsumption 0} n#10 != null;
                    assume true;
                    assert {:id "id461"} {:subsumption 0} n#10 != null;
                    assume true;
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id462"} (forall n#11: ref :: 
              { read($Heap, n#11, _module.Node.children) } 
                { read($Heap, n#11, _module.Node.childrenVisited) } 
                { Seq#Contains(stackNodes#0, $Box(n#11)) } 
              $Is(n#11, Tclass._module.Node())
                 ==> 
                Seq#Contains(stackNodes#0, $Box(n#11))
                 ==> $Unbox(read($Heap, n#11, _module.Node.childrenVisited)): int
                   < Seq#Length($Unbox(read($Heap, n#11, _module.Node.children)): Seq));
            assert {:id "id464"} {:subsumption 0} t#0 != null;
            assume true;
            assert {:id "id465"} {:subsumption 0} t#0 != null;
            assume true;
            assume true;
            assume {:id "id466"} $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
               <= Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq);
            // Begin Comprehension WF check
            havoc n#12;
            if ($Is(n#12, Tclass._module.Node()) && $IsAlloc(n#12, Tclass._module.Node(), $Heap))
            {
                if (Set#IsMember(S#0, $Box(n#12)))
                {
                }

                if (Set#IsMember(S#0, $Box(n#12)) && !Seq#Contains(stackNodes#0, $Box(n#12)))
                {
                }

                if (Set#IsMember(S#0, $Box(n#12))
                   && !Seq#Contains(stackNodes#0, $Box(n#12))
                   && n#12 != t#0)
                {
                    assert {:id "id468"} {:subsumption 0} n#12 != null;
                    assume true;
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id469"} (forall n#13: ref :: 
              { read($Heap, n#13, _module.Node.childrenVisited) } 
                { Seq#Contains(stackNodes#0, $Box(n#13)) } 
                { Set#IsMember(S#0, $Box(n#13)) } 
              $Is(n#13, Tclass._module.Node())
                 ==> 
                Set#IsMember(S#0, $Box(n#13))
                   && !Seq#Contains(stackNodes#0, $Box(n#13))
                   && n#13 != t#0
                 ==> $Unbox(read($Heap, n#13, _module.Node.childrenVisited)): int == LitInt(0));
            // Begin Comprehension WF check
            havoc n#14;
            if ($Is(n#14, Tclass._module.Node()) && $IsAlloc(n#14, Tclass._module.Node(), $Heap))
            {
                if (Seq#Contains(stackNodes#0, $Box(n#14)))
                {
                    assert {:id "id471"} {:subsumption 0} n#14 != null;
                    assume true;
                    assert {:id "id472"} {:subsumption 0} n#14 != null;
                    assert {:id "id473"} $IsAlloc(n#14, Tclass._module.Node(), old($Heap));
                    assume true;
                    if (Seq#Length($Unbox(read($Heap, n#14, _module.Node.children)): Seq)
                       == Seq#Length($Unbox(read(old($Heap), n#14, _module.Node.children)): Seq))
                    {
                        // Begin Comprehension WF check
                        havoc j#0;
                        if (true)
                        {
                            if (LitInt(0) <= j#0)
                            {
                                assert {:id "id474"} {:subsumption 0} n#14 != null;
                                assume true;
                            }

                            if (LitInt(0) <= j#0
                               && j#0 < Seq#Length($Unbox(read($Heap, n#14, _module.Node.children)): Seq))
                            {
                                assert {:id "id475"} {:subsumption 0} n#14 != null;
                                assume true;
                                if (j#0 != $Unbox(read($Heap, n#14, _module.Node.childrenVisited)): int)
                                {
                                    assert {:id "id476"} {:subsumption 0} n#14 != null;
                                    assume true;
                                    assert {:id "id477"} {:subsumption 0} 0 <= j#0
                                       && j#0 < Seq#Length($Unbox(read($Heap, n#14, _module.Node.children)): Seq);
                                    assert {:id "id478"} {:subsumption 0} n#14 != null;
                                    assert {:id "id479"} $IsAlloc(n#14, Tclass._module.Node(), old($Heap));
                                    assume true;
                                    assert {:id "id480"} {:subsumption 0} 0 <= j#0
                                       && j#0 < Seq#Length($Unbox(read(old($Heap), n#14, _module.Node.children)): Seq);
                                }
                            }
                        }

                        assume true;
                        // End Comprehension WF check
                        assume true;
                    }
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id481"} (forall n#15: ref :: 
              { read($Heap, n#15, _module.Node.childrenVisited) } 
                { read(old($Heap), n#15, _module.Node.children) } 
                { read($Heap, n#15, _module.Node.children) } 
                { Seq#Contains(stackNodes#0, $Box(n#15)) } 
              $Is(n#15, Tclass._module.Node())
                 ==> (Seq#Contains(stackNodes#0, $Box(n#15))
                     ==> Seq#Length($Unbox(read($Heap, n#15, _module.Node.children)): Seq)
                       == Seq#Length($Unbox(read(old($Heap), n#15, _module.Node.children)): Seq))
                   && (Seq#Contains(stackNodes#0, $Box(n#15))
                     ==> (forall j#2: int :: 
                      { Seq#Index($Unbox(read(old($Heap), n#15, _module.Node.children)): Seq, j#2) } 
                        { Seq#Index($Unbox(read($Heap, n#15, _module.Node.children)): Seq, j#2) } 
                      LitInt(0) <= j#2
                           && j#2 < Seq#Length($Unbox(read($Heap, n#15, _module.Node.children)): Seq)
                         ==> j#2 == $Unbox(read($Heap, n#15, _module.Node.childrenVisited)): int
                           || $Unbox(Seq#Index($Unbox(read($Heap, n#15, _module.Node.children)): Seq, j#2)): ref
                             == $Unbox(Seq#Index($Unbox(read(old($Heap), n#15, _module.Node.children)): Seq, j#2)): ref)));
            // Begin Comprehension WF check
            havoc n#16;
            if ($Is(n#16, Tclass._module.Node()) && $IsAlloc(n#16, Tclass._module.Node(), $Heap))
            {
                if (Set#IsMember(S#0, $Box(n#16)))
                {
                }

                if (Set#IsMember(S#0, $Box(n#16)) && !Seq#Contains(stackNodes#0, $Box(n#16)))
                {
                    assert {:id "id483"} {:subsumption 0} n#16 != null;
                    assume true;
                    assert {:id "id484"} {:subsumption 0} n#16 != null;
                    assert {:id "id485"} $IsAlloc(n#16, Tclass._module.Node(), old($Heap));
                    assume true;
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id486"} (forall n#17: ref :: 
              { read(old($Heap), n#17, _module.Node.children) } 
                { read($Heap, n#17, _module.Node.children) } 
                { Seq#Contains(stackNodes#0, $Box(n#17)) } 
                { Set#IsMember(S#0, $Box(n#17)) } 
              $Is(n#17, Tclass._module.Node())
                 ==> 
                Set#IsMember(S#0, $Box(n#17)) && !Seq#Contains(stackNodes#0, $Box(n#17))
                 ==> Seq#Equal($Unbox(read($Heap, n#17, _module.Node.children)): Seq, 
                  $Unbox(read(old($Heap), n#17, _module.Node.children)): Seq));
            if (0 < Seq#Length(stackNodes#0))
            {
                assert {:id "id488"} {:subsumption 0} 0 <= LitInt(0) && LitInt(0) < Seq#Length(stackNodes#0);
                assert {:id "id489"} {:subsumption 0} $Unbox(Seq#Index(stackNodes#0, LitInt(0))): ref != null;
                assume true;
                // ----- assert statement ----- module:_module:statement:39
                assert {:id "id490"} {:subsumption 0} 0 <= LitInt(0) && LitInt(0) < Seq#Length(stackNodes#0);
                assume true;
                assert {:id "id491"} Seq#Contains(stackNodes#0, Seq#Index(stackNodes#0, LitInt(0)));
                // ----- assert statement ----- module:_module:statement:40
                assert {:id "id492"} {:subsumption 0} 0 <= LitInt(0) && LitInt(0) < Seq#Length(stackNodes#0);
                assert {:id "id493"} {:subsumption 0} $Unbox(Seq#Index(stackNodes#0, LitInt(0))): ref != null;
                assume true;
                assert {:id "id494"} {:subsumption 0} 0 <= LitInt(0) && LitInt(0) < Seq#Length(stackNodes#0);
                assert {:id "id495"} {:subsumption 0} $Unbox(Seq#Index(stackNodes#0, LitInt(0))): ref != null;
                assume true;
                assume true;
                assert {:id "id496"} $Unbox(read($Heap, 
                      $Unbox(Seq#Index(stackNodes#0, LitInt(0))): ref, 
                      _module.Node.childrenVisited)): int
                   < Seq#Length($Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, LitInt(0))): ref, _module.Node.children)): Seq);
                assert {:id "id497"} {:subsumption 0} 0 <= LitInt(0) && LitInt(0) < Seq#Length(stackNodes#0);
                assert {:id "id498"} {:subsumption 0} $Unbox(Seq#Index(stackNodes#0, LitInt(0))): ref != null;
                assume true;
                assert {:id "id499"} {:subsumption 0} 0
                     <= $Unbox(read($Heap, 
                        $Unbox(Seq#Index(stackNodes#0, LitInt(0))): ref, 
                        _module.Node.childrenVisited)): int
                   && $Unbox(read($Heap, 
                        $Unbox(Seq#Index(stackNodes#0, LitInt(0))): ref, 
                        _module.Node.childrenVisited)): int
                     < Seq#Length($Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, LitInt(0))): ref, _module.Node.children)): Seq);
                newtype$check#4 := null;
            }

            assume true;
            assume {:id "id500"} 0 < Seq#Length(stackNodes#0)
               ==> $Unbox(Seq#Index($Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, LitInt(0))): ref, _module.Node.children)): Seq, 
                    $Unbox(read($Heap, 
                        $Unbox(Seq#Index(stackNodes#0, LitInt(0))): ref, 
                        _module.Node.childrenVisited)): int)): ref
                 == null;
            // Begin Comprehension WF check
            havoc k#0;
            if (true)
            {
                if (0 < k#0)
                {
                }

                if (0 < k#0 && k#0 < Seq#Length(stackNodes#0))
                {
                    assert {:id "id502"} {:subsumption 0} 0 <= k#0 && k#0 < Seq#Length(stackNodes#0);
                    assert {:id "id503"} {:subsumption 0} $Unbox(Seq#Index(stackNodes#0, k#0)): ref != null;
                    assume true;
                    assert {:id "id504"} {:subsumption 0} 0 <= k#0 && k#0 < Seq#Length(stackNodes#0);
                    assert {:id "id505"} {:subsumption 0} $Unbox(Seq#Index(stackNodes#0, k#0)): ref != null;
                    assume true;
                    assert {:id "id506"} {:subsumption 0} 0
                         <= $Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, k#0)): ref, _module.Node.childrenVisited)): int
                       && $Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, k#0)): ref, _module.Node.childrenVisited)): int
                         < Seq#Length($Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, k#0)): ref, _module.Node.children)): Seq);
                    assert {:id "id507"} {:subsumption 0} 0 <= k#0 - 1 && k#0 - 1 < Seq#Length(stackNodes#0);
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id508"} (forall k#1: int :: 
              { read($Heap, $Unbox(Seq#Index(stackNodes#0, k#1)): ref, _module.Node.childrenVisited) } 
                { read($Heap, $Unbox(Seq#Index(stackNodes#0, k#1)): ref, _module.Node.children) } 
              0 < k#1 && k#1 < Seq#Length(stackNodes#0)
                 ==> $Unbox(Seq#Index($Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, k#1)): ref, _module.Node.children)): Seq, 
                      $Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, k#1)): ref, _module.Node.childrenVisited)): int)): ref
                   == $Unbox(Seq#Index(stackNodes#0, k#1 - 1)): ref);
            // Begin Comprehension WF check
            havoc k#2;
            if (true)
            {
                if (LitInt(0) <= k#2)
                {
                }

                if (LitInt(0) <= k#2 && k#2 < Seq#Length(stackNodes#0) - 1)
                {
                    assert {:id "id510"} {:subsumption 0} 0 <= k#2 && k#2 < Seq#Length(stackNodes#0);
                    assert {:id "id511"} {:subsumption 0} $Unbox(Seq#Index(stackNodes#0, k#2)): ref != null;
                    assert {:id "id512"} $IsAlloc($Unbox(Seq#Index(stackNodes#0, k#2)): ref, Tclass._module.Node(), old($Heap));
                    assume true;
                    assert {:id "id513"} {:subsumption 0} 0 <= k#2 && k#2 < Seq#Length(stackNodes#0);
                    assert {:id "id514"} {:subsumption 0} $Unbox(Seq#Index(stackNodes#0, k#2)): ref != null;
                    assume true;
                    assert {:id "id515"} {:subsumption 0} 0
                         <= $Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, k#2)): ref, _module.Node.childrenVisited)): int
                       && $Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, k#2)): ref, _module.Node.childrenVisited)): int
                         < Seq#Length($Unbox(read(old($Heap), $Unbox(Seq#Index(stackNodes#0, k#2)): ref, _module.Node.children)): Seq);
                    assert {:id "id516"} {:subsumption 0} 0 <= k#2 + 1 && k#2 + 1 < Seq#Length(stackNodes#0);
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id517"} (forall k#3: int :: 
              { read($Heap, $Unbox(Seq#Index(stackNodes#0, k#3)): ref, _module.Node.childrenVisited) } 
                { Seq#Index(stackNodes#0, k#3) } 
              LitInt(0) <= k#3 && k#3 < Seq#Length(stackNodes#0) - 1
                 ==> $Unbox(Seq#Index($Unbox(read(old($Heap), $Unbox(Seq#Index(stackNodes#0, k#3)): ref, _module.Node.children)): Seq, 
                      $Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, k#3)): ref, _module.Node.childrenVisited)): int)): ref
                   == $Unbox(Seq#Index(stackNodes#0, k#3 + 1)): ref);
            if (0 < Seq#Length(stackNodes#0))
            {
                assert {:id "id519"} {:subsumption 0} 0 <= Seq#Length(stackNodes#0) - 1
                   && Seq#Length(stackNodes#0) - 1 < Seq#Length(stackNodes#0);
                assert {:id "id520"} {:subsumption 0} $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref != null;
                assert {:id "id521"} $IsAlloc($Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                  Tclass._module.Node(), 
                  old($Heap));
                assume true;
                assert {:id "id522"} {:subsumption 0} 0 <= Seq#Length(stackNodes#0) - 1
                   && Seq#Length(stackNodes#0) - 1 < Seq#Length(stackNodes#0);
                assert {:id "id523"} {:subsumption 0} $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref != null;
                assume true;
                assert {:id "id524"} {:subsumption 0} 0
                     <= $Unbox(read($Heap, 
                        $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                        _module.Node.childrenVisited)): int
                   && $Unbox(read($Heap, 
                        $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                        _module.Node.childrenVisited)): int
                     < Seq#Length($Unbox(read(old($Heap), 
                          $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                          _module.Node.children)): Seq);
            }

            assume true;
            assume {:id "id525"} 0 < Seq#Length(stackNodes#0)
               ==> $Unbox(Seq#Index($Unbox(read(old($Heap), 
                        $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                        _module.Node.children)): Seq, 
                    $Unbox(read($Heap, 
                        $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                        _module.Node.childrenVisited)): int)): ref
                 == t#0;
            assert {:id "id527"} {:subsumption 0} root#0 != null;
            assume true;
            assume true;
            assume {:id "id528"} $Unbox(read($Heap, root#0, _module.Node.marked)): bool;
            // Begin Comprehension WF check
            havoc n#18;
            if ($Is(n#18, Tclass._module.Node()) && $IsAlloc(n#18, Tclass._module.Node(), $Heap))
            {
                if (Set#IsMember(S#0, $Box(n#18)))
                {
                    assert {:id "id530"} {:subsumption 0} n#18 != null;
                    assume true;
                }

                if (Set#IsMember(S#0, $Box(n#18))
                   && $Unbox(read($Heap, n#18, _module.Node.marked)): bool)
                {
                }

                if (Set#IsMember(S#0, $Box(n#18))
                   && $Unbox(read($Heap, n#18, _module.Node.marked)): bool
                   && !Seq#Contains(stackNodes#0, $Box(n#18)))
                {
                }

                if (Set#IsMember(S#0, $Box(n#18))
                   && $Unbox(read($Heap, n#18, _module.Node.marked)): bool
                   && !Seq#Contains(stackNodes#0, $Box(n#18))
                   && n#18 != t#0)
                {
                    // Begin Comprehension WF check
                    havoc ch#6;
                    if ($Is(ch#6, Tclass._module.Node?())
                       && $IsAlloc(ch#6, Tclass._module.Node?(), $Heap))
                    {
                        assert {:id "id531"} {:subsumption 0} n#18 != null;
                        assume true;
                        if (Seq#Contains($Unbox(read($Heap, n#18, _module.Node.children)): Seq, $Box(ch#6)))
                        {
                            newtype$check#5 := null;
                        }

                        if (Seq#Contains($Unbox(read($Heap, n#18, _module.Node.children)): Seq, $Box(ch#6))
                           && ch#6 != null)
                        {
                            assert {:id "id532"} {:subsumption 0} ch#6 != null;
                            assume true;
                        }
                    }

                    assume true;
                    // End Comprehension WF check
                    assume true;
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id533"} (forall n#19: ref :: 
              { read($Heap, n#19, _module.Node.children) } 
                { Seq#Contains(stackNodes#0, $Box(n#19)) } 
                { read($Heap, n#19, _module.Node.marked) } 
                { Set#IsMember(S#0, $Box(n#19)) } 
              $Is(n#19, Tclass._module.Node())
                 ==> 
                Set#IsMember(S#0, $Box(n#19))
                   && $Unbox(read($Heap, n#19, _module.Node.marked)): bool
                   && !Seq#Contains(stackNodes#0, $Box(n#19))
                   && n#19 != t#0
                 ==> (forall ch#8: ref :: 
                  { read($Heap, ch#8, _module.Node.marked) } 
                    { Seq#Contains($Unbox(read($Heap, n#19, _module.Node.children)): Seq, $Box(ch#8)) } 
                  $Is(ch#8, Tclass._module.Node?())
                     ==> 
                    Seq#Contains($Unbox(read($Heap, n#19, _module.Node.children)): Seq, $Box(ch#8))
                       && ch#8 != null
                     ==> $Unbox(read($Heap, ch#8, _module.Node.marked)): bool));
            // Begin Comprehension WF check
            havoc n#20;
            if ($Is(n#20, Tclass._module.Node()) && $IsAlloc(n#20, Tclass._module.Node(), $Heap))
            {
                if (!Seq#Contains(stackNodes#0, $Box(n#20)))
                {
                }

                if (Seq#Contains(stackNodes#0, $Box(n#20)) || n#20 == t#0)
                {
                    assert {:id "id535"} {:subsumption 0} n#20 != null;
                    assume true;
                    if ($Unbox(read($Heap, n#20, _module.Node.marked)): bool)
                    {
                        // Begin Comprehension WF check
                        havoc j#3;
                        if (true)
                        {
                            if (LitInt(0) <= j#3)
                            {
                                assert {:id "id536"} {:subsumption 0} n#20 != null;
                                assume true;
                            }

                            if (LitInt(0) <= j#3
                               && j#3 < $Unbox(read($Heap, n#20, _module.Node.childrenVisited)): int)
                            {
                                assert {:id "id537"} {:subsumption 0} n#20 != null;
                                assume true;
                                assert {:id "id538"} {:subsumption 0} 0 <= j#3
                                   && j#3 < Seq#Length($Unbox(read($Heap, n#20, _module.Node.children)): Seq);
                                newtype$check#6 := null;
                                if ($Unbox(Seq#Index($Unbox(read($Heap, n#20, _module.Node.children)): Seq, j#3)): ref
                                   != null)
                                {
                                    assert {:id "id539"} {:subsumption 0} n#20 != null;
                                    assume true;
                                    assert {:id "id540"} {:subsumption 0} 0 <= j#3
                                       && j#3 < Seq#Length($Unbox(read($Heap, n#20, _module.Node.children)): Seq);
                                    assert {:id "id541"} {:subsumption 0} $Unbox(Seq#Index($Unbox(read($Heap, n#20, _module.Node.children)): Seq, j#3)): ref
                                       != null;
                                    assume true;
                                }
                            }
                        }

                        assume true;
                        // End Comprehension WF check
                        assume true;
                    }
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id542"} (forall n#21: ref :: 
                { read($Heap, n#21, _module.Node.marked) } 
                  { Seq#Contains(stackNodes#0, $Box(n#21)) } 
                $Is(n#21, Tclass._module.Node()) && $IsAlloc(n#21, Tclass._module.Node(), $Heap)
                   ==> 
                  Seq#Contains(stackNodes#0, $Box(n#21)) || n#21 == t#0
                   ==> $Unbox(read($Heap, n#21, _module.Node.marked)): bool)
               && (forall n#21: ref :: 
                { read($Heap, n#21, _module.Node.children) } 
                  { read($Heap, n#21, _module.Node.childrenVisited) } 
                  { Seq#Contains(stackNodes#0, $Box(n#21)) } 
                $Is(n#21, Tclass._module.Node()) && $IsAlloc(n#21, Tclass._module.Node(), $Heap)
                   ==> 
                  Seq#Contains(stackNodes#0, $Box(n#21)) || n#21 == t#0
                   ==> (forall j#5: int :: 
                    { Seq#Index($Unbox(read($Heap, n#21, _module.Node.children)): Seq, j#5) } 
                    LitInt(0) <= j#5
                         && j#5 < $Unbox(read($Heap, n#21, _module.Node.childrenVisited)): int
                       ==> $Unbox(Seq#Index($Unbox(read($Heap, n#21, _module.Node.children)): Seq, j#5)): ref
                           == null
                         || $Unbox(read($Heap, 
                            $Unbox(Seq#Index($Unbox(read($Heap, n#21, _module.Node.children)): Seq, j#5)): ref, 
                            _module.Node.marked)): bool));
            if ($IsAllocBox($Box(path#0), Tclass._module.Path(), old($Heap)))
            {
                ##source#1 := root#0;
                ##p#0 := path#0;
                assert {:id "id545"} $Is(t#0, Tclass._module.Node());
                ##sink#1 := t#0;
                ##S#1 := S#0;
                assert {:id "id546"} $IsAlloc(root#0, Tclass._module.Node(), old($Heap));
                assert {:id "id547"} $IsAlloc(path#0, Tclass._module.Path(), old($Heap));
                assert {:id "id548"} $IsAlloc(t#0, Tclass._module.Node?(), old($Heap));
                assert {:id "id549"} $IsAlloc(S#0, TSet(Tclass._module.Node()), old($Heap));
                assume _module.__default.ReachableVia#canCall(old($Heap), root#0, path#0, t#0, S#0);
            }

            assume $IsAllocBox($Box(path#0), Tclass._module.Path(), old($Heap))
               ==> _module.__default.ReachableVia#canCall(old($Heap), root#0, path#0, t#0, S#0);
            assume {:id "id550"} $IsAllocBox($Box(path#0), Tclass._module.Path(), old($Heap))
               && _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, path#0, t#0, S#0);
            // Begin Comprehension WF check
            havoc n#24;
            if ($Is(n#24, Tclass._module.Node()) && $IsAlloc(n#24, Tclass._module.Node(), $Heap))
            {
                if (Set#IsMember(S#0, $Box(n#24)))
                {
                    assert {:id "id557"} {:subsumption 0} n#24 != null;
                    assume true;
                }

                if (Set#IsMember(S#0, $Box(n#24))
                   && $Unbox(read($Heap, n#24, _module.Node.marked)): bool)
                {
                    havoc pth#Z#0;
                    assert {:id "id558"} {:subsumption 0} n#24 != null;
                    assume true;
                    assume true;
                    assume {:id "id559"} let#0#0#0 == $Unbox(read($Heap, n#24, _module.Node.pathFromRoot)): DatatypeType;
                    // CheckWellformedWithResult: any expression
                    assume $Is(let#0#0#0, Tclass._module.Path());
                    assume {:id "id560"} pth#Z#0 == let#0#0#0;
                    if ($IsAllocBox($Box(pth#Z#0), Tclass._module.Path(), old($Heap)))
                    {
                        ##source#2 := root#0;
                        ##p#1 := pth#Z#0;
                        ##sink#2 := n#24;
                        ##S#2 := S#0;
                        assert {:id "id561"} $IsAlloc(root#0, Tclass._module.Node(), old($Heap));
                        assert {:id "id562"} $IsAlloc(pth#Z#0, Tclass._module.Path(), old($Heap));
                        assert {:id "id563"} $IsAlloc(n#24, Tclass._module.Node(), old($Heap));
                        assert {:id "id564"} $IsAlloc(S#0, TSet(Tclass._module.Node()), old($Heap));
                        assume _module.__default.ReachableVia#canCall(old($Heap), root#0, pth#Z#0, n#24, S#0);
                    }
                }
            }

            assume true;
            // End Comprehension WF check
            assume (forall n#25: ref :: 
              { _module.__default.ReachableVia($LS($LZ), 
                  old($Heap), 
                  root#0, 
                  $Unbox(read(old($Heap), n#25, _module.Node.pathFromRoot)): DatatypeType, 
                  n#25, 
                  S#0) } 
                { read($Heap, n#25, _module.Node.pathFromRoot) } 
                { read($Heap, n#25, _module.Node.marked) } 
                { Set#IsMember(S#0, $Box(n#25)) } 
              $Is(n#25, Tclass._module.Node())
                 ==> 
                Set#IsMember(S#0, $Box(n#25))
                 ==> 
                $Unbox(read($Heap, n#25, _module.Node.marked)): bool
                 ==> (var pth#0 := $Unbox(read($Heap, n#25, _module.Node.pathFromRoot)): DatatypeType; 
                  $IsAllocBox($Box(pth#0), Tclass._module.Path(), old($Heap))
                     ==> _module.__default.ReachableVia#canCall(old($Heap), root#0, pth#0, n#25, S#0)));
            assume (forall n#25: ref :: 
              { _module.__default.ReachableVia($LS($LZ), 
                  old($Heap), 
                  root#0, 
                  $Unbox(read(old($Heap), n#25, _module.Node.pathFromRoot)): DatatypeType, 
                  n#25, 
                  S#0) } 
                { read($Heap, n#25, _module.Node.pathFromRoot) } 
                { read($Heap, n#25, _module.Node.marked) } 
                { Set#IsMember(S#0, $Box(n#25)) } 
              $Is(n#25, Tclass._module.Node())
                 ==> 
                Set#IsMember(S#0, $Box(n#25))
                 ==> 
                $Unbox(read($Heap, n#25, _module.Node.marked)): bool
                 ==> (var pth#0 := $Unbox(read($Heap, n#25, _module.Node.pathFromRoot)): DatatypeType; 
                  $IsAllocBox($Box(pth#0), Tclass._module.Path(), old($Heap))
                     ==> _module.__default.ReachableVia#canCall(old($Heap), root#0, pth#0, n#25, S#0)));
            assume {:id "id565"} (forall n#25: ref :: 
              { _module.__default.ReachableVia($LS($LZ), 
                  old($Heap), 
                  root#0, 
                  $Unbox(read(old($Heap), n#25, _module.Node.pathFromRoot)): DatatypeType, 
                  n#25, 
                  S#0) } 
                { read($Heap, n#25, _module.Node.pathFromRoot) } 
                { read($Heap, n#25, _module.Node.marked) } 
                { Set#IsMember(S#0, $Box(n#25)) } 
              $Is(n#25, Tclass._module.Node())
                 ==> 
                Set#IsMember(S#0, $Box(n#25))
                   && $Unbox(read($Heap, n#25, _module.Node.marked)): bool
                 ==> (var pth#0 := $Unbox(read($Heap, n#25, _module.Node.pathFromRoot)): DatatypeType; 
                  $IsAllocBox($Box(pth#0), Tclass._module.Path(), old($Heap))
                     && _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, pth#0, n#25, S#0)));
            // Begin Comprehension WF check
            havoc n#26;
            if ($Is(n#26, Tclass._module.Node()) && $IsAlloc(n#26, Tclass._module.Node(), $Heap))
            {
                if (Set#IsMember(S#0, $Box(n#26)))
                {
                    assert {:id "id567"} {:subsumption 0} n#26 != null;
                    assume true;
                }

                if (Set#IsMember(S#0, $Box(n#26))
                   && $Unbox(read($Heap, n#26, _module.Node.marked)): bool)
                {
                    ##source#3 := root#0;
                    ##sink#3 := n#26;
                    ##S#3 := S#0;
                    assert {:id "id568"} $IsAlloc(root#0, Tclass._module.Node(), old($Heap));
                    assert {:id "id569"} $IsAlloc(n#26, Tclass._module.Node(), old($Heap));
                    assert {:id "id570"} $IsAlloc(S#0, TSet(Tclass._module.Node()), old($Heap));
                    assume _module.__default.Reachable#canCall(old($Heap), root#0, n#26, S#0);
                }
            }

            assume true;
            // End Comprehension WF check
            assume (forall n#27: ref :: 
              { _module.__default.Reachable(old($Heap), root#0, n#27, S#0) } 
                { read($Heap, n#27, _module.Node.marked) } 
                { Set#IsMember(S#0, $Box(n#27)) } 
              $Is(n#27, Tclass._module.Node())
                 ==> 
                Set#IsMember(S#0, $Box(n#27))
                 ==> 
                $Unbox(read($Heap, n#27, _module.Node.marked)): bool
                 ==> _module.__default.Reachable#canCall(old($Heap), root#0, n#27, S#0));
            assume (forall n#27: ref :: 
              { _module.__default.Reachable(old($Heap), root#0, n#27, S#0) } 
                { read($Heap, n#27, _module.Node.marked) } 
                { Set#IsMember(S#0, $Box(n#27)) } 
              $Is(n#27, Tclass._module.Node())
                 ==> 
                Set#IsMember(S#0, $Box(n#27))
                 ==> 
                $Unbox(read($Heap, n#27, _module.Node.marked)): bool
                 ==> _module.__default.Reachable#canCall(old($Heap), root#0, n#27, S#0));
            assume {:id "id571"} (forall n#27: ref :: 
              { _module.__default.Reachable(old($Heap), root#0, n#27, S#0) } 
                { read($Heap, n#27, _module.Node.marked) } 
                { Set#IsMember(S#0, $Box(n#27)) } 
              $Is(n#27, Tclass._module.Node())
                 ==> 
                Set#IsMember(S#0, $Box(n#27))
                   && $Unbox(read($Heap, n#27, _module.Node.marked)): bool
                 ==> _module.__default.Reachable(old($Heap), root#0, n#27, S#0));
            // Begin Comprehension WF check
            havoc n#28;
            if ($Is(n#28, Tclass._module.Node()) && $IsAlloc(n#28, Tclass._module.Node(), $Heap))
            {
                if (Set#IsMember(S#0, $Box(n#28)))
                {
                    assert {:id "id573"} {:subsumption 0} n#28 != null;
                    assume true;
                }

                if (Set#IsMember(S#0, $Box(n#28))
                   && !$Unbox(read($Heap, n#28, _module.Node.marked)): bool)
                {
                }
            }

            assume true;
            // End Comprehension WF check
            assume true;
            assume true;
            assume {:id "id574"} (forall n#29: ref :: 
              { Set#IsMember(unmarkedNodes#0, $Box(n#29)) } 
                { read($Heap, n#29, _module.Node.marked) } 
                { Set#IsMember(S#0, $Box(n#29)) } 
              $Is(n#29, Tclass._module.Node())
                 ==> 
                Set#IsMember(S#0, $Box(n#29))
                   && !$Unbox(read($Heap, n#29, _module.Node.marked)): bool
                 ==> Set#IsMember(unmarkedNodes#0, $Box(n#29)));
            assume true;
            assume true;
            assert {:id "id576"} t#0 != null;
            assume true;
            assert {:id "id577"} t#0 != null;
            assume true;
            assume true;
            assume false;
        }

        assume true;
        if (!Lit(true))
        {
            break;
        }

        assume true;
        assume true;
        assume true;
        $decr$loop#00 := unmarkedNodes#0;
        $decr$loop#01 := stackNodes#0;
        $decr$loop#02 := Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq)
           - $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int;
        // ----- if statement ----- module:_module:statement:41
        assert {:id "id578"} t#0 != null;
        assume true;
        assert {:id "id579"} t#0 != null;
        assume true;
        assume true;
        if {:allow_path_isolation} ($Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
           == Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq))
        {
            // ----- assert statement ----- module:_module:statement:42
            assume true;
            assert {:id "id580"} {:focus} Lit(true);
            // ----- assignment statement ----- module:_module:statement:43
            assert {:id "id581"} t#0 != null;
            assume true;
            assume true;
            assert {:id "id582"} $_ModifiesFrame[t#0, _module.Node.childrenVisited];
            assert {:id "id583"} $Is(LitInt(0), Tclass._System.nat());
            assume true;
            $rhs#0_0_0 := {:id "id584"} LitInt(0);
            $Heap := {:id "id585"} update($Heap, t#0, _module.Node.childrenVisited, $Box($rhs#0_0_0));
            assume $IsGoodHeap($Heap);
            assume {:captureState "module:_module:state:43"} true;
            // ----- if statement ----- module:_module:statement:44
            newtype$check#0_0_0 := null;
            assume true;
            if {:allow_path_isolation} (p#0 == null)
            {
                push;
                // ----- return statement ----- module:_module:statement:45
                pop;
                return;

                pop;
            }
            else
            {
            }

            // ----- assignment statement ----- module:_module:statement:46
            assume true;
            assert {:id "id586"} p#0 != null;
            assume true;
            assert {:id "id587"} p#0 != null;
            assume true;
            assert {:id "id588"} 0 <= $Unbox(read($Heap, p#0, _module.Node.childrenVisited)): int
               && $Unbox(read($Heap, p#0, _module.Node.childrenVisited)): int
                 < Seq#Length($Unbox(read($Heap, p#0, _module.Node.children)): Seq);
            assume true;
            oldP#0_0_0 := {:id "id589"} $Unbox(Seq#Index($Unbox(read($Heap, p#0, _module.Node.children)): Seq, 
                $Unbox(read($Heap, p#0, _module.Node.childrenVisited)): int)): ref;
            assume {:captureState "module:_module:state:44"} true;
            // ----- assignment statement ----- module:_module:statement:47
            assert {:id "id590"} p#0 != null;
            assume true;
            assume true;
            assert {:id "id591"} $_ModifiesFrame[p#0, _module.Node.children];
            assert {:id "id592"} p#0 != null;
            assume true;
            assert {:id "id593"} p#0 != null;
            assume true;
            assert {:id "id594"} 0 <= $Unbox(read($Heap, p#0, _module.Node.childrenVisited)): int
               && $Unbox(read($Heap, p#0, _module.Node.childrenVisited)): int
                 < Seq#Length($Unbox(read($Heap, p#0, _module.Node.children)): Seq);
            assume true;
            $rhs#0_0_1 := {:id "id595"} Seq#Update($Unbox(read($Heap, p#0, _module.Node.children)): Seq, 
              $Unbox(read($Heap, p#0, _module.Node.childrenVisited)): int, 
              $Box(t#0));
            $Heap := {:id "id596"} update($Heap, p#0, _module.Node.children, $Box($rhs#0_0_1));
            assume $IsGoodHeap($Heap);
            assume {:captureState "module:_module:state:45"} true;
            // ----- assignment statement ----- module:_module:statement:48
            assume true;
            assume true;
            t#0 := {:id "id597"} p#0;
            assume {:captureState "module:_module:state:46"} true;
            // ----- assignment statement ----- module:_module:statement:49
            assume true;
            assume true;
            p#0 := {:id "id598"} oldP#0_0_0;
            assume {:captureState "module:_module:state:47"} true;
            // ----- assignment statement ----- module:_module:statement:50
            assume true;
            assert {:id "id599"} 0 <= Seq#Length(stackNodes#0) - 1
               && Seq#Length(stackNodes#0) - 1 <= Seq#Length(stackNodes#0);
            assume true;
            stackNodes#0 := {:id "id600"} Seq#Take(stackNodes#0, Seq#Length(stackNodes#0) - 1);
            assume {:captureState "module:_module:state:48"} true;
            // ----- assignment statement ----- module:_module:statement:51
            assert {:id "id601"} t#0 != null;
            assume true;
            assume true;
            assert {:id "id602"} $_ModifiesFrame[t#0, _module.Node.childrenVisited];
            assert {:id "id603"} t#0 != null;
            assume true;
            assert {:id "id604"} $Is($Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int + 1, 
              Tclass._System.nat());
            assume true;
            $rhs#0_0_2 := {:id "id605"} $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int + 1;
            $Heap := {:id "id606"} update($Heap, t#0, _module.Node.childrenVisited, $Box($rhs#0_0_2));
            assume $IsGoodHeap($Heap);
            assume {:captureState "module:_module:state:49"} true;
            // ----- assignment statement ----- module:_module:statement:52
            assume true;
            assert {:id "id607"} t#0 != null;
            assume true;
            assume true;
            path#0 := {:id "id608"} $Unbox(read($Heap, t#0, _module.Node.pathFromRoot)): DatatypeType;
            assume {:captureState "module:_module:state:50"} true;
        }
        else
        {
            // ----- if statement ----- module:_module:statement:53
            assert {:id "id609"} t#0 != null;
            assume true;
            assert {:id "id610"} t#0 != null;
            assume true;
            assert {:id "id611"} 0 <= $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
               && $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                 < Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq);
            newtype$check#0_1_0 := null;
            if ($Unbox(Seq#Index($Unbox(read($Heap, t#0, _module.Node.children)): Seq, 
                  $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int)): ref
               != null)
            {
                assert {:id "id612"} t#0 != null;
                assume true;
                assert {:id "id613"} t#0 != null;
                assume true;
                assert {:id "id614"} 0 <= $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                   && $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                     < Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq);
                assert {:id "id615"} $Unbox(Seq#Index($Unbox(read($Heap, t#0, _module.Node.children)): Seq, 
                      $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int)): ref
                   != null;
                assume true;
            }

            assume true;
            if {:allow_path_isolation} ($Unbox(Seq#Index($Unbox(read($Heap, t#0, _module.Node.children)): Seq, 
                    $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int)): ref
                 == null
               || $Unbox(read($Heap, 
                  $Unbox(Seq#Index($Unbox(read($Heap, t#0, _module.Node.children)): Seq, 
                      $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int)): ref, 
                  _module.Node.marked)): bool)
            {
                // ----- assert statement ----- module:_module:statement:54
                assume true;
                assert {:id "id616"} {:focus} Lit(true);
                // ----- assignment statement ----- module:_module:statement:55
                assert {:id "id617"} t#0 != null;
                assume true;
                assume true;
                assert {:id "id618"} $_ModifiesFrame[t#0, _module.Node.childrenVisited];
                assert {:id "id619"} t#0 != null;
                assume true;
                assert {:id "id620"} $Is($Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int + 1, 
                  Tclass._System.nat());
                assume true;
                $rhs#0_1_0_0 := {:id "id621"} $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int + 1;
                $Heap := {:id "id622"} update($Heap, t#0, _module.Node.childrenVisited, $Box($rhs#0_1_0_0));
                assume $IsGoodHeap($Heap);
                assume {:captureState "module:_module:state:51"} true;
            }
            else
            {
                // ----- assert statement ----- module:_module:statement:56
                assume true;
                assert {:id "id623"} {:focus} Lit(true);
                $Heap_at_0_1_1_0 := $Heap;

              after_0_1_1_0:
                // ----- assignment statement ----- module:_module:statement:57
                assume true;
                assert {:id "id624"} t#0 != null;
                assume true;
                assert {:id "id625"} t#0 != null;
                assume true;
                assert {:id "id626"} 0 <= $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                   && $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                     < Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq);
                assume true;
                newT#0_1_1_0 := {:id "id627"} $Unbox(Seq#Index($Unbox(read($Heap, t#0, _module.Node.children)): Seq, 
                    $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int)): ref;
                assume {:captureState "module:_module:state:52"} true;
                // ----- assignment statement ----- module:_module:statement:58
                assert {:id "id628"} t#0 != null;
                assume true;
                assume true;
                assert {:id "id629"} $_ModifiesFrame[t#0, _module.Node.children];
                assert {:id "id630"} t#0 != null;
                assume true;
                assert {:id "id631"} t#0 != null;
                assume true;
                assert {:id "id632"} 0 <= $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                   && $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                     < Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq);
                assume true;
                $rhs#0_1_1_0 := {:id "id633"} Seq#Update($Unbox(read($Heap, t#0, _module.Node.children)): Seq, 
                  $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int, 
                  $Box(p#0));
                $Heap := {:id "id634"} update($Heap, t#0, _module.Node.children, $Box($rhs#0_1_1_0));
                assume $IsGoodHeap($Heap);
                assume {:captureState "module:_module:state:53"} true;
                // ----- assignment statement ----- module:_module:statement:59
                assume true;
                assume true;
                p#0 := {:id "id635"} t#0;
                assume {:captureState "module:_module:state:54"} true;
                // ----- assignment statement ----- module:_module:statement:60
                assume true;
                assert {:id "id636"} $Is(Seq#Append(stackNodes#0, Seq#Build(Seq#Empty(): Seq, $Box(t#0))), 
                  TSeq(Tclass._module.Node()));
                assume true;
                stackNodes#0 := {:id "id637"} Seq#Append(stackNodes#0, Seq#Build(Seq#Empty(): Seq, $Box(t#0)));
                assume {:captureState "module:_module:state:55"} true;
                // ----- assignment statement ----- module:_module:statement:61
                assume true;
                assert {:id "id638"} $Is(t#0, Tclass._module.Node());
                assume true;
                path#0 := {:id "id639"} #_module.Path.Extend(path#0, t#0);
                assume {:captureState "module:_module:state:56"} true;
                // ----- assignment statement ----- module:_module:statement:62
                assume true;
                assume true;
                t#0 := {:id "id640"} newT#0_1_1_0;
                assume {:captureState "module:_module:state:57"} true;
                // ----- assignment statement ----- module:_module:statement:63
                assert {:id "id641"} t#0 != null;
                assume true;
                assume true;
                assert {:id "id642"} $_ModifiesFrame[t#0, _module.Node.marked];
                assume true;
                $rhs#0_1_1_1 := {:id "id643"} Lit(true);
                $Heap := {:id "id644"} update($Heap, t#0, _module.Node.marked, $Box($rhs#0_1_1_1));
                assume $IsGoodHeap($Heap);
                assume {:captureState "module:_module:state:58"} true;
                // ----- assignment statement ----- module:_module:statement:64
                assert {:id "id645"} t#0 != null;
                assume true;
                assume true;
                assert {:id "id646"} $_ModifiesFrame[t#0, _module.Node.pathFromRoot];
                assume true;
                $rhs#0_1_1_2 := {:id "id647"} path#0;
                $Heap := {:id "id648"} update($Heap, t#0, _module.Node.pathFromRoot, $Box($rhs#0_1_1_2));
                assume $IsGoodHeap($Heap);
                assume {:captureState "module:_module:state:59"} true;
                // ----- assignment statement ----- module:_module:statement:65
                assume true;
                assume true;
                unmarkedNodes#0 := {:id "id649"} Set#Difference(unmarkedNodes#0, Set#UnionOne(Set#Empty(): Set, $Box(t#0)));
                assume {:captureState "module:_module:state:60"} true;
                // ----- forall statement (proof) ----- module:_module:statement:66
                if (*)
                {
                    // Assume Fuel Constant
                    havoc n#0_1_1_0_0;
                    assume $Is(n#0_1_1_0_0, Tclass._module.Node());
                    if (Set#IsMember(S#0, $Box(n#0_1_1_0_0)))
                    {
                        assert {:id "id650"} {:subsumption 0} n#0_1_1_0_0 != null;
                        assume true;
                    }

                    assume true;
                    assume {:id "id651"} Set#IsMember(S#0, $Box(n#0_1_1_0_0))
                       && $Unbox(read($Heap, n#0_1_1_0_0, _module.Node.marked)): bool;
                    if (*)
                    {
                        havoc pth#Z#0_1_1_0_0;
                        assert {:id "id652"} {:subsumption 0} n#0_1_1_0_0 != null;
                        assume true;
                        assume true;
                        assume {:id "id653"} let#0_1_1_0_0#0#0
                           == $Unbox(read($Heap, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType;
                        // CheckWellformedWithResult: any expression
                        assume $Is(let#0_1_1_0_0#0#0, Tclass._module.Path());
                        assume {:id "id654"} pth#Z#0_1_1_0_0 == let#0_1_1_0_0#0#0;
                        assume true;
                        assume {:id "id655"} (var pth#0_1_1_0_0 := $Unbox(read($Heap, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType; 
                          $IsAllocBox($Box(pth#0_1_1_0_0), Tclass._module.Path(), old($Heap)));
                        havoc pth#Z#0_1_1_0_1;
                        assert {:id "id656"} {:subsumption 0} n#0_1_1_0_0 != null;
                        assume true;
                        assume true;
                        assume {:id "id657"} let#0_1_1_0_1#0#0
                           == $Unbox(read($Heap, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType;
                        // CheckWellformedWithResult: any expression
                        assume $Is(let#0_1_1_0_1#0#0, Tclass._module.Path());
                        assume {:id "id658"} pth#Z#0_1_1_0_1 == let#0_1_1_0_1#0#0;
                        ##source#0_1_1_0_0 := root#0;
                        ##p#0_1_1_0_0 := pth#Z#0_1_1_0_1;
                        ##sink#0_1_1_0_0 := n#0_1_1_0_0;
                        ##S#0_1_1_0_0 := S#0;
                        assert {:id "id659"} $IsAlloc(root#0, Tclass._module.Node(), old($Heap));
                        assert {:id "id660"} $IsAlloc(pth#Z#0_1_1_0_1, Tclass._module.Path(), old($Heap));
                        assert {:id "id661"} $IsAlloc(n#0_1_1_0_0, Tclass._module.Node(), old($Heap));
                        assert {:id "id662"} $IsAlloc(S#0, TSet(Tclass._module.Node()), old($Heap));
                        assume _module.__default.ReachableVia#canCall(old($Heap), root#0, pth#Z#0_1_1_0_1, n#0_1_1_0_0, S#0);
                        assume (var pth#0_1_1_0_0 := $Unbox(read($Heap, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType; 
                          _module.__default.ReachableVia#canCall(old($Heap), root#0, pth#0_1_1_0_0, n#0_1_1_0_0, S#0));
                        assume {:id "id663"} (var pth#0_1_1_0_0 := $Unbox(read($Heap, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType; 
                          _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, pth#0_1_1_0_0, n#0_1_1_0_0, S#0));
                        assume false;
                    }

                    // ----- if statement ----- module:_module:statement:67
                    assume true;
                    if {:allow_path_isolation} (n#0_1_1_0_0 == t#0)
                    {
                        // ----- assert statement ----- module:_module:statement:68
                        assert {:id "id664"} {:subsumption 0} n#0_1_1_0_0 != null;
                        assume true;
                        assume $IsA#_module.Path($Unbox(read($Heap, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType)
                           && $IsA#_module.Path(path#0);
                        assert {:id "id665"} _module.Path#Equal($Unbox(read($Heap, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType, 
                          path#0);
                        // ----- assert statement ----- module:_module:statement:69
                        ##source#0_1_1_0_0_0 := root#0;
                        ##p#0_1_1_0_0_0 := path#0;
                        assert {:id "id666"} $Is(t#0, Tclass._module.Node());
                        ##sink#0_1_1_0_0_0 := t#0;
                        ##S#0_1_1_0_0_0 := S#0;
                        assert {:id "id667"} $IsAlloc(root#0, Tclass._module.Node(), old($Heap));
                        assert {:id "id668"} $IsAlloc(path#0, Tclass._module.Path(), old($Heap));
                        assert {:id "id669"} $IsAlloc(t#0, Tclass._module.Node?(), old($Heap));
                        assert {:id "id670"} $IsAlloc(S#0, TSet(Tclass._module.Node()), old($Heap));
                        assume _module.__default.ReachableVia#canCall(old($Heap), root#0, path#0, t#0, S#0);
                        assume _module.__default.ReachableVia#canCall(old($Heap), root#0, path#0, t#0, S#0);
                        assert {:id "id671"} {:subsumption 0} _module.__default.ReachableVia#canCall(old($Heap), root#0, path#0, t#0, S#0)
                           ==> _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, path#0, t#0, S#0)
                             || (_module.Path.Empty_q(path#0) ==> root#0 == t#0);
                        assert {:id "id672"} {:subsumption 0} _module.__default.ReachableVia#canCall(old($Heap), root#0, path#0, t#0, S#0)
                           ==> _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, path#0, t#0, S#0)
                             || (!_module.Path.Empty_q(path#0)
                               ==> (var n#0_1_1_0_0_0 := _module.Path._h1(path#0); 
                                (var prefix#0_1_1_0_0_0 := _module.Path._h0(path#0); 
                                  Set#IsMember(S#0, $Box(n#0_1_1_0_0_0)))));
                        assert {:id "id673"} {:subsumption 0} _module.__default.ReachableVia#canCall(old($Heap), root#0, path#0, t#0, S#0)
                           ==> _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, path#0, t#0, S#0)
                             || (!_module.Path.Empty_q(path#0)
                               ==> (var n#0_1_1_0_0_0 := _module.Path._h1(path#0); 
                                (var prefix#0_1_1_0_0_0 := _module.Path._h0(path#0); 
                                  Seq#Contains($Unbox(read(old($Heap), n#0_1_1_0_0_0, _module.Node.children)): Seq, $Box(t#0)))));
                        assert {:id "id674"} {:subsumption 0} _module.__default.ReachableVia#canCall(old($Heap), root#0, path#0, t#0, S#0)
                           ==> _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, path#0, t#0, S#0)
                             || (!_module.Path.Empty_q(path#0)
                               ==> (var n#0_1_1_0_0_0 := _module.Path._h1(path#0); 
                                (var prefix#0_1_1_0_0_0 := _module.Path._h0(path#0); 
                                  _module.__default.ReachableVia($LS($LS($LZ)), old($Heap), root#0, prefix#0_1_1_0_0_0, n#0_1_1_0_0_0, S#0))));
                        assume {:id "id675"} _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, path#0, t#0, S#0);
                    }
                    else
                    {
                        // ----- assert statement ----- module:_module:statement:70
                        assert {:id "id676"} {:subsumption 0} n#0_1_1_0_0 != null;
                        assert {:id "id677"} $IsAlloc(n#0_1_1_0_0, Tclass._module.Node(), $Heap_at_0_1_1_0);
                        assume true;
                        assume true;
                        assert {:id "id678"} $Unbox(read($Heap_at_0_1_1_0, n#0_1_1_0_0, _module.Node.marked)): bool;
                        // ----- assert statement ----- module:_module:statement:71
                        assert {:id "id679"} {:subsumption 0} n#0_1_1_0_0 != null;
                        assume true;
                        assert {:id "id680"} {:subsumption 0} n#0_1_1_0_0 != null;
                        assert {:id "id681"} $IsAlloc(n#0_1_1_0_0, Tclass._module.Node(), $Heap_at_0_1_1_0);
                        assume true;
                        assume $IsA#_module.Path($Unbox(read($Heap, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType)
                           && $IsA#_module.Path($Unbox(read($Heap_at_0_1_1_0, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType);
                        assert {:id "id682"} _module.Path#Equal($Unbox(read($Heap, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType, 
                          $Unbox(read($Heap_at_0_1_1_0, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType);
                    }

                    assume true;
                    assert {:id "id683"} (var pth#0_1_1_0_0 := $Unbox(read($Heap, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType; 
                      $IsAllocBox($Box(pth#0_1_1_0_0), Tclass._module.Path(), old($Heap)));
                    assume (var pth#0_1_1_0_0 := $Unbox(read($Heap, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType; 
                      _module.__default.ReachableVia#canCall(old($Heap), root#0, pth#0_1_1_0_0, n#0_1_1_0_0, S#0));
                    assert {:id "id684"} (var pth#0_1_1_0_0 := $Unbox(read($Heap, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType; 
                      _module.__default.ReachableVia#canCall(old($Heap), root#0, pth#0_1_1_0_0, n#0_1_1_0_0, S#0)
                         ==> _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, pth#0_1_1_0_0, n#0_1_1_0_0, S#0)
                           || (_module.Path.Empty_q(pth#0_1_1_0_0) ==> root#0 == n#0_1_1_0_0));
                    assert {:id "id685"} (var pth#0_1_1_0_0 := $Unbox(read($Heap, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType; 
                      _module.__default.ReachableVia#canCall(old($Heap), root#0, pth#0_1_1_0_0, n#0_1_1_0_0, S#0)
                         ==> _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, pth#0_1_1_0_0, n#0_1_1_0_0, S#0)
                           || (!_module.Path.Empty_q(pth#0_1_1_0_0)
                             ==> (var n#0_1_1_0_1 := _module.Path._h1(pth#0_1_1_0_0); 
                              (var prefix#0_1_1_0_0 := _module.Path._h0(pth#0_1_1_0_0); 
                                Set#IsMember(S#0, $Box(n#0_1_1_0_1))))));
                    assert {:id "id686"} (var pth#0_1_1_0_0 := $Unbox(read($Heap, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType; 
                      _module.__default.ReachableVia#canCall(old($Heap), root#0, pth#0_1_1_0_0, n#0_1_1_0_0, S#0)
                         ==> _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, pth#0_1_1_0_0, n#0_1_1_0_0, S#0)
                           || (!_module.Path.Empty_q(pth#0_1_1_0_0)
                             ==> (var n#0_1_1_0_1 := _module.Path._h1(pth#0_1_1_0_0); 
                              (var prefix#0_1_1_0_0 := _module.Path._h0(pth#0_1_1_0_0); 
                                Seq#Contains($Unbox(read(old($Heap), n#0_1_1_0_1, _module.Node.children)): Seq, 
                                  $Box(n#0_1_1_0_0))))));
                    assert {:id "id687"} (var pth#0_1_1_0_0 := $Unbox(read($Heap, n#0_1_1_0_0, _module.Node.pathFromRoot)): DatatypeType; 
                      _module.__default.ReachableVia#canCall(old($Heap), root#0, pth#0_1_1_0_0, n#0_1_1_0_0, S#0)
                         ==> _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, pth#0_1_1_0_0, n#0_1_1_0_0, S#0)
                           || (!_module.Path.Empty_q(pth#0_1_1_0_0)
                             ==> (var n#0_1_1_0_1 := _module.Path._h1(pth#0_1_1_0_0); 
                              (var prefix#0_1_1_0_0 := _module.Path._h0(pth#0_1_1_0_0); 
                                _module.__default.ReachableVia($LS($LS($LZ)), old($Heap), root#0, prefix#0_1_1_0_0, n#0_1_1_0_1, S#0)))));
                    assume false;
                }
                else
                {
                    assume (forall n#0_1_1_0_3: ref :: 
                      { _module.__default.ReachableVia($LS($LZ), 
                          old($Heap), 
                          root#0, 
                          $Unbox(read(old($Heap), n#0_1_1_0_3, _module.Node.pathFromRoot)): DatatypeType, 
                          n#0_1_1_0_3, 
                          S#0) } 
                        { read($Heap, n#0_1_1_0_3, _module.Node.pathFromRoot) } 
                        { read($Heap, n#0_1_1_0_3, _module.Node.marked) } 
                        { Set#IsMember(S#0, $Box(n#0_1_1_0_3)) } 
                      $Is(n#0_1_1_0_3, Tclass._module.Node())
                         ==> 
                        Set#IsMember(S#0, $Box(n#0_1_1_0_3))
                           && $Unbox(read($Heap, n#0_1_1_0_3, _module.Node.marked)): bool
                         ==> (var pth#0_1_1_0_1 := $Unbox(read($Heap, n#0_1_1_0_3, _module.Node.pathFromRoot)): DatatypeType; 
                          $IsAllocBox($Box(pth#0_1_1_0_1), Tclass._module.Path(), old($Heap))
                             ==> _module.__default.ReachableVia#canCall(old($Heap), root#0, pth#0_1_1_0_1, n#0_1_1_0_3, S#0)));
                    assume (forall n#0_1_1_0_3: ref :: 
                      { _module.__default.ReachableVia($LS($LZ), 
                          old($Heap), 
                          root#0, 
                          $Unbox(read(old($Heap), n#0_1_1_0_3, _module.Node.pathFromRoot)): DatatypeType, 
                          n#0_1_1_0_3, 
                          S#0) } 
                        { read($Heap, n#0_1_1_0_3, _module.Node.pathFromRoot) } 
                        { read($Heap, n#0_1_1_0_3, _module.Node.marked) } 
                        { Set#IsMember(S#0, $Box(n#0_1_1_0_3)) } 
                      $Is(n#0_1_1_0_3, Tclass._module.Node())
                           && 
                          (Set#IsMember(S#0, $Box(n#0_1_1_0_3))
                           && $Unbox(read($Heap, n#0_1_1_0_3, _module.Node.marked)): bool)
                         ==> (var pth#0_1_1_0_1 := $Unbox(read($Heap, n#0_1_1_0_3, _module.Node.pathFromRoot)): DatatypeType; 
                          $IsAllocBox($Box(pth#0_1_1_0_1), Tclass._module.Path(), old($Heap))
                             && _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, pth#0_1_1_0_1, n#0_1_1_0_3, S#0)));
                }

                assume {:captureState "module:_module:state:61"} true;
            }
        }

        assume true;
        assume true;
        assume true;
        // ----- loop termination check ----- module:_module:statement:72
        assert {:id "id688"} (Set#Subset(unmarkedNodes#0, $decr$loop#00)
             && !Set#Subset($decr$loop#00, unmarkedNodes#0))
           || (Set#Subset(unmarkedNodes#0, $decr$loop#00)
             && (Seq#Rank(stackNodes#0) < Seq#Rank($decr$loop#01)
               || (Seq#Rank(stackNodes#0) <= Seq#Rank($decr$loop#01)
                 && 
                (0 <= $decr$loop#02
                 && Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq)
                     - $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
                   < $decr$loop#02))));
        assume (forall i#1: int :: 
            { Seq#Index(stackNodes#0, i#1) } 
            LitInt(0) <= i#1 && i#1 < Seq#Length(stackNodes#0)
               ==> Set#IsMember(S#0, Seq#Index(stackNodes#0, i#1)))
           ==> 
          Set#IsMember(S#0, $Box(t#0))
           ==> 
          !Seq#Contains(stackNodes#0, $Box(t#0))
           ==> 
          p#0
             == (if Seq#Length(stackNodes#0) == LitInt(0)
               then null
               else $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref)
           ==> 
          (forall n#11: ref :: 
            { read($Heap, n#11, _module.Node.children) } 
              { read($Heap, n#11, _module.Node.childrenVisited) } 
              { Seq#Contains(stackNodes#0, $Box(n#11)) } 
            $Is(n#11, Tclass._module.Node())
               ==> 
              Seq#Contains(stackNodes#0, $Box(n#11))
               ==> $Unbox(read($Heap, n#11, _module.Node.childrenVisited)): int
                 < Seq#Length($Unbox(read($Heap, n#11, _module.Node.children)): Seq))
           ==> 
          $Unbox(read($Heap, t#0, _module.Node.childrenVisited)): int
             <= Seq#Length($Unbox(read($Heap, t#0, _module.Node.children)): Seq)
           ==> 
          (forall n#13: ref :: 
            { read($Heap, n#13, _module.Node.childrenVisited) } 
              { Seq#Contains(stackNodes#0, $Box(n#13)) } 
              { Set#IsMember(S#0, $Box(n#13)) } 
            $Is(n#13, Tclass._module.Node())
               ==> 
              Set#IsMember(S#0, $Box(n#13))
                 && !Seq#Contains(stackNodes#0, $Box(n#13))
                 && n#13 != t#0
               ==> $Unbox(read($Heap, n#13, _module.Node.childrenVisited)): int == LitInt(0))
           ==> 
          (forall n#15: ref :: 
            { read($Heap, n#15, _module.Node.childrenVisited) } 
              { read(old($Heap), n#15, _module.Node.children) } 
              { read($Heap, n#15, _module.Node.children) } 
              { Seq#Contains(stackNodes#0, $Box(n#15)) } 
            $Is(n#15, Tclass._module.Node())
               ==> (Seq#Contains(stackNodes#0, $Box(n#15))
                   ==> Seq#Length($Unbox(read($Heap, n#15, _module.Node.children)): Seq)
                     == Seq#Length($Unbox(read(old($Heap), n#15, _module.Node.children)): Seq))
                 && (Seq#Contains(stackNodes#0, $Box(n#15))
                   ==> (forall j#2: int :: 
                    { Seq#Index($Unbox(read(old($Heap), n#15, _module.Node.children)): Seq, j#2) } 
                      { Seq#Index($Unbox(read($Heap, n#15, _module.Node.children)): Seq, j#2) } 
                    LitInt(0) <= j#2
                         && j#2 < Seq#Length($Unbox(read($Heap, n#15, _module.Node.children)): Seq)
                       ==> j#2 == $Unbox(read($Heap, n#15, _module.Node.childrenVisited)): int
                         || $Unbox(Seq#Index($Unbox(read($Heap, n#15, _module.Node.children)): Seq, j#2)): ref
                           == $Unbox(Seq#Index($Unbox(read(old($Heap), n#15, _module.Node.children)): Seq, j#2)): ref)))
           ==> 
          (forall n#17: ref :: 
            { read(old($Heap), n#17, _module.Node.children) } 
              { read($Heap, n#17, _module.Node.children) } 
              { Seq#Contains(stackNodes#0, $Box(n#17)) } 
              { Set#IsMember(S#0, $Box(n#17)) } 
            $Is(n#17, Tclass._module.Node())
               ==> 
              Set#IsMember(S#0, $Box(n#17)) && !Seq#Contains(stackNodes#0, $Box(n#17))
               ==> Seq#Equal($Unbox(read($Heap, n#17, _module.Node.children)): Seq, 
                $Unbox(read(old($Heap), n#17, _module.Node.children)): Seq))
           ==> 
          (0 < Seq#Length(stackNodes#0)
           ==> $Unbox(Seq#Index($Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, LitInt(0))): ref, _module.Node.children)): Seq, 
                $Unbox(read($Heap, 
                    $Unbox(Seq#Index(stackNodes#0, LitInt(0))): ref, 
                    _module.Node.childrenVisited)): int)): ref
             == null)
           ==> 
          (forall k#1: int :: 
            { read($Heap, $Unbox(Seq#Index(stackNodes#0, k#1)): ref, _module.Node.childrenVisited) } 
              { read($Heap, $Unbox(Seq#Index(stackNodes#0, k#1)): ref, _module.Node.children) } 
            0 < k#1 && k#1 < Seq#Length(stackNodes#0)
               ==> $Unbox(Seq#Index($Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, k#1)): ref, _module.Node.children)): Seq, 
                    $Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, k#1)): ref, _module.Node.childrenVisited)): int)): ref
                 == $Unbox(Seq#Index(stackNodes#0, k#1 - 1)): ref)
           ==> 
          (forall k#3: int :: 
            { read($Heap, $Unbox(Seq#Index(stackNodes#0, k#3)): ref, _module.Node.childrenVisited) } 
              { Seq#Index(stackNodes#0, k#3) } 
            LitInt(0) <= k#3 && k#3 < Seq#Length(stackNodes#0) - 1
               ==> $Unbox(Seq#Index($Unbox(read(old($Heap), $Unbox(Seq#Index(stackNodes#0, k#3)): ref, _module.Node.children)): Seq, 
                    $Unbox(read($Heap, $Unbox(Seq#Index(stackNodes#0, k#3)): ref, _module.Node.childrenVisited)): int)): ref
                 == $Unbox(Seq#Index(stackNodes#0, k#3 + 1)): ref)
           ==> 
          (0 < Seq#Length(stackNodes#0)
           ==> $Unbox(Seq#Index($Unbox(read(old($Heap), 
                    $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                    _module.Node.children)): Seq, 
                $Unbox(read($Heap, 
                    $Unbox(Seq#Index(stackNodes#0, Seq#Length(stackNodes#0) - 1)): ref, 
                    _module.Node.childrenVisited)): int)): ref
             == t#0)
           ==> 
          $Unbox(read($Heap, root#0, _module.Node.marked)): bool
           ==> 
          (forall n#19: ref :: 
            { read($Heap, n#19, _module.Node.children) } 
              { Seq#Contains(stackNodes#0, $Box(n#19)) } 
              { read($Heap, n#19, _module.Node.marked) } 
              { Set#IsMember(S#0, $Box(n#19)) } 
            $Is(n#19, Tclass._module.Node())
               ==> 
              Set#IsMember(S#0, $Box(n#19))
                 && $Unbox(read($Heap, n#19, _module.Node.marked)): bool
                 && !Seq#Contains(stackNodes#0, $Box(n#19))
                 && n#19 != t#0
               ==> (forall ch#8: ref :: 
                { read($Heap, ch#8, _module.Node.marked) } 
                  { Seq#Contains($Unbox(read($Heap, n#19, _module.Node.children)): Seq, $Box(ch#8)) } 
                $Is(ch#8, Tclass._module.Node?())
                   ==> 
                  Seq#Contains($Unbox(read($Heap, n#19, _module.Node.children)): Seq, $Box(ch#8))
                     && ch#8 != null
                   ==> $Unbox(read($Heap, ch#8, _module.Node.marked)): bool))
           ==> 
          (forall n#21: ref :: 
              { read($Heap, n#21, _module.Node.marked) } 
                { Seq#Contains(stackNodes#0, $Box(n#21)) } 
              $Is(n#21, Tclass._module.Node()) && $IsAlloc(n#21, Tclass._module.Node(), $Heap)
                 ==> 
                Seq#Contains(stackNodes#0, $Box(n#21)) || n#21 == t#0
                 ==> $Unbox(read($Heap, n#21, _module.Node.marked)): bool)
             && (forall n#21: ref :: 
              { read($Heap, n#21, _module.Node.children) } 
                { read($Heap, n#21, _module.Node.childrenVisited) } 
                { Seq#Contains(stackNodes#0, $Box(n#21)) } 
              $Is(n#21, Tclass._module.Node()) && $IsAlloc(n#21, Tclass._module.Node(), $Heap)
                 ==> 
                Seq#Contains(stackNodes#0, $Box(n#21)) || n#21 == t#0
                 ==> (forall j#5: int :: 
                  { Seq#Index($Unbox(read($Heap, n#21, _module.Node.children)): Seq, j#5) } 
                  LitInt(0) <= j#5
                       && j#5 < $Unbox(read($Heap, n#21, _module.Node.childrenVisited)): int
                     ==> $Unbox(Seq#Index($Unbox(read($Heap, n#21, _module.Node.children)): Seq, j#5)): ref
                         == null
                       || $Unbox(read($Heap, 
                          $Unbox(Seq#Index($Unbox(read($Heap, n#21, _module.Node.children)): Seq, j#5)): ref, 
                          _module.Node.marked)): bool))
           ==> 
          $IsAllocBox($Box(path#0), Tclass._module.Path(), old($Heap))
           ==> _module.__default.ReachableVia#canCall(old($Heap), root#0, path#0, t#0, S#0)
             && (_module.__default.ReachableVia($LS($LZ), old($Heap), root#0, path#0, t#0, S#0)
               ==> (forall n#25: ref :: 
                  { _module.__default.ReachableVia($LS($LZ), 
                      old($Heap), 
                      root#0, 
                      $Unbox(read(old($Heap), n#25, _module.Node.pathFromRoot)): DatatypeType, 
                      n#25, 
                      S#0) } 
                    { read($Heap, n#25, _module.Node.pathFromRoot) } 
                    { read($Heap, n#25, _module.Node.marked) } 
                    { Set#IsMember(S#0, $Box(n#25)) } 
                  $Is(n#25, Tclass._module.Node())
                     ==> 
                    Set#IsMember(S#0, $Box(n#25))
                     ==> 
                    $Unbox(read($Heap, n#25, _module.Node.marked)): bool
                     ==> (var pth#0 := $Unbox(read($Heap, n#25, _module.Node.pathFromRoot)): DatatypeType; 
                      $IsAllocBox($Box(pth#0), Tclass._module.Path(), old($Heap))
                         ==> _module.__default.ReachableVia#canCall(old($Heap), root#0, pth#0, n#25, S#0)))
                 && ((forall n#25: ref :: 
                    { _module.__default.ReachableVia($LS($LZ), 
                        old($Heap), 
                        root#0, 
                        $Unbox(read(old($Heap), n#25, _module.Node.pathFromRoot)): DatatypeType, 
                        n#25, 
                        S#0) } 
                      { read($Heap, n#25, _module.Node.pathFromRoot) } 
                      { read($Heap, n#25, _module.Node.marked) } 
                      { Set#IsMember(S#0, $Box(n#25)) } 
                    $Is(n#25, Tclass._module.Node())
                       ==> 
                      Set#IsMember(S#0, $Box(n#25))
                         && $Unbox(read($Heap, n#25, _module.Node.marked)): bool
                       ==> (var pth#0 := $Unbox(read($Heap, n#25, _module.Node.pathFromRoot)): DatatypeType; 
                        $IsAllocBox($Box(pth#0), Tclass._module.Path(), old($Heap))
                           && _module.__default.ReachableVia($LS($LZ), old($Heap), root#0, pth#0, n#25, S#0)))
                   ==> (forall n#27: ref :: 
                    { _module.__default.Reachable(old($Heap), root#0, n#27, S#0) } 
                      { read($Heap, n#27, _module.Node.marked) } 
                      { Set#IsMember(S#0, $Box(n#27)) } 
                    $Is(n#27, Tclass._module.Node())
                       ==> 
                      Set#IsMember(S#0, $Box(n#27))
                       ==> 
                      $Unbox(read($Heap, n#27, _module.Node.marked)): bool
                       ==> _module.__default.Reachable#canCall(old($Heap), root#0, n#27, S#0))));
    }
}



const unique class._module.Node?: ClassName;

// $Is axiom for class Node
axiom (forall $o: ref :: 
  { $Is($o, Tclass._module.Node?()) } 
  $Is($o, Tclass._module.Node?())
     <==> $o == null || dtype($o) == Tclass._module.Node?());

// $IsAlloc axiom for class Node
axiom (forall $o: ref, $h: Heap :: 
  { $IsAlloc($o, Tclass._module.Node?(), $h) } 
  $IsAlloc($o, Tclass._module.Node?(), $h)
     <==> $o == null || $Unbox(read($h, $o, alloc)): bool);

const _module.Node.children: Field
uses {
axiom FDim(_module.Node.children) == 0
   && FieldOfDecl(class._module.Node?, field$children) == _module.Node.children
   && !$IsGhostField(_module.Node.children);
}

// Node.children: Type axiom
axiom (forall $h: Heap, $o: ref :: 
  { $Unbox(read($h, $o, _module.Node.children)): Seq } 
  $IsGoodHeap($h) && ($o != null && dtype($o) == Tclass._module.Node?())
     ==> $Is($Unbox(read($h, $o, _module.Node.children)): Seq, TSeq(Tclass._module.Node?()))
       && $IsBox(read($h, $o, _module.Node.children), TSeq(Tclass._module.Node?())));

// Node.children: Allocation axiom
axiom (forall $h: Heap, $o: ref :: 
  { $Unbox(read($h, $o, _module.Node.children)): Seq } 
  $IsGoodHeap($h)
       && 
      ($o != null
       && dtype($o) == Tclass._module.Node?())
       && $Unbox(read($h, $o, alloc)): bool
     ==> $IsAlloc($Unbox(read($h, $o, _module.Node.children)): Seq, 
      TSeq(Tclass._module.Node?()), 
      $h));

const _module.Node.marked: Field
uses {
axiom FDim(_module.Node.marked) == 0
   && FieldOfDecl(class._module.Node?, field$marked) == _module.Node.marked
   && !$IsGhostField(_module.Node.marked);
}

// Node.marked: Type axiom
axiom (forall $h: Heap, $o: ref :: 
  { $Unbox(read($h, $o, _module.Node.marked)): bool } 
  $IsGoodHeap($h) && ($o != null && dtype($o) == Tclass._module.Node?())
     ==> $Is($Unbox(read($h, $o, _module.Node.marked)): bool, TBool)
       && $IsBox(read($h, $o, _module.Node.marked), TBool));

// Node.marked: Allocation axiom
axiom (forall $h: Heap, $o: ref :: 
  { $Unbox(read($h, $o, _module.Node.marked)): bool } 
  $IsGoodHeap($h)
       && 
      ($o != null
       && dtype($o) == Tclass._module.Node?())
       && $Unbox(read($h, $o, alloc)): bool
     ==> $IsAlloc($Unbox(read($h, $o, _module.Node.marked)): bool, TBool, $h));

const _module.Node.childrenVisited: Field
uses {
axiom FDim(_module.Node.childrenVisited) == 0
   && FieldOfDecl(class._module.Node?, field$childrenVisited)
     == _module.Node.childrenVisited
   && !$IsGhostField(_module.Node.childrenVisited);
}

// Node.childrenVisited: Type axiom
axiom (forall $h: Heap, $o: ref :: 
  { $Unbox(read($h, $o, _module.Node.childrenVisited)): int } 
  $IsGoodHeap($h) && ($o != null && dtype($o) == Tclass._module.Node?())
     ==> $Is($Unbox(read($h, $o, _module.Node.childrenVisited)): int, Tclass._System.nat())
       && $IsBox(read($h, $o, _module.Node.childrenVisited), Tclass._System.nat()));

// Node.childrenVisited: Allocation axiom
axiom (forall $h: Heap, $o: ref :: 
  { $Unbox(read($h, $o, _module.Node.childrenVisited)): int } 
  $IsGoodHeap($h)
       && 
      ($o != null
       && dtype($o) == Tclass._module.Node?())
       && $Unbox(read($h, $o, alloc)): bool
     ==> $IsAlloc($Unbox(read($h, $o, _module.Node.childrenVisited)): int, 
      Tclass._System.nat(), 
      $h));

const _module.Node.pathFromRoot: Field
uses {
axiom FDim(_module.Node.pathFromRoot) == 0
   && FieldOfDecl(class._module.Node?, field$pathFromRoot)
     == _module.Node.pathFromRoot
   && $IsGhostField(_module.Node.pathFromRoot);
}

// Node.pathFromRoot: Type axiom
axiom (forall $h: Heap, $o: ref :: 
  { $Unbox(read($h, $o, _module.Node.pathFromRoot)): DatatypeType } 
  $IsGoodHeap($h) && ($o != null && dtype($o) == Tclass._module.Node?())
     ==> $Is($Unbox(read($h, $o, _module.Node.pathFromRoot)): DatatypeType, 
        Tclass._module.Path())
       && $IsBox(read($h, $o, _module.Node.pathFromRoot), Tclass._module.Path()));

// Node.pathFromRoot: Allocation axiom
axiom (forall $h: Heap, $o: ref :: 
  { $Unbox(read($h, $o, _module.Node.pathFromRoot)): DatatypeType } 
  $IsGoodHeap($h)
       && 
      ($o != null
       && dtype($o) == Tclass._module.Node?())
       && $Unbox(read($h, $o, alloc)): bool
     ==> $IsAlloc($Unbox(read($h, $o, _module.Node.pathFromRoot)): DatatypeType, 
      Tclass._module.Path(), 
      $h));

// $Is axiom for non-null type _module.Node
axiom (forall c#0: ref :: 
  { $Is(c#0, Tclass._module.Node()) } { $Is(c#0, Tclass._module.Node?()) } 
  $Is(c#0, Tclass._module.Node())
     <==> $Is(c#0, Tclass._module.Node?()) && c#0 != null);

// $IsAlloc axiom for non-null type _module.Node
axiom (forall c#0: ref, $h: Heap :: 
  { $IsAlloc(c#0, Tclass._module.Node(), $h) } 
  $IsAlloc(c#0, Tclass._module.Node(), $h)
     <==> $IsAlloc(c#0, Tclass._module.Node?(), $h));

// Constructor function declaration
function #_module.Path.Empty() : DatatypeType
uses {
// Constructor identifier
axiom DatatypeCtorId(#_module.Path.Empty()) == ##_module.Path.Empty;
// Constructor $Is
axiom $Is(#_module.Path.Empty(), Tclass._module.Path());
// Constructor literal
axiom #_module.Path.Empty() == Lit(#_module.Path.Empty());
}

const unique ##_module.Path.Empty: DtCtorId
uses {
// Constructor identifier
axiom DatatypeCtorId(#_module.Path.Empty()) == ##_module.Path.Empty;
}

function _module.Path.Empty_q(DatatypeType) : bool;

// Questionmark and identifier
axiom (forall d: DatatypeType :: 
  { _module.Path.Empty_q(d) } 
  _module.Path.Empty_q(d) <==> DatatypeCtorId(d) == ##_module.Path.Empty);

// Constructor questionmark has arguments
axiom (forall d: DatatypeType :: 
  { _module.Path.Empty_q(d) } 
  _module.Path.Empty_q(d) ==> d == #_module.Path.Empty());

// Constructor $IsAlloc
axiom (forall $h: Heap :: 
  { $IsAlloc(#_module.Path.Empty(), Tclass._module.Path(), $h) } 
  $IsGoodHeap($h) ==> $IsAlloc(#_module.Path.Empty(), Tclass._module.Path(), $h));

// Constructor function declaration
function #_module.Path.Extend(DatatypeType, ref) : DatatypeType;

const unique ##_module.Path.Extend: DtCtorId
uses {
// Constructor identifier
axiom (forall a#4#0#0: DatatypeType, a#4#1#0: ref :: 
  { #_module.Path.Extend(a#4#0#0, a#4#1#0) } 
  DatatypeCtorId(#_module.Path.Extend(a#4#0#0, a#4#1#0)) == ##_module.Path.Extend);
}

function _module.Path.Extend_q(DatatypeType) : bool;

// Questionmark and identifier
axiom (forall d: DatatypeType :: 
  { _module.Path.Extend_q(d) } 
  _module.Path.Extend_q(d) <==> DatatypeCtorId(d) == ##_module.Path.Extend);

// Constructor questionmark has arguments
axiom (forall d: DatatypeType :: 
  { _module.Path.Extend_q(d) } 
  _module.Path.Extend_q(d)
     ==> (exists a#5#0#0: DatatypeType, a#5#1#0: ref :: 
      d == #_module.Path.Extend(a#5#0#0, a#5#1#0)));

// Constructor $Is
axiom (forall a#6#0#0: DatatypeType, a#6#1#0: ref :: 
  { $Is(#_module.Path.Extend(a#6#0#0, a#6#1#0), Tclass._module.Path()) } 
  $Is(#_module.Path.Extend(a#6#0#0, a#6#1#0), Tclass._module.Path())
     <==> $Is(a#6#0#0, Tclass._module.Path()) && $Is(a#6#1#0, Tclass._module.Node()));

// Constructor $IsAlloc
axiom (forall a#6#0#0: DatatypeType, a#6#1#0: ref, $h: Heap :: 
  { $IsAlloc(#_module.Path.Extend(a#6#0#0, a#6#1#0), Tclass._module.Path(), $h) } 
  $IsGoodHeap($h)
     ==> ($IsAlloc(#_module.Path.Extend(a#6#0#0, a#6#1#0), Tclass._module.Path(), $h)
       <==> $IsAlloc(a#6#0#0, Tclass._module.Path(), $h)
         && $IsAlloc(a#6#1#0, Tclass._module.Node(), $h)));

// Destructor $IsAlloc
axiom (forall d: DatatypeType, $h: Heap :: 
  { $IsAlloc(_module.Path._h0(d), Tclass._module.Path(), $h) } 
  $IsGoodHeap($h)
       && 
      (_module.Path.Extend_q(d)
       && $IsAlloc(d, Tclass._module.Path(), $h))
     ==> $IsAlloc(_module.Path._h0(d), Tclass._module.Path(), $h));

// Destructor $IsAlloc
axiom (forall d: DatatypeType, $h: Heap :: 
  { $IsAlloc(_module.Path._h1(d), Tclass._module.Node(), $h) } 
  $IsGoodHeap($h)
       && 
      (_module.Path.Extend_q(d)
       && $IsAlloc(d, Tclass._module.Path(), $h))
     ==> $IsAlloc(_module.Path._h1(d), Tclass._module.Node(), $h));

// Constructor literal
axiom (forall a#7#0#0: DatatypeType, a#7#1#0: ref :: 
  { #_module.Path.Extend(Lit(a#7#0#0), Lit(a#7#1#0)) } 
  #_module.Path.Extend(Lit(a#7#0#0), Lit(a#7#1#0))
     == Lit(#_module.Path.Extend(a#7#0#0, a#7#1#0)));

function _module.Path._h0(DatatypeType) : DatatypeType;

// Constructor injectivity
axiom (forall a#8#0#0: DatatypeType, a#8#1#0: ref :: 
  { #_module.Path.Extend(a#8#0#0, a#8#1#0) } 
  _module.Path._h0(#_module.Path.Extend(a#8#0#0, a#8#1#0)) == a#8#0#0);

// Inductive rank
axiom (forall a#9#0#0: DatatypeType, a#9#1#0: ref :: 
  { DtRank(#_module.Path.Extend(a#9#0#0, a#9#1#0)) } 
  DtRank(a#9#0#0) < DtRank(#_module.Path.Extend(a#9#0#0, a#9#1#0)));

function _module.Path._h1(DatatypeType) : ref;

// Constructor injectivity
axiom (forall a#10#0#0: DatatypeType, a#10#1#0: ref :: 
  { #_module.Path.Extend(a#10#0#0, a#10#1#0) } 
  _module.Path._h1(#_module.Path.Extend(a#10#0#0, a#10#1#0)) == a#10#1#0);

// Depth-one case-split function
function $IsA#_module.Path(DatatypeType) : bool;

// Depth-one case-split axiom
axiom (forall d: DatatypeType :: 
  { $IsA#_module.Path(d) } 
  $IsA#_module.Path(d) ==> _module.Path.Empty_q(d) || _module.Path.Extend_q(d));

// Questionmark data type disjunctivity
axiom (forall d: DatatypeType :: 
  { _module.Path.Extend_q(d), $Is(d, Tclass._module.Path()) } 
    { _module.Path.Empty_q(d), $Is(d, Tclass._module.Path()) } 
  $Is(d, Tclass._module.Path())
     ==> _module.Path.Empty_q(d) || _module.Path.Extend_q(d));

// Datatype extensional equality declaration
function _module.Path#Equal(DatatypeType, DatatypeType) : bool;

// Datatype extensional equality definition: #_module.Path.Empty
axiom (forall a: DatatypeType, b: DatatypeType :: 
  { _module.Path#Equal(a, b), _module.Path.Empty_q(a) } 
    { _module.Path#Equal(a, b), _module.Path.Empty_q(b) } 
  _module.Path.Empty_q(a) && _module.Path.Empty_q(b) ==> _module.Path#Equal(a, b));

// Datatype extensional equality definition: #_module.Path.Extend
axiom (forall a: DatatypeType, b: DatatypeType :: 
  { _module.Path#Equal(a, b), _module.Path.Extend_q(a) } 
    { _module.Path#Equal(a, b), _module.Path.Extend_q(b) } 
  _module.Path.Extend_q(a) && _module.Path.Extend_q(b)
     ==> (_module.Path#Equal(a, b)
       <==> _module.Path#Equal(_module.Path._h0(a), _module.Path._h0(b))
         && _module.Path._h1(a) == _module.Path._h1(b)));

// Datatype extensionality axiom: _module.Path
axiom (forall a: DatatypeType, b: DatatypeType :: 
  { _module.Path#Equal(a, b) } 
  _module.Path#Equal(a, b) <==> a == b);

const unique class._module.Path: ClassName;

const unique tytagFamily$_#Func0: TyTagFamily;

const unique tytagFamily$_#Func1: TyTagFamily;

const unique tytagFamily$_#Func3: TyTagFamily;

const unique tytagFamily$_#Func4: TyTagFamily;

const unique tytagFamily$_#PartialFunc0: TyTagFamily;

const unique tytagFamily$_#PartialFunc1: TyTagFamily;

const unique tytagFamily$_#PartialFunc3: TyTagFamily;

const unique tytagFamily$_#PartialFunc4: TyTagFamily;

const unique tytagFamily$_#TotalFunc0: TyTagFamily;

const unique tytagFamily$_#TotalFunc1: TyTagFamily;

const unique tytagFamily$_#TotalFunc3: TyTagFamily;

const unique tytagFamily$_#TotalFunc4: TyTagFamily;

const unique tytagFamily$_tuple#0: TyTagFamily;

const unique tytagFamily$_tuple#2: TyTagFamily;

const unique tytagFamily$array: TyTagFamily;

const unique tytagFamily$nat: TyTagFamily;

const unique tytagFamily$object: TyTagFamily;

const unique tytagFamily$Node: TyTagFamily;

const unique tytagFamily$Path: TyTagFamily;

const unique field$children: NameFamily;

const unique field$marked: NameFamily;

const unique field$childrenVisited: NameFamily;

const unique field$pathFromRoot: NameFamily;
