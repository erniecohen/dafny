// Match sources that propagation alone does not determine.  With
// --conservative-match-inference the resolver falls back to the solve with decisions,
// as it always did, so these resolve in every mode.
datatype Option<+U> = None | Some(val: U)

// The source is a literal: its type is int by default advice.
function LiteralSource(): int {
  match 1
  case 1 => 10
  case _ => 20
}

// The source is a constructor application with a literal argument.
function ConstructorSource(): int {
  var d := Some(0);
  match d
  case None => 0
  case Some(n) => n + 1
}

// The source is a pair of literals.
function TupleSource(): int {
  match (1, true)
  case (n, true) => n
  case (_, false) => 0
}
