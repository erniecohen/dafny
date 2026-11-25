#!/usr/bin/env python3
"""Serial, offline controls for admission guards; never edits the checkout under test."""
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

HERE = Path(__file__).resolve().parent
ACTION = HERE.parent / 'actions/self-hosted-job'
# Each replacement removes or corrupts one decision. A selected assertion must fail,
# rather than treating a syntax/import error or timeout as a detected mutation.
MUTATIONS = [
    ('reserve equality', 'admit.py', ">= policy['reserve_mb']", "> policy['reserve_mb']", 'HeadroomTests.test_exact_reserve_boundary_and_one_mib_below'),
    ('reserve deficit', 'admit.py', "available - expected_mb - outstanding_mb >= policy['reserve_mb']", 'True', 'HeadroomTests.test_exact_reserve_boundary_and_one_mib_below'),
    ('critical pressure', 'admit.py', 'level in (1, 2)', 'level in (1, 2, 4)', 'HeadroomTests.test_normal_and_warn_admit_critical_and_unknown_refuse'),
    ('unknown pressure', 'admit.py', 'level in (1, 2)', 'True', 'HeadroomTests.test_normal_and_warn_admit_critical_and_unknown_refuse'),
    ('warn pressure', 'admit.py', 'level in (1, 2)', 'level == 1', 'HeadroomTests.test_normal_and_warn_admit_critical_and_unknown_refuse'),
    ('available units', 'admit.py', 'free * total / 100', 'free * total', 'HeadroomTests.test_each_missing_probe_refuses_and_percentage_is_scaled'),
    ('physical units', 'admit.py', 'int(text) / 1048576', 'int(text) / 1024', 'ProbeTests.test_commands_timeouts_units_and_parsing'),
    ('probe timeout', 'admit.py', 'text=True, timeout=timeout', 'text=True, timeout=999', 'ProbeTests.test_commands_timeouts_units_and_parsing'),
    ('failed probe', 'admit.py', 'if r.returncode == 0 else None', 'if True else None', 'ProbeTests.test_commands_timeouts_units_and_parsing'),
    ('free range', 'admit.py', '0 <= int(match[1]) <= 100', 'True', 'ProbeTests.test_commands_timeouts_units_and_parsing'),
    ('pressure parse', 'admit.py', "text in ('1', '2', '4')", "text in ('0', '1', '2', '3', '4')", 'ProbeTests.test_commands_timeouts_units_and_parsing'),
    ('positive policy', 'admit.py', 'or value <= 0:', 'or value < 0:', 'PolicyTests.test_positive_finite_numbers_and_integer_samples'),
    ('finite policy', 'admit.py', 'or not math.isfinite(value)', '', 'PolicyTests.test_positive_finite_numbers_and_integer_samples'),
    ('boolean policy', 'admit.py', 'type(value) not in (int, float)', 'not isinstance(value, (int, float))', 'PolicyTests.test_positive_finite_numbers_and_integer_samples'),
    ('sample integer', 'admit.py', 'positive_integer(value, name)', 'positive_number(value, name)', 'PolicyTests.test_positive_finite_numbers_and_integer_samples'),
    ('required policy', 'admit.py', 'for name in POLICY_KEYS:', 'for name in ():', 'PolicyTests.test_requires_every_policy_field'),
    ('caller priority', 'admit.py', 'if caller is not None:', 'if False:', 'PeakTests.test_caller_overrides_history_and_rejects_invalid_peak'),
    ('project group', 'admit.py', "if r.get('project') == project and r.get('kind') == kind", "if r.get('kind') == kind", 'PeakTests.test_project_kind_then_kind_across_projects'),
    ('kind fallback', 'admit.py', "elif project:", 'if project:', 'PeakTests.test_sparse_group_never_mixes_kinds_and_missing_history_uses_default'),
    ('sample minimum', 'admit.py', "len(values) >= policy['peak_min_samples']", 'len(values) >= 1', 'PeakTests.test_sparse_group_never_mixes_kinds_and_missing_history_uses_default'),
    ('quantile rounding', 'admit.py', 'round(0.95 * (len(values) - 1))', 'int(0.95 * (len(values) - 1))', 'PeakTests.test_dedup_rotations_and_round_to_even_quantile'),
    ('completed only', 'admit.py', "row.get('event') == 'end'", 'True', 'PeakTests.test_completed_positive_finite_recent_sampled_peaks_only'),
    ('history time', 'admit.py', 'since <= t <= now', 'True', 'PeakTests.test_completed_positive_finite_recent_sampled_peaks_only'),
    ('history samples', 'admit.py', 'samples > 0', 'True', 'PeakTests.test_completed_positive_finite_recent_sampled_peaks_only'),
    ('history positive', 'admit.py', 'math.isfinite(peak) and peak > 0', 'True', 'PeakTests.test_completed_positive_finite_recent_sampled_peaks_only'),
    ('history dedup', 'admit.py', "ended[row['key']] = row", "ended[str(len(ended))] = row", 'PeakTests.test_dedup_rotations_and_round_to_even_quantile'),
    ('history rotation order', 'admit.py', 'rotations + [path]', '[path] + rotations', 'PeakTests.test_current_file_wins_over_rotated_duplicate'),
    ('growth predicate', 'admit.py', 'available - expected_mb - outstanding_mb', 'available - expected_mb', 'HeadroomTests.test_two_low_rss_holders_refuse_third_despite_incoming_headroom'),
    ('growth admission consistency', 'admit.py', 'peak = expected', 'peak = max(amount, expected)', 'HeadroomTests.test_growth_charges_the_same_peak_used_at_admission'),
    ('flock holder PID', 'admit.py', "data = {'pid': os.getpid(),", "data = {'pid': owner_pid,", 'test_admission_budget.ProcessTests.test_orphaned_keeper_records_worker_and_credits_worker_tree'),
    ('orphaned keeper owner', 'admit.py', "'owner_pid': owner_pid", "'owner_pid': os.getpid()", 'test_admission_budget.ProcessTests.test_orphaned_keeper_records_worker_and_credits_worker_tree'),
    ('RSS owner lookup', 'admit.py', 'process_tree_mb(owner, timeout)', "process_tree_mb(data['pid'], timeout)", 'HeadroomTests.test_sidecar_keeps_holder_pid_and_uses_owner_for_rss'),
    ('missing owner credit', 'admit.py', "owner = data.get('owner_pid')", "owner = data.get('owner_pid', data['pid'])", 'HeadroomTests.test_missing_dead_or_invalid_owner_charges_full_expected_peak'),
    ('dead owner credit', 'admit.py', 'and pid_alive(owner)', '', 'HeadroomTests.test_missing_dead_or_invalid_owner_charges_full_expected_peak'),
    ('reserve never acquired', 'admit.py', 'if not reserve:\n            probe = try_lock(path)', 'if True:\n            probe = try_lock(path)', 'HeadroomTests.test_scanning_reserve_never_acquires_it'),
    ('release gap polling', 'admit.py', "in ('slots', 'mutex', 'unaccounted'):", "in ('slots', 'mutex'):", 'HeadroomTests.test_release_gap_uses_slot_poll_instead_of_memory_backoff'),
    ('unknown RSS', 'admit.py', 'peak if actual is None', '0 if actual is None', 'HeadroomTests.test_growth_uses_expected_peak_unknown_tree_and_nonnegative_difference'),
    ('growth clamp', 'admit.py', 'max(0, peak - actual)', 'peak - actual', 'HeadroomTests.test_growth_uses_expected_peak_unknown_tree_and_nonnegative_difference'),
    ('expected sidecar', 'admit.py', "'expected_mb': expected_mb", "'expected_mb': amount", 'HeadroomTests.test_publishes_selected_peak_and_uses_runner_project'),
    ('RSS sibling tree', 'admit.py', 'root = ancestor', 'root = pid', 'ProbeTests.test_process_tree_includes_worker_siblings_but_excludes_unrelated'),
    ('RSS descendants', 'admit.py', 'descendants.update(added)', 'break', 'ProbeTests.test_process_tree_includes_worker_siblings_but_excludes_unrelated'),
    ('RSS units', 'admit.py', 'for p in descendants) / 1024', 'for p in descendants) / 1', 'ProbeTests.test_process_tree_includes_worker_siblings_but_excludes_unrelated'),
    ('backoff cap', 'admit.py', 'min(self.index + 1, len(delays) - 1)', '0', 'PolicyTests.test_backoff_caps_and_slot_or_mutex_wait_resets'),
    ('backoff reset', 'admit.py', 'self.index = 0\n            return', 'self.index = self.index\n            return', 'PolicyTests.test_backoff_caps_and_slot_or_mutex_wait_resets'),
    ('slot polling', 'admit.py', "return self.policy['slot_poll_s']", 'return 999', 'PolicyTests.test_backoff_caps_and_slot_or_mutex_wait_resets'),
    ('reason changes', 'admit.py', 'if reason != previous:', 'if previous is None:', 'HeadroomTests.test_repeated_reason_is_logged_again_after_it_changes'),
    ('JS required policy', 'index.cjs', 'value = JSON.parse(input)', 'value = JSON.parse(input ?? "1")', 'js'),
    ('JS sample integer', 'index.cjs', "name === 'peak_min_samples' && !Number.isSafeInteger(value)", 'false', 'js'),
    ('JS positive numbers', 'index.cjs', 'Number.isFinite(v) && v > 0', 'Number.isFinite(v) && v >= 0', 'js'),
    ('JS caller positive', 'index.cjs', 'value <= 0', 'value < 0', 'js'),
    ('JS caller forwarding', 'index.cjs', "callerPeak(env)];", "''];", 'js'),
]


def main():
    if shutil.which('node') is None:
        print('Node is required for admission mutation controls', file=sys.stderr)
        return 2
    with tempfile.TemporaryDirectory(prefix='admission-mutations.') as directory:
        root = Path(directory)
        action = root / '.github/actions/self-hosted-job'
        tests = root / '.github/review'
        shutil.copytree(ACTION, action)
        tests.mkdir(parents=True)
        for name in ('test_admission_budget.py', 'test_admission_headroom.py', 'test_self_hosted_job.py'):
            shutil.copy2(HERE / name, tests / name)
        for label, file, old, new, test in MUTATIONS:
            path = action / file
            original = path.read_text()
            if old not in original:
                raise AssertionError(f'Mutation site missing: {label}')
            path.write_text(original.replace(old, new, 1))
            selector = ('test_self_hosted_job.CapacityInputTests' if test == 'js'
                        else test if test.startswith('test_') else 'test_admission_headroom.' + test)
            try:
                result = subprocess.run([sys.executable, '-m', 'unittest', selector], cwd=tests,
                                        capture_output=True, text=True, timeout=15)
            finally:
                path.write_text(original)
            assertion = ('FAIL:' in result.stderr or (test == 'js' and 'AssertionError [ERR_ASSERTION]' in result.stderr))
            if result.returncode == 0 or not assertion:
                print(f'UNDETECTED or invalid control: {label}\n{result.stderr}', file=sys.stderr)
                return 1
            print(f'Detected: {label}')
    print(f'{len(MUTATIONS)} serial mutations detected')
    return 0


if __name__ == '__main__':
    sys.exit(main())
