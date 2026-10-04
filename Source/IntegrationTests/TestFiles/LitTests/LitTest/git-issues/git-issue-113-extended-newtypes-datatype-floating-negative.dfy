// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype Box<T> = Box(value: T)
newtype F32 = Box<fp32> witness *
newtype F64 = Box<fp64> witness *
newtype Nested = Box<Box<fp32>> witness *
newtype Id<T> = T witness *
newtype Repeated = Box<Id<Id<fp32>>> witness *
newtype Permuted<X,Y> = p: (Y, X) | true witness *
datatype Container = Container(value: F64)

method Compare32(x: F32) { var b := x == x; }
method Compare64(x: F64) { var b := x == x; }
method CompareNested(x: Nested) { var b := x == x; }
method CompareRepeated(x: Repeated) { var b := x == x; }
method ComparePermuted(x: Permuted<int,fp32>) { var b := x == x; }
method CompareField(x: Container) { var b := x == x; }
