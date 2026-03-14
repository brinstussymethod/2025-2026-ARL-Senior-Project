using System.Collections.ObjectModel;

namespace UnBox3D.Rendering.Rulers
{
    public class RulerManager : IRulerManager
    {
        private readonly ObservableCollection<RulerObject> _rulers = new();

        public RulerUnit GlobalUnit { get; set; } = RulerUnit.M;

        public ObservableCollection<RulerObject> GetRulers() => _rulers;

        public void AddRuler(RulerObject ruler)
        {
            if (ruler == null) throw new ArgumentNullException(nameof(ruler));
            _rulers.Add(ruler);
        }

        public void RemoveRuler(RulerObject ruler)
        {
            if (ruler != null) _rulers.Remove(ruler);
        }

        public void ClearRulers() => _rulers.Clear();

        public RulerObject? GetById(Guid id) =>
            _rulers.FirstOrDefault(r => r.Id == id);
    }
}
