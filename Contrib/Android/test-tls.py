"""Run isolated localhost TLS regressions through the packaged Android runtime."""
import argparse
import base64
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import shutil
import ssl
import subprocess
import threading
import time
import uuid


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--serial", required=True)
    parser.add_argument("--apk", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=Path("artifacts/android"))
    parser.add_argument("--openssl", default=shutil.which("openssl"))
    args = parser.parse_args()
    if not args.openssl:
        raise ValueError("Supply the installed OpenSSL executable with --openssl")
    output = args.output.resolve() / ("device-tls-" + uuid.uuid4().hex)
    output.mkdir(parents=True)
    certificate, key = output / "public-certificate.pem", output / "synthetic-private-key.pem"
    with (output / "openssl.log").open("w") as log:
        subprocess.run([args.openssl, "req", "-x509", "-newkey", "rsa:2048", "-sha256", "-nodes", "-days", "1",
                        "-keyout", str(key), "-out", str(certificate), "-subj", "/CN=localhost",
                        "-addext", "subjectAltName=DNS:localhost"], stdout=log, stderr=subprocess.STDOUT, check=True)
    public_der = ssl.PEM_cert_to_DER_cert(certificate.read_text())
    public_argument = base64.b64encode(public_der).decode()

    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            self.send_response(200)
            self.send_header("Content-Length", "2")
            self.send_header("Connection", "close")
            self.end_headers()
            self.wfile.write(b"OK")

        def log_message(self, *arguments):
            pass

    class Server(ThreadingHTTPServer):
        daemon_threads = True

        def get_request(self):
            connection, address = super().get_request()
            connection.settimeout(20)
            try:
                return context.wrap_socket(connection, server_side=True), address
            except BaseException:
                connection.close()
                raise

    context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    context.maximum_version = ssl.TLSVersion.TLSv1_2
    context.load_cert_chain(certificate, key)
    server = Server(("127.0.0.1", 18446), Handler)
    worker = threading.Thread(target=server.serve_forever, daemon=True)
    worker.start()
    package = "io.wasabiwallet.android.runtimeprobe"

    def adb(*arguments):
        return subprocess.check_output(["adb", "-s", args.serial, *arguments], text=True, stderr=subprocess.STDOUT, timeout=60)

    try:
        adb("install", "--no-incremental", "-r", str(args.apk.resolve()))
        adb("reverse", "tcp:18446", "tcp:18446")
        adb("shell", "am", "force-stop", package)
        adb("logcat", "-c")
        adb("shell", "am", "start", "-n", package + "/io.wasabiwallet.android.RuntimeProbeActivity",
            "--ez", "tls-vectors", "true", "--es", "fixture-certificate", public_argument)
        deadline = time.monotonic() + 120
        while time.monotonic() < deadline:
            result = adb("logcat", "-d", "-s", "WasabiRuntime:I", "*:S")
            (output / "tls.log").write_text(result)
            if "FAIL:" in result:
                raise RuntimeError("Packaged Android TLS regression failed: " + str(output / "tls.log"))
            if "PASS: TLS certificate and hostname vectors, three fresh cycles" in result:
                for expected, count in (("untrusted certificate rejected", 3), ("wrong hostname rejected", 6), ("authenticated localhost TLS and response", 6)):
                    if result.count("PASS: " + expected) != count:
                        raise RuntimeError("Incomplete TLS fixture checks")
                print("PASS: packaged TLS trust/hostname and supplemental-root regressions; " + str(output))
                return
            time.sleep(1)
        raise TimeoutError("Android TLS regression exceeded its deadline: " + str(output / "tls.log"))
    finally:
        try:
            adb("shell", "am", "force-stop", package)
            adb("reverse", "--remove", "tcp:18446")
        finally:
            server.shutdown()
            server.server_close()
            worker.join(timeout=5)
            # Only this newly generated public fixture's private key is removed.
            key.unlink()


if __name__ == "__main__":
    main()
