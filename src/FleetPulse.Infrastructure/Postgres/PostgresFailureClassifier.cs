using Npgsql;

namespace FleetPulse.Infrastructure.Postgres;

public static class PostgresFailureClassifier
{
    public static bool IsPermanent(Exception exception)
    {
        return exception is PostgresException postgres
            && postgres.SqlState is "22023" or "22007" or "23514" or "22P02" or "23502";
    }

    public static bool IsTransient(Exception exception)
    {
        if (IsPermanent(exception))
            return false;

        if (exception is TimeoutException)
            return true;

        if (exception is PostgresException postgres)
        {
            return postgres.IsTransient
                || postgres.SqlState is "40001" or "40P01" or "53300" or "57P01" or "57P03";
        }

        return exception is NpgsqlException npgsql && npgsql.IsTransient;
    }
}
