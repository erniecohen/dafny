// Private resolution-negative draft, isolated from compiled-type-test checks.
datatype Pair<A, B> = Pair(left: A, right: B)
newtype Permuted<X, Y> = Pair<Y, X> witness *

method DifferentActuals(p: Pair<bool, int>) returns (b: bool) {
  b := p is Permuted<bool, int>; // ERROR: base is Pair<int,bool>, not Pair<bool,int>
}
