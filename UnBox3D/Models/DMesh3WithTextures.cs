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
        public Vector2f[]? UVsArray { get; private set; }
        public string DiffuseTexturePath { get; set; }

        // Default constructor
        public DMesh3WithTextures(string diffuseTexturePath = "") : base()
        {
            UVsArray = Array.Empty<Vector2f>(); // initialize empty array for safety
            DiffuseTexturePath = diffuseTexturePath;
        }

        // Copy constructor
        public DMesh3WithTextures(DMesh3WithTextures copy) : base(copy)
        {
            // Deep copy UVs
            if (copy.UVsArray != null)
            {
                UVsArray = new Vector2f[copy.UVsArray.Length];
                Array.Copy(copy.UVsArray, UVsArray, copy.UVsArray.Length);
            }
            else
            {
                UVsArray = null;
            }

            DiffuseTexturePath = copy.DiffuseTexturePath;
        }

        // Resize the UVs array to match the number of vertices
        public void AllocateUVs()
        {
            UVsArray = new Vector2f[VertexCount];
        }

        // Set UV coordinate for a specific vertex
        public void SetUV(int i, Vector2d uv)
        {
            if (UVsArray != null)
            {
            // safety: ensure array is large enough
            if (UVsArray.Length != VertexCount)
                AllocateUVs();

            UVsArray[i] = new Vector2f((float)uv.x, (float)uv.y);
            }
        }
    }
}
