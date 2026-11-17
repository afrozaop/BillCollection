using BillCollection.Interfaces;
using BillCollection.Models.BREB;
using BillCollection.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var url = args.Length > 0 ? args[0] : "http://localhost:5099";
var admin = new HttpClient { BaseAddress = new Uri(url) };
async Task<string> State() => await admin.GetStringAsync("/_mock/state");
async Task<string> Admin(string path) => await (await admin.PostAsync(path, null)).Content.ReadAsStringAsync();

int pass = 0, fail = 0;
void Check(string name, bool ok, string? detail = null)
{
    if (ok) pass++; else fail++;
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}" + (ok || detail == null ? "" : $"   <-- {detail}"));
}

ServiceProvider Build(BREBOptions o, int timeoutSec = 3)
{
    var sc = new ServiceCollection();
    sc.AddLogging(b => b.SetMinimumLevel(LogLevel.Error));
    sc.AddSingleton<IOptions<BREBOptions>>(Options.Create(o));
    sc.AddHttpClient("BREB", c =>
    {
        c.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
        c.Timeout = TimeSpan.FromSeconds(timeoutSec);
    });
    sc.AddSingleton<IBREBTokenProvider, BREBTokenProvider>();
    sc.AddScoped<IBREBApiClient, BREBApiClient>();
    return sc.BuildServiceProvider();
}

var good = new BREBOptions { BaseUrl = url, ApiKey = "MOCK-API-KEY", Username = "bkb_mock", Password = "mock-pass-123", TokenSafetyMarginSeconds = 60 };
var sp = Build(good);
IBREBApiClient Api() => sp.CreateScope().ServiceProvider.GetRequiredService<IBREBApiClient>();
string TodayStr() => DateTime.Now.ToString("yyyy-MM-dd");

BREBBillingInfoRequest Bill(string sms, string payer = "bkbvas1604", int type = 50, string? routing = "090123456") =>
    new() { SMSAccountNo = sms, BillType = type, PayerId = payer, PayerTypeId = 6, BranchRoutingNo = routing };

int seq = 0;
string Ptid() => DateTime.Now.ToString("ddMMyyHHmmssfff") + (seq++ % 10);   // like production + counter, to stay unique in a fast loop

await Admin("/_mock/reset");

// ------------------------------------------------------------------ 1. authentication
Console.WriteLine("\n1. Authentication / token handling");
var r1 = await Api().BillingInfoAsync(Bill("1010032036503"));
Check("first call authenticates and bill query works", r1.IsSuccess, r1.ErrorText);
Check("second call re-uses the cached token (login count stays 1)",
    (await Api().BillingInfoAsync(Bill("1010032036507"))).IsSuccess &&
    (await State()).Contains("\"authCount\":1"));

await Admin("/_mock/revoke-access-tokens");
var rr = await Api().BillingInfoAsync(Bill("1010032036503"));
var st = await State();
Check("revoked token -> 401 -> client renews with refresh token and retries", rr.IsSuccess, rr.ErrorText);
Check("... and the refresh did NOT use another full login", st.Contains("\"authCount\":1"), st);

var badUser = Build(new BREBOptions { BaseUrl = url, ApiKey = "MOCK-API-KEY", Username = "bkb_mock", Password = "wrong" });
try { await badUser.CreateScope().ServiceProvider.GetRequiredService<IBREBApiClient>().BillingInfoAsync(Bill("1010032036503")); Check("wrong password -> exception", false); }
catch (BREBApiException ex) { Check("wrong password -> BREBApiException with BREB message", ex.Message.Contains("Username or password is invalid"), ex.Message); }

var badKey = Build(new BREBOptions { BaseUrl = url, ApiKey = "nope", Username = "bkb_mock", Password = "mock-pass-123" });
try { await badKey.CreateScope().ServiceProvider.GetRequiredService<IBREBApiClient>().BillingInfoAsync(Bill("1010032036503")); Check("wrong API key -> exception", false); }
catch (BREBApiException ex) { Check("wrong API key -> BREBApiException with BREB message", ex.Message.Contains("API Key is invalid"), ex.Message); }

var down = Build(new BREBOptions { BaseUrl = "http://localhost:59999", ApiKey = "x", Username = "x", Password = "x" });
try { await down.CreateScope().ServiceProvider.GetRequiredService<IBREBApiClient>().BillingInfoAsync(Bill("1010032036503")); Check("server down -> exception", false); }
catch (BREBApiException ex) { Check("server down -> friendly BREBApiException (VPN message)", ex.Message.Contains("unreachable"), ex.Message); }

// ------------------------------------------------------------------ 2. billinginfo
Console.WriteLine("\n2. /billinginfo");
await Admin("/_mock/reset");
var b = await Api().BillingInfoAsync(Bill("1010032036503"));
Check("200: fields parsed (Amount 328, Vat 16, DueType Normal)", b.IsSuccess && b.Data!.Amount == 328 && b.Data.Vat == 16 && b.Data.DueType == "Normal", b.ErrorText);
Check("ExpiresIn came as a JSON number and is read as \"5\"", b.Data?.ExpiresIn == "5", b.Data?.ExpiresIn);
Check("RefNo is an opaque 50-char string (not a GUID)", b.Data?.RefNo?.Length == 50 && !Guid.TryParse(b.Data.RefNo, out _), b.Data?.RefNo);

var miss = await Api().BillingInfoAsync(new BREBBillingInfoRequest { SMSAccountNo = "", BillType = 50, PayerId = "", PayerTypeId = 2 });
Check("400: all missing fields reported together (SMS, PayerId, Routing, AgentID)",
    miss.Status == 400 && miss.Errors.Count == 4 && miss.HasError("400.1"), miss.ErrorText);
var nf = await Api().BillingInfoAsync(Bill("9999999999999"));
Check("404.1 account not found", nf.Status == 404 && nf.HasError("404.1"), nf.ErrorText);
var paid = await Api().BillingInfoAsync(Bill("1010032036505"));
Check("202.1 bill already paid", paid.Status == 202 && paid.HasError("202.1"), paid.ErrorText);
var lpc = await Api().BillingInfoAsync(Bill("1010032036506"));
Check("202.4 LPC date over", lpc.Status == 202 && lpc.HasError("202.4"), lpc.ErrorText);
var other = await Api().BillingInfoAsync(Bill("1010032036503", payer: "someone_else"));
Check("202.3 bill locked by another payer", other.Status == 202 && other.HasError("202.3"), other.ErrorText);
var bt = await Api().BillingInfoAsync(Bill("1010032036503", type: 99));
Check("401.2 no permission for bill type", bt.Status == 401 && bt.HasError("401.2"), bt.ErrorText);
var another = await Api().BillingInfoAsync(Bill("2020000000001", type: 20));
Check("other bill type (20): amount only, no DueType/Vat", another.IsSuccess && another.Data!.Amount == 5000 && another.Data.DueType == null && another.Data.ExpiresIn == "14", another.ErrorText);

// ------------------------------------------------------------------ 3. submit + acknowledge
Console.WriteLine("\n3. /paymentsubmit and /paymentacknowledgment (happy path)");
await Admin("/_mock/reset");
var q = await Api().BillingInfoAsync(Bill("1010032036503"));
var refNo = q.Data!.RefNo!;
var wrongAmt = await Api().PaymentSubmitAsync(new BREBPaymentSubmitRequest { SMSAccountNo = "1010032036503", RefNo = refNo, PayerTransactionId = Ptid(), Amount = 100 });
Check("400.2 amount mismatch, message carries the real amount", wrongAmt.Status == 400 && wrongAmt.HasError("400.2") && wrongAmt.ErrorText.Contains("328.00"), wrongAmt.ErrorText);
var noRef = await Api().PaymentSubmitAsync(new BREBPaymentSubmitRequest { SMSAccountNo = "1010032036503", RefNo = "unknown-ref", PayerTransactionId = Ptid(), Amount = 328 });
Check("404.1 unknown RefNo", noRef.Status == 404 && noRef.HasError("404.1"), noRef.ErrorText);

var ptid = Ptid();
var sub = await Api().PaymentSubmitAsync(new BREBPaymentSubmitRequest { SMSAccountNo = "1010032036503", RefNo = refNo, PayerTransactionId = ptid, Amount = 328 });
Check("submit 200 + 'Waiting for Acknowledgment' + ExpiresAt", sub.IsSuccess && sub.Data!.Message == "Waiting for Acknowledgment" && !string.IsNullOrEmpty(sub.Data.ExpiresAt), sub.ErrorText);
var subAgain = await Api().PaymentSubmitAsync(new BREBPaymentSubmitRequest { SMSAccountNo = "1010032036503", RefNo = refNo, PayerTransactionId = Ptid(), Amount = 328 });
Check("submit twice -> 202.2 already submitted", subAgain.Status == 202 && subAgain.HasError("202.2"), subAgain.ErrorText);
var stWait = await Api().PaymentStatusAsync(new BREBPaymentStatusRequest { SMSAccountNo = "1010032036503", RefNo = refNo });
Check("paymentstatus = \"30\" (Waiting for Acknowledge)", stWait.IsSuccess && stWait.Data!.Status == "30", stWait.ErrorText);

var ack = await Api().PaymentAcknowledgeAsync(new BREBPaymentAcknowledgmentRequest { SMSAccountNo = "1010032036503", RefNo = refNo, PayerTransactionId = ptid });
Check("acknowledge 200 'Success'", ack.IsSuccess && ack.Data!.Message == "Success", ack.ErrorText);
var ack2 = await Api().PaymentAcknowledgeAsync(new BREBPaymentAcknowledgmentRequest { SMSAccountNo = "1010032036503", RefNo = refNo });
Check("acknowledge twice -> 202.1 already paid", ack2.Status == 202 && ack2.HasError("202.1"), ack2.ErrorText);
var stPaid = await Api().PaymentStatusAsync(new BREBPaymentStatusRequest { SMSAccountNo = "1010032036503", RefNo = refNo });
Check("paymentstatus = \"50\" (Paid Acknowledged)", stPaid.Data?.Status == "50", stPaid.ErrorText);
var after = await Api().BillingInfoAsync(Bill("1010032036503"));
Check("bill is now 202.1 already paid", after.Status == 202 && after.HasError("202.1"), after.ErrorText);

var q2 = await Api().BillingInfoAsync(Bill("1010032036507"));
var dup = await Api().PaymentSubmitAsync(new BREBPaymentSubmitRequest { SMSAccountNo = "1010032036507", RefNo = q2.Data!.RefNo!, PayerTransactionId = ptid, Amount = 1500 });
Check("400.3 duplicate PayerTransactionId", dup.Status == 400 && dup.HasError("400.3"), dup.ErrorText);

// ------------------------------------------------------------------ 4. missed acknowledgement + dispute
Console.WriteLine("\n4. Missed acknowledgement window -> dispute -> BREB decision");
var q3 = await Api().BillingInfoAsync(Bill("1010032036507"));
var ref3 = q3.Data!.RefNo!; var ptid3 = Ptid();
await Api().PaymentSubmitAsync(new BREBPaymentSubmitRequest { SMSAccountNo = "1010032036507", RefNo = ref3, PayerTransactionId = ptid3, Amount = 1500 });
var early = await Api().DisputeAsync(new BREBDisputeRequest { SMSAccountNo = "1010032036507", RefNo = ref3, PayerTransactionId = ptid3, DisputeType = 5 });
Check("dispute while acknowledgement is still possible -> 202.4 'in progress'", early.Status == 202 && early.HasError("202.4"), early.ErrorText);

await Admin("/_mock/expire-ack");
var late = await Api().PaymentAcknowledgeAsync(new BREBPaymentAcknowledgmentRequest { SMSAccountNo = "1010032036507", RefNo = ref3 });
Check("acknowledge after ExpiresAt -> 202.2 'Manual reconciliation required'", late.Status == 202 && late.HasError("202.2") && late.ErrorText.Contains("Manual reconciliation"), late.ErrorText);

var disp = await Api().DisputeAsync(new BREBDisputeRequest { SMSAccountNo = "1010032036507", RefNo = ref3, PayerTransactionId = ptid3, DisputeType = 5 });
Check("dispute (5 = Acknowledge) accepted: 'Received Successfully.'", disp.IsSuccess && disp.Data!.Message == "Received Successfully." && disp.Data.DisputeType == "5", disp.ErrorText);
var disp2 = await Api().DisputeAsync(new BREBDisputeRequest { SMSAccountNo = "1010032036507", RefNo = ref3, PayerTransactionId = ptid3, DisputeType = 5 });
Check("dispute twice -> 202.1 already submitted", disp2.Status == 202 && disp2.HasError("202.1"), disp2.ErrorText);
var st9 = await Api().PaymentStatusAsync(new BREBPaymentStatusRequest { SMSAccountNo = "1010032036507", RefNo = ref3 });
Check("paymentstatus = \"9\" (Submitted for Dispute)", st9.Data?.Status == "9", st9.ErrorText);

var rep30 = await Api().ReportDetailAsync(new BREBReportDetailRequest { CollectionDate = TodayStr(), CollectionStatus = 30, BranchRoutingNo = "090123456" });
Check("report/detail status 30 lists the disputed bill with its Message",
    rep30.IsSuccess && rep30.Data!.Entities.Any(e => e.RefNo == ref3 && e.Message == "Submitted for Dispute"), rep30.ErrorText);

await Admin("/_mock/resolve-disputes");
var st50 = await Api().PaymentStatusAsync(new BREBPaymentStatusRequest { SMSAccountNo = "1010032036507", RefNo = ref3 });
Check("after BREB resolves it -> status \"50\"", st50.Data?.Status == "50", st50.ErrorText);
var dispPaid = await Api().DisputeAsync(new BREBDisputeRequest { SMSAccountNo = "1010032036507", RefNo = ref3, PayerTransactionId = ptid3, DisputeType = 10 });
Check("dispute on an acknowledged payment -> 400.2 'Already Acknowledged.'", dispPaid.Status == 400 && dispPaid.ErrorText.Contains("Already Acknowledged"), dispPaid.ErrorText);

// ------------------------------------------------------------------ 5. reports
Console.WriteLine("\n5. /report/detail and /report/summary");
var det = await Api().ReportDetailAsync(new BREBReportDetailRequest { CollectionDate = TodayStr(), BranchRoutingNo = "090123456" });
Check("detail (default status 50): 2 paid bills, BillType read from number", det.IsSuccess && det.Data!.TotalCount == 2 && det.Data.Entities.All(e => e.BillType == "50"), det.ErrorText);
Check("detail: Amount/Vat parsed", det.Data!.Entities.Sum(e => e.Amount) == 1828 && det.Data.Entities.Sum(e => e.Vat) == 91, det.ErrorText);
var detWild = await Api().ReportDetailAsync(new BREBReportDetailRequest { CollectionDate = TodayStr(), BranchRoutingNo = "0901***" });
Check("detail: wildcard routing number '0901***' works", detWild.IsSuccess && detWild.Data!.TotalCount == 2, detWild.ErrorText);
var detNone = await Api().ReportDetailAsync(new BREBReportDetailRequest { CollectionDate = "2001-01-01" });
Check("detail: 404.1 'Information not found.'", detNone.Status == 404 && detNone.HasError("404.1"), detNone.ErrorText);
var detMissing = await Api().ReportDetailAsync(new BREBReportDetailRequest { CollectionDate = "" });
Check("detail: 400.1 date required", detMissing.Status == 400 && detMissing.HasError("400.1"), detMissing.ErrorText);
var sum = await Api().ReportSummaryAsync(new BREBReportSummaryRequest { StartDate = DateTime.Now.AddDays(-3).ToString("yyyy-MM-dd"), EndDate = TodayStr(), BranchRoutingNo = "090123456" });
Check("summary: 1 day, 2 bills, 1828.00, VAT 91",
    sum.IsSuccess && sum.Data!.TotalCount == 1 && sum.Data.Entities[0].NoOfBill == 2 && sum.Data.Entities[0].Amount == 1828 && sum.Data.Entities[0].Vat == 91, sum.ErrorText);
Check("summary: PaymentDate parses as a date", DateTime.TryParse(sum.Data?.Entities.FirstOrDefault()?.PaymentDate, out var pd) && pd.Date == DateTime.Today, sum.Data?.Entities.FirstOrDefault()?.PaymentDate);

// ------------------------------------------------------------------ 6. outcome-unknown handling
Console.WriteLine("\n6. Timeouts / server errors (the dangerous cases)");
await Admin("/_mock/reset");
var q6 = await Api().BillingInfoAsync(Bill("1010032036503"));
var ref6 = q6.Data!.RefNo!; var ptid6 = Ptid();

await Admin("/_mock/fault?endpoint=paymentsubmit&mode=timeout&when=after&delay=8");
try
{
    await Api().PaymentSubmitAsync(new BREBPaymentSubmitRequest { SMSAccountNo = "1010032036503", RefNo = ref6, PayerTransactionId = ptid6, Amount = 328 });
    Check("submit timeout -> exception", false);
}
catch (BREBApiException ex) { Check("submit timeout -> BREBApiException, OutcomeUnknown = true", ex.OutcomeUnknown, ex.Message); }

var truth = await Api().PaymentStatusAsync(new BREBPaymentStatusRequest { SMSAccountNo = "1010032036503", RefNo = ref6, PayerTransactionId = ptid6 });
Check("...BREB DID record it: paymentstatus = \"30\" (this is why 'Pending' + Check & Sync matters)", truth.Data?.Status == "30", truth.ErrorText);

await Admin("/_mock/fault?endpoint=paymentacknowledgment&mode=http500&when=after");
try { await Api().PaymentAcknowledgeAsync(new BREBPaymentAcknowledgmentRequest { SMSAccountNo = "1010032036503", RefNo = ref6 }); Check("acknowledge HTTP 500 -> exception", false); }
catch (BREBApiException ex) { Check("acknowledge answered 500 -> OutcomeUnknown = true, Status 500", ex.OutcomeUnknown && ex.Status == 500, ex.Message); }
var ackRetry = await Api().PaymentAcknowledgeAsync(new BREBPaymentAcknowledgmentRequest { SMSAccountNo = "1010032036503", RefNo = ref6 });
Check("...retry afterwards -> 202.1 already paid (acknowledge went through the first time)", ackRetry.Status == 202 && ackRetry.HasError("202.1"), ackRetry.ErrorText);

await Admin("/_mock/fault?endpoint=billinginfo&mode=http500&when=before");
var q500 = await Api().BillingInfoAsync(Bill("1010032036507"));
Check("billinginfo 500 (read-only call) -> returned as response, not thrown", q500.Status == 500, q500.ErrorText);

await Admin("/_mock/fault?endpoint=billinginfo&mode=garbage&when=before");
try { await Api().BillingInfoAsync(Bill("1010032036507")); Check("HTML error page -> exception", false); }
catch (BREBApiException ex) { Check("HTML / proxy error page -> BREBApiException 'Unexpected response'", ex.Message.Contains("Unexpected response"), ex.Message); }

await Admin("/_mock/config?alwaysHttp200=true");
var q200 = await Api().BillingInfoAsync(Bill("9999999999999"));
Check("server that answers HTTP 200 for everything: code still read from body", q200.Status == 404 && q200.HasError("404.1"), q200.ErrorText);
await Admin("/_mock/config?alwaysHttp200=false");

Console.WriteLine($"\n==== {pass} passed, {fail} failed ====");
return fail == 0 ? 0 : 1;
