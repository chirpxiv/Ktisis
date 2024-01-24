using System.Threading.Tasks;

using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility;

using Ktisis.Data.Files;
using Ktisis.Editor.Context;
using Ktisis.Scene.Entities.Game;
using Ktisis.Scene.Factory.Types;
using Ktisis.Scene.Modules.Actors;

namespace Ktisis.Scene.Factory.Creators;

public interface IActorCreator : IEntityCreator<ActorEntity, IActorCreator> {
	public IActorCreator FromOverworld(GameObject originator);
	public IActorCreator WithAppearance(CharaFile file);
}

public sealed class ActorCreator : EntityCreator<ActorEntity, IActorCreator>, IActorCreator {
	private readonly IEditorContext Context;
	
	private GameObject? Originator;
	private CharaFile? Appearance;

	public ActorCreator(
		IEditorContext context
	) : base(context.Scene) {
		this.Context = context;
	}
	
	protected override IActorCreator Builder => this;
	
	public IActorCreator FromOverworld(GameObject originator) {
		this.Originator = originator;
		return this;
	}

	public IActorCreator WithAppearance(CharaFile file) {
		this.Appearance = file;
		return this;
	}

	public async Task<ActorEntity> Spawn() {
		var module = this.Scene.GetModule<ActorModule>();

		ActorEntity entity;
		if (this.Originator != null && this.Originator.IsValid())
			entity = await module.AddFromOverworld(this.Originator);
		else
			entity = await module.Spawn();

		if (!this.Name.IsNullOrEmpty()) {
			module.SetActorName(entity.Actor, this.Name);
			entity.Name = entity.Actor.Name.TextValue;
		}

		if (this.Appearance != null)
			await this.Context.Characters.ApplyCharaFile(entity, this.Appearance, gameState: true);

		return entity;
	}
}
