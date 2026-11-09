using UniversityLeaders.Application.Common;
using UniversityLeaders.Application.Users;

namespace UniversityLeaders.Api.Security;

/// <summary>
/// Offline University Leader password recovery: <c>dotnet UniversityLeaders.Api.dll recover-university-leader &lt;userName&gt;</c>.
/// There is no HTTP route. Running it requires the API's deployment configuration (database credentials, key-encryption
/// certificate), so only operators who were explicitly given shell access to the API host can use it. The temporary
/// password goes to this terminal only (never to a logger) and must be changed at first login.
/// </summary>
internal static class UniversityLeaderRecoveryCommand
{
    public const string Name = "recover-university-leader";

    public static bool Matches(string[] args)
    {
        return args.Length > 0 && args[0] == Name;
    }

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine($"Usage: {Name} <userName>");
            return 2;
        }

        using IServiceScope scope = services.CreateScope();
        IUserManagementService users = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        try
        {
            ProvisionedUserResponse recovered = await users.RecoverUniversityLeaderAsync(args[1], CancellationToken.None);
            Console.WriteLine($"Temporary password for {recovered.User.UserName} (shown once; change it at the next login):");
            Console.WriteLine(recovered.TemporaryPassword);
            Console.WriteLine("All existing sessions of this account have been ended.");
            return 0;
        }
        catch (AppException failure)
        {
            Console.Error.WriteLine($"{failure.Code}: {failure.Message}");
            return 1;
        }
    }
}
