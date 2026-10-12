using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlatPlate.App.Services;

/// <summary>Optional local image preview. The host supplies the root for relative paths.</summary>
public sealed class RecipeImageService
{
    private readonly string _root;

    public RecipeImageService(string relativePathRoot)
    {
        _root = Path.GetFullPath(relativePathRoot);
    }

    public ImageSource? Load(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return null;
        try
        {
            var path = imagePath;
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
            {
                if (!uri.IsFile || uri.IsUnc) return null;
                path = uri.LocalPath;
            }
            var fullPath = Path.GetFullPath(path, _root);
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal)) return null;
            using var stream = File.OpenRead(fullPath);
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit();
            image.Freeze(); return image;
        }
        catch (Exception exception) when (exception is IOException or FileFormatException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
