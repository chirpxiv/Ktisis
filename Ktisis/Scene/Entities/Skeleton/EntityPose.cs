using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using FFXIVClientStructs.Havok;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

using Ktisis.Editor.Posing.Partials;

using RenderSkeleton = FFXIVClientStructs.FFXIV.Client.Graphics.Render.Skeleton;

using Ktisis.Scene.Entities.Character;
using Ktisis.Scene.Factory.Builders;
using Ktisis.Scene.Types;

namespace Ktisis.Scene.Entities.Skeleton;

public class EntityPose : SkeletonGroup {
	private readonly IPoseBuilder _builder;
	
	public EntityPose(
		ISceneManager scene,
		IPoseBuilder builder
	) : base(scene) {
		this._builder = builder;
		this.Type = EntityType.Armature;
		this.Name = "Pose";
		this.Pose = this;
	}
	
	// Bones

	private readonly Dictionary<int, PartialSkeletonInfo> Partials = new();

	private readonly Dictionary<(int p, int i), BoneNode> BoneMap = new();
	
	// Update handler

	public override void Update() {
		if (this.IsValid)
			this.UpdatePose();
	}

	private unsafe void UpdatePose() {
		var skeleton = this.GetSkeleton();
		if (skeleton == null) return;

		for (var index = 0; index < skeleton->PartialSkeletonCount; index++)
			this.UpdatePartial(skeleton, index);
	}

	private unsafe void UpdatePartial(RenderSkeleton* skeleton, int index) {
		var partial = skeleton->PartialSkeletons[index];
		var resource = partial.SkeletonResourceHandle;

		var id = resource != null ? resource->ResourceHandle.Id : 0;

		uint prevId = 0;
		if (this.Partials.TryGetValue(index, out var info)) {
			prevId = info.Id;
		} else {
			info = new PartialSkeletonInfo(id);
			this.Partials.Add(index, info);
		}

		if (id == prevId) return;
		
		Ktisis.Log.Verbose($"Skeleton of '{this.Parent?.Name ?? "UNKNOWN"}' detected a change in partial #{index} (was {prevId:X}, now {id:X}), rebuilding.");

		var t = new Stopwatch();
		t.Start();
		
		var builder = this._builder.BuildBoneTree(index, id, partial);
		var isWeapon = skeleton->Owner->GetModelType() == CharacterBase.ModelType.Weapon;
		if (!isWeapon)
			builder.BuildCategoryMap();
		else
			builder.BuildBoneList();
		
		if (prevId != 0)
			this.Clean(index, id);

		info.CopyPartial(id, partial);
		if (id != 0)
			builder.BindTo(this);

		this.BuildBoneMap(index, id);
		
		t.Stop();
		Ktisis.Log.Debug($"Rebuild took {t.Elapsed.TotalMilliseconds:00.00}ms");
	}

	private void BuildBoneMap(int index, uint id) {
		foreach (var key in this.BoneMap.Keys.Where(key => key.p == index))
			this.BoneMap.Remove(key);

		if (id == 0) return;
		foreach (var child in this.Recurse())
			if (child is BoneNode bone && bone.Info.PartialIndex == index)
				this.BoneMap.Add((index, bone.Info.BoneIndex), bone);
	}
	
	// Skeleton access

	public unsafe RenderSkeleton* GetSkeleton() {
		var character = this.Parent is CharaEntity parent ? parent.GetCharacter() : null;
		return character != null ? character->Skeleton : null;
	}

	public unsafe hkaPose* GetPose(int index) {
		var skeleton = this.GetSkeleton();
		if (skeleton == null) return null;
		var partial = skeleton->PartialSkeletons[index];
		return partial.GetHavokPose(0);
	}

	public BoneNode? GetBoneFromMap(int partialIx, int boneIx)
		=> this.BoneMap.GetValueOrDefault((partialIx, boneIx));

	public PartialSkeletonInfo? GetPartial(int index)
		=> this.Partials.GetValueOrDefault(index);
}
