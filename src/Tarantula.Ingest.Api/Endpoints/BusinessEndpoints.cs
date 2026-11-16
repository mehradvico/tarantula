using Tarantula.Application.Abstractions;
using Tarantula.Application.Business;
using Tarantula.Application.Common;
using Tarantula.Ingest.Api.Contracts;

namespace Tarantula.Ingest.Api.Endpoints;

/// <summary>
/// What the product's business data says (Users, Vendors, sales). Read-only and aggregate: the database behind it
/// exposes no personal data. Protected by the same secret read key as the live endpoints.
/// </summary>
internal static class BusinessEndpoints
{
    public static IEndpointRouteBuilder MapBusiness(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/business/overview", OverviewAsync).RequireCors(Program.ReadCorsPolicy).RequireRateLimiting(Program.ReadRateLimitPolicy);
        app.MapGet("/v1/business/users", UsersAsync).RequireCors(Program.ReadCorsPolicy).RequireRateLimiting(Program.ReadRateLimitPolicy);
        app.MapGet("/v1/business/vendors", VendorsAsync).RequireCors(Program.ReadCorsPolicy).RequireRateLimiting(Program.ReadRateLimitPolicy);
        return app;
    }

    private static Task<IResult> OverviewAsync(HttpContext http, IQueryHandler<GetBusinessOverviewQuery, BusinessOverview> handler, CancellationToken ct) =>
        RunAsync(http, key => new GetBusinessOverviewQuery(key), handler, r => r, ct);

    private static Task<IResult> UsersAsync(HttpContext http, IQueryHandler<GetUsersReportQuery, UsersReport> handler, CancellationToken ct) =>
        RunAsync(http, key => new GetUsersReportQuery(key), handler, r => r, ct);

    private static async Task<IResult> VendorsAsync(HttpContext http, IQueryHandler<GetVendorsReportQuery, VendorsReport> handler, CancellationToken ct)
    {
        VendorKind? kind = null;
        if (http.Request.Query.TryGetValue("kind", out var raw) && !string.IsNullOrEmpty(raw))
        {
            if (!Enum.TryParse<VendorKind>(raw.ToString(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return Failure(StatusCodes.Status400BadRequest, "invalid_kind", "kind must be one of: store, clinic, groomer, coach, pansion, school.");
            }

            kind = parsed;
        }

        return await RunAsync(http, key => new GetVendorsReportQuery(key, kind), handler, r => r, ct);
    }

    private static async Task<IResult> RunAsync<TQuery, TResult>(
        HttpContext http,
        Func<string, TQuery> makeQuery,
        IQueryHandler<TQuery, TResult> handler,
        Func<TResult, object> shape,
        CancellationToken cancellationToken)
    {
        var key = http.Request.Headers[LiveEndpoints.ReadKeyHeader].FirstOrDefault() ?? http.Request.Query[LiveEndpoints.ReadKeyQuery].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            return Failure(StatusCodes.Status401Unauthorized, "missing_read_key", "Send the read key in the X-Tarantula-Read-Key header.");
        }

        var result = await handler.HandleAsync(makeQuery(key), cancellationToken);
        return result.Error is { } error
            ? Failure(error.Kind switch
            {
                ErrorKind.Validation => StatusCodes.Status400BadRequest,
                ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status500InternalServerError
            }, error.Code, error.Message)
            : Results.Json(shape(result.Value!), CollectEndpoints.Json);
    }

    private static IResult Failure(int status, string code, string message) =>
        Results.Json(new ErrorResponse(code, message), CollectEndpoints.Json, statusCode: status);
}
