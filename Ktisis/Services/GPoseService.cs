using System;

using Dalamud.Plugin.Services;

using Ktisis.Core.Attributes;
using Ktisis.Events;

namespace Ktisis.Services;

public delegate void GPoseStateHandler(GPoseService sender, bool state);

public interface IGPoseService {
	public event Action Update;
	public event GPoseStateHandler StateChanged;
	
	public bool IsGPosing { get; }
}

[Singleton]
public class GPoseService : IGPoseService, IDisposable {
	private readonly IClientState _clientState;
	private readonly IFramework _framework;
	
	private readonly Event<Action> _updateEvent;
	public event Action Update {
		add => this._updateEvent.Add(value);
		remove => this._updateEvent.Remove(value);
	}

	private readonly Event<Action<GPoseService, bool>> _gposeEvent;
	public event GPoseStateHandler StateChanged {
		add => this._gposeEvent.Add(value.Invoke);
		remove => this._gposeEvent.Remove(value.Invoke);
	}
	
	private bool _isActive;

	public bool IsGPosing => this._clientState.IsGPosing;
	
	public GPoseService(
		IClientState clientState,
		IFramework framework,
		Event<Action> updateEvent,
		Event<Action<GPoseService, bool>> gposeEvent
	) {
		this._clientState = clientState;
		this._framework = framework;
		this._updateEvent = updateEvent;
		this._gposeEvent = gposeEvent;
	}

	public void Subscribe() {
		this._framework.Update += this.OnFrameworkUpdate;
	}

	private void OnFrameworkUpdate(IFramework sender) {
		var state = this.IsGPosing;
		if (this._isActive != state) {
			this._isActive = state;
			Ktisis.Log.Info($"GPose state changed: {state}");
			this._gposeEvent.Invoke(this, state);
		}
		
		if (state) this._updateEvent.Invoke();
	}

	public void Dispose() {
		this._framework.Update -= this.OnFrameworkUpdate;
	}
}