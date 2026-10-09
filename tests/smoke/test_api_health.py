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
