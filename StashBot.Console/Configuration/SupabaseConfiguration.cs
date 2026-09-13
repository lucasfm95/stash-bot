namespace StashBot.Configuration;

public sealed record SupabaseConfiguration(string Url, string ServiceRoleKey)
{
    public static SupabaseConfiguration LoadFromEnvironment()
    {
        var url = Environment.GetEnvironmentVariable("SUPABASE_URL");
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(
                "Environment variable SUPABASE_URL is not set. " +
                "Set it to the Supabase project URL (e.g.: https://xxxxx.supabase.co).");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                $"Environment variable SUPABASE_URL='{url}' is not a valid absolute URL (e.g.: https://xxxxx.supabase.co).");
        }

        var serviceRoleKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY");
        if (string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            throw new InvalidOperationException(
                "Environment variable SUPABASE_SERVICE_ROLE_KEY is not set. " +
                "Set it to the Supabase project's service_role key (Project Settings > API).");
        }

        return new SupabaseConfiguration(url, serviceRoleKey);
    }
}
