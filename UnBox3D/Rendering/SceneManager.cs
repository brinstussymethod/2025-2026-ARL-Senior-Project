using OpenTK.Mathematics;
using g4;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using UnBox3D.Utils;
using Microsoft.Windows.Themes;

namespace UnBox3D.Rendering
{
    public interface ISceneManager
    {
        ObservableCollection<IAppMesh> GetMeshes();
        void AddMesh(IAppMesh mesh);
        void DeleteMesh(IAppMesh mesh);
        void ClearScene();
        void RemoveSmallMeshes(List<IAppMesh> originalMesh, float threshold);
        void ReplaceMesh(IAppMesh oldMesh, IAppMesh newMesh);
        Vector3 GetMeshCenter(DMesh3 mesh);
        Vector3 GetMeshDimensions(DMesh3 mesh);
        List<AppMesh> LoadBoundingBoxes();
    }

    public class SceneManager : ISceneManager
    {
        private ObservableCollection<IAppMesh> _sceneMeshes;

        public SceneManager()
        {
            _sceneMeshes = new ObservableCollection<IAppMesh>();
        }

        public ObservableCollection<IAppMesh> GetMeshes() => _sceneMeshes;

        public void AddMesh(IAppMesh mesh)
        {
            if (mesh != null)
            {
                _sceneMeshes.Add(mesh);
            }
        }

        public void DeleteMesh(IAppMesh mesh)
        {
            if (mesh == null) return;

            if (_sceneMeshes.Contains(mesh))
            {
                _sceneMeshes.Remove(mesh);
            }
        }

        public void ClearScene()
        {
            _sceneMeshes.Clear();
        }

        public void ReplaceMesh(IAppMesh oldMesh, IAppMesh newMesh)
        {
            if (_sceneMeshes.Contains(oldMesh))
            {
                int index = _sceneMeshes.IndexOf(oldMesh);
                _sceneMeshes[index] = newMesh;
            }
            else
            {
                // Fallback: add if old mesh isn't found
                _sceneMeshes.Add(newMesh);
            }
        }

        public void RemoveSmallMeshes(List<IAppMesh> originalMesh, float threshold)
        {
            if (originalMesh == null || originalMesh.Count == 0) return;

            // Rank-based filtering: sort meshes by surface area, then the slider
            // controls what percentage of meshes (by count) to remove from the bottom.
            // Slider at 0% = all visible, slider at 50% = bottom half removed.
            var meshAreas = new List<(IAppMesh mesh, double area)>();

            foreach (IAppMesh mesh in originalMesh)
            {
                var dmesh = mesh.GetG4Mesh();
                double area = 0;
                foreach (int tid in dmesh.TriangleIndices())
                {
                    area += dmesh.GetTriArea(tid);
                }
                meshAreas.Add((mesh, area));
            }

            // Sort ascending by surface area (smallest first)
            meshAreas.Sort((a, b) => a.area.CompareTo(b.area));

            // How many meshes to remove from the bottom
            int removeCount = (int)Math.Floor((threshold / 100.0) * meshAreas.Count);

            for (int i = 0; i < meshAreas.Count; i++)
            {
                var mesh = meshAreas[i].mesh;

                if (i < removeCount)
                {
                    // This mesh is in the bottom N% — remove it
                    if (_sceneMeshes.Contains(mesh))
                        DeleteMesh(mesh);
                }
                else
                {
                    // This mesh is above the cutoff — keep it
                    if (!_sceneMeshes.Contains(mesh))
                        AddMesh(mesh);
                }
            }
        }

        public Vector3 GetMeshCenter(DMesh3 mesh)
        {
            if (mesh.VertexCount > 0)
            {
                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

                for (int i = 0; i < mesh.VertexCount; i++)
                {
                    g4.Vector3d vertex = mesh.GetVertex(i);
                    Vector3 vertexVec = new Vector3((float)vertex.x, (float)vertex.y, (float)vertex.z);

                    // Update min and max
                    min = Vector3.ComponentMin(min, vertexVec);
                    max = Vector3.ComponentMax(max, vertexVec);
                }

                // Calculate the center
                return (min + max) * 0.5f;
            }

            return Vector3.Zero;
        }

        public Vector3 GetMeshDimensions(DMesh3 mesh)
        {
            if (mesh.VertexCount > 0)
            {
                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

                for (int i = 0; i < mesh.VertexCount; i++)
                {
                    g4.Vector3d vertex = mesh.GetVertex(i);
                    Vector3 vertexVec = new Vector3((float)vertex.x, (float)vertex.y, (float)vertex.z);

                    min = Vector3.ComponentMin(min, vertexVec);
                    max = Vector3.ComponentMax(max, vertexVec);
                }

                return max - min;
            }

            return Vector3.Zero;
        }

        public List<AppMesh> LoadBoundingBoxes()
        {
            var originalMeshes = _sceneMeshes.ToList();
            var boxMeshes = new List<AppMesh>();

            foreach (IAppMesh mesh in originalMeshes)
            {
                if (mesh.Name != "GeneratedCylinder")
                {
                    DMesh3 geomMesh = mesh.GetG4Mesh();
                    Vector3 meshCenter = GetMeshCenter(geomMesh);
                    Vector3 meshDimensions = GetMeshDimensions(geomMesh);

                    AppMesh boxMesh = GeometryGenerator.CreateBox(meshCenter, meshDimensions.X, meshDimensions.Y, meshDimensions.Z, mesh.Name);
                    boxMeshes.Add(boxMesh);

                    _sceneMeshes.Add(boxMesh);
                    _sceneMeshes.Remove(mesh);
                }
                else if (mesh.Name == "GeneratedCylinder")
                {
                    boxMeshes.Add((AppMesh)mesh);
                }
            }

            return boxMeshes;
        }

        

        private float GetMeshSize(DMesh3 mesh)
        {
            if (mesh.VertexCount == 0) return 0;

            Vector3 min = new(float.MaxValue);
            Vector3 max = new(float.MinValue);

            foreach (int i in mesh.VertexIndices())
            {
                var vertex = mesh.GetVertex(i);
                Vector3 vertexVec = new((float)vertex.x, (float)vertex.y, (float)vertex.z);

                min = Vector3.ComponentMin(min, vertexVec);
                max = Vector3.ComponentMax(max, vertexVec);
            }

            return (max - min).Length;
        }
    }
}
