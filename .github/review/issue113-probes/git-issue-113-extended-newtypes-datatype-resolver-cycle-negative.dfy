// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes --extended-newtype-bases "%s" > "%t"

newtype Expanding<T> = Expanding<seq<T>> witness * // existing redirecting cycle rejection
