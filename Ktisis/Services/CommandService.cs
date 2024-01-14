using System;
using System.Collections.Generic;

using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using HandlerDelegate = Dalamud.Game.Command.CommandInfo.HandlerDelegate;

using Ktisis.Interface;
using Ktisis.Interface.Windows;
using Ktisis.Core.Attributes;

namespace Ktisis.Services;

[Singleton]
public class CommandService : IDisposable {
	private readonly ICommandManager _cmd;
	private readonly GuiManager _gui;

	private readonly HashSet<string> _register = new();
	
	public CommandService(
		ICommandManager cmd,
		GuiManager gui
	) {
		this._cmd = cmd;
		this._gui = gui;
	}
	
	// Handler registration

	public void RegisterHandlers() {
		BuildCommand("/ktisis", OnMainCommand)
			.SetMessage("Toggle the main Ktisis window.")
			.Create();
	}

	private void Add(string name, CommandInfo info) {
		if (this._register.Add(name))
			this._cmd.AddHandler(name, info);
	}

	private CommandFactory BuildCommand(string name, HandlerDelegate handler)
		=> new(this, name, handler);
	
	// Command handlers

	private void OnMainCommand(string _command, string _arguments) {
		Ktisis.Log.Info("Main command used");
		this._gui.GetOrCreate<WorkspaceWindow>().Toggle();
	}
	
	// Disposal

	public void Dispose() {
		foreach (var cmdName in this._register)
			this._cmd.RemoveHandler(cmdName);
	}
	
	// Factory

	private class CommandFactory {
		private readonly CommandService _cmd;

		private readonly string Name;
		private readonly List<string> Alias = new();

		private readonly HandlerDelegate Handler;

		private bool ShowInHelp;
		private string HelpMessage = string.Empty;
		
		public CommandFactory(CommandService _cmd, string name, HandlerDelegate handler) {
			this._cmd = _cmd;
			
			this.Name = name;
			this.Handler = handler;
		}
		
		// Factory methods
		
		public CommandFactory SetMessage(string message) {
			this.ShowInHelp = true;
			this.HelpMessage = message;
			return this;
		}

		public CommandFactory AddAlias(string alias) {
			this.Alias.Add(alias);
			return this;
		}

		public CommandFactory AddAliases(params string[] aliases) {
			this.Alias.AddRange(aliases);
			return this;
		}

		public void Create() {
			this._cmd.Add(this.Name, BuildCommandInfo());
			this.Alias.ForEach(CreateAlias);
		}

		private void CreateAlias(string alias) {
			this._cmd.Add(alias, new CommandInfo(this.Handler) {
				ShowInHelp = false
			});
		}

		// CommandInfo

		private CommandInfo BuildCommandInfo() {
			var message = this.HelpMessage;
			if (this.HelpMessage != string.Empty && this.Alias.Count > 0) {
				var padding = new string(' ', this.Name.Length * 2);
				message += $"\n{padding} (Aliases: {string.Join(", ", this.Alias)})";
			}

			return new CommandInfo(this.Handler) {
				ShowInHelp = this.ShowInHelp,
				HelpMessage = message
			};
		}
	}
}
