namespace StashBot.Configuration;

public sealed record SupabaseConfiguration(string Url, string ServiceRoleKey)
{
    public static SupabaseConfiguration LoadFromEnvironment()
    {
        var url = Environment.GetEnvironmentVariable("SUPABASE_URL");
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(
                "Variável de ambiente SUPABASE_URL não definida. " +
                "Defina-a com a URL do projeto Supabase (ex: https://xxxxx.supabase.co).");
        }

        var serviceRoleKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY");
        if (string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            throw new InvalidOperationException(
                "Variável de ambiente SUPABASE_SERVICE_ROLE_KEY não definida. " +
                "Defina-a com a service_role key do projeto Supabase (Project Settings > API).");
        }

        return new SupabaseConfiguration(url, serviceRoleKey);
    }
}
