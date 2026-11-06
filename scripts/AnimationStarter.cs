using Godot;
using System;
using System.Linq;

public partial class AnimationStarter : Node3D
{
	[Export] private AnimationPlayer _animationPlayer;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_animationPlayer ??= GetNodeOrNull<AnimationPlayer>(
            "AnimationPlayer"
		);

		if (_animationPlayer == null)
		{
			GD.PushError(
                "AnimationStarter: AnimationPlayer was not found."
			);
			return;
		}

		const string idleAnimation = "Rig_Medium_General/Idle_A";

		if (!_animationPlayer.HasAnimation(idleAnimation))
		{
			GD.PushError(
				$"AnimationStarter: Animation '{idleAnimation}' not found."
			);
			return;
		}

		_animationPlayer.Play(idleAnimation);

		GD.Print(
			$"AnimationStarter: Playing {_animationPlayer.CurrentAnimation}"
		);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
