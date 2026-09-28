using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MemoryInSchritten.Client.Cards;

/// <summary>Decoded, frozen card faces of one artwork set. Pair ids map onto faces by position.</summary>
public sealed class CardDeck
{
    private const int DecodeWidth = 256;
    private static readonly string AssetDirectory = Path.Combine(AppContext.BaseDirectory, "bilder");
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp"];

    private readonly ImageSource[] faces;

    private CardDeck(ImageSource[] faces) => this.faces = faces;

    public static ImageSource Back { get; } = Decode(Path.Combine(AssetDirectory, "starsolid.gif"));

    public int PairCount => faces.Length;

    public static CardDeck Load(string set)
    {
        var files = Directory.GetFiles(Path.Combine(AssetDirectory, set))
            .Where(file => ImageExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (files.Length == 0) throw new InvalidOperationException($"Kartensatz \"{set}\" enthält keine Bilder.");
        return new CardDeck(files.Select(Decode).ToArray());
    }

    public ImageSource Face(int pairId) => faces[pairId % faces.Length];

    private static BitmapImage Decode(string file)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(file);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = DecodeWidth;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
