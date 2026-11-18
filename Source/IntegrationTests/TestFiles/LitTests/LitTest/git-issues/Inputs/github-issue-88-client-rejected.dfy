// A client of github-issue-88-library.dfy that adds a library trait as a parent without
// @AssumeCrossModuleTermination: the library trait is not open to extension from another module.

module App0 {
  trait T extends object {}
}
module App1 refines App0 {
  import LibTraits
  trait T ... extends LibTraits.Named {} // error: LibTraits.Named is in another module
}
