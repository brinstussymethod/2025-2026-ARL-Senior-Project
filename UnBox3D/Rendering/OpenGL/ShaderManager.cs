namespace UnBox3D.Rendering.OpenGL
{
    public static class ShaderManager
    {
        private static Shader? _lightingShader;
        private static Shader? _lampShader;

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

        public static void Cleanup()
        {
            // Optionally dispose shaders
            //_lightingShader?.Dispose();
            //_lampShader?.Dispose();
            _lightingShader = null;
            _lampShader = null;
        }
    }
}
