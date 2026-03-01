using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using UnBox3D.Utils;
using System;

namespace UnBox3D.Rendering
{
    public class Gizmo
    {
        private Vector3 center = new Vector3();
        private readonly float radius;
        private readonly int numSegments;
        private bool isVisible { get; set; }

        public Gizmo(float radius, int numSegments)
        {
            this.radius = radius;
            this.numSegments = numSegments;
        }

        public Vector3 GetCenter() { return center; }

        public void SetCenter(Vector3 center) 
        {
            this.center.X = center.X;
            this.center.Y = center.Z;
            this.center.Z = center.Y;
        }

        private void RenderCircle(Vector3 axis, float lineWidth)
        {
            // Set the line width to make the gizmos thicker
            GL.LineWidth(lineWidth);

            GL.Begin(PrimitiveType.LineLoop);
            for (int i = 0; i < numSegments; i++)
            {
                float angle = 2.0f * (float)Math.PI * i / numSegments;
                float x = radius * (float)Math.Cos(angle);
                float y = radius * (float)Math.Sin(angle);

                // Rotate the circle based on the axis (X, Y, or Z)
                if (axis == Vector3.UnitX)
                {
                    GL.Vertex3(center.X, center.Y + x, center.Z + y); // YZ plane
                }
                else if (axis == Vector3.UnitY)
                {
                    GL.Vertex3(center.X + x, center.Y, center.Z + y); // XZ plane
                }
                else if (axis == Vector3.UnitZ)
                {
                    GL.Vertex3(center.X + x, center.Y + y, center.Z); // XY plane
                }
            }
            GL.End();
            // Set the line width back to its default
            GL.LineWidth(1.0f);
        }


        public bool RayIntersectsGizmoCircle(Vector3 rayOrigin, Vector3 rayDirection, Vector3 axis)
        {
            // Find the plane that the gizmo circle lies in
            Vector3 gizmoCenter = center;

            // Compute the normal to the plane (same as the axis for the circle)
            Vector3 planeNormal = axis;

            // Calculate intersection point between ray and plane of the gizmo's circle
            float d = Vector3.Dot(gizmoCenter - rayOrigin, planeNormal) / Vector3.Dot(rayDirection, planeNormal);

            // If d < 0, no intersection (ray points away from the plane)
            if (d < 0) return false;

            // Compute the intersection point on the plane
            Vector3 intersectionPoint = rayOrigin + rayDirection * d;

            // Calculate the distance from the intersection point to the gizmo's center
            float distanceToCenter = (intersectionPoint - gizmoCenter).Length;

            // If distance to center is less than the radius of the gizmo's circle, ray intersects
            return distanceToCenter <= radius;
        }


        // Method to draw all the circles (X, Y, Z)
        public void RenderGizmos()
        {
            // X-axis (red circle)
            GL.Color3(Colors.Red);
            RenderCircle(Vector3.UnitX, 3.0f);

            // Y-axis (green circle)
            GL.Color3(Colors.Green);
            RenderCircle(Vector3.UnitY, 3.0f);

            // Z-axis (blue circle)
            GL.Color3(Colors.Blue);
            RenderCircle(Vector3.UnitZ, 3.0f);
        }

    }
}
