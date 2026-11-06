using HljuML.Core.GamePlay;
using HljuML.Shared.Model;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace HljuML.App.Core;

public class MainViewModel(GameInstanceService gameInstanceService, IHostApplicationLifetime appLifetime)
{
    internal Task<ServiceResult<int?>> LaunchGameAsync() =>
        gameInstanceService.LaunchGameInstance(appLifetime.ApplicationStopping);
}
