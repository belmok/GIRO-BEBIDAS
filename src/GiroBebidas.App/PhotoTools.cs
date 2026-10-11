using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GiroBebidas.App;

public static class PhotoTools
{
    public static ImageSource? Decode(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        try
        {
            using var stream = new MemoryStream(Convert.FromBase64String(base64));
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 600; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException or IOException or System.Runtime.InteropServices.COMException or ArgumentException) { return null; }
    }
    public static string Import(string path)
    {
        if (new FileInfo(path).Length > 10_000_000) throw new InvalidOperationException("Escolha uma imagem de até 10 MB.");
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        if ((long)frame.PixelWidth * frame.PixelHeight > 40_000_000) throw new InvalidOperationException("Escolha uma imagem com menos de 40 megapixels.");
        double scale = Math.Min(1, 600d / Math.Max(frame.PixelWidth, frame.PixelHeight));
        BitmapSource resized = scale < 1 ? new TransformedBitmap(frame, new ScaleTransform(scale, scale)) : frame;
        var encoder = new JpegBitmapEncoder { QualityLevel = 88 }; encoder.Frames.Add(BitmapFrame.Create(resized));
        using var output = new MemoryStream(); encoder.Save(output);
        return Convert.ToBase64String(output.ToArray());
    }
}
