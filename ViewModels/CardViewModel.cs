using System.Windows.Media;
using MemoryInSchritten.Client.Cards;

namespace MemoryInSchritten.Client.ViewModels;

public sealed class CardViewModel(int index) : ObservableObject
{
    private ImageSource? face;
    private bool isMatched;

    public int Index { get; } = index;
    public ImageSource Image => face ?? CardDeck.Back;
    public bool IsFaceUp => face is not null;

    public bool IsMatched
    {
        get => isMatched;
        set => Set(ref isMatched, value);
    }

    public void Reveal(ImageSource image) => SetFace(image);

    public void Cover() => SetFace(null);

    private void SetFace(ImageSource? image)
    {
        if (ReferenceEquals(face, image)) return;
        face = image;
        Raise(nameof(Image));
        Raise(nameof(IsFaceUp));
    }
}
