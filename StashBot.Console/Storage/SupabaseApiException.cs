namespace StashBot.Storage;

public sealed class SupabaseApiException(int statusCode, string? responseBody)
    : Exception($"Supabase API error {statusCode}: {responseBody}")
{
    public int StatusCode { get; } = statusCode;
}
