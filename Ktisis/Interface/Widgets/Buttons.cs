using System.Numerics;

using Dalamud.Interface;

using ImGuiNET;

namespace Ktisis.Interface.Widgets; 

internal static class Buttons {
	internal static bool DrawIconButton(FontAwesomeIcon icon, Vector2? size = null) {
		var font = UiBuilder.IconFont;

		if (size == null) {
			var newSize = font.FontSize + ImGui.GetStyle().ItemInnerSpacing.X * 2;
			size = new Vector2(newSize, newSize);
		}

		ImGui.PushFont(font);
		var result = ImGui.Button(icon.ToIconString(), size.Value);
		ImGui.PopFont();
		
		return result;
	}
}