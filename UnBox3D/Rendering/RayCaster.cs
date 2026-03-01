using g4;
using OpenTK.Mathematics;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using UnBox3D.Rendering.OpenGL;

// Following these tutorials:
// https://antongerdelan.net/opengl/raycasting.html
// https://www.youtube.com/watch?v=DLKN0jExRIM

namespace UnBox3D.Rendering
{
    public interface IRayCaster
    {
        Vector3 GetRay();
        bool RayIntersectsMesh(ObservableCollection<IAppMesh> scene, Vector3 rayOrigin, Vector3 rayDirection, out float intersectionDistance, out IAppMesh? clickedMesh);
        bool RayIntersectsTriangle(Vector3 rayOrigin, Vector3 rayDirection, Vector3 v0, Vector3 v1, Vector3 v2, out float distance);
    }
    public class RayCaster : IRayCaster
    {
        private readonly ICamera _camera;
        private readonly IGLControlHost _glControlHost;

        public RayCaster(IGLControlHost glControlHost, ICamera camera)
        {
            _glControlHost = glControlHost;
            _camera = camera;
        }

        public Vector3 GetRay()
        {
            Vector2 mousePosition = GetMousePosition();
            Vector3 rayNDC = ConvertToNDC(mousePosition);
            Vector4 rayClip = HomogeneousClipCoordinates(rayNDC);
            Vector4 rayEye = ToEyeCoords(rayClip);
            Vector3 rayWorld = ToWorldCoords(rayEye);
            return rayWorld;
        }

        // Step 0:
        // Get the current mouse position in window coordinates; top-left is (0, 0)
        public Vector2 GetMousePosition()
        {
            var screen = Control.MousePosition;

            // You need the actual WinForms control here (GLControl).
            var control = (Control)_glControlHost;
            var client = control.PointToClient(screen);

            return new Vector2(client.X, client.Y);
        }

        // Step 1:
        // Convert mouse position to Normalized Device Coordinates (NDC)
        public Vector3 ConvertToNDC(Vector2 mousePosition)
        {
            float x = (2.0f * mousePosition.X) / _glControlHost.GetWidth() - 1.0f;
            float y = 1.0f - (2.0f * mousePosition.Y) / _glControlHost.GetHeight();
            return new Vector3(x, y, -1.0f);
        }

        //Step 2:
        // Homogeneous Clip Coordinates
        private Vector4 HomogeneousClipCoordinates(Vector3 rayNDC) 
        {
            return new Vector4(rayNDC.X, rayNDC.Y, -1.0f, 1.0f);
        }

        //Step 3:   
        // Convert to eye space (invert projection matrix)
        private Vector4 ToEyeCoords(Vector4 clipCoords)
        {
            Matrix4 projectionMatrix;
            projectionMatrix = _camera.GetProjectionMatrix();
            Matrix4 invertedProjection = Matrix4.Invert(projectionMatrix);
            Vector4 rayEye = invertedProjection * clipCoords;
            // We only care about direction in eye space
            return new Vector4(rayEye.X, rayEye.Y, -1.0f, 0.0f);
        }

        //Step 4:
        // Convert to world space (invert view matrix)
        private Vector3 ToWorldCoords(Vector4 eyeCoords)
        {
            Matrix4 viewMatrix = _camera.GetViewMatrix();
            Matrix4 invertedView = Matrix4.Invert(viewMatrix);
            Vector4 rayWorld =  eyeCoords * invertedView;
            Vector3 rayWorldDirection = new Vector3(rayWorld.X, rayWorld.Y, rayWorld.Z);
            // Normalize the direction vector
            rayWorldDirection.Normalize();
            return rayWorldDirection;
        }

        public bool RayIntersectsMesh(ObservableCollection<IAppMesh> scene, Vector3 rayOrigin, Vector3 rayDirection, out float intersectionDistance, out IAppMesh? clickedMesh)
        {
            intersectionDistance = float.MaxValue;
            clickedMesh = null;
            bool hasIntersection = false;

            foreach (var mesh in scene)
            {
                for (int i = 0; i < mesh.GetG4Mesh().TriangleCount; i++)
                {
                    Index3i triangle = mesh.GetG4Mesh().GetTriangle(i);

                    // Get the vertices of the triangle
                    g4.Vector3d v0 = mesh.GetG4Mesh().GetVertex(triangle.a);
                    g4.Vector3d v1 = mesh.GetG4Mesh().GetVertex(triangle.b);
                    g4.Vector3d v2 = mesh.GetG4Mesh().GetVertex(triangle.c);

                    // Convert g4.Vector3d to OpenTK.Vector3 for ray intersection test
                    Vector3 vertex0 = new Vector3((float)v0.x, (float)v0.z, (float)v0.y);
                    Vector3 vertex1 = new Vector3((float)v1.x, (float)v1.z, (float)v1.y);
                    Vector3 vertex2 = new Vector3((float)v2.x, (float)v2.z, (float)v2.y);

                    // Test ray-triangle intersection
                    if (RayIntersectsTriangle(rayOrigin, rayDirection, vertex0, vertex1, vertex2, out float distance))
                    {
                        hasIntersection = true;
                        if (distance < intersectionDistance)
                        {
                            intersectionDistance = distance;
                            clickedMesh = mesh;
                        }
                    }
                }
            }
            return hasIntersection;
        }

        public IAppMesh GetClickedMesh(ObservableCollection<IAppMesh> scene, Vector3 rayOrigin, Vector3 rayDirection)
        {
            if (RayIntersectsMesh(scene, rayOrigin, rayDirection, out float distance, out IAppMesh clickedMesh))
            {
                return clickedMesh;
            }
            return null;
        }

        public bool RayIntersectsTriangle(Vector3 rayOrigin, Vector3 rayDirection, Vector3 v0, Vector3 v1, Vector3 v2, out float distance)
        {
            distance = 0;
            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 h = Vector3.Cross(rayDirection, edge2);
            float a = Vector3.Dot(edge1, h);

            if (a > -float.Epsilon && a < float.Epsilon)
                return false; // Ray is parallel to the triangle

            float f = 1.0f / a;
            Vector3 s = rayOrigin - v0;
            float u = f * Vector3.Dot(s, h);

            if (u < 0.0f || u > 1.0f)
                return false;

            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(rayDirection, q);

            if (v < 0.0f || u + v > 1.0f)
                return false;

            // Calculate the distance along the ray to the intersection point
            float t = f * Vector3.Dot(edge2, q);

            if (t > float.Epsilon) // Ray intersection
            {
                distance = t;
                return true;
            }
            return false;
        }

    }
}