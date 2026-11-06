using Capstone.Render;
using Godot;
using Grpc.Core;
using System.Threading;
using System.Threading.Tasks;

namespace Renderer;

public partial class Main : Node
{
	// private Grpc.Core.Server? _grpcServer;
	// private RenderServer? _renderServer;

	public override void _Ready()
	{
		// GD.Print("Starting renderer...");

		// // This is our implementation of the generated
		// // RenderServiceBase class.
		// _renderServer = new RenderServer(this);

		// // Create the actual gRPC server.
		// _grpcServer = new Grpc.Core.Server
		// {
		// 	Services =
		// 	{
		// 		RenderService.BindService(_renderServer)
		// 	},

		// 	Ports =
		// 	{
		// 		new ServerPort(
		// 			"0.0.0.0",
		// 			5002,
		// 			ServerCredentials.Insecure
		// 		)
		// 	}
		// };

		// _grpcServer.Start();

		GD.Print("RenderServer listening on port 5002.");
	}

	// public override void _ExitTree()
	// {
	// 	if (_grpcServer != null)
	// 	{
	// 		GD.Print("Stopping RenderServer...");

	// 		_grpcServer.ShutdownAsync().GetAwaiter().GetResult();

	// 		_grpcServer = null;
	// 	}
	// }

	// public override void _Process(double delta)
	// {
	// 	_renderServer?.ProcessPendingEvents();
	// }
}
