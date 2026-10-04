namespace CNC.App.Services;

/// <summary>Application-level actions on the main window and process lifetime.</summary>
public interface IShellService
{
    void RequestShutdown();
}
