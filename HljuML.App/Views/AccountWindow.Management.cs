using System;
using System.Data.Common;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HljuML.Core.GamePlay.Auth;
using HljuML.Shared.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TKCode.Arc;

namespace HljuML.App.Views;

public partial class AccountWindow
{
    private bool _managingAccount;

    /// <summary>为过期会话重新输入凭据；保留档案 ID 以及实例对该档案的引用。</summary>
    private void OnReloginProfileClicked(object? sender, RoutedEventArgs e)
    {
        if (_managingAccount || sender is not Control { DataContext: AccountProfileRow row })
        {
            return;
        }

        var server = AuthServers.FirstOrDefault(item => item.Server.Id == row.AuthServerId)?.Server;
        if (server is null)
        {
            ShowAccountError("找不到该档案的认证服务器，请重新打开账户管理。");
            return;
        }

        OpenLogin(server, row);
    }

    /// <summary>确认后只删除本地档案和登录缓存，不删除服务器账号；服务会清除实例的最近使用引用。</summary>
    private async void OnDeleteProfileClicked(object? sender, RoutedEventArgs e)
    {
        if (_managingAccount || _scopes is null || sender is not Control { DataContext: AccountProfileRow row })
        {
            return;
        }

        _managingAccount = true;
        var token = _lifetime.Token;
        try
        {
            if (!await Dialog.ConfirmAsync(this, "删除玩家档案？",
                    $"将从启动器移除“{row.Name}”及其登录缓存，关联实例将取消该档案选择。服务器账号和游戏文件会保留。",
                    "删除档案", isDanger: true) || token.IsCancellationRequested)
            {
                return;
            }

            await using var scope = _scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<PlayerProfileService>()
                .DeletePlayerProfile(row.Id, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (result.Status != ServiceStatus.Success)
            {
                ShowAccountError(result.Message);
                return;
            }

            // 头像可能在确认期间更新为另一行记录，按 ID 查找最新行而不是删除旧的引用。
            var current = Profiles.FirstOrDefault(item => item.Id == row.Id);
            if (current is not null)
            {
                Profiles.Remove(current);
            }

            EmptyProfiles.IsVisible = Profiles.Count == 0;
            AccountStatus.IsVisible = false;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception error) when (error is DbException or DbUpdateException)
        {
            if (!token.IsCancellationRequested)
            {
                ShowAccountError("无法删除玩家档案，请重试。");
            }
        }
        finally
        {
            _managingAccount = false;
        }
    }

    /// <summary>沿用服务的关联保护：服务器仍被档案使用时明确提示，避免留下无法认证的档案。</summary>
    private async void OnDeleteServerClicked(object? sender, RoutedEventArgs e)
    {
        if (_managingAccount || _scopes is null || sender is not Control { DataContext: AccountServerRow row })
        {
            return;
        }

        var count = Profiles.Count(item => item.AuthServerId == row.Server.Id);
        if (count > 0)
        {
            ShowAccountError($"“{row.Server.Name}”仍有 {count} 个玩家档案，请先删除这些档案后再删除服务器。");
            return;
        }

        _managingAccount = true;
        var token = _lifetime.Token;
        try
        {
            if (!await Dialog.ConfirmAsync(this, "删除认证服务器？", $"将从启动器移除“{row.Server.Name}”，以后可以重新添加。",
                    "删除服务器", isDanger: true) || token.IsCancellationRequested)
            {
                return;
            }

            await using var scope = _scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<AuthServerService>()
                .DeleteAuthServer(row.Server.Id, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (result.Status != ServiceStatus.Success)
            {
                ShowAccountError(result.Message);
                return;
            }

            AuthServers.Remove(row);
            if (_addedServer?.Id == row.Server.Id)
            {
                _addedServer = null;
                AddedServerCard.IsVisible = false;
            }

            AccountStatus.IsVisible = false;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception error) when (error is DbException or DbUpdateException)
        {
            if (!token.IsCancellationRequested)
            {
                ShowAccountError("无法删除认证服务器，请重试。");
            }
        }
        finally
        {
            _managingAccount = false;
        }
    }

    private void ShowAccountError(string message)
    {
        AccountStatus.Text = message;
        AccountStatus.IsVisible = true;
    }
}