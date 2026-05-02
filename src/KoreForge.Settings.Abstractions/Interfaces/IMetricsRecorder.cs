namespace KoreForge.Settings.Interfaces;

public interface IMetricsRecorder
{
    void Increment(string name, long value = 1);
    void SetGauge(string name, double value);
    IDisposable Time(string name);
}
