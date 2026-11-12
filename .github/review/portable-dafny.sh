#!/bin/sh
# The framework-dependent CI artifact is shared across operating systems.
set -eu
cat > "$1/Dafny" <<'LAUNCHER'
#!/bin/sh
exec dotnet "$(dirname "$0")/Dafny.dll" "$@"
LAUNCHER
chmod +x "$1/Dafny"
