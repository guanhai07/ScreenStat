using System.Windows;
using ScreenStat.App.Resources;

namespace ScreenStat.App.Services;

public sealed class ClipboardService
{
    public void SetText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Exception? last = null;
        for (var i = 0; i < 5; i++)
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                Thread.Sleep(30);
            }
        }

        throw new InvalidOperationException(Strings.ClipboardFailed, last);
    }
}

