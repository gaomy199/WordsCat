namespace ReadDock;

// One content zoom preference for both main panes and every new selection popup.
internal sealed class ReadingZoom(AppConfig config, Action save)
{
    public double Factor => config.ReadingZoomFactor;
    public event Action? Changed;

    internal static double Normalize(double factor) => double.IsFinite(factor) && factor > 0
        ? Math.Clamp(factor, 0.25, 5) : 1;

    public void SetFactor(double factor)
    {
        factor = Normalize(factor);
        if (Math.Abs(Factor - factor) < 0.0001) return;
        config.ReadingZoomFactor = factor;
        Changed?.Invoke();
        save();
    }
}
