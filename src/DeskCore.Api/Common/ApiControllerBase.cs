using DeskCore.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace DeskCore.Api.Common;

/// <summary>
/// Base dos controllers: traduz <see cref="Result"/>/<see cref="Result{T}"/>
/// em respostas HTTP, mapeando o tipo de erro para o status correto.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<T> Resolve<T>(Result<T> result) =>
        result.Succeeded ? Ok(result.Value) : ProblemFrom(result.Error!);

    protected IActionResult Resolve(Result result) =>
        result.Succeeded ? NoContent() : ProblemFrom(result.Error!);

    protected ActionResult ProblemFrom(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return Problem(detail: error.Message, statusCode: status);
    }
}
