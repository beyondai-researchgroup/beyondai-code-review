namespace CodeReviewAI.Api.Services;

/// <summary>
/// Best-effort remote control of the local EEG data-collection console app
/// (<c>EmotivEEG_DataCollecting</c>) used during the study. Calls are no-ops when
/// <c>Eeg:Enabled</c> is false (the default outside local dev) and never throw —
/// a missing/offline EEG app must never block a participant's study flow.
/// </summary>
public interface IEegControlService
{
    /// <summary>Starts (or restarts) recording, tagging subsequent rows with the participant id.</summary>
    Task StartAsync(string participantId, CancellationToken ct);

    /// <summary>Resumes recording after a pause, without changing the participant id.</summary>
    Task ResumeAsync(CancellationToken ct);

    /// <summary>Pauses recording without stopping the app.</summary>
    Task PauseAsync(CancellationToken ct);

    /// <summary>Writes a marker row tagged with the given phase code (e.g. "AI_START", "DECISION").</summary>
    Task MarkerAsync(string code, CancellationToken ct);

    /// <summary>Stops recording and lets the EEG app finalize its CSV.</summary>
    Task StopAsync(CancellationToken ct);
}
