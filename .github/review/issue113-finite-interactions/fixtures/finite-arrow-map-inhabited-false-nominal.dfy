// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"

class Cell { constructor() {} }
newtype Mapper<A,B> = A -> B witness *
newtype MapMaker<T> = bool -> map<int,T> witness *

ghost function BuildMap<A,B>(f:Mapper<A,B>, value:A):MapMaker<B> {
  ((flag:bool) => (map x:int | x == (if flag then 0 else 1) :: 0 := f(value))) as MapMaker<B>
}

lemma MapContract<A,B>(f:Mapper<A,B>, value:A, flag:bool)
  ensures BuildMap(f,value)(flag).Keys == {0}
  ensures BuildMap(f,value)(flag)[0] == f(value)
{
  var witness := if flag then 0 else 1;
  assert witness == (if flag then 0 else 1);
  var m := BuildMap(f,value)(flag);
  assert m.Keys == {0};
  assert m[0] == f(value);
}

lemma SeparateEnvironments<A,B>(f:Mapper<A,B>, g:Mapper<A,B>, a:A, b:A, flag:bool)
  ensures BuildMap(f,a)(flag)[0] == f(a)
  ensures BuildMap(g,b)(flag)[0] == g(b)
{
  MapContract(f,a,flag);
  MapContract(g,b,flag);
}

method PreallocatedFiniteCapture()
{
  var c := new Cell();
  label Before:
  ghost var f := ((x:Cell) => x) as Mapper<Cell,Cell>;
  ghost var maker := BuildMap(f,c);
  MapContract(f,c,true);
  assert old@Before(allocated(c));
  assert old@Before(allocated(maker));
  assert maker(true)[0] == c;
}

method FreshFiniteCapture()
{
  label Before:
  var c := new Cell();
  ghost var f := ((x:Cell) => x) as Mapper<Cell,Cell>;
  ghost var maker := BuildMap(f,c);
  MapContract(f,c,true);
  assert maker(true)[0] == c;
  assert !old@Before(allocated(c));
  assert false;
}
