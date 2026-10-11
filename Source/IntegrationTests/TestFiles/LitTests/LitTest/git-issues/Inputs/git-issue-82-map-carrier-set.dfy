// Finite map domains do not make the carrier of whole map values finite.
ghost function InvalidMapCarrierSet(): set<map<bool, int>> {
  set m: map<bool, int> | true
}
