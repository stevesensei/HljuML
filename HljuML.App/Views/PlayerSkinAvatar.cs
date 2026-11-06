using Avalonia.Media.Imaging;
using SkiaSharp;

namespace HljuML.App.Views;

internal static class PlayerSkinAvatar
{
    internal static Bitmap? FromSkin(string path)
    {
        using var image = CropHead(path);
        if (image is null)
            return null;

        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = encoded.AsStream();
        return new Bitmap(stream);
    }

    internal static SKImage? CropHead(string path)
    {
        using var codec = SKCodec.Create(path);
        if (codec is null)
            return null;

        var (width, height) = (codec.Info.Width, codec.Info.Height);
        if (width is < 64 or > 1024 || width % 64 != 0 || (height != width && height != width / 2))
            return null;

        using var skin = SKBitmap.Decode(codec);
        if (skin is null)
            return null;

        var scale = width / 64;
        using var texture = SKImage.FromBitmap(skin);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var destination = new SKRect(0, 0, 64, 64);
        var sampling = new SKSamplingOptions(SKFilterMode.Nearest);
        canvas.DrawImage(texture, new SKRect(8 * scale, 8 * scale, 16 * scale, 16 * scale), destination, sampling);
        canvas.DrawImage(texture, new SKRect(40 * scale, 8 * scale, 48 * scale, 16 * scale), destination, sampling);
        return surface.Snapshot();
    }
}
