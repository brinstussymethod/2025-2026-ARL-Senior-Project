using OpenTK;
using System.Collections.Generic;
using System.Windows.Forms;
using g3;
using UnBox3D.Commands;
using OpenTK.GLControl;
using UnBox3D.Controls.States;
using UnBox3D.Models;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;

namespace UnBox3D.Controls.States
{
    // InteractionHandler to handle model interaction logic
    public class ReplaceState : IState
    {
        private readonly ISceneManager _sceneManager;
        private readonly IGLControlHost _glControlHost;
        private readonly IRayCaster _rayCaster;
        private readonly ICamera _camera;
        private ICommandHistory _commandHistory;


        public ReplaceState(IGLControlHost glControlHost, ISceneManager sceneManager, ICamera camera, IRayCaster rayCaster, ICommandHistory commandHistory)
        {
            _glControlHost = glControlHost;
            _sceneManager = sceneManager;
            _camera = camera;
            _rayCaster = rayCaster;
            _commandHistory = commandHistory;
        }

        public void OnMouseDown(MouseEventArgs e)
        {
            ICommand replaceCommand = new ReplaceCommand(_glControlHost, _sceneManager, _rayCaster, _camera);
            _commandHistory.PushCommand(replaceCommand);
            replaceCommand.Execute();
        }

        public void OnMouseMove(MouseEventArgs e)
        {

        }

        public void OnMouseUp(MouseEventArgs e)
        {

        }
    }
}