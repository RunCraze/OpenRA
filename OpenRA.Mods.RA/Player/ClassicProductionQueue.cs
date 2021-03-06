#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.RA.Buildings;
using OpenRA.Traits;

namespace OpenRA.Mods.RA
{
	[Desc("Attach this to the world actor (not a building!) to define a new shared build queue.",
		"Will only work together with the Production: trait on the actor that actually does the production.",
		"You will also want to add PrimaryBuildings: to let the user choose where new units should exit.")]
	public class ClassicProductionQueueInfo : ProductionQueueInfo, Requires<TechTreeInfo>, Requires<PowerManagerInfo>, Requires<PlayerResourcesInfo>
	{
		[Desc("If you build more actors of the same type,", "the same queue will get its build time lowered for every actor produced there.")]
		public readonly bool SpeedUp = false;

		[Desc("Every time another production building of the same queue is",
			"contructed, the build times of all actors in the queue",
			"decreased by a percentage of the original time.")]
		public readonly int[] BuildTimeSpeedReduction = { 100, 85, 75, 65, 60, 55, 50 };

		public override object Create(ActorInitializer init) { return new ClassicProductionQueue(init, this); }
	}

	public class ClassicProductionQueue : ProductionQueue, ISync
	{
		static readonly ActorInfo[] NoItems = { };

		readonly Actor self;
		readonly ClassicProductionQueueInfo info;

		public ClassicProductionQueue(ActorInitializer init, ClassicProductionQueueInfo info)
			: base(init, init.self, info)
		{
			this.self = init.self;
			this.info = info;
		}

		[Sync] bool isActive = false;

		public override void Tick(Actor self)
		{
			isActive = false;
			foreach (var x in self.World.ActorsWithTrait<Production>())
			{
				if (x.Actor.Owner == self.Owner && x.Trait.Info.Produces.Contains(Info.Type))
				{
					isActive = true;
					break;
				}
			}

			base.Tick(self);
		}

		public override IEnumerable<ActorInfo> AllItems()
		{
			return isActive ? base.AllItems() : NoItems;
		}

		public override IEnumerable<ActorInfo> BuildableItems()
		{
			return isActive ? base.BuildableItems() : NoItems;
		}

		protected override bool BuildUnit(string name)
		{
			// Find a production structure to build this actor
			var producers = self.World.ActorsWithTrait<Production>()
				.Where(x => x.Actor.Owner == self.Owner
					&& x.Trait.Info.Produces.Contains(Info.Type))
					.OrderByDescending(x => x.Actor.IsPrimaryBuilding())
					.ThenByDescending(x => x.Actor.ActorID);

			if (!producers.Any())
			{
				CancelProduction(name, 1);
				return true;
			}

			foreach (var p in producers.Where(p => !p.Actor.IsDisabled()))
			{
				if (p.Trait.Produce(p.Actor, self.World.Map.Rules.Actors[name], Race))
				{
					FinishProduction();
					return true;
				}
			}

			return false;
		}

		public override int GetBuildTime(string unitString)
		{
			var unit = self.World.Map.Rules.Actors[unitString];
			if (unit == null || !unit.Traits.Contains<BuildableInfo>())
				return 0;

			if (self.World.AllowDevCommands && self.Owner.PlayerActor.Trait<DeveloperMode>().FastBuild)
				return 0;

			var time = (int)(unit.GetBuildTime() * Info.BuildSpeed);

			if (info.SpeedUp)
			{
				var queues = unit.Traits.Get<BuildableInfo>().Queue;
				var selfsameBuildings = self.World.ActorsWithTrait<Production>()
					.Where(p => p.Actor.Owner == self.Owner && p.Trait.Info.Produces.Intersect(queues).Any())
					.ToArray();

				var speedModifier = selfsameBuildings.Count().Clamp(1, info.BuildTimeSpeedReduction.Length) - 1;
				time = (time * info.BuildTimeSpeedReduction[speedModifier]) / 100;
			}

			return time;
		}
	}
}
