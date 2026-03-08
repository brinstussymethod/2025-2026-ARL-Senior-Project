using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using UnBox3D.Rendering.OpenGL;

namespace UnBox3D.Rendering
{
    /// <summary>
    /// Renders the full transform gizmo:
    ///   • Three coloured rings  (rotate X=red, Y=green, Z=blue)
    ///   • A white square handle on each ring so the user can clearly see where to grab
    ///   • Three axis arrows from the mesh centre (translate X/Y/Z)
    /// All geometry is rebuilt every time <see cref="UpdateRings"/> is called.
    /// </summary>
    public sealed class GizmoRenderer : IDisposable
    {
        // ── Constants ──────────────────────────────────────────────────────
        private const int RingSegs   = 64;
        private const int DiscSegs   = 20;   // segments for the filled handle disc
        private const int ConeSegs   = 8;    // triangles for the arrowhead cone

        // ── VAO / VBO handles ──────────────────────────────────────────────
        // Rings
        private int _vaoRX, _vboRX, _vaoRY, _vboRY, _vaoRZ, _vboRZ;
        // Handles (filled disc at grab point on each ring)
        private int _vaoHX, _vboHX, _vaoHY, _vboHY, _vaoHZ, _vboHZ;
        // Arrow shafts
        private int _vaoAX, _vboAX, _vaoAY, _vboAY, _vaoAZ, _vboAZ;
        // Arrowhead cones
        private int _vaoCX, _vboCX, _vaoCY, _vboCY, _vaoCZ, _vboCZ;

        private bool _initialized = false;
        private bool _disposed    = false;

        // ── Colors ─────────────────────────────────────────────────────────
        private static readonly Vector4 ColX      = new(1.00f, 0.22f, 0.22f, 1f); // red
        private static readonly Vector4 ColY      = new(0.22f, 1.00f, 0.22f, 1f); // green
        private static readonly Vector4 ColZ      = new(0.22f, 0.45f, 1.00f, 1f); // blue
        private static readonly Vector4 ColHandle = new(1.00f, 1.00f, 1.00f, 1f); // white

        // ── Public state ───────────────────────────────────────────────────
        /// <summary>Render-space centre last passed to <see cref="UpdateRings"/>.</summary>
        public Vector3 Center { get; private set; }
        /// <summary>Ring radius last passed to <see cref="UpdateRings"/>.</summary>
        public float   Radius { get; private set; }

        // ── Static position helpers (shared with GimbalState for hit testing) ──

        // X ring is in the YZ plane.  Handle sits at angle 0° → (cx, cy+r, cz).
        public static Vector3 HandlePosX(Vector3 c, float r) => c + new Vector3(0f,  r,  0f);
        // Y ring is in the XZ plane.  Handle sits at angle 0° → (cx+r, cy, cz).
        public static Vector3 HandlePosY(Vector3 c, float r) => c + new Vector3( r, 0f,  0f);
        // Z ring is in the XY plane.  Handle sits at angle 270° → (cx, cy-r, cz).
        public static Vector3 HandlePosZ(Vector3 c, float r) => c + new Vector3(0f, -r,  0f);

        // Arrow tips — slightly inside the ring radius so arrows sit neatly inside.
        public static float   ArrowLen(float r)             => r * 0.88f;
        public static Vector3 ArrowTipX(Vector3 c, float r) => c + new Vector3(ArrowLen(r), 0f,          0f);
        public static Vector3 ArrowTipY(Vector3 c, float r) => c + new Vector3(0f,          ArrowLen(r), 0f);
        public static Vector3 ArrowTipZ(Vector3 c, float r) => c + new Vector3(0f,          0f,          ArrowLen(r));

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>Rebuild all geometry for the given render-space centre and ring radius.</summary>
        public void UpdateRings(Vector3 renderCenter, float radius)
        {
            if (_initialized) DeleteBuffers();
            Center = renderCenter;
            Radius = radius;

            float headLen  = radius * 0.18f;
            float headR    = radius * 0.08f;
            float handleR  = radius * 0.13f; // disc radius for the grab handle — large enough to click

            // ── Rings ────────────────────────────────────────────────────
            (_vaoRX, _vboRX) = Upload(BuildCircle(renderCenter, radius, Axis.X));
            (_vaoRY, _vboRY) = Upload(BuildCircle(renderCenter, radius, Axis.Y));
            (_vaoRZ, _vboRZ) = Upload(BuildCircle(renderCenter, radius, Axis.Z));

            // ── Handle discs (small filled circle at grab point) ─────────
            (_vaoHX, _vboHX) = Upload(BuildDisc(HandlePosX(renderCenter, radius), handleR, Axis.X));
            (_vaoHY, _vboHY) = Upload(BuildDisc(HandlePosY(renderCenter, radius), handleR, Axis.Y));
            (_vaoHZ, _vboHZ) = Upload(BuildDisc(HandlePosZ(renderCenter, radius), handleR, Axis.Z));

            // ── Arrow shafts ─────────────────────────────────────────────
            (_vaoAX, _vboAX) = Upload(BuildLine(renderCenter, ArrowTipX(renderCenter, radius)));
            (_vaoAY, _vboAY) = Upload(BuildLine(renderCenter, ArrowTipY(renderCenter, radius)));
            (_vaoAZ, _vboAZ) = Upload(BuildLine(renderCenter, ArrowTipZ(renderCenter, radius)));

            // ── Arrowhead cones ──────────────────────────────────────────
            (_vaoCX, _vboCX) = Upload(BuildCone(ArrowTipX(renderCenter, radius), new Vector3(-1f, 0f, 0f), headLen, headR, Axis.X));
            (_vaoCY, _vboCY) = Upload(BuildCone(ArrowTipY(renderCenter, radius), new Vector3(0f, -1f, 0f), headLen, headR, Axis.Y));
            (_vaoCZ, _vboCZ) = Upload(BuildCone(ArrowTipZ(renderCenter, radius), new Vector3(0f, 0f, -1f), headLen, headR, Axis.Z));

            _initialized = true;
        }

        /// <summary>Draw the gizmo.  <paramref name="mode"/> controls which parts are visible.</summary>
        public void Render(Matrix4 view, Matrix4 projection, GizmoMode mode = GizmoMode.Full)
        {
            if (!_initialized || mode == GizmoMode.None) return;

            var shader = ShaderManager.GizmoShader;
            shader.Use();
            shader.SetMatrix4("model",      Matrix4.Identity);
            shader.SetMatrix4("view",       view);
            shader.SetMatrix4("projection", projection);

            GL.Disable(EnableCap.DepthTest); // always draw on top

            bool showRings   = mode == GizmoMode.RingsOnly || mode == GizmoMode.Full;
            bool showArrows  = mode == GizmoMode.ArrowsOnly || mode == GizmoMode.Full;
            bool showHandles = mode == GizmoMode.Full;

            // ── Rings ───────────────────────────────────────────────────
            if (showRings)
            {
                GL.LineWidth(3f);
                DrawVAO(_vaoRX, RingSegs, PrimitiveType.LineLoop, ColX, shader);
                DrawVAO(_vaoRY, RingSegs, PrimitiveType.LineLoop, ColY, shader);
                DrawVAO(_vaoRZ, RingSegs, PrimitiveType.LineLoop, ColZ, shader);
            }

            // ── Arrow shafts + cones ──────────────────────────────────────
            if (showArrows)
            {
                GL.LineWidth(4f);
                DrawVAO(_vaoAX, 2,            PrimitiveType.Lines,     ColX, shader);
                DrawVAO(_vaoAY, 2,            PrimitiveType.Lines,     ColY, shader);
                DrawVAO(_vaoAZ, 2,            PrimitiveType.Lines,     ColZ, shader);

                GL.LineWidth(1f);
                DrawVAO(_vaoCX, ConeSegs * 3, PrimitiveType.Triangles, ColX, shader);
                DrawVAO(_vaoCY, ConeSegs * 3, PrimitiveType.Triangles, ColY, shader);
                DrawVAO(_vaoCZ, ConeSegs * 3, PrimitiveType.Triangles, ColZ, shader);
            }

            // ── Handle discs (Gimbal only) ──────────────────────────────────
            if (showHandles)
            {
                GL.LineWidth(2f);
                DrawVAO(_vaoHX, DiscSegs + 2, PrimitiveType.TriangleFan, ColHandle, shader);
                DrawVAO(_vaoHY, DiscSegs + 2, PrimitiveType.TriangleFan, ColHandle, shader);
                DrawVAO(_vaoHZ, DiscSegs + 2, PrimitiveType.TriangleFan, ColHandle, shader);
                // Coloured outline so axes are clearly labelled
                DrawVAO(_vaoHX, DiscSegs + 2, PrimitiveType.LineLoop,    ColX,      shader);
                DrawVAO(_vaoHY, DiscSegs + 2, PrimitiveType.LineLoop,    ColY,      shader);
                DrawVAO(_vaoHZ, DiscSegs + 2, PrimitiveType.LineLoop,    ColZ,      shader);
            }

            GL.Enable(EnableCap.DepthTest);
            GL.LineWidth(1f);
            GL.BindVertexArray(0);
        }

        // ── Geometry builders ──────────────────────────────────────────────

        private enum Axis { X, Y, Z }

        /// <summary>Full circle (LineLoop) lying in the plane perpendicular to <paramref name="axis"/>.</summary>
        private static float[] BuildCircle(Vector3 c, float r, Axis axis)
        {
            float[] v = new float[RingSegs * 3];
            for (int i = 0; i < RingSegs; i++)
            {
                float a = 2f * MathF.PI * i / RingSegs;
                float cos = r * MathF.Cos(a), sin = r * MathF.Sin(a);
                int   j   = i * 3;
                switch (axis)
                {
                    case Axis.X: v[j]=c.X;     v[j+1]=c.Y+cos; v[j+2]=c.Z+sin; break;
                    case Axis.Y: v[j]=c.X+cos; v[j+1]=c.Y;     v[j+2]=c.Z+sin; break;
                    case Axis.Z: v[j]=c.X+cos; v[j+1]=c.Y+sin; v[j+2]=c.Z;     break;
                }
            }
            return v;
        }

        /// <summary>
        /// Small filled disc (TriangleFan) lying in the same plane as the ring.
        /// Vertex layout: [center, ring_0, ring_1, …, ring_n, ring_0]  (n+2 vertices).
        /// </summary>
        private static float[] BuildDisc(Vector3 center, float r, Axis ringAxis)
        {
            int     n = DiscSegs;
            float[] v = new float[(n + 2) * 3];
            // First vertex = centre of fan
            v[0] = center.X; v[1] = center.Y; v[2] = center.Z;
            for (int i = 0; i <= n; i++)
            {
                float a   = 2f * MathF.PI * i / n;
                float cos = r * MathF.Cos(a), sin = r * MathF.Sin(a);
                int   j   = (i + 1) * 3;
                switch (ringAxis)
                {
                    case Axis.X: v[j]=center.X;     v[j+1]=center.Y+cos; v[j+2]=center.Z+sin; break;
                    case Axis.Y: v[j]=center.X+cos; v[j+1]=center.Y;     v[j+2]=center.Z+sin; break;
                    case Axis.Z: v[j]=center.X+cos; v[j+1]=center.Y+sin; v[j+2]=center.Z;     break;
                }
            }
            return v;
        }

        private static float[] BuildLine(Vector3 a, Vector3 b)
            => new[] { a.X, a.Y, a.Z, b.X, b.Y, b.Z };

        /// <summary>
        /// Filled cone (GL_TRIANGLES) pointing from <paramref name="tip"/> backward
        /// along <paramref name="backDir"/> for <paramref name="headLen"/> units,
        /// with base radius <paramref name="baseR"/>.
        /// </summary>
        private static float[] BuildCone(Vector3 tip, Vector3 backDir, float headLen, float baseR, Axis axis)
        {
            int     n    = ConeSegs;
            float[] verts = new float[n * 9]; // n triangles × 3 verts × 3 floats
            Vector3 bc   = tip + backDir * headLen; // base centre

            for (int i = 0; i < n; i++)
            {
                float a0 = 2f * MathF.PI * i       / n;
                float a1 = 2f * MathF.PI * (i + 1) / n;
                Vector3 p0, p1;
                switch (axis)
                {
                    case Axis.X:
                        p0 = bc + new Vector3(0, baseR * MathF.Cos(a0), baseR * MathF.Sin(a0));
                        p1 = bc + new Vector3(0, baseR * MathF.Cos(a1), baseR * MathF.Sin(a1));
                        break;
                    case Axis.Y:
                        p0 = bc + new Vector3(baseR * MathF.Cos(a0), 0, baseR * MathF.Sin(a0));
                        p1 = bc + new Vector3(baseR * MathF.Cos(a1), 0, baseR * MathF.Sin(a1));
                        break;
                    default: // Z
                        p0 = bc + new Vector3(baseR * MathF.Cos(a0), baseR * MathF.Sin(a0), 0);
                        p1 = bc + new Vector3(baseR * MathF.Cos(a1), baseR * MathF.Sin(a1), 0);
                        break;
                }
                int idx = i * 9;
                verts[idx]   = tip.X; verts[idx+1] = tip.Y; verts[idx+2] = tip.Z;
                verts[idx+3] = p0.X;  verts[idx+4] = p0.Y;  verts[idx+5] = p0.Z;
                verts[idx+6] = p1.X;  verts[idx+7] = p1.Y;  verts[idx+8] = p1.Z;
            }
            return verts;
        }

        // ── Upload / draw helpers ──────────────────────────────────────────

        private static (int vao, int vbo) Upload(float[] verts)
        {
            int vao = GL.GenVertexArray();
            int vbo = GL.GenBuffer();
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts, BufferUsageHint.DynamicDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.BindVertexArray(0);
            return (vao, vbo);
        }

        private static void DrawVAO(int vao, int count, PrimitiveType prim, Vector4 color, Shader shader)
        {
            shader.SetVector4("uColor", color);
            GL.BindVertexArray(vao);
            GL.DrawArrays(prim, 0, count);
        }

        // ── Cleanup ────────────────────────────────────────────────────────

        private void DeleteBuffers()
        {
            void Del(int vao, int vbo) { if (vao != 0) GL.DeleteVertexArray(vao); if (vbo != 0) GL.DeleteBuffer(vbo); }
            Del(_vaoRX, _vboRX); Del(_vaoRY, _vboRY); Del(_vaoRZ, _vboRZ);
            Del(_vaoHX, _vboHX); Del(_vaoHY, _vboHY); Del(_vaoHZ, _vboHZ);
            Del(_vaoAX, _vboAX); Del(_vaoAY, _vboAY); Del(_vaoAZ, _vboAZ);
            Del(_vaoCX, _vboCX); Del(_vaoCY, _vboCY); Del(_vaoCZ, _vboCZ);
            _vaoRX=_vboRX=_vaoRY=_vboRY=_vaoRZ=_vboRZ=0;
            _vaoHX=_vboHX=_vaoHY=_vboHY=_vaoHZ=_vboHZ=0;
            _vaoAX=_vboAX=_vaoAY=_vboAY=_vaoAZ=_vboAZ=0;
            _vaoCX=_vboCX=_vaoCY=_vboCY=_vaoCZ=_vboCZ=0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            DeleteBuffers();
            _disposed = true;
        }
    }
}
