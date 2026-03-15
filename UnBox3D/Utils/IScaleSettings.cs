namespace UnBox3D.Utils
{
    /// <summary>
    /// Holds the single global scene scale: how many millimetres equal one world unit.
    /// All ruler labels read from here.  Default: 1 world unit = 1 m = 1000 mm.
    ///
    /// The scale is intentionally session-only (in-memory).  It is changed exclusively by
    /// typing a value into a ruler label — never by the height-drag interaction.
    /// Restarting the app or clearing the scene resets it to the 1 m/unit default.
    /// </summary>
    public interface IScaleSettings
    {
        double MmPerWorldUnit { get; }
        void SetScale(double mmPerWorldUnit);
    }

    public class ScaleSettings : IScaleSettings
    {
        private const double DefaultMm = 1000.0; // 1 world unit = 1 m = 1000 mm

        private double _mmPerWorldUnit = DefaultMm;

        public double MmPerWorldUnit => _mmPerWorldUnit;

        public void SetScale(double mmPerWorldUnit)
        {
            // Silently ignore nonsense values — a ≤0 scale has no physical meaning.
            if (mmPerWorldUnit <= 0) return;
            _mmPerWorldUnit = mmPerWorldUnit;
        }
    }
}
