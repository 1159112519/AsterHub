#!/usr/bin/env python3
"""Small write-only feedback intake. Run behind the dedicated loopback proxy."""
import argparse
from collections import deque
from datetime import datetime, timezone
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import re
import secrets
import threading
import time
import uuid

MAX_BODY = 32768
CATEGORIES = {"bug", "ui", "suggestion", "other"}

class FeedbackError(Exception):
    def __init__(self, status, message):
        super().__init__(message)
        self.status = status

class FeedbackStore:
    def __init__(self, root, rate_limit=10):
        self.root = Path(root)
        self.root.mkdir(parents=True, exist_ok=True)
        self.rate_limit = rate_limit
        self.max_files = 10000
        self.max_bytes = 200 * 1024 * 1024
        files = list(self.root.rglob("FB-*.json"))
        self.count = len(files)
        self.bytes = sum(p.stat().st_size for p in files)
        self.rates = {}
        self.salt = secrets.token_bytes(32)
        self.lock = threading.Lock()

    def validate(self, payload):
        fields = {"schemaVersion", "appVersion", "platform", "category", "title", "description", "contact", "environment"}
        if not isinstance(payload, dict) or set(payload) - fields or type(payload.get("schemaVersion")) is not int or payload["schemaVersion"] != 1:
            raise FeedbackError(400, "反馈格式不正确。")
        limits = {"appVersion": 30, "platform": 30, "category": 20, "title": 100, "description": 8000, "contact": 200, "environment": 300}
        clean = {"schemaVersion": 1}
        for key, limit in limits.items():
            value = payload.get(key, "")
            if value is None and key in ("contact", "environment"): value = ""
            if not isinstance(value, str) or len(value) > limit or "\x00" in value:
                raise FeedbackError(400, "反馈内容过长或格式不正确。")
            value = value.strip()
            if key not in ("contact", "environment") and not value:
                raise FeedbackError(400, "请填写标题和问题详情。")
            if value: clean[key] = value
        if not re.fullmatch(r"\d+\.\d+\.\d+", clean["appVersion"]) or clean["category"] not in CATEGORIES or clean["platform"] not in {"Windows", "Ubuntu"}:
            raise FeedbackError(400, "版本、平台或分类不正确。")
        return clean

    def accept(self, payload, client_ip):
        clean = self.validate(payload)
        now = datetime.now(timezone.utc)
        identifier = "FB-" + now.strftime("%Y%m%d") + "-" + uuid.uuid4().hex[:16]
        clean.update(id=identifier, receivedUtc=now.isoformat())
        content = (json.dumps(clean, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
        # Rate keys are salted process-local hashes, never written with feedback.
        rate_key = hashlib.sha256(self.salt + client_ip.encode("utf-8")).hexdigest()
        tick = time.monotonic()
        with self.lock:
            if len(self.rates) > 10000:
                self.rates = {k: v for k, v in self.rates.items() if v and tick - v[-1] < 3600}
                if len(self.rates) > 10000: raise FeedbackError(503, "反馈服务繁忙，请稍后重试。")
            recent = self.rates.setdefault(rate_key, deque())
            while recent and tick - recent[0] >= 3600: recent.popleft()
            if len(recent) >= self.rate_limit: raise FeedbackError(429, "提交过于频繁，请稍后重试。")
            if self.count >= self.max_files or self.bytes + len(content) > self.max_bytes:
                raise FeedbackError(503, "反馈服务暂时不可用，请稍后重试。")
            folder = self.root / now.strftime("%Y-%m-%d")
            folder.mkdir(mode=0o700, exist_ok=True)
            path = folder / (identifier + ".json")
            try:
                fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
                with os.fdopen(fd, "wb") as output:
                    output.write(content)
                    output.flush()
                    os.fsync(output.fileno())
            except OSError:
                try: path.unlink(missing_ok=True)
                except OSError: pass
                raise FeedbackError(503, "反馈暂时无法保存，请稍后重试。")
            self.count += 1; self.bytes += len(content); recent.append(tick)
        return {"id": identifier, "status": "received"}

class Handler(BaseHTTPRequestHandler):
    server_version = "AQFeedback"
    def setup(self):
        super().setup()
        self.connection.settimeout(5)
    def log_message(self, *args): pass
    def reply(self, status, payload):
        content = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Content-Length", str(len(content)))
        self.end_headers()
        try: self.wfile.write(content)
        except (BrokenPipeError, ConnectionResetError): pass
    def do_GET(self):
        if self.path == "/healthz": self.reply(200, {"status": "ok"})
        else: self.reply(405, {"error": "仅支持提交反馈。"})
    def do_POST(self):
        try:
            if self.path != "/aqserver/api/feedback": raise FeedbackError(404, "接口不存在。")
            if self.headers.get("Origin") or self.headers.get("X-AQServer-Feedback") != "1": raise FeedbackError(403, "不支持此提交方式。")
            if self.headers.get("Content-Type", "").split(";")[0].strip().lower() != "application/json": raise FeedbackError(415, "仅支持JSON。")
            try: length = int(self.headers.get("Content-Length", "0"))
            except ValueError: raise FeedbackError(400, "请求长度无效。")
            if not 1 <= length <= MAX_BODY: raise FeedbackError(413, "反馈内容过长。")
            content = self.rfile.read(length)
            if len(content) != length: raise FeedbackError(400, "反馈内容不完整。")
            try: payload = json.loads(content.decode("utf-8"))
            except (UnicodeError, ValueError, RecursionError): raise FeedbackError(400, "反馈格式无效。")
            client_ip = self.headers.get("X-Real-IP") or self.client_address[0]
            self.reply(201, self.server.store.accept(payload, client_ip))
        except FeedbackError as error: self.reply(error.status, {"error": str(error)})
        except (OSError, TimeoutError): self.reply(408, {"error": "提交超时，请重试。"})

class Server(ThreadingHTTPServer):
    daemon_threads = True
    allow_reuse_address = True

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--data", type=Path, required=True)
    parser.add_argument("--port", type=int, default=18772)
    args = parser.parse_args()
    server = Server(("127.0.0.1", args.port), Handler)
    server.store = FeedbackStore(args.data)
    server.serve_forever()
