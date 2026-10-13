using System.Diagnostics;
using System.Text.Json;
using Aim.Application;
using Aim.Domain;

namespace Aim.Infrastructure;

public sealed class NodeExtractor(string root, string script, string nodeExecutable) : IExtractor
{
    public async Task<FileSnapshot> ExtractAsync(string path, CancellationToken cancellationToken = default)
    {
        path = SourcePath.Validate(root, path);
        var start = new ProcessStartInfo(nodeExecutable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(script);
        start.ArgumentList.Add(root);
        start.ArgumentList.Add(path);
        // Do not inherit Node loader hooks or credentials into the parser process.
        start.Environment.Clear();
        foreach (var key in new[] { "SystemRoot", "TEMP", "TMP", "PATH" })
            if (Environment.GetEnvironmentVariable(key) is { } value) start.Environment[key] = value;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Não foi possível iniciar o extrator.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(true);
            cancellationToken.ThrowIfCancellationRequested();
            throw new InputException("O extrator excedeu o tempo de 10 segundos.");
        }
        var output = await stdout;
        _ = await stderr; // Never log repository content or process stderr.
        if (process.ExitCode != 0)
            return new(path, [], [], ["Análise indisponível: verifique a sintaxe, os limites e as permissões da fonte. Relações anteriores foram invalidadas."]);
        return JsonSerializer.Deserialize<FileSnapshot>(output, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Resposta vazia do extrator.");
    }
}
