using Avalonia.Controls;
using HljuML.App.Core;

namespace HljuML.App.Views.Settings;

internal static class SettingsEditing
{
    public static void Commit(object? context, object? field)
    {
        if (context is SettingsViewModel model && field is Control { Tag: string name }) model.Commit(name);
    }
}
