namespace UnBox3D.Rendering
{
    /// <summary>
    /// Identifies which individual gizmo element the mouse is currently hovering over.
    /// Shared between the Rendering layer (GizmoRenderer) and the Controls.States layer
    /// to avoid a circular namespace dependency.
    /// </summary>
    public enum GizmoHoverElement
    {
        None,
        MoveX, MoveY, MoveZ,       // arrow shaft + cone for each axis
        RotateX, RotateY, RotateZ  // rotation ring (+ handle disc) for each axis
    }
}
