// https://github.com/erniecohen/dafny/issues/309
// A match pattern that binds a variable whose given type leaves a type argument to inference, which
// cannot infer it, crashed the refreshed resolver. Both resolvers report the underspecified type.
// RUN: echo "Legacy resolver" > "%t"
// RUN: %exits-with 2 %baredafny resolve --standard-libraries:false --use-basename-for-filename --show-snippets:false --type-system-refresh:false "%s" >> "%t"
// RUN: echo "Refreshed resolver" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --standard-libraries:false --use-basename-for-filename --show-snippets:false --type-system-refresh:true "%s" >> "%t"
// RUN: echo "Refreshed resolver, the program of the issue" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --standard-libraries:false --use-basename-for-filename --show-snippets:false --type-system-refresh:true --general-newtypes:false "%S/git-issue-3921.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Expression {
  class Clx { }
  type MyClx<U> = c: Clx | true witness *
  datatype D = DX | DY(o: Clx)

  ghost function F(d: D): int {
    match d
    case DX => 2
    case DY(o: MyClx) => 3 // error: type argument underspecified
  }
}

module Statement {
  class Clx { }
  type MyClx<U> = c: Clx | true witness *
  datatype D = DX | DY(o: Clx)

  method M(d: D) {
    match d
    case DX =>
    case DY(o: MyClx) => // error: type argument underspecified
  }
}

module Nested {
  type SubsetType<X> = x: int | true
  datatype Option<T> = None | Some(t: T)

  method M(r: Option<Option<int>>) {
    match r
    case Some(Some(s: SubsetType)) => // error: type argument underspecified
    case _ =>
  }
}

module Determined {
  class Clx { }
  type MyClx<U> = c: Clx | true witness *
  datatype D = DX | DY(o: Clx)

  ghost function F<U>(d: D): int {
    match d
    case DX => 2
    case DY(o: MyClx<U>) => 3
  }

  method M(d: D) {
    match d
    case DX =>
    case DY(o: MyClx<int>) =>
  }
}
