// Both operators require a nonzero divisor, even for an unsigned type.
function DivisionByZero(x: bv8): bv8 {
  x / 0
}

function RemainderByZero(x: bv8): bv8 {
  x % 0
}
