namespace FunctionalSpecification._06_DriverCustomizableWithExternalizedContextManagementAndRegistrationList;

public class Disposables
{
  private readonly List<IDisposable> _disposables = new();
  private readonly List<IAsyncDisposable> _asyncDisposables = new();

  public void AddDisposable(IDisposable disposable)
  {
    _disposables.Add(disposable);
  }

  public void AddAsyncDisposable(IAsyncDisposable asyncDisposable)
  {
    _asyncDisposables.Add(asyncDisposable);
  }

  public async Task DisposeAsync()
  {
    foreach (var disposable in _disposables)
    {
      // in real-life scenario, wrapping the line below in a try-catch
      // would help ensure all disposables are disposed of.
      disposable.Dispose();
    }

    // in real-life scenario, wrapping the line below in a try-catch
    // would help ensure all disposables are disposed of.
    await Task.WhenAll(_asyncDisposables.Select(d => d.DisposeAsync().AsTask()));
  }
}