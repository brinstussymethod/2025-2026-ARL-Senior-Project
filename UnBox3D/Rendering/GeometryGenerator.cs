using OpenTK.Mathematics;
using Assimp;
using g4;
using UnBox3D.Utils;

namespace UnBox3D.Rendering
{
    public class GeometryGenerator
    {
        public static AppMesh CreateBox(Vector3 center, float width, float height, float depth, string name = "Box")
        {
            Mesh assimpMesh = new Mesh(name, PrimitiveType.Triangle);
            DMesh3 g4Mesh = new DMesh3();

            // Define box vertices
            Vector3[] vertices =
            [
                new Vector3(center.X - width / 2, center.Y - height / 2, center.Z - depth / 2), // 0
                new Vector3(center.X + width / 2, center.Y - height / 2, center.Z - depth / 2), // 1
                new Vector3(center.X + width / 2, center.Y + height / 2, center.Z - depth / 2), // 2
                new Vector3(center.X - width / 2, center.Y + height / 2, center.Z - depth / 2), // 3

                new Vector3(center.X - width / 2, center.Y - height / 2, center.Z + depth / 2), // 4
                new Vector3(center.X + width / 2, center.Y - height / 2, center.Z + depth / 2), // 5
                new Vector3(center.X + width / 2, center.Y + height / 2, center.Z + depth / 2), // 6
                new Vector3(center.X - width / 2, center.Y + height / 2, center.Z + depth / 2)  // 7
            ];

            // Add vertices to Assimp Mesh
            foreach (var v in vertices)
            {
                assimpMesh.Vertices.Add(new Assimp.Vector3D(v.X, v.Y, v.Z));

                // Create Equivalent DMesh3
                // Add vertices to g4Mesh
                g4Mesh.AppendVertex(new g4.Vector3d(v.X, v.Y, v.Z));
            }

            // Define box faces (two triangles per face)
            int[][] faces =
            [
                [0, 1, 2], [0, 2, 3], // Front
                [5, 4, 7], [5, 7, 6], // Back
                [4, 0, 3], [4, 3, 7], // Left
                [1, 5, 6], [1, 6, 2], // Right
                [3, 2, 6], [3, 6, 7], // Top
                [4, 5, 1], [4, 1, 0]  // Bottom
            ];

            // Initialize empty normals for each vertex
            Vector3[] vertexNormals = new Vector3[vertices.Length];

            foreach (var face in faces)
            {
                Vector3 v0 = vertices[face[0]];
                Vector3 v1 = vertices[face[1]];
                Vector3 v2 = vertices[face[2]];

                Vector3 edge1 = v1 - v0;
                Vector3 edge2 = v2 - v0;

                Vector3 faceNormal = Vector3.Cross(edge1, edge2).Normalized();

                // Accumulate normals at each vertex of the face
                vertexNormals[face[0]] += faceNormal;
                vertexNormals[face[1]] += faceNormal;
                vertexNormals[face[2]] += faceNormal;
                assimpMesh.Faces.Add(new Face(face));

                // Add faces to g4Mesh
                g4Mesh.AppendTriangle(face[0], face[1], face[2]);
            }

            //Normalize accumulated vertex normals and add to Assimp
            foreach (var normal in vertexNormals)
            {
                var n = normal.Normalized();
                assimpMesh.Normals.Add(new Assimp.Vector3D(n.X, n.Y, n.Z));
            }

            AppMesh appMesh = new AppMesh(g4Mesh, assimpMesh);
            appMesh.SetColor(Colors.Red);

            return appMesh;
        }

        public static AppMesh CreateCylinder(Vector3 center, float radius, float height, int segments)
        {
            Mesh assimpMesh = new Mesh("Cylinder", PrimitiveType.Triangle);
            DMesh3 g4Mesh = new DMesh3();
            float halfHeight = height * 0.5f;
            List<Vector3> vertices = new List<Vector3>();

            // Generate vertices for the cylinder
            for (int i = 0; i < segments; i++)
            {
                float angle = i * ((float)Math.PI * 2 / segments);
                float x = (float)Math.Cos(angle) * radius;
                float z = (float)Math.Sin(angle) * radius;

                // Bottom ring
                vertices.Add(new Vector3(center.X + x, center.Y - halfHeight, center.Z + z));
                // Top ring
                vertices.Add(new Vector3(center.X + x, center.Y + halfHeight, center.Z + z));
            }

            // Bottom and top center points
            Vector3 bottomCenter = new Vector3(center.X, center.Y - halfHeight, center.Z);
            Vector3 topCenter = new Vector3(center.X, center.Y + halfHeight, center.Z);
            vertices.Add(bottomCenter);
            vertices.Add(topCenter);

            // Add vertices to Assimp mesh
            foreach (var v in vertices)
            {
                assimpMesh.Vertices.Add(new Assimp.Vector3D(v.X, v.Y, v.Z));
            }

            // Add vertices to g4 mesh
            foreach (var v in vertices)
            {
                g4Mesh.AppendVertex(new g4.Vector3d(v.X, v.Y, v.Z));
            }

            // Define cylinder faces (triangles)
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;

                // Side faces (two triangles per segment)
                int bottomCurrent = i * 2;
                int topCurrent = bottomCurrent + 1;
                int bottomNext = next * 2;
                int topNext = bottomNext + 1;

                // Side triangles
                assimpMesh.Faces.Add(new Face([bottomCurrent, topCurrent, bottomNext]));
                assimpMesh.Faces.Add(new Face([bottomNext, topCurrent, topNext]));

                g4Mesh.AppendTriangle(bottomCurrent, topCurrent, bottomNext);
                g4Mesh.AppendTriangle(bottomNext, topCurrent, topNext);

                // Bottom cap
                assimpMesh.Faces.Add(new Face([segments * 2, bottomNext, bottomCurrent]));
                g4Mesh.AppendTriangle(segments * 2, bottomNext, bottomCurrent);

                // Top cap
                assimpMesh.Faces.Add(new Face([segments * 2 + 1, topCurrent, topNext]));
                g4Mesh.AppendTriangle(segments * 2 + 1, topCurrent, topNext);
            }
            return new AppMesh(g4Mesh, assimpMesh);
        }

        public static AppMesh CreateRotatedCylinder(Vector3 center, float radius, float height, int segments, Vector3 direction)
        {
            Mesh assimpMesh = new Mesh("GeneratedCylinder", PrimitiveType.Triangle);
            DMesh3 g4Mesh = new DMesh3();
            float halfHeight = height * 0.5f;
            List<Vector3> vertices = new List<Vector3>();

            // Generate vertices for the cylinder
            for (int i = 0; i < segments; i++)
            {
                float angle = i * ((float)Math.PI * 2 / segments);
                float x = (float)Math.Cos(angle) * radius;
                float z = (float)Math.Sin(angle) * radius;

                // Bottom ring
                vertices.Add(new Vector3(center.X + x, center.Y - halfHeight, center.Z + z));
                // Top ring
                vertices.Add(new Vector3(center.X + x, center.Y + halfHeight, center.Z + z));
            }

            // Bottom and top center points
            Vector3 bottomCenter = new Vector3(center.X, center.Y - halfHeight, center.Z);
            Vector3 topCenter = new Vector3(center.X, center.Y + halfHeight, center.Z);
            vertices.Add(bottomCenter);
            vertices.Add(topCenter);

            // Add vertices to Assimp mesh
            //foreach (var v in vertices)
            //{
            //    assimpMesh.Vertices.Add(new Assimp.Vector3D(v.X, v.Y, v.Z));
            //}

            // Add vertices to g4 mesh
            foreach (var v in vertices)
            {
                g4Mesh.AppendVertex(new g4.Vector3d(v.X, v.Y, v.Z));
            }


            Vector3 up = new Vector3(0, 1, 0);
            if (direction != up)
            {
                direction = Vector3.Normalize(direction);
                OpenTK.Mathematics.Quaternion rotation = OpenTK.Mathematics.Quaternion.FromAxisAngle(Vector3.Cross(up, direction), (float)Math.Acos(Vector3.Dot(up, direction)));
                //Matrix4 rotationMatrix = Matrix4.CreateFromQuaternion(rotation);

                // ROTATE MESH
                for (int i = 0; i < g4Mesh.VertexCount; i++)
                {
                    // Retrieve the vertex
                    g4.Vector3d vertex = g4Mesh.GetVertex(i);

                    // Convert the vertex to a Vector4 for transformation (homogeneous coordinates)
                    Vector4 transformedVertex = new Vector4((float)vertex.x, (float)vertex.y, (float)vertex.z, 1.0f);

                    transformedVertex = Vector4.Transform(transformedVertex, rotation);

                    // Update the vertex with the transformed coordinates
                    g4Mesh.SetVertex(i, new g4.Vector3d(transformedVertex.X, transformedVertex.Y, transformedVertex.Z));
                }
            }

            Vector3 newCenter = Vector3.Zero;

            if (g4Mesh.VertexCount > 0)
            {
                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

                for (int i = 0; i < g4Mesh.VertexCount; i++)
                {
                    g4.Vector3d vertex = g4Mesh.GetVertex(i);
                    Vector3 vertexVec = new Vector3((float)vertex.x, (float)vertex.y, (float)vertex.z);

                    // Update min and max
                    min = Vector3.ComponentMin(min, vertexVec);
                    max = Vector3.ComponentMax(max, vertexVec);
                }

                // Calculate the center
                newCenter = (min + max) * 0.5f;
            }

            // Adjust the cylinder back to its original center
            Vector3 translationOffset = center - newCenter;
            for (int i = 0; i < g4Mesh.VertexCount; i++)
            {
                g4.Vector3d vertex = g4Mesh.GetVertex(i);
                vertex.x += translationOffset.X;
                vertex.y += translationOffset.Y;
                vertex.z += translationOffset.Z;
                g4Mesh.SetVertex(i, vertex);
                assimpMesh.Vertices.Add(new Assimp.Vector3D((float)vertex.x, (float)vertex.y, (float)vertex.z));
            }

            // Define cylinder faces (triangles)
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;

                // Side faces (two triangles per segment)
                int bottomCurrent = i * 2;
                int topCurrent = bottomCurrent + 1;
                int bottomNext = next * 2;
                int topNext = bottomNext + 1;

                // Side triangles
                assimpMesh.Faces.Add(new Face([bottomCurrent, topCurrent, bottomNext]));
                assimpMesh.Faces.Add(new Face([bottomNext, topCurrent, topNext]));

                g4Mesh.AppendTriangle(bottomCurrent, topCurrent, bottomNext);
                g4Mesh.AppendTriangle(bottomNext, topCurrent, topNext);

                // Bottom cap
                assimpMesh.Faces.Add(new Face([segments * 2, bottomNext, bottomCurrent]));
                g4Mesh.AppendTriangle(segments * 2, bottomNext, bottomCurrent);

                // Top cap
                assimpMesh.Faces.Add(new Face([segments * 2 + 1, topCurrent, topNext]));
                g4Mesh.AppendTriangle(segments * 2 + 1, topCurrent, topNext);
            }

            AppMesh appMesh = new AppMesh(g4Mesh, assimpMesh);
            appMesh.SetColor(Colors.Red);

            return appMesh;
        }

        /// <summary>
        /// Creates a triangular prism (wedge) shape. The cross-section is a right triangle
        /// with the right angle at the bottom-front. Useful for windshield-like shapes.
        /// The wedge extends along the Z axis (depth).
        /// </summary>
        /// <param name="center">Center position of the wedge</param>
        /// <param name="width">Width along X axis (base of triangle)</param>
        /// <param name="height">Height along Y axis (vertical edge of triangle)</param>
        /// <param name="depth">Depth along Z axis (extrusion length)</param>
        /// <param name="name">Optional mesh name</param>
        public static AppMesh CreateWedge(Vector3 center, float width, float height, float depth, string name = "Wedge")
        {
            Mesh assimpMesh = new Mesh(name, PrimitiveType.Triangle);
            DMesh3 g4Mesh = new DMesh3();

            float hw = width / 2, hh = height / 2, hd = depth / 2;

            // Cross-section is a right triangle:
            //   bottom-left (0), bottom-right (1), top-left (2)
            // Front face (Z = -hd)
            // Back face  (Z = +hd)
            Vector3[] vertices =
            [
                new Vector3(center.X - hw, center.Y - hh, center.Z - hd), // 0 front bottom-left
                new Vector3(center.X + hw, center.Y - hh, center.Z - hd), // 1 front bottom-right
                new Vector3(center.X - hw, center.Y + hh, center.Z - hd), // 2 front top-left

                new Vector3(center.X - hw, center.Y - hh, center.Z + hd), // 3 back bottom-left
                new Vector3(center.X + hw, center.Y - hh, center.Z + hd), // 4 back bottom-right
                new Vector3(center.X - hw, center.Y + hh, center.Z + hd), // 5 back top-left
            ];

            foreach (var v in vertices)
            {
                assimpMesh.Vertices.Add(new Assimp.Vector3D(v.X, v.Y, v.Z));
                g4Mesh.AppendVertex(new g4.Vector3d(v.X, v.Y, v.Z));
            }

            // Faces (two triangles per quad face, one triangle for each end cap)
            int[][] faces =
            [
                [0, 1, 2],       // Front triangle
                [3, 5, 4],       // Back triangle
                [0, 3, 4], [0, 4, 1], // Bottom quad
                [0, 2, 5], [0, 5, 3], // Left quad
                [1, 4, 5], [1, 5, 2], // Hypotenuse (slope) quad
            ];

            Vector3[] vertexNormals = new Vector3[vertices.Length];

            foreach (var face in faces)
            {
                Vector3 v0 = vertices[face[0]];
                Vector3 v1 = vertices[face[1]];
                Vector3 v2 = vertices[face[2]];

                Vector3 faceNormal = Vector3.Cross(v1 - v0, v2 - v0).Normalized();

                vertexNormals[face[0]] += faceNormal;
                vertexNormals[face[1]] += faceNormal;
                vertexNormals[face[2]] += faceNormal;

                assimpMesh.Faces.Add(new Face(face));
                g4Mesh.AppendTriangle(face[0], face[1], face[2]);
            }

            foreach (var normal in vertexNormals)
            {
                var n = normal.Normalized();
                assimpMesh.Normals.Add(new Assimp.Vector3D(n.X, n.Y, n.Z));
            }

            AppMesh appMesh = new AppMesh(g4Mesh, assimpMesh);
            appMesh.SetColor(Colors.Red);
            return appMesh;
        }
    }
}
