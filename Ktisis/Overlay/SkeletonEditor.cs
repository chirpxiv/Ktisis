﻿using System;
using System.Numerics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using ImGuiNET;
using ImGuizmoNET;

using Dalamud.Game.Gui;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;

using FFXIVClientStructs.FFXIV.Client.Game.Control;

using Ktisis.Structs;
using Ktisis.Structs.Actor;
using Ktisis.Structs.Bones;
using Ktisis.Structs.FFXIV;

namespace Ktisis.Overlay {
	public sealed class SkeletonEditor {
		private Ktisis Plugin;
		private GameGui Gui;
		private ObjectTable ObjectTable;

		public bool Visible = true;

		public GameObject? Subject;
		public List<BoneList>? Skeleton;

		public BoneSelector BoneSelector;
		public BoneMod BoneMod;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		internal delegate IntPtr GetMatrixDelegate();
		internal GetMatrixDelegate GetMatrix;

		float[] cameraView = {
			1.0f, 0.0f, 0.0f, 0.0f,
			0.0f, 1.0f, 0.0f, 0.0f,
			0.0f, 0.0f, 1.0f, 0.0f,
			0.0f, 0.0f, 0.0f, 1.0f
		};

		// Controls

		public OPERATION GizmoOp = OPERATION.UNIVERSAL;
		public MODE Gizmode = MODE.LOCAL; // TODO: Improve this.

		// Constructor

		public unsafe SkeletonEditor(Ktisis plugin, GameObject? subject) {
			Plugin = plugin;
			Gui = plugin.GameGui;
			ObjectTable = plugin.ObjectTable;

			Subject = subject;

			BoneSelector = new BoneSelector();
			BoneMod = new BoneMod();

			var matrixAddr = plugin.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8D 4C 24 ?? 48 89 4c 24 ?? 4C 8D 4D ?? 4C 8D 44 24 ??");
			GetMatrix = Marshal.GetDelegateForFunctionPointer<GetMatrixDelegate>(matrixAddr);
		}

		// Toggle visibility

		public void Show() {
			Visible = true;
		}

		public void Hide() {
			Visible = false;
		}

		// Get ActorModel

		public unsafe ActorModel* GetSubjectModel() {
			return ((Actor*)Subject?.Address)->Model;
		}

		// Bone selection

		public unsafe void SelectBone(Bone bone, BoneList bones) {
			var model = GetSubjectModel();
			if (model == null) return;

			BoneSelector.Current = (bones.Id, bone.Index);
			BoneMod.SnapshotBone(bone, model, Gizmode);
		}

		// Build skeleton

		public unsafe void BuildSkeleton() {
			Skeleton = new List<BoneList>();

			var model = GetSubjectModel();
			if (model == null)
				return;

			var linkList = new Dictionary<string, List<int>>(); // name : [index]

			// Create BoneLists

			var list = *model->HkaIndex;
			for (int i = 0; i < list.Count; i++) {
				var index = list[i];
				if (index.Pose == null)
					continue;

				var bones = new BoneList(i, index.Pose);

				var first = bones[0];
				first.IsRoot = true;

				// Is linked
				if (i > 0) {
					var firstName = first.HkaBone.Name!;

					if (!linkList.ContainsKey(firstName))
						linkList.Add(firstName, new List<int>());
					linkList[firstName].Add(i);
				}

				Skeleton.Add(bones);
			}

			// Set LinkedTo

			foreach (Bone bone in Skeleton[0]) {
				var name = bone.HkaBone.Name!;
				if (linkList.ContainsKey(name))
					bone.LinkedTo = linkList[name];
			}
		}

		// Draw

		public unsafe void Draw(ImDrawListPtr draw) {
			if (!Visible || !Plugin.Configuration.ShowSkeleton)
				return;

			if (!Plugin.IsInGpose())
				return;

			var tarSys = TargetSystem.Instance();
			if (tarSys == null)
				return;

			var target = ObjectTable.CreateObjectReference((IntPtr)(tarSys->GPoseTarget));
			if (target == null || Subject == null || Subject.Address != target.Address) {
				Subject = target;
				if (Subject != null)
					BuildSkeleton();
			}

			if (Subject == null)
				return;
			if (Skeleton == null)
				return;

			var model = GetSubjectModel();
			if (model == null)
				return;

			var cam = CameraManager.Instance()->Camera;
			if (cam == null)
				return;

			var hoveredBones = new List<(int ListId, int Index)>();


			foreach (BoneList bones in Skeleton) {
				foreach (Bone bone in bones) {
					if (bone.IsRoot)
						continue;

					var pair = (bones.Id, bone.Index);

					var worldPos = model->Position + bone.Rotate(model->Rotation) * model->Height;
					Gui.WorldToScreen(worldPos, out var pos);

					if (Plugin.Configuration.DrawLinesOnSkeleton) {
						if (bone.ParentId > 0) { // Lines
							var parent = bones.GetParentOf(bone);
							var parentPos = model->Position + parent.Rotate(model->Rotation) * model->Height;

							Gui.WorldToScreen(parentPos, out var pPos);
							draw.AddLine(pos, pPos, 0x90ffffff);
						}
					}

					if (pair == BoneSelector.Current) { // Gizmo
						var io = ImGui.GetIO();
						var wp = ImGui.GetWindowPos();

						var matrix = (WorldMatrix*)GetMatrix();
						if (matrix == null)
							return;

						ImGuizmo.BeginFrame();
						ImGuizmo.SetDrawlist();
						ImGuizmo.SetRect(wp.X, wp.Y, io.DisplaySize.X, io.DisplaySize.Y);

						ImGuizmo.AllowAxisFlip(Plugin.Configuration.AllowAxisFlip);

						ImGuizmo.Manipulate(
							ref matrix->Projection.M11,
							ref cameraView[0],
							GizmoOp,
							Gizmode,
							ref BoneMod.BoneMatrix.M11,
							ref BoneMod.DeltaMatrix.M11
						);

						ImGuizmo.DrawCubes(
							ref matrix->Projection.M11,
							ref cameraView[0],
							ref BoneMod.BoneMatrix.M11,
							1
						);

						// TODO: Streamline this.

						//BoneMod.SnapshotBone(bone, model);

						var delta = BoneMod.GetDelta();

						bone.Transform.Rotate *= delta.Rotate;
						bone.TransformBone(delta, Skeleton);

					} else { // Dot
						var radius = Math.Max(3.0f, 10.0f - cam->Distance);

						var area = new Vector2(radius, radius);
						var rectMin = pos - area;
						var rectMax = pos + area;

						var hovered = ImGui.IsMouseHoveringRect(rectMin, rectMax);
						if (hovered)
							hoveredBones.Add(pair);

						draw.AddCircleFilled(pos, Math.Max(2.0f, 8.0f - cam->Distance), hovered ? 0xffffffff : 0x90ffffff, 100);
					}
				}

				//break;
			}

			if (hoveredBones.Count > 0)
				BoneSelector.Draw(this, hoveredBones);
		}
	}
}