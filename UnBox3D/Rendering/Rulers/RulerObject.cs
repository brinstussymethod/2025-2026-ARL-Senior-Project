using OpenTK.Mathematics;

namespace UnBox3D.Rendering.Rulers
{
    /// <summary>
    /// Data model for a single ruler placed in the scene.
    /// All real-world display values are derived from HeightWorld + IScaleSettings — no per-ruler unit or scale.
    /// </summary>
    public class RulerObject
    {
        public Guid    Id           { get; } = Guid.NewGuid();
        /// <summary>World-space base position. Y is always 0 (ground plane).</summary>
        public Vector3 BasePosition { get; set; }
        /// <summary>Height in world units (1 world unit = 1 m by default).</summary>
        public float   HeightWorld  { get; set; } = 1.0f;
        public bool    IsSelected   { get; set; }
    }
}
