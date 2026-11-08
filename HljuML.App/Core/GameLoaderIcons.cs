using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ColorMC.Core.Objs;

namespace HljuML.App.Core;

internal static class GameLoaderIcons
{
    private static readonly IReadOnlyDictionary<Loaders, Bitmap> Images = new Dictionary<Loaders, Bitmap>
    {
        [Loaders.Normal] = Load("minecraft"),
        [Loaders.Forge] = Load("forge"),
        [Loaders.NeoForge] = Load("neoforge"),
        [Loaders.Fabric] = Load("fabric"),
        [Loaders.OptiFine] = Load("optifine")
    };

    internal static Bitmap Get(Loaders loader) => Images[loader];

    private static Bitmap Load(string name)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://HljuML.App/Assets/loaders/{name}.png"));
        return new Bitmap(stream);
    }
}
