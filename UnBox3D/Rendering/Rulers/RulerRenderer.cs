using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using UnBox3D.Rendering.OpenGL;

namespace UnBox3D.Rendering.Rulers
{
    /// <summary>
    /// Renders all placed rulers as OpenGL geometry.
    /// Each ruler consists of:
    ///   • Base disc  — small flat circle on the ground
    ///   • Stem line  — vertical line from base to cap
    ///   • Cap disc   — small flat circle at the top (reference plane)
    ///
    /// Geometry is in render space (renderX=worldX, renderY=worldZ, renderZ=worldY).
    /// "Up" in world = +render Z direction — same convention as the rest of the app.
    /// All geometry is rebuilt whenever <see cref="BuildOrRebuild"/> is called.
    /// </summary>
    public sealed class RulerRenderer : IDisposable
    {
        // ── Constants ──────────────────────────────────────────────────────
        private const int   DiscSegs   = 24;
        private const float BaseDiscR  = 0.08f; // base disc radius as fraction of ruler height
        private const float CapDiscR   = 0.06f;
        private const float MinDiscR   = 0.04f; // absolute min disc radius (for short rulers)

        // ── Colors ─────────────────────────────────────────────────────────
        private static readonly Vector4 ColNormal   = new(0.25f, 0.25f, 0.25f, 0.90f);
        private static readonly Vector4 ColSelected = new(0.00f, 0.80f, 0.62f, 1.00f);

        // ── Per-ruler GPU data ──────────────────────────────────────────────
        private readonly Dictionary<Guid, RulerGpuData> _gpuData = new();
        private bool _disposed;

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>
        /// (Re)builds all GPU buffers for <paramref name="ruler"/>.
        /// Call this after any change to BasePosition or HeightWorld.
        /// </summary>
        public void BuildOrRebuild(RulerObject ruler)
        {
            if (_disposed) return;

            if (_gpuData.TryGetValue(ruler.Id, out var old))
                old.Dispose();

            float h  = Math.Max(ruler.HeightWorld, 0.01f);
            float br = Math.Max(h * BaseDiscR, MinDiscR);
            float cr = Math.Max(h * CapDiscR,  MinDiscR * 0.8f);

            // Render-space positions: render Y is "up" (camera _up = UnitY).
            // BasePosition stores render-space (X, 0, Z); base sits on the Y=0 grid plane.
            float rx = ruler.BasePosition.X;
            float rz = ruler.BasePosition.Z;
            var   baseR = new Vector3(rx, 0f, rz);
            var   capR  = new Vector3(rx, h,  rz);

            var data = new RulerGpuData
            {
                VaoBase  = Upload(BuildDisc(baseR, br), out int vboBase),
                VboBase  = vboBase,
                VaoStem  = Upload(BuildLine(baseR, capR), out int vboStem),
                VboStem  = vboStem,
                VaoCap   = Upload(BuildDisc(capR,  cr), out int vboCap),
                VboCap   = vboCap,
                VertCountBase  = DiscSegs + 2,
                VertCountShaft = 2,
                BaseRender     = baseR,
            };

            _gpuData[ruler.Id] = data;
        }

        /// <summary>Renders all rulers with depth test disabled (always on top).</summary>
        public void Render(IEnumerable<RulerObject> rulers, Matrix4 view, Matrix4 projection)
        {
            if (_disposed) return;

            var shader = ShaderManager.GizmoShader;
            shader.Use();
            shader.SetMatrix4("model",      Matrix4.Identity);
            shader.SetMatrix4("view",       view);
            shader.SetMatrix4("projection", projection);

            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            foreach (var ruler in rulers)
            {
                if (!_gpuData.TryGetValue(ruler.Id, out var d)) continue;

                bool sel  = ruler.IsSelected;
                var  body = sel ? ColSelected : ColNormal;

                // Base disc
                DrawVAO(d.VaoBase, d.VertCountBase,  PrimitiveType.TriangleFan, body, shader);
                DrawVAO(d.VaoBase, d.VertCountBase,  PrimitiveType.LineLoop,    body, shader);

                // Stem
                GL.LineWidth(3f);
                DrawVAO(d.VaoStem, d.VertCountShaft, PrimitiveType.Lines,       body, shader);
                GL.LineWidth(1f);

                // Cap disc
                DrawVAO(d.VaoCap,  d.VertCountBase,  PrimitiveType.TriangleFan, body, shader);
                DrawVAO(d.VaoCap,  d.VertCountBase,  PrimitiveType.LineLoop,    body, shader);
            }

            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);
            GL.BindVertexArray(0);
        }

        /// <summary>Returns the base disc centre (render space) for hit-testing.</summary>
        public bool TryGetBaseCenter(Guid id, out Vector3 baseCenter)
        {
            if (_gpuData.TryGetValue(id, out var d))
            {
                baseCenter = d.BaseRender;
                return true;
            }
            baseCenter = Vector3.Zero;
            return false;
        }

        // ── Geometry builders ──────────────────────────────────────────────

        /// <summary>
        /// Flat horizontal disc (TriangleFan) in the XZ plane.
        /// Layout: [center, ring_0, ring_1, …, ring_n, ring_0] — DiscSegs+2 vertices.
        /// </summary>
        private static float[] BuildDisc(Vector3 center, float r)
        {
            int     n = DiscSegs;
            float[] v = new float[(n + 2) * 3];
            v[0] = center.X; v[1] = center.Y; v[2] = center.Z;
            for (int i = 0; i <= n; i++)
            {
                float a   = 2f * MathF.PI * i / n;
                int   j   = (i + 1) * 3;
                v[j] = center.X + r * MathF.Cos(a); v[j+1] = center.Y; v[j+2] = center.Z + r * MathF.Sin(a);
            }
            return v;
        }

        private static float[] BuildLine(Vector3 a, Vector3 b)
            => new[] { a.X, a.Y, a.Z, b.X, b.Y, b.Z };

        // ── Upload / draw helpers ──────────────────────────────────────────

        private static int Upload(float[] verts, out int vbo)
        {
            int vao = GL.GenVertexArray();
            vbo     = GL.GenBuffer();
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts, BufferUsageHint.DynamicDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.BindVertexArray(0);
            return vao;
        }

        private static void DrawVAO(int vao, int count, PrimitiveType prim, Vector4 color, Shader shader)
        {
            shader.SetVector4("uColor", color);
            GL.BindVertexArray(vao);
            GL.DrawArrays(prim, 0, count);
        }

        // ── Cleanup ────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            foreach (var d in _gpuData.Values) d.Dispose();
            _gpuData.Clear();
            _disposed = true;
        }

        // ── Inner data struct ──────────────────────────────────────────────

        private struct RulerGpuData : IDisposable
        {
            public int VaoBase,  VboBase;
            public int VaoStem,  VboStem;
            public int VaoCap,   VboCap;
            public int VertCountBase;
            public int VertCountShaft;
            public Vector3 BaseRender;

            public void Dispose()
            {
                void Del(int vao, int vbo)
                {
                    if (vao != 0) GL.DeleteVertexArray(vao);
                    if (vbo != 0) GL.DeleteBuffer(vbo);
                }
                Del(VaoBase, VboBase);
                Del(VaoStem, VboStem);
                Del(VaoCap,  VboCap);
                VaoBase = VboBase = VaoStem = VboStem = VaoCap = VboCap = 0;
            }
        }
    }
}
