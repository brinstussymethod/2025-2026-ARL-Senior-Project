using UnBox3D.Controls;
using UnBox3D.Controls.States;
using UnBox3D.Models;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;
using UnBox3D.Utils;

namespace UnBox3D.Commands
{
    class SetRotateStateCommand : ICommand
    {
        private readonly ISettingsManager  _settingsManager;
        private readonly IGLControlHost    _controlHost;
        private readonly ISceneManager     _sceneManager;
        private readonly ICamera           _camera;
        private readonly IRayCaster        _rayCaster;
        private readonly MouseController   _mouseController;   // FIX: was declared but never injected
        private readonly ICommandHistory   _commandHistory;
        private IState? _defaultState;

        public SetRotateStateCommand(
            ISettingsManager settingsManager,
            MouseController  mouseController,
            IGLControlHost   controlHost,
            ISceneManager    sceneManager,
            ICamera          camera,
            IRayCaster       rayCaster,
            ICommandHistory  commandHistory)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _mouseController = mouseController ?? throw new ArgumentNullException(nameof(mouseController));
            _controlHost     = controlHost     ?? throw new ArgumentNullException(nameof(controlHost));
            _sceneManager    = sceneManager    ?? throw new ArgumentNullException(nameof(sceneManager));
            _camera          = camera          ?? throw new ArgumentNullException(nameof(camera));
            _rayCaster       = rayCaster       ?? throw new ArgumentNullException(nameof(rayCaster));
            _commandHistory  = commandHistory  ?? throw new ArgumentNullException(nameof(commandHistory));
        }

        public void Execute()
        {
            var rotateState = new RotateState(_settingsManager, _sceneManager, _controlHost, _camera, _rayCaster, _commandHistory);
            _mouseController.SetState(rotateState);
        }

        public void Undo()
        {
            _defaultState = new DefaultState(_sceneManager, _controlHost, _camera, _rayCaster);
            _mouseController.SetState(_defaultState);
        }
    }
}
