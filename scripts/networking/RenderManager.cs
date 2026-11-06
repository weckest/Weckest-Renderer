using Godot;
using Capstone.Render;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Grpc.Core;

namespace Renderer;

public partial class RenderManager : Node
{
	private Grpc.Core.Server? _grpcServer;
	private RenderServer? _renderServer;
	private bool _changingScene;
	private readonly ConcurrentQueue<RenderEvent> _eventQueue = new();

	public ConcurrentQueue<RenderEvent> EventQueue => _eventQueue;

	private readonly Dictionary<string, Node3D> _entities = new();

	public override void _Ready()
	{
		GD.Print("Starting renderer...");

		// Create the gRPC service implementation.
		_renderServer = new RenderServer(_eventQueue);

		// Create the actual gRPC server.
		_grpcServer = new Grpc.Core.Server
		{
			Services =
		{
			RenderService.BindService(_renderServer)
		},

			Ports =
		{
			new ServerPort(
				"0.0.0.0",
				5002,
				ServerCredentials.Insecure
			)
		}
		};

		_grpcServer.Start();

		GD.Print("RenderServer listening on port 5002.");
	}

	public override void _Process(double delta)
	{
		ProcessPendingEvents();
	}

	public override void _ExitTree()
	{
		if (_grpcServer != null)
		{
			GD.Print("Stopping RenderServer...");

			_grpcServer.ShutdownAsync().GetAwaiter().GetResult();

			_grpcServer = null;
		}
	}

	private void ProcessPendingEvents()
	{

		if (_changingScene) return;

		const int maxEventsPerFrame = 100;
		int processed = 0;

		while (processed < maxEventsPerFrame &&
			   _eventQueue.TryDequeue(out RenderEvent renderEvent))
		{
			try
			{
				HandleEvent(renderEvent);
			}
			catch (System.Exception ex)
			{
				GD.PrintErr(
					$"Failed to process render event " +
					$"{renderEvent.Type} for {renderEvent.EntityId}: {ex}"
				);
			}

			processed++;

			if (_changingScene) return;
		}
	}
	private void HandleEvent(RenderEvent renderEvent)
	{
		switch (renderEvent.Type)
		{
			case RenderEventType.RenderTransformMoved:
				HandleTransformMoved(renderEvent);
				break;

			case RenderEventType.RenderPlayAnimation:
				HandlePlayAnimation(renderEvent);
				break;

			case RenderEventType.RenderSpawnEntity:
				HandleSpawnEntity(renderEvent);
				break;

			case RenderEventType.RenderChangeColor:
				HandleChangeColor(renderEvent);
				break;

			case RenderEventType.RenderRemoveEntity:
				HandleRemoveEntity(renderEvent);
				break;

			case RenderEventType.RenderStartScene:
				HandleStartScene(renderEvent);
				break;

			case RenderEventType.RenderChangeHealth:
				HandleChangeHealth(renderEvent);
				break;

			case RenderEventType.RenderDisplayMessage:
				HandleDisplayMessage(renderEvent);
				break;

			case RenderEventType.RenderModuleMapping:
				HandleModuleMapping(renderEvent);
				break;

			default:
				GD.PrintErr($"Unknown render event type: {renderEvent.Type}");
				break;
		}
	}

	private void HandleTransformMoved(
		RenderEvent renderEvent)
	{
		if (renderEvent.PayloadCase != RenderEvent.PayloadOneofCase.TransformMoved)
		{
			GD.PrintErr("TransformMoved event has no payload.");
			return;
		}

		if (!_entities.TryGetValue(renderEvent.EntityId, out Node3D entity))
		{
			GD.PrintErr($"Entity '{renderEvent.EntityId}' is not registered.");
			return;
		}

		if (entity == null)
		{
			GD.PrintErr($"Entity '{renderEvent.EntityId}' is null.");
			return;
		}

		TransformMoved transform = renderEvent.TransformMoved;

		Vector3 newPosition = new(
			transform.X,
			transform.Y,
			transform.Z
		);

		entity.SetDeferred(Node3D.PropertyName.Position, newPosition);
	}

	private void HandlePlayAnimation(
		RenderEvent renderEvent)
	{
		if (!renderEvent.PayloadCase.Equals(RenderEvent.PayloadOneofCase.PlayAnimation))
		{
			GD.PrintErr(
                "PlayAnimation event has no payload."
			);

			return;
		}

		PlayAnimation animation =
			renderEvent.PlayAnimation;

		GD.Print(
			$"Play '{animation.AnimationName}' " +
			$"on {renderEvent.EntityId}"
		);

		// Actual Godot animation handling goes here.
		_entities.TryGetValue(renderEvent.EntityId, out Node3D entity);
		AnimationPlayer animationPlayer = entity.FindChildren("*", "AnimationPlayer", true, false)
		.OfType<AnimationPlayer>()
		.FirstOrDefault();
		animationPlayer?.Play(animation.AnimationName);
		animationPlayer?.Queue(animation.AnimationAfter);
	}

	private void HandleSpawnEntity(
		RenderEvent renderEvent)
	{
		if (renderEvent.PayloadCase != RenderEvent.PayloadOneofCase.SpawnEntity)
		{
			GD.PrintErr("SpawnEntity event has no payload.");
			return;
		}

		SpawnEntity spawn = renderEvent.SpawnEntity;

		string entityPath =
			$"res://Models/Spawnables/{spawn.EntityType}.tscn";

		GD.Print($"Loading entity: {entityPath}");

		PackedScene packedEntity = GD.Load<PackedScene>(entityPath);

		if (packedEntity == null)
		{
			GD.PrintErr($"Could not load entity scene: {entityPath}");
			return;
		}

		Node3D entity = packedEntity.Instantiate<Node3D>();

		if (entity == null)
		{
			GD.PrintErr($"Could not instantiate entity: {entityPath}");
			return;
		}

		Node currentScene = GetTree().CurrentScene;

		if (currentScene == null)
		{
			GD.PrintErr("Cannot spawn entity: CurrentScene is null.");
			entity.Free();
			return;
		}

		entity.Position = new Vector3(
			spawn.X,
			spawn.Y,
			spawn.Z
		);

		entity.Name = renderEvent.EntityId;

		_entities[renderEvent.EntityId] = entity;

		GetTree().CurrentScene.AddChild(entity);

		GD.Print(
			$"Spawned '{renderEvent.EntityId}' " +
			$"at ({spawn.X}, {spawn.Y}, {spawn.Z})"
		);

	}

	private void HandleChangeColor(RenderEvent renderEvent)
	{
		if (!renderEvent.PayloadCase.Equals(RenderEvent.PayloadOneofCase.ChangeColor))
		{
			GD.PrintErr(
                "ChangeColor event has no payload."
			);

			return;
		}

		ChangeColor changeColor = renderEvent.ChangeColor;

		if (!_entities.TryGetValue(renderEvent.EntityId, out Node3D entity))
		{
			GD.PrintErr($"Entity '{renderEvent.EntityId}' is not registered.");
			return;
		}

		// 1. Create a unique standard material
		var newMaterial = new StandardMaterial3D();
		newMaterial.AlbedoColor = new Color(
			changeColor.R / 255.0f,
			changeColor.G / 255.0f,
			changeColor.B / 255.0f
		);

		// 2. Safely apply the material to the mesh node at the end of the frame
		entity.SetDeferred(MeshInstance3D.PropertyName.MaterialOverride, newMaterial);
	}

	private void HandleRemoveEntity(RenderEvent renderEvent)
	{
		if (!_entities.TryGetValue(renderEvent.EntityId, out Node3D entity))
		{
			GD.PrintErr($"Entity '{renderEvent.EntityId}' is not registered.");
			return;
		}

		// Remove the entity from the scene tree
		entity.QueueFree();
		_entities.Remove(renderEvent.EntityId);
	}

	private void HandleStartScene(RenderEvent renderEvent)
	{
		string sceneName = renderEvent.StartScene.SceneName;
		string scenePath = $"res://Scenes/{sceneName}.tscn";

		GD.Print($"Starting scene: {sceneName}");

		if (!ResourceLoader.Exists(scenePath))
		{
			GD.PrintErr($"Scene does not exist: {scenePath}");
			return;
		}

		_changingScene = true;

		Error result = GetTree().ChangeSceneToFile(scenePath);

		if (result != Error.Ok)
		{
			GD.PrintErr($"Failed to change scene: {result}");
			_changingScene = false;
			return;
		}

		GetTree().SceneChanged += OnSceneChanged;
	}

	private void OnSceneChanged()
	{
		GetTree().SceneChanged -= OnSceneChanged;

		Node currentScene = GetTree().CurrentScene;

		if (currentScene == null)
		{
			GD.PrintErr("SceneChanged fired, but CurrentScene is null!");
			return;
		}

		GD.Print($"Scene changed to: {currentScene.Name}");

		_entities.Clear();
		_changingScene = false;
	}

	private void HandleChangeHealth(RenderEvent renderEvent)
	{
		if (!renderEvent.PayloadCase.Equals(RenderEvent.PayloadOneofCase.ChangeHealth))
		{
			GD.PrintErr("ChangeHealth event has no payload.");
			return;
		}

		ChangeHealth changeHealth = renderEvent.ChangeHealth;

		if (!_entities.TryGetValue(renderEvent.EntityId, out Node3D entity))
		{
			GD.PrintErr($"Entity '{renderEvent.EntityId}' is not registered.");
			return;
		}

		// Assuming the entity has a HealthBar child node to update
		// var healthBar = entity.FindChild("HealthBar", true, false) as ProgressBar;

		GD.Print("Changing Health for entity: " + renderEvent.EntityId + " to " + changeHealth.Health);

		var health = entity.FindChild("ProgressBar", true, false) as ProgressBar;

		health.Value = changeHealth.Health;

		// if (healthBar == null)
		// {
		//     GD.PrintErr($"Entity '{renderEvent.EntityId}' does not have a HealthBar.");
		//     return;
		// }

		// healthBar.Value = changeHealth.NewHealth;
	}

	private void HandleDisplayMessage(RenderEvent renderEvent)
	{
		if (!renderEvent.PayloadCase.Equals(RenderEvent.PayloadOneofCase.DisplayMessage))
		{
			GD.PrintErr("DisplayMessage event has no payload.");
			return;
		}

		DisplayMessage displayMessage = renderEvent.DisplayMessage;

		// Assuming you have a UI node to display messages
		GD.Print("Displaying message: " + displayMessage.Message);
		var uiNode = GetTree().CurrentScene?.FindChild("Label", true, false) as Label;

		if (uiNode == null)
		{
			GD.PrintErr("UI node not found in the current scene.");
			return;
		}

		// Assuming the UI node has a method to display messages
		uiNode.Text = displayMessage.Message;
	}

	private void HandleModuleMapping(RenderEvent renderEvent)
	{
		if (!renderEvent.PayloadCase.Equals(RenderEvent.PayloadOneofCase.ModuleMapping))
		{
			GD.PrintErr("ModuleMapping event has no payload.");
			return;
		}

		ModuleMapping moduleMapping = renderEvent.ModuleMapping;

		// Handle the module mapping logic here
		var topLabel = GetTree().CurrentScene?.FindChild("Top", true, false) as Label;
		if(topLabel != null) {
			topLabel.Text = moduleMapping.Top.Substring(moduleMapping.Top.IndexOf(".") + 1);
		} else
		{
			GD.PrintErr("Top label not found in the current scene.");
		}
		
		var bottomLabel = GetTree().CurrentScene?.FindChild("Bottom", true, false) as Label;
		if(bottomLabel != null) {
			bottomLabel.Text = moduleMapping.Bottom.Substring(moduleMapping.Bottom.IndexOf(".") + 1);
		} else
		{
			GD.PrintErr("Bottom label not found in the current scene.");
		}

		var leftLabel = GetTree().CurrentScene?.FindChild("LeftLabel", true, false) as Label;
		if(leftLabel != null) {
			leftLabel.Text = moduleMapping.Left.Substring(moduleMapping.Left.IndexOf(".") + 1);
		} else
		{
			GD.PrintErr("Left label not found in the current scene.");
		}

		var rightLabel = GetTree().CurrentScene?.FindChild("Right", true, false) as Label;
		if(rightLabel != null) {
			rightLabel.Text = moduleMapping.Right.Substring(moduleMapping.Right.IndexOf(".") + 1);
		} else
		{
			GD.PrintErr("Right label not found in the current scene.");
		}

		var selectionLabel = GetTree().CurrentScene?.FindChild("Selection", true, false) as Label;
		if(selectionLabel != null) {
			selectionLabel.Text = moduleMapping.Select.Substring(moduleMapping.Select.IndexOf(".") + 1); 
		} else
		{
			GD.PrintErr("Selection label not found in the current scene.");
		}
		
	}
}
