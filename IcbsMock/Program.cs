// =====================================================================================
//  IcbsMock - local simulator of the ICBS / BREB e-Payment API (spec v1.5, 14-Aug-2025)
//
//  Purpose : develop and test the BillCollection integration (UseMock = false) before the
//            real BREB credentials / VPN / IP whitelisting are available.
//  Rules   : the response bodies, status codes and messages follow the specification
//            document. Behaviour the document does NOT define is marked "ASSUMPTION".
//  Warning : this is a test tool, NOT a copy of BREB's real server.
// =====================================================================================
using System.Globalization;
using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);
// Fixed port (default 5099; change with  --Mock:Port=6000).  This deliberately IGNORES ASPNETCORE_URLS /
// launchSettings.json, so Visual Studio can never move the simulator to a random port.
builder.WebHost.UseUrls($"http://localhost:{builder.Configuration["Mock:Port"] ?? "5099"}");
var app = builder.Build();

var S = new MockState(app.Configuration, app.Environment.ContentRootPath);

Console.WriteLine("==============================================================");
Console.WriteLine(" ICBS MOCK SERVER  (test tool - not the real BREB)");
Console.WriteLine($" API key  : {S.ApiKey}");
Console.WriteLine($" Username : {S.Username}");
Console.WriteLine($" Password : {S.Password}");
Console.WriteLine(" Test SMS accounts (BillType 50):");
Console.WriteLine("   1010032036503  Normal  328.00 (VAT 16)     -> 200");
Console.WriteLine("   1010032036504  LPC     345.00 (VAT 17)     -> 200");
Console.WriteLine("   1010032036505  already paid                -> 202.1");
Console.WriteLine("   1010032036506  LPC date over               -> 202.4");
Console.WriteLine("   1010032036507  Normal  1500.00 (VAT 75)    -> 200");
Console.WriteLine("   anything else                              -> 404.1");
Console.WriteLine("   1010032036510 ... 1010032036539  30 more Normal bills (215 to 7000)");
Console.WriteLine(" BillType 20 account: 2020000000001 (5000.00)");
Console.WriteLine($" Bills loaded: {S.Bills.Count}  ({S.LoadMessage})");
Console.WriteLine(" All accounts: /_mock/bills");
Console.WriteLine(" Admin / fault injection: /_mock/state  (see README.md)");
Console.WriteLine("==============================================================");

// ------------------------------------------------------------------ admin endpoints
app.MapMethods("/_mock/state", new[] { "GET" }, () => { lock (S.Gate) return Results.Json(S.Dump()); });

app.MapMethods("/_mock/reset", new[] { "GET", "POST" }, () =>
{
    lock (S.Gate) { S.Reset(); return Results.Json(new { reset = true, bills = S.Bills.Count, file = S.LoadMessage }); }
});

app.MapMethods("/_mock/bills", new[] { "GET" }, () =>
{
    lock (S.Gate)
        return Results.Json(S.Bills.Select(b => new
        {
            account = b.Sms, billType = b.BillType, b.DueType, b.BillMonth, b.Amount, b.Vat, b.Paid, b.LpcOver
        }));
});

app.MapMethods("/_mock/revoke-access-tokens", new[] { "GET", "POST" }, () =>
{
    lock (S.Gate) { S.AccessTokens.Clear(); }
    return Results.Json(new { revoked = "access tokens (refresh tokens kept)" });
});

app.MapMethods("/_mock/expire-ack", new[] { "GET", "POST" }, (string? refNo) =>
{
    lock (S.Gate)
    {
        var n = 0;
        foreach (var r in S.Refs.Where(r => r.Status == 30 && (refNo == null || r.RefNo == refNo)))
        {
            r.AckExpiresAt = DateTime.Now.AddMinutes(-1);
            n++;
        }
        return Results.Json(new { acknowledgementWindowClosed = n });
    }
});

// Simulates BREB staff processing the disputes: 5 -> Paid Acknowledged, 10 -> Reset
app.MapMethods("/_mock/resolve-disputes", new[] { "GET", "POST" }, () =>
{
    lock (S.Gate)
    {
        var n = 0;
        foreach (var r in S.Refs.Where(r => r.Status == 9))
        {
            if (r.DisputeType == 5) { r.Status = 50; r.Bill.Paid = true; }
            else { r.Status = 8; r.Bill.Paid = false; }
            n++;
        }
        return Results.Json(new { resolved = n });
    }
});

// /_mock/fault?endpoint=paymentsubmit&mode=timeout|http500|garbage&when=before|after&count=1&delay=15
app.MapMethods("/_mock/fault", new[] { "GET", "POST" }, (string endpoint, string mode, string? when, int? count, int? delay) =>
{
    lock (S.Gate)
    {
        S.Faults[endpoint.ToLowerInvariant()] = new Fault(mode.ToLowerInvariant(), (when ?? "after").ToLowerInvariant(), count ?? 1, delay ?? 15);
        return Results.Json(new { armed = endpoint, mode, when = when ?? "after", count = count ?? 1 });
    }
});

app.MapMethods("/_mock/config", new[] { "GET", "POST" }, (bool? alwaysHttp200, int? ackMinutes, int? lockMinutes) =>
{
    lock (S.Gate)
    {
        if (alwaysHttp200.HasValue) S.AlwaysHttp200 = alwaysHttp200.Value;
        if (ackMinutes.HasValue) S.AckMinutes = ackMinutes.Value;
        if (lockMinutes.HasValue) S.LockMinutes = lockMinutes.Value;
        return Results.Json(new { S.AlwaysHttp200, S.AckMinutes, S.LockMinutes });
    }
});

// ------------------------------------------------------------------ 3. /authenticate
app.MapPost("/authenticate", async (HttpContext ctx) =>
{
    JsonObject body;
    try { body = (await ctx.Request.ReadFromJsonAsync<JsonObject>()) ?? new JsonObject(); }
    catch { return S.Err(400, ("400.1", "Request body is not valid JSON.")); }

    var apiKey = ctx.Request.Headers["apikey"].ToString();
    var refresh = MockState.Str(body, "RefreshToken");
    var user = MockState.Str(body, "Username");
    var pass = MockState.Str(body, "Password");

    lock (S.Gate)
    {
        var errs = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(apiKey)) errs.Add(("400.1", "API Key is required."));
        if (string.IsNullOrWhiteSpace(refresh))
        {
            if (string.IsNullOrWhiteSpace(user)) errs.Add(("400.1", "Username is required."));
            if (string.IsNullOrWhiteSpace(pass)) errs.Add(("400.1", "Password is required."));
        }
        if (errs.Count > 0) return S.Err(400, errs.ToArray());

        if (apiKey != S.ApiKey) return S.Err(401, ("401.1", "API Key is invalid."));

        var now = DateTime.Now;

        if (!string.IsNullOrWhiteSpace(refresh))
        {
            if (!S.RefreshTokens.TryGetValue(refresh!, out var rexp) || rexp < now)
                return S.Err(401, ("401.1", "Refresh token is invalid or expired."));

            var token = MockState.NewToken();
            S.AccessTokens[token] = now.AddMinutes(S.TokenMinutes);

            // refresh login returns only these three fields (spec 3.2)
            return S.Ok(new JsonObject
            {
                ["AccessToken"] = token,
                ["ExpiresIn"] = S.TokenMinutes,
                ["CreateTimestamp"] = MockState.Ts(now)
            });
        }

        if (user != S.Username || pass != S.Password)
            return S.Err(401, ("401.1", "Username or password is invalid."));

        // ASSUMPTION: only username/password logins count against the daily limit.
        if (S.AuthCount >= S.AuthLimit)
            return S.Err(401, ("401.1", "Authentication limit exceeded."));
        S.AuthCount++;

        var access = MockState.NewToken();
        var rt = MockState.NewToken();
        S.AccessTokens[access] = now.AddMinutes(S.TokenMinutes);
        S.RefreshTokens[rt] = now.AddHours(6);

        return S.Ok(new JsonObject
        {
            ["AccessToken"] = access,
            ["ExpiresIn"] = S.TokenMinutes,
            ["CreateTimestamp"] = MockState.Ts(now),
            ["RefreshToken"] = rt,
            ["RefreshTokenExpiresAt"] = MockState.Ts(now.AddHours(6)),
            ["AuthenticationLimit"] = $"{S.AuthCount} of {S.AuthLimit}"
        });
    }
});

// ------------------------------------------------------------------ protected endpoints
// (explicit Func<> cast: otherwise the lambda binds to RequestDelegate and the IResult is discarded)
void Post(string path, string name, Func<JsonObject, IResult> handler) =>
    app.MapPost(path, (Func<HttpContext, Task<IResult>>)(c => S.Handle(c, name, handler)));

Post("/billinginfo", "billinginfo", S.BillingInfo);
Post("/billinginfo/paymentsubmit", "paymentsubmit", S.PaymentSubmit);
Post("/billinginfo/paymentacknowledgment", "paymentacknowledgment", S.PaymentAcknowledgment);
Post("/billinginfo/paymentstatus", "paymentstatus", S.PaymentStatus);
Post("/billinginfo/dispute", "dispute", S.Dispute);
Post("/billinginfo/checkbill", "checkbill", S.CheckBill);
Post("/report/detail", "reportdetail", S.ReportDetail);
Post("/report/summary", "reportsummary", S.ReportSummary);
// note: the spec writes "/report/summary/"; ASP.NET treats the trailing slash as the same route
Post("/authenticate/changepassword", "changepassword", S.ChangePassword);

app.Run();

// =====================================================================================
//  state + business rules
// =====================================================================================
record Fault(string Mode, string When, int Remaining, int DelaySeconds);

class BillDef
{
    public string Sms = "";
    public int BillType;
    public string DueType = "Normal";
    public string BillMonth = "";
    public decimal Amount;
    public decimal Vat;
    public bool Paid;
    public bool LpcOver;
}

class RefRec
{
    public string RefNo = "";
    public BillDef Bill = null!;
    public string PayerId = "";
    public int PayerTypeId;
    public string? Routing;
    public string? AgentId;
    public DateTime LockExpiresAt;
    // 10 Pending | 30 Waiting for Acknowledge | 50 Paid Acknowledged | 9 Submitted for Dispute | 8 Reset
    public int Status = 10;
    public string? PayerTxnId;
    public DateTime? AckExpiresAt;
    public int? DisputeType;
    public DateTime PaymentDate;
}

class MockState
{
    public readonly object Gate = new();

    public string ApiKey, Username, Password;
    public int TokenMinutes = 15;
    public int AuthLimit = 100;
    public int AuthCount;
    public int AckMinutes = 5;     // time allowed between paymentsubmit and paymentacknowledgment
    public int LockMinutes = 5;    // ExpiresIn of /billinginfo for electricity bills
    public bool AlwaysHttp200;     // some servers answer HTTP 200 and put the code only in the body

    public Dictionary<string, DateTime> AccessTokens = new();
    public Dictionary<string, DateTime> RefreshTokens = new();
    public Dictionary<string, Fault> Faults = new();
    public List<BillDef> Bills = new();
    public List<RefRec> Refs = new();

    static readonly HashSet<int> MfsTypes = new() { 1, 3, 11, 12 };
    static readonly HashSet<int> AgentTypes = new() { 1, 2, 8, 10, 11 };
    static readonly HashSet<int> BillTypes = new() { 10, 19, 20, 30, 50 };

    public string ContentRoot;
    public string LoadMessage = "";

    public MockState(IConfiguration cfg, string contentRoot)
    {
        ContentRoot = contentRoot;
        ApiKey = cfg["Mock:ApiKey"] ?? "MOCK-API-KEY";
        Username = cfg["Mock:Username"] ?? "bkb_mock";
        Password = cfg["Mock:Password"] ?? "mock-pass-123";
        Reset();
    }

    public void Reset()
    {
        AccessTokens.Clear(); RefreshTokens.Clear(); Faults.Clear(); Refs.Clear(); AuthCount = 0;
        var month = DateTime.Now.AddMonths(-1).ToString("MMM-yyyy", CultureInfo.InvariantCulture);
        Bills = new List<BillDef>
        {
            new() { Sms = "1010032036503", BillType = 50, DueType = "Normal", BillMonth = month, Amount = 328m,  Vat = 16m },
            new() { Sms = "1010032036504", BillType = 50, DueType = "LPC",    BillMonth = month, Amount = 345m,  Vat = 17m },
            new() { Sms = "1010032036505", BillType = 50, DueType = "Normal", BillMonth = month, Amount = 500m,  Vat = 25m, Paid = true },
            new() { Sms = "1010032036506", BillType = 50, DueType = "LPC",    BillMonth = month, Amount = 410m,  Vat = 20m, LpcOver = true },
            new() { Sms = "1010032036507", BillType = 50, DueType = "Normal", BillMonth = month, Amount = 1500m, Vat = 75m },
            new() { Sms = "2020000000001", BillType = 20, Amount = 5000m },
        };

        // 30 more NORMAL bills for demos: 1010032036510 .. 1010032036539 (VAT = 5 % of the amount)
        var amounts = new decimal[]
        {
            215, 342, 478, 560, 633, 725, 810, 945, 1120, 1250, 1380, 1495, 1620, 1780, 1905,
            2100, 2350, 2575, 2800, 3050, 3300, 3600, 3925, 4200, 4550, 4900, 5300, 5750, 6200, 7000
        };
        for (var i = 0; i < amounts.Length; i++)
            Bills.Add(new BillDef
            {
                Sms = (1010032036510L + i).ToString(CultureInfo.InvariantCulture),
                BillType = 50,
                DueType = "Normal",
                BillMonth = month,
                Amount = amounts[i],
                Vat = Math.Round(amounts[i] * 0.05m, 0, MidpointRounding.AwayFromZero)
            });

        LoadDemoFile(month);
    }

    // Optional demo-bills.json (next to Program.cs): your own accounts, read again on every /_mock/reset
    static bool Bool(JsonObject o, string name) => Prop(o, name) is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    void LoadDemoFile(string month)
    {
        LoadMessage = "demo-bills.json not found (optional)";
        var path = Path.Combine(ContentRoot, "demo-bills.json");
        if (!File.Exists(path)) return;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonArray arr)
            {
                LoadMessage = "demo-bills.json must be a JSON array - ignored";
                return;
            }

            var n = 0;
            foreach (var item in arr.OfType<JsonObject>())
            {
                var sms = Str(item, "SmsAccountNo");
                var amount = Dec(item, "Amount");
                if (string.IsNullOrWhiteSpace(sms) || amount == null) continue;

                var type = Int(item, "BillType") ?? 50;
                Bills.RemoveAll(x => x.Sms == sms && x.BillType == type);      // the file wins over built-in data
                Bills.Add(new BillDef
                {
                    Sms = sms!,
                    BillType = type,
                    DueType = Str(item, "DueType") ?? "Normal",
                    BillMonth = Str(item, "BillMonth") ?? month,
                    Amount = amount.Value,
                    Vat = Dec(item, "Vat") ?? Math.Round(amount.Value * 0.05m, 0, MidpointRounding.AwayFromZero),
                    Paid = Bool(item, "Paid"),
                    LpcOver = Bool(item, "LpcOver")
                });
                n++;
            }
            LoadMessage = $"demo-bills.json: {n} bill(s) loaded";
        }
        catch (Exception ex)
        {
            LoadMessage = "demo-bills.json ignored - " + ex.Message;
        }
    }

    // ------------------------------------------------------------ helpers
    public static string Ts(DateTime d) => d.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
    public static string NewToken() => Convert.ToBase64String(Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray())
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    static JsonNode? Prop(JsonObject b, string name) =>
        b.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    public static string? Str(JsonObject b, string name)
    {
        var n = Prop(b, name);
        if (n is null) return null;
        if (n is JsonValue v && v.TryGetValue<string>(out var s)) return s;
        return n.ToString();
    }

    static int? Int(JsonObject b, string name) => int.TryParse(Str(b, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;
    static decimal? Dec(JsonObject b, string name) => decimal.TryParse(Str(b, name), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    IResult Env(int status, JsonObject data, JsonArray? errors) =>
        Results.Json(new JsonObject { ["Status"] = status, ["Data"] = data, ["Errors"] = errors ?? new JsonArray() },
            statusCode: AlwaysHttp200 ? 200 : status);

    public IResult Ok(JsonObject data) => Env(200, data, null);

    public IResult Err(int status, params (string code, string msg)[] errors)
    {
        var arr = new JsonArray();
        foreach (var (code, msg) in errors) arr.Add(new JsonObject { ["Code"] = code, ["Message"] = msg });
        return Env(status, new JsonObject(), arr);
    }

    public object Dump() => new
    {
        authCount = AuthCount,
        authLimit = AuthLimit,
        activeAccessTokens = AccessTokens.Count(t => t.Value > DateTime.Now),
        armedFaults = Faults,
        config = new { AlwaysHttp200, AckMinutes, LockMinutes },
        refs = Refs.Select(r => new
        {
            r.RefNo, sms = r.Bill.Sms, billType = r.Bill.BillType, r.Status, r.PayerId, r.PayerTxnId, amount = r.Bill.Amount,
            lockExpiresAt = Ts(r.LockExpiresAt), ackExpiresAt = r.AckExpiresAt.HasValue ? Ts(r.AckExpiresAt.Value) : null,
            r.DisputeType, paymentDate = r.PaymentDate.ToString("yyyy-MM-dd")
        })
    };

    // ------------------------------------------------------------ request pipeline for protected endpoints
    public async Task<IResult> Handle(HttpContext ctx, string name, Func<JsonObject, IResult> handler)
    {
        var auth = ctx.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || auth.Length < 8)
            return Err(401, ("401", "Unauthorized"));              // null / invalid format of access token

        lock (Gate)
        {
            var token = auth[7..].Trim();
            if (!AccessTokens.TryGetValue(token, out var exp) || exp < DateTime.Now)
                return Err(401, ("401.1", "Invalid Token."));
        }

        JsonObject body;
        try { body = (await ctx.Request.ReadFromJsonAsync<JsonObject>()) ?? new JsonObject(); }
        catch { return Err(400, ("400.1", "Request body is not valid JSON.")); }

        Fault? fault = null;
        lock (Gate)
        {
            if (Faults.TryGetValue(name, out var f))
            {
                fault = f;
                if (f.Remaining <= 1) Faults.Remove(name); else Faults[name] = f with { Remaining = f.Remaining - 1 };
            }
        }

        if (fault is { When: "before" })
        {
            var r = await ApplyFault(fault, ctx);
            if (r != null) return r;
        }

        IResult result;
        lock (Gate) { result = handler(body); }

        if (fault is { When: "after" })
        {
            // the request WAS processed; the caller just never learns about it
            var r = await ApplyFault(fault, ctx);
            if (r != null) return r;
        }

        return result;
    }

    async Task<IResult?> ApplyFault(Fault f, HttpContext ctx)
    {
        switch (f.Mode)
        {
            case "timeout":
                await Task.Delay(TimeSpan.FromSeconds(f.DelaySeconds), ctx.RequestAborted);
                return null;
            case "http500":
                return Err(500, ("500.1", "Simulated internal error. Please try again later."));
            case "garbage":
                return Results.Content("<html><body>502 Bad Gateway (simulated)</body></html>", "text/html", statusCode: 502);
            default:
                return null;
        }
    }

    // ------------------------------------------------------------ 4. /billinginfo
    bool IsActive(RefRec r) => r.Status == 30 || r.Status == 9 || (r.Status == 10 && r.LockExpiresAt > DateTime.Now);

    JsonObject BillData(RefRec r)
    {
        var b = r.Bill;
        var o = new JsonObject
        {
            ["SMSAccountNo"] = b.Sms,
            ["RefNo"] = r.RefNo,
            ["PaymentTimestamp"] = Ts(DateTime.Now)
        };
        if (b.BillType == 50)
        {
            o["DueType"] = b.DueType;
            o["BillMonth"] = b.BillMonth;
            o["Amount"] = b.Amount;
            o["Vat"] = b.Vat;
            o["ExpiresIn"] = LockMinutes;
        }
        else
        {
            o["Amount"] = b.Amount;
            o["ExpiresIn"] = 14;
        }
        return o;
    }

    public IResult BillingInfo(JsonObject b)
    {
        var sms = Str(b, "SMSAccountNo");
        var billType = Int(b, "BillType");
        var payerId = Str(b, "PayerId");
        var ptype = Int(b, "PayerTypeId");
        var routing = Str(b, "BranchRoutingNo");
        var agent = Str(b, "AgentId");

        var errs = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(sms)) errs.Add(("400.1", "SMS Account No. is required."));
        if (billType == null) errs.Add(("400.1", "Bill Type is required."));
        if (string.IsNullOrWhiteSpace(payerId)) errs.Add(("400.1", "Payer Id is required."));
        if (ptype == null) errs.Add(("400.1", "Payer Type Id is required."));
        if (ptype != null && !MfsTypes.Contains(ptype.Value) && string.IsNullOrWhiteSpace(routing))
            errs.Add(("400.1", "Branch Routing Number is required."));
        if (ptype != null && AgentTypes.Contains(ptype.Value) && string.IsNullOrWhiteSpace(agent))
            errs.Add(("400.1", "Agent ID is required."));
        if (errs.Count > 0) return Err(400, errs.ToArray());

        if (!BillTypes.Contains(billType!.Value)) return Err(401, ("401.2", "You don't have permission to pay this bill type."));
        if (ptype! < 1 || ptype > 12) return Err(401, ("401.2", "You don't have permission to pay for this payer type."));

        var bill = Bills.FirstOrDefault(x => x.Sms == sms && x.BillType == billType);
        if (bill == null) return Err(404, ("404.1", "Billing information not found."));
        if (bill.Paid) return Err(202, ("202.1", "Bill already paid."));
        if (bill.LpcOver) return Err(202, ("202.4", "LPC date is over. Please wait for next Bill."));

        var active = Refs.FirstOrDefault(r => r.Bill == bill && IsActive(r));
        if (active != null)
        {
            if (active.Status == 30) return Err(202, ("202.2", "Bill already submitted, waiting for acknowledge."));
            if (active.Status == 9) return Err(202, ("202.2", "Submitted for dispute."));
            if (active.PayerId != payerId) return Err(202, ("202.3", "Other user requests found for this particular bill number."));

            // ASSUMPTION: the same payer asking again gets the same RefNo and the lock is renewed.
            active.LockExpiresAt = DateTime.Now.AddMinutes(bill.BillType == 50 ? LockMinutes : 14);
            return Ok(BillData(active));
        }

        var g = Guid.NewGuid().ToString();   // RefNo is an opaque string; this mimics the format seen in production
        var rec = new RefRec
        {
            RefNo = $"5310Q2214Q{DateTime.Now:ddMM}{g[..8]}Q{g[9..13]}Q{g[14..18]}Q{g[19..23]}Q{g[24..]}",
            Bill = bill,
            PayerId = payerId!,
            PayerTypeId = ptype!.Value,
            Routing = routing,
            AgentId = agent,
            LockExpiresAt = DateTime.Now.AddMinutes(bill.BillType == 50 ? LockMinutes : 14),
            PaymentDate = DateTime.Now.Date
        };
        Refs.Add(rec);
        return Ok(BillData(rec));
    }

    // ------------------------------------------------------------ 5. /billinginfo/paymentsubmit
    public IResult PaymentSubmit(JsonObject b)
    {
        var sms = Str(b, "SMSAccountNo");
        var refNo = Str(b, "RefNo");
        var ptid = Str(b, "PayerTransactionId");
        var amount = Dec(b, "Amount");

        var errs = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(sms)) errs.Add(("400.1", "SMS Account No. is required."));
        if (string.IsNullOrWhiteSpace(refNo)) errs.Add(("400.1", "Ref. No. is required."));
        if (string.IsNullOrWhiteSpace(ptid)) errs.Add(("400.1", "Payer Transaction Id is required."));
        if (amount == null) errs.Add(("400.1", "Amount is required."));
        if (errs.Count > 0) return Err(400, errs.ToArray());

        var r = Refs.FirstOrDefault(x => x.RefNo == refNo);
        if (r == null || r.Status == 8 || (r.Status == 10 && r.LockExpiresAt < DateTime.Now))
            return Err(404, ("404.1", "Ref No. not found or expired."));
        if (r.Bill.Sms != sms) return Err(404, ("404.1", "Billing information not found."));

        if (r.Status == 50) return Err(202, ("202.1", "Bill already paid."));
        if (r.Status == 30 || r.Status == 9) return Err(202, ("202.2", "Bill already submitted, waiting for acknowledge."));

        if (amount != r.Bill.Amount)
            return Err(400, ("400.2", $"Bill amount not equal. This bill amount is: {r.Bill.Amount.ToString("0.00", CultureInfo.InvariantCulture)}."));
        if (Refs.Any(x => x.PayerTxnId == ptid))
            return Err(400, ("400.3", "Payer Transaction Id should be unique."));

        r.Status = 30;
        r.PayerTxnId = ptid;
        r.AckExpiresAt = DateTime.Now.AddMinutes(AckMinutes);
        r.PaymentDate = DateTime.Now.Date;

        return Ok(new JsonObject
        {
            ["SMSAccountNo"] = r.Bill.Sms,
            ["RefNo"] = r.RefNo,
            ["PayerTransactionId"] = r.PayerTxnId,
            ["ExpiresAt"] = Ts(r.AckExpiresAt!.Value),
            ["Message"] = "Waiting for Acknowledgment"
        });
    }

    // ------------------------------------------------------------ 6. /billinginfo/paymentacknowledgment
    public IResult PaymentAcknowledgment(JsonObject b)
    {
        var sms = Str(b, "SMSAccountNo");
        var refNo = Str(b, "RefNo");

        var errs = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(sms)) errs.Add(("400.1", "SMS Account No. is required."));
        if (string.IsNullOrWhiteSpace(refNo)) errs.Add(("400.1", "Ref. No. is required."));
        if (errs.Count > 0) return Err(400, errs.ToArray());

        var r = Refs.FirstOrDefault(x => x.RefNo == refNo);
        if (r == null) return Err(404, ("404.1", "Ref. No. not found."));
        if (r.Bill.Sms != sms) return Err(404, ("404.1", "Billing information not found."));

        if (r.Status == 50) return Err(202, ("202.1", "Bill already paid."));

        // ASSUMPTION: acknowledging something that was never submitted / was reset behaves like "not found".
        if (r.Status == 10 || r.Status == 8) return Err(404, ("404.1", "Ref. No. not found."));

        if (r.Status == 9 || (r.Status == 30 && DateTime.Now > r.AckExpiresAt))
            return Err(202, ("202.2", "Bill already submitted, Acknowledgement time is over, Manual reconciliation required."));

        r.Status = 50;
        r.Bill.Paid = true;

        return Ok(new JsonObject
        {
            ["SMSAccountNo"] = r.Bill.Sms,
            ["RefNo"] = r.RefNo,
            ["PayerTransactionId"] = r.PayerTxnId,
            ["Message"] = "Success"
        });
    }

    // ------------------------------------------------------------ 7.1 /billinginfo/paymentstatus
    public IResult PaymentStatus(JsonObject b)
    {
        var sms = Str(b, "SMSAccountNo");
        var refNo = Str(b, "RefNo");

        var errs = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(sms)) errs.Add(("400.1", "SMS Account No. is required."));
        if (string.IsNullOrWhiteSpace(refNo)) errs.Add(("400.1", "Ref. No. is required."));
        if (errs.Count > 0) return Err(400, errs.ToArray());

        var r = Refs.FirstOrDefault(x => x.RefNo == refNo && x.Bill.Sms == sms);
        if (r == null) return Err(404, ("404.1", "Data not found."));
        if (r.Status == 10 && r.LockExpiresAt < DateTime.Now) return Err(404, ("404.1", "Ref. No. expired."));

        return Ok(new JsonObject
        {
            ["SMSAccountNo"] = r.Bill.Sms,
            ["RefNo"] = r.RefNo,
            ["PayerTransactionId"] = r.PayerTxnId,
            ["Status"] = r.Status.ToString(CultureInfo.InvariantCulture)    // "{Status}" is a string in the spec
        });
    }

    // ------------------------------------------------------------ 8. /billinginfo/dispute
    public IResult Dispute(JsonObject b)
    {
        var sms = Str(b, "SMSAccountNo");
        var refNo = Str(b, "RefNo");
        var ptid = Str(b, "PayerTransactionId");
        var type = Int(b, "DisputeType");

        var errs = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(sms)) errs.Add(("400.1", "SMS Account No. is required."));
        if (string.IsNullOrWhiteSpace(refNo)) errs.Add(("400.1", "Ref. No. is required."));
        if (string.IsNullOrWhiteSpace(ptid)) errs.Add(("400.1", "Payer Transaction Id is required."));
        if (type == null) errs.Add(("400.1", "Dispute Type is required."));
        if (errs.Count > 0) return Err(400, errs.ToArray());

        if (type != 5 && type != 10) return Err(400, ("400.2", "Dispute Type is invalid."));   // ASSUMPTION (not in spec)

        var r = Refs.FirstOrDefault(x => x.RefNo == refNo && x.Bill.Sms == sms);
        if (r == null || r.Status == 10 || r.Status == 8) return Err(404, ("404.1", "Data not found."));

        if (r.Status == 9) return Err(202, ("202.1", "Already submitted for dispute."));
        if (r.Status == 50) return Err(400, ("400.2", "Already Acknowledged."));
        if (r.PaymentDate.Date != DateTime.Now.Date) return Err(400, ("400.2", "Request can’t be done after payment date."));
        if (r.Status == 30 && DateTime.Now <= r.AckExpiresAt)
            return Err(202, ("202.4", $"Bill Payment is in progress. Please try after:{Ts(r.AckExpiresAt!.Value)}"));

        r.Status = 9;
        r.DisputeType = type;

        return Ok(new JsonObject
        {
            ["SMSAccountNo"] = r.Bill.Sms,
            ["RefNo"] = r.RefNo,
            ["PayerTransactionId"] = r.PayerTxnId,
            ["DisputeType"] = type,
            ["Message"] = "Received Successfully."
        });
    }

    // ------------------------------------------------------------ 9. /billinginfo/checkbill
    public IResult CheckBill(JsonObject b)
    {
        var sms = Str(b, "SMSAccountNo");
        var billType = Int(b, "BillType");

        var errs = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(sms)) errs.Add(("400.1", "SMS Account No. is required."));
        if (billType == null) errs.Add(("400.1", "Bill Type is required."));
        if (errs.Count > 0) return Err(400, errs.ToArray());

        if (!BillTypes.Contains(billType!.Value)) return Err(401, ("401.2", "You don't have permission to pay this bill type."));

        var bill = Bills.FirstOrDefault(x => x.Sms == sms && x.BillType == billType);
        if (bill == null) return Err(404, ("404.1", "Billing information not found."));
        if (bill.Paid) return Err(202, ("202.1", "Bill already paid."));
        if (bill.LpcOver) return Err(202, ("202.4", "LPC date is over. Please wait for next Bill."));

        var o = new JsonObject { ["SMSAccountNo"] = bill.Sms };
        if (bill.BillType == 50) { o["DueType"] = bill.DueType; o["BillMonth"] = bill.BillMonth; }
        o["Amount"] = bill.Amount;
        return Ok(o);
    }

    // ------------------------------------------------------------ 7.2 /report/detail
    static bool RoutingMatches(string? filter, string? actual)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        var star = filter.IndexOf('*');
        return star >= 0
            ? (actual ?? "").StartsWith(filter[..star], StringComparison.Ordinal)
            : filter == actual;
    }

    public IResult ReportDetail(JsonObject b)
    {
        var dateText = Str(b, "CollectionDate");
        if (string.IsNullOrWhiteSpace(dateText)) return Err(400, ("400.1", "Collection Date is required."));
        if (!DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return Err(400, ("400.2", "Collection Date must be yyyy-mm-dd."));                     // ASSUMPTION

        var status = Int(b, "CollectionStatus") ?? 50;
        var routing = Str(b, "BranchRoutingNo");
        var agent = Str(b, "AgentId");

        var rows = Refs.Where(r => r.PaymentDate.Date == date.Date
                                   && (status == 50 ? r.Status == 50 : (r.Status == 30 || r.Status == 9))
                                   && RoutingMatches(routing, r.Routing)
                                   && (string.IsNullOrWhiteSpace(agent) || r.AgentId == agent))
                       .ToList();
        if (rows.Count == 0) return Err(404, ("404.1", "Information not found."));

        var arr = new JsonArray();
        foreach (var r in rows)
        {
            var e = new JsonObject
            {
                ["SMSAccountNo"] = r.Bill.Sms,
                ["RefNo"] = r.RefNo,
                ["PayerTransactionId"] = r.PayerTxnId,
                ["BillType"] = r.Bill.BillType,
                ["Amount"] = r.Bill.Amount,
                ["Vat"] = r.Bill.Vat
            };
            if (r.Status == 9) e["Message"] = "Submitted for Dispute";
            arr.Add(e);
        }
        return Ok(new JsonObject { ["TotalCount"] = rows.Count, ["Entities"] = arr });
    }

    // ------------------------------------------------------------ 7.3 /report/summary
    public IResult ReportSummary(JsonObject b)
    {
        var s = Str(b, "StartDate");
        var e = Str(b, "EndDate");
        var errs = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(s)) errs.Add(("400.1", "Start Date is required."));
        if (string.IsNullOrWhiteSpace(e)) errs.Add(("400.1", "End Date is required."));
        if (errs.Count > 0) return Err(400, errs.ToArray());

        if (!DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) ||
            !DateTime.TryParseExact(e, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
            return Err(400, ("400.2", "Dates must be yyyy-mm-dd."));                                // ASSUMPTION

        var routing = Str(b, "BranchRoutingNo");
        var agent = Str(b, "AgentId");

        var groups = Refs.Where(r => r.Status == 50 && r.PaymentDate.Date >= from.Date && r.PaymentDate.Date <= to.Date
                                     && RoutingMatches(routing, r.Routing)
                                     && (string.IsNullOrWhiteSpace(agent) || r.AgentId == agent))
                         .GroupBy(r => r.PaymentDate.Date).OrderBy(g => g.Key).ToList();
        if (groups.Count == 0) return Err(404, ("404.1", "Information not found."));

        var arr = new JsonArray();
        foreach (var g in groups)
            arr.Add(new JsonObject
            {
                ["PaymentDate"] = Ts(g.Key),
                ["NoOfBill"] = g.Count(),
                ["Amount"] = g.Sum(r => r.Bill.Amount),
                ["Vat"] = g.Sum(r => r.Bill.Vat)
            });
        return Ok(new JsonObject { ["TotalCount"] = groups.Count, ["Entities"] = arr });
    }

    // ------------------------------------------------------------ 10. /authenticate/changepassword
    public IResult ChangePassword(JsonObject b)
    {
        var cur = Str(b, "CurrentPassword");
        var nw = Str(b, "NewPassword");
        var errs = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(cur)) errs.Add(("400.1", "Current Password is required."));
        if (string.IsNullOrWhiteSpace(nw)) errs.Add(("400.1", "New Password is required."));
        if (errs.Count > 0) return Err(400, errs.ToArray());

        if (cur != Password) return Err(401, ("401.1", "Your current password is not valid."));
        Password = nw!;
        return Ok(new JsonObject { ["Code"] = "200.1", ["Message"] = "Your password is changed." });
    }
}
