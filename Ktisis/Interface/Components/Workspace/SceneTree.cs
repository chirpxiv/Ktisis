using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

using GLib.Popups;
using GLib.Widgets;

using ImGuiNET;

using Ktisis.Common.Extensions;
using Ktisis.Common.Utility;
using Ktisis.Core.Attributes;
using Ktisis.Data.Config;
using Ktisis.Editor.Context;
using Ktisis.Interface.Menus;
using Ktisis.Scene;
using Ktisis.Scene.Decor;
using Ktisis.Scene.Entities;

namespace Ktisis.Interface.Components.Workspace;

[Transient]
public class SceneTree {
	private readonly ConfigManager _cfg;
	private readonly EntityMenuFactory _menu;
	private readonly SceneDragDropHandler _dragDrop;

	private readonly PopupManager _popup = new();
	
	public SceneTree(
		ConfigManager cfg,
		EntityMenuFactory menu,
		SceneDragDropHandler dragDrop
	) {
		this._cfg = cfg;
		this._menu = menu;
		this._dragDrop = dragDrop;
	}
	
	// Draw frame
    
	public void Draw(ISceneManager? scene, float height) {
		var frame = false;
		try {
			var id = ImGui.GetID("SceneTree_Frame");
			frame = ImGui.BeginChildFrame(id, new Vector2(-1, height));
			if (frame && scene != null)
				this.DrawScene(scene, height);
		} catch (Exception err) {
			Ktisis.Log.Error($"Error drawing scene tree:\n{err}");
		} finally {
			if (frame) ImGui.EndChildFrame();
		}
		
		this._popup.Draw();
	}
	
	// Draw scene entities

	private static float IconSpacing => UiBuilder.IconFont.FontSize;

	private float MinY;
	private float MaxY;

	private void PreCalc(float height) {
		var scroll = ImGui.GetScrollY();
		this.MinY = scroll - ImGui.GetFrameHeight();
		this.MaxY = height + scroll;
	}

	private void DrawScene(ISceneManager scene, float height) {
		this.PreCalc(height);

		var spacing = ImGui.GetStyle().ItemSpacing;
		using var _ = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing with { Y = 5.0f });
		this.IterateTree(scene, scene.Children);
	}

	private void IterateTree(ISceneManager scene, IEnumerable<SceneEntity> entities) {
		try {
			ImGui.TreePush();
			foreach (var item in entities)
				this.DrawNode(scene, item);
		} finally {
			ImGui.TreePop();
		}
	}
	
	// Nodes

	private enum TreeNodeFlag {
		Leaf,
		Expand,
		Collapse
	}

	private void DrawNode(ISceneManager scene, SceneEntity node) {
		var pos = ImGui.GetCursorPos();
		var isRender = pos.Y > this.MinY && pos.Y < this.MaxY;

		var id = $"##SceneTree_{node.GetHashCode():X}";

		const ImGuiSelectableFlags selectFlags = ImGuiSelectableFlags.AllowItemOverlap | ImGuiSelectableFlags.SpanAllColumns;
		ImGui.Selectable(id, node.IsSelected, selectFlags);

		var isHover = ImGui.IsWindowHovered();
		var size = ImGui.GetItemRectSize();
		
		// NOTE: This blocks ImGUi.IsWindowHovered for some stupid reason
		this._dragDrop.Handle(node);

		var imKey = ImGui.GetID(id);
		var state = ImGui.GetStateStorage();
		
		var isExpand = state.GetBool(imKey);

		var children = node.Children.ToList();
		
		if (isRender) {
			var flag = isExpand switch {
				_ when children.Count is 0 => TreeNodeFlag.Leaf,
				true => TreeNodeFlag.Expand,
				false => TreeNodeFlag.Collapse
			};

			var rightAdjust = this.DrawButtons(scene, node, isHover);
			if (this.DrawNodeLabel(node, pos, flag, rightAdjust))
				state.SetBool(imKey, isExpand = !isExpand);

			if (isHover && this.IsNodeHovered(pos, size, rightAdjust)) {
				if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) {
					this._menu.OpenEditorFor(scene.Context, node);
				} else if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
					var mode = GuiHelpers.GetSelectMode();
					node.Select(mode);
				} else if (ImGui.IsMouseClicked(ImGuiMouseButton.Right)) {
					var ctx = this._menu.Build(scene.Context, node).Open();
					this._popup.Add(ctx);
				}
			}
		}

		if (isExpand)
			this.IterateTree(scene, children);
	}

	private bool DrawNodeLabel(SceneEntity item, Vector2 pos, TreeNodeFlag flag, float rightAdjust = 0.0f) {
		var display = this._cfg.Config.Editor.GetDisplayForType(item.Type);
        
        // Caret

		var style = ImGui.GetStyle();
		ImGui.SameLine();
		ImGui.SetCursorPosX(pos.X - style.ItemSpacing.X);
		var expand = this.DrawNodeCaret(display.Color, flag);
		
		// Icon + Label

		using var _ = ImRaii.PushColor(ImGuiCol.Text, display.Color);
		this.DrawNodeIcon(display.Icon);
			
		var avail = ImGui.GetContentRegionAvail().X;
		ImGui.Text(item.Name.FitToWidth(avail - rightAdjust));

		return expand;
	}

	private bool DrawNodeCaret(uint color, TreeNodeFlag flag) {
		var cursor = ImGui.GetCursorPosX();

		var caretIcon = flag switch {
			TreeNodeFlag.Collapse => FontAwesomeIcon.CaretRight,
			TreeNodeFlag.Expand => FontAwesomeIcon.CaretDown,
			_ => FontAwesomeIcon.None
		};
		
		using (var _ = ImRaii.PushColor(ImGuiCol.Text, color.SetAlpha(0xCF)))
			Icons.DrawIcon(caretIcon);

		ImGui.SameLine();
		
		var spacing = ImGui.GetStyle().ItemInnerSpacing;
		cursor += spacing.X + IconSpacing;
		ImGui.SetCursorPosX(cursor);
		
		var iconSize = Icons.CalcIconSize(caretIcon);
		var frameHeight = ImGui.GetFrameHeight();
		return ButtonsEx.IsClicked(
			new Vector2(
				IconSpacing - iconSize.X,
				(frameHeight - iconSize.Y - spacing.Y / 2) / 2
			)
		);
	}

	private void DrawNodeIcon(FontAwesomeIcon icon) {
		var hasIcon = icon != FontAwesomeIcon.None;
		var iconPadding = hasIcon ? Icons.CalcIconSize(icon).X / 2.0f : 0.0f;
		var iconSpace = hasIcon ? IconSpacing : 0;
		Icons.DrawIcon(icon);
		ImGui.SameLine(0, iconSpace - iconPadding);
	}
	
	// Buttons

	private float DrawButtons(ISceneManager scene, SceneEntity node, bool isHover) {
		var initial = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
		var cursor = initial;

		this.DrawVisibilityButton(node, ref cursor, isHover);
		this.DrawAttachButton(scene.Context, node, ref cursor, isHover);
		
		return initial - cursor;
	}

	private void DrawVisibilityButton(SceneEntity node, ref float cursor, bool isHover) {
		if (node is not IVisibility vis) return;
		
		if (this.DrawButton(ref cursor, FontAwesomeIcon.Eye, vis.Visible ? 0xEFFFFFFF : 0x80FFFFFF) && isHover)
			vis.Toggle();
	}

	private void DrawAttachButton(IEditorContext context, SceneEntity node, ref float cursor, bool isHover) {
		if (node is not IAttachable attach || !attach.IsAttached()) return;
		
		if (this.DrawButton(ref cursor, FontAwesomeIcon.Link, 0xFFFFFFFF) && isHover)
			attach.Detach();

		if (!isHover || !ImGui.IsItemHovered()) return;

		var bone = attach.GetParentBone();
		var name = bone != null ? context.Locale.GetBoneName(bone) : "UNKNOWN";
		using var _tooltip = ImRaii.Tooltip();
		ImGui.Text($"Attached to {name}");
	}

	private bool DrawButton(ref float cursor, FontAwesomeIcon icon, uint? color = null) {
		cursor -= Icons.CalcIconSize(icon).X + ImGui.GetStyle().ItemSpacing.X;
		ImGui.SameLine();
		ImGui.SetCursorPosX(cursor);
		using var _ = ImRaii.PushColor(ImGuiCol.Text, color ?? 0, color.HasValue);
		Icons.DrawIcon(icon);
		return ButtonsEx.IsClicked();
	}
	
	// Handle hover + click

	private bool IsNodeHovered(Vector2 pos, Vector2 size, float rightAdjust) {
		var pad = ImGui.GetStyle().ItemSpacing.X;
		var min = ImGui.GetWindowPos() + pos.AddX(pad).SubY(ImGui.GetScrollY() + 2);
		var max = min.Add(size.X - pos.X - pad - rightAdjust, size.Y);
		return ImGui.IsMouseHoveringRect(min, max);
	}
}
