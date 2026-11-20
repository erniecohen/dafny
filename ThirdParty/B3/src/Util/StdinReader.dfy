module StdinReader {
  import opened Std.Wrappers

  export
    provides ReadStdin
    provides Wrappers

  method {:extern} ReadStdin() returns (r: Result<string, string>)
}
