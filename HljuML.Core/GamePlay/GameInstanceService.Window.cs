using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace HljuML.Core.GamePlay;

public sealed partial class GameInstanceService
{
    /// <summary>等待 GLFW 创建实际窗口后请求系统最大化，不用全屏或屏幕分辨率模拟。</summary>
    private async Task MaximizeGameWindowAsync(Process process, CancellationToken stopping)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            while (!process.HasExited)
            {
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(process.MainWindowHandle, 3);
                    return;
                }

                await Task.Delay(250, timeout.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception)
        {
            logger.LogWarning(error, "无法最大化游戏窗口");
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);
}