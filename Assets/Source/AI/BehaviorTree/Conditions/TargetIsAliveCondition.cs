using System;
using Unity.Behavior;
using UnityEngine;

namespace Quinn.AI.BehaviorTree
{
	[Serializable, Unity.Properties.GeneratePropertyBag]
	[Condition(name: "Target Alive State", story: "[Target] is [State]", category: "Conditions", id: "eff4a53a5663fc9240e43f18e03c6a1a")]
	public partial class TargetIsAliveCondition : Condition
	{
		public enum AliveState
		{
			Alive,
			Dead
		}

		[SerializeReference]
		public BlackboardVariable<Health> Target;
		[SerializeReference]
		public BlackboardVariable<AliveState> State;

		public override bool IsTrue()
		{
			if (Target.Value == null) return false;

			if (State.Value is AliveState.Alive) return Target.Value.IsAlive;
			if (State.Value is AliveState.Dead) return !Target.Value.IsAlive;

			return false;
		}
	}
}
