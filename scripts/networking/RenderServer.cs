using System;
using System.Threading.Tasks;
using Grpc.Core;
using Godot;
using Capstone.Render;
using System.Collections.Concurrent;

namespace Renderer;

public class RenderServer : RenderService.RenderServiceBase
{
	private readonly ConcurrentQueue<RenderEvent> _eventQueue;

	public RenderServer(ConcurrentQueue<RenderEvent> eventQueue)
	{
		_eventQueue = eventQueue;
	}

	public override async Task StreamEvent(
		IAsyncStreamReader<RenderEvent> requestStream,
		IServerStreamWriter<RenderReply> responseStream,
		ServerCallContext context)
	{
		GD.Print("RoomEngine connected to RenderServer.");

		try
		{
			while (await requestStream.MoveNext())
			{
				RenderEvent renderEvent = requestStream.Current;

				GD.Print(
					$"Received render event: " +
					$"Type={renderEvent.Type}, " +
					$"Entity={renderEvent.EntityId}"
				);

				// Queue a copy for the Godot main thread.
				_eventQueue.Enqueue(new RenderEvent(renderEvent));

				await responseStream.WriteAsync(new RenderReply
				{
					EntityId = renderEvent.EntityId,
					Message = "Render event queued"
				});
			}
		}
		catch (RpcException ex)
		{
			GD.PrintErr($"Render gRPC error: {ex.Status}");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Render server error: {ex}");
		}

		GD.Print("RoomEngine disconnected from RenderServer.");
	}
}
