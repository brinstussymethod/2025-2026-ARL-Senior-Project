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
                // Get the index of the clicked mesh
                //int meshIndex = scene.IndexOf(clickedMesh);
                int meshIndex = _sceneManager.GetMeshes().IndexOf(clickedMesh);
                if (meshIndex == -1) return; // Safety check

                //MeshConnectedComponents c = new MeshConnectedComponents(clickedMesh);
                // c.FindConnectedT();

                /*
                DMeshAABBTree3 spatial = new DMeshAABBTree3(clickedMesh);
                spatial.Build();

                // Convert your ray origin and direction from OpenTK to g3 types
                g3.Vector3d origin = new g3.Vector3d(rayOrigin.X, rayOrigin.Y, rayOrigin.Z);
                g3.Vector3d direction = new g3.Vector3d(rayWorld.X, rayWorld.Y, rayWorld.Z);
                Ray3d ray = new Ray3d(origin, direction);

                // Find intersected triangle
                int triID = spatial.FindNearestHitTriangle(ray, float.MaxValue);
                if (triID == DMesh3.InvalidID) return;
                

                Dictionary<int, int> triangleToGroupMap = new Dictionary<int, int>();

                // Build map: triangle ID -> group index
                for (int groupIndex = 0; groupIndex < c.Components.Count; groupIndex++)
                {
                    foreach (int triID in c.Components[groupIndex].Indices)
                    {
                        triangleToGroupMap[triID] = groupIndex;
                    }
                }

                DMeshAABBTree3 spatial = new DMeshAABBTree3(clickedMesh);
                spatial.Build(); 

                g3.Vector3d origin = new g3.Vector3d(rayOrigin.X, rayOrigin.Y, rayOrigin.Z);
                g3.Vector3d direction = new g3.Vector3d(rayWorld.X, rayWorld.Y, rayWorld.Z);
                Ray3d ray = new Ray3d(origin, direction);

                int tri = spatial.FindNearestHitTriangle(ray, float.MaxValue);
                if (tri == DMesh3.InvalidID) return;

                int clickedGroup = triangleToGroupMap[tri];
                var group = c.Components[clickedGroup];

                // Now extract that submesh
                DMesh3 newMesh = new DMesh3();
                MeshEditor editor = new MeshEditor(newMesh);

                */

                // Create replacement cylinder

                Vector3 meshCenter = _sceneManager.GetMeshCenter(clickedMesh.GetG4Mesh());
                Vector3 meshDimensions = _sceneManager.GetMeshDimensions(clickedMesh.GetG4Mesh());

                bool isXAligned = (meshDimensions.X < meshDimensions.Z);

                float radius = Math.Max(Math.Min(meshDimensions.X, meshDimensions.Z), meshDimensions.Y) / 2;
                float height = isXAligned ? meshDimensions.X : meshDimensions.Z;
                AppMesh replacementMesh = GeometryGenerator.CreateRotatedCylinder(meshCenter, radius, height, 32, Vector3.UnitY);

                
                Vector3 color = new Vector3(1.0f, 0.0f, 0.0f); // red color

                MeshReplaceMemento replaceMesh = new MeshReplaceMemento(clickedMesh, replacementMesh, color);
                replacedMeshes.Push(replaceMesh);

                replacementMesh.SetColor(color);
                _sceneManager.ReplaceMesh(clickedMesh, replacementMesh);
                Console.WriteLine($"Replacement Complete!");
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