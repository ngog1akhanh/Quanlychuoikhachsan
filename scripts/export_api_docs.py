"""Export the OpenAPI and authorization metadata produced by the HTTP test host."""
from pathlib import Path
import json

root = Path(__file__).resolve().parents[1]
build = root / "Quanlychuoikhachsan.Api.Tests/bin/Debug/net10.0"
spec = json.loads((build / "api-openapi.json").read_text(encoding="utf-8-sig"))
access = {(a["path"], a["method"].lower()): a for a in json.loads((build / "api-access.json").read_text(encoding="utf-8-sig"))}
groups = {}
count = 0
for path, methods in sorted(spec["paths"].items()):
    for method, operation in methods.items():
        if method not in {"get", "post", "put", "patch", "delete", "options", "head"}:
            continue
        a = access[(path, method)]
        if a["anonymous"]:
            operation["security"] = []
        operation["x-required-permissions"] = a["permissions"]
        permission = "Công khai" if a["anonymous"] else " + ".join(a["permissions"]) or "JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ"
        tag = operation.get("tags", ["API"])[0]
        groups.setdefault(tag, []).append(f"| {method.upper()} | `{path}` | {permission} |")
        count += 1

docs = root / "docs"
docs.mkdir(exist_ok=True)
(docs / "openapi.json").write_text(json.dumps(spec, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
lines = ["# Danh mục API", "", f"Tổng hiện tại: **{count} thao tác HTTP** trên **{len(spec['paths'])} đường dẫn** (bao gồm API có sẵn và API bổ sung).", "",
         "Sinh từ Swagger và metadata phân quyền của HTTP test host. Body/response chi tiết xem [OpenAPI](openapi.json); luồng sử dụng xem [hướng dẫn](API_WORKFLOWS.md).",
         "", "Cột quyền thể hiện yêu cầu khai báo ở controller. Các API JWT tự phục vụ còn kiểm tra chủ booking/tài khoản; ID người khác có thể trả 404. Mỗi mã quyền được kiểm tra với vai trò đang hoạt động trong database.", ""]
for tag, rows in sorted(groups.items()):
    lines += [f"## {tag}", "", "| Method | Endpoint | Quyền truy cập |", "|---|---|---|", *rows, ""]
(docs / "API_ENDPOINTS.md").write_text("\n".join(lines), encoding="utf-8")
print(f"Exported {count} operations on {len(spec['paths'])} paths")
