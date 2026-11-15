// The abstract type is covariant.  The synonym keeps '+', but its target is contravariant.
module ACo { type O<+X> { function F(): int } }
module BTarget refines ACo {
  datatype D<-X> = D(f: X -> int) { function F(): int { 0 } }
  type O<+X> = D<X>
}
// The synonym itself changes the variance.
module BSynonym refines ACo {
  datatype D<-X> = D(f: X -> int) { function F(): int { 0 } }
  type O<-X> = D<X>
}
