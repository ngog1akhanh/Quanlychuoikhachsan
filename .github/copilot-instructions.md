# Copilot Instructions

## Project Guidelines
- Khi hoàn thiện API hiện tại, ưu tiên cài đặt trước các NuGet package cần thiết cho kiến trúc và vận hành ổn định; các package còn thiếu sẽ bổ sung theo từng tính năng sau.
- For this .NET 10 Razor Pages backend, follow a layered architecture and return a consistent JSON envelope (success, message, data); implement payOS payments as PENDING on creation and only transition to PAID via verified successful payOS webhook; enforce ADMIN/MANAGER RBAC so MANAGER can view/edit ancillary notes but cannot manually mark payments PAID, while ADMIN can manage orders and statuses.

## Code Cleanup
- For code cleanup requests, add concise Vietnamese comments explaining the function of each newly added/modified code section, following existing file conventions, then validate and provide a handoff summary.