using UnBox3D.Controls;
using UnBox3D.Controls.States;
using UnBox3D.Models;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;
using UnBox3D.Utils;

namespace UnBox3D.Commands
{
    public class SetMoveStateCommand : ICommand
    {
        private MouseController _mouseController;
        private IGLControlHost _controlHost;
        private ISceneManager _sceneManager;
        private IRayCaster _rayCaster;
        private ICamera _camera;
        private IRenderer _renderer;

        private IState? _defaultState;
        private ICommandHistory _commandHistory;

        public SetMoveStateCommand(MouseController mouseController,
                                   IGLControlHost controlHost,
                                   ISceneManager sceneManager,
                                   ICamera camera,
                                   IRayCaster rayCaster,
                                   ICommandHistory commandHistory,
                                   IRenderer renderer)
               {
                   _mouseController = mouseController;
                   _controlHost = controlHost;
                   _sceneManager = sceneManager;
                   _camera = camera;
                   _rayCaster = rayCaster;
                   _commandHistory = commandHistory;
                   _renderer = renderer;
        }

        public void Execute()
        {
            var moveState = new MoveState(_controlHost, _sceneManager, _camera, _rayCaster, _commandHistory, _renderer);
            _mouseController.SetState(moveState);
        }

        public void Undo()
        {
            _defaultState = new DefaultState(_sceneManager, _controlHost, _camera, _rayCaster);

            _mouseController.SetState(_defaultState);
        }
    }
}