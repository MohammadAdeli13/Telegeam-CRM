namespace TelegramCrm.IntegrationTests.Support;

/// <summary>
/// A <see cref="FactAttribute"/> for tests that need a relational provider. They are skipped
/// when <c>TEST_POSTGRES_CONNECTION_STRING</c> is unset (the local in-memory fallback), because
/// the in-memory provider does not implement <c>ExecuteUpdate</c> or transactions — the very
/// features these tests exist to prove. CI always sets the variable, so they always run there.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(CrmApiFactory.PostgresConnectionStringVariable)))
        {
            Skip = $"Requires {CrmApiFactory.PostgresConnectionStringVariable} (relational provider).";
        }
    }
}
