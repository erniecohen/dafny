// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes --extended-newtype-bases "%s" > "%t"
// Unexecuted draft. The false predicate is an implicit postcondition, never a co-call assumption.

codatatype Stream = Cons(head: int, tail: Stream)
newtype Impossible = s: Stream | false witness *

function BadResult(): Impossible {
  Cons(0, BadResult() as Stream) as Impossible
}

type EmptySubset = s: Stream | false witness *
newtype OverSubset = EmptySubset witness *
function BadSubsetResult(): OverSubset {
  Cons(0, BadSubsetResult() as Stream) as OverSubset
}

newtype Raw = Stream witness *
function StrengtheningGuard(): Raw {
  Cons(0, (StrengtheningGuard() as Stream) as Impossible as Stream) as Raw
}

function ConstructorUnderRefinement(): Raw {
  ((Cons(0, ConstructorUnderRefinement() as Stream) as EmptySubset) as Stream) as Raw
  // The constructor inside a refining cast must not recreate a co-call guard.
}
