using System.Diagnostics;
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


        private readonly Stack<MeshReplaceMemento> replacedMeshes;

        public ReplaceCommand(IGLControlHost glControlHost, ISceneManager sceneManager, IRayCaster rayCaster, ICamera camera)
        {
            _glControlHost = glControlHost;
            _rayCaster = rayCaster;
            _camera = camera;
            _sceneManager = sceneManager;

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