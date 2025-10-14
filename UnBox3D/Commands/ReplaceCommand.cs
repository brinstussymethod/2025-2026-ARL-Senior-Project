using Assimp;
using g3;
using OpenTK.Mathematics;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;
using UnBox3D.Utils;

namespace UnBox3D.Commands
{
    public class ReplaceCommand : ICommand
    {
        private readonly ISceneManager _sceneManager;
        private readonly IRayCaster _rayCaster;
        private readonly IGLControlHost _glControlHost;
        private readonly ICamera _camera;
        private readonly string _shape;


        private readonly Stack<MeshReplaceMemento> replacedMeshes;


        public ReplaceCommand(IGLControlHost glControlHost, ISceneManager sceneManager, IRayCaster rayCaster, ICamera camera, string shape)
        {
            _glControlHost = glControlHost;
            _rayCaster = rayCaster;
            _camera = camera;
            _sceneManager = sceneManager;
            _shape = shape;

            replacedMeshes = new Stack<MeshReplaceMemento>();
        }


        public static void AppendMeshFromTriangles(DMesh3 sourceMesh, DMesh3 targetMesh, IEnumerable<int> triangleIndices)
        {
            MeshEditor editor = new MeshEditor(targetMesh);

            // Maps from original vertex index → new vertex index in the target mesh
            Dictionary<int, int> vertexMap = new Dictionary<int, int>();

            foreach (int tid in triangleIndices)
            {
                Index3i tri = sourceMesh.GetTriangle(tid);

                int[] newVerts = new int[3];

                for (int i = 0; i < 3; i++)
                {
                    int vi = (i == 0) ? tri.a : (i == 1) ? tri.b : tri.c;

                    if (!vertexMap.TryGetValue(vi, out int newIndex))
                    {
                        g3.Vector3d v = sourceMesh.GetVertex(vi);
                        newIndex = targetMesh.AppendVertex(v);
                        vertexMap[vi] = newIndex;
                    }

                    newVerts[i] = vertexMap[vi];
                }

                targetMesh.AppendTriangle(newVerts[0], newVerts[1], newVerts[2]);
            }
        }

        public void Execute()
        {
            Vector3 rayWorld = _rayCaster.GetRay();
            Vector3 rayOrigin = _camera.Position;

            // Check for intersection with the model
            if (_rayCaster.RayIntersectsMesh(_sceneManager.GetMeshes(), rayOrigin, rayWorld, out float distance, out IAppMesh clickedMesh))
            {
                // Get the index of the clicked mesh
                int meshIndex = _sceneManager.GetMeshes().IndexOf(clickedMesh);
                if (meshIndex == -1) return; // Safety check

                // Gets the center of the mesh, its dimensions, and sets the color for the replacement mesh
                Vector3 meshCenter = _sceneManager.GetMeshCenter(clickedMesh.GetG4Mesh());
                Vector3 meshDimensions = _sceneManager.GetMeshDimensions(clickedMesh.GetG4Mesh());
                Vector3 color = new Vector3(1.0f, 0.0f, 0.0f); // red color

                AppMesh replacementMesh;

                // When you want to replace with a cube (or rectangular prism)
                if (_shape == "cube")
                {
                    // Use existing dimensions of the clicked mesh as box extents
                    replacementMesh = GeometryGenerator.CreateBox(
                        meshCenter,
                        meshDimensions.X,
                        meshDimensions.Y,
                        meshDimensions.Z
                    );
                }
                else // or replace with cylinder
                {
                    bool isXAligned = (meshDimensions.X < meshDimensions.Z);
                    float radius = Math.Max(Math.Min(meshDimensions.X, meshDimensions.Z), meshDimensions.Y) / 2;
                    float height = isXAligned ? meshDimensions.X : meshDimensions.Z;
                    replacementMesh = GeometryGenerator.CreateRotatedCylinder(
                        meshCenter, radius, height, 32, Vector3.UnitY
                    );
                }

                // Whatever shape was picked to replace will then be that shape to replace with
                var replaceMesh = new MeshReplaceMemento(clickedMesh, replacementMesh, color);
                replacedMeshes.Push(replaceMesh);
                replacementMesh.SetColor(color);
                _sceneManager.ReplaceMesh(clickedMesh, replacementMesh);
                Console.WriteLine("Replacement Complete!");

            }
        }

        public void Undo()
        {
            if (replacedMeshes.Count > 0)
            {
                MeshReplaceMemento lastReplacementMemento = replacedMeshes.Pop();
                IAppMesh meshToRestore = lastReplacementMemento.OriginalMesh;
                IAppMesh replacementMesh = lastReplacementMemento.ReplacementMesh;

                Vector3 color = lastReplacementMemento.Color;

                meshToRestore.SetColor(color);

                _sceneManager.AddMesh(meshToRestore);
                _sceneManager.DeleteMesh(replacementMesh);


            }
            else
            {
                Console.WriteLine($"No meshes to restore.");
            }
        }
    }

    public class MeshReplaceMemento
    {
        public IAppMesh OriginalMesh { get; }
        public IAppMesh ReplacementMesh { get; }
        public Vector3 Color { get; }

        public MeshReplaceMemento(IAppMesh mesh, IAppMesh replacement, Vector3 color)
        {
            OriginalMesh = mesh;
            ReplacementMesh = replacement;
            Color = color;
        }


    }


}
