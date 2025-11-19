using g4;

namespace UnBox3D.Models
{
    /// <summary>
    /// DMesh3 subclass designed to support texture mapping. It extends the base 
    /// DMesh3 functionality by adding UV coordinates and a path to the texture 
    /// image to the DMesh3 attributes.
    /// </summary>
    public class DMesh3WithTextures : DMesh3
    {
        public Vector2f[] UVsArray { get; private set; }
        public string DiffuseTexturePath { get; set; }
        public DMesh3WithTextures(string diffuseTexturePath = "") : base()
        {
            UVsArray = Array.Empty<Vector2f>(); // initialize empty array for safety
            DiffuseTexturePath = diffuseTexturePath;
        }

        // Resize the UVs array to match the number of vertices
        public void AllocateUVs()
        {
            UVsArray = new Vector2f[VertexCount];
        }

        // Set UV coordinate for a specific vertex
        public void SetUV(int i, Vector2d uv)
        {
            // safety: ensure array is large enough
            if (UVsArray.Length != VertexCount)
                AllocateUVs();

            UVsArray[i] = new Vector2f((float)uv.x, (float)uv.y);
        }
    }
}
