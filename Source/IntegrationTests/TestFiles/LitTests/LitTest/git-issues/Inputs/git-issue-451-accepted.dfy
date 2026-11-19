// A single replacement remains accepted.
replaceable module Target {}
module Implementation replaces Target {}
module Client {
  import Target
  method Check() { assert 1 + 1 == 2; }
}
