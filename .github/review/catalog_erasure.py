"""Check executable backend output without C#'s retained Dafny source metadata."""
from pathlib import Path
import re


CSHARP_SOURCE_ATTRIBUTE = re.compile(
    r'\[assembly:\s*DafnyAssembly\.DafnySourceAttribute\(@"(?:[^"]|"")*"\)\]')
GHOST_FIELDS = ('allowance', 'producedAllowanceCeiling', 'maximumProduced')


def check_ghost_erasure(path, target):
    path = Path(path)
    text = path.read_text(errors='replace')
    if target == 'cs' and path.suffix == '.cs':
        # The C# backend serializes the original program, including its ghost code,
        # in this assembly attribute. Keep checking all code outside that metadata.
        text = CSHARP_SOURCE_ATTRIBUTE.sub('', text)
    for field in GHOST_FIELDS:
        assert field not in text, str(path) + ': ghost evidence survived compilation'
