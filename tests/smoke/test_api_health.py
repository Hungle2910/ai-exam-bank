"""Run the built API and verify its public liveness contract over HTTP."""

import json
import os
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
ASSEMBLY = ROOT / "src" / "Api" / "bin" / "Release" / "net10.0" / "AiExamBank.Api.dll"


def available_port():
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


def stop_api(process, port):
    if process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)

    deadline = time.monotonic() + 2
    while time.monotonic() < deadline:
        with socket.socket() as sock:
            if sock.connect_ex(("127.0.0.1", port)) != 0:
                return
        time.sleep(0.1)
    raise RuntimeError(f"API port {port} remained open after process exit")


def test_job_enqueue_and_completion(port):
    """Verify that jobs enqueued through the API are claimed by the worker and reach Completed."""
    enqueue_url = f"http://127.0.0.1:{port}/api/v1/jobs"
    payload = json.dumps({
        "jobType": "SampleJob",
        "payloadReference": "s3://smoke-test",
        "scopeKind": "School",
        "scopeResourceId": "school-hn-01",
        "idempotencyKey": f"smoke-key-{int(time.time())}"
    }).encode("utf-8")

    req = urllib.request.Request(
        enqueue_url,
        data=payload,
        headers={
            "Content-Type": "application/json",
            "Accept": "application/json",
            "Authorization": "Bearer smoke-test-token-hn",
            "X-User-Id": "smoke-tester",
            "X-Scope-Id": "school-hn-01"
        },
        method="POST"
    )

    with urllib.request.urlopen(req, timeout=5) as response:
        if response.status != 202:
            raise RuntimeError(f"Job enqueue failed with HTTP {response.status}")
        data = json.load(response)
        job_id = data.get("jobId")
        if not job_id:
            raise RuntimeError(f"JobId missing from enqueue response: {data}")

    # Thăm dò cho đến khi job hoàn tất (tối đa 15 giây)
    status_url = f"http://127.0.0.1:{port}/api/v1/jobs/{job_id}"
    deadline = time.monotonic() + 15
    while time.monotonic() < deadline:
        status_req = urllib.request.Request(
            status_url,
            headers={
                "Accept": "application/json",
                "Authorization": "Bearer smoke-test-token-hn",
                "X-User-Id": "smoke-tester",
                "X-Scope-Id": "school-hn-01"
            }
        )
        with urllib.request.urlopen(status_req, timeout=5) as status_resp:
            job_data = json.load(status_resp)
            if job_data.get("status") == "Completed":
                print(f"Integration test passed: Job {job_id} reached Completed state.")
                return
        time.sleep(0.5)

    raise RuntimeError(f"Job {job_id} did not reach Completed state within 15 seconds.")


def test_invalid_scope_returns_400(port):
    """Verify that whitespace resource ID and invalid scope kind enum return HTTP 400 Bad Request."""
    # 1. Whitespace scopeResourceId using valid credentials
    url_whitespace = f"http://127.0.0.1:{port}/api/v1/jobs?scopeKind=School&scopeResourceId=%20%20%20"
    req_ws = urllib.request.Request(
        url_whitespace,
        headers={
            "Accept": "application/json",
            "Authorization": "Bearer smoke-test-token-hn"
        }
    )
    try:
        urllib.request.urlopen(req_ws, timeout=5)
        raise RuntimeError("Whitespace scopeResourceId should have returned HTTP 400 Bad Request")
    except urllib.error.HTTPError as err:
        if err.code != 400:
            raise RuntimeError(f"Expected HTTP 400 for whitespace scope, got {err.code}")

    # 2. Invalid scopeKind=999 using valid credentials
    url_invalid_kind = f"http://127.0.0.1:{port}/api/v1/jobs?scopeKind=999&scopeResourceId=school-hn-01"
    req_kind = urllib.request.Request(
        url_invalid_kind,
        headers={
            "Accept": "application/json",
            "Authorization": "Bearer smoke-test-token-hn"
        }
    )
    try:
        urllib.request.urlopen(req_kind, timeout=5)
        raise RuntimeError("Invalid scopeKind=999 should have returned HTTP 400")
    except urllib.error.HTTPError as err:
        if err.code != 400:
            raise RuntimeError(f"Expected HTTP 400 for invalid scope kind 999, got {err.code}")

    print("Scope validation tests passed: whitespace resource ID and invalid enum return HTTP 400.")


def test_cross_school_listing_returns_403(port):
    """Verify that listing jobs across different schools is rejected with HTTP 403 SCOPED_ACCESS_DENIED."""
    url = f"http://127.0.0.1:{port}/api/v1/jobs?scopeKind=School&scopeResourceId=school-sg-02"
    # User belongs to school-hn-01 in authenticated claim, but attempts to query school-sg-02
    req = urllib.request.Request(
        url,
        headers={
            "Accept": "application/json",
            "Authorization": "Bearer dev-teacher-hn-token",
            "X-User-Scope-Id": "school-sg-02",
            "X-Scope-Id": "school-sg-02"
        }
    )
    try:
        urllib.request.urlopen(req, timeout=5)
        raise RuntimeError("Cross-school query should have returned HTTP 403 Forbidden")
    except urllib.error.HTTPError as err:
        if err.code != 403:
            raise RuntimeError(f"Expected HTTP 403 for cross-school listing, got {err.code}")

    print("Cross-school authorization test passed: Querying jobs of another school returns HTTP 403.")


def test_unauthenticated_request_returns_401(port):
    """Verify that requests missing credentials to /api/v1/jobs are rejected with HTTP 401 Unauthorized."""
    url = f"http://127.0.0.1:{port}/api/v1/jobs?scopeKind=School&scopeResourceId=school-hn-01"
    req = urllib.request.Request(
        url,
        headers={"Accept": "application/json"}
    )
    try:
        urllib.request.urlopen(req, timeout=5)
        raise RuntimeError("Unauthenticated request should have returned HTTP 401 Unauthorized")
    except urllib.error.HTTPError as err:
        if err.code != 401:
            raise RuntimeError(f"Expected HTTP 401 for unauthenticated request, got {err.code}")

    print("Authentication test passed: Unauthenticated request rejected with HTTP 401.")


def test_forged_headers_without_auth_returns_401(port):
    """Verify that forged client headers (X-Scope-Id, X-User-Scope-Id) without credentials return HTTP 401."""
    url = f"http://127.0.0.1:{port}/api/v1/jobs?scopeKind=School&scopeResourceId=school-hn-01"
    req = urllib.request.Request(
        url,
        headers={
            "Accept": "application/json",
            "X-User-Id": "attacker",
            "X-Scope-Id": "school-hn-01",
            "X-User-Scope-Id": "school-hn-01"
        }
    )
    try:
        urllib.request.urlopen(req, timeout=5)
        raise RuntimeError("Forged headers request should have returned HTTP 401 Unauthorized")
    except urllib.error.HTTPError as err:
        if err.code != 401:
            raise RuntimeError(f"Expected HTTP 401 for forged headers request, got {err.code}")

    print("Header forgery test passed: Request with only fake client headers rejected with HTTP 401.")


def test_untrusted_bearer_token_returns_401(port):
    """Verify that arbitrary client-forged Bearer tokens (e.g. attacker:school-sg-02) return HTTP 401."""
    url = f"http://127.0.0.1:{port}/api/v1/jobs?scopeKind=School&scopeResourceId=school-sg-02"
    req = urllib.request.Request(
        url,
        headers={
            "Accept": "application/json",
            "Authorization": "Bearer attacker:school-sg-02"
        }
    )
    try:
        urllib.request.urlopen(req, timeout=5)
        raise RuntimeError("Untrusted Bearer token should have returned HTTP 401 Unauthorized")
    except urllib.error.HTTPError as err:
        if err.code != 401:
            raise RuntimeError(f"Expected HTTP 401 for untrusted Bearer token, got {err.code}")

    print("Untrusted token test passed: Arbitrary client-crafted Bearer token rejected with HTTP 401.")


def test_job_create_cross_school_returns_403(port):
    """Verify that creating a job in a different school than authenticated identity is rejected with HTTP 403."""
    url = f"http://127.0.0.1:{port}/api/v1/jobs"
    payload = json.dumps({
        "jobType": "SampleJob",
        "payloadReference": "s3://test-cross",
        "scopeKind": "School",
        "scopeResourceId": "school-sg-02",
        "idempotencyKey": f"cross-create-{int(time.time())}"
    }).encode("utf-8")

    # User authenticated for school-hn-01 tries to create job in school-sg-02
    req = urllib.request.Request(
        url,
        data=payload,
        headers={
            "Content-Type": "application/json",
            "Accept": "application/json",
            "Authorization": "Bearer dev-teacher-hn-token"
        },
        method="POST"
    )
    try:
        urllib.request.urlopen(req, timeout=5)
        raise RuntimeError("Cross-school job creation should have returned HTTP 403 Forbidden")
    except urllib.error.HTTPError as err:
        if err.code != 403:
            raise RuntimeError(f"Expected HTTP 403 for cross-school job creation, got {err.code}")

    print("Cross-school create test passed: Job creation in different school rejected with HTTP 403.")


def main():
    if not ASSEMBLY.is_file():
        print(f"Built API assembly missing: {ASSEMBLY}", file=sys.stderr)
        return 1
    port = available_port()
    address = f"http://127.0.0.1:{port}/health/live"
    environment = os.environ.copy()
    environment["ASPNETCORE_URLS"] = f"http://127.0.0.1:{port}"
    process = subprocess.Popen(
        ["dotnet", str(ASSEMBLY)],
        cwd=ROOT,
        env=environment,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    try:
        deadline = time.monotonic() + 20
        while time.monotonic() < deadline:
            if process.poll() is not None:
                raise RuntimeError(f"API exited with code {process.returncode}")
            try:
                with urllib.request.urlopen(address, timeout=1) as response:
                    payload = json.load(response)
                    if response.status != 200 or payload != {"status": "alive"}:
                        raise RuntimeError(f"Unexpected liveness response: {response.status} {payload}")
                    try:
                        urllib.request.urlopen(
                            urllib.request.Request(
                                f"http://127.0.0.1:{port}/missing",
                                headers={"Accept": "application/json"},
                            ),
                            timeout=1,
                        )
                        raise RuntimeError("Unknown route returned success")
                    except urllib.error.HTTPError as error:
                        problem = json.load(error)
                        if (
                            error.code != 404
                            or problem.get("status") != 404
                            or not problem.get("traceId")
                            or "application/problem+json" not in error.headers.get("Content-Type", "")
                        ):
                            raise RuntimeError(f"Unexpected error response: {error.code} {problem}")
                    print("API liveness and error response smoke tests passed.")
                    test_unauthenticated_request_returns_401(port)
                    test_forged_headers_without_auth_returns_401(port)
                    test_untrusted_bearer_token_returns_401(port)
                    test_job_create_cross_school_returns_403(port)
                    test_invalid_scope_returns_400(port)
                    test_cross_school_listing_returns_403(port)
                    test_job_enqueue_and_completion(port)
                    return 0
            except (urllib.error.URLError, TimeoutError, ConnectionError):
                time.sleep(0.2)
        raise RuntimeError("API did not become ready within 20 seconds")
    except RuntimeError as error:
        print(error, file=sys.stderr)
        return 1
    finally:
        stop_api(process, port)


if __name__ == "__main__":
    raise SystemExit(main())
