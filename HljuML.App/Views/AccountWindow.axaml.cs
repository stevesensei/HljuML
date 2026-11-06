using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ColorMC.Core.GuiHandle;
using HljuML.Core.GamePlay.Auth;
using HljuML.Core.Model.Entity;
using HljuML.Shared.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TKCode.Arc;

namespace HljuML.App.Views;

public partial class AccountWindow : Window
{
    private enum FormStep { Address, Preview, Login, Offline, Role }

    private readonly IServiceScopeFactory? _scopes;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Bitmap _defaultAvatar;
    private readonly List<Bitmap> _avatars = [];
    private CancellationTokenSource? _formOperation;
    private TaskCompletionSource<int>? _roleSelection;
    private ResolvedYggdrasilServer? _preview;
    private CustomAuthServer? _loginServer;
    private CustomAuthServer? _addedServer;
    private Uri? _homepage;
    private FormStep _step;

    public ObservableCollection<AccountServerRow> AuthServers { get; } = [];
    public ObservableCollection<AccountProfileRow> Profiles { get; } = [];

    public AccountWindow()
    {
        using var image = AssetLoader.Open(new Uri("avares://HljuML.App/Assets/player-placeholder.png"));
        _defaultAvatar = new Bitmap(image);
        InitializeComponent();
        DataContext = this;
    }

    internal AccountWindow(IServiceScopeFactory scopes) : this() => _scopes = scopes;

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ClipDialogOverlay();
        if (_scopes is null)
            return;

        var token = _lifetime.Token;
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var servers = await scope.ServiceProvider.GetRequiredService<AuthServerService>().GetAllAuthServers(token);
            var profiles = await scope.ServiceProvider.GetRequiredService<PlayerProfileService>().GetAllPlayerProfiles(token);
            token.ThrowIfCancellationRequested();
            foreach (var server in servers.Data ?? [])
                AuthServers.Add(new AccountServerRow(server));
            foreach (var profile in profiles.Data ?? [])
                AddProfileRow(profile, token);

            AccountStatus.IsVisible = false;
            EmptyProfiles.IsVisible = Profiles.Count == 0;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (DbException)
        {
            if (!token.IsCancellationRequested)
                AccountStatus.Text = "无法读取账户，请关闭后重试。";
        }
    }

    private void ClipDialogOverlay()
    {
        if (OverlayLayer.GetOverlayLayer(this) is not { } overlay)
            return;

        // Arc places its dialog scrim in the window overlay, outside AccountFrame.
        var clip = new RectangleGeometry();
        overlay.Clip = clip;

        void UpdateClip()
        {
            clip.Rect = new Rect(overlay.Bounds.Size);
            clip.RadiusX = clip.RadiusY = AccountFrame.CornerRadius.TopLeft;
        }

        overlay.SizeChanged += (_, _) => UpdateClip();
        AccountFrame.PropertyChanged += (_, change) =>
        {
            if (change.Property == Border.CornerRadiusProperty)
                UpdateClip();
        };
        UpdateClip();
    }

    protected override void OnClosed(EventArgs e)
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        foreach (var avatar in _avatars)
            avatar.Dispose();
        _defaultAvatar.Dispose();
        base.OnClosed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && !e.Handled && !AccountForm.IsOpen)
        {
            e.Handled = true;
            Close();
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnAddServerClicked(object? sender, RoutedEventArgs e)
    {
        ServerAddress.Text = string.Empty;
        OpenForm(FormStep.Address);
    }

    private void OnOfflineClicked(object? sender, RoutedEventArgs e)
    {
        OfflineName.Text = string.Empty;
        OpenForm(FormStep.Offline);
    }

    private void OnServerClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: AccountServerRow row })
            OpenLogin(row.Server);
    }

    private void OnAddedServerClicked(object? sender, RoutedEventArgs e)
    {
        if (_addedServer is { } server)
            OpenLogin(server);
    }

    private void OpenLogin(CustomAuthServer server)
    {
        _loginServer = server;
        _homepage = null;
        LoginServerName.Text = server.Name;
        HomepageLink.IsVisible = false;
        RegistrationLink.IsVisible = !string.IsNullOrWhiteSpace(server.RegisterAddress);
        LoginAccount.Text = string.Empty;
        LoginPassword.Text = string.Empty;
        LoginPassword.IsRevealed = false;
        OpenForm(FormStep.Login);
        _ = LoadHomepage(server, _lifetime.Token);
    }

    private void OpenForm(FormStep step)
    {
        ClearErrors();
        ShowStep(step);
        AccountForm.Open();
    }

    private void ShowStep(FormStep step)
    {
        _step = step;
        ServerAddress.IsVisible = step == FormStep.Address;
        ServerPreview.IsVisible = step == FormStep.Preview;
        LoginFields.IsVisible = step == FormStep.Login;
        OfflineName.IsVisible = step == FormStep.Offline;
        RoleList.IsVisible = step == FormStep.Role;
        BackButton.IsVisible = step == FormStep.Preview;
        AccountForm.Title = step switch
        {
            FormStep.Login => "添加外置登录账户",
            FormStep.Offline => "添加离线账户",
            FormStep.Role => "选择游戏角色",
            _ => "添加认证服务器"
        };
        ConfirmButton.Content = step switch
        {
            FormStep.Address => "下一步",
            FormStep.Login => "登录",
            FormStep.Role => "选择角色",
            _ => "添加"
        };
        UpdateBusyState();
    }

    private void UpdateBusyState()
    {
        var busy = _formOperation is not null;
        ConfirmButton.IsLoading = busy && _step != FormStep.Role;
        ConfirmButton.IsEnabled = !busy || _step == FormStep.Role;
        ServerAddress.IsEnabled = LoginFields.IsEnabled = OfflineName.IsEnabled = BackButton.IsEnabled = !busy;
    }

    private void OnBackClicked(object? sender, RoutedEventArgs e)
    {
        _preview = null;
        ClearErrors();
        ShowStep(FormStep.Address);
        ServerAddress.Focus();
    }

    private void OnCancelFormClicked(object? sender, RoutedEventArgs e) => AccountForm.Close(null);

    private void OnFormOpened(object? sender, EventArgs e)
    {
        if (_step == FormStep.Address)
            ServerAddress.Focus();
        else if (_step == FormStep.Login)
            LoginAccount.Focus();
        else if (_step == FormStep.Offline)
            OfflineName.Focus();
    }

    private void OnFormClosed(object? sender, OverlayClosedEventArgs e)
    {
        _formOperation?.Cancel();
        _formOperation = null;
        _roleSelection = null;
        _preview = null;
        _loginServer = null;
        LoginPassword.Text = string.Empty;
        RoleList.ItemsSource = null;
        UpdateBusyState();
    }

    private async void OnConfirmClicked(object? sender, RoutedEventArgs e)
    {
        if (_step == FormStep.Role)
        {
            if (_roleSelection is { } selection && RoleList.SelectedIndex >= 0)
            {
                selection.TrySetResult(RoleList.SelectedIndex);
                _roleSelection = null;
                ShowStep(FormStep.Login);
            }
            return;
        }

        if (_formOperation is not null || _scopes is null)
            return;

        ClearErrors();
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _formOperation = operation;
        UpdateBusyState();
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var token = operation.Token;
            switch (_step)
            {
                case FormStep.Address:
                    await PreviewServer(scope.ServiceProvider.GetRequiredService<AuthServerService>(), token);
                    break;
                case FormStep.Preview:
                    await SaveServer(scope.ServiceProvider.GetRequiredService<AuthServerService>(), token);
                    break;
                case FormStep.Login:
                case FormStep.Offline:
                    await SaveProfile(scope.ServiceProvider.GetRequiredService<PlayerProfileService>(), token);
                    break;
            }
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            if (!operation.IsCancellationRequested)
                SetFormError("无法保存，请重试。");
        }
        finally
        {
            if (_formOperation == operation)
            {
                _formOperation = null;
                UpdateBusyState();
            }
        }
    }

    private async Task PreviewServer(AuthServerService service, CancellationToken token)
    {
        var result = await service.GetYggdrasilServerMetadata(ServerAddress.Text?.Trim() ?? string.Empty, token);
        token.ThrowIfCancellationRequested();
        if (result.Data is not { } preview)
        {
            ServerAddress.ErrorMessage = result.Message;
            return;
        }

        _preview = preview;
        PreviewName.Text = preview.DisplayName;
        PreviewAddress.Text = preview.ApiUri.AbsoluteUri;
        ShowStep(FormStep.Preview);
    }

    private async Task SaveServer(AuthServerService service, CancellationToken token)
    {
        if (_preview is not { } preview)
            return;

        var result = await service.CreateAuthServer(preview, token);
        token.ThrowIfCancellationRequested();
        if (result.Data is not { } server)
        {
            SetFormError(result.Message);
            return;
        }

        AuthServers.Add(new AccountServerRow(server));
        _addedServer = server;
        AddedServerName.Text = server.Name;
        AddedServerAddress.Text = server.Address;
        AddedServerCard.IsVisible = true;
        AccountForm.Close(null);
    }

    private async Task SaveProfile(PlayerProfileService service, CancellationToken token)
    {
        ServiceResult<PlayerProfile> result;
        if (_step == FormStep.Offline)
        {
            var name = OfflineName.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                OfflineName.ErrorMessage = "请输入玩家名。";
                return;
            }
            result = await service.CreatePlayerProfile(new PlayerProfile { PlayerName = name }, token);
        }
        else
        {
            var account = LoginAccount.Text;
            var password = LoginPassword.Text;
            if (string.IsNullOrWhiteSpace(account))
                LoginAccount.ErrorMessage = "请输入用户名或邮箱。";
            if (string.IsNullOrEmpty(password))
                LoginPassword.ErrorMessage = "请输入密码。";
            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrEmpty(password) || _loginServer is null)
                return;

            var selector = new RoleSelector(roles => ChooseRole(roles, token));
            result = await service.CreateCustomPlayerProfile(_loginServer.Id, account, password, selector, token);
        }

        token.ThrowIfCancellationRequested();
        if (result.Data is not { } profile)
        {
            SetFormError(result.Message);
            return;
        }

        AddProfileRow(profile, _lifetime.Token);
        EmptyProfiles.IsVisible = false;
        AccountForm.Close(null);
    }

    private async Task<int> ChooseRole(List<string> roles, CancellationToken token)
    {
        var selection = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            token.ThrowIfCancellationRequested();
            _roleSelection = selection;
            RoleList.ItemsSource = roles;
            RoleList.SelectedIndex = 0;
            ShowStep(FormStep.Role);
            RoleList.Focus();
        });
        return await selection.Task.WaitAsync(token);
    }

    private void OnRegistrationClicked(object? sender, RoutedEventArgs e)
    {
        if (TryGetWebLink(_loginServer?.RegisterAddress) is { } link)
            OpenBrowser(link);
    }

    private void OnHomepageClicked(object? sender, RoutedEventArgs e)
    {
        if (_homepage is { } link)
            OpenBrowser(link);
    }

    private async Task LoadHomepage(CustomAuthServer server, CancellationToken token)
    {
        if (_scopes is null)
            return;

        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<AuthServerService>();
            var result = await service.GetYggdrasilServerMetadata(server.Address, token);
            if (token.IsCancellationRequested || _loginServer != server)
                return;

            _homepage = TryGetWebLink(result.Data?.Metadata.Meta?.Links?.Homepage);
            HomepageLink.IsVisible = _homepage is not null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private static Uri? TryGetWebLink(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.UserInfo.Length == 0
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ? uri : null;

    private void OpenBrowser(Uri uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception)
        {
            SetFormError("无法打开链接。");
        }
    }

    private void AddProfileRow(PlayerProfile profile, CancellationToken token)
    {
        var provider = profile.AuthMethod switch
        {
            AuthMethod.Custom => profile.CustomAuthServer?.Name ?? "外置登录",
            AuthMethod.Mojang => "微软账户",
            _ => "离线模式"
        };
        var details = string.IsNullOrWhiteSpace(profile.PlayerEmail) ? provider : $"{provider} · {profile.PlayerEmail}";
        var row = new AccountProfileRow(profile.Id, profile.PlayerName, details, _defaultAvatar);
        Profiles.Add(row);
        if (profile.AuthMethod == AuthMethod.Custom)
            _ = LoadAvatar(profile, row, token);
    }

    private async Task LoadAvatar(PlayerProfile profile, AccountProfileRow row, CancellationToken token)
    {
        try
        {
            var path = await PlayerProfileService.DownloadProfileSkin(profile);
            if (token.IsCancellationRequested || path is null || PlayerSkinAvatar.FromSkin(path) is not { } avatar)
                return;

            var index = Profiles.IndexOf(row);
            if (index < 0)
            {
                avatar.Dispose();
                return;
            }

            _avatars.Add(avatar);
            Profiles[index] = row with { Avatar = avatar };
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException) { }
    }

    private void ClearErrors()
    {
        ServerAddress.ErrorMessage = LoginAccount.ErrorMessage = LoginPassword.ErrorMessage = OfflineName.ErrorMessage = null;
        FormError.IsVisible = false;
    }

    private void SetFormError(string message)
    {
        FormError.Text = message;
        FormError.IsVisible = true;
    }

    private sealed class RoleSelector(Func<List<string>, Task<int>> select) : ILoginGui
    {
        public Task<int> SelectAuth(List<string> items) => select(items);
    }
}

public sealed record AccountProfileRow(Guid Id, string Name, string Details, Bitmap Avatar);

public sealed record AccountServerRow(CustomAuthServer Server)
{
    public string Host => Uri.TryCreate(Server.Address, UriKind.Absolute, out var uri)
        ? uri.Authority : Server.Address;
}
