using HljuML.Core.GamePlay;
using Microsoft.Extensions.DependencyInjection;

namespace HljuML.App.Core;

public static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection collection)
    {
        collection.AddHostedService<GameInstanceService>();
        collection.AddSingleton<GameInstanceService>();
    }
}