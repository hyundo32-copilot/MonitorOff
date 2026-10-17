namespace MonitorOff;

internal static class Program
{
    private const string MutexName = @"Local\MonitorOff.SingleInstance";

    [STAThread]
    private static void Main()
    {
        // 이미 실행 중이면 트레이 아이콘이 중복으로 쌓이지 않도록 종료한다.
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
