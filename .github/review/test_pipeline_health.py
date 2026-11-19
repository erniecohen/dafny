"""Keep the pipeline's regression tests in the standing Debug CI gate."""
from pathlib import Path
import re
import unittest


class PipelineHealthTests(unittest.TestCase):
    def test_pipeline_project_runs_with_contract_assertions(self):
        workflow = Path(__file__).parents[1] / 'workflows' / 'review.yml'
        self.assertRegex(workflow.read_text(), re.compile(
            r'^        run: dotnet test --settings \.github/review/ci\.runsettings '
            r'Source/DafnyPipeline\.Test -c Debug --logger "console;verbosity=normal"$', re.M))


if __name__ == '__main__':
    unittest.main()
