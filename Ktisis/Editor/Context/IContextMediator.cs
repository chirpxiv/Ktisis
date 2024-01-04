using Ktisis.Data.Config;
using Ktisis.Localization;

namespace Ktisis.Editor.Context;

public interface IContextMediator {
	public IEditorContext Context { get; }
	
	public Configuration Config { get; }
	public LocaleManager Locale { get; }
	
	public bool IsGPosing { get; }

	public void Initialize(IEditorContext context);
	public void Destroy();
}
