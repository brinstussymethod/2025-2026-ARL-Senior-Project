using Assimp;
using g4;
using System.Diagnostics;
using System.IO;
using UnBox3D.Rendering;
using UnBox3D.Utils;

namespace UnBox3D.Models
{
    public class ModelExporter
    {
        private readonly ISettingsManager _settingsManager;

        public ModelExporter(ISettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
        }

        public string? ExportToObj(List<IAppMesh> meshes)
        {
            try
            {
                string? exportDir = _settingsManager.GetSetting<string>(
                 new AppSettings().GetKey(),
                 AppSettings.ExportDirectory
                );

                if (string.IsNullOrWhiteSpace(exportDir) || !Directory.Exists(exportDir))
                {
                    exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Export");
                    Directory.CreateDirectory(exportDir);
                }

                var scene = new Scene();
                scene.RootNode = new Node("Root");
                scene.Materials.Add(new Material());

                foreach (var appMesh in meshes)
                {
                    var dmesh = appMesh.GetG4Mesh();
                    var assimpMesh = new Mesh(PrimitiveType.Triangle);

                    foreach (int vid in dmesh.VertexIndices())
                    {
                        var v = dmesh.GetVertex(vid);
                        assimpMesh.Vertices.Add(new Vector3D((float)v.x, (float)v.y, (float)v.z));
                    }

                    foreach (int tid in dmesh.TriangleIndices())
                    {
                        var tri = dmesh.GetTriangle(tid);
                        assimpMesh.Faces.Add(new Face(new[] { tri.a, tri.b, tri.c }));
                    }

                    if (assimpMesh.Vertices.Count > 0 && assimpMesh.Faces.Count > 0)
                    {
                        assimpMesh.MaterialIndex = 0;
                        scene.Meshes.Add(assimpMesh);
                        scene.RootNode.MeshIndices.Add(scene.MeshCount - 1);
                    }
                }

                var context = new AssimpContext();
                var fileName = $"export_{DateTime.Now:yyyyMMdd_HHmmss}.obj";
                string outputPath = Path.Combine(exportDir, fileName);
                var success = context.ExportFile(scene, outputPath, "obj");

                return outputPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModelExporter] Export failed: {ex.Message}");
                return null;
            }
        }

        public string? ExportToObj(List<IAppMesh> meshes, string fileName)
        {
            try
            {
                // Use default folder from settings or fallback
                string? exportDir = _settingsManager.GetSetting<string>(
                    new AppSettings().GetKey(),
                    AppSettings.ExportDirectory
                );

                if (string.IsNullOrWhiteSpace(exportDir) || !Directory.Exists(exportDir))
                {
                    exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Export");
                    Directory.CreateDirectory(exportDir);
                }

                string outputPath = Path.Combine(exportDir, fileName);

                var scene = new Scene
                {
                    RootNode = new Node("Root")
                };
                scene.Materials.Add(new Material());

                foreach (var appMesh in meshes)
                {
                    var dmesh = appMesh.GetG4Mesh();
                    var assimpMesh = new Mesh(PrimitiveType.Triangle)
                    {
                        MaterialIndex = 0
                    };

                    foreach (int vid in dmesh.VertexIndices())
                    {
                        var v = dmesh.GetVertex(vid);
                        assimpMesh.Vertices.Add(new Vector3D((float)v.x, (float)v.y, (float)v.z));
                    }

                    foreach (int tid in dmesh.TriangleIndices())
                    {
                        var tri = dmesh.GetTriangle(tid);
                        assimpMesh.Faces.Add(new Face(new[] { tri.a, tri.b, tri.c }));
                    }

                    if (assimpMesh.Vertices.Count > 0 && assimpMesh.Faces.Count > 0)
                    {
                        scene.Meshes.Add(assimpMesh);
                        scene.RootNode.MeshIndices.Add(scene.MeshCount - 1);
                    }
                }

                using var context = new AssimpContext();
                context.ExportFile(scene, outputPath, "obj");

                return outputPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModelExporter] Export failed: {ex.Message}");
                return null;
            }
        }
        public string? ExportToObjAbsolutePath(List<IAppMesh> meshes, string absolutePath)
        {
            try
            {
                var scene = new Scene
                {
                    RootNode = new Node("Root")
                };

                scene.Materials.Add(new Material());

                foreach (var appMesh in meshes)
                {
                    var dmesh = appMesh.GetG4Mesh();
                    var assimpMesh = new Mesh(PrimitiveType.Triangle) { MaterialIndex = 0 };

                    foreach (int vid in dmesh.VertexIndices())
                    {
                        var v = dmesh.GetVertex(vid);
                        assimpMesh.Vertices.Add(new Vector3D((float)v.x, (float)v.y, (float)v.z));
                    }

                    foreach (int tid in dmesh.TriangleIndices())
                    {
                        var tri = dmesh.GetTriangle(tid);
                        assimpMesh.Faces.Add(new Face(new[] { tri.a, tri.b, tri.c }));
                    }

                    if (assimpMesh.Vertices.Count > 0 && assimpMesh.Faces.Count > 0)
                    {
                        scene.Meshes.Add(assimpMesh);
                        scene.RootNode.MeshIndices.Add(scene.MeshCount - 1);
                    }
                }

                using var context = new AssimpContext();
                context.ExportFile(scene, absolutePath, "obj");

                return absolutePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModelExporter] Export failed: {ex.Message}");
                return null;
            }
        }

    }
}
