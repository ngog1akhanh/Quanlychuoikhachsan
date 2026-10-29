# Danh mục API

Tổng hiện tại: **223 thao tác HTTP** trên **178 đường dẫn** (bao gồm API có sẵn và API bổ sung).

Sinh từ Swagger và metadata phân quyền của HTTP test host. Body/response chi tiết xem [OpenAPI](openapi.json); luồng sử dụng xem [hướng dẫn](API_WORKFLOWS.md).

Cột quyền thể hiện yêu cầu khai báo ở controller. Các API JWT tự phục vụ còn kiểm tra chủ booking/tài khoản; ID người khác có thể trả 404. Mỗi mã quyền được kiểm tra với vai trò đang hoạt động trong database.

## Administration

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/accounts/{id}/roles` | ROLE_READ |
| PUT | `/v1/accounts/{id}/roles` | ROLE_WRITE |
| PATCH | `/v1/accounts/{id}/status` | ACCOUNT_MANAGE |
| POST | `/v1/accounts/{id}:reset-password` | ACCOUNT_MANAGE |
| GET | `/v1/audit-logs` | AUDIT_READ |
| POST | `/v1/customers` | CUSTOMER_WRITE |
| GET | `/v1/customers/search` | CUSTOMER_READ |
| GET | `/v1/customers/{id}/history` | CUSTOMER_READ |
| GET | `/v1/permissions` | ROLE_READ |
| GET | `/v1/positions` | STAFF_READ |
| POST | `/v1/positions` | STAFF_WRITE |
| POST | `/v1/roles` | ROLE_WRITE |
| PUT | `/v1/roles/{id}` | ROLE_WRITE |
| GET | `/v1/roles/{id}/permissions` | ROLE_READ |
| PUT | `/v1/roles/{id}/permissions` | ROLE_WRITE |
| POST | `/v1/staff` | STAFF_WRITE |
| PUT | `/v1/staff/{id}` | STAFF_WRITE |

## Assets

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/assets` | INVENTORY_READ |
| GET | `/v1/assets/{id}` | INVENTORY_READ |
| POST | `/v1/assets/{id}:condition` | ASSET_WRITE |
| POST | `/v1/assets/{id}:transfer` | ASSET_WRITE |

## Auth

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| POST | `/v1/auth/login` | Công khai |
| POST | `/v1/auth/register` | Công khai |

## BillingActions

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| POST | `/v1/cancellation-requests/{id}:approve` | CANCELLATION_APPROVE |
| POST | `/v1/invoices/{id}:pay` | BILLING_PAYMENT_WRITE |

## BillingManagement

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/deposits` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| GET | `/v1/invoices/{id}` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| GET | `/v1/invoices/{id}/print` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| POST | `/v1/invoices/{id}:send` | BILLING_WRITE |
| POST | `/v1/invoices/{id}:void` | BILLING_WRITE |
| GET | `/v1/payment-methods` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| GET | `/v1/refunds` | BILLING_READ |
| POST | `/v1/refunds` | REFUND_WRITE |
| POST | `/v1/refunds/{id}:confirm` | REFUND_CONFIRM |
| GET | `/v1/shifts` | BILLING_READ |
| POST | `/v1/shifts` | BILLING_PAYMENT_WRITE |
| GET | `/v1/shifts/{id}/reconciliation` | BILLING_READ |
| POST | `/v1/shifts/{id}:close` | BILLING_PAYMENT_WRITE |

## BookingOperations

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/booking-change-requests` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| POST | `/v1/booking-change-requests/{id}:resolve` | BOOKING_WRITE |
| GET | `/v1/bookings/arrivals-departures` | BOOKING_READ |
| POST | `/v1/bookings/counter` | BOOKING_WRITE |
| GET | `/v1/bookings/search` | BOOKING_READ |
| GET | `/v1/bookings/{id}` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| POST | `/v1/bookings/{id}/change-requests` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| GET | `/v1/bookings/{id}/guests` | BOOKING_READ |
| GET | `/v1/bookings/{id}/history` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| POST | `/v1/bookings/{id}/invoice:prepare` | BILLING_WRITE |
| POST | `/v1/bookings/{id}/rooms/{detailId}/guests` | BOOKING_WRITE |
| POST | `/v1/bookings/{id}/rooms/{detailId}:move` | BOOKING_WRITE |
| POST | `/v1/bookings/{id}:reschedule` | BOOKING_WRITE |
| GET | `/v1/cancellation-requests` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| POST | `/v1/cancellation-requests/{id}:reject` | CANCELLATION_APPROVE |

## Bookings

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| POST | `/v1/bookings` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| PATCH | `/v1/bookings/{id}` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| POST | `/v1/bookings/{id}:cancel` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| POST | `/v1/bookings/{id}:check-in` | BOOKING_WRITE |
| POST | `/v1/bookings/{id}:check-out` | BOOKING_WRITE |
| POST | `/v1/bookings/{id}:confirm-deposit` | BILLING_PAYMENT_WRITE |
| POST | `/v1/bookings/{id}:mark-no-show` | BOOKING_WRITE |

## Customers

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/customers` | CUSTOMER_READ |
| GET | `/v1/customers/{id}` | CUSTOMER_READ |
| PATCH | `/v1/customers/{id}` | CUSTOMER_WRITE |

## DepositPayments

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| POST | `/v1/bookings/{id}/deposit-payment` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| GET | `/v1/deposit-payments` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| POST | `/v1/deposit-payments/{orderId}/inquiry` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |

## FrontDesk

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/front-desk/available-rooms` | BOOKING_READ |
| GET | `/v1/front-desk/bookings` | BOOKING_READ |
| POST | `/v1/front-desk/bookings` | BOOKING_WRITE |
| GET | `/v1/front-desk/bookings/{id}` | BOOKING_READ |
| GET | `/v1/front-desk/bookings/{id}/checkout-preview` | BILLING_READ |
| PUT | `/v1/front-desk/bookings/{id}/guests` | BOOKING_WRITE |
| POST | `/v1/front-desk/bookings/{id}:cancel` | BOOKING_WRITE |
| POST | `/v1/front-desk/bookings/{id}:change-dates` | BOOKING_WRITE |
| POST | `/v1/front-desk/bookings/{id}:change-room` | BOOKING_WRITE |
| POST | `/v1/front-desk/bookings/{id}:check-in` | BOOKING_WRITE |
| POST | `/v1/front-desk/bookings/{id}:check-out` | BOOKING_WRITE |
| POST | `/v1/front-desk/bookings/{id}:confirm-deposit` | BILLING_PAYMENT_WRITE |
| POST | `/v1/front-desk/bookings/{id}:mark-no-show` | BOOKING_WRITE |
| GET | `/v1/front-desk/cancellation-requests` | BOOKING_READ |
| POST | `/v1/front-desk/cancellation-requests/{id}:approve` | CANCELLATION_APPROVE |
| POST | `/v1/front-desk/cancellation-requests/{id}:reject` | CANCELLATION_APPROVE |
| GET | `/v1/front-desk/customers` | BOOKING_READ |
| GET | `/v1/front-desk/day` | BOOKING_READ |
| GET | `/v1/front-desk/payment-methods` | BOOKING_READ |

## HotelCatalog

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/amenities` | Công khai |
| POST | `/v1/amenities` | ROOM_TYPE_WRITE |
| GET | `/v1/hotels` | Công khai |
| POST | `/v1/hotels` | HOTEL_WRITE |
| PUT | `/v1/hotels/{id}` | HOTEL_WRITE |
| GET | `/v1/public/room-types` | Công khai |
| GET | `/v1/public/rooms/availability` | Công khai |
| GET | `/v1/public/rooms/{id}` | Công khai |
| GET | `/v1/public/rooms/{id}/price` | Công khai |
| PUT | `/v1/room-types/{id}` | ROOM_TYPE_WRITE |
| PUT | `/v1/room-types/{id}/amenities` | ROOM_TYPE_WRITE |
| POST | `/v1/room-types/{id}/images` | ROOM_TYPE_WRITE |
| DELETE | `/v1/room-types/{id}/images/{imageId}` | ROOM_TYPE_WRITE |
| GET | `/v1/rooms/calendar` | ROOM_READ |
| GET | `/v1/rooms/map` | ROOM_READ |
| PUT | `/v1/rooms/{id}` | ROOM_WRITE |
| POST | `/v1/rooms/{id}/images` | ROOM_WRITE |
| DELETE | `/v1/rooms/{id}/images/{imageId}` | ROOM_WRITE |
| PATCH | `/v1/rooms/{id}/status` | ROOM_WRITE |

## Housekeeping

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/housekeeping-tasks` | HOUSEKEEPING_READ |
| POST | `/v1/housekeeping-tasks/checkout` | HOUSEKEEPING_WRITE |
| POST | `/v1/room-conditions` | HOUSEKEEPING_WRITE |

## HousekeepingOperations

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| POST | `/v1/housekeeping-tasks` | HOUSEKEEPING_WRITE |
| GET | `/v1/housekeeping-tasks/{id}` | HOUSEKEEPING_READ |
| PUT | `/v1/housekeeping-tasks/{id}/assignee` | HOUSEKEEPING_ASSIGN |
| PATCH | `/v1/housekeeping-tasks/{id}/status` | HOUSEKEEPING_WRITE |
| GET | `/v1/room-conditions` | HOUSEKEEPING_READ |
| PATCH | `/v1/room-conditions/{id}` | HOUSEKEEPING_WRITE |

## Inventory

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/inventory-items` | INVENTORY_READ |
| POST | `/v1/inventory-items` | INVENTORY_WRITE |
| POST | `/v1/purchase-orders` | PURCHASE_WRITE |
| POST | `/v1/purchase-orders/{id}:confirm` | PURCHASE_CONFIRM |
| POST | `/v1/stock-issues` | STOCK_ISSUE_WRITE |
| POST | `/v1/stock-issues/{id}:confirm` | STOCK_ISSUE_CONFIRM |
| GET | `/v1/warehouses/{warehouseId}/inventory-items/{itemId}/balance` | INVENTORY_READ |

## InventoryManagement

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/inventory-alerts` | INVENTORY_READ |
| GET | `/v1/inventory-groups` | INVENTORY_READ |
| POST | `/v1/inventory-groups` | INVENTORY_WRITE |
| PUT | `/v1/inventory-groups/{id}` | INVENTORY_WRITE |
| GET | `/v1/inventory-items/{id}` | INVENTORY_READ |
| PUT | `/v1/inventory-items/{id}` | INVENTORY_WRITE |
| PATCH | `/v1/inventory-items/{id}/status` | INVENTORY_WRITE |
| GET | `/v1/inventory-lots` | INVENTORY_READ |
| GET | `/v1/inventory-units` | INVENTORY_READ |
| POST | `/v1/inventory-units` | INVENTORY_WRITE |
| PUT | `/v1/inventory-units/{id}` | INVENTORY_WRITE |
| GET | `/v1/purchase-orders` | INVENTORY_READ |
| GET | `/v1/purchase-orders/{id}` | INVENTORY_READ |
| PUT | `/v1/purchase-orders/{id}` | PURCHASE_WRITE |
| POST | `/v1/purchase-orders/{id}:cancel` | PURCHASE_WRITE |
| POST | `/v1/stock-adjustments` | STOCK_ADJUST |
| GET | `/v1/stock-issues` | INVENTORY_READ |
| GET | `/v1/stock-issues/{id}` | INVENTORY_READ |
| PUT | `/v1/stock-issues/{id}` | STOCK_ISSUE_WRITE |
| POST | `/v1/stock-issues/{id}:cancel` | STOCK_ISSUE_WRITE |
| GET | `/v1/stock-ledger` | INVENTORY_READ |
| GET | `/v1/suppliers` | INVENTORY_READ |
| POST | `/v1/suppliers` | INVENTORY_WRITE |
| GET | `/v1/suppliers/{id}` | INVENTORY_READ |
| PUT | `/v1/suppliers/{id}` | INVENTORY_WRITE |
| GET | `/v1/warehouses` | INVENTORY_READ |
| POST | `/v1/warehouses` | INVENTORY_WRITE |
| PUT | `/v1/warehouses/{id}` | INVENTORY_WRITE |

## MyAccount

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| POST | `/v1/auth/forgot-password` | Công khai |
| POST | `/v1/auth/logout` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| POST | `/v1/auth/reset-password` | Công khai |
| GET | `/v1/me` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| PUT | `/v1/me` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| GET | `/v1/me/bookings` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| POST | `/v1/me/change-password` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |

## Notifications

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/notifications` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| POST | `/v1/notifications` | NOTIFICATION_WRITE |
| GET | `/v1/notifications/settings` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| PUT | `/v1/notifications/settings` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| POST | `/v1/notifications/{id}:read` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |

## OperationalReports

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/reports/bookings` | REPORT_READ |
| GET | `/v1/reports/customers` | REPORT_READ |
| GET | `/v1/reports/inventory` | REPORT_READ |
| GET | `/v1/reports/operations` | REPORT_READ |
| GET | `/v1/reports/revenue-breakdown` | REPORT_READ |
| GET | `/v1/reports/services` | REPORT_READ |

## PagedResources

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/bookings` | BOOKING_READ |
| GET | `/v1/invoices` | BILLING_READ |
| GET | `/v1/transactions` | BILLING_READ |

## PricingPolicies

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/cancellation-policies` | Công khai |
| POST | `/v1/cancellation-policies` | PRICING_WRITE |
| PATCH | `/v1/cancellation-policies/{id}/status` | PRICING_WRITE |
| GET | `/v1/deposit-policies` | PRICING_WRITE |
| POST | `/v1/deposit-policies` | PRICING_WRITE |
| PATCH | `/v1/deposit-policies/{id}/status` | PRICING_WRITE |
| GET | `/v1/pricing-policies` | PRICING_WRITE |
| POST | `/v1/pricing-policies` | PRICING_WRITE |
| PUT | `/v1/pricing-policies/{id}` | PRICING_WRITE |
| GET | `/v1/surcharge-policies` | PRICING_WRITE |
| POST | `/v1/surcharge-policies` | PRICING_WRITE |
| PATCH | `/v1/surcharge-policies/{id}/status` | PRICING_WRITE |

## Reports

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/reports/dashboard` | REPORT_READ |
| GET | `/v1/reports/occupancy` | REPORT_READ |
| GET | `/v1/reports/revenue` | REPORT_READ |

## Reviews

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/reviews` | Công khai |
| POST | `/v1/reviews` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| GET | `/v1/reviews/manage` | REVIEW_MANAGE |
| GET | `/v1/reviews/mine` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| PUT | `/v1/reviews/{id}/reply` | REVIEW_MANAGE |
| PATCH | `/v1/reviews/{id}/status` | REVIEW_MANAGE |

## Roles

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/roles` | ROLE_READ |
| GET | `/v1/roles/{id}` | ROLE_READ |

## RoomAdmin

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/room-board` | ROOM_READ |
| GET | `/v1/room-board/types` | ROOM_TYPE_READ |
| GET | `/v1/room-calendar` | ROOM_READ |
| PATCH | `/v1/room-types/{id}` | ROOM_TYPE_WRITE |
| PATCH | `/v1/rooms/{id}` | ROOM_WRITE |
| POST | `/v1/rooms/{id}:set-status` | ROOM_WRITE |

## RoomTypes

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/room-types` | ROOM_TYPE_READ |
| POST | `/v1/room-types` | ROOM_TYPE_WRITE |
| GET | `/v1/room-types/{id}` | ROOM_TYPE_READ |

## Rooms

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/rooms` | ROOM_READ |
| POST | `/v1/rooms` | ROOM_WRITE |
| GET | `/v1/rooms/{id}` | ROOM_READ |
| GET | `/v1/rooms/{id}/price` | ROOM_READ |

## Services

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/service-invoices` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| GET | `/v1/service-invoices/{id}` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| POST | `/v1/service-invoices/{id}:pay` | BILLING_PAYMENT_WRITE |
| POST | `/v1/service-invoices/{id}:void` | BILLING_WRITE |
| POST | `/v1/service-requests` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| GET | `/v1/service-requests` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| PATCH | `/v1/service-requests/{id}/status` | SERVICE_WRITE |
| GET | `/v1/services` | Công khai |
| POST | `/v1/services` | SERVICE_CATALOG_WRITE |
| GET | `/v1/services/manage` | SERVICE_READ |
| PUT | `/v1/services/{id}` | SERVICE_CATALOG_WRITE |

## Staff

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/staff` | STAFF_READ |
| GET | `/v1/staff/{id}` | STAFF_READ |

## VietQrPaymentManagement

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| GET | `/v1/payments/vietqr` | VIETQR_PAYMENT_READ |
| PUT | `/v1/payments/vietqr/{orderId}/note` | VIETQR_PAYMENT_NOTE_WRITE |
| PATCH | `/v1/payments/vietqr/{orderId}/status` | VIETQR_PAYMENT_STATUS_WRITE |

## VietQrPayments

| Method | Endpoint | Quyền truy cập |
|---|---|---|
| POST | `/v1/invoices/{invoiceId}/vietqr-payment` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
| POST | `/v1/payments/vietqr/{orderId}/inquiry` | JWT; kiểm tra chủ sở hữu/quyền trong nghiệp vụ |
