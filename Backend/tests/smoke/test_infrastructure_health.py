"""Local HTTP boundary checks. No AWS calls, credentials or database required."""

from contextlib import contextmanager
import json
import os
import subprocess
import time
import urllib.error
import urllib.request

from test_api_health import ROOT, ASSEMBLY, available_port, stop_api, smoke_environment


def request(port, path, headers=None):
    try:
        response = urllib.request.urlopen(
            urllib.request.Request(f"http://127.0.0.1:{port}{path}", headers=headers or {}), timeout=3
        )
    except urllib.error.HTTPError as error:
        response = error
    with response:
        return response.status, response.headers, response.read().decode("utf-8")


@contextmanager
def api(environment, preview):
    port = available_port()
    settings = smoke_environment()
    settings.update({
        "ASPNETCORE_URLS": f"http://127.0.0.1:{port}",
        "ASPNETCORE_ENVIRONMENT": environment,
        "DOTNET_ENVIRONMENT": environment,
        "M2__EnableLocalPreview": str(preview).lower(),
    })
    process = subprocess.Popen(
        ["dotnet", str(ASSEMBLY), "--contentRoot", str(ROOT / "Api")],
        cwd=ROOT, env=settings, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
    )
    try:
        for _ in range(100):
            if process.poll() is not None:
                raise AssertionError(f"API exited with code {process.returncode}")
            try:
                if request(port, "/health/live")[0] == 200:
                    break
            except (urllib.error.URLError, TimeoutError, ConnectionError):
                time.sleep(0.2)
        else:
            raise AssertionError("API did not start")
        yield port
    finally:
        stop_api(process, port)


def assert_protected(port):
    for path in ["/api/v1/admin/infrastructure/health", "/admin/infrastructure"]:
        assert request(port, path)[0] == 401, path
        assert request(port, path, {
            "X-Role": "Admin", "X-Permission": "InfrastructureHealth.Read",
            "Authorization": "Bearer fabricated-local-token",
        })[0] == 401, "Untrusted client headers must never authenticate the caller"


def main():
    assert ASSEMBLY.is_file(), "Build the Release API first"
    with api("Production", True) as port:
        assert_protected(port)
        assert request(port, "/dev/infrastructure-health")[0] == 404
        assert request(port, "/dev/infrastructure-health/snapshot")[0] == 404
    with api("Development", False) as port:
        assert request(port, "/dev/infrastructure-health")[0] == 404
    with api("Development", True) as port:
        assert_protected(port)
        status, headers, body = request(port, "/dev/infrastructure-health/snapshot")
        assert status == 200 and headers.get("Cache-Control") == "no-store"
        payload = json.loads(body)
        assert payload["status"] == "UNKNOWN" and payload["checkedAt"] is None
        assert payload["deployment"] is None and payload["traceId"]
        assert len(payload["components"]) == 3
        assert all(item["status"] == "UNKNOWN" and item["checkedAt"] is None for item in payload["components"])
        assert set(payload) == {"status", "checkedAt", "environment", "deployment", "components", "traceId"}
        assert request(port, "/dev/infrastructure-health")[0] == 200
        assert request(port, "/infrastructure-panel.js")[0] == 200
        assert request(port, "/infrastructure-panel.css")[0] == 200
    print("Infrastructure health checks passed: fail-closed routes, no header bypass, Development-only preview and UNKNOWN response.")


if __name__ == "__main__":
    main()
