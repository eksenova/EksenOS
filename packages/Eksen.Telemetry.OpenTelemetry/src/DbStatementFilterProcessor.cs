using System.Diagnostics;
using OpenTelemetry;

namespace Eksen.Telemetry.OpenTelemetry;

/// <summary>
/// Removes database statement text from spans when <see cref="EksenTelemetryOptions.CaptureDbStatements"/> is off.
/// Covers both the EF Core and the Npgsql instrumentation, which have no switch of their own for it.
/// </summary>
public sealed class DbStatementFilterProcessor : BaseProcessor<Activity>
{
    public static readonly IReadOnlyList<string> StatementTags = ["db.query.text", "db.statement"];

    public override void OnEnd(Activity data)
    {
        foreach (var tag in StatementTags)
        {
            if (data.GetTagItem(tag) is not null)
            {
                data.SetTag(tag, null);
            }
        }
    }
}
