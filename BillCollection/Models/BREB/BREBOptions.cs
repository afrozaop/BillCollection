namespace BillCollection.Models.BREB
{
    public class BREBOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>true = built-in test accounts (no BREB call); false = real ICBS API.</summary>
        public bool UseMock { get; set; } = true;

        /// <summary>6 = Self_Branch_Bank (what BKBUtility used). Agent types (1,2,8,10,11) need AgentId.</summary>
        public int PayerTypeId { get; set; } = 6;

        public string? AgentId { get; set; }

        /// <summary>
        /// true  = Pay Bill submits AND acknowledges in one click (the old BKB project; the manual says
        ///         "collect the Amount from the customer and click Pay Bill").
        /// false = Pay Bill only submits (status "Waiting for Acknowledgment"); the teller presses
        ///         Acknowledge afterwards (two-step, as drawn in the ICBS flow chart).
        /// </summary>
        public bool AutoAcknowledge { get; set; } = true;

        /// <summary>Renew the access token this many seconds before it really expires.</summary>
        public int TokenSafetyMarginSeconds { get; set; } = 60;
    }
}
