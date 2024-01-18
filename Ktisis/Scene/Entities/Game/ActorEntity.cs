using Dalamud.Game.ClientState.Objects.Types;

using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using CSCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

using Ktisis.Editor.Characters.Data;
using Ktisis.Scene.Decor;
using Ktisis.Scene.Entities.Character;
using Ktisis.Scene.Factory.Builders;
using Ktisis.Scene.Modules.Actors;
using Ktisis.Scene.Types;

namespace Ktisis.Scene.Entities.Game;

public class ActorEntity : CharaEntity, IDeletable {
	public readonly GameObject Actor;

	public AppearanceState Appearance { get; } = new();

	public override bool IsValid => base.IsValid && this.Actor.IsValid();

	public ActorEntity(
		ISceneManager scene,
		IPoseBuilder pose,
		GameObject actor
	) : base(scene, pose) {
		this.Type = EntityType.Actor;
		this.Actor = actor;
	}
	
	// Update handler

	public override void Update() {
		if (!this.IsObjectValid) return;
		this.UpdateChara();
		base.Update();
	}

	private unsafe void UpdateChara() {
		var address = (nint)this.GetCharacter();
		if (this.Address != address)
			this.Address = address;
	}
	
	// GameObject
	
	public unsafe CSGameObject* CsGameObject => (CSGameObject*)this.Actor.Address;

	public unsafe CSCharacter* Character => this.CsGameObject != null && this.CsGameObject->IsCharacter() ? (CSCharacter*)this.CsGameObject : null;
	
	// CharacterBase

	public unsafe override Object* GetObject()
		=> this.CsGameObject != null ? &this.CsGameObject->DrawObject->Object : null;

	public unsafe override CharacterBase* GetCharacter() {
		if (!this.IsObjectValid) return null;
		var ptr = this.CsGameObject != null ? this.CsGameObject->DrawObject : null;
		if (ptr == null || ptr->Object.GetObjectType() != ObjectType.CharacterBase)
			return null;
		return (CharacterBase*)ptr;
	}

	public unsafe void Redraw() {
		if (this.CsGameObject == null) return;
		this.CsGameObject->DisableDraw();
		this.CsGameObject->EnableDraw();
	}
	
	// Deletable

	public bool Delete() {
		this.Scene.GetModule<ActorModule>().Delete(this);
		return false;
	}
}
