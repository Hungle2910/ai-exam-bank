#!/usr/bin/env python3
"""
AI Exam Bank - M5-04 Failure Drill & Resilience Test Suite Runner
Executes automated failure drill validations:
1. Bedrock Throttling simulation (Transient backoff + jitter)
2. Worker Process Crash simulation (Stale Lease Recovery)
3. AI Provider Circuit Breaker Tripping (5 consecutive errors -> Open -> HalfOpen)
4. Zero Information Leak Sanitization (CWE-209 redaction)
5. Retry Eligibility Enforcement (Permanent vs Transient)
"""

import json
import os
import subprocess
import sys
import time
from contextlib import contextmanager
from datetime import datetime, timezone

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

WORKSPACE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
TEST_PROJECT = os.path.join(WORKSPACE_ROOT, "tests", "Modules", "Jobs.Tests", "AiExamBank.Modules.Jobs.Tests.csproj")
OUTPUT_EVIDENCE = os.path.join(WORKSPACE_ROOT, "docs", "M5_04", "drill_evidence.json")
GLOBAL_JSON_PATH = os.path.join(WORKSPACE_ROOT, "global.json")


def run_command(args, cwd=None):
    """Run shell command and return exit code, stdout, stderr."""
    process = subprocess.Popen(
        args,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        errors="replace",
        cwd=cwd or WORKSPACE_ROOT
    )
    stdout, stderr = process.communicate()
    return process.returncode, stdout, stderr


@contextmanager
def temporary_local_sdk_context():
    """Ensure local preview SDK works if 10.0.400 is not installed, and ALWAYS restore original global.json."""
    original_content = None
    if os.path.exists(GLOBAL_JSON_PATH):
        with open(GLOBAL_JSON_PATH, "r", encoding="utf-8") as f:
            original_content = f.read()
    try:
        _, sdks, _ = run_command(["dotnet", "--list-sdks"])
        if "10.0.400" not in sdks and "10.0." in sdks:
            installed = [line.split()[0] for line in sdks.splitlines() if line.startswith("10.0.")]
            if installed:
                with open(GLOBAL_JSON_PATH, "w", encoding="utf-8") as f:
                    json.dump({"sdk": {"version": installed[0], "rollForward": "latestPatch"}}, f, indent=2)
        yield
    finally:
        if original_content is not None:
            with open(GLOBAL_JSON_PATH, "w", encoding="utf-8") as f:
                f.write(original_content)


def execute_dotnet_resilience_tests():
    """Execute .NET MSTest suite for failure handling and resilience."""
    print("================================================================================")
    print("[M5-04 Drill] Running .NET Failure Handling & Resilience Test Suite...")
    print("================================================================================")
    cmd = [
        "dotnet", "test", TEST_PROJECT,
        "-c", "Release",
        "--filter", "FullyQualifiedName~FailureHandlingAndResilienceTests",
        "--verbosity", "minimal"
    ]
    with temporary_local_sdk_context():
        returncode, stdout, stderr = run_command(cmd)
    print(stdout)
    if returncode != 0:
        print("[ERROR] Resilience tests failed!", file=sys.stderr)
        print(stderr, file=sys.stderr)
        return False
    return True


def generate_redacted_evidence():
    """Generates standardized, redacted JSON evidence for the failure drill runbook."""
    timestamp = datetime.now(timezone.utc).isoformat()
    evidence = {
        "engineVersion": "job-engine/v1.2",
        "releaseTag": "v1.2.0-rc1",
        "drillExecutionTimeUtc": timestamp,
        "evidenceType": "Illustrative Simulated Benchmark",
        "simulated": True,
        "liveAws": False,
        "executionEnvironment": "Local Unit & Resilience Test Suite",
        "drills": [
            {
                "drillId": "DRILL-01-THROTTLING",
                "title": "Amazon Bedrock API Throttling & Rate-Limit Backoff",
                "simulatedError": "Amazon.BedrockRuntime.Model.ThrottlingException: Rate limit exceeded",
                "sanitizedErrorCode": "AWS_BEDROCK_THROTTLED",
                "actionableReason": "Dịch vụ AI đang quá tải lượt yêu cầu. Hệ thống đang tự động giãn cách và thử lại.",
                "attemptStateBefore": {
                    "attempt": 1,
                    "status": "Started",
                    "workerId": "worker-aws-apse1-01"
                },
                "attemptStateAfter": {
                    "attempt": 1,
                    "status": "Failed",
                    "errorCode": "AWS_BEDROCK_THROTTLED",
                    "safeErrorMessage": "Dịch vụ AI đang quá tải lượt yêu cầu. Hệ thống đang tự động giãn cách và thử lại."
                },
                "jobStateAfter": {
                    "status": "Pending",
                    "attemptCount": 1,
                    "maxAttempts": 3,
                    "backoffDelaySeconds": 30,
                    "isRetryEligible": False,
                    "dataLoss": False
                }
            },
            {
                "drillId": "DRILL-02-STALE-LEASE-RECOVERY",
                "title": "Worker Process Hard Crash (Process Kill / OOM / Host Reboot)",
                "simulatedFailure": "Stop-Process -Force on worker process during active execution",
                "recoveryMechanism": "RecoverExpiredLeasesAsync (LeaseExpiresAt < UtcNow)",
                "actionableReason": "Tiến trình xử lý bị mất kết nối đột ngột. Tác vụ đã được tự động cấp cứu và khôi phục.",
                "abandonedAttempt": {
                    "attemptNumber": 1,
                    "status": "Abandoned",
                    "workerId": "worker-crash-test-01",
                    "executionDurationMs": 120450
                },
                "recoveredJobState": {
                    "status": "Pending",
                    "attemptCount": 1,
                    "maxAttempts": 3,
                    "errorCode": "STALE_LEASE_RECOVERED",
                    "retryReason": "Tự động phục hồi sau khi Worker bị mất tín hiệu Heartbeat.",
                    "indefiniteSpinnerResolved": True,
                    "dataLoss": False
                }
            },
            {
                "drillId": "DRILL-03-CIRCUIT-BREAKER",
                "title": "Extended Provider Outage & Circuit Breaker Tripping",
                "thresholdRule": "5 consecutive AI failures within 60 seconds",
                "circuitTransitions": [
                    {"step": 1, "state": "Closed", "failures": 0, "action": "Allow request"},
                    {"step": 2, "state": "Closed", "failures": 4, "action": "Allow request"},
                    {"step": 3, "state": "Open", "failures": 5, "action": "Trip circuit; reject all incoming with CIRCUIT_BREAKER_OPEN"},
                    {"step": 4, "state": "HalfOpen", "cooldownSeconds": 60, "action": "Allow 1 probe request"},
                    {"step": 5, "state": "Closed", "probeResult": "Success", "action": "Reset failures to 0, resume normal traffic"}
                ],
                "actionableReason": "Dịch vụ AI đang tạm ngưng do phát hiện sự cố liên tiếp. Hệ thống sẽ tự động khôi phục sau ít phút.",
                "quotaProtected": True
            },
            {
                "drillId": "DRILL-04-ZERO-INFORMATION-LEAK",
                "title": "Zero Information Leak Sanitization (CWE-209)",
                "rawDiagnosticInput": "System.Exception: S3 failure at C:\\app\\aws\\secret_keys.cs:42 (AKIA****REDACTED) arn:aws:iam::123456789012:role/Dev",
                "sanitizedClientOutput": "Không có quyền truy cập kho lưu trữ tài liệu đám mây Amazon S3.",
                "sanitizationChecks": {
                    "stackTraceMasked": True,
                    "internalPathsMasked": True,
                    "awsAccessKeysMasked": True,
                    "awsArnsMasked": True
                }
            }
        ]
    }

    os.makedirs(os.path.dirname(OUTPUT_EVIDENCE), exist_ok=True)
    with open(OUTPUT_EVIDENCE, "w", encoding="utf-8") as f:
        json.dump(evidence, f, ensure_ascii=False, indent=2)

    print(f"[OK] Redacted evidence artifact saved: {OUTPUT_EVIDENCE}")
    return evidence


def main():
    start_time = time.time()
    print("================================================================================")
    print("[M5-04] AI Exam Bank - Failure Drill Runner")
    print("================================================================================")

    # 1. Run unit resilience tests
    success = execute_dotnet_resilience_tests()
    if not success:
        sys.exit(1)

    # 2. Generate redacted evidence artifact
    evidence = generate_redacted_evidence()

    elapsed = time.time() - start_time
    print("================================================================================")
    print(f"[SUCCESS] Resilience unit tests passed and illustrative failure drill evidence generated in {elapsed:.2f}s!")
    print(f"[INFO] Total drills recorded: {len(evidence['drills'])}")
    print("================================================================================")
    return 0


if __name__ == "__main__":
    sys.exit(main())
