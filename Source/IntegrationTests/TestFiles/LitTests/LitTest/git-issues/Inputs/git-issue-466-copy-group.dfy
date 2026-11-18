// Copying refines one member and keeps its references to the original siblings.
module Left {
  import Right
  function F(): int { 1 }
}
module Right {
  import Left
  function G(): int { Left.F() }
}
module Outside {
  import Copy := Left
}
