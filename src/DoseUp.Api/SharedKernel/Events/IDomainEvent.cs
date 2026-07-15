namespace DoseUp.Api.SharedKernel.Events;

/// <summary>
/// Marker for domain events — facts raised by aggregates at the point of state change,
/// dispatched synchronously inside the unit of work (ADR-0002 § Events). Named in past
/// tense, plain name in Domain (e.g. <c>DoseLogged</c>).
/// </summary>
public interface IDomainEvent;
