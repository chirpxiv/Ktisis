using System.Numerics;

using ImGuiNET;

using Ktisis.ImGuizmo;

namespace Ktisis.Interface.Overlay;

public enum GizmoId : int {
	Default = -1,
	OverlayMain,
	TransformEditor
}

public interface IGizmo {
	public GizmoId Id { get; }
	
	public float ScaleFactor { get; set; }
	
	public Mode Mode { get; set; }
	public Operation Operation { get; set; }

	public bool AllowAxisFlip { get; set; }

	public bool IsEnded { get; }

	public void SetMatrix(Matrix4x4 view, Matrix4x4 proj);

	public void BeginFrame(Vector2 pos, Vector2 size);
	public void PushDrawList();

	public bool Manipulate(ref Matrix4x4 mx, out Matrix4x4 delta);

	public void EndFrame();
}

public class Gizmo : IGizmo {
	public GizmoId Id { get; }
	
	public Gizmo(
		GizmoId id
	) {
		this.Id = id;
	}
	
	// Proeprties

	public float ScaleFactor { get; set; } = 0.1f;
	
	// State

	private bool IsUsedPrev;

	private bool HasDrawn;
	private bool HasMoved;

	private Matrix4x4 ViewMatrix = Matrix4x4.Identity;
	private Matrix4x4 ProjMatrix = Matrix4x4.Identity;

	private Matrix4x4 ResultMatrix = Matrix4x4.Identity;
	private Matrix4x4 DeltaMatrix = Matrix4x4.Identity;

	public Mode Mode { get; set; } = Mode.Local;
	public Operation Operation { get; set; } = Operation.UNIVERSAL;

	public bool AllowAxisFlip { get; set; } = true;

	public bool IsEnded { get; private set; } = false;
	
	// Draw

	public void SetMatrix(Matrix4x4 view, Matrix4x4 proj) {
		this.ViewMatrix = view;
		this.ProjMatrix = proj;
	}

	public void BeginFrame(Vector2 pos, Vector2 size) {
		this.HasDrawn = false;
		this.HasMoved = false;

		ImGuizmo.Gizmo.SetDrawRect(pos.X, pos.Y, size.X, size.Y);

		ImGuizmo.Gizmo.ID = (int)this.Id;
		ImGuizmo.Gizmo.GizmoScale = this.ScaleFactor;
		ImGuizmo.Gizmo.AllowAxisFlip = this.AllowAxisFlip;
		ImGuizmo.Gizmo.BeginFrame();

		this.IsUsedPrev = ImGuizmo.Gizmo.IsUsing;
	}

	public unsafe void PushDrawList() {
		ImGuizmo.Gizmo.DrawList = (nint)ImGui.GetWindowDrawList().NativePtr;
	}

	public bool Manipulate(ref Matrix4x4 mx, out Matrix4x4 delta) {
		delta = Matrix4x4.Identity;

		if (this.HasDrawn) return false;

		var result = ImGuizmo.Gizmo.Manipulate(
			this.ViewMatrix,
			this.ProjMatrix,
			this.Operation,
			this.Mode,
			ref mx,
			out delta
		);

		this.HasDrawn = true;
		return this.HasMoved = result;
	}

	public void EndFrame() {
		this.IsEnded = !ImGuizmo.Gizmo.IsUsing && this.IsUsedPrev;
	}
}
