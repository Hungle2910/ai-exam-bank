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
PROJECT = ROOT / "src" / "Api" / "AiExamBank.Api.csproj"


def available_port():
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


def main():
    port = available_port()
    address = f"http://127.0.0.1:{port}/health/live"
    environment = os.environ.copy()
    environment["ASPNETCORE_URLS"] = f"http://127.0.0.1:{port}"
    process = subprocess.Popen(
        ["dotnet", "run", "--project", str(PROJECT), "--configuration", "Release",
         "--no-build", "--no-launch-profile"],
        cwd=ROOT,
        env=environment,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.PIPE,
        text=True,
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
                    print("API liveness smoke test passed.")
                    return 0
            except (urllib.error.URLError, TimeoutError, ConnectionError):
                time.sleep(0.2)
        raise RuntimeError("API did not become ready within 20 seconds")
    except RuntimeError as error:
        print(error, file=sys.stderr)
        return 1
    finally:
        process.terminate()
        try:
            process.communicate(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
            process.communicate()


if __name__ == "__main__":
    raise SystemExit(main())
