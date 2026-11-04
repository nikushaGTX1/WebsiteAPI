"""Exercise the HTTP request contract without loading GPU models or Modal."""
import ast
import hmac
import os
from pathlib import Path
import unittest
from unittest.mock import patch

from fastapi.testclient import TestClient


class RequestContractTests(unittest.TestCase):
    def setUp(self):
        source = ast.parse(Path(__file__).with_name("velven_image_ai.py").read_text(encoding="utf-8"))
        factory = next(node for node in source.body
                       if isinstance(node, ast.FunctionDef) and node.name == "create_api")
        namespace = {"os": os, "hmac": hmac, "APP_NAME": "test"}
        exec(compile(ast.Module(body=[factory], type_ignores=[]), "<api>", "exec"), namespace)
        self.requests = []

        async def run(request):
            self.requests.append(request)
            return {"action": "processed"}

        with patch.dict(os.environ, {"VELVEN_IMAGE_AI_TOKEN": "test-token"}):
            self.client = TestClient(namespace["create_api"](run))

    def send(self, **fields):
        return self.client.post("/v1/process", headers={"Authorization": "Bearer test-token"},
                                json={"image_b64": "aGVsbG8=", **fields})

    def test_all_watermark_scope_reaches_worker(self):
        response = self.send(mode="process", authorization_basis="owned", watermark_scope="all")
        self.assertEqual(response.status_code, 200)
        self.assertEqual(self.requests[0]["watermark_scope"], "all")

    def test_default_keeps_brand_scope(self):
        self.assertEqual(self.send().status_code, 200)
        self.assertEqual(self.requests[0]["watermark_scope"], "brands")

    def test_invalid_scope_is_rejected(self):
        self.assertEqual(self.send(watermark_scope="invalid").status_code, 422)
        self.assertEqual(self.requests, [])


if __name__ == "__main__":
    unittest.main()
