// A concrete module cannot depend on an abstract sibling, even through a plain import.
abstract module Abstract {
  import Concrete
  type T
}
module Concrete {
  import Abstract
}
