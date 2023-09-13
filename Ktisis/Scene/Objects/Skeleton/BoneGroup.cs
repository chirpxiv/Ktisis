using System.Linq;
using System.Numerics;

using Ktisis.Scene.Impl;
using Ktisis.Common.Utility;
using Ktisis.Data.Config.Bones;
using Ktisis.Data.Config.Display;

namespace Ktisis.Scene.Objects.Skeleton; 

public class BoneGroup : ArmatureGroup, IDummy {
	// Properties

	public override ItemType ItemType => ItemType.BoneGroup;
	
	// Constructor

	private readonly Armature Armature;

	public readonly BoneCategory? Category;

	public BoneGroup(Armature armature, BoneCategory category) {
		this.Name = category.Name ?? "Unknown";
		
		this.Armature = armature;
		this.Category = category;
	}
	
	// Armature access
	
	public override Armature GetArmature() => this.Armature;

	// Stale check

	public bool IsStale() => this.Children.Count == 0;
	
	// IDummy

	public Transform Transform { get; set; } = new();

	private Transform MakeTransform() {
		var transforms = GetIndividualBones()
			.Select(bone => bone.GetTransform())
			.Where(trans => trans != null)
			.Cast<Transform>()
			.ToList();

		var result = new Transform();

		var count = transforms.Count;
		if (count == 0) return result;

		Quaternion rot;
		if (this.GetCommonParent()?.GetTransform() is Transform pTrans) {
			rot = pTrans.Rotation;
		} else {
			var weight = 1f / count;
			rot = transforms
				.Select(t => t.Rotation)
				.Aggregate((a, b) => a * Quaternion.Slerp(Quaternion.Identity, b, weight));
		}
		
		result = transforms.Aggregate(result, (a, b) => {
			a.Position += b.Position;
			a.Scale += b.Scale;
			return a;
		});
        
        result.Position /= count;
		result.Rotation = Quaternion.Normalize(rot);
		result.Scale /= count;
		
		return result;
	}

	public void CalcTransform()
		=> this.Transform = MakeTransform();

	public Transform GetTransform() {
		var calc = MakeTransform();
		if (Vector3.Distance(calc.Position, this.Transform.Position) > 0.1f)
			this.Transform = calc;
		return this.Transform;
	}
}
