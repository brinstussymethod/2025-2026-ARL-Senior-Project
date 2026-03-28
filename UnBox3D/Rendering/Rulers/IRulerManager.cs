using System.Collections.ObjectModel;

namespace UnBox3D.Rendering.Rulers
{
    public interface IRulerManager
    {
        ObservableCollection<RulerObject> GetRulers();
        void AddRuler(RulerObject ruler);
        void RemoveRuler(RulerObject ruler);
        void ClearRulers();
        RulerObject? GetById(Guid id);

        /// <summary>Global display unit applied to all rulers.</summary>
        RulerUnit GlobalUnit { get; set; }
    }
}
