namespace MessagingPoc.Api.Modules.Scheduling.Contracts;

/// <summary>
/// Scheduling's published-language integration event — thin, id-only (NFR-5 extends to messages).
/// Travels over the Azure Service Bus <c>dose-events</c> queue to the Adherence module.
/// </summary>
public sealed record DoseRecorded(Guid DoseRecordId, Guid ProfileId);
