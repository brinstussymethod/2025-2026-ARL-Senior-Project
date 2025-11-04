using UnBox3D.Controls.States;
using UnBox3D.Controls;
using UnBox3D.Models;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;

namespace UnBox3D.Commands
{
    public class SetReplaceStateCommand : ICommand
    {
        private MouseController _mouseController;
        private IState _defaultState;
        private ISceneManager _sceneManager;
        private IGLControlHost _gLControlHost;
        private ICamera _camera;
        private IRayCaster _rayCaster;
        private ICommandHistory _moveCommandHistory;

        public SetReplaceStateCommand(IGLControlHost glControlHost, MouseController mouseController, ISceneManager sceneManager, IRayCaster rayCaster, ICamera camera, ICommandHistory commandHistory)
        {
            _mouseController = mouseController;
            _sceneManager = sceneManager;
            _rayCaster = rayCaster;
            _camera = camera;
            _moveCommandHistory = commandHistory;
            _gLControlHost = glControlHost;
        }

        public void Execute()
        {
            var replaceState = new ReplaceState(_gLControlHost, _sceneManager, _camera, new RayCaster(_gLControlHost, _camera), _moveCommandHistory);
            _mouseController.SetState(replaceState);
        }

        public void Undo()
        {
            _defaultState = new DefaultState(_sceneManager, _gLControlHost, _camera, _rayCaster);
            _mouseController.SetState(_defaultState);
        }
    }
}
