# Order Ready → Email/SMS: Why You Don’t See a “Notification” Call in the Browser

## You’re not missing anything

When you change an order’s status to **Ready** in the web app:

- In **browser DevTools** you will only see:
  - **PUT** to **Web BFF** (e.g. `.../WebBff/orders/{orderId}/status`).
- There is **no** HTTP request from the browser (or BFF) to the NotificationService. That’s by design.

Notifications are sent by a **background process** that listens to **RabbitMQ**, not by a direct API call when you click “Ready”.

---

## How it actually works

```
[Browser]  →  PUT /WebBff/orders/{id}/status  →  [Web BFF]  →  PUT /api/Order/{id}/status  →  [OrderService]
                                                                                                    │
                                                                                                    ▼
                                                                                            Update DB + publish event
                                                                                                    │
                                                                                                    ▼
[RabbitMQ]  ←  message "order.status.changed"  ←  OrderService
     │
     │  (NotificationService is already running and consuming from the queue)
     ▼
[NotificationService]  →  GET CustomerService/by-user/{userId}  →  Send Email/SMS (Mailgun/Vonage)
```

So:

1. **Browser** only talks to **Web BFF** (status update).
2. **OrderService** updates the order and **publishes** to **RabbitMQ**.
3. **NotificationService** (running in the background) **consumes** from RabbitMQ, then calls CustomerService and sends email/SMS.

That’s why you don’t see any “call to notification” in the browser.

---

## What to check when email/SMS doesn’t arrive

### 1. RabbitMQ is running

OrderService and NotificationService both need RabbitMQ (default: `localhost:5672`).

```bash
# If using Docker:
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
# Management UI: http://localhost:15672 (guest/guest)
```

If RabbitMQ is down, OrderService will log something like:  
`Failed to publish order status changed event` (and the exception will show connection refused, etc.).

### 2. OrderService really publishes

When you set status to **Ready**, in the **OrderService** console look for:

- **Success:**  
  `UpdateOrderStatusAsync: Order status changed event published - OrderId=..., OldStatus=..., NewStatus=Ready`
- **Failure:**  
  `Failed to publish order status changed event` (with exception details).

If you see the failure line, fix RabbitMQ (or config) first.

### 3. NotificationService is running and consuming

NotificationService must be **running** (e.g. `dotnet run` in TraditionalEats.NotificationService).

On startup you should see:

- `OrderStatusEventHandler started, waiting for messages...`

When an order is set to Ready, you should then see:

- `Received message: order.status.changed, {...}`
- `Handling order status changed: OrderId=..., NewStatus=Ready`
- Then either `Email notification sent` / `SMS notification sent` or warnings (e.g. customer not found, email disabled).

If you never see `Received message: order.status.changed`, then either:

- RabbitMQ wasn’t running when you set status, or
- NotificationService wasn’t running, or
- OrderService failed to publish (see step 2).

### 4. Customer has email/phone

NotificationService calls **CustomerService** at:

- `GET /api/customer/by-user/{userId}`  
  (the `userId` is the order’s `CustomerId`).

If that returns 404 or a customer without Email/PhoneNumber, you’ll get a log like “Customer not found” or no email/SMS. Ensure that user has a customer record with email/phone (e.g. from registration/profile).

### 5. Email/SMS config

In **NotificationService** `appsettings.Development.json` (or equivalent):

- **Email:** `Email:Enabled` = true, Mailgun (or SMTP) configured.
- **SMS:** Vonage configured if you want SMS.

Check NotificationService logs for “Email sending is disabled”, “Mailgun API error”, or similar.

---

## Quick checklist

| Step | What to check                                                                                      |
| ---- | -------------------------------------------------------------------------------------------------- |
| 1    | RabbitMQ running on localhost:5672                                                                 |
| 2    | OrderService running; on status → Ready, log shows “event published” (no “Failed to publish”)      |
| 3    | NotificationService running; on status → Ready, log shows “Received message: order.status.changed” |
| 4    | CustomerService running; GET `/api/customer/by-user/{userId}` returns 200 with email/phone         |
| 5    | NotificationService config has Email (and SMS if desired) enabled and correct                      |

You are not missing anything in the browser: the only call you should see when changing status to Ready is the PUT to Web BFF. The rest happens via RabbitMQ and the NotificationService background process.
