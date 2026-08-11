namespace Aprillz.MewUI.Windowless.Sample;

internal interface IHotkeyProvider : IDisposable
{
    string Name { get; }

    void Start(Action activated);
}
