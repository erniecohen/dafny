// A newtype over an arrow type, under --general-newtypes: resolution rejects it.

newtype Kids<T> = f: nat -> T | true witness *
datatype W = WLeaf | WNode(k: Kids<W>)
function WSize(w: W): nat {
  if w.WLeaf? then 0 else WSize((w.k as nat -> W)(0)) + 1
}
