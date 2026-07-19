using DoseUp.Api.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace DoseUp.Api.Platform.ErrorHandling;

/// <summary>
/// The single ApiResult→ProblemDetails mapper (domain-rules.md §1/§7): the only producer of
/// ApiResult-derived error responses — endpoints send its output, nothing else crafts error
/// bodies. Statuses follow the conventions matrix; the two 409 classes stay
/// distinguishable by ProblemDetails <c>type</c>. Unset <c>Type</c>/<c>Detail</c> fields
/// are filled with RFC-9457 defaults by the ProblemDetails service at write time.
/// </summary>
public static class ApiResultProblemDetailsMapper {
  public const string RULE_VIOLATION_TYPE = "https://doseup.app/problems/rule-violation";
  public const string CONFLICT_TYPE = "https://doseup.app/problems/conflict";

  public static ProblemDetails ToProblemDetails(this ApiResult result) => result switch {
    ApiResult.Success => throw new ArgumentException("Success carries no error to map — map failure results only.", nameof(result)),
    ApiResult.Validation validation => new HttpValidationProblemDetails(validation.Errors) {
      Status = StatusCodes.Status400BadRequest,
    },
    ApiResult.NotFound => new ProblemDetails {
      Status = StatusCodes.Status404NotFound,
      Title = "Not found.",
    },
    ApiResult.RuleViolations ruleViolations => new ProblemDetails {
      Status = StatusCodes.Status409Conflict,
      Type = RULE_VIOLATION_TYPE,
      Title = "One or more domain rules were violated.",
      Extensions = { ["violations"] = ruleViolations.Violations },
    },
    ApiResult.Conflict => new ProblemDetails {
      Status = StatusCodes.Status409Conflict,
      Type = CONFLICT_TYPE,
      Title = "The request conflicts with the current state of the resource.",
    },
    ApiResult.Forbidden => new ProblemDetails {
      Status = StatusCodes.Status403Forbidden,
      Title = "Forbidden.",
    },
    ApiResult.Unexpected => new ProblemDetails {
      Status = StatusCodes.Status500InternalServerError,
      Title = "An unexpected error occurred.",
    },
  };
}