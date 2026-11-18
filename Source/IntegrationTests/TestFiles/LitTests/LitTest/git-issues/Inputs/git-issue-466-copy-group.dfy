// Copying is refinement, so copying a group member remains unsupported in stages 1 to 3.
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
