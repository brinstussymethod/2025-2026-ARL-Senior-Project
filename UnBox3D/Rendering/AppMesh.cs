using Assimp;
using g4;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Diagnostics;
using UnBox3D.Rendering.OpenGL;
using Quaternion = OpenTK.Mathematics.Quaternion;

namespace UnBox3D.Rendering
{
    public interface IAppMesh
    {
        #region Properties
        DMesh3 GetG4Mesh();
        Assimp.Mesh GetAssimpMesh();
        string Name { get; set; }
        int VertexCount { get; }
        Vector3 GetColor();
        bool GetHighlighted();
        float[] Vertices { get; }
        List<Vector3> GetEdges();
        Quaternion GetTransform();
        int[] GetIndices();
        #endregion

        #region Actions
        void SetColor(Vector3 color);
        void Rotate(Quaternion rotation);
        #endregion

        #region OpenGL Handles
        int GetVAO();
        int GetVBO();
        #endregion
    }

    public class AppMesh : IAppMesh, IDisposable
    {
        #region Fields
        private DMesh3 _g4Mesh;
        private Mesh _assimpMesh;
        private string _name;
        private bool _highlighted = false;
        private Vector3 _color;
        private float[] _vertices;
        private List<Vector3> _edges;
        private int _vao, _vertexBufferObject;
        private bool _disposed = false;
        private Quaternion _transform = Quaternion.Identity;
        private int _ebo;
        private int[] _indices;
        #endregion

        #region Constructor
        public AppMesh(DMesh3 g4mesh, Assimp.Mesh assimpMesh)
        {
            _g4Mesh = g4mesh;
            _assimpMesh = assimpMesh;
            _name = assimpMesh.Name;
            _edges = new List<Vector3>();

            // Populate vertices float array
            if (assimpMesh.HasNormals)
            {
                _vertices = new float[assimpMesh.VertexCount * 6];
                for (int i = 0; i < assimpMesh.VertexCount; i++)
                {
                    int index = i * 6;
                    _vertices[index] = assimpMesh.Vertices[i].X;
                    _vertices[index + 1] = assimpMesh.Vertices[i].Z;
                    _vertices[index + 2] = assimpMesh.Vertices[i].Y;
                    _vertices[index + 3] = assimpMesh.Normals[i].X;
                    _vertices[index + 4] = assimpMesh.Normals[i].Z;
                    _vertices[index + 5] = assimpMesh.Normals[i].Y;
                }
            }
            else
            {
                _vertices = new float[assimpMesh.VertexCount * 3];
                for (int i = 0; i < assimpMesh.VertexCount; i++)
                {
                    int index = i * 3;
                    _vertices[index] = assimpMesh.Vertices[i].X;
                    _vertices[index + 1] = assimpMesh.Vertices[i].Z;
                    _vertices[index + 2] = assimpMesh.Vertices[i].Y;
                }
            }

            // Populate indices
            _indices = assimpMesh.GetIndices();

            // Populate edges
            for (int i = 0; i < _g4Mesh.EdgeCount; i++)
            {
                Index2i edgeVertices = _g4Mesh.GetEdgeV(i);

                _edges.Add(new Vector3(
                    (float)_g4Mesh.GetVertex(edgeVertices.a).x,
                    (float)_g4Mesh.GetVertex(edgeVertices.a).y,
                    (float)_g4Mesh.GetVertex(edgeVertices.a).z));

                _edges.Add(new Vector3(
                    (float)_g4Mesh.GetVertex(edgeVertices.b).x,
                    (float)_g4Mesh.GetVertex(edgeVertices.b).y,
                    (float)_g4Mesh.GetVertex(edgeVertices.b).z));
            }

            SetupMesh();
        }
        #endregion

        #region OpenGL Setup
        private void SetupMesh()
        {
            // Ensure that this method is running on the thread with a valid OpenGL context.
            // Clean up previously allocated buffers (if any)
            if (_vao != 0)
            {
                GL.DeleteVertexArray(_vao);
                _vao = 0;
            }
            if (_vertexBufferObject != 0)
            {
                GL.DeleteBuffer(_vertexBufferObject);
                _vertexBufferObject = 0;
            }
            if (_ebo != 0)
            {
                GL.DeleteBuffer(_ebo);
                _ebo = 0;
            }

            Shader _lightingShader = ShaderManager.LightingShader;

            // Setup vertex buffer object (VBO)
            _vertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

            // Setup element buffer object (EBO)
            _ebo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(int), _indices, BufferUsageHint.StaticDraw);

            // Setup vertex array object (VAO)
            _vao = GL.GenVertexArray();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);

            var positionLocation = _lightingShader.GetAttribLocation("aPos");
            GL.EnableVertexAttribArray(positionLocation);

            if (_assimpMesh.HasNormals)
            {
                GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
                var normalLocation = _lightingShader.GetAttribLocation("aNormal");
                GL.EnableVertexAttribArray(normalLocation);
                GL.VertexAttribPointer(normalLocation, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            }
            else
            {
                GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            }

            // Unbind for safety
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
        }

        #endregion

        #region Properties
        public string Name
        {
            get => _name;
            set => _name = value;
        }
        public int VertexCount => _assimpMesh.VertexCount;
        public float[] Vertices => _vertices;
        public int GetVAO() => _vao;
        public int GetVBO() => _vertexBufferObject;
        #endregion

        #region Getters
        public DMesh3 GetG4Mesh() => _g4Mesh;
        public Assimp.Mesh GetAssimpMesh() => _assimpMesh;
        public Vector3 GetColor() => _color;
        public bool GetHighlighted() => _highlighted;
        public List<Vector3> GetEdges() => _edges;
        public Quaternion GetTransform() => _transform;
        public int[] GetIndices() => _indices;
        #endregion

        #region Setters
        public void SetName(string name) => _name = name;
        public void SetColor(Vector3 color) => _color = color;
        public void SetHighlighted(bool flag) => _highlighted = flag;
        #endregion

        #region Transformations
        public void Rotate(Quaternion rotation)
        {
            _transform = rotation * _transform;
            _transform.Normalize();
        }
        #endregion

        #region MeshSimplification

        public void SimplifyEdgeCollapse(double targetRatio = 0.5)
        {
            int triCountBefore = _g4Mesh.TriangleCount;
            int targetTris = (int)(triCountBefore * targetRatio);

            var reducer = new MyReducer(_g4Mesh)
            {
                PreserveBoundaryShape = false,
                ProjectionMode = Reducer.TargetProjectionMode.NoProjection,
                ENABLE_PROFILING = true,
            };

            reducer.MinEdgeLengthPublic = 0.0;

            reducer.ReduceToTriangleCount(targetTris);

            int triCountAfter = _g4Mesh.TriangleCount;
            Debug.WriteLine($"EdgeCollapse: Before = {triCountBefore}, After = {triCountAfter}");

            RefreshAssimpMesh();
            SetupMesh();
        }



        public void SimplifyDecimation(double targetEdgeLen = -1)
        {
            // Compute a default target edge length from the mesh bounds if none is provided.
            double maxDim = _g4Mesh.CachedBounds.MaxDim;
            double wantedEdgeLen = (targetEdgeLen < 0) ? maxDim * 0.01 : targetEdgeLen;

            // Create a Reducer instance for your mesh.
            Reducer reducer = new Reducer(_g4Mesh);

            reducer.PreserveBoundaryShape = false;

            // Use the ReduceToEdgeLength method to simplify the mesh based on edge length.
            reducer.ReduceToEdgeLength(wantedEdgeLen);

            // After reduction, the mesh is modified in-place.
            // _g4Mesh = reducer.mesh; // if you need to reassign explicitly

            // Refresh the Assimp mesh and OpenGL buffers so they reflect changes.
            RefreshAssimpMesh();
            SetupMesh();
        }

        public void SimplifyAdaptiveDecimation()
        {
            // Create a new DMesh3 instance from your current mesh 
            DMesh3 newMesh = new DMesh3(_g4Mesh);

            // Create a Remesher instance using the new mesh
            Remesher remesh = new Remesher(newMesh);

            // Set the target edge length based on your mesh's size.
            // For instance, using 2% of the maximum dimension:
            double maxDim = newMesh.CachedBounds.MaxDim;
            remesh.SetTargetEdgeLength(maxDim * 0.02);

            // Configure remesher parameters: enable flips and collapses for simplification.
            remesh.EnableFlips = true;
            remesh.EnableCollapses = true;
            // Optionally, you can disable splits if you prefer only to reduce complexity.
            remesh.EnableSplits = false;
            remesh.EnableSmoothing = false;

            // Execute multiple remeshing passes; the number of passes may depend on your
            // desired quality and simplification level.
            for (int i = 0; i < 10; i++)
                remesh.BasicRemeshPass();

            // Assign the new (remeshed) DMesh3 back to your app's internal mesh
            _g4Mesh = newMesh;
            RefreshAssimpMesh();
            SetupMesh();
        }

        private void RefreshAssimpMesh()
        {
            _assimpMesh.Vertices.Clear();
            _assimpMesh.Normals.Clear();
            _assimpMesh.Faces.Clear();

            // Recreate from new _g4Mesh
            // Notice we swap Y and Z if you did so when you originally populated your vertex buffer
            foreach (var vID in _g4Mesh.VertexIndices())
            {
                g4.Vector3d v = _g4Mesh.GetVertex(vID);
                _assimpMesh.Vertices.Add(new Assimp.Vector3D((float)v.x, (float)v.z, (float)v.y));
            }

            // Recreate faces
            foreach (var tID in _g4Mesh.TriangleIndices())
            {
                Index3i tri = _g4Mesh.GetTriangle(tID);
                _assimpMesh.Faces.Add(new Assimp.Face(new int[] { tri.a, tri.b, tri.c }));
            }

            // If you still need a local _indices array, build it manually
            List<int> newIndices = new List<int>();
            foreach (int tID in _g4Mesh.TriangleIndices())
            {
                Index3i tri = _g4Mesh.GetTriangle(tID);
                newIndices.Add(tri.a);
                newIndices.Add(tri.b);
                newIndices.Add(tri.c);
            }
            _indices = newIndices.ToArray();
        }


        #endregion

        #region Cleanup & Disposal
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Managed resources can be disposed here.
                _edges?.Clear();
            }

            // Clean up OpenGL resources.
            if (_vao != 0)
            {
                GL.DeleteVertexArray(_vao);
                _vao = 0;
            }
            if (_vertexBufferObject != 0)
            {
                GL.DeleteBuffer(_vertexBufferObject);
                _vertexBufferObject = 0;
            }
            if (_ebo != 0)
            {
                GL.DeleteBuffer(_ebo);
                _ebo = 0;
            }

            _disposed = true;
        }

        #endregion
    }

    public class MyReducer : Reducer
    {
        public MyReducer(DMesh3 m) : base(m)
        {
            PreserveBoundaryShape = false;
            ProjectionMode = TargetProjectionMode.NoProjection;
        }

        // Expose the protected MinEdgeLength as a public property
        public double MinEdgeLengthPublic
        {
            get => MinEdgeLength;
            set => MinEdgeLength = value;
        }

        protected override void Precompute(bool bMeshIsClosed = false)
        {
            HaveBoundary = false;
            IsBoundaryVtxCache = new bool[mesh.MaxVertexID];
        }
    }

}
