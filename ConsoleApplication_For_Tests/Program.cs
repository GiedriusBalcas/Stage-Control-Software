using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using opentk_painter_library;
using opentk_painter_library.common;
using opentk_painter_library.render_objects;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using standa_controller_software.painter;
using static Antlr4.Runtime.Atn.SemanticContext;
class Program
{
    private static ControllerManager _controllerManager;
    private static CommandManager _commandManager;
    private static PainterManager _painterManager;
    private static RenderLayer _renderLayer;

    [STAThread]
    static void Main(string[] args)
    {
        _controllerManager = new ControllerManager();
        _commandManager = new CommandManager(_controllerManager);
        _painterManager = new PainterManager(_commandManager, _controllerManager);

        _renderLayer = _painterManager.CreateCommandLayer();

        var lineCollection = new LineObjectCollection();

        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                for (int k = 0; k < 10; k++)
                {
                    lineCollection.AddLine(new Vector3(-i*10, -j * 10, k*10- 10), new Vector3(i*10, j*10, k*10 - 10), new Vector4(0,1,0,1));
                }
            }
        }

        lineCollection.AddLine(new Vector3(0,0,0), new Vector3(10, 10, 10), new Vector4(1,0,0,1));
        //lineCollection.InitializeBuffers();
        _renderLayer.AddObjectCollection(lineCollection);

        try
        {
            using (var window = new Window([_renderLayer]))
            {
                window.Run();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}