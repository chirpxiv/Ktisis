using System;
using System.Linq;
using System.Numerics;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;

using GLib.Widgets;

using ImGuiNET;

using Ktisis.Services;
using Ktisis.Structs.Characters;
using Ktisis.Core.Attributes;
using Ktisis.Editor.Characters.Make;
using Ktisis.Editor.Characters.Types;
using Ktisis.Interface.Components.Actors.Popup;

namespace Ktisis.Interface.Components.Actors;

[Transient]
public class CustomizeEditorUi {
	private readonly IDataManager _data;
	private readonly ITextureProvider _tex;
	private readonly CustomizeDiscoveryService _discovery;

	private readonly MakeTypeData _makeTypeData = new();

	private readonly FeatureSelectPopup _selectPopup;
	
	public ICustomizeEditor Editor { set; private get; } = null!;
	
	public CustomizeEditorUi(
		IDataManager data,
		ITextureProvider tex,
		CustomizeDiscoveryService discovery
	) {
		this._data = data;
		this._tex = tex;
		this._discovery = discovery;
		this._selectPopup = new FeatureSelectPopup(tex);
	}
	
	// Setup

	private bool _isSetup;
	
	public void Setup() {
		if (this._isSetup) return;
		this._isSetup = true;
		this._makeTypeData.Build(this._data, this._discovery).ContinueWith(task => {
			if (task.Exception != null)
				Ktisis.Log.Error($"Failed to build customize data:\n{task.Exception}");
		});
	}
	
	// Draw
	
	public void Draw() {
		this.ButtonSize = CalcButtonSize();
		
		var tribe = (Tribe)this.Editor.GetCustomization(CustomizeIndex.Tribe);
		var gender = (Gender)this.Editor.GetCustomization(CustomizeIndex.Gender);
		
		var data = this._makeTypeData.GetData(tribe, gender);
		if (data == null) return;

		this.Draw(data);
		
		this._selectPopup.Draw(this.Editor);
	}

	private void Draw(MakeTypeRace data) {
		this.DrawSideFrame(data);
		ImGui.SameLine();
		this.DrawMainFrame(data);
	}
	
	// Side frame

	private const float SideRatio = 0.35f;

	private void DrawSideFrame(MakeTypeRace data) {
		var size = ImGui.GetContentRegionAvail();
		size.X = MathF.Max(size.X * SideRatio, 240.0f);
		using var _frame = ImRaii.Child("##CustomizeSideFrame", size, true);

		var cX = ImGui.GetCursorPosX();
		this.DrawBodySelect(data.Gender);
		ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
		ImGui.SetNextItemWidth(ImGui.CalcItemWidth() - (ImGui.GetCursorPosX() - cX));
		this.DrawTribeSelect(data.Tribe);
		
		ImGui.Spacing();

		this.DrawFeatSlider(CustomizeIndex.Height, data);
		this.DrawFeatSlider(CustomizeIndex.BustSize, data);
		this.DrawFeatSlider(CustomizeIndex.RaceFeatureSize, data);

		ImGui.Spacing();
		
		this.DrawFeatParams(CustomizeIndex.EyeShape, data);
		
		ImGui.Spacing();
		
		this.DrawFeatParams(CustomizeIndex.LipStyle, data);
		
		ImGui.Spacing();
		
		this.DrawFeatParams(CustomizeIndex.Eyebrows, data);
		this.DrawFeatParams(CustomizeIndex.NoseShape, data);
		this.DrawFeatParams(CustomizeIndex.JawShape, data);

		var intValue = (int)this.Editor.GetCustomization(CustomizeIndex.HairColor2);
		if (ImGui.InputInt("Highlights", ref intValue))
			this.Editor.SetCustomization(CustomizeIndex.HairColor2, (byte)intValue);
	}
	
	// Body + Tribe selectors

	private void DrawBodySelect(Gender current) {
		var icon = current == Gender.Masculine ? FontAwesomeIcon.Mars : FontAwesomeIcon.Venus;
		if (Buttons.IconButton(icon))
			this.Editor.SetCustomization(CustomizeIndex.Gender, (byte)(current == Gender.Feminine ? 0 : 1));
	}

	private void DrawTribeSelect(Tribe current) {
		using var _combo = ImRaii.Combo("Body", current.ToString());
		if (!_combo.Success) return;
		
		foreach (var tribe in Enum.GetValues<Tribe>()) {
			if (ImGui.Selectable(tribe.ToString(), tribe == current)) {
				this.Editor.Prepare()
					.SetCustomization(CustomizeIndex.Tribe, (byte)tribe)
					.SetCustomization(CustomizeIndex.Race, (byte)Math.Floor(((decimal)tribe + 1) / 2))
					.Dispatch();
			}
		}
	}
	
	// Sliders

	private void DrawSlider(string label, CustomizeIndex index) {
		var intValue = (int)this.Editor.GetCustomization(index);
		if (ImGui.SliderInt(label, ref intValue, 0, 100))
			this.Editor.SetCustomization(index, (byte)intValue);
	}

	private void DrawFeatSlider(CustomizeIndex index, MakeTypeRace data) {
		var feat = data.GetFeature(index);
		if (feat == null) return;
		this.DrawSlider(feat.Name, index);
	}
	
	// Params

	private void DrawFeatParams(CustomizeIndex index, MakeTypeRace data) {
		var feat = data.GetFeature(index);
		if (feat == null) return;

		var baseValue = this.Editor.GetCustomization(index);
		var current = (byte)(baseValue & ~0x80);

		var isZeroIndex = feat.Params.FirstOrDefault()?.Value == 0;

		var intValue = (int)current;
		if (isZeroIndex) intValue++;
		if (ImGui.InputInt(feat.Name, ref intValue) && intValue >= (isZeroIndex ? 1 : 0)) {
			var newValue = (byte)(isZeroIndex ? --intValue : intValue);
			this.Editor.SetCustomization(index, (byte)(newValue | (baseValue & 0x80)));
		}
	}
	
	// Main frame

	private void DrawMainFrame(MakeTypeRace data) {
		using var _frame = ImRaii.Child("##CustomizeMainFrame", ImGui.GetContentRegionAvail());
		if (!_frame.Success) return;
		
		if (ImGui.CollapsingHeader("Primary Features"))
			this.DrawFeatIconParams(data);

		var faceFeatLabel = "Facial Features";
		var faceFeat = data.GetFeature(CustomizeIndex.FaceFeatures);
		if (faceFeat != null)
			faceFeatLabel += $" / {faceFeat.Name}";
		faceFeatLabel += " / Tattoos";
		
		if (ImGui.CollapsingHeader(faceFeatLabel))
			this.DrawFacialFeatures(data);
	}
	
	// Icons
	
	private const string LegacyTexPath = "chara/common/texture/decal_equip/_stigma.tex";

	private readonly static Vector2 MaxButtonSize = new(64, 64);
	
	private Vector2 ButtonSize = MaxButtonSize;

	private static Vector2 CalcButtonSize() {
		var width = ImGui.GetWindowSize().X * (1 - SideRatio);
		var widthVec2 = new Vector2(width, width);
		return Vector2.Min(MaxButtonSize, widthVec2 / 8f);
	}
	
	// Icon params

	private readonly static CustomizeIndex[] FeatIconParams = [
		CustomizeIndex.FaceType,
		CustomizeIndex.HairStyle,
		CustomizeIndex.Facepaint,
		CustomizeIndex.RaceFeatureType
	];

	private void DrawFeatIconParams(MakeTypeRace data) {
		var style = ImGui.GetStyle();
		ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X / 2 - this.ButtonSize.X - (style.FramePadding.X + style.ItemSpacing.X) * 2);
		try {
			var i = 0;
			var isSameLine = false;
			foreach (var feat in FeatIconParams) {
				if (!this.DrawFeatIconParams(data, feat)) continue;
				isSameLine = ++i % 2 != 0;
				if (isSameLine) ImGui.SameLine();
			}
			if (isSameLine) ImGui.Dummy(Vector2.Zero);
		} finally {
			ImGui.PopItemWidth();
		}
	}

	private bool DrawFeatIconParams(MakeTypeRace data, CustomizeIndex index) {
		var feat = data.GetFeature(index);
		if (feat == null) return false;
		
		var baseValue = this.Editor.GetCustomization(index);

		var active = feat.Params.FirstOrDefault(param => param.Value == baseValue);
		if (this.DrawFeatIconButton($"{baseValue}", active))
			this._selectPopup.Open(feat);
		
		var btnHeight = ImGui.GetItemRectSize().Y;

		ImGui.SameLine();
		using var _group = ImRaii.Group();
		
		var padHeight = btnHeight / 2 - (ImGui.GetFrameHeightWithSpacing() + UiBuilder.IconFont.FontSize);
		ImGui.Dummy(Vector2.Zero with { Y = padHeight });
		
		ImGui.Text(feat.Name);

		var intValue = (int)baseValue;
		if (ImGui.InputInt($"##Input_{feat.Index}", ref intValue)) {
			var valid = index != CustomizeIndex.FaceType || feat.Params.Any(p => p.Value == intValue);
			if (valid)
				this.Editor.SetCustomization(index, (byte)intValue);
		}

		return true;
	}
	
	private bool DrawFeatIconButton(string fallback, MakeTypeParam? param) {
		using var _col = ImRaii.PushColor(ImGuiCol.Button, 0);
		
		var icon = param != null ? this._tex.GetIcon(param.Graphic) : null;

		bool clicked;
		if (icon != null)
			clicked = ImGui.ImageButton(icon.ImGuiHandle, this.ButtonSize);
		else
			clicked = ImGui.Button(fallback, this.ButtonSize + ImGui.GetStyle().FramePadding * 2);
		return clicked;
	}
	
	// Facial features
	
	private void DrawFacialFeatures(MakeTypeRace data) {
		var current = this.Editor.GetCustomization(CustomizeIndex.FaceFeatures);
		
		this.DrawFacialFeaturesGroup(data, current);
		
		var style = ImGui.GetStyle();
		
		ImGui.SameLine(0, style.ItemInnerSpacing.X);
		
		var inputHasRoom =ImGui.GetContentRegionAvail().X > ImGui.GetFrameHeightWithSpacing() * 3;
		if (inputHasRoom) ImGui.BeginGroup();
		try {
			ImGui.Dummy(new Vector2(0, (this.ButtonSize.Y + style.FramePadding.Y * 2 - ImGui.GetFrameHeight()) / 2 - style.ItemSpacing.Y));
			ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - style.ItemInnerSpacing.X * 2);
			
			var intValue = (int)current;
			if (ImGui.InputInt("##FaceFeatureFlags", ref intValue))
				this.Editor.SetCustomization(CustomizeIndex.FaceFeatures, (byte)intValue);
		} finally {
			if (inputHasRoom) ImGui.EndGroup();
		}
	}

	private void DrawFacialFeaturesGroup(MakeTypeRace data, byte current) {
		using var _group = ImRaii.Group();
		var style = ImGui.GetStyle();
		
		var faceId = this.Editor.GetCustomization(CustomizeIndex.FaceType);
		if (!data.FaceFeatureIcons.TryGetValue(faceId, out var iconIds))
			iconIds = data.FaceFeatureIcons.Values.FirstOrDefault();
		iconIds ??= Array.Empty<uint>();

		var icons = iconIds.Select(id => this._tex.GetIcon(id))
			.Append(this._tex.GetTextureFromGame(LegacyTexPath));
		
		var i = 0;
		foreach (var icon in icons) {
			if (i++ % 4 != 0)
				ImGui.SameLine(0, style.ItemInnerSpacing.X);

			var flag = (byte)Math.Pow(2, i - 1);
			var isActive = (current & flag) != 0;

			using var _col = ImRaii.PushColor(ImGuiCol.Button, isActive ? ImGui.GetColorU32(ImGuiCol.ButtonActive) : 0);

			bool button;
			if (icon != null)
				button = ImGui.ImageButton(icon.ImGuiHandle, this.ButtonSize);
			else
				button = ImGui.Button($"{i}", this.ButtonSize + style.FramePadding * 2);
			if (button)
				this.Editor.SetCustomization(CustomizeIndex.FaceFeatures, (byte)(current ^ flag));
		}
	}
	
	// Colors

	private void DrawEyeColorSwitch(MakeTypeRace data) {
		
	}
}
