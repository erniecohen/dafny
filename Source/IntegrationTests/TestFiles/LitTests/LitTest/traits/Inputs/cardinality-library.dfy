module Api { trait {:termination false} V {} }
module Storage {
  import A = Api
  type F = A.V -> bool
  datatype B = B(f: F)
}
