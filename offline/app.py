"""Serve the local browser interface for macOS."""

import json
import re
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from secrets import token_hex
from threading import Timer
import webbrowser

from vocalize import MARKS, WORD, readings

NIKUD = re.compile(rf"[{MARKS}]")


def for_browser(text):
    spans = list(WORD.finditer(text))
    if spans and all(NIKUD.search(match.group()) for match in spans):
        options = [
            {"preferred": 0, "readings": [{"text": NIKUD.sub("", match.group()), "meaning": ""}]}
            for match in spans
        ]
    else:
        options = readings(text)
    words = []
    for match, word in zip(spans, options, strict=True):
        choices = word["readings"]
        words.append({
            "original": match.group(),
            "start": len(text[:match.start()].encode("utf-16-le")) // 2,
            "selected": word["preferred"],
            "choices": choices,
        })
    return words


def create_server(vocalizer=for_browser):
    token = token_hex(24)
    template = Path(__file__).resolve().parents[1] / "index.html"
    page = template.read_text(encoding="utf-8").replace("__SESSION_TOKEN__", token).encode()

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *args):
            pass

        @property
        def host(self):
            return f"127.0.0.1:{self.server.server_port}"

        def respond(self, status, content_type, body=b""):
            self.send_response(status)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.send_header("Content-Security-Policy",
                             "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'")
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self):
            if self.headers.get("Host") != self.host:
                self.respond(403, "text/plain")
            elif self.path != "/":
                self.respond(404, "text/plain")
            else:
                self.respond(200, "text/html; charset=utf-8", page)

        def do_POST(self):
            if (self.headers.get("Host") != self.host or
                    self.headers.get("Origin") != f"http://{self.host}" or
                    self.headers.get("X-Nikud-Session") != token):
                self.respond(403, "text/plain")
                return
            if self.path != "/api/nikud":
                self.respond(404, "text/plain")
                return
            try:
                size = int(self.headers.get("Content-Length", "0"))
                if not 0 < size <= 16000:
                    raise ValueError("The request is too large or empty.")
                text = json.loads(self.rfile.read(size)).get("text")
                if not isinstance(text, str) or not 0 < len(text.strip()) or len(text) > 1500:
                    raise ValueError("Enter up to 1,500 characters of Hebrew text.")
                status, result = 200, {"words": vocalizer(text)}
            except (ValueError, AttributeError) as error:
                status, result = 400, {"error": str(error)}
            except Exception:
                status, result = 500, {"error": "The offline vocalizer could not finish. Try again."}
            self.respond(status, "application/json; charset=utf-8",
                         json.dumps(result, ensure_ascii=False).encode())

    return ThreadingHTTPServer(("127.0.0.1", 0), Handler)


if __name__ == "__main__":
    with create_server() as server:
        url = f"http://127.0.0.1:{server.server_port}/"
        print(f"Nikud is open at {url}\nPress Ctrl+C to quit.", flush=True)
        Timer(0.1, lambda: webbrowser.open(url)).start()
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            pass
