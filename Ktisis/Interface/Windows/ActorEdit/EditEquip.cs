﻿using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

using ImGuiNET;
using ImGuiScene;

using Dalamud.Interface;

using Ktisis.GameData;
using Ktisis.GameData.Excel;
using Ktisis.Structs.Actor;
using Ktisis.Util;

namespace Ktisis.Interface.Windows.ActorEdit {
	public static class EditEquip {
		// Constants

		public const int _IconSize = 36;
		public static Vector2 IconSize = new(_IconSize, _IconSize);

		// Properties

		public unsafe static Actor* Target => EditActor.Target;

		public static IEnumerable<Item>? Items;

		public static Dictionary<EquipSlot, ItemCache> Equipped = new();

		public static EquipSlot? SlotSelect;
		public static IEnumerable<Item>? SlotItems;
		private static string ItemSearch = "";
		private static string SetSearch = "";
		private static string DyeSearch = "";
		private static bool DrawSetSelection = false;
		private static bool DrawSetDyeSelection = false;
		private static EquipmentSets? Sets = null;

		private static EquipSlot? SlotSelectDye;
		public static readonly IEnumerable<Dye> Dyes = Sheets.GetSheet<Dye>()
			.Where(i => i.IsValid())
			.OrderBy(i => i.Shade).ThenBy(i => i.SubOrder);

		public static Item? FindItem(object item, EquipSlot slot)
			=> Items?.FirstOrDefault(i => (item is WeaponEquip ? i.IsWeapon() : i.IsEquippable(slot)) && i.IsEquipItem(item), null!);

		public static EquipIndex SlotToIndex(EquipSlot slot) => (EquipIndex)(slot - ((int)slot >= 5 ? 3 : 2));

		// UI Code

		public unsafe static void Draw() {
			if (Items == null)
				Items = Sheets.GetSheet<Item>().Where(i => i.IsEquippable());

			DrawControls();

			ImGui.BeginGroup();
			for (var i = 2; i < 13; i++) {
				var slot = (EquipSlot)i;
				if (slot == EquipSlot.Waist) continue;
				if (i == 8) {
					ImGui.EndGroup();
					ImGui.SameLine();
					ImGui.BeginGroup();
					DrawSelector(EquipSlot.OffHand);
				}
				if (i == 2) DrawSelector(EquipSlot.MainHand);
				DrawSelector(slot);
			}
			ImGui.EndGroup();

			ImGui.EndTabItem();
		}

		public unsafe static void DrawSelector(EquipSlot slot) {
			var tar = EditActor.Target;
			var isWeapon = slot == EquipSlot.MainHand || slot == EquipSlot.OffHand;
			var index = isWeapon ? SlotToIndex(slot) : 0;

			object equipObj;
			if (isWeapon)
				equipObj = slot == EquipSlot.MainHand ? tar->MainHand.Equip : tar->OffHand.Equip;
			else
				equipObj = (ItemEquip)tar->Equipment.Slots[(int)SlotToIndex(slot)];

			if (!Equipped.ContainsKey(slot)) {
				Equipped.Add(slot, new() {
					Equip = equipObj,
					Item = FindItem(equipObj, slot)
				});
			} else if (!Equipped[slot].Equip!.Equals(equipObj)) {
				Equipped[slot].Equip = equipObj;
				Equipped[slot].Item = FindItem(equipObj, slot);
				Equipped[slot].Icon = null;
			}

			var item = Equipped[slot];
			if (item.Icon == null)
				item.Icon = Dalamud.DataManager.GetImGuiTextureIcon(item.Item == null ? (uint)0 : item.Item.Icon);

			if (ImGui.ImageButton(item.Icon!.ImGuiHandle, IconSize) && SlotSelect == null)
				OpenSelector(slot);

			if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
				if (isWeapon)
					tar->Equip((int)slot, new WeaponEquip() { Dye = ((WeaponEquip)equipObj).Dye });
				else
					tar->Equip(SlotToIndex(slot), new ItemEquip() { Dye = ((ItemEquip)equipObj).Dye });
			}

			ImGui.SameLine();
			ImGui.BeginGroup();

			var name = item.Item == null ? "Unknown" : item.Item.Name;
			ImGui.Text(name);

			ImGui.PushItemWidth(120);
			if (isWeapon) {
				var equip = (WeaponEquip)equipObj;
				var val = new int[3] { equip.Set, equip.Base, equip.Variant };
				if (ImGui.InputInt3($"##{slot}", ref val[0])) {
					equip.Set = (ushort)val[0];
					equip.Base = (ushort)val[1];
					equip.Variant = (ushort)val[2];
					tar->Equip((int)slot, equip);
				}
			} else {
				var equip = (ItemEquip)equipObj;
				var val = new int[2] { equip.Id, equip.Variant };
				if (ImGui.InputInt2($"##{slot}", ref val[0])) {
					equip.Id = (ushort)val[0];
					equip.Variant = (byte)val[1];
					tar->Equip(index, equip);
				}
			}
			ImGui.PopItemWidth();
			ImGui.SameLine();

			var dye = Dyes.FirstOrDefault(i => i.RowId == (isWeapon ? ((WeaponEquip)equipObj).Dye : ((ItemEquip)equipObj).Dye))!;
			if (ImGui.ColorButton($"{dye.Name} [{dye.RowId}]##{slot}", dye.ColorVector4, ImGuiColorEditFlags.NoBorder))
				OpenDyePicker(slot);

			ImGui.EndGroup();

			if (SlotSelect == slot)
				DrawSelectorList(slot, equipObj);
			if (SlotSelectDye == slot)
				DrawDyePicker(slot, equipObj);
		}

		public static void OpenSelector(EquipSlot slot) {
			SlotSelect = slot;
			SlotItems = Items!.Where(i => i.IsEquippable(slot));
		}
		public static void CloseSelector() {
			SlotSelect = null;
			SlotItems = null;
		}

		public static void OpenDyePicker(EquipSlot slot) =>	SlotSelectDye = slot;
		public static void CloseDyePicker() =>	SlotSelectDye = null;

		public static void OpenSetSelector() => DrawSetSelection = true;
		public static void CloseSetSelector() => DrawSetSelection = false;
		public static void OpenSetDyePicker() => DrawSetDyeSelection = true;
		public static void CloseSetDyePicker() => DrawSetDyeSelection = false;


		public static void DrawControls()
		{
			Sets = new EquipmentSets(Items!);

			if (GuiHelpers.IconButtonTooltip(FontAwesomeIcon.Tshirt, "Look up for a set."))
				OpenSetSelector();
			ImGui.SameLine();
			if (GuiHelpers.IconButtonTooltip(FontAwesomeIcon.PaintRoller, "Dye them all."))
				OpenSetDyePicker();

			if (DrawSetSelection)
				DrawSetSelectorList();
			if (DrawSetDyeSelection)
				DrawSetDyePicker();

			ImGui.Separator();
		}

		public unsafe static void DrawSelectorList(EquipSlot slot, object equipObj)
		{
			if (SlotItems == null)
				return;

			GuiHelpers.HoverPopupWindow(
				GuiHelpers.HoverPopupWindowFlags.SelectorList | GuiHelpers.HoverPopupWindowFlags.SearchBar,
				SlotItems,
				(e, input) => e.Where(i => i.Name.Contains(input, StringComparison.OrdinalIgnoreCase)),
				(i) => { },
				(i, a) => (  // draw Line
						ImGui.Selectable(i.Name, a),
						ImGui.IsItemFocused()
				),
				(i) => { // on Select
					if (equipObj is ItemEquip item) {
						item.Id = i.Model.Id;
						item.Variant = (byte)i.Model.Variant;
						Target->Equip(SlotToIndex(slot), item);
					} else if (equipObj is WeaponEquip wep) {
						if (slot == EquipSlot.MainHand) {
							wep.Set = i.Model.Id;
							wep.Base = i.Model.Base;
							wep.Variant = i.Model.Variant;
							Target->Equip(0, wep);
						}

						if (i.SubModel.Id != 0) {
							wep.Set = i.SubModel.Id;
							wep.Base = i.SubModel.Base;
							wep.Variant = i.SubModel.Variant;
							Target->Equip(1, wep);
						}
					}
				},
				CloseSelector,
				ref ItemSearch,
				"Item Select",
				"##equip_items",
				"##equip_search"
			);
		}

		public unsafe static void DrawSetSelectorList()
		{
			if (Sets?.LoadSources() == null)
				Sets = EquipmentSets.InitAndLoadSources(Items!);

			IEnumerable<EquipmentSet> sets = Sets.GetSets();

			if (!sets.Any())
				return;

			GuiHelpers.HoverPopupWindow(
				GuiHelpers.HoverPopupWindowFlags.SelectorList | GuiHelpers.HoverPopupWindowFlags.SearchBar,
				sets.Cast<dynamic>(),
				(e,input) => e.Where(i => i.Name.Contains(input, StringComparison.OrdinalIgnoreCase)),
				(i) => { },
				(i,a) => {
					bool selected = ImGui.Selectable(i.Name, a);
					bool focus = ImGui.IsItemFocused();

					ImGui.SameLine();
					ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
					GuiHelpers.TextCentered($"{i.Source}");
					ImGui.PopStyleVar();

					return (selected, focus);
				}, // draw Before Line
				(i) => Target->Equip(Sets.GetItems(i)), // on Select
				CloseSetSelector, // on close
				ref SetSearch,
				"Set Select",
				"##equip_sets",
				"##set_search"
			);
		}

		private static int DyeLastSubOrder = -1;
		private const int DyePickerWidth = 485;
		public static unsafe void DrawDyePicker(EquipSlot slot, object equipObj)
		{
			GuiHelpers.HoverPopupWindow(
				GuiHelpers.HoverPopupWindowFlags.SearchBar
				| GuiHelpers.HoverPopupWindowFlags.TwoDimenssion
				| GuiHelpers.HoverPopupWindowFlags.Header,
				Dyes,
				(e, input) => e.Where(i => i.Name.Contains(input, StringComparison.OrdinalIgnoreCase)),
				DrawDyePickerHeader,
				DrawDyePickerItem,
				(i) => { // on Select
					if (equipObj is WeaponEquip wep) {
						wep.Dye = (byte)i.RowId;
						Target->Equip((int)slot, wep);
					} else if (equipObj is ItemEquip item) {
						item.Dye = (byte)i.RowId;
						Target->Equip(SlotToIndex(slot), item);
					}
				},
				CloseDyePicker, // on close
				ref DyeSearch,
				$"Dye {slot}",
				"",
				$"##dye_search",
				"Search...", // searchbar hint
				DyePickerWidth, // window width
				12 // number of columns
			);
		}
		public static unsafe void DrawSetDyePicker()
		{
			GuiHelpers.HoverPopupWindow(
				GuiHelpers.HoverPopupWindowFlags.SearchBar
				| GuiHelpers.HoverPopupWindowFlags.TwoDimenssion
				| GuiHelpers.HoverPopupWindowFlags.Header,
				Dyes,
				(e, input) => e.Where(i => i.Name.Contains(input, StringComparison.OrdinalIgnoreCase)),
				DrawDyePickerHeader,
				DrawDyePickerItem,
				(i) => { // on Select
					foreach ((EquipSlot equipSlot, ItemCache itemCache) in Equipped)
					{
						var equip = itemCache.Equip;
						if (equip is WeaponEquip wep) {
							wep.Dye = (byte)i.RowId;
							Target->Equip((int)equipSlot, wep);
						} else if (equip is ItemEquip item) {
							item.Dye = (byte)i.RowId;
							Target->Equip(SlotToIndex(equipSlot), item);
						}
					}
				},
				CloseSetDyePicker, // on close
				ref DyeSearch,
				$"Dye All##dye_all",
				"",
				$"##dye_all_search##dye_all_search",
				"Search...", // searchbar hint
				DyePickerWidth, // window width
				12 // number of columns
			);
		}
		private static (bool, bool) DrawDyePickerItem(dynamic i, bool isActive)
		{
			bool isThisRealNewLine = GuiHelpers.HoverPopupWindowIndexKey % GuiHelpers.HoverPopupWindowColumns == 0;
			bool isThisANewShade = i.SubOrder == 1;

			if (!isThisRealNewLine && isThisANewShade)
			{
				// skip some index key if we don't finish the row
				int howManyMissedButtons = 12 - (DyeLastSubOrder % 12);
				GuiHelpers.HoverPopupWindowIndexKey += howManyMissedButtons;
			} else if (!isThisRealNewLine && !isThisANewShade)
				ImGui.SameLine();
			if (isThisANewShade)
				ImGui.Spacing();

			DyeLastSubOrder = i.SubOrder;

			// as we previously changed the index key, let's calculate calculate isActive again
			isActive = GuiHelpers.HoverPopupWindowIndexKey == GuiHelpers.HoverPopupWindowLastSelectedItemKey;

			if (isActive)
			{
				ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 6f);
				ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 1f);
			}
			var selecting = ImGui.ColorButton($"{i.Name}##{i.RowId}", i.ColorVector4);
			if (isActive)
				ImGui.PopStyleVar(2);

			return (selecting, ImGui.IsItemFocused());
		}
		private static void DrawDyePickerHeader(dynamic i)
		{
			// TODO: configuration to not show this
			var textSize = ImGui.CalcTextSize(i.Name);
			float dyeShowcaseWidth = (DyePickerWidth - textSize.X - (ImGui.GetStyle().ItemSpacing.X * 2)) / 2;
			ImGui.ColorButton($"{i.Name}##{i.RowId}##selected1", i.ColorVector4, ImGuiColorEditFlags.None, new Vector2(dyeShowcaseWidth, textSize.Y));
			ImGui.SameLine();
			ImGui.Text(i.Name);
			ImGui.SameLine();
			ImGui.ColorButton($"{i.Name}##{i.RowId}##selected2", i.ColorVector4, ImGuiColorEditFlags.None, new Vector2(dyeShowcaseWidth, textSize.Y));
		}
	}

	public class ItemCache {
		public object? Equip;
		public Item? Item;
		public TextureWrap? Icon;
	}
}