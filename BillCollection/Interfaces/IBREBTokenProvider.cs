namespace BillCollection.Interfaces
{
    public interface IBREBTokenProvider
    {
        /// <summary>Returns a valid access token (cached, refreshed or newly authenticated).</summary>
        Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

        /// <summary>Forget the cached access token (e.g. after a 401 from BREB).</summary>
        void InvalidateAccessToken();
    }
}
