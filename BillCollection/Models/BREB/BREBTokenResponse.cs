namespace BillCollection.Models.BREB
{
    public class BREBTokenResponse
    {
        public string? AccessToken { get; set; }
        public int ExpiresIn { get; set; }
        public string? CreateTimestamp { get; set; }
        public string? RefreshToken { get; set; }
        public string? RefreshTokenExpiresAt { get; set; }
        public int AuthenticationLimit { get; set; }
    }
}
