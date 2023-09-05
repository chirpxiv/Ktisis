using System.Linq;
using System.Collections.Generic;

using Ktisis.Scene.Impl;
using Ktisis.Scene.Objects;

namespace Ktisis.Scene.Editing;

public enum SelectFlags {
	None,
	Multiple
}

public class SelectState {
	// Constructor
	
	private readonly List<SceneObject> _selected = new();

	public void Attach(SceneGraph scene) {
		scene.OnSceneObjectRemoved += OnSceneObjectRemoved;
	}
	
	// Events

	private void OnSceneObjectRemoved(SceneGraph _scene, SceneObject item)
		=> RemoveItem(item);

	// Item access
	
	public int Count => this._selected.Count;

	public void Clear() => this._selected.Clear();

	public IEnumerable<SceneObject> GetSelected()
		=> this._selected.AsReadOnly();

	public IEnumerable<IManipulable> GetManipulable() => GetSelected()
        .Where(item => item is IManipulable)
        .Cast<IManipulable>();
	
	// Item management

	public void AddItem(SceneObject item) {
		item.Flags |= ObjectFlags.Selected;
		this._selected.Remove(item);
		this._selected.Insert(0, item);
	}

	public void RemoveItem(SceneObject item) {
		item.Flags &= ~ObjectFlags.Selected;
		this._selected.Remove(item);
	}

	public void RemoveAll() {
		this._selected.ForEach(item => item.Flags &= ~ObjectFlags.Selected);
		this._selected.Clear();
	}
	
	// Handler

	public void HandleClick(SceneObject item, SelectFlags flags) {
		var isSelect = item.IsSelected();
		var isMulti = this.Count > 1;
		
		// Ctrl modifier
		if (flags.HasFlag(SelectFlags.Multiple)) {
			RemoveItem(item);
		} else {
			RemoveAll();
		}

		if (!isSelect || isMulti)
			AddItem(item);
	}
}
