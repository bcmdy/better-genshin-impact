using BetterGenshinImpact.Model;
using System.Threading;
using System.Collections.Generic;

namespace BetterGenshinImpact.Core.Script;

public class CancellationContext : Singleton<CancellationContext>
{
    public CancellationTokenSource Cts;
    private List<CancellationTokenSource> _externalCtsList;

    public CancellationToken Token => Cts.Token;
    public bool IsManualStop { get; private set; }

    private bool disposed;

    public CancellationContext()
    {
        Cts = new CancellationTokenSource();
        _externalCtsList = new List<CancellationTokenSource>();
        IsManualStop = false;
        disposed = false;
    }

    public void Set()
    {
        Cts = new CancellationTokenSource();
        _externalCtsList.Clear();
        IsManualStop = false;
        disposed = false;
    }

    public CancellationToken Register(CancellationToken externalToken)
    {
        if (!disposed)
        {
            var externalCts = CancellationTokenSource.CreateLinkedTokenSource(Cts.Token, externalToken);
            _externalCtsList.Add(externalCts);
            return externalCts.Token;
        }
        return CancellationToken.None;
    }

    public void ManualCancel()
    {
        if (!disposed)
        {
            IsManualStop = true;
            Cts.Cancel();

            foreach (var externalCts in _externalCtsList)
            {
                externalCts.Cancel();
                externalCts.Dispose();
            }

            _externalCtsList.Clear();
        }
    }

    public void Cancel()
    {
        if (!disposed)
        {
            Cts.Cancel();
        }
    }

    public void Clear()
    {
        Cts.Dispose();
        foreach (var externalCts in _externalCtsList)
        {
            externalCts.Dispose();
        }
        _externalCtsList.Clear();
        disposed = true;
    }
}
