// Legacy control: adding a library trait as a parent without @AssumeCrossModuleTermination
// is rejected under --legacy-no-modular-termination; revision 3 permits this bodyless subtrait.

module App0 {
  trait T extends object {}
}
module App1 refines App0 {
  import LibTraits
  trait T ... extends LibTraits.Named {} // error: LibTraits.Named is in another module
}
