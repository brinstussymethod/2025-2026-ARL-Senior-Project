using CommunityToolkit.Mvvm.ComponentModel;
using System.Configuration;
using System.Windows.Media;
using UnBox3D.Rendering;

namespace UnBox3D.Models
{
    /// <summary>
    /// Represents a lightweight summary of a mesh object used for UI display.
    /// Keeps track of the mesh name, vertex count, and a reference to the full mesh.
    /// Helps avoid performance issues by not exposing all the heavy data to the view.
    /// </summary>
    public partial class MeshSummary: ObservableObject
    {
        public string Name { get; set; }
        public int VertexCount { get; set; }
        public string Display => $"{Name} ({VertexCount} vertices)";

        public string ShapeIcon =>
            Name.Contains("(Cylinder)") ? "⧗" :
            Name.Contains("(Prism)")    ? "■" :
            Name.Contains("(Wedge)")    ? "◀" : "◆";

        public SolidColorBrush ShapeBrush => new SolidColorBrush(
            Name.Contains("(Cylinder)") ? System.Windows.Media.Color.FromRgb(0x2D, 0xD4, 0xBF) :
            Name.Contains("(Prism)")    ? System.Windows.Media.Color.FromRgb(0x81, 0x8C, 0xF8) :
            Name.Contains("(Wedge)")    ? System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B) :
                                          System.Windows.Media.Color.FromRgb(0x9A, 0x9A, 0x9A));
        public IAppMesh SourceMesh { get; set; }
        public bool IsSelected
        {
            get => SourceMesh.GetHighlighted();
            set
            {
                if (SourceMesh.GetHighlighted() == value) return;

                ((AppMesh)SourceMesh).SetHighlighted(value);

                // tell WPF: IsSelected changed
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public MeshSummary(IAppMesh source)
        {
            SourceMesh = source;
            Name = source.Name;
            VertexCount = source.VertexCount;
        }
    }
}