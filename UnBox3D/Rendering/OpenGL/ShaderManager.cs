namespace UnBox3D.Rendering.OpenGL
{
    public static class ShaderManager
    {
        private static Shader? _lightingShader;
        private static Shader? _lampShader;
        private static Shader? _gizmoShader;

        public static Shader LightingShader
        {
            get
            {
                if (_lightingShader == null)
                {
                    _lightingShader = new Shader("Rendering/OpenGL/Shaders/shader.vert", "Rendering/OpenGL/Shaders/lighting.frag");
                }
                return _lightingShader;
            }
        }

        public static Shader LampShader
        {
            get
            {
                if (_lampShader == null)
                {
                    _lampShader = new Shader("Rendering/OpenGL/Shaders/shader.vert", "Rendering/OpenGL/Shaders/shader.frag");
                }
                return _lampShader;
            }
        }

        public static Shader GizmoShader
        {
            get
            {
                if (_gizmoShader == null)
                    _gizmoShader = new Shader("Rendering/OpenGL/Shaders/gizmo.vert", "Rendering/OpenGL/Shaders/gizmo.frag");
                return _gizmoShader;
            }
        }

        public static void Cleanup()
        {
            _lightingShader = null;
            _lampShader = null;
            _gizmoShader = null;
        }
    }
}
