namespace UnBox3D.Rendering.Rulers
{
    /// <summary>
    /// Stateless unit conversion helpers.
    /// All conversions use millimetres as the canonical intermediate.
    /// </summary>
    public static class RulerUnitConverter
    {
        // mm per unit
        private static readonly Dictionary<RulerUnit, double> MmFactor = new()
        {
            [RulerUnit.Mm] = 1.0,
            [RulerUnit.Cm] = 10.0,
            [RulerUnit.M]  = 1000.0,
            [RulerUnit.In] = 25.4,
            [RulerUnit.Ft] = 304.8,
        };

        public static double ToMm(double value, RulerUnit unit)   => value * MmFactor[unit];
        public static double FromMm(double mm, RulerUnit unit)     => mm / MmFactor[unit];
        public static double Convert(double value, RulerUnit from, RulerUnit to)
            => FromMm(ToMm(value, from), to);

        /// <summary>World units → real-world millimetres.</summary>
        public static double WorldUnitsToRealMm(float worldHeight, double mmPerWorldUnit)
            => worldHeight * mmPerWorldUnit;

        public static string UnitSymbol(RulerUnit unit) => unit switch
        {
            RulerUnit.Mm => "mm",
            RulerUnit.Cm => "cm",
            RulerUnit.M  => "m",
            RulerUnit.In => "in",
            RulerUnit.Ft => "ft",
            _            => "?"
        };
    }
}
