using HljuML.Core.GamePlay;
using Microsoft.Extensions.DependencyInjection;

namespace HljuML.App.Core;

public static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection collection)
    {
        collection.AddSingleton<GameInstanceService>();
        collection.AddHostedService(services => services.GetRequiredService<GameInstanceService>());
        collection.AddSingleton<MainViewModel>();
        collection.AddSingleton(services => new MainWindow
        {
            DataContext = services.GetRequiredService<MainViewModel>()
        });
    }
}
