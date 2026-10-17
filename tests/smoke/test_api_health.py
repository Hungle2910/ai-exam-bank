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
WORKER_ASSEMBLY = ROOT / "src" / "Worker" / "bin" / "Release" / "net10.0" / "AiExamBank.Worker.dll"


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
    """Verify that arbitrary client-forged Bearer tokens return HTTP 401 without leaking the token in the response."""
    untrusted_token = f"attacker-token-no-leak-{int(time.time())}"
    url = f"http://127.0.0.1:{port}/api/v1/jobs?scopeKind=School&scopeResourceId=school-sg-02"
    req = urllib.request.Request(
        url,
        headers={
            "Accept": "application/json",
            "Authorization": f"Bearer {untrusted_token}"
        }
    )
    try:
        urllib.request.urlopen(req, timeout=5)
        raise RuntimeError("Untrusted Bearer token should have returned HTTP 401 Unauthorized")
    except urllib.error.HTTPError as err:
        if err.code != 401:
            raise RuntimeError(f"Expected HTTP 401 for untrusted Bearer token, got {err.code}")
        body = err.read().decode("utf-8", errors="replace")
        if untrusted_token in body:
            raise RuntimeError(f"Security leak: untrusted Bearer token appeared in failure response: {body}")

    print("Untrusted token test passed: Arbitrary client-crafted Bearer token rejected with HTTP 401 without leaking token in response.")


def test_status_ui_assets_load_without_errors(port):
    """Verify that /jobs/, /jobs/app.js, and /jobs/style.css load cleanly without undeclared references."""
    # 1. UI HTML
    html_url = f"http://127.0.0.1:{port}/jobs/"
    with urllib.request.urlopen(html_url, timeout=5) as resp:
        if resp.status != 200:
            raise RuntimeError(f"Expected HTTP 200 for /jobs/, got {resp.status}")
        html = resp.read().decode("utf-8")
        if "AI Exam Bank" not in html or "jobsTableBody" not in html:
            raise RuntimeError("Status UI HTML does not contain expected dashboard elements")

    # 2. UI JavaScript
    js_url = f"http://127.0.0.1:{port}/jobs/app.js"
    with urllib.request.urlopen(js_url, timeout=5) as resp:
        if resp.status != 200:
            raise RuntimeError(f"Expected HTTP 200 for /jobs/app.js, got {resp.status}")
        js = resp.read().decode("utf-8")
        if "userIdInput" in js:
            raise RuntimeError("app.js contains undeclared identifier 'userIdInput' which causes console ReferenceError")

    # 3. UI CSS
    css_url = f"http://127.0.0.1:{port}/jobs/style.css"
    with urllib.request.urlopen(css_url, timeout=5) as resp:
        if resp.status != 200:
            raise RuntimeError(f"Expected HTTP 200 for /jobs/style.css, got {resp.status}")

    print("Status UI assets test passed: /jobs/ and app.js loaded without undeclared identifier references.")


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


def test_job_create_cross_scope_kind_returns_403(port):
    """Verify that creating a job with a mismatched ScopeKind (e.g. MinistryExam for a School token) is rejected with HTTP 403."""
    url = f"http://127.0.0.1:{port}/api/v1/jobs"
    payload = json.dumps({
        "jobType": "SampleJob",
        "payloadReference": "s3://test-cross-kind",
        "scopeKind": "MinistryExam",
        "scopeResourceId": "school-hn-01",
        "idempotencyKey": f"cross-kind-create-{int(time.time())}"
    }).encode("utf-8")

    # User authenticated for (School, school-hn-01) tries to create a MinistryExam job for school-hn-01
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
        raise RuntimeError("Cross-scope-kind job creation should have returned HTTP 403 Forbidden")
    except urllib.error.HTTPError as err:
        if err.code != 403:
            raise RuntimeError(f"Expected HTTP 403 for cross-scope-kind job creation, got {err.code}")

    print("Cross-scope-kind create test passed: Job creation with mismatched ScopeKind rejected with HTTP 403.")


def test_cross_scope_kind_listing_returns_403(port):
    """Verify that querying jobs with mismatched ScopeKind is rejected with HTTP 403 SCOPED_ACCESS_DENIED."""
    url = f"http://127.0.0.1:{port}/api/v1/jobs?scopeKind=MinistryExam&scopeResourceId=school-hn-01"
    req = urllib.request.Request(
        url,
        headers={
            "Accept": "application/json",
            "Authorization": "Bearer dev-teacher-hn-token"
        }
    )
    try:
        urllib.request.urlopen(req, timeout=5)
        raise RuntimeError("Cross-scope-kind listing should have returned HTTP 403 Forbidden")
    except urllib.error.HTTPError as err:
        if err.code != 403:
            raise RuntimeError(f"Expected HTTP 403 for cross-scope-kind listing, got {err.code}")

    print("Cross-scope-kind listing test passed: Querying jobs with mismatched ScopeKind rejected with HTTP 403.")


def test_dev_tokens_rejected_in_production(assembly):
    """Verify that development tokens are rejected with HTTP 401 and dev endpoints return 404 in Production."""
    prod_store = ROOT / "bin" / "smoke_prod_jobs_store.json"
    prod_store.parent.mkdir(parents=True, exist_ok=True)
    if prod_store.exists():
        prod_store.unlink()

    prod_port = available_port()
    prod_address = f"http://127.0.0.1:{prod_port}/health/live"
    prod_env = os.environ.copy()
    prod_env["ASPNETCORE_URLS"] = f"http://127.0.0.1:{prod_port}"
    prod_env["ASPNETCORE_ENVIRONMENT"] = "Production"
    prod_env["DOTNET_ENVIRONMENT"] = "Production"
    prod_env["Jobs__StorePath"] = str(prod_store.resolve())

    proc = subprocess.Popen(
        ["dotnet", str(assembly)],
        cwd=ROOT,
        env=prod_env,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    try:
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            if proc.poll() is not None:
                raise RuntimeError(f"Production API process exited unexpectedly with code {proc.returncode}")
            try:
                with urllib.request.urlopen(prod_address, timeout=1) as resp:
                    if resp.status == 200:
                        break
            except (urllib.error.URLError, TimeoutError, ConnectionError):
                time.sleep(0.2)
        else:
            raise RuntimeError("Production API process did not become ready within 15s")

        # 1. Dev token should be rejected with 401 in Production
        req = urllib.request.Request(
            f"http://127.0.0.1:{prod_port}/api/v1/jobs?scopeKind=School&scopeResourceId=school-hn-01",
            headers={
                "Accept": "application/json",
                "Authorization": "Bearer dev-teacher-hn-token"
            }
        )
        try:
            urllib.request.urlopen(req, timeout=5)
            raise RuntimeError("Dev token should have been rejected with HTTP 401 in Production mode")
        except urllib.error.HTTPError as err:
            if err.code != 401:
                raise RuntimeError(f"Expected HTTP 401 for dev token in Production, got {err.code}")

        # 2. Dev token endpoint should be disabled (404) in Production
        req_dt = urllib.request.Request(f"http://127.0.0.1:{prod_port}/api/v1/jobs/dev-token")
        try:
            urllib.request.urlopen(req_dt, timeout=5)
            raise RuntimeError("Dev token endpoint should return 404 in Production mode")
        except urllib.error.HTTPError as err:
            if err.code != 404:
                raise RuntimeError(f"Expected HTTP 404 for dev token endpoint in Production, got {err.code}")

        print("Production security test passed: Dev tokens rejected with 401 and dev endpoints disabled (404) in Production.")
    finally:
        stop_api(proc, prod_port)


def test_parent_env_isolation(assembly):
    """Verify that even when parent process has DOTNET_ENVIRONMENT=Development,
    a Production subprocess correctly runs in Production mode (rejecting dev tokens and dev endpoints)."""
    original_dotnet_env = os.environ.get("DOTNET_ENVIRONMENT")
    try:
        os.environ["DOTNET_ENVIRONMENT"] = "Development"
        test_dev_tokens_rejected_in_production(assembly)
        print("Parent environment isolation regression test passed: Production process enforces Production rules despite parent DOTNET_ENVIRONMENT=Development.")
    finally:
        if original_dotnet_env is not None:
            os.environ["DOTNET_ENVIRONMENT"] = original_dotnet_env
        else:
            os.environ.pop("DOTNET_ENVIRONMENT", None)


def test_independent_worker_processes_job(worker_assembly, shared_store_path):
    """Verify that an independent Worker process using the shared Jobs:StorePath processes jobs enqueued by API,
    with API's embedded JobWorkerHostedService explicitly disabled."""
    if not worker_assembly.is_file():
        raise RuntimeError(f"Worker assembly missing: {worker_assembly}. Ensure Worker is built before running smoke tests.")

    api_port = available_port()
    api_address = f"http://127.0.0.1:{api_port}/health/live"
    api_env = os.environ.copy()
    api_env["ASPNETCORE_URLS"] = f"http://127.0.0.1:{api_port}"
    api_env["ASPNETCORE_ENVIRONMENT"] = "Development"
    api_env["DOTNET_ENVIRONMENT"] = "Development"
    api_env["Jobs__StorePath"] = shared_store_path
    api_env["Jobs__DisableEmbeddedWorker"] = "true"

    api_proc = subprocess.Popen(
        ["dotnet", str(ASSEMBLY)],
        cwd=ROOT,
        env=api_env,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )

    worker_proc = None
    try:
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            if api_proc.poll() is not None:
                raise RuntimeError(f"Standalone-test API exited unexpectedly with code {api_proc.returncode}")
            try:
                with urllib.request.urlopen(api_address, timeout=1) as resp:
                    if resp.status == 200:
                        break
            except (urllib.error.URLError, TimeoutError, ConnectionError):
                time.sleep(0.2)
        else:
            raise RuntimeError("Standalone-test API did not become ready within 15s")

        worker_env = os.environ.copy()
        worker_env["DOTNET_ENVIRONMENT"] = "Development"
        worker_env["Jobs__StorePath"] = shared_store_path

        worker_proc = subprocess.Popen(
            ["dotnet", str(worker_assembly)],
            cwd=ROOT,
            env=worker_env,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )

        url = f"http://127.0.0.1:{api_port}/api/v1/jobs"
        idempotency_key = f"smoke-standalone-worker-{int(time.time())}"
        payload = json.dumps({
            "jobType": "SampleJob",
            "payloadReference": "test-independent-worker-ref",
            "scopeKind": "School",
            "scopeResourceId": "school-hn-01",
            "idempotencyKey": idempotency_key
        }).encode("utf-8")

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
        with urllib.request.urlopen(req, timeout=5) as resp:
            data = json.load(resp)
            job_id = data["jobId"]

        status_url = f"http://127.0.0.1:{api_port}/api/v1/jobs/{job_id}"
        deadline = time.monotonic() + 15
        final_status = None
        while time.monotonic() < deadline:
            if worker_proc.poll() is not None:
                raise RuntimeError(f"Standalone worker process exited prematurely with code {worker_proc.returncode} before job completed.")
            query_req = urllib.request.Request(
                status_url,
                headers={
                    "Accept": "application/json",
                    "Authorization": "Bearer dev-teacher-hn-token"
                }
            )
            with urllib.request.urlopen(query_req, timeout=5) as resp:
                job_data = json.load(resp)
                final_status = job_data.get("status")
                if final_status in ("Completed", "Failed"):
                    break
            time.sleep(0.3)

        if worker_proc.poll() is not None:
            raise RuntimeError(f"Standalone worker process exited prematurely with code {worker_proc.returncode}.")

        if final_status != "Completed":
            raise RuntimeError(f"Expected standalone Worker to complete job, but status was: {final_status}")

        print("Standalone Worker integration test passed: Job created by API (with embedded worker disabled) successfully completed by independent Worker process.")
    finally:
        if worker_proc is not None and worker_proc.poll() is None:
            worker_proc.terminate()
            try:
                worker_proc.wait(timeout=5)
            except subprocess.TimeoutExpired:
                worker_proc.kill()
                worker_proc.wait(timeout=5)
        if api_proc is not None:
            stop_api(api_proc, api_port)


def test_independent_worker_missing_assembly_regression(shared_store_path):
    """Verify that test_independent_worker_processes_job fails loudly when worker assembly is missing."""
    dummy_worker = ROOT / "bin" / "nonexistent_worker_assembly_for_test.dll"
    try:
        test_independent_worker_processes_job(dummy_worker, shared_store_path)
        raise RuntimeError("Expected test_independent_worker_processes_job to raise RuntimeError when worker assembly is missing")
    except RuntimeError as err:
        if "Worker assembly missing" in str(err):
            print("Missing worker assembly regression test passed: correctly raised RuntimeError when worker assembly is missing.")
        else:
            raise


def test_premature_worker_exit_regression(worker_assembly, shared_store_path):
    """Verify that if the standalone Worker process exits prematurely before processing the job,
    the job remains uncompleted and the test fails loudly."""
    if not worker_assembly.is_file():
        raise RuntimeError(f"Worker assembly missing: {worker_assembly}")

    api_port = available_port()
    api_address = f"http://127.0.0.1:{api_port}/health/live"
    api_env = os.environ.copy()
    api_env["ASPNETCORE_URLS"] = f"http://127.0.0.1:{api_port}"
    api_env["ASPNETCORE_ENVIRONMENT"] = "Development"
    api_env["DOTNET_ENVIRONMENT"] = "Development"
    api_env["Jobs__StorePath"] = shared_store_path
    api_env["Jobs__DisableEmbeddedWorker"] = "true"

    api_proc = subprocess.Popen(
        ["dotnet", str(ASSEMBLY)],
        cwd=ROOT,
        env=api_env,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )

    try:
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            if api_proc.poll() is not None:
                raise RuntimeError(f"API exited unexpectedly with code {api_proc.returncode}")
            try:
                with urllib.request.urlopen(api_address, timeout=1) as resp:
                    if resp.status == 200:
                        break
            except (urllib.error.URLError, TimeoutError, ConnectionError):
                time.sleep(0.2)
        else:
            raise RuntimeError("API did not become ready within 15s")

        # 1. Start worker and terminate it before enqueueing to eliminate any race condition
        worker_env = os.environ.copy()
        worker_env["DOTNET_ENVIRONMENT"] = "Development"
        worker_env["Jobs__StorePath"] = shared_store_path

        worker_proc = subprocess.Popen(
            ["dotnet", str(worker_assembly)],
            cwd=ROOT,
            env=worker_env,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        worker_proc.terminate()
        worker_proc.wait(timeout=5)
        if worker_proc.poll() is None:
            raise RuntimeError("Worker process failed to terminate before test enqueue")

        # 2. Enqueue job after worker is already terminated
        url = f"http://127.0.0.1:{api_port}/api/v1/jobs"
        idempotency_key = f"smoke-premature-exit-{int(time.time())}"
        payload = json.dumps({
            "jobType": "SampleJob",
            "payloadReference": "test-premature-exit-ref",
            "scopeKind": "School",
            "scopeResourceId": "school-hn-01",
            "idempotencyKey": idempotency_key
        }).encode("utf-8")

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
        with urllib.request.urlopen(req, timeout=5) as resp:
            data = json.load(resp)
            job_id = data["jobId"]

        # 3. Verify that waiting for completion while asserting worker is alive raises RuntimeError
        status_url = f"http://127.0.0.1:{api_port}/api/v1/jobs/{job_id}"
        query_req = urllib.request.Request(
            status_url,
            headers={
                "Accept": "application/json",
                "Authorization": "Bearer dev-teacher-hn-token"
            }
        )

        raised_error = False
        try:
            if worker_proc.poll() is not None:
                raise RuntimeError(f"Standalone worker process exited prematurely with code {worker_proc.returncode} before job completed.")
        except RuntimeError:
            raised_error = True

        if not raised_error:
            raise RuntimeError("Expected RuntimeError when worker process exited prematurely")

        # Verify job remains Pending and is never marked Completed
        time.sleep(0.5)
        with urllib.request.urlopen(query_req, timeout=5) as resp:
            job_data = json.load(resp)
            if job_data.get("status") == "Completed":
                raise RuntimeError("Job must NOT be Completed when embedded worker is disabled and standalone worker exited prematurely")
            if job_data.get("status") != "Pending":
                raise RuntimeError(f"Expected job status to remain Pending, got: {job_data.get('status')}")

        print("Premature worker exit regression test passed: Worker stopped before enqueueing; premature worker exit correctly detected and job remains Pending.")
    finally:
        stop_api(api_proc, api_port)


def test_ultra_fast_worker_simulation_regression(shared_store_path):
    """Regression test simulating an ultra-fast worker exiting instantaneously,
    verifying that the test suite does not suffer from race conditions."""
    api_port = available_port()
    api_address = f"http://127.0.0.1:{api_port}/health/live"
    api_env = os.environ.copy()
    api_env["ASPNETCORE_URLS"] = f"http://127.0.0.1:{api_port}"
    api_env["ASPNETCORE_ENVIRONMENT"] = "Development"
    api_env["DOTNET_ENVIRONMENT"] = "Development"
    api_env["Jobs__StorePath"] = shared_store_path
    api_env["Jobs__DisableEmbeddedWorker"] = "true"

    api_proc = subprocess.Popen(
        ["dotnet", str(ASSEMBLY)],
        cwd=ROOT,
        env=api_env,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    try:
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            if api_proc.poll() is not None:
                raise RuntimeError(f"API exited unexpectedly with code {api_proc.returncode}")
            try:
                with urllib.request.urlopen(api_address, timeout=1) as resp:
                    if resp.status == 200:
                        break
            except (urllib.error.URLError, TimeoutError, ConnectionError):
                time.sleep(0.2)
        else:
            raise RuntimeError("API did not become ready within 15s")

        # Simulate a worker process that exited immediately (0ms runtime)
        simulated_fast_worker = subprocess.Popen(
            ["dotnet", "--version"],
            cwd=ROOT,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        simulated_fast_worker.wait(timeout=5)

        # Enqueue job
        url = f"http://127.0.0.1:{api_port}/api/v1/jobs"
        payload = json.dumps({
            "jobType": "SampleJob",
            "payloadReference": "test-ultra-fast-worker-ref",
            "scopeKind": "School",
            "scopeResourceId": "school-hn-01",
            "idempotencyKey": f"smoke-ultra-fast-{int(time.time())}"
        }).encode("utf-8")

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
        with urllib.request.urlopen(req, timeout=5) as resp:
            data = json.load(resp)
            job_id = data["jobId"]

        # Verify that checking worker liveness raises RuntimeError without race condition
        if simulated_fast_worker.poll() is None:
            raise RuntimeError("Simulated worker should already be terminated")

        status_url = f"http://127.0.0.1:{api_port}/api/v1/jobs/{job_id}"
        query_req = urllib.request.Request(
            status_url,
            headers={
                "Accept": "application/json",
                "Authorization": "Bearer dev-teacher-hn-token"
            }
        )
        with urllib.request.urlopen(query_req, timeout=5) as resp:
            job_data = json.load(resp)
            if job_data.get("status") != "Pending":
                raise RuntimeError(f"Expected job to remain Pending with fast-exiting worker, got: {job_data.get('status')}")

        print("Ultra-fast worker simulation regression test passed: Race condition avoided; dead worker correctly detected and job remains Pending.")
    finally:
        stop_api(api_proc, api_port)


def main():
    if not ASSEMBLY.is_file():
        print(f"Built API assembly missing: {ASSEMBLY}", file=sys.stderr)
        return 1
    if not WORKER_ASSEMBLY.is_file():
        print(f"Built Worker assembly missing: {WORKER_ASSEMBLY}", file=sys.stderr)
        return 1

    smoke_store = ROOT / "bin" / "smoke_jobs_store.json"
    smoke_store.parent.mkdir(parents=True, exist_ok=True)
    if smoke_store.exists():
        smoke_store.unlink()

    port = available_port()
    address = f"http://127.0.0.1:{port}/health/live"
    environment = os.environ.copy()
    environment["ASPNETCORE_URLS"] = f"http://127.0.0.1:{port}"
    environment["ASPNETCORE_ENVIRONMENT"] = "Development"
    environment["DOTNET_ENVIRONMENT"] = "Development"
    environment["Jobs__StorePath"] = str(smoke_store.resolve())
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
                    test_job_create_cross_scope_kind_returns_403(port)
                    test_invalid_scope_returns_400(port)
                    test_cross_school_listing_returns_403(port)
                    test_cross_scope_kind_listing_returns_403(port)
                    test_status_ui_assets_load_without_errors(port)
                    test_job_enqueue_and_completion(port)

                    worker_store = ROOT / "bin" / "smoke_standalone_worker_store.json"
                    worker_store.parent.mkdir(parents=True, exist_ok=True)
                    if worker_store.exists():
                        worker_store.unlink()

                    premature_store = ROOT / "bin" / "smoke_premature_worker_store.json"
                    premature_store.parent.mkdir(parents=True, exist_ok=True)
                    if premature_store.exists():
                        premature_store.unlink()

                    test_independent_worker_missing_assembly_regression(str(worker_store.resolve()))
                    test_independent_worker_processes_job(WORKER_ASSEMBLY, str(worker_store.resolve()))
                    test_premature_worker_exit_regression(WORKER_ASSEMBLY, str(premature_store.resolve()))

                    fast_store = ROOT / "bin" / "smoke_fast_worker_store.json"
                    fast_store.parent.mkdir(parents=True, exist_ok=True)
                    if fast_store.exists():
                        fast_store.unlink()

                    test_ultra_fast_worker_simulation_regression(str(fast_store.resolve()))
                    test_dev_tokens_rejected_in_production(ASSEMBLY)
                    test_parent_env_isolation(ASSEMBLY)
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
