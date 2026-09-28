namespace MemoryInSchritten.Client.ViewModels;

public sealed class PlayerViewModel : ObservableObject
{
    private string name = "";
    private int? rating;
    private int score;
    private bool isConnected = true;
    private bool isActive;

    public string Name
    {
        get => name;
        set => Set(ref name, value);
    }

    /// <summary>Null in unrated (local) games.</summary>
    public int? Rating
    {
        get => rating;
        set => Set(ref rating, value);
    }

    public int Score
    {
        get => score;
        set => Set(ref score, value);
    }

    public bool IsConnected
    {
        get => isConnected;
        set => Set(ref isConnected, value);
    }

    public bool IsActive
    {
        get => isActive;
        set => Set(ref isActive, value);
    }

    public void Reset()
    {
        Name = "";
        Rating = null;
        Score = 0;
        IsConnected = true;
        IsActive = false;
    }
}
