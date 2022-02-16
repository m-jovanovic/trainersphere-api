using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Users.Infrastructure.Configuration;

public static class UsersStartup
{
    public static void Initialize(IServiceCollection services)
    {
        services.AddMediatR(Application.AssemblyReference.Assembly);

        services.AddControllers().AddApplicationPart(Api.AssemblyReference.Assembly);
    }
}
