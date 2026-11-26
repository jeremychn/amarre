using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Remotely.Server.Services;
using Remotely.Shared;
using Remotely.Shared.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Remotely.Server.Auth;

public class ExpiringTokenFilter : ActionFilterAttribute, IAsyncAuthorizationFilter
{
    private const string ScopeKey = nameof(ExpiringTokenScope);

    /// <summary>
    /// Portée du jeton d'agent de la requête ; faux pour un utilisateur connecté ou une clé d'API.
    /// </summary>
    public static bool TryGetScope(HttpContext httpContext, [NotNullWhen(true)] out ExpiringTokenScope? scope)
    {
        scope = httpContext.Items.TryGetValue(ScopeKey, out var value) ? value as ExpiringTokenScope : null;
        return scope is not null;
    }

    private readonly IDataService _dataService;
    private readonly IExpiringTokenService _expiringTokenService;
    private readonly ILogger<ExpiringTokenFilter> _logger;

    public ExpiringTokenFilter(
        IExpiringTokenService expiringTokenService,
        IDataService dataService,
        ILogger<ExpiringTokenFilter> logger)
    {
        _dataService = dataService;
        _expiringTokenService = expiringTokenService;
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        try
        {
            await Authorize(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while authorizing expiring token.");
            context.Result = new UnauthorizedResult();
        }
    }

    private async Task Authorize(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        http.Request.Headers["OrganizationID"] = string.Empty;

        // Utilisateur connecté : administrateurs seulement, comme une clé d'API. Ces actions servent aux
        // agents (script, fichiers, résultat) ; un non-admin y écrirait des résultats pour des postes
        // hors de ses groupes ou lirait les scripts privés des autres.
        if (http.User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{http.User.Identity.Name}");
            if (!userResult.IsSuccess || !userResult.Value.IsAdministrator)
            {
                http.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                context.Result = new UnauthorizedResult();
                return;
            }

            http.Request.Headers["OrganizationID"] = userResult.Value.OrganizationID;
            return;
        }

        if (http.Request.Headers.TryGetValue(AppConstants.ApiKeyHeaderName, out var apiHeaderValue))
        {
            var headerComponents = apiHeaderValue.ToString().Split(":");
            if (headerComponents.Length < 2)
            {
                http.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                context.Result = new UnauthorizedResult();
                return;
            };

            var keyId = headerComponents[0].Trim();
            var secret = headerComponents[1].Trim();

            var isValid = await _dataService.ValidateApiKey(
                       keyId,
                       secret,
                       http.Request.Path,
                       $"{http.Connection.RemoteIpAddress}");

            if (isValid)
            {
                var keyResult = await _dataService.GetApiKey(keyId);

                if (keyResult.IsSuccess)
                {
                    _logger.LogDebug("Expiring token authorized via API key.  Key ID: {keyId}.", keyId);
                    http.Request.Headers["OrganizationID"] = keyResult.Value.OrganizationID;
                    return;
                }
            }
        }

        if (http.Request.Headers.TryGetValue(AppConstants.ExpiringTokenHeaderName, out var expiringToken))
        {
            if (_expiringTokenService.TryGetToken(expiringToken.ToString(), out var scope) &&
                scope.Expiration > Time.Now &&
                !string.IsNullOrWhiteSpace(scope.OrganizationId))
            {
                _logger.LogDebug("Expiring token authorized.  Expiration: {expiration}", scope.Expiration);
                http.Request.Headers["OrganizationID"] = scope.OrganizationId;
                // Chaque action vérifie que le jeton vaut pour ce qu'elle sert (script, fichier, résultat).
                http.Items[ScopeKey] = scope;
                return;
            }
        }

        _logger.LogDebug("Expiring token not authorization failed.");
        context.Result = new UnauthorizedResult();
    }
}
