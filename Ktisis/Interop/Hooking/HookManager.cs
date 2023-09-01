using System;
using System.Collections.Generic;

using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Logging;

namespace Ktisis.Interop.Hooking; 

public class HookManager : IDisposable {
	// Constructor

	private readonly ISigScanner _sig;
	
	public HookManager(ISigScanner _sig) {
		this._sig = _sig;
	}
	
	// Hook registration & creation
	
	private readonly List<IHookWrapper> Registered = new();

	public void Add(IHookWrapper hook)
		=> this.Registered.Add(hook);

	public void Add<T>(Hook<T> hook) where T : Delegate {
		var inst = HookWrapper<T>.FromHook(hook);
		Add(inst);
		PluginLog.Verbose($"Registered hook '{GetHookName(hook)}' @ 0x{hook.Address:X}");
	}
	
	public Hook<T> AddAddress<T>(nint addr, T detour) where T : Delegate {
		var hook = Hook<T>.FromAddress(addr, detour);
		Add(hook);
		return hook;
	}

	public Hook<T> AddSignature<T>(string sig, T detour) where T : Delegate {
		var addr = this._sig.ScanText(sig);
		return AddAddress(addr, detour);
	}
	
	// Helpers

	private static string GetHookName<T>(Hook<T> hook) where T : Delegate
		=> hook.GetType().GetGenericArguments()[0].Name;
	
	// Disposal

	public void Dispose() {
		this.Registered.ForEach(Dispose);
	}

	private void Dispose(IHookWrapper hook) {
		try {
			hook.Disable();
			if (!hook.IsDisposed)
				hook.Dispose();
		} catch (Exception err) {
			PluginLog.Error($"Failed to dispose hook @ {hook.Address}':\n{err}");
		}
	}
}