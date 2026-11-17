# IcbsMock - local ICBS / BREB e-Payment API simulator

Test tool for the BillCollection BREB integration while the real BREB access
(credentials, VPN, IP whitelisting) is not available yet.

* Follows **API Specification v1.5 (14-Aug-2025)**: endpoints, request fields, response bodies,
  status codes (200/202/400/401/404/500) and messages.
* Things the document does not define are marked `ASSUMPTION` in `Program.cs` (see list below).
* It is a **simulator, not BREB**. When real access arrives, repeat the scenarios below against
  BREB's test environment and compare.

## Run

```
cd IcbsMock
dotnet run
```

The simulator always listens on **http://localhost:5099** (Visual Studio / `ASPNETCORE_URLS` /
launchSettings cannot change it). Another port: `dotnet run -- --Mock:Port=6000`.

The console prints the API key, username, password and the test accounts.

Visual Studio: open `IcbsMock.csproj` in its own window and press `Ctrl + F5`.

## Point BillCollection at it

Put this in `appsettings.Development.json` (already git-ignored) - do NOT touch the live values
in `appsettings.json`:

```json
{
  "BREB": {
    "BaseUrl": "http://localhost:5099/",
    "ApiKey": "MOCK-API-KEY",
    "Username": "bkb_mock",
    "Password": "mock-pass-123",
    "UseMock": false,
    "PayerTypeId": 6,
    "AutoAcknowledge": false
  }
}
```

* The simulator requires `BranchRoutingNo` for bank payer types, so every branch you test with
  needs a `RoutingNumber` in the `Branches` table.
* The real API is HTTPS only; the simulator uses plain http on localhost. The client accepts any BaseUrl.

## Test accounts (BillType 50 = Electricity Bill)

| SMS account                      | Result of /billinginfo                    |
|----------------------------------|-------------------------------------------|
| 1010032036503                    | 200, Normal, 328.00, VAT 16               |
| 1010032036504                    | 200, LPC, 345.00, VAT 17                  |
| 1010032036505                    | 202.1 Bill already paid                   |
| 1010032036506                    | 202.4 LPC date is over                    |
| 1010032036507                    | 200, Normal, 1500.00, VAT 75              |
| 1010032036510 ... 1010032036539  | 30 more Normal bills, 215.00 to 7000.00 (VAT = 5 %) |
| 2020000000001                    | BillType 20 (Demand Note), 5000.00        |
| anything else                    | 404.1 Billing information not found       |

`GET /_mock/bills` lists every account the simulator currently knows.
A bill that has been paid (acknowledged) answers 202.1 afterwards; `/_mock/reset` makes all bills payable again.

### Your own demo accounts: `demo-bills.json`

Edit `IcbsMock/demo-bills.json` (a JSON array) - no code change needed. The file is read at start-up and
again on every `/_mock/reset`, so after editing it just open `http://localhost:<port>/_mock/reset`.

```json
[
  { "SmsAccountNo": "1010032036601", "Amount": 450 },
  { "SmsAccountNo": "1010032036602", "Amount": 1200, "DueType": "LPC", "Vat": 60 },
  { "SmsAccountNo": "1010032036603", "Amount": 3300, "BillMonth": "Oct-2026" }
]
```

| Field          | Required | Default |
|----------------|----------|---------|
| SmsAccountNo   | yes      | -       |
| Amount         | yes      | -       |
| BillType       | no       | 50 (10, 19, 20, 30, 50 allowed) |
| DueType        | no       | Normal  (or LPC) |
| BillMonth      | no       | last month, e.g. Oct-2026 |
| Vat            | no       | 5 % of Amount, rounded |
| Paid           | no       | false (true = answers 202.1) |
| LpcOver        | no       | false (true = answers 202.4) |

An entry with the same account and BillType as a built-in bill replaces it. A broken file is ignored
and the reason is printed in the console / returned by `/_mock/reset`.

## Admin / fault-injection endpoints (GET or POST)

| URL | What it does |
|-----|--------------|
| `/_mock/state` | Dump of all references, statuses, login count, armed faults |
| `/_mock/reset` | Back to the initial data (re-reads `demo-bills.json`) |
| `/_mock/bills` | All accounts the simulator knows |
| `/_mock/expire-ack` | Closes the acknowledgement window of all submitted payments (`?refNo=` for one) |
| `/_mock/resolve-disputes` | Simulates BREB staff: dispute 5 -> Paid Acknowledged (50), dispute 10 -> Reset (8) |
| `/_mock/revoke-access-tokens` | Invalidates access tokens (refresh tokens stay valid) |
| `/_mock/fault?endpoint=E&mode=M&when=W&count=N&delay=S` | One-shot failure. E = billinginfo, paymentsubmit, paymentacknowledgment, paymentstatus, dispute, reportdetail, reportsummary. M = `timeout`, `http500`, `garbage` (HTML error page). W = `before` (not processed) or `after` (processed, but the caller never learns) |
| `/_mock/config?alwaysHttp200=true&ackMinutes=5&lockMinutes=5` | HTTP 200 for everything / change the windows |

## Scenarios worth running through the BillCollection screens

1. **Normal**: query 1010032036503 -> Pay Bill -> Acknowledge -> status Success.
2. **Missed acknowledgement window**: Pay Bill, call `/_mock/expire-ack`, press Acknowledge
   -> "Acknowledgement time is over", status Pending -> Reconcile Report -> *Dispute: Acknowledge*
   -> call `/_mock/resolve-disputes` -> *Check & Sync* -> Success.
3. **Submit timeout** (BillCollection `TimeoutSeconds` is 60): call
   `/_mock/fault?endpoint=paymentsubmit&mode=timeout&when=after&delay=70`, then Pay Bill
   -> "No confirmation received", status Pending -> *Check & Sync* -> BREB says Waiting (30) -> Acknowledge.
4. **Submit rejected**: query a bill, change nothing, query the same bill as another user -> 202.3;
   or pay a bill twice -> 202.2 / 202.1 messages appear in the red banner.
5. **Token handling**: `/_mock/revoke-access-tokens` in the middle of work - the next call must
   still succeed (client renews with the refresh token; `/_mock/state` shows `authCount` unchanged).
6. **BREB unreachable**: stop the simulator -> friendly "unreachable ... VPN" message.

## ASSUMPTIONS (not defined by the specification)

* The same payer querying the same bill again gets the same `RefNo` and the lock is renewed;
  another payer gets 202.3.
* Only username/password logins count towards `AuthenticationLimit`, not refresh-token logins.
* Acknowledging something that was never submitted -> 404.1.
* `Dispute`: an invalid `DisputeType` -> 400.2; dispute while the acknowledgement window is still
  open -> 202.4 "Bill Payment is in progress"; dispute on a payment of an earlier date -> 400.2.
* `report/detail` with `CollectionStatus` 30 also lists disputed bills (Message: "Submitted for Dispute").
* Date parameters must be `yyyy-MM-dd`, otherwise 400.2.
* HTTP status code equals the `Status` in the body (use `alwaysHttp200` to test the other style).
