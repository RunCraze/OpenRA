﻿#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.RA
{
	public class ChatCommandsInfo : TraitInfo<ChatCommands> { }

	public class ChatCommands : INotifyChat
	{
		public Dictionary<string, IChatCommand> Commands { get; private set; }

		public ChatCommands()
		{
			Commands = new Dictionary<string, IChatCommand>();
		}

		public bool OnChat(string playername, string message)
		{
			if (message.StartsWith("/"))
			{
				var name = message.Substring(1).Split(' ')[0].ToLowerInvariant();
				var command = Commands.FirstOrDefault(x => x.Key == name);

				if (command.Value != null)
					command.Value.InvokeCommand(name.ToLowerInvariant(), message.Substring(1 + name.Length));
				else
					Game.Debug("{0} is not a valid command.", name);

				return false;
			}

			return true;
		}

		public void RegisterCommand(string name, IChatCommand command)
		{
			Commands.Add(name.ToLowerInvariant(), command);
		}
	}

	public interface IChatCommand
	{
		void InvokeCommand(string command, string arg);
	}
}
